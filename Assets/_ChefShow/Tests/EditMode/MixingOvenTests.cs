using System.Linq;
using ChefShow.Core;
using ChefShow.Data;
using ChefShow.Inventory;
using ChefShow.Ingredients;
using ChefShow.Cooking;
using NUnit.Framework;
using UnityEngine;
namespace ChefShow.Tests
{
    public sealed class MixingOvenTests
    {
        private PrototypeRun run;private InventoryState state;private IngredientDefinition flour,egg,mixture,salt;
        [SetUp]public void Setup(){run=new PrototypeRun(240,1,null,cooking:new CookingSettings(3,30,45,60,.55f,1,1.75f,3));state=run.Inventory;flour=Ingredient("flour");egg=Ingredient("egg");mixture=Ingredient("mixture");salt=Ingredient("salt");salt.IsDoseContainer=true;}
        [TearDown]public void Cleanup(){run.Dispose();foreach(var d in new[]{flour,egg,mixture,salt})Object.DestroyImmediate(d);}
        private IngredientDefinition Ingredient(string id){var d=ScriptableObject.CreateInstance<IngredientDefinition>();d.Id=id;d.CanUseBoard=true;return d;}
        private FoodPortion Hand(IngredientDefinition d)
        {state.TryMoveBasket(BasketPlacement.Carried,out _);Assert.That(state.TryCollect(d,out _),Is.True);state.TryMoveBasket(BasketPlacement.Station,out _);state.TryUnload(out _);state.TryTakeTray(0,out _);return state.Held;}
        private FoodPortion Add(IngredientDefinition d){var food=Hand(d);Assert.That(state.TryPlaceBowl(out _),Is.True);return food;}
        [Test] public void MixConsumesEachInputOnceAndPreservesImmutableComponentsAndDoses()
        {
            var a=Add(flour);var b=Add(egg);Hand(salt);state.TrySeasonBowl(1,out _);state.TryPutInTray(out _);var before=b.Snapshot();
            Assert.That(state.TryAdvanceMix(3,KitchenToolKind.Spoon,mixture,out _),Is.True);var mixed=state.Bowl.Single();
            Assert.That(mixed.Quantity,Is.EqualTo(2));Assert.That(mixed.OriginComponents.Select(o=>o.SourcePortionId),Is.EquivalentTo(new[]{a.Id,b.Id}));Assert.That(mixed.SaltDoses,Is.EqualTo(1));
            Assert.That(mixed.Components.Select(c=>c.Id),Is.EquivalentTo(new[]{a.Id,b.Id}));Assert.That(a.Location,Is.EqualTo(PortionLocation.Mixed));Assert.That(before.Location,Is.EqualTo(PortionLocation.MixingBowl));
            Assert.That(state.TryAdvanceMix(3,KitchenToolKind.Spoon,mixture,out _),Is.False);Assert.That(state.Bowl.Single(),Is.SameAs(mixed));
            state.TryTakeBowl(0,out _);state.TryPlaceServing(out _);Assert.That(state.CaptureDish().Portions.Single().Components.Count,Is.EqualTo(2));
        }
        [Test] public void MixProgressStopsOnPauseRejectsWrongToolAndResetsOnlyWhenCompositionChanges()
        {
            Add(flour);Add(egg);Assert.That(state.TryAdvanceMix(1,KitchenToolKind.Knife,mixture,out _),Is.False);Assert.That(state.MixProgress,Is.Zero);
            state.TryAdvanceMix(1.2f,KitchenToolKind.Spatula,mixture,out _);run.SetPaused(true);Assert.That(state.TryAdvanceMix(2,KitchenToolKind.Spatula,mixture,out _),Is.False);Assert.That(state.MixProgress,Is.EqualTo(1.2f));
            run.SetPaused(false);state.TryTakeBowl(0,out _);Assert.That(state.MixProgress,Is.Zero);state.TryCancelHeld(out _);Assert.That(state.Bowl.Count,Is.EqualTo(2));
            state.TryAdvanceMix(3,KitchenToolKind.Spatula,mixture,out _);Assert.That(state.Bowl.Single().Preparation,Is.EqualTo(PreparationState.Mixed));
        }
        [Test] public void BurnedAndContaminatedInputCannotBeRepairedByMixing()
        {
            var a=Hand(flour);state.TryPlaceCooker(CookerKind.Pan,out _);state.TryCycleHeat(CookerKind.Pan,out _);state.TryCycleHeat(CookerKind.Pan,out _);run.Tick(60);state.TryTakeCooker(CookerKind.Pan,0,out _);
            typeof(FoodPortion).GetProperty("Contaminated").SetValue(a,true);state.TryPlaceBowl(out _);Add(egg);state.TryAdvanceMix(3,KitchenToolKind.Spoon,mixture,out _);
            var mixed=state.Bowl.Single();Assert.That(mixed.Cooking,Is.EqualTo(CookState.Burned));Assert.That(mixed.Contaminated,Is.True);Assert.That(mixed.Components.Single(c=>c.Id==a.Id).Cooking,Is.EqualTo(CookState.Burned));
        }
        [Test] public void OvenRequiresClosedDoorAndHeatAndPreservesBurnedStateAfterTaking()
        {
            var food=Hand(egg);Assert.That(state.TryPlaceCooker(CookerKind.Oven,out _),Is.True);run.Tick(10);Assert.That(food.HeatProgress,Is.Zero);
            state.TryCycleHeat(CookerKind.Oven,out _);state.TryCycleHeat(CookerKind.Oven,out _);run.Tick(10);Assert.That(food.HeatProgress,Is.Zero);
            state.TryToggleOvenDoor(out _);run.Tick(45);Assert.That(food.Cooking,Is.EqualTo(CookState.Cooked));Assert.That(food.LastCooker,Is.EqualTo(CookerKind.Oven));Assert.That(state.TryTakeCooker(CookerKind.Oven,0,out _),Is.False);
            state.TryToggleOvenDoor(out _);run.Tick(10);Assert.That(food.HeatProgress,Is.EqualTo(45));state.TryToggleOvenDoor(out _);run.Tick(30);Assert.That(food.Cooking,Is.EqualTo(CookState.Burned));
            state.TryToggleOvenDoor(out _);state.TryTakeCooker(CookerKind.Oven,0,out _);state.TryPlaceServing(out _);Assert.That(state.Served.Single().Cooking,Is.EqualTo(CookState.Burned));
        }
        [Test] public void BowlAndOvenCapacityAndTimeoutRejectWithoutLosingPortions()
        {
            for(int i=0;i<6;i++)Add(egg);var held=Hand(flour);Assert.That(state.TryPlaceBowl(out _),Is.False);Assert.That(state.Held,Is.SameAs(held));state.TryPutInTray(out _);
            state.TryTakeBowl(0,out _);state.TryPlaceCooker(CookerKind.Oven,out _);state.TryTakeBowl(0,out _);Assert.That(state.TryPlaceCooker(CookerKind.Oven,out _),Is.False);state.TryPutInTray(out _);
            run.SetRemaining(.01f);run.Tick(1);Assert.That(state.TryAdvanceMix(10,KitchenToolKind.Spoon,mixture,out _),Is.False);Assert.That(state.Bowl.Count,Is.EqualTo(4));Assert.That(state.SubmittedDish.Quantity,Is.Zero);
        }
    }
}
