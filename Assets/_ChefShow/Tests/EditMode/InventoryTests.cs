using System;
using System.Linq;
using ChefShow.Core;
using ChefShow.Data;
using ChefShow.Inventory;
using ChefShow.Ingredients;
using NUnit.Framework;
using UnityEngine;

namespace ChefShow.Tests
{
    public sealed class InventoryTests
    {
        private PrototypeRun run;
        private IngredientDefinition potato, flour, sack;
        private InventoryState state;
        [SetUp]
        public void Setup()
        {
            run = new PrototypeRun(240, 1005, null); state = run.Inventory;
            potato = ScriptableObject.CreateInstance<IngredientDefinition>(); potato.Id = "potato"; potato.CanUseBoard = true;
            flour = ScriptableObject.CreateInstance<IngredientDefinition>(); flour.Id = "flour";
            sack = ScriptableObject.CreateInstance<IngredientDefinition>(); sack.Id = "potato_sack"; sack.Contents = potato; sack.ContentsQuantity = 5;
        }
        [TearDown]
        public void Cleanup()
        {
            run.Dispose(); UnityEngine.Object.DestroyImmediate(potato); UnityEngine.Object.DestroyImmediate(flour); UnityEngine.Object.DestroyImmediate(sack);
        }
        private void Move(BasketPlacement place) => Assert.That(state.TryMoveBasket(place, out var error), Is.True, error);
        private void Fill(IngredientDefinition ingredient, int count)
        { for (int i = 0; i < count; i++) Assert.That(state.TryCollect(ingredient, out var error), Is.True, error); }
        private void Unload() => Assert.That(state.TryUnload(out var error), Is.True, error);
        private void Conserved()
        {
            var all = state.Portions;
            Assert.That(all.Select(p => p.Id).Distinct().Count(), Is.EqualTo(all.Count));
            Assert.That(all.Count(p => p.Location == PortionLocation.Basket), Is.EqualTo(state.Basket.Count));
            Assert.That(all.Count(p => p.Location == PortionLocation.Tray), Is.EqualTo(state.Tray.Count));
            Assert.That(all.Count(p => p.Location == PortionLocation.Hand), Is.EqualTo(state.Held == null ? 0 : 1));
            Assert.That(all.Count(p => p.Location == PortionLocation.Station), Is.EqualTo(new[] { state.Socket(0), state.Socket(1) }.Count(p => p != null)));
            Assert.That(all.Count, Is.EqualTo(state.Basket.Count + state.Tray.Count + (state.Held == null ? 0 : 1)
                + new[] { state.Socket(0), state.Socket(1) }.Count(p => p != null)
                + all.Count(p => p.Location == PortionLocation.Returned || p.Location == PortionLocation.Trash || p.Location == PortionLocation.Unpacked)));
        }

        [Test]
        public void InventoryFactsCarryActorRunTimeAndImmutableLocation()
        {
            InventoryChanged taken = default;
            using (run.Events.Subscribe<InventoryChanged>(fact => { if (fact.Action == "ingredient_taken") taken = fact; }))
            {
                run.Tick(1); Move(BasketPlacement.Carried); Fill(potato, 1);
                Move(BasketPlacement.Station); Unload();
                Assert.That(taken.RunId, Is.EqualTo(run.RunId)); Assert.That(taken.ActorId, Is.EqualTo("A1"));
                Assert.That(taken.RoundId, Is.EqualTo("prototype")); Assert.That(taken.Team, Is.EqualTo(TeamId.A));
                Assert.That(taken.SimulationTime, Is.EqualTo(1)); Assert.That(taken.Count, Is.EqualTo(1));
                Assert.That(taken.Location, Is.EqualTo(PortionLocation.Basket));
                Assert.That(state.Tray[0].Location, Is.EqualTo(PortionLocation.Tray)); Conserved();
            }
        }
        [Test]
        public void FoodHistoryAndSnapshotSurviveTransfersWithoutAliasingLiveData()
        {
            Move(BasketPlacement.Carried); run.Tick(1); Fill(potato, 1);
            var portion = state.Basket[0]; var taken = portion.Snapshot();
            Move(BasketPlacement.Station); Unload(); run.Tick(2);
            Assert.That(state.TryTakeTray(0, out _), Is.True);
            Assert.That(state.TryPlaceSocket(0, out _), Is.True);
            Assert.That(state.TryTakeSocket(0, out _), Is.True);
            Assert.That(state.TryCancelHeld(out _), Is.True);
            var prepared = portion.Snapshot();
            Assert.That(state.TryTakeSocket(0, out _), Is.True);
            Assert.That(state.TryRemove(false, out _), Is.True);
            Assert.That(taken.Location, Is.EqualTo(PortionLocation.Basket));
            Assert.That(taken.Operations.Count, Is.EqualTo(1));
            Assert.That(taken.Operations[0].SimulationTime, Is.EqualTo(1));
            Assert.That(prepared.Location, Is.EqualTo(PortionLocation.Station));
            Assert.That(prepared.Operations.Count, Is.EqualTo(6));
            Assert.That(portion.Location, Is.EqualTo(PortionLocation.Trash));
            Assert.That(portion.Operations.Count, Is.EqualTo(8));
            Assert.That(portion.Id, Is.EqualTo(taken.Id));
            Assert.That(portion.Quantity, Is.EqualTo(1));
            Assert.That(portion.Preparation, Is.EqualTo(PreparationState.Whole));
            Assert.That(portion.Cooking, Is.EqualTo(CookState.Raw));
            Assert.That(portion.OriginComponents[0].SourcePortionId, Is.EqualTo(taken.Id));
            Assert.That(portion.OriginComponents[0].IngredientId, Is.EqualTo("potato"));
            potato.Id = "changed_definition";
            Assert.That(prepared.IngredientId, Is.EqualTo("potato"));
            Assert.That(prepared.OriginComponents[0].IngredientId, Is.EqualTo("potato"));
            Conserved();
        }

        [Test]
        public void SixFastCutsPreserveFoodStateProgressAndEmitCompletionOnce()
        {
            Move(BasketPlacement.Carried); Fill(potato, 1); Move(BasketPlacement.Station); Unload();
            state.TryTakeTray(0, out _); state.TryPlaceSocket(0, out _); var food = state.Socket(0);
            // Seed an independent cooking/hygiene state; cutting must not erase it.
            typeof(FoodPortion).GetProperty(nameof(FoodPortion.Cooking)).SetValue(food, CookState.Cooked);
            typeof(FoodPortion).GetProperty(nameof(FoodPortion.HeatProgress)).SetValue(food, .7f);
            typeof(FoodPortion).GetProperty(nameof(FoodPortion.Contaminated)).SetValue(food, true);
            typeof(FoodPortion).GetProperty(nameof(FoodPortion.SaltDoses)).SetValue(food, 2);
            var before = food.Snapshot(); var facts = new System.Collections.Generic.List<PreparationChanged>();
            bool duplicateCompletionRejected = false;
            using (run.Events.Subscribe<PreparationChanged>(fact =>
            {
                facts.Add(fact);
                if (fact.Action == "preparation_completed") duplicateCompletionRejected = !state.TryChop(KitchenToolKind.Knife, out _);
            }))
            {
                for (int i = 0; i < 3; i++) { run.Tick(.001f); Assert.That(state.TryChop(KitchenToolKind.Knife, out var reason), Is.True, reason); }
                var halfway = food.Snapshot();
                Assert.That(state.TryTakeSocket(0, out _), Is.True); Assert.That(state.TryPutInTray(out _), Is.True);
                Assert.That(state.TryTakeTray(0, out _), Is.True); Assert.That(state.TryPlaceSocket(0, out _), Is.True);
                for (int i = 0; i < 3; i++) { run.Tick(.001f); Assert.That(state.TryChop(KitchenToolKind.Knife, out var reason), Is.True, reason); }
                int version = state.Version;
                Assert.That(state.TryChop(KitchenToolKind.Knife, out _), Is.False); Assert.That(state.Version, Is.EqualTo(version));
                Assert.That(facts.Count, Is.EqualTo(6)); Assert.That(facts.Count(f => f.Action == "preparation_completed"), Is.EqualTo(1));
                Assert.That(duplicateCompletionRejected, Is.True);
                Assert.That(facts[0].RunId, Is.EqualTo(run.RunId)); Assert.That(facts[0].ActorId, Is.EqualTo("A1"));
                Assert.That(facts[0].Presses, Is.EqualTo(1)); Assert.That(facts[0].Preparation, Is.EqualTo(PreparationState.Whole));
                Assert.That(facts[5].Preparation, Is.EqualTo(PreparationState.Chopped));
                Assert.That(before.ChopPresses, Is.Zero); Assert.That(halfway.ChopPresses, Is.EqualTo(3));
                Assert.That(food.Id, Is.EqualTo(before.Id)); Assert.That(food.Quantity, Is.EqualTo(before.Quantity));
                Assert.That(food.ChopPresses, Is.EqualTo(6)); Assert.That(food.Preparation, Is.EqualTo(PreparationState.Chopped));
                Assert.That(food.Cooking, Is.EqualTo(CookState.Cooked)); Assert.That(food.HeatProgress, Is.EqualTo(.7f));
                Assert.That(food.Contaminated, Is.True); Assert.That(food.SaltDoses, Is.EqualTo(2));
                Assert.That(food.OriginComponents.Count, Is.EqualTo(1));
                Assert.That(food.Operations.Count(o => o.Action == "preparation_completed"), Is.EqualTo(1)); Conserved();
            }
        }

        [Test]
        public void InvalidPausedTimedOutAndDisposedCutCommandsKeepActualPartialState()
        {
            Assert.That(state.TryChop(KitchenToolKind.Knife, out _), Is.False);
            Move(BasketPlacement.Carried); Fill(potato, 1); Move(BasketPlacement.Station); Unload();
            state.TryTakeTray(0, out _); var food = state.Held;
            Assert.That(state.TryChop(KitchenToolKind.Knife, out _), Is.False);
            state.TryPlaceSocket(0, out _);
            Assert.That(state.TryChop(KitchenToolKind.Fork, out _), Is.False);
            run.SetPaused(true); Assert.That(state.TryChop(KitchenToolKind.Knife, out _), Is.False); run.Tick(10);
            run.SetPaused(false); Assert.That(state.TryChop(KitchenToolKind.Knife, out _), Is.True);
            state.TryTakeSocket(0, out _); state.TryPutInTray(out _);
            Assert.That(state.TryChop(KitchenToolKind.Knife, out _), Is.False);
            state.TryTakeTray(0, out _); state.TryPlaceSocket(0, out _);
            run.SetRemaining(0); int version = state.Version;
            Assert.That(state.TryChop(KitchenToolKind.Knife, out _), Is.False);
            run.Dispose(); Assert.That(state.TryChop(KitchenToolKind.Knife, out _), Is.False);
            Assert.That(state.Version, Is.EqualTo(version)); Assert.That(food.ChopPresses, Is.EqualTo(1));
            Assert.That(food.Preparation, Is.EqualTo(PreparationState.Whole)); Conserved();
        }

        [TestCase(5)]
        [TestCase(6)]
        public void UnpackingCreatesExactSeparateContentsOnceWithImmutableOriginsAndHistory(int count)
        {
            state = new InventoryState(run, 10, count); sack.ContentsQuantity = count;
            Move(BasketPlacement.Carried); Fill(sack, 1); var package = state.Basket[0];
            var packed = package.Snapshot(); sack.ContentsQuantity = count + 100;
            Move(BasketPlacement.Station); Unload();
            typeof(FoodPortion).GetProperty(nameof(FoodPortion.Contaminated)).SetValue(package, true);
            PackageUnpacked recorded = default; int events = 0;
            using (run.Events.Subscribe<PackageUnpacked>(fact =>
            {
                recorded = fact; events++;
                Assert.That(state.TryUnpack(0, out _), Is.False, "Повтор из callback не должен создавать второй набор.");
            }))
            {
                Assert.That(state.TryUnpack(0, out var error), Is.True, error);
                Assert.That(state.Tray.Count, Is.EqualTo(count)); Assert.That(state.Socket(1), Is.Null);
                Assert.That(package.Location, Is.EqualTo(PortionLocation.Unpacked));
                Assert.That(state.Tray.All(p => p.Quantity == 1 && p.Ingredient == potato && p.PackedIngredient == null), Is.True);
                Assert.That(state.Tray.All(p => p.Contaminated && p.Preparation == PreparationState.Whole && p.Cooking == CookState.Raw), Is.True);
                Assert.That(state.Tray.All(p => p.OriginComponents.Single().SourcePortionId == package.Id), Is.True);
                Assert.That(state.Tray.Sum(p => p.OriginComponents.Single().Quantity), Is.EqualTo(count));
                Assert.That(state.Tray.All(p => p.Operations.Any(o => o.Action == "basket_unloaded")
                    && p.Operations.Count(o => o.Action == "package_unpacked") == 1), Is.True);
                Assert.That(recorded.PackageId, Is.EqualTo(package.Id)); Assert.That(recorded.Quantity, Is.EqualTo(count));
                Assert.That(recorded.ActorId, Is.EqualTo("A1")); Assert.That(recorded.Contents.Count, Is.EqualTo(count));
                Assert.That(packed.Location, Is.EqualTo(PortionLocation.Basket)); Assert.That(packed.PackedQuantity, Is.EqualTo(count));
                var child = state.Tray[0]; state.TryTakeTray(0, out _); state.TryPlaceSocket(0, out _);
                Assert.That(state.TryChop(KitchenToolKind.Knife, out _), Is.True);
                Assert.That(recorded.Contents[0].Location, Is.EqualTo(PortionLocation.Tray));
                Assert.That(recorded.Contents[0].ChopPresses, Is.Zero); Assert.That(child.ChopPresses, Is.EqualTo(1));
                Assert.That(state.TryUnpack(0, out _), Is.False); Assert.That(events, Is.EqualTo(1)); Conserved();
            }
        }

        [Test]
        public void UnpackingRejectsFullTrayHeldPausedTimeoutAndDisposedWithoutConsumingPackage()
        {
            state = new InventoryState(run, 10, 5);
            Assert.That(state.TryUnpack(-1, out _), Is.False);
            Move(BasketPlacement.Carried); Fill(sack, 1); Fill(potato, 1); Move(BasketPlacement.Station); Unload();
            var package = state.Tray[0]; int version = state.Version; var trayIds = state.Tray.Select(p => p.Id).ToArray();
            Assert.That(state.TryUnpack(0, out _), Is.False); Assert.That(state.Version, Is.EqualTo(version));
            CollectionAssert.AreEqual(trayIds, state.Tray.Select(p => p.Id)); Assert.That(state.Tray[0], Is.SameAs(package));
            state.TryTakeTray(1, out _); Assert.That(state.TryUnpack(0, out _), Is.False);
            state.TryRemove(false, out _); run.SetPaused(true); Assert.That(state.TryUnpack(0, out _), Is.False);
            run.SetPaused(false); run.SetRemaining(0); Assert.That(state.TryUnpack(0, out _), Is.False);
            run.Dispose(); Assert.That(state.TryUnpack(0, out _), Is.False);
            Assert.That(package.Location, Is.EqualTo(PortionLocation.Tray));
            Assert.That(package.Operations.Any(o => o.Action == "package_unpacked"), Is.False); Conserved();
        }

        [Test]
        public void NonPackageAndMalformedPackageCannotProduceContents()
        {
            Move(BasketPlacement.Carried); Fill(potato, 1); Move(BasketPlacement.Station); Unload();
            var whole = state.Tray[0]; Assert.That(state.TryUnpack(0, out _), Is.False); Assert.That(state.Tray[0], Is.SameAs(whole));
            state.TryTakeTray(0, out _); state.TryRemove(false, out _);
            Move(BasketPlacement.Carried); sack.ContentsQuantity = 0;
            Assert.That(state.TryCollect(sack, out _), Is.False); Assert.That(state.Basket, Is.Empty);
            sack.ContentsQuantity = 5; sack.Contents = sack;
            Assert.That(state.TryCollect(sack, out _), Is.False); Assert.That(state.Basket, Is.Empty); Conserved();
        }

        [Test]
        public void DishAreaAcceptsExperimentalFoodWithoutFixingItAndRejectsPackagesAndContainers()
        {
            Move(BasketPlacement.Carried); Fill(potato, 1); Fill(sack, 1); Move(BasketPlacement.Station); Unload();
            state.TryTakeTray(0, out _); var food = state.Held; var snapshot = food.Snapshot();
            Assert.That(state.TryPlaceSocket(1, out _), Is.True);
            Assert.That(food.Cooking, Is.EqualTo(CookState.Raw));
            state.TryTakeSocket(1, out _); state.TryPlaceSocket(0, out _);
            for (int i = 0; i < 6; i++) state.TryChop(KitchenToolKind.Knife, out _);
            state.TryTakeSocket(0, out _);
            typeof(FoodPortion).GetProperty(nameof(FoodPortion.Cooking)).SetValue(food, CookState.Burned);
            typeof(FoodPortion).GetProperty(nameof(FoodPortion.Contaminated)).SetValue(food, true);
            Assert.That(state.TryPlaceSocket(1, out _), Is.True);
            Assert.That(food.Contaminated, Is.True); Assert.That(food.Cooking, Is.EqualTo(CookState.Burned));
            Assert.That(food.Id, Is.EqualTo(snapshot.Id)); Assert.That(food.ChopPresses, Is.EqualTo(6));
            Assert.That(snapshot.Cooking, Is.EqualTo(CookState.Raw)); Assert.That(snapshot.Contaminated, Is.False);
            state.TryTakeSocket(1, out _); state.TryPutInTray(out _);
            state.TryTakeTray(0, out _); var package = state.Held; int version = state.Version;
            Assert.That(state.TryPlaceSocket(1, out var reason), Is.False); Assert.That(reason, Does.Contain("Упаковки"));
            Assert.That(state.Held, Is.SameAs(package)); Assert.That(state.Version, Is.EqualTo(version));
            Assert.That(InventoryState.CanPlaceOnServingSurface(package), Is.False); state.TryCancelHeld(out _);
            var salt = ScriptableObject.CreateInstance<IngredientDefinition>(); salt.Id="salt"; salt.IsDoseContainer=true;
            try
            {
                Move(BasketPlacement.Carried); Fill(salt, 1); Move(BasketPlacement.Station); Unload();
                state.TryTakeTray(2, out _); var source=state.Held;
                Assert.That(state.TryPlaceSocket(1, out _), Is.False); Assert.That(state.Held, Is.SameAs(source));
                state.TryCancelHeld(out _); Conserved();
            }
            finally { UnityEngine.Object.DestroyImmediate(salt); }
        }

        [Test]
        public void UnpackingReusesPackageSlotAndPreservesNeighbourOrderAtExactCapacity()
        {
            state = new InventoryState(run, 10, 7);
            Move(BasketPlacement.Carried); Fill(potato, 1); Fill(sack, 1); Fill(flour, 1); Move(BasketPlacement.Station); Unload();
            var before=state.Tray[0]; var after=state.Tray[2]; var package=state.Tray[1];
            Assert.That(state.TryUnpack(1, out var error), Is.True, error);
            Assert.That(state.Tray.Count, Is.EqualTo(7)); Assert.That(state.Tray[0], Is.SameAs(before));
            Assert.That(state.Tray[6], Is.SameAs(after)); Assert.That(state.Tray.Skip(1).Take(5)
                .All(p=>p.OriginComponents.Single().SourcePortionId==package.Id), Is.True);
            Assert.That(state.TryUnpack(1, out _), Is.False); Assert.That(state.Tray.Count, Is.EqualTo(7)); Conserved();
        }

        [Test]
        public void TenItemsIncludeRepeatedPotatoesAndOneWholePackage()
        {
            Assert.That(state.TryCollect(potato, out _), Is.False);
            Move(BasketPlacement.Carried); Fill(potato, 5); Fill(sack, 1); Fill(flour, 4);
            var ids = state.Basket.Select(p => p.Id).ToArray();
            Assert.That(state.TryCollect(potato, out _), Is.False);
            CollectionAssert.AreEqual(ids, state.Basket.Select(p => p.Id));
            Assert.That(state.Basket.Count(p => p.Ingredient == sack), Is.EqualTo(1)); Conserved();
        }
        [Test]
        public void TwoTripsUnloadTwentyAndThirdTripFailsWithoutLosingAnyItem()
        {
            for (int trip = 0; trip < 2; trip++) { Move(BasketPlacement.Carried); Fill(potato, 10); Move(BasketPlacement.Station); Unload(); }
            Assert.That(state.Tray.Count, Is.EqualTo(20));
            Move(BasketPlacement.Carried); Fill(flour, 10); Move(BasketPlacement.Station);
            var basketIds = state.Basket.Select(p => p.Id).ToArray(); var trayIds = state.Tray.Select(p => p.Id).ToArray();
            Assert.That(state.TryUnload(out _), Is.False);
            CollectionAssert.AreEqual(basketIds, state.Basket.Select(p => p.Id)); CollectionAssert.AreEqual(trayIds, state.Tray.Select(p => p.Id)); Conserved();
        }
        [Test]
        public void BasketCanBePlacedAndPickedWithContentsAndDroppedWithoutLosingThem()
        {
            Move(BasketPlacement.Carried); Fill(potato, 5);
            var ids = state.Basket.Select(p => p.Id).ToArray();
            Move(BasketPlacement.Floor); Assert.That(state.TryUnload(out _), Is.False);
            Move(BasketPlacement.Carried); Move(BasketPlacement.Pantry);
            Assert.That(state.TryCollect(potato, out _), Is.False);
            Move(BasketPlacement.Carried); Move(BasketPlacement.Station);
            CollectionAssert.AreEqual(ids, state.Basket.Select(p => p.Id)); Unload(); Conserved();
        }
        [Test]
        public void RejectedSocketPlacementKeepsHeldItemAndCancelRestoresOrigin()
        {
            Move(BasketPlacement.Carried); Fill(flour, 1); Fill(potato, 2); Move(BasketPlacement.Station); Unload();
            Assert.That(state.TryTakeTray(0, out _), Is.True); var held = state.Held;
            Assert.That(state.TryPlaceSocket(0, out _), Is.False); Assert.That(state.Held, Is.SameAs(held));
            Assert.That(state.TryCancelHeld(out _), Is.True); Assert.That(state.Tray[0], Is.SameAs(held));
            Assert.That(state.TryTakeTray(1, out _), Is.True); Assert.That(state.TryPlaceSocket(0, out _), Is.True);
            Assert.That(state.TryTakeTray(1, out _), Is.True); held = state.Held;
            Assert.That(state.TryPlaceSocket(0, out _), Is.False); Assert.That(state.Held, Is.SameAs(held));
            Assert.That(state.TryCancelHeld(out _), Is.True); Conserved();
        }
        [Test]
        public void RoundTripFromSocketAndExplicitReturnTrashAccountForAllItems()
        {
            Move(BasketPlacement.Carried); Fill(potato, 3); Move(BasketPlacement.Station); Unload();
            var first = state.Tray[0];
            Assert.That(state.TryTakeTray(0, out _), Is.True); Assert.That(state.TryPlaceSocket(0, out _), Is.True);
            Assert.That(state.TryTakeSocket(0, out _), Is.True); Assert.That(state.TryCancelHeld(out _), Is.True);
            Assert.That(state.Socket(0), Is.SameAs(first));
            Assert.That(state.TryTakeSocket(0, out _), Is.True); Assert.That(state.TryPutInTray(out _), Is.True);
            Assert.That(state.TryTakeTray(0, out _), Is.True); Assert.That(state.TryRemove(true, out _), Is.True);
            Assert.That(state.TryTakeTray(0, out _), Is.True); Assert.That(state.TryRemove(false, out _), Is.True);
            Assert.That(state.Portions.Count(p => p.Location == PortionLocation.Returned), Is.EqualTo(1));
            Assert.That(state.Portions.Count(p => p.Location == PortionLocation.Trash), Is.EqualTo(1)); Conserved();
        }
        [Test]
        public void PausedTimedOutAndDisposedRunsRejectMutationsAndOldRunIdsDiffer()
        {
            Move(BasketPlacement.Carried); Fill(potato, 1); string oldId = state.Basket[0].Id;
            run.SetPaused(true); Assert.That(state.TryCollect(potato, out _), Is.False); Assert.That(state.TryMoveBasket(BasketPlacement.Floor, out _), Is.False);
            run.SetPaused(false); run.SetRemaining(0); Assert.That(state.TryCollect(potato, out _), Is.False);
            run.Dispose(); Assert.That(state.TryMoveBasket(BasketPlacement.Station, out _), Is.False);
            using (var next = new PrototypeRun(240, 1005, null))
            {
                Assert.That(next.Inventory.Basket.Count, Is.Zero);
                next.Inventory.TryMoveBasket(BasketPlacement.Carried, out _); next.Inventory.TryCollect(potato, out _);
                Assert.That(next.Inventory.Basket[0].Id, Is.Not.EqualTo(oldId));
            }
            Conserved();
        }
    }
}
