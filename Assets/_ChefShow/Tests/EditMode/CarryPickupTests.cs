using System.Collections.Generic;
using System.Linq;
using ChefShow.Cooking;
using ChefShow.Core;
using ChefShow.Data;
using ChefShow.Ingredients;
using ChefShow.Inventory;
using NUnit.Framework;
using UnityEngine;

namespace ChefShow.Tests
{
    public sealed class CarryPickupTests
    {
        private PrototypeRun run;
        private InventoryState state;
        private IngredientDefinition beef, potato, egg, salt, oil, sack;
        private readonly List<IngredientDefinition> definitions = new List<IngredientDefinition>();
        private DishwareSettings dishware;
        private CookingSettings cooking;

        [SetUp] public void Setup()
        {
            beef = Ingredient("beef", true); potato = Ingredient("potato", true); egg = Ingredient("egg");
            salt = Ingredient("salt"); salt.IsDoseContainer = true;
            oil = Ingredient("oil"); oil.IsDoseContainer = true;
            sack = Ingredient("potato_sack"); sack.Contents = potato; sack.ContentsQuantity = 5;
            dishware = new DishwareSettings(new[] { DishwareSnapshot.Default,
                new DishwareSnapshot("bowl", "Bowl", 10, .61f, .18f, true, Color.green) }, "small_flat");
            cooking = new CookingSettings(3, 30, 45, 60, .55f, 1, 1.75f, 3);
            StartRun();
        }
        [TearDown] public void Cleanup()
        {
            run?.Dispose(); foreach (var definition in definitions) Object.DestroyImmediate(definition); definitions.Clear();
        }
        private IngredientDefinition Ingredient(string id, bool board = false)
        {
            var definition = ScriptableObject.CreateInstance<IngredientDefinition>();
            definition.Id = definition.DisplayName = id; definition.CanUseBoard = board;
            definitions.Add(definition); return definition;
        }
        private void StartRun(int trayCapacity = 24)
        {
            run?.Dispose(); run = new PrototypeRun(600, 1005, (type, error) => Assert.Fail(error.ToString()),
                trayCapacity: trayCapacity, cooking: cooking, dishware: dishware); state = run.Inventory;
        }
        private FoodPortion Direct(IngredientDefinition ingredient)
        {
            Assert.That(state.TryCollect(ingredient, out var reason), Is.True, reason);
            Assert.That(state.Held, Is.Not.Null); Assert.That(state.Held.Ingredient, Is.SameAs(ingredient));
            return state.Held;
        }
        private FoodPortion CookPlate(IngredientDefinition ingredient, bool chopped = false, bool burned = false)
        {
            var food = Direct(ingredient);
            if (chopped)
            {
                Assert.That(state.TryPlaceSocket(0, out _), Is.True);
                for (int i = 0; i < 6; i++) Assert.That(state.TryChop(KitchenToolKind.Knife, out _), Is.True);
                Assert.That(state.TryTakeSocket(0, out _), Is.True);
            }
            Assert.That(state.TryPlaceCooker(CookerKind.Pan, out _), Is.True);
            while (state.Heat(CookerKind.Pan) != HeatLevel.Medium) state.TryCycleHeat(CookerKind.Pan, out _);
            run.Tick(burned ? cooking.Burned : cooking.Ready);
            Assert.That(state.TryTakeCooker(CookerKind.Pan, 0, out _), Is.True);
            Assert.That(state.TryPlaceServing(out _), Is.True); return food;
        }
        private FoodPortion[] FilledPlate()
        {
            var first = CookPlate(beef);
            Assert.That(state.TryTakeServing(0, out _), Is.True); Assert.That(state.TryPlaceSocket(0, out _), Is.True);
            Direct(salt); Assert.That(state.TrySeasonBoard(out _), Is.True); Assert.That(state.TryCancelHeld(out _), Is.True);
            Assert.That(state.TryTakeSocket(0, out _), Is.True); Assert.That(state.TryPlaceServing(out _), Is.True);
            var second = CookPlate(potato, chopped: true, burned: true);
            var source = Direct(salt);
            // Hygiene actions are a later feature; seed only the dose source's contamination.
            typeof(FoodPortion).GetProperty(nameof(FoodPortion.Contaminated)).SetValue(source, true);
            Assert.That(state.TrySeasonPlate(out _), Is.True); Assert.That(state.TrySeasonPlate(out _), Is.True);
            Assert.That(state.TryCancelHeld(out _), Is.True);
            Direct(oil); Assert.That(state.TrySeasonPlate(out _), Is.True); Assert.That(state.TryCancelHeld(out _), Is.True);
            return new[] { first, second };
        }
        private void Conserved()
        {
            var active = state.Basket.Concat(state.Tray).Concat(state.Served).Concat(state.HeldServed)
                .Concat(state.Bowl).Concat(state.Cooker(CookerKind.Pan)).Concat(state.Cooker(CookerKind.Pot)).Concat(state.Cooker(CookerKind.Oven))
                .Concat(new[] { state.Held, state.Socket(0) }.Where(p => p != null)).ToArray();
            var archived = state.Portions.Where(p => p.Location == PortionLocation.Returned || p.Location == PortionLocation.Trash
                || p.Location == PortionLocation.Unpacked || p.Location == PortionLocation.Mixed).ToArray();
            Assert.That(active.Select(p => p.Id).Distinct().Count(), Is.EqualTo(active.Length));
            Assert.That(active.Length + archived.Length, Is.EqualTo(state.Portions.Count));
            Assert.That(state.Portions.Select(p => p.Id).Distinct().Count(), Is.EqualTo(state.Portions.Count));
            Assert.That(state.HeldServed.All(p => p.Location == PortionLocation.HeldDishware), Is.True);
        }

        [Test] public void FilledTakePutBackPreservesOwnershipOrderStatesDosesAndSnapshots()
        {
            var food = FilledPlate(); var before = state.CaptureDish(); var count = state.Portions.Count;
            Assert.That(state.TryTakePlacedDishware(out _), Is.True);
            Assert.That(state.CurrentDishware, Is.Null); Assert.That(state.HeldDishwareFromStation, Is.True);
            Assert.That(state.HeldDishware.Id, Is.EqualTo("small_flat")); Assert.That(state.Served, Is.Empty);
            Assert.That(state.HeldServed.Select(p => p.Id), Is.EqualTo(food.Select(p => p.Id)));
            Assert.That(state.HeldPlateSaltDoses, Is.EqualTo(2)); Assert.That(state.HeldPlateOilDoses, Is.EqualTo(1)); Assert.That(state.HeldPlateContaminated, Is.True);
            Assert.That(state.PlateSaltDoses + state.PlateOilDoses, Is.Zero); Assert.That(state.PlateContaminated, Is.False);
            Assert.That(state.CaptureDish().Quantity, Is.Zero); Assert.That(state.PresentationPenalty, Is.EqualTo(1)); Conserved();
            Assert.That(state.TryPlaceDishware(out _), Is.True);
            Assert.That(state.HeldDishware, Is.Null); Assert.That(state.HeldServed, Is.Empty);
            Assert.That(state.Served.Select(p => p.Id), Is.EqualTo(food.Select(p => p.Id)));
            Assert.That(state.PlateSaltDoses, Is.EqualTo(2)); Assert.That(state.PlateOilDoses, Is.EqualTo(1)); Assert.That(state.PlateContaminated, Is.True);
            Assert.That(state.PresentationPenalty, Is.EqualTo(1)); Assert.That(state.Portions.Count, Is.EqualTo(count));
            Assert.That(state.Served[0].Cooking, Is.EqualTo(CookState.Cooked)); Assert.That(state.Served[1].Cooking, Is.EqualTo(CookState.Burned));
            Assert.That(state.Served[0].SaltDoses, Is.EqualTo(1));
            for (int i = 0; i < food.Length; i++)
            {
                var after = state.Served[i].Snapshot(); var original = before.Portions[i];
                Assert.That(state.Served[i], Is.SameAs(food[i]));
                Assert.That(after.Quantity, Is.EqualTo(original.Quantity)); Assert.That(after.HeatProgress, Is.EqualTo(original.HeatProgress));
                Assert.That(after.LastCooker, Is.EqualTo(original.LastCooker)); Assert.That(after.Preparation, Is.EqualTo(original.Preparation));
                Assert.That(after.ChopPresses, Is.EqualTo(original.ChopPresses)); Assert.That(after.Cooking, Is.EqualTo(original.Cooking));
                Assert.That(after.StirPresses, Is.EqualTo(original.StirPresses)); Assert.That(after.RequiredStirs, Is.EqualTo(original.RequiredStirs));
                Assert.That(after.SaltDoses, Is.EqualTo(original.SaltDoses)); Assert.That(after.OilDoses, Is.EqualTo(original.OilDoses));
                Assert.That(after.Contaminated, Is.EqualTo(original.Contaminated)); Assert.That(after.Components, Is.EqualTo(original.Components));
                Assert.That(after.OriginComponents, Is.EqualTo(original.OriginComponents)); Assert.That(after.Operations, Is.EqualTo(original.Operations));
            }
            Assert.That(state.Served[1].ChopPresses, Is.EqualTo(6)); Assert.That(state.Served[1].Preparation, Is.EqualTo(PreparationState.Chopped));
            Assert.That(before.Portions.Select(p => p.Id), Is.EqualTo(food.Select(p => p.Id))); Assert.That(before.SaltDoses, Is.EqualTo(2));
            Assert.That(before.PresentationPenalty, Is.Zero); Assert.That(before.Portions.All(p => p.Location == PortionLocation.Station), Is.True);
            Assert.That(state.Portions.Any(p => p.Location == PortionLocation.Trash), Is.False);
            Assert.That(state.Portions.Any(p => p.Operations.Any(o => o.Action == "discarded_with_dishware")), Is.False); Conserved();
        }

        [Test] public void SafeReturnRestoresFilledPlateWhileExplicitShelfReturnRejectsWithoutLoss()
        {
            var food = FilledPlate(); state.TryTakePlacedDishware(out _); var version = state.Version;
            Assert.That(state.TryReturnDishware(out _, true), Is.False);
            Assert.That(state.Version, Is.EqualTo(version)); Assert.That(state.HeldServed.Select(p => p.Id), Is.EqualTo(food.Select(p => p.Id)));
            Assert.That(state.TryReturnDishware(out _), Is.True);
            Assert.That(state.CurrentDishware.Id, Is.EqualTo("small_flat")); Assert.That(state.HeldDishware, Is.Null);
            Assert.That(state.Served.Select(p => p.Id), Is.EqualTo(food.Select(p => p.Id))); Assert.That(state.PresentationPenalty, Is.EqualTo(1));
            Assert.That(state.PlateSaltDoses, Is.EqualTo(2)); Assert.That(state.PlateOilDoses, Is.EqualTo(1));
            Assert.That(state.TryReturnDishware(out _), Is.False); Assert.That(state.TryPlaceDishware(out _), Is.False); Conserved();
        }

        [Test] public void CarriedPlateOccupiesLeftHandAndPauseRejectsPutBackWithoutChangingContents()
        {
            var food = FilledPlate(); state.TryTakePlacedDishware(out _); var ids = food.Select(p => p.Id).ToArray();
            Assert.That(state.TryCollect(egg, out _), Is.False); Assert.That(state.TryTakeDishware("bowl", out _), Is.False);
            Assert.That(state.TryMoveBasket(BasketPlacement.Carried, out _), Is.False); Assert.That(state.TryRemove(false, out _), Is.False);
            Assert.That(state.TryTakeServing(0, out _), Is.False); Assert.That(state.Held, Is.Null);
            run.SetPaused(true); var version = state.Version;
            Assert.That(state.TryPlaceDishware(out _), Is.False); Assert.That(state.TryReturnDishware(out _), Is.False);
            run.Tick(100); Assert.That(state.Version, Is.EqualTo(version)); Assert.That(state.HeldServed.Select(p => p.Id), Is.EqualTo(ids));
            run.SetPaused(false); Assert.That(state.TryPlaceDishware(out _), Is.True); Assert.That(state.Served.Select(p => p.Id), Is.EqualTo(ids)); Conserved();
        }

        [Test] public void TimeupSubmitsOnlyStationContentsAndRetainsFilledPlateInHand()
        {
            var food = FilledPlate(); var before = state.CaptureDish(); state.TryTakePlacedDishware(out _);
            run.SetRemaining(.01f); run.Tick(1);
            Assert.That(state.SubmittedDish.IsSubmitted, Is.True); Assert.That(state.SubmittedDish.Dishware, Is.Null); Assert.That(state.SubmittedDish.Quantity, Is.Zero);
            Assert.That(state.HeldServed.Select(p => p.Id), Is.EqualTo(food.Select(p => p.Id))); Assert.That(state.HeldDishware, Is.Not.Null);
            Assert.That(state.TryPlaceDishware(out _), Is.False); Assert.That(state.TryReturnDishware(out _), Is.False);
            Assert.That(before.Quantity, Is.EqualTo(2)); Assert.That(state.HeldPlateSaltDoses, Is.EqualTo(2)); Conserved();
        }

        [Test] public void DirectPickupKeepsParkedBasketUntouchedAndCancelReturnsTheSameItemToPantry()
        {
            state.TryMoveBasket(BasketPlacement.Carried, out _); state.TryCollect(egg, out _); state.TryMoveBasket(BasketPlacement.Floor, out _);
            var basketIds = state.Basket.Select(p => p.Id).ToArray(); InventoryChanged taken = default;
            using (run.Events.Subscribe<InventoryChanged>(fact => { if (fact.Action == "ingredient_taken") taken = fact; }))
            {
                var item = Direct(potato); var version = state.Version;
                Assert.That(item.Quantity, Is.EqualTo(1)); Assert.That(item.Location, Is.EqualTo(PortionLocation.Hand));
                Assert.That(state.TryCollect(beef, out _), Is.False); Assert.That(state.Version, Is.EqualTo(version)); Assert.That(state.Held, Is.SameAs(item));
                Assert.That(state.Basket.Select(p => p.Id), Is.EqualTo(basketIds)); Assert.That(state.Placement, Is.EqualTo(BasketPlacement.Floor));
                Assert.That(taken.Location, Is.EqualTo(PortionLocation.Hand)); Assert.That(taken.Count, Is.EqualTo(1)); Assert.That(taken.PortionId, Is.EqualTo(item.Id));
                Assert.That(state.TryCancelHeld(out _), Is.True); Assert.That(state.Held, Is.Null); Assert.That(item.Location, Is.EqualTo(PortionLocation.Returned));
                Assert.That(taken.Location, Is.EqualTo(PortionLocation.Hand)); Assert.That(state.Tray, Is.Empty); Assert.That(state.Basket.Select(p => p.Id), Is.EqualTo(basketIds)); Conserved();
            }
        }

        [TestCase(true)] [TestCase(false)]
        public void DirectDoseContainerOrPackageIsOneWholeItemWithoutAutomaticUnpacking(bool package)
        {
            var item = Direct(package ? sack : salt);
            Assert.That(item.Quantity, Is.EqualTo(1)); Assert.That(state.Basket, Is.Empty); Assert.That(state.Tray, Is.Empty);
            Assert.That(item.Preparation, Is.EqualTo(PreparationState.Whole)); Assert.That(state.Portions.Count, Is.EqualTo(1));
            if (package) { Assert.That(item.PackedIngredient, Is.SameAs(potato)); Assert.That(item.PackedQuantity, Is.EqualTo(5)); }
            else Assert.That(item.Ingredient.IsDoseContainer, Is.True);
            Assert.That(state.TryPlaceServing(out _), Is.False); Assert.That(state.Held, Is.SameAs(item));
            Assert.That(state.TryCancelHeld(out _), Is.True); Assert.That(item.Location, Is.EqualTo(PortionLocation.Returned)); Conserved();
        }

        [Test] public void FullTrayCannotPreventDirectPickupOrSafePantryReturn()
        {
            StartRun(1); state.TryMoveBasket(BasketPlacement.Carried, out _); state.TryCollect(egg, out _);
            state.TryMoveBasket(BasketPlacement.Station, out _); state.TryUnload(out _); var original = state.Tray.Single();
            var item = Direct(potato); Assert.That(state.TryPutInTray(out _), Is.False); Assert.That(state.Held, Is.SameAs(item));
            Assert.That(state.TryCancelHeld(out _), Is.True); Assert.That(state.Tray.Single(), Is.SameAs(original));
            Assert.That(item.Location, Is.EqualTo(PortionLocation.Returned)); Assert.That(state.Basket, Is.Empty); Conserved();
        }

        [Test] public void CarriedBasketRetainsTenItemLimitAndParkedBasketDoesNotReceiveDirectExtra()
        {
            state.TryMoveBasket(BasketPlacement.Carried, out _);
            for (int i = 0; i < 10; i++) Assert.That(state.TryCollect(egg, out _), Is.True);
            var basketIds = state.Basket.Select(p => p.Id).ToArray(); Assert.That(state.Held, Is.Null);
            Assert.That(state.TryCollect(potato, out _), Is.False); Assert.That(state.Basket.Count, Is.EqualTo(10));
            state.TryMoveBasket(BasketPlacement.Station, out _); var extra = Direct(potato);
            Assert.That(state.Basket.Select(p => p.Id), Is.EqualTo(basketIds)); Assert.That(state.Basket.Count, Is.EqualTo(10));
            Assert.That(state.Held, Is.SameAs(extra)); Assert.That(state.TryCancelHeld(out _), Is.True); Conserved();
        }

        [Test] public void InvalidPausedExpiredAndDisposedDirectPickupNeverCreatesPortions()
        {
            Assert.That(state.TryCollect(null, out _), Is.False); sack.CanUseBoard = true;
            Assert.That(state.TryCollect(sack, out _), Is.False); sack.CanUseBoard = false;
            run.SetPaused(true); Assert.That(state.TryCollect(potato, out _), Is.False); run.SetPaused(false);
            run.SetRemaining(0); Assert.That(state.TryCollect(potato, out _), Is.False); run.Dispose();
            Assert.That(state.TryCollect(potato, out _), Is.False); Assert.That(state.Portions, Is.Empty); Assert.That(state.Held, Is.Null);
        }
    }
}
