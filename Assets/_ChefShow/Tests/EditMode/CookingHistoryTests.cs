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
    public sealed class CookingHistoryTests
    {
        private PrototypeRun run;
        private InventoryState state;
        private IngredientDefinition beef, egg, mixture;

        [SetUp] public void Setup()
        {
            beef = Ingredient("beef"); egg = Ingredient("egg"); mixture = Ingredient("mixture");
            run = new PrototypeRun(240, 1005, (type, error) => Assert.Fail(error.ToString()),
                cooking: new CookingSettings(3, 30, 45, 60, .55f, 1, 1.75f, 3));
            state = run.Inventory;
        }
        [TearDown] public void Cleanup()
        {
            run.Dispose();
            foreach (var definition in new[] { beef, egg, mixture }) Object.DestroyImmediate(definition);
        }
        private IngredientDefinition Ingredient(string id)
        {
            var definition = ScriptableObject.CreateInstance<IngredientDefinition>();
            definition.Id = id; definition.DisplayName = id; definition.CanUseBoard = true;
            return definition;
        }
        private FoodPortion Hand(IngredientDefinition definition)
        {
            Assert.That(state.TryMoveBasket(BasketPlacement.Carried, out _), Is.True);
            Assert.That(state.TryCollect(definition, out _), Is.True);
            Assert.That(state.TryMoveBasket(BasketPlacement.Station, out _), Is.True);
            Assert.That(state.TryUnload(out _), Is.True);
            Assert.That(state.TryTakeTray(state.Tray.Count - 1, out _), Is.True);
            return state.Held;
        }
        private FoodPortion Add(IngredientDefinition definition, CookerKind kind)
        {
            var food = Hand(definition);
            Assert.That(state.TryPlaceCooker(kind, out var reason), Is.True, reason);
            return food;
        }
        private void Medium(CookerKind kind)
        {
            Assert.That(state.TryCycleHeat(kind, out _), Is.True);
            Assert.That(state.TryCycleHeat(kind, out _), Is.True);
        }
        private FoodOperation[] Starts(FoodPortionSnapshot food)
            => food.Operations.Where(operation => operation.Action == "cook_started").ToArray();

        [Test] public void ChangingApplianceRecordsActualMethodOnceAndPreservesEarlierSnapshots()
        {
            var food = Add(beef, CookerKind.Pan); Medium(CookerKind.Pan); run.Tick(10);
            var panSnapshot = food.Snapshot(); var panStart = Starts(panSnapshot).Single();
            Assert.That(panStart.Cooker, Is.EqualTo(CookerKind.Pan));
            Assert.That(panStart.SourcePortionId, Is.EqualTo(food.Id));
            Assert.That(panStart.HeatProgress, Is.EqualTo(10));
            Assert.That(panStart.Cooking, Is.EqualTo(CookState.Cooking));
            Assert.That(state.TryTakeCooker(CookerKind.Pan, 0, out _), Is.True);
            Assert.That(state.TryPlaceCooker(CookerKind.Pot, out _), Is.True);
            run.Tick(4); // Transferring into an inactive appliance is not a cooking method.
            Assert.That(Starts(food.Snapshot()).Length, Is.EqualTo(1));
            Assert.That(food.LastCooker, Is.EqualTo(CookerKind.Pan));
            Medium(CookerKind.Pot); run.Tick(2); run.Tick(1);
            Assert.That(Starts(food.Snapshot()).Select(operation => operation.Cooker),
                Is.EqualTo(new CookerKind?[] { CookerKind.Pan, CookerKind.Pot }));
            Assert.That(Starts(food.Snapshot()).Last().HeatProgress, Is.EqualTo(12));
            Assert.That(food.HeatProgress, Is.EqualTo(13));
            Assert.That(state.TryStir(CookerKind.Pot, KitchenToolKind.Spatula, out _), Is.True);
            var stir = food.Operations.Last(operation => operation.Action == "food_stirred");
            Assert.That(stir.Cooker, Is.EqualTo(CookerKind.Pot));
            Assert.That(stir.SourcePortionId, Is.EqualTo(food.Id));
            Assert.That(stir.HeatProgress, Is.EqualTo(13));
            Assert.That(state.TryTakeCooker(CookerKind.Pot, 0, out _), Is.True);
            Assert.That(state.TryPlaceCooker(CookerKind.Pan, out _), Is.True); run.Tick(1);
            Assert.That(Starts(food.Snapshot()).Select(operation => operation.Cooker),
                Is.EqualTo(new CookerKind?[] { CookerKind.Pan, CookerKind.Pot, CookerKind.Pan }));
            Assert.That(panSnapshot.LastCooker, Is.EqualTo(CookerKind.Pan));
            Assert.That(panSnapshot.HeatProgress, Is.EqualTo(10));
            Assert.That(Starts(panSnapshot).Length, Is.EqualTo(1));
            var legacy = new FoodOperation("legacy", 1, PortionLocation.Hand, PreparationState.Whole);
            Assert.That(legacy.Cooker, Is.Null); Assert.That(legacy.SourcePortionId, Is.Null);
            Assert.That(legacy.Cooking, Is.EqualTo(CookState.Raw)); Assert.That(legacy.HeatProgress, Is.Zero);
        }

        [Test] public void OffApplianceOpenOvenAndBurnedFoodDoNotInventHeatMethods()
        {
            var food = Add(beef, CookerKind.Pan); run.Tick(1);
            Assert.That(Starts(food.Snapshot()), Is.Empty); Assert.That(food.LastCooker, Is.Null);
            state.TryTakeCooker(CookerKind.Pan, 0, out _); state.TryPlaceCooker(CookerKind.Oven, out _);
            Medium(CookerKind.Oven); run.Tick(1);
            Assert.That(Starts(food.Snapshot()), Is.Empty); Assert.That(food.HeatProgress, Is.Zero);
            Assert.That(state.TryToggleOvenDoor(out _), Is.True); run.Tick(1);
            Assert.That(Starts(food.Snapshot()).Single().Cooker, Is.EqualTo(CookerKind.Oven));
            state.TryCycleHeat(CookerKind.Oven, out _); state.TryCycleHeat(CookerKind.Oven, out _);
            run.Tick(1); Assert.That(food.HeatProgress, Is.EqualTo(1));
            Medium(CookerKind.Oven); run.Tick(74);
            Assert.That(food.Cooking, Is.EqualTo(CookState.Burned));
            var burned = food.Snapshot();
            Assert.That(burned.Operations.Single(operation => operation.Action == "food_burned").Cooker,
                Is.EqualTo(CookerKind.Oven));
            state.TryToggleOvenDoor(out _); state.TryTakeCooker(CookerKind.Oven, 0, out _);
            state.TryPlaceCooker(CookerKind.Pan, out _); Medium(CookerKind.Pan); run.Tick(1);
            Assert.That(food.HeatProgress, Is.EqualTo(75)); Assert.That(food.LastCooker, Is.EqualTo(CookerKind.Oven));
            Assert.That(Starts(food.Snapshot()).Length, Is.EqualTo(1));
            Assert.That(food.Cooking, Is.EqualTo(CookState.Burned));
            Assert.That(burned.HeatProgress, Is.EqualTo(75));
        }

        [Test] public void HeatFactsCaptureStateMethodAndSourceBeforeReentrantCallbacks()
        {
            var first = Add(beef, CookerKind.Pan); var second = Add(egg, CookerKind.Pan); Medium(CookerKind.Pan);
            var initialFacts = new List<CookingChanged>(); bool moved = false;
            using (run.Events.Subscribe<CookingChanged>(fact =>
            {
                if (fact.SimulationTime != 30 || fact.Food == null) return;
                initialFacts.Add(fact);
                if (moved) return;
                moved = true;
                Assert.That(first.Cooking, Is.EqualTo(CookState.Cooked));
                Assert.That(second.Cooking, Is.EqualTo(CookState.Cooked));
                Assert.That(state.TryTakeCooker(CookerKind.Pan, 0, out _), Is.True);
                Assert.That(state.TryPlaceCooker(CookerKind.Pot, out _), Is.True);
                Medium(CookerKind.Pot); run.Tick(1);
            })) run.Tick(30);
            Assert.That(moved, Is.True); Assert.That(initialFacts.Count, Is.EqualTo(4));
            foreach (var fact in initialFacts)
            {
                Assert.That(fact.Cooker, Is.EqualTo(CookerKind.Pan));
                Assert.That(fact.Food.HeatProgress, Is.EqualTo(30));
                Assert.That(fact.Food.Cooking, Is.EqualTo(CookState.Cooked));
                foreach (var operation in fact.Food.Operations.Where(operation => operation.Action == "cook_started" || operation.Action == "food_state_changed"))
                {
                    Assert.That(operation.Cooker, Is.EqualTo(CookerKind.Pan));
                    Assert.That(operation.SourcePortionId, Is.EqualTo(fact.Food.Id));
                    Assert.That(operation.HeatProgress, Is.EqualTo(30));
                    Assert.That(operation.Cooking, Is.EqualTo(CookState.Cooked));
                }
            }
            Assert.That(first.HeatProgress, Is.EqualTo(31)); Assert.That(first.LastCooker, Is.EqualTo(CookerKind.Pot));
            Assert.That(first.Cooking, Is.EqualTo(CookState.Cooking)); Assert.That(second.HeatProgress, Is.EqualTo(31));
        }

        [Test] public void MixtureDistinguishesInheritedHeatFromItsOwnMethodWithoutRewritingComponents()
        {
            var input = Add(beef, CookerKind.Pan); Medium(CookerKind.Pan); run.Tick(10);
            var inputSnapshot = input.Snapshot();
            state.TryTakeCooker(CookerKind.Pan, 0, out _); Assert.That(state.TryPlaceBowl(out _), Is.True);
            Hand(egg); Assert.That(state.TryPlaceBowl(out _), Is.True);
            Assert.That(state.TryAdvanceMix(3, KitchenToolKind.Spoon, mixture, out _), Is.True);
            var mixed = state.Bowl.Single(); var beforeOven = mixed.Snapshot();
            Assert.That(Starts(beforeOven).Single().SourcePortionId, Is.EqualTo(input.Id));
            Assert.That(beforeOven.Operations.Last(operation => operation.Action == "mixture_completed").SourcePortionId,
                Is.EqualTo(mixed.Id));
            state.TryTakeBowl(0, out _); state.TryPlaceCooker(CookerKind.Oven, out _);
            Medium(CookerKind.Oven); state.TryToggleOvenDoor(out _); run.Tick(1);
            var afterOven = mixed.Snapshot();
            Assert.That(Starts(afterOven).Single(operation => operation.SourcePortionId == mixed.Id).Cooker,
                Is.EqualTo(CookerKind.Oven));
            Assert.That(Starts(afterOven).Single(operation => operation.SourcePortionId == input.Id).Cooker,
                Is.EqualTo(CookerKind.Pan));
            Assert.That(afterOven.Components.Single(component => component.Id == input.Id).HeatProgress, Is.EqualTo(10));
            Assert.That(beforeOven.HeatProgress, Is.Zero); Assert.That(inputSnapshot.HeatProgress, Is.EqualTo(10));
            Assert.That(beforeOven.Components.Single(component => component.Id == input.Id).LastCooker,
                Is.EqualTo(CookerKind.Pan));
            Assert.That(Starts(beforeOven).All(operation => operation.SourcePortionId != mixed.Id), Is.True);
        }
    }
}
