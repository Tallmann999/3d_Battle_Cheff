using System;
using System.Collections.Generic;
using System.Linq;
using ChefShow.Cooking;
using ChefShow.Core;
using ChefShow.Data;
using ChefShow.Ingredients;
using ChefShow.Inventory;
using ChefShow.Recipes;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ChefShow.Tests
{
    public sealed class RecipeRecognitionTests
    {
        private const string CatalogPath = "Assets/_ChefShow/Data/Recipes/Recognition/RecipeRecognitionCatalog.asset";
        private const string IngredientPath = "Assets/_ChefShow/Generated/Data/Ingredients/";
        private RecipeRecognitionCatalog catalog;
        private RecipeIdentitySettings rules;
        private CookingSettings cooking;
        private PrototypeRun run;
        private InventoryState state;

        [SetUp] public void Setup()
        {
            catalog = AssetDatabase.LoadAssetAtPath<RecipeRecognitionCatalog>(CatalogPath);
            Assert.That(catalog, Is.Not.Null, "Install the saved recognition catalog before running this suite.");
            Assert.That(catalog.Validate(), Is.Null);
            rules = catalog.Capture();
            var config = AssetDatabase.LoadAssetAtPath<CookingConfig>("Assets/_ChefShow/Generated/Data/CookingConfig.asset");
            Assert.That(config, Is.Not.Null);
            cooking = config.Capture();
            StartRun(rules);
        }

        [TearDown] public void Cleanup() { run?.Dispose(); }

        private void StartRun(RecipeIdentitySettings settings)
        {
            run?.Dispose();
            run = new PrototypeRun(600, 1005, (type, error) => Assert.Fail(error.ToString()),
                cooking: cooking, recognition: settings);
            state = run.Inventory;
        }

        private IngredientDefinition Ingredient(string id)
        {
            var definition = AssetDatabase.LoadAssetAtPath<IngredientDefinition>(IngredientPath + id + ".asset");
            Assert.That(definition, Is.Not.Null, id);
            return definition;
        }

        private FoodPortion Hand(string id)
        {
            Assert.That(state.TryMoveBasket(BasketPlacement.Carried, out var reason), Is.True, reason);
            Assert.That(state.TryCollect(Ingredient(id), out reason), Is.True, reason);
            Assert.That(state.TryMoveBasket(BasketPlacement.Station, out reason), Is.True, reason);
            Assert.That(state.TryUnload(out reason), Is.True, reason);
            // Dose containers returned earlier remain in the tray; collect the newly added portion.
            Assert.That(state.TryTakeTray(state.Tray.Count - 1, out reason), Is.True, reason);
            return state.Held;
        }

        private FoodPortion PreparedHand(string id, bool chopped)
        {
            var food = Hand(id);
            if (!chopped) return food;
            Assert.That(state.TryPlaceSocket((int)StationSocketKind.Board, out var reason), Is.True, reason);
            for (int i = 0; i < InventoryState.RequiredChopPresses; i++)
                Assert.That(state.TryChop(KitchenToolKind.Knife, out reason), Is.True, reason);
            Assert.That(state.TryTakeSocket((int)StationSocketKind.Board, out reason), Is.True, reason);
            Assert.That(food.Preparation, Is.EqualTo(PreparationState.Chopped));
            return food;
        }

        private void Medium(CookerKind kind)
        {
            while (state.Heat(kind) != HeatLevel.Medium)
                Assert.That(state.TryCycleHeat(kind, out var reason), Is.True, reason);
        }

        private FoodPortion CookAndPlate(string id, CookerKind kind, bool chopped = false, bool burned = false)
        {
            var food = PreparedHand(id, chopped);
            Assert.That(state.TryPlaceCooker(kind, out var reason), Is.True, reason);
            Medium(kind);
            if (kind == CookerKind.Pot)
                for (int i = 0; i < cooking.PotStirs; i++)
                    Assert.That(state.TryStir(kind, KitchenToolKind.Spatula, out reason), Is.True, reason);
            if (kind == CookerKind.Oven && state.OvenDoorOpen)
                Assert.That(state.TryToggleOvenDoor(out reason), Is.True, reason);
            float seconds = burned ? (kind == CookerKind.Oven ? cooking.OvenBurned : cooking.Burned) : cooking.ReadyFor(kind);
            run.Tick(seconds / cooking.MediumRate);
            Assert.That(food.Cooking, Is.EqualTo(burned ? CookState.Burned : CookState.Cooked));
            if (kind == CookerKind.Oven)
                Assert.That(state.TryToggleOvenDoor(out reason), Is.True, reason);
            Assert.That(state.TryTakeCooker(kind, 0, out reason), Is.True, reason);
            Assert.That(state.TryPlaceServing(out reason), Is.True, reason);
            return food;
        }

        private FoodPortion AddToBowl(string id, bool chopped = false)
        {
            var food = PreparedHand(id, chopped);
            Assert.That(state.TryPlaceBowl(out var reason), Is.True, reason);
            return food;
        }

        private FoodPortion Mix()
        {
            Assert.That(state.TryAdvanceMix(cooking.MixSeconds, KitchenToolKind.Spoon, Ingredient("mixture"), out var reason), Is.True, reason);
            return state.Bowl.Single();
        }

        private void CookBowlAndPlate(CookerKind kind)
        {
            Assert.That(state.TryTakeBowl(0, out var reason), Is.True, reason);
            Assert.That(state.TryPlaceCooker(kind, out reason), Is.True, reason);
            Medium(kind);
            if (kind == CookerKind.Oven) Assert.That(state.TryToggleOvenDoor(out reason), Is.True, reason);
            run.Tick(cooking.ReadyFor(kind) / cooking.MediumRate);
            if (kind == CookerKind.Oven) Assert.That(state.TryToggleOvenDoor(out reason), Is.True, reason);
            Assert.That(state.TryTakeCooker(kind, 0, out reason), Is.True, reason);
            Assert.That(state.TryPlaceServing(out reason), Is.True, reason);
        }

        [TestCase("carrot")]
        [TestCase("onion")]
        public void VegetablesChooseSpecificSteakCoreWithoutReplacingWholeSteak(string vegetable)
        {
            var beef = CookAndPlate("beef", CookerKind.Pan);
            var steak = state.CaptureDish();
            Assert.That(steak.Recognition.RecipeId, Is.EqualTo("beef_steak"));
            CookAndPlate("potato", CookerKind.Pot, chopped: true);
            CookAndPlate(vegetable, CookerKind.Pan, chopped: true);
            var result = state.CaptureDish();
            Assert.That(result.Recognition.RecipeId, Is.EqualTo("steak_vegetables"));
            Assert.That(result.Portions.First().Id, Is.EqualTo(beef.Id));
            Assert.That(result.Portions.First().Preparation, Is.EqualTo(PreparationState.Whole));
            Assert.That(steak.Recognition.RecipeId, Is.EqualTo("beef_steak"));
            Assert.That(steak.Portions.Count, Is.EqualTo(1));
        }

        [Test] public void OmeletRequiresActuallyMixedEggAndCheeseHeatedTogether()
        {
            CookAndPlate("egg", CookerKind.Pan);
            Hand("cheese"); Assert.That(state.TryPlaceServing(out _), Is.True);
            Assert.That(state.CaptureDish().Recognition.RecipeId, Is.Not.EqualTo("cheese_omelet"));
            StartRun(rules);
            var egg = AddToBowl("egg"); var cheese = AddToBowl("cheese");
            var mixed = Mix(); var beforeHeat = mixed.Snapshot();
            CookBowlAndPlate(CookerKind.Pan);
            var dish = state.CaptureDish();
            Assert.That(dish.Recognition.RecipeId, Is.EqualTo("cheese_omelet"));
            Assert.That(dish.Portions.Single().Id, Is.EqualTo(mixed.Id));
            Assert.That(dish.Portions.Single().Components.Select(c => c.Id), Is.EquivalentTo(new[] { egg.Id, cheese.Id }));
            Assert.That(beforeHeat.Cooking, Is.EqualTo(CookState.Raw));
            Assert.That(beforeHeat.Operations.Any(o => o.Action == "cook_started" && o.SourcePortionId == mixed.Id), Is.False);
            Assert.That(dish.Portions.Single().Operations.Single(o => o.Action == "cook_started" && o.SourcePortionId == mixed.Id).Cooker,
                Is.EqualTo(CookerKind.Pan));
        }

        [Test] public void NestedOvenTartCountsOnlyOriginalLeavesAndKeepsTheirPreparation()
        {
            var flour = AddToBowl("flour"); var egg = AddToBowl("egg"); var dough = Mix();
            var apple = AddToBowl("apple", chopped: true);
            AddToBowl("butter"); AddToBowl("sugar");
            var finalMix = Mix(); var rawSnapshot = finalMix.Snapshot();
            CookBowlAndPlate(CookerKind.Oven);
            var dish = state.CaptureDish();
            Assert.That(dish.Recognition.RecipeId, Is.EqualTo("apple_tart"));
            Assert.That(dish.Quantity, Is.EqualTo(5));
            Assert.That(dish.Recognition.Ingredients.Select(i => i.Id), Is.EqualTo(new[] { "apple", "butter", "egg", "flour", "sugar" }));
            Assert.That(dish.Recognition.Ingredients.All(i => i.Amount == 1), Is.True);
            Assert.That(dish.Recognition.Ingredients.Sum(i => i.Amount), Is.EqualTo(5));
            Assert.That(dish.Recognition.Ingredients.Any(i => i.Id == "mixture"), Is.False);
            Assert.That(rawSnapshot.Components.Single(c => c.Id == dough.Id).Components.Select(c => c.Id),
                Is.EquivalentTo(new[] { flour.Id, egg.Id }));
            Assert.That(rawSnapshot.Components.Single(c => c.Id == apple.Id).ChopPresses, Is.EqualTo(6));
            Assert.That(rawSnapshot.Cooking, Is.EqualTo(CookState.Raw));
            Assert.That(rawSnapshot.Components.Single(c => c.Id == dough.Id).Cooking, Is.EqualTo(CookState.Raw));
        }

        [Test] public void RepeatedQuantitiesAndActualPortionAndPlateDosesDoNotChangeIdentity()
        {
            var first = Hand("egg"); Assert.That(state.TryPlaceCooker(CookerKind.Pan, out _), Is.True);
            Hand("salt"); Assert.That(state.TryApplySeasoning(CookerKind.Pan, 0, out _), Is.True); state.TryPutInTray(out _);
            Medium(CookerKind.Pan); run.Tick(cooking.Ready / cooking.MediumRate);
            state.TryTakeCooker(CookerKind.Pan, 0, out _); state.TryPlaceServing(out _);
            var oneEgg = state.CaptureDish();
            Assert.That(oneEgg.Recognition.RecipeId, Is.EqualTo("fried_egg"));
            Assert.That(first.SaltDoses, Is.EqualTo(cooking.DosesPerPress));
            CookAndPlate("egg", CookerKind.Pan); CookAndPlate("egg", CookerKind.Pan);
            Hand("salt"); for (int i = 0; i < 4; i++) Assert.That(state.TrySeasonPlate(out _), Is.True); state.TryPutInTray(out _);
            Hand("oil"); for (int i = 0; i < 3; i++) Assert.That(state.TrySeasonPlate(out _), Is.True); state.TryPutInTray(out _);
            var threeEggs = state.CaptureDish();
            Assert.That(threeEggs.Recognition.RecipeId, Is.EqualTo(oneEgg.Recognition.RecipeId));
            Assert.That(threeEggs.Recognition.Ingredients.Single().Amount, Is.EqualTo(3));
            Assert.That(threeEggs.SaltDoses, Is.EqualTo(4 * cooking.DosesPerPress));
            Assert.That(threeEggs.OilDoses, Is.EqualTo(3 * cooking.DosesPerPress));
            Assert.That(threeEggs.Portions.Sum(p => p.SaltDoses), Is.EqualTo(cooking.DosesPerPress));
            Assert.That(oneEgg.Recognition.Ingredients.Single().Amount, Is.EqualTo(1));
            Assert.That(oneEgg.SaltDoses, Is.Zero);
        }

        [Test] public void WrongApplianceAndPanThenPotCannotEraseEarlierMethodEvidence()
        {
            CookAndPlate("beef", CookerKind.Pot);
            Assert.That(state.CaptureDish().Recognition.RecipeId, Is.EqualTo("experimental"));
            StartRun(rules);
            var food = Hand("beef"); state.TryPlaceCooker(CookerKind.Pan, out _);
            Medium(CookerKind.Pan); run.Tick(10); var panOnly = food.Snapshot();
            state.TryTakeCooker(CookerKind.Pan, 0, out _); state.TryPlaceCooker(CookerKind.Pot, out _);
            Medium(CookerKind.Pot); run.Tick(cooking.Ready / cooking.MediumRate - 10);
            for (int i = 0; i < cooking.PotStirs; i++) state.TryStir(CookerKind.Pot, KitchenToolKind.Spatula, out _);
            state.TryTakeCooker(CookerKind.Pot, 0, out _); state.TryPlaceServing(out _);
            var dish = state.CaptureDish();
            Assert.That(dish.Recognition.RecipeId, Is.EqualTo("experimental"));
            Assert.That(dish.Portions.Single().Operations.Where(o => o.Action == "cook_started" && o.SourcePortionId == food.Id).Select(o => o.Cooker),
                Is.EqualTo(new CookerKind?[] { CookerKind.Pan, CookerKind.Pot }));
            Assert.That(panOnly.LastCooker, Is.EqualTo(CookerKind.Pan));
            Assert.That(panOnly.Operations.Count(o => o.Action == "cook_started"), Is.EqualTo(1));
        }

        [Test] public void EmptyMissingCoreAndUnavailableProcessesNeverClaimFutureRecipes()
        {
            var empty = state.CaptureDish().Recognition;
            Assert.That(empty.RecipeId, Is.EqualTo("no_dish")); Assert.That(empty.Recognized, Is.False);
            Assert.That(empty.Ingredients, Is.Empty);
            Hand("flour"); state.TryPlaceServing(out _);
            Assert.That(state.CaptureDish().Recognition.RecipeId, Is.EqualTo("experimental"));
            Assert.That(state.CaptureDish().Recognition.Reason, Is.Not.Empty);
            StartRun(rules);
            CookAndPlate("beef", CookerKind.Pan); CookAndPlate("onion", CookerKind.Pan, chopped: true);
            Assert.That(state.CaptureDish().Recognition.RecipeId, Is.Not.EqualTo("braised_beef_onion"));
            StartRun(rules);
            AddToBowl("egg"); AddToBowl("butter"); Mix(); CookBowlAndPlate(CookerKind.Pan);
            Assert.That(state.CaptureDish().Recognition.RecipeId, Is.Not.EqualTo("scrambled_eggs"));
            foreach (string id in new[] { "braised_beef_onion", "scrambled_eggs" })
                Assert.That(rules.Recipes.Single(r => r.Id == id).UnavailableProcess, Is.Not.Empty);
        }

        [Test] public void RecognitionReportsBurnedAndDirtyFoodWithoutRepairingIt()
        {
            var food = CookAndPlate("beef", CookerKind.Pan, burned: true);
            // Hygiene is outside this slice. Seed only contamination, preserving the real heat history.
            typeof(FoodPortion).GetProperty(nameof(FoodPortion.Contaminated)).SetValue(food, true);
            var before = food.Snapshot(); var dish = state.CaptureDish();
            Assert.That(dish.Recognition.RecipeId, Is.EqualTo("beef_steak"));
            Assert.That(dish.Recognition.Issues, Does.Contain("Есть сгоревшие продукты"));
            Assert.That(dish.Recognition.Issues, Does.Contain("Есть загрязнение"));
            Assert.That(food.Cooking, Is.EqualTo(CookState.Burned)); Assert.That(food.Contaminated, Is.True);
            Assert.That(food.HeatProgress, Is.EqualTo(before.HeatProgress));
            Assert.That(food.Operations.Count, Is.EqualTo(before.Operations.Count));
            Assert.That(dish.Portions.Single().Cooking, Is.EqualTo(CookState.Burned));
            Assert.That(dish.Portions.Single().Contaminated, Is.True);
        }

        [Test] public void CapturedRulesAndSubmittedRecognitionRemainImmutableThroughResetResubmitAndTimeup()
        {
            var source = catalog.Recipes.Single(r => r.Reference.Id == "beef_steak");
            var profile = Object.Instantiate(source); var reference = Object.Instantiate(source.Reference);
            try
            {
                profile.Reference = reference;
                var settings = new RecipeIdentitySettings(rules.Recipes.Where(r => r.Id != "beef_steak").Concat(new[] { profile.Capture() }));
                var capturedRule = settings.Recipes.Single(r => r.Id == "beef_steak");
                var name = capturedRule.Name;
                reference.DisplayName = "Changed after Restart";
                profile.Core[0].AllowedCookers[0] = CookerKind.Pot;
                Assert.That(capturedRule.Name, Is.EqualTo(name));
                Assert.That(capturedRule.Core[0].AllowedCookers, Is.EqualTo(new[] { CookerKind.Pan }));
                Assert.Throws<NotSupportedException>(() => ((IList<CookerKind>)capturedRule.Core[0].AllowedCookers)[0] = CookerKind.Pot);
                StartRun(settings); CookAndPlate("beef", CookerKind.Pan);
                Assert.That(state.TrySubmitDish(out _), Is.True); var first = state.SubmittedDish;
                Assert.That(first.Recognition.Name, Is.EqualTo(name));
                Assert.That(state.TryResetSubmission(out _), Is.True);
                CookAndPlate("beef", CookerKind.Pan);
                Assert.That(state.TrySubmitDish(out _), Is.True); var second = state.SubmittedDish;
                Assert.That(first.Recognition.Ingredients.Single().Amount, Is.EqualTo(1));
                Assert.That(second.Recognition.Ingredients.Single().Amount, Is.EqualTo(2));
                Assert.That(second.Recognition.RecipeId, Is.EqualTo("beef_steak"));
                Assert.That(state.TryResetSubmission(out _), Is.True);
                run.SetRemaining(.01f); run.Tick(1);
                Assert.That(state.SubmittedDish.Recognition.RecipeId, Is.EqualTo("beef_steak"));
                Assert.That(state.SubmittedDish.Recognition.Ingredients.Single().Amount, Is.EqualTo(2));
                Assert.That(state.TryResetSubmission(out _), Is.False);
                Assert.That(first.Recognition.Name, Is.EqualTo(name));
            }
            finally { Object.DestroyImmediate(profile); Object.DestroyImmediate(reference); }
        }

        [Test] public void ChoppingAfterHeatAtTheSameSimulationTimeDoesNotSatisfyBeforeHeat()
        {
            CookAndPlate("potato", CookerKind.Pan);
            Assert.That(state.TryTakeServing(0, out _), Is.True);
            Assert.That(state.TryPlaceSocket(0, out _), Is.True);
            for (int i = 0; i < 6; i++) Assert.That(state.TryChop(KitchenToolKind.Knife, out _), Is.True);
            state.TryTakeSocket(0, out _); state.TryPlaceServing(out _);
            var potato = state.Served.Single().Snapshot();
            var heat = potato.Operations.Single(o => o.Action == "cook_started" && o.SourcePortionId == potato.Id);
            var chop = potato.Operations.First(o => o.Preparation == PreparationState.Chopped && o.SourcePortionId == potato.Id);
            Assert.That(chop.SimulationTime, Is.EqualTo(heat.SimulationTime));
            CookAndPlate("onion", CookerKind.Pan, chopped: true);
            var result = state.CaptureDish().Recognition;
            Assert.That(result.RecipeId, Is.EqualTo("experimental"));
            Assert.That(result.SuggestedRecipeId, Is.EqualTo("fried_potatoes"));
            Assert.That(result.Reason, Does.Contain("до нагрева"));
        }

        [Test] public void ColdButterAfterOneValidPotPortionIsNotRawAndDoesNotDependOnPortionOrder()
        {
            var early = Hand("potato"); state.TryPlaceCooker(CookerKind.Pot, out _);
            Medium(CookerKind.Pot); run.Tick(10 / cooking.MediumRate);
            var later = Hand("potato"); state.TryPlaceCooker(CookerKind.Pot, out _);
            for (int i = 0; i < cooking.PotStirs; i++) state.TryStir(CookerKind.Pot, KitchenToolKind.Spatula, out _);
            run.Tick((cooking.Ready - 10) / cooking.MediumRate);
            Assert.That(early.Cooking, Is.EqualTo(CookState.Cooked));
            Assert.That(later.Cooking, Is.EqualTo(CookState.Cooking));
            state.TryTakeCooker(CookerKind.Pot, 0, out _); state.TryPutInTray(out _);
            var butter = Hand("butter"); state.TryPlaceServing(out _);
            run.Tick(10 / cooking.MediumRate);
            state.TryTakeCooker(CookerKind.Pot, 0, out _); state.TryPlaceServing(out _);
            state.TryTakeTray(0, out _); state.TryPlaceServing(out _);
            // The later-ready potato precedes the earlier-ready potato on the plate.
            Assert.That(state.Served.Select(p => p.Id), Is.EqualTo(new[] { butter.Id, later.Id, early.Id }));
            var lateFirst = state.CaptureDish().Recognition;
            Assert.That(lateFirst.RecipeId, Is.EqualTo("boiled_potatoes"));
            Assert.That(lateFirst.Issues, Does.Not.Contain("Есть неготовые продукты"));
            state.TryTakeServing(1, out _); state.TryPlaceServing(out _);
            var earlyFirst = state.CaptureDish().Recognition;
            Assert.That(earlyFirst.RecipeId, Is.EqualTo(lateFirst.RecipeId));
            Assert.That(earlyFirst.Issues, Is.EqualTo(lateFirst.Issues));
            Assert.That(butter.Cooking, Is.EqualTo(CookState.Raw));
            Assert.That(butter.HeatProgress, Is.Zero);
        }

        [Test] public void PanPotatoReadinessCannotSatisfyButterAddedBeforeRequiredPotPotato()
        {
            CookAndPlate("potato", CookerKind.Pan);
            var butter = Hand("butter"); state.TryPlaceServing(out _);
            CookAndPlate("potato", CookerKind.Pot);
            var beforePotReady = state.CaptureDish().Recognition;
            Assert.That(beforePotReady.RecipeId, Is.EqualTo("experimental"));
            Assert.That(beforePotReady.SuggestedRecipeId, Is.EqualTo("boiled_potatoes"));
            Assert.That(beforePotReady.Reason, Does.Contain("после"));
            state.TryTakeServing(1, out _); state.TryPlaceServing(out _);
            var afterPotReady = state.CaptureDish().Recognition;
            Assert.That(afterPotReady.RecipeId, Is.EqualTo("boiled_potatoes"));
            Assert.That(butter.Cooking, Is.EqualTo(CookState.Raw));
            Assert.That(beforePotReady.RecipeId, Is.EqualTo("experimental"));
        }

        [Test] public void ReorderingRepeatedInvalidProductsDoesNotChangeFailureReason()
        {
            CookAndPlate("potato", CookerKind.Pan);
            CookAndPlate("potato", CookerKind.Pot, chopped: true);
            CookAndPlate("onion", CookerKind.Pan, chopped: true);
            var first = state.CaptureDish().Recognition;
            Assert.That(first.RecipeId, Is.EqualTo("experimental"));
            Assert.That(first.SuggestedRecipeId, Is.EqualTo("fried_potatoes"));
            state.TryTakeServing(0, out _); state.TryPlaceServing(out _);
            var second = state.CaptureDish().Recognition;
            Assert.That(second.RecipeId, Is.EqualTo(first.RecipeId));
            Assert.That(second.SuggestedRecipeId, Is.EqualTo(first.SuggestedRecipeId));
            Assert.That(second.Reason, Is.EqualTo(first.Reason));
        }

        [Test] public void EquivalentCoreTieAndIngredientOrderAreDeterministicAcrossCatalogOrder()
        {
            CookAndPlate("egg", CookerKind.Pan); CookAndPlate("beef", CookerKind.Pan); CookAndPlate("egg", CookerKind.Pan);
            var dish = state.CaptureDish();
            var reordered = new RecipeIdentitySettings(rules.Recipes.Reverse());
            var first = rules.Resolve(dish); var second = reordered.Resolve(dish);
            Assert.That(first.RecipeId, Is.EqualTo("beef_steak"));
            Assert.That(second.RecipeId, Is.EqualTo(first.RecipeId));
            Assert.That(first.Ingredients.Select(i => i.Id), Is.EqualTo(new[] { "beef", "egg" }));
            Assert.That(second.Ingredients.Select(i => i.Amount), Is.EqualTo(new long[] { 1, 2 }));
            Assert.That(first.Issues, Does.Contain("Есть добавки вне рецепта"));
            Assert.That(second.Issues, Is.EqualTo(first.Issues));
        }
        [Test] public void ChoppedPotatoAndOnionFriedSeparatelyRecognizeFriedPotatoes()
        {
            var potato = CookAndPlate("potato", CookerKind.Pan, chopped: true);
            var onion = CookAndPlate("onion", CookerKind.Pan, chopped: true);
            var dish = state.CaptureDish();
            Assert.That(dish.Recognition.RecipeId, Is.EqualTo("fried_potatoes"));
            Assert.That(dish.Recognition.Issues, Is.Empty);
            Assert.That(dish.Quantity, Is.EqualTo(2));
            Assert.That(dish.Portions.Select(p => p.Id), Is.EquivalentTo(new[] { potato.Id, onion.Id }));
            Assert.That(dish.Portions.All(p => p.ChopPresses == InventoryState.RequiredChopPresses), Is.True);
            Assert.That(dish.Portions.All(p => p.Cooking == CookState.Cooked && p.LastCooker == CookerKind.Pan), Is.True);
        }

        [Test] public void ChoppedPotatoEggAndFlourMixedThenPanHeatedRecognizePancakes()
        {
            var potato = AddToBowl("potato", chopped: true);
            var egg = AddToBowl("egg"); var flour = AddToBowl("flour");
            var mixed = Mix(); CookBowlAndPlate(CookerKind.Pan);
            var dish = state.CaptureDish();
            Assert.That(dish.Recognition.RecipeId, Is.EqualTo("potato_pancakes"));
            Assert.That(dish.Recognition.Issues, Is.Empty);
            Assert.That(dish.Portions.Single().Id, Is.EqualTo(mixed.Id));
            Assert.That(dish.Portions.Single().Components.Select(c => c.Id),
                Is.EquivalentTo(new[] { potato.Id, egg.Id, flour.Id }));
            Assert.That(dish.Recognition.Ingredients.Select(i => i.Id), Is.EqualTo(new[] { "egg", "flour", "potato" }));
            Assert.That(dish.Recognition.Ingredients.All(i => i.Amount == 1), Is.True);
            Assert.That(new[] { potato, egg, flour }.All(p => p.Location == PortionLocation.Mixed), Is.True);
        }

        [Test] public void WholePotCarrotThenColdButterRecognizeButteredCarrotsWithoutRawWarning()
        {
            var carrot = CookAndPlate("carrot", CookerKind.Pot);
            var beforeButter = state.CaptureDish();
            Assert.That(beforeButter.Recognition.RecipeId, Is.Not.EqualTo("buttered_carrots"));
            var butter = Hand("butter"); Assert.That(state.TryPlaceServing(out _), Is.True);
            var dish = state.CaptureDish();
            Assert.That(dish.Recognition.RecipeId, Is.EqualTo("buttered_carrots"));
            Assert.That(dish.Recognition.Issues, Is.Empty);
            Assert.That(carrot.LastCooker, Is.EqualTo(CookerKind.Pot));
            Assert.That(carrot.StirPresses, Is.EqualTo(cooking.PotStirs));
            Assert.That(butter.Cooking, Is.EqualTo(CookState.Raw)); Assert.That(butter.HeatProgress, Is.Zero);
            Assert.That(beforeButter.Portions.Count, Is.EqualTo(1));
            Assert.That(dish.Portions.Count, Is.EqualTo(2));
        }

        [Test] public void ChoppedAppleSugarAndButterPanHeatedRecognizeCaramelApples()
        {
            var apple = CookAndPlate("apple", CookerKind.Pan, chopped: true);
            var sugar = CookAndPlate("sugar", CookerKind.Pan);
            var butter = CookAndPlate("butter", CookerKind.Pan);
            var dish = state.CaptureDish();
            Assert.That(dish.Recognition.RecipeId, Is.EqualTo("caramel_apples"));
            Assert.That(dish.Recognition.Issues, Is.Empty);
            Assert.That(dish.Portions.Select(p => p.Id), Is.EquivalentTo(new[] { apple.Id, sugar.Id, butter.Id }));
            Assert.That(dish.Portions.All(p => p.Cooking == CookState.Cooked && p.LastCooker == CookerKind.Pan), Is.True);
            Assert.That(apple.Preparation, Is.EqualTo(PreparationState.Chopped));
            Assert.That(dish.Recognition.Ingredients.Select(i => i.Id), Is.EqualTo(new[] { "apple", "butter", "sugar" }));
            Assert.That(dish.Portions.All(p => p.Components.Count == 0), Is.True);
        }

        [Test] public void SingleWholePanEggRecognizesFriedEggWithoutMixingOrMandatoryDoses()
        {
            var egg = CookAndPlate("egg", CookerKind.Pan);
            var dish = state.CaptureDish();
            Assert.That(dish.Recognition.RecipeId, Is.EqualTo("fried_egg"));
            Assert.That(dish.Recognition.Issues, Is.Empty);
            Assert.That(dish.Portions.Single().Id, Is.EqualTo(egg.Id));
            Assert.That(egg.Preparation, Is.EqualTo(PreparationState.Whole));
            Assert.That(egg.Components, Is.Empty); Assert.That(egg.Cooking, Is.EqualTo(CookState.Cooked));
            Assert.That(egg.SaltDoses + egg.OilDoses + dish.SaltDoses + dish.OilDoses, Is.Zero);
            Assert.That(dish.Recognition.Ingredients.Single().Id, Is.EqualTo("egg"));
            Assert.That(dish.Recognition.Ingredients.Single().Amount, Is.EqualTo(1));
        }

    }
}
