using System;
using System.Collections.Generic;
using System.Linq;
using ChefShow.Cooking;
using ChefShow.Core;
using ChefShow.Data;
using ChefShow.Ingredients;
using ChefShow.Inventory;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ChefShow.Tests
{
    public sealed class TastingStandTests
    {
        private PrototypeRun run;
        private InventoryState state;
        private IngredientDefinition beef, potato, egg, salt, oil;
        private readonly List<IngredientDefinition> definitions = new List<IngredientDefinition>();
        [SetUp] public void Setup()
        {
            beef = Ingredient("beef", true); potato = Ingredient("potato", true); egg = Ingredient("egg");
            salt = Ingredient("salt"); salt.IsDoseContainer = true; oil = Ingredient("oil"); oil.IsDoseContainer = true;
            var ware = new DishwareSettings(new[] { DishwareSnapshot.Default,
                new DishwareSnapshot("bowl", "Bowl", 10, .61f, .18f, true, Color.green) }, "small_flat");
            run = new PrototypeRun(600, 1005, (type, error) => Assert.Fail(error.ToString()),
                cooking: new CookingSettings(3, 30, 45, 60, .55f, 1, 1.75f, 3), dishware: ware);
            state = run.Inventory;
        }
        [TearDown] public void Cleanup()
        { run.Dispose(); foreach (var definition in definitions) Object.DestroyImmediate(definition); definitions.Clear(); }
        private IngredientDefinition Ingredient(string id, bool board = false)
        {
            var value = ScriptableObject.CreateInstance<IngredientDefinition>(); value.Id = value.DisplayName = id;
            value.CanUseBoard = board; definitions.Add(value); return value;
        }
        private FoodPortion Hand(IngredientDefinition ingredient)
        { Assert.That(state.TryCollect(ingredient, out var reason), Is.True, reason); return state.Held; }
        private FoodPortion CookPlate(IngredientDefinition ingredient, bool chop = false, bool burn = false)
        {
            var food = Hand(ingredient);
            if (chop)
            {
                Assert.That(state.TryPlaceSocket(0, out _), Is.True);
                for (int i = 0; i < 6; i++) Assert.That(state.TryChop(KitchenToolKind.Knife, out _), Is.True);
                Assert.That(state.TryTakeSocket(0, out _), Is.True);
            }
            Assert.That(state.TryPlaceCooker(CookerKind.Pan, out _), Is.True);
            while (state.Heat(CookerKind.Pan) != HeatLevel.Medium) state.TryCycleHeat(CookerKind.Pan, out _);
            run.Tick(burn ? 60 : 30); Assert.That(state.TryTakeCooker(CookerKind.Pan, 0, out _), Is.True);
            Assert.That(state.TryPlaceServing(out _), Is.True); return food;
        }
        private FoodPortion[] Filled()
        {
            var first = CookPlate(beef); var second = CookPlate(potato, true, true);
            var source = Hand(salt);
            // The hygiene mechanic is a later feature. Seed only the real dose source's contamination.
            typeof(FoodPortion).GetProperty(nameof(FoodPortion.Contaminated)).SetValue(source, true);
            Assert.That(state.TrySeasonPlate(out _), Is.True); Assert.That(state.TrySeasonPlate(out _), Is.True);
            Assert.That(state.TryCancelHeld(out _), Is.True);
            Hand(oil); Assert.That(state.TrySeasonPlate(out _), Is.True); Assert.That(state.TryCancelHeld(out _), Is.True);
            return new[] { first, second };
        }
        private DishSnapshot SubmitToStand()
        {
            Assert.That(state.TryTakePlacedDishware(out _), Is.True);
            Assert.That(state.TryPlaceTastingDish(out _), Is.True); return state.SubmittedDish;
        }
        private void Conserved()
        {
            var active = state.Basket.Concat(state.Tray).Concat(state.Served).Concat(state.HeldServed).Concat(state.TastingServed)
                .Concat(state.Bowl).Concat(state.Cooker(CookerKind.Pan)).Concat(state.Cooker(CookerKind.Pot)).Concat(state.Cooker(CookerKind.Oven))
                .Concat(new[] { state.Held, state.Socket(0) }.Where(p => p != null)).ToArray();
            var archived = state.Portions.Where(p => p.Location == PortionLocation.Returned || p.Location == PortionLocation.Trash
                || p.Location == PortionLocation.Unpacked || p.Location == PortionLocation.Mixed).ToArray();
            Assert.That(active.Select(p => p.Id).Distinct().Count(), Is.EqualTo(active.Length));
            Assert.That(active.Length + archived.Length, Is.EqualTo(state.Portions.Count));
            Assert.That(state.TastingServed.All(p => p.Location == PortionLocation.TastingStand), Is.True);
        }

        [Test] public void PlacementTransfersRealFoodAndDosesBeforePublishingOneImmutableSubmission()
        {
            var food = Filled(); var before = state.CaptureDish(); int count = state.Portions.Count;
            Assert.That(state.TryTakePlacedDishware(out _), Is.True); int submissions = 0;
            using (run.Events.Subscribe<DishChanged>(fact =>
            {
                if (fact.Action != "dish_submitted") return;
                submissions++; Assert.That(fact.Dish, Is.SameAs(state.SubmittedDish));
                Assert.That(state.HeldDishware, Is.Null); Assert.That(state.HeldServed, Is.Empty);
                Assert.That(state.Served, Is.Empty); Assert.That(state.TastingServed, Is.EqualTo(food));
                Assert.That(state.TryPlaceTastingDish(out _), Is.False); Conserved();
            }))
            {
                Assert.That(state.TryPlaceTastingDish(out _), Is.True); var submitted = state.SubmittedDish;
                Assert.That(submissions, Is.EqualTo(1)); Assert.That(submitted.IsSubmitted, Is.True);
                Assert.That(submitted.Portions.Select(p => p.Id), Is.EqualTo(food.Select(p => p.Id)));
                Assert.That(submitted.Portions.All(p => p.Location == PortionLocation.TastingStand), Is.True);
                Assert.That(submitted.SaltDoses, Is.EqualTo(2)); Assert.That(submitted.OilDoses, Is.EqualTo(1)); Assert.That(submitted.Contaminated, Is.True);
                Assert.That(state.PlateSaltDoses + state.PlateOilDoses, Is.Zero); Assert.That(state.CurrentDishware, Is.Null);
                Assert.That(state.TastingDishware.Id, Is.EqualTo("small_flat")); Assert.That(state.PresentationPenalty, Is.Zero);
                Assert.That(state.CaptureDish().Quantity, Is.Zero); Assert.That(state.CaptureDisplayedDish().Quantity, Is.EqualTo(2));
                Assert.That(state.Portions.Count, Is.EqualTo(count)); Assert.That(state.TryResetSubmission(out _), Is.False);
                run.Tick(1); run.Tick(1); Assert.That(submissions, Is.EqualTo(1)); Assert.That(state.SubmittedDish, Is.SameAs(submitted));
                Assert.That(before.Portions[0].Operations, Is.EqualTo(submitted.Portions[0].Operations));
                Assert.That(before.Portions[1].HeatProgress, Is.EqualTo(submitted.Portions[1].HeatProgress)); Conserved();
            }
        }

        [Test] public void RetakePutBackAndResubmitPreserveIdentityDosesHistoryAndOldSnapshots()
        {
            var food = Filled(); var first = SubmitToStand(); float remaining = run.RemainingSeconds;
            Assert.That(state.TryTakeTastingDish(out _), Is.True); Assert.That(state.SubmittedDish, Is.Null);
            Assert.That(state.TastingDishware, Is.Null); Assert.That(state.TastingServed, Is.Empty);
            Assert.That(state.HeldServed, Is.EqualTo(food)); Assert.That(state.HeldPlateSaltDoses, Is.EqualTo(2));
            Assert.That(state.HeldPlateOilDoses, Is.EqualTo(1)); Assert.That(state.HeldPlateContaminated, Is.True);
            Assert.That(state.TryReturnDishware(out _, true), Is.False); Assert.That(state.TryReturnDishware(out _), Is.True);
            Assert.That(state.Served, Is.EqualTo(food)); Assert.That(state.PresentationPenalty, Is.Zero);
            var extra = Hand(egg); Assert.That(state.TryPlaceServing(out _), Is.True); var second = SubmitToStand();
            Assert.That(second, Is.Not.SameAs(first)); Assert.That(first.Quantity, Is.EqualTo(2)); Assert.That(second.Quantity, Is.EqualTo(3));
            Assert.That(second.Portions.Select(p => p.Id), Is.EqualTo(food.Select(p => p.Id).Concat(new[] { extra.Id })));
            Assert.That(first.SaltDoses, Is.EqualTo(2)); Assert.That(first.Portions[1].Cooking, Is.EqualTo(CookState.Burned));
            Assert.That(second.Portions[0].Operations, Is.EqualTo(first.Portions[0].Operations));
            Assert.That(second.Portions[1].OriginComponents, Is.EqualTo(first.Portions[1].OriginComponents));
            Assert.That(run.RemainingSeconds, Is.EqualTo(remaining)); Conserved();
        }

        [Test] public void TrashDiscardsOnlyHeldPlateAndFoodAndStartsCleanAttemptWithNewFoodIds()
        {
            Assert.That(state.TryTakeDishware("bowl", out _), Is.True); Assert.That(state.TryPlaceDishware(out _), Is.True);
            Assert.That(state.PresentationPenalty, Is.EqualTo(1)); var food = Filled();
            var tray = Hand(egg); state.TryPutInTray(out _); var board = Hand(potato); state.TryPlaceSocket(0, out _);
            var pan = Hand(egg); state.TryPlaceCooker(CookerKind.Pan, out _); var old = SubmitToStand(); int count = state.Portions.Count;
            Assert.That(state.TryTakeTastingDish(out _), Is.True); Assert.That(state.TryDiscardHeldDishware(out _), Is.True);
            Assert.That(state.HeldDishware, Is.Null); Assert.That(state.HeldServed, Is.Empty); Assert.That(state.HeldPlateSaltDoses + state.HeldPlateOilDoses, Is.Zero);
            Assert.That(state.HeldPlateContaminated, Is.False); Assert.That(state.PresentationPenalty, Is.Zero); Assert.That(state.SubmittedDish, Is.Null);
            Assert.That(state.Portions.Count, Is.EqualTo(count)); Assert.That(food.All(p => p.Location == PortionLocation.Trash), Is.True);
            Assert.That(food.All(p => p.Operations.Count(o => o.Action == "discarded_with_dishware") == 1), Is.True);
            Assert.That(state.Tray.Single(), Is.SameAs(tray)); Assert.That(state.Socket(0), Is.SameAs(board)); Assert.That(state.Cooker(CookerKind.Pan).Single(), Is.SameAs(pan));
            int version = state.Version; Assert.That(state.TryDiscardHeldDishware(out _), Is.False); Assert.That(state.Version, Is.EqualTo(version));
            Assert.That(old.Quantity, Is.EqualTo(2)); Assert.That(old.SaltDoses, Is.EqualTo(2)); Assert.That(old.PresentationPenalty, Is.EqualTo(1));
            Assert.That(old.Portions.All(p => p.Location == PortionLocation.TastingStand), Is.True);
            Assert.That(state.TryTakeDishware("small_flat", out _), Is.True); Assert.That(state.TryPlaceDishware(out _), Is.True);
            var fresh = Hand(beef); Assert.That(state.TryPlaceServing(out _), Is.True); var next = SubmitToStand();
            Assert.That(next.Quantity, Is.EqualTo(1)); Assert.That(next.SaltDoses + next.OilDoses, Is.Zero); Assert.That(next.PresentationPenalty, Is.Zero);
            Assert.That(next.Portions.Single().Id, Is.EqualTo(fresh.Id)); Assert.That(food.Select(p => p.Id), Does.Not.Contain(fresh.Id)); Conserved();
        }

        [Test] public void SpareSupplyPlateCannotSubmitWhileWorkingDishRetainsItsFoodAndDoses()
        {
            var food = Filled(); var working = state.CaptureDish();
            Assert.That(state.TryTakeDishware("bowl", out _), Is.True); int version = state.Version;
            Assert.That(state.HeldDishwareFromStation, Is.False); Assert.That(state.TryPlaceTastingDish(out _), Is.False);
            Assert.That(state.Version, Is.EqualTo(version)); Assert.That(state.SubmittedDish, Is.Null); Assert.That(state.TastingDishware, Is.Null);
            Assert.That(state.TastingServed, Is.Empty); Assert.That(state.Served, Is.EqualTo(food)); Assert.That(state.CurrentDishware.Id, Is.EqualTo("small_flat"));
            Assert.That(state.PlateSaltDoses, Is.EqualTo(2)); Assert.That(state.PlateOilDoses, Is.EqualTo(1)); Assert.That(state.PlateContaminated, Is.True);
            Assert.That(state.HeldDishware.Id, Is.EqualTo("bowl")); Assert.That(state.TryReturnDishware(out _), Is.True);
            Assert.That(state.CaptureDish().Portions.Select(p => p.Id), Is.EqualTo(working.Portions.Select(p => p.Id))); Conserved();
        }

        [Test] public void DiscardingSpareSupplyPlateDoesNotResetWorkingDishPenaltyOrDiscardItsFood()
        {
            Assert.That(state.TryTakeDishware("bowl", out _), Is.True); Assert.That(state.TryPlaceDishware(out _), Is.True);
            var food = Filled(); var before = state.CaptureDish(); Assert.That(before.PresentationPenalty, Is.EqualTo(1));
            Assert.That(state.TryTakeDishware("small_flat", out _), Is.True); int count = state.Portions.Count;
            Assert.That(state.TryDiscardHeldDishware(out _), Is.True); Assert.That(state.HeldDishware, Is.Null);
            Assert.That(state.PresentationPenalty, Is.EqualTo(1)); Assert.That(state.CurrentDishware.Id, Is.EqualTo("bowl")); Assert.That(state.Served, Is.EqualTo(food));
            Assert.That(state.Portions.Count, Is.EqualTo(count)); Assert.That(food.All(p => p.Location == PortionLocation.Station), Is.True);
            Assert.That(food.Any(p => p.Operations.Any(o => o.Action == "discarded_with_dishware")), Is.False);
            Assert.That(state.PlateSaltDoses, Is.EqualTo(2)); Assert.That(state.PlateOilDoses, Is.EqualTo(1)); Assert.That(state.PlateContaminated, Is.True);
            Assert.That(before.Portions.Select(p => p.Id), Is.EqualTo(food.Select(p => p.Id))); Assert.That(before.PresentationPenalty, Is.EqualTo(1)); Conserved();
        }

        [Test] public void PlacementRejectsAHandIngredientAndOccupiedStandWithoutAnyPartialTransfer()
        {
            Assert.That(state.TryPlaceTastingDish(out _), Is.False); var hand = Hand(beef); var version = state.Version;
            Assert.That(state.TryPlaceTastingDish(out _), Is.False); Assert.That(state.Version, Is.EqualTo(version)); Assert.That(state.Held, Is.SameAs(hand));
            Assert.That(state.TryPlaceServing(out _), Is.True); var submitted = SubmitToStand(); version = state.Version;
            Assert.That(state.TryPlaceTastingDish(out _), Is.False); Assert.That(state.TrySubmitDish(out _), Is.False); Assert.That(state.TryResetSubmission(out _), Is.False);
            Assert.That(state.Version, Is.EqualTo(version)); Assert.That(state.SubmittedDish, Is.SameAs(submitted)); Assert.That(state.TastingServed.Single(), Is.SameAs(hand)); Conserved();
        }

        [Test] public void PauseOccupiedHandBasketAndDisposedRunRejectRetakeAndDiscardWithoutLoss()
        {
            Filled(); state.TryTakePlacedDishware(out _); run.SetPaused(true); var version = state.Version;
            Assert.That(state.TryPlaceTastingDish(out _), Is.False); Assert.That(state.TryDiscardHeldDishware(out _), Is.False); Assert.That(state.Version, Is.EqualTo(version));
            run.SetPaused(false); Assert.That(state.TryPlaceTastingDish(out _), Is.True); var submitted = state.SubmittedDish;
            var item = Hand(egg); version = state.Version; Assert.That(state.TryTakeTastingDish(out _), Is.False); Assert.That(state.Held, Is.SameAs(item)); Assert.That(state.Version, Is.EqualTo(version));
            state.TryCancelHeld(out _); state.TryMoveBasket(BasketPlacement.Carried, out _); Assert.That(state.TryTakeTastingDish(out _), Is.False);
            state.TryMoveBasket(BasketPlacement.Station, out _); run.SetPaused(true); Assert.That(state.TryTakeTastingDish(out _), Is.False); Assert.That(state.SubmittedDish, Is.SameAs(submitted));
            run.SetPaused(false); Assert.That(state.TryTakeTastingDish(out _), Is.True); run.Dispose(); version = state.Version;
            Assert.That(state.TryDiscardHeldDishware(out _), Is.False); Assert.That(state.TryPlaceTastingDish(out _), Is.False); Assert.That(state.TryPlaceDishware(out _), Is.False);
            Assert.That(state.Version, Is.EqualTo(version)); Assert.That(state.HeldServed.Count, Is.EqualTo(2)); Conserved();
        }

        [TestCase(false)] [TestCase(true)] public void TimeoutCapturesOnlyWorkingPlateAndNeverHeldOrApplianceFood(bool carried)
        {
            var food = Filled(); var unplated = Hand(egg); state.TryPlaceCooker(CookerKind.Pot, out _);
            if (carried) Assert.That(state.TryTakePlacedDishware(out _), Is.True);
            run.SetRemaining(.01f); run.Tick(1); var submitted = state.SubmittedDish;
            Assert.That(submitted.IsSubmitted, Is.True); Assert.That(submitted.Quantity, Is.EqualTo(carried ? 0 : 2)); Assert.That(state.TastingDishware, Is.Null);
            Assert.That(submitted.Portions.Select(p => p.Id), Does.Not.Contain(unplated.Id)); Assert.That(unplated.Location, Is.EqualTo(PortionLocation.Appliance));
            Assert.That(state.TryTakeTastingDish(out _), Is.False); Assert.That(state.TryPlaceTastingDish(out _), Is.False); Assert.That(state.TryDiscardHeldDishware(out _), Is.False);
            if (carried) Assert.That(state.HeldServed, Is.EqualTo(food)); else Assert.That(state.Served, Is.EqualTo(food)); Conserved();
        }

        [Test] public void TimeoutKeepsAlreadyAssessedStandSnapshotAndNeverSubmitsOrRetrievesAgain()
        {
            Filled(); int submissions = 0;
            using (run.Events.Subscribe<DishChanged>(fact => { if (fact.Action == "dish_submitted") submissions++; }))
            {
                var submitted = SubmitToStand(); run.SetRemaining(.01f); run.Tick(1); run.Tick(100);
                Assert.That(submissions, Is.EqualTo(1)); Assert.That(state.SubmittedDish, Is.SameAs(submitted)); Assert.That(state.TastingServed.Count, Is.EqualTo(2));
                Assert.That(state.TryTakeTastingDish(out _), Is.False); Assert.That(state.TryResetSubmission(out _), Is.False); Conserved();
            }
        }
    }
}
