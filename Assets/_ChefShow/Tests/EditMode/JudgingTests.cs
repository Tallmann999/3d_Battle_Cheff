using System;
using System.Collections.Generic;
using System.Linq;
using ChefShow.Cooking;
using ChefShow.Core;
using ChefShow.Data;
using ChefShow.Ingredients;
using ChefShow.Inventory;
using ChefShow.Judging;
using ChefShow.Recipes;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ChefShow.Tests
{
    public sealed class JudgingTests
    {
        private const string ConfigPath = "Assets/_ChefShow/Data/Judging/JudgingConfig.asset";
        private PrototypeRun run;
        private InventoryState state;
        private JudgingConfig config;
        private JudgingSettings settings;
        private RecipeIdentitySettings recognition;
        private CookingSettings cooking;
        private DishwareSettings dishware;

        [SetUp] public void Setup()
        {
            config = AssetDatabase.LoadAssetAtPath<JudgingConfig>(ConfigPath);
            Assert.That(config, Is.Not.Null, "Install the saved judging profiles before running this suite.");
            var recipes = AssetDatabase.LoadAssetAtPath<RecipeRecognitionCatalog>("Assets/_ChefShow/Data/Recipes/Recognition/RecipeRecognitionCatalog.asset");
            var heat = AssetDatabase.LoadAssetAtPath<CookingConfig>("Assets/_ChefShow/Generated/Data/CookingConfig.asset");
            var dishes = AssetDatabase.LoadAssetAtPath<DishwareConfig>("Assets/_ChefShow/Generated/Data/DishwareConfig.asset");
            Assert.That(recipes, Is.Not.Null); Assert.That(heat, Is.Not.Null); Assert.That(dishes, Is.Not.Null);
            recognition = recipes.Capture(); cooking = heat.Capture(); dishware = dishes.Capture();
            settings = config.Capture(recognition, cooking);
            StartRun();
        }

        [TearDown] public void Cleanup() { run?.Dispose(); }

        private void StartRun(DishwareSettings ware = null)
        {
            run?.Dispose();
            run = new PrototypeRun(600, 1005, (type, error) => Assert.Fail(error.ToString()),
                cooking: cooking, dishware: ware ?? dishware, recognition: recognition);
            state = run.Inventory;
        }

        private IngredientDefinition Ingredient(string id)
        {
            var ingredient = AssetDatabase.LoadAssetAtPath<IngredientDefinition>("Assets/_ChefShow/Generated/Data/Ingredients/" + id + ".asset");
            Assert.That(ingredient, Is.Not.Null, id); return ingredient;
        }

        private FoodPortion Hand(string id)
        {
            Assert.That(state.TryMoveBasket(BasketPlacement.Carried, out var reason), Is.True, reason);
            Assert.That(state.TryCollect(Ingredient(id), out reason), Is.True, reason);
            Assert.That(state.TryMoveBasket(BasketPlacement.Station, out reason), Is.True, reason);
            Assert.That(state.TryUnload(out reason), Is.True, reason);
            Assert.That(state.TryTakeTray(state.Tray.Count - 1, out reason), Is.True, reason);
            return state.Held;
        }

        private FoodPortion PreparedHand(string id, bool chopped)
        {
            var food = Hand(id);
            if (!chopped) return food;
            Assert.That(state.TryPlaceSocket(0, out var reason), Is.True, reason);
            for (int i = 0; i < InventoryState.RequiredChopPresses; i++)
                Assert.That(state.TryChop(KitchenToolKind.Knife, out reason), Is.True, reason);
            Assert.That(state.TryTakeSocket(0, out reason), Is.True, reason);
            return food;
        }

        private void Medium(CookerKind kind)
        {
            while (state.Heat(kind) != HeatLevel.Medium)
                Assert.That(state.TryCycleHeat(kind, out var reason), Is.True, reason);
        }

        private FoodPortion CookAndPlate(string id, CookerKind kind = CookerKind.Pan, bool chopped = false, float? heatSeconds = null)
        {
            var food = PreparedHand(id, chopped);
            Assert.That(state.TryPlaceCooker(kind, out var reason), Is.True, reason); Medium(kind);
            if (kind == CookerKind.Pot)
                for (int i = 0; i < cooking.PotStirs; i++)
                    Assert.That(state.TryStir(kind, KitchenToolKind.Spatula, out reason), Is.True, reason);
            if (kind == CookerKind.Oven) Assert.That(state.TryToggleOvenDoor(out reason), Is.True, reason);
            run.Tick((heatSeconds ?? cooking.ReadyFor(kind)) / cooking.MediumRate);
            if (kind == CookerKind.Oven) Assert.That(state.TryToggleOvenDoor(out reason), Is.True, reason);
            Assert.That(state.TryTakeCooker(kind, 0, out reason), Is.True, reason);
            Assert.That(state.TryPlaceServing(out reason), Is.True, reason);
            return food;
        }

        private FoodPortion AddToBowl(string id, bool chopped = false)
        {
            var food = PreparedHand(id, chopped);
            Assert.That(state.TryPlaceBowl(out var reason), Is.True, reason); return food;
        }

        private FoodPortion Mix()
        {
            Assert.That(state.TryAdvanceMix(cooking.MixSeconds, KitchenToolKind.Spoon, Ingredient("mixture"), out var reason), Is.True, reason);
            return state.Bowl.Single();
        }

        private void CookBowlAndPlate(CookerKind kind)
        {
            Assert.That(state.TryTakeBowl(0, out var reason), Is.True, reason);
            Assert.That(state.TryPlaceCooker(kind, out reason), Is.True, reason); Medium(kind);
            if (kind == CookerKind.Oven) Assert.That(state.TryToggleOvenDoor(out reason), Is.True, reason);
            run.Tick(cooking.ReadyFor(kind) / cooking.MediumRate);
            if (kind == CookerKind.Oven) Assert.That(state.TryToggleOvenDoor(out reason), Is.True, reason);
            Assert.That(state.TryTakeCooker(kind, 0, out reason), Is.True, reason);
            Assert.That(state.TryPlaceServing(out reason), Is.True, reason);
        }

        private ScoreBreakdown Evaluate(string assigned = null, JudgingSettings captured = null, DishSnapshot snapshot = null)
            => DishScorer.Evaluate(snapshot ?? state.CaptureDish(), captured ?? settings, assigned);
        private static double Points(ScoreBreakdown score, ScoreCategoryId id) => score.Categories.Single(c => c.Id == id).Points;
        private static void EqualCategories(ScoreBreakdown a, ScoreBreakdown b)
        {
            Assert.That(a.Categories.Select(c => c.Id), Is.EqualTo(b.Categories.Select(c => c.Id)));
            for (int i = 0; i < a.Categories.Count; i++)
                Assert.That(a.Categories[i].Points, Is.EqualTo(b.Categories[i].Points).Within(.000001), a.Categories[i].Name);
        }

        [Test] public void SavedProfilesPreserveWeightsAndEmptyDishAlwaysScoresZero()
        {
            Assert.That(config.Profiles.Select(p => p.Reference.Id).Distinct().Count(), Is.EqualTo(12));
            var score = Evaluate();
            Assert.That(score.RecipeId, Is.EqualTo("no_dish")); Assert.That(score.Total, Is.Zero);
            Assert.That(score.UnroundedTotal, Is.Zero); Assert.That(score.RawTotal, Is.Zero);
            Assert.That(score.Categories.Select(c => c.Maximum), Is.EqualTo(new double[] { 25, 30, 20, 10, 10, 5 }));
            Assert.That(score.Categories.All(c => c.Points == 0), Is.True);
            Assert.That(state.TrySubmitDish(out _), Is.True);
            Assert.That(Evaluate("beef_steak", snapshot: state.SubmittedDish).UnroundedTotal, Is.Zero);
        }

        [Test] public void ProductionAggregateMatchesNinetyFourAndKeepsExactValueBeforeUiRounding()
        {
            var ids = new[] { ScoreCategoryId.Composition, ScoreCategoryId.ProcessTaste, ScoreCategoryId.Doneness,
                ScoreCategoryId.Preparation, ScoreCategoryId.Plating, ScoreCategoryId.Cleanliness };
            var points = new double[] { 24, 28, 20, 9, 8, 5 };
            var maxima = new double[] { 25, 30, 20, 10, 10, 5 };
            var categories = ids.Select((id, i) => new ScoreCategory(id, id.ToString(), points[i], maxima[i])).ToArray();
            Assert.That(ScoreBreakdown.Aggregate(categories, 100), Is.EqualTo(94));
            categories[0] = new ScoreCategory(ids[0], ids[0].ToString(), 23.6, 25);
            var score = new ScoreBreakdown("beef_steak", "Steak", "beef_steak", "Steak", false, categories, new[] { "Reference fixture" }, 100);
            Assert.That(score.RawTotal, Is.EqualTo(93.6).Within(.000001));
            Assert.That(score.UnroundedTotal, Is.EqualTo(93.6).Within(.000001)); Assert.That(score.Total, Is.EqualTo(94));
            Assert.That(ScoreBreakdown.Aggregate(categories, Math.Min(45, 50)), Is.EqualTo(45));
            Assert.Throws<ArgumentException>(() => new ScoreCategory(ids[0], "Invalid", double.NaN, 25));
            Assert.Throws<ArgumentException>(() => new ScoreCategory(ids[0], "Invalid", 26, 25));
            Assert.Throws<ArgumentException>(() => ScoreBreakdown.Aggregate(categories, -1));
            Assert.Throws<ArgumentException>(() => ScoreBreakdown.Aggregate(new[] { categories[0], categories[0], categories[2], categories[3], categories[4], categories[5] }, 100));
        }

        [Test] public void ActualHeatControlsDonenessAndCookingDoesNotRewriteEarlierSnapshot()
        {
            var readyFood = CookAndPlate("beef"); var readySnapshot = state.CaptureDish(); var ready = Evaluate(snapshot: readySnapshot);
            StartRun(); CookAndPlate("beef", heatSeconds: (cooking.Overcooked + cooking.Burned) / 2);
            var overcooked = Evaluate();
            StartRun(); CookAndPlate("beef", heatSeconds: cooking.Burned); var burned = Evaluate();
            Assert.That(Points(ready, ScoreCategoryId.Doneness), Is.GreaterThan(Points(overcooked, ScoreCategoryId.Doneness)));
            Assert.That(Points(overcooked, ScoreCategoryId.Doneness), Is.GreaterThan(Points(burned, ScoreCategoryId.Doneness)));
            Assert.That(readySnapshot.Portions.Single().Id, Is.EqualTo(readyFood.Id));
            Assert.That(readySnapshot.Portions.Single().Cooking, Is.EqualTo(CookState.Cooked));
            Assert.That(readySnapshot.Portions.Single().HeatProgress, Is.EqualTo(cooking.Ready));
            EqualCategories(ready, Evaluate(snapshot: readySnapshot));
        }

        [Test] public void RawMandatoryProteinAndBurnedMainApplySmallestRelevantCap()
        {
            Hand("beef"); state.TryPlaceServing(out _); var raw = Evaluate("beef_steak");
            Assert.That(raw.ApplicableCap, Is.EqualTo(45)); Assert.That(raw.UnroundedTotal, Is.LessThanOrEqualTo(45));
            StartRun(); CookAndPlate("beef", heatSeconds: cooking.Burned); var burned = Evaluate("beef_steak");
            Assert.That(burned.ApplicableCap, Is.EqualTo(50)); Assert.That(burned.UnroundedTotal, Is.LessThanOrEqualTo(50));
            Hand("beef"); state.TryPlaceServing(out _); var both = Evaluate("beef_steak");
            Assert.That(both.ApplicableCap, Is.EqualTo(45)); Assert.That(both.UnroundedTotal, Is.LessThanOrEqualTo(45));
            Assert.That(both.Reasons, Is.Not.Empty);
        }

        [Test] public void MethodAndPreparationAreSeparateCategoriesForWholeVersusChoppedFood()
        {
            CookAndPlate("potato"); CookAndPlate("onion", chopped: true); var wholePan = Evaluate("fried_potatoes");
            StartRun(); CookAndPlate("potato", CookerKind.Pot); CookAndPlate("onion", chopped: true); var wholePot = Evaluate("fried_potatoes");
            StartRun(); CookAndPlate("potato", chopped: true); CookAndPlate("onion", chopped: true); var choppedPan = Evaluate("fried_potatoes");
            Assert.That(Points(wholePan, ScoreCategoryId.Preparation), Is.EqualTo(Points(wholePot, ScoreCategoryId.Preparation)).Within(.000001));
            Assert.That(Points(wholePan, ScoreCategoryId.ProcessTaste), Is.GreaterThan(Points(wholePot, ScoreCategoryId.ProcessTaste)));
            Assert.That(Points(choppedPan, ScoreCategoryId.Preparation), Is.GreaterThan(Points(wholePan, ScoreCategoryId.Preparation)));
            Assert.That(Points(choppedPan, ScoreCategoryId.ProcessTaste), Is.EqualTo(Points(wholePan, ScoreCategoryId.ProcessTaste)).Within(.000001));
            Assert.That(Points(wholePan, ScoreCategoryId.Doneness), Is.EqualTo(Points(wholePot, ScoreCategoryId.Doneness)).Within(.000001));
        }

        [Test] public void CookingInputsThenMixingColdCannotCompleteTheMixThenHeatRecipe()
        {
            CookAndPlate("egg"); CookAndPlate("cheese");
            state.TryTakeServing(0, out _); state.TryPlaceBowl(out _);
            state.TryTakeServing(0, out _); state.TryPlaceBowl(out _);
            Mix(); state.TryTakeBowl(0, out _); state.TryPlaceServing(out _);
            var coldMixture = state.CaptureDish(); var cold = Evaluate("cheese_omelet", snapshot: coldMixture);
            Assert.That(coldMixture.Portions.Single().Cooking, Is.EqualTo(CookState.Raw));
            Assert.That(coldMixture.Recognition.RecipeId, Is.Not.EqualTo("cheese_omelet"));
            StartRun(); AddToBowl("egg"); AddToBowl("cheese"); Mix(); CookBowlAndPlate(CookerKind.Pan);
            var correctlyHeated = Evaluate("cheese_omelet");
            Assert.That(correctlyHeated.RecipeId, Is.EqualTo("cheese_omelet"));
            Assert.That(Points(correctlyHeated, ScoreCategoryId.ProcessTaste) - Points(cold, ScoreCategoryId.ProcessTaste),
                Is.EqualTo(settings.CompletionMaximum).Within(.000001));
        }

        [Test] public void OneProperMixtureCannotHideAColdCookedInputMixtureInTheSameDish()
        {
            AddToBowl("egg"); AddToBowl("cheese"); var proper = Mix(); CookBowlAndPlate(CookerKind.Pan);
            var properOnly = Evaluate("cheese_omelet");
            Assert.That(Points(properOnly, ScoreCategoryId.ProcessTaste), Is.EqualTo(settings.ProcessTaste).Within(.000001));
            CookAndPlate("egg"); CookAndPlate("cheese");
            state.TryTakeServing(1, out _); state.TryPlaceBowl(out _);
            state.TryTakeServing(1, out _); state.TryPlaceBowl(out _);
            var cold = Mix(); state.TryTakeBowl(0, out _); state.TryPlaceServing(out _);
            var dish = state.CaptureDish(); var score = Evaluate("cheese_omelet", snapshot: dish);
            Assert.That(dish.Portions.Select(p => p.Id), Is.EqualTo(new[] { proper.Id, cold.Id }));
            Assert.That(dish.Recognition.RecipeId, Is.EqualTo("cheese_omelet"));
            Assert.That(cold.Cooking, Is.EqualTo(CookState.Raw));
            // Every input really used Pan and dose zero is in the profile's optional range.
            // The remaining fractional contribution must come from evaluating both mixes.
            double completion = Points(score, ScoreCategoryId.ProcessTaste) - settings.MethodMaximum - settings.SeasoningMaximum;
            Assert.That(completion, Is.GreaterThan(0));
            Assert.That(completion, Is.LessThan(settings.CompletionMaximum));
            Assert.That(Points(score, ScoreCategoryId.ProcessTaste), Is.LessThan(Points(properOnly, ScoreCategoryId.ProcessTaste)));
        }

        [TestCase("carrot", "onion")]
        [TestCase("onion", "carrot")]
        public void AllowedPanVegetableNeedsNoPotStirAndAlternativesShareOneQuantityGroup(string firstVegetable, string secondVegetable)
        {
            CookAndPlate("beef"); CookAndPlate("potato", CookerKind.Pot, chopped: true);
            CookAndPlate(firstVegetable, chopped: true); var oneVegetable = Evaluate();
            Assert.That(oneVegetable.RecipeId, Is.EqualTo("steak_vegetables"));
            Assert.That(Points(oneVegetable, ScoreCategoryId.ProcessTaste), Is.EqualTo(settings.ProcessTaste).Within(.000001));
            CookAndPlate(secondVegetable, chopped: true); var twoVegetables = Evaluate();
            Assert.That(twoVegetables.RecipeId, Is.EqualTo("steak_vegetables"));
            Assert.That(Points(twoVegetables, ScoreCategoryId.Composition), Is.EqualTo(Points(oneVegetable, ScoreCategoryId.Composition)).Within(.000001));
            Assert.That(Points(twoVegetables, ScoreCategoryId.ProcessTaste), Is.EqualTo(Points(oneVegetable, ScoreCategoryId.ProcessTaste)).Within(.000001));
            Assert.That(Points(twoVegetables, ScoreCategoryId.Plating), Is.LessThan(Points(oneVegetable, ScoreCategoryId.Plating)));
        }

        [Test] public void RepeatedAmountKeepsIdentityAndCompositionAndChangesOnlyQuantityPresentation()
        {
            CookAndPlate("egg"); var one = Evaluate(); var identity = state.CaptureDish().Recognition.RecipeId;
            CookAndPlate("egg"); CookAndPlate("egg"); var three = Evaluate();
            Assert.That(state.CaptureDish().Recognition.RecipeId, Is.EqualTo(identity));
            Assert.That(Points(one, ScoreCategoryId.Composition), Is.EqualTo(Points(three, ScoreCategoryId.Composition)).Within(.000001));
            Assert.That(Points(one, ScoreCategoryId.ProcessTaste), Is.EqualTo(Points(three, ScoreCategoryId.ProcessTaste)).Within(.000001));
            Assert.That(Points(one, ScoreCategoryId.Preparation), Is.EqualTo(Points(three, ScoreCategoryId.Preparation)).Within(.000001));
            Assert.That(Points(one, ScoreCategoryId.Doneness), Is.EqualTo(Points(three, ScoreCategoryId.Doneness)).Within(.000001));
            Assert.That(Points(three, ScoreCategoryId.Plating), Is.LessThan(Points(one, ScoreCategoryId.Plating)));
        }

        [Test] public void PortionDoseAndOneWholeDishDoseHaveTheSameTasteContribution()
        {
            Hand("beef"); state.TryPlaceCooker(CookerKind.Pan, out _);
            Hand("salt"); state.TryApplySeasoning(CookerKind.Pan, 0, out _); state.TryPutInTray(out _);
            Medium(CookerKind.Pan); run.Tick(cooking.Ready / cooking.MediumRate);
            state.TryTakeCooker(CookerKind.Pan, 0, out _); state.TryPlaceServing(out _);
            var portionDose = state.CaptureDish(); var before = Evaluate(snapshot: portionDose);
            StartRun(); CookAndPlate("beef"); Hand("salt"); state.TrySeasonPlate(out _); state.TryPutInTray(out _);
            var plateDose = state.CaptureDish(); var after = Evaluate(snapshot: plateDose);
            Assert.That(portionDose.Portions.Sum(p => p.SaltDoses), Is.EqualTo(cooking.DosesPerPress));
            Assert.That(portionDose.SaltDoses, Is.Zero); Assert.That(plateDose.SaltDoses, Is.EqualTo(cooking.DosesPerPress));
            Assert.That(plateDose.Portions.Sum(p => p.SaltDoses), Is.Zero);
            Assert.That(Points(before, ScoreCategoryId.ProcessTaste), Is.EqualTo(Points(after, ScoreCategoryId.ProcessTaste)).Within(.000001));
        }

        [Test] public void NestedComponentsAndInheritedDoseAreCountedOnceAgainstEquivalentFlatTart()
        {
            var copy = Object.Instantiate(config);
            var profile = Object.Instantiate(config.Profiles.Single(p => p.Reference.Id == "apple_tart"));
            try
            {
                copy.Profiles = copy.Profiles.Select(p => p.Reference.Id == "apple_tart" ? profile : p).ToArray();
                // A test-only narrow dose range exposes double counting without prescribing production balance.
                profile.Salt = new JudgingRange(0, cooking.DosesPerPress / 5f, cooking.DosesPerPress / 5f, 3 * cooking.DosesPerPress / 5f);
                var captured = copy.Capture(recognition, cooking);
                AddToBowl("flour"); AddToBowl("egg"); Hand("salt"); state.TrySeasonBowl(0, out _); state.TryPutInTray(out _);
                Mix(); AddToBowl("apple", chopped: true); AddToBowl("butter"); AddToBowl("sugar"); Mix(); CookBowlAndPlate(CookerKind.Oven);
                var nestedDish = state.CaptureDish(); var nested = Evaluate(captured: captured, snapshot: nestedDish);
                StartRun(); AddToBowl("flour"); AddToBowl("egg"); AddToBowl("apple", chopped: true); AddToBowl("butter"); AddToBowl("sugar");
                Mix(); CookBowlAndPlate(CookerKind.Oven); Hand("salt"); state.TrySeasonPlate(out _); state.TryPutInTray(out _);
                var flatDish = state.CaptureDish(); var flat = Evaluate(captured: captured, snapshot: flatDish);
                Assert.That(nestedDish.Quantity, Is.EqualTo(5)); Assert.That(flatDish.Quantity, Is.EqualTo(5));
                Assert.That(nestedDish.Recognition.Ingredients.Sum(i => i.Amount), Is.EqualTo(5));
                Assert.That(nestedDish.Portions.Single().SaltDoses, Is.EqualTo(cooking.DosesPerPress));
                Assert.That(nested.RecipeId, Is.EqualTo("apple_tart")); Assert.That(flat.RecipeId, Is.EqualTo("apple_tart"));
                Assert.That(nested.ApplicableCap, Is.EqualTo(100)); Assert.That(flat.ApplicableCap, Is.EqualTo(100));
                EqualCategories(nested, flat);
            }
            finally { Object.DestroyImmediate(copy); Object.DestroyImmediate(profile); }
        }

        [Test] public void ProperCapacityImprovesDishwarePresentationAndColorHasNoEffect()
        {
            var starting = dishware.Starting;
            Func<int, Color, DishwareSettings> ware = (capacity, color) => new DishwareSettings(new[] {
                new DishwareSnapshot(starting.Id, starting.DisplayName, capacity, starting.Diameter, starting.Depth, starting.SupportsLiquid, color)
            }, starting.Id);
            StartRun(ware(1, Color.white)); for (int i = 0; i < 3; i++) CookAndPlate("egg"); var tooSmall = Evaluate();
            StartRun(ware(6, Color.white)); for (int i = 0; i < 3; i++) CookAndPlate("egg"); var fits = Evaluate();
            StartRun(ware(6, Color.magenta)); for (int i = 0; i < 3; i++) CookAndPlate("egg"); var colored = Evaluate();
            Assert.That(Points(fits, ScoreCategoryId.Plating), Is.GreaterThan(Points(tooSmall, ScoreCategoryId.Plating)));
            Assert.That(Points(fits, ScoreCategoryId.Composition), Is.EqualTo(Points(tooSmall, ScoreCategoryId.Composition)).Within(.000001));
            EqualCategories(fits, colored);
            Assert.That(fits.UnroundedTotal, Is.EqualTo(colored.UnroundedTotal).Within(.000001));
        }

        [Test] public void ExistingPresentationPenaltyIsAppliedOnceToPlatingOnly()
        {
            CookAndPlate("beef"); var beforeDish = state.CaptureDish(); var before = Evaluate(snapshot: beforeDish);
            state.TryTakeServing(0, out _); state.TryPlaceSocket(0, out _);
            Assert.That(state.TryTakePlacedDishware(out _), Is.True); Assert.That(state.TryPlaceDishware(out _), Is.True);
            state.TryTakeSocket(0, out _); state.TryPlaceServing(out _); var afterDish = state.CaptureDish(); var after = Evaluate(snapshot: afterDish);
            Assert.That(afterDish.PresentationPenalty, Is.EqualTo(1)); Assert.That(beforeDish.PresentationPenalty, Is.Zero);
            Assert.That(Points(before, ScoreCategoryId.Plating) - Points(after, ScoreCategoryId.Plating), Is.EqualTo(1).Within(.000001));
            Assert.That(before.RawTotal - after.RawTotal, Is.EqualTo(1).Within(.000001));
            foreach (var id in before.Categories.Select(c => c.Id).Where(id => id != ScoreCategoryId.Plating))
                Assert.That(Points(before, id), Is.EqualTo(Points(after, id)).Within(.000001));
        }

        [Test] public void SubmissionAddsCompletionPointOnlyAndPreviewCannotClaimIt()
        {
            CookAndPlate("beef"); var previewDish = state.CaptureDish(); var preview = Evaluate(snapshot: previewDish);
            Assert.That(state.TrySubmitDish(out _), Is.True);
            var submitted = Evaluate(snapshot: state.SubmittedDish);
            Assert.That(previewDish.IsSubmitted, Is.False); Assert.That(state.SubmittedDish.IsSubmitted, Is.True);
            Assert.That(Points(submitted, ScoreCategoryId.Cleanliness) - Points(preview, ScoreCategoryId.Cleanliness), Is.EqualTo(1).Within(.000001));
            Assert.That(submitted.RawTotal - preview.RawTotal, Is.EqualTo(1).Within(.000001));
            foreach (var id in preview.Categories.Select(c => c.Id).Where(id => id != ScoreCategoryId.Cleanliness))
                Assert.That(Points(submitted, id), Is.EqualTo(Points(preview, id)).Within(.000001));
        }

        [Test] public void ContaminationChangesCleanlinessWithoutRepairingFoodOrOldSnapshot()
        {
            var food = CookAndPlate("beef"); var cleanDish = state.CaptureDish(); var clean = Evaluate(snapshot: cleanDish);
            // Hygiene interactions are outside this slice; seed only contamination on genuinely cooked food.
            typeof(FoodPortion).GetProperty(nameof(FoodPortion.Contaminated)).SetValue(food, true);
            var dirty = Evaluate();
            Assert.That(Points(dirty, ScoreCategoryId.Cleanliness), Is.LessThan(Points(clean, ScoreCategoryId.Cleanliness)));
            Assert.That(dirty.UnroundedTotal, Is.LessThan(clean.UnroundedTotal));
            Assert.That(food.Contaminated, Is.True); Assert.That(food.Cooking, Is.EqualTo(CookState.Cooked));
            Assert.That(cleanDish.Portions.Single().Contaminated, Is.False); EqualCategories(clean, Evaluate(snapshot: cleanDish));
        }

        [Test] public void AssignedRecipeIsEvaluatedWithoutChangingActualRecognitionOrRun()
        {
            CookAndPlate("beef"); var dish = state.CaptureDish(); var beforeVersion = state.Version;
            var beforeTime = run.RemainingSeconds; var actual = Evaluate(snapshot: dish); var assigned = Evaluate("fried_egg", snapshot: dish);
            Assert.That(actual.RecipeId, Is.EqualTo("beef_steak")); Assert.That(assigned.RecipeId, Is.EqualTo("beef_steak"));
            Assert.That(assigned.EvaluatedRecipeId, Is.EqualTo("fried_egg"));
            Assert.That(Points(assigned, ScoreCategoryId.Composition), Is.LessThan(Points(actual, ScoreCategoryId.Composition)));
            Assert.That(dish.Recognition.RecipeId, Is.EqualTo("beef_steak"));
            Assert.That(state.Version, Is.EqualTo(beforeVersion)); Assert.That(run.RemainingSeconds, Is.EqualTo(beforeTime));
        }

        [Test] public void CapturedConfigAndSubmittedSnapshotSurviveResetResubmitAndTimeup()
        {
            var copy = Object.Instantiate(config); var profile = Object.Instantiate(config.Profiles.Single(p => p.Reference.Id == "beef_steak"));
            try
            {
                copy.Profiles = copy.Profiles.Select(p => p.Reference.Id == "beef_steak" ? profile : p).ToArray();
                var captured = copy.Capture(recognition, cooking);
                CookAndPlate("beef"); state.TrySubmitDish(out _); var firstDish = state.SubmittedDish; var first = Evaluate(captured: captured, snapshot: firstDish);
                copy.RawProteinCap = 3; profile.Core[0].ReferenceQuantity += 10; profile.Salt = new JudgingRange(0, 12, 15, 20);
                EqualCategories(first, Evaluate(captured: captured, snapshot: firstDish));
                Assert.That(state.TryResetSubmission(out _), Is.True); Hand("beef"); state.TryPlaceServing(out _); state.TrySubmitDish(out _);
                var secondDish = state.SubmittedDish; var second = Evaluate(captured: captured, snapshot: secondDish);
                Assert.That(second.ApplicableCap, Is.EqualTo(45)); Assert.That(firstDish.Quantity, Is.EqualTo(1)); Assert.That(secondDish.Quantity, Is.EqualTo(2));
                Assert.That(state.TryResetSubmission(out _), Is.True); run.SetRemaining(.01f); run.Tick(1);
                var timed = Evaluate(captured: captured, snapshot: state.SubmittedDish);
                EqualCategories(second, timed); Assert.That(state.TryResetSubmission(out _), Is.False);
                EqualCategories(first, Evaluate(captured: captured, snapshot: firstDish));
                Assert.That(first.Reasons, Is.EqualTo(Evaluate(captured: captured, snapshot: firstDish).Reasons));
            }
            finally { Object.DestroyImmediate(copy); Object.DestroyImmediate(profile); }
        }

        [Test] public void DeterministicResultsAndReadOnlyListsStayWithinZeroToHundred()
        {
            CookAndPlate("beef"); Hand("egg"); state.TryPlaceServing(out _); var dish = state.CaptureDish();
            var first = Evaluate(snapshot: dish); var second = Evaluate(snapshot: dish);
            EqualCategories(first, second); Assert.That(first.Reasons, Is.EqualTo(second.Reasons));
            Assert.That(first.Reasons.Count, Is.InRange(1, 3));
            Assert.That(first.UnroundedTotal, Is.EqualTo(second.UnroundedTotal));
            Assert.That(double.IsNaN(first.UnroundedTotal) || double.IsInfinity(first.UnroundedTotal), Is.False);
            Assert.That(first.UnroundedTotal, Is.InRange(0d, 100d)); Assert.That(first.Total, Is.InRange(0, 100));
            foreach (var category in first.Categories)
            {
                Assert.That(double.IsNaN(category.Points) || double.IsInfinity(category.Points), Is.False);
                Assert.That(category.Points, Is.InRange(0d, category.Maximum));
            }
            Assert.Throws<NotSupportedException>(() => ((IList<ScoreCategory>)first.Categories)[0] = null);
            Assert.That(((ICollection<string>)first.Reasons).IsReadOnly, Is.True);
        }

        [Test] public void FixedCategoryWeightRejectsFractionalDriftButProcessSubweightsCanRemainFractional()
        {
            var copy = Object.Instantiate(config);
            try
            {
                copy.Composition = 25.00001f;
                Assert.Throws<InvalidOperationException>(() => copy.Capture(recognition, cooking));
                copy.Composition = config.Composition;
                copy.MethodMaximum = 14.25f; copy.SeasoningMaximum = 10.125f; copy.CompletionMaximum = 5.625f;
                JudgingSettings captured = null;
                Assert.DoesNotThrow(() => captured = copy.Capture(recognition, cooking));
                Assert.That(captured.MethodMaximum + captured.SeasoningMaximum + captured.CompletionMaximum, Is.EqualTo(30));
                Assert.That(captured.MethodMaximum, Is.EqualTo(14.25));
                Assert.That(captured.SeasoningMaximum, Is.EqualTo(10.125));
                Assert.That(captured.CompletionMaximum, Is.EqualTo(5.625));
            }
            finally { Object.DestroyImmediate(copy); }
        }

        [Test] public void InvalidWeightsCapsDoseRangesAndMissingProfileFailBeforeScoring()
        {
            var copy = Object.Instantiate(config); var profile = Object.Instantiate(config.Profiles[0]);
            try
            {
                copy.Profiles = copy.Profiles.Select(p => p == config.Profiles[0] ? profile : p).ToArray();
                float maximum = copy.Composition; copy.Composition = -1;
                Assert.Throws<InvalidOperationException>(() => copy.Capture(recognition, cooking)); copy.Composition = maximum;
                float cap = copy.RawProteinCap; copy.RawProteinCap = float.NaN;
                Assert.Throws<InvalidOperationException>(() => copy.Capture(recognition, cooking)); copy.RawProteinCap = cap;
                var salt = profile.Salt; profile.Salt = new JudgingRange(0, 3, 2, 5);
                Assert.Throws<InvalidOperationException>(() => copy.Capture(recognition, cooking)); profile.Salt = salt;
                profile.Reference = null; Assert.Throws<InvalidOperationException>(() => copy.Capture(recognition, cooking));
            }
            finally { Object.DestroyImmediate(copy); Object.DestroyImmediate(profile); }
        }
    }
}
