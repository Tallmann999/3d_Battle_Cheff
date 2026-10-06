using System;
using System.Linq;
using ChefShow.Core;
using ChefShow.Data;
using ChefShow.Inventory;
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
            sack = ScriptableObject.CreateInstance<IngredientDefinition>(); sack.Id = "potato_sack"; sack.Contents = potato;
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
                + all.Count(p => p.Location == PortionLocation.Returned || p.Location == PortionLocation.Trash)));
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
