using System.Linq;
using ChefShow.Core;
using ChefShow.Data;
using ChefShow.Inventory;
using ChefShow.Cooking;
using NUnit.Framework;
using UnityEngine;
namespace ChefShow.Tests
{
    public sealed class DishwareTests
    {
        private PrototypeRun run;private InventoryState state;private IngredientDefinition beef,salt;
        private DishwareSettings settings;
        [SetUp] public void Setup()
        {
            settings=new DishwareSettings(new[]{DishwareSnapshot.Default,new DishwareSnapshot("bowl","РњРёСЃРєР°",10,.61f,.18f,true,Color.green),new DishwareSnapshot("kosushka","РљРѕСЃСѓС€РєР°",4,.44f,.13f,true,Color.magenta)},"small_flat");
            run=NewRun();state=run.Inventory;beef=ScriptableObject.CreateInstance<IngredientDefinition>();beef.Id="beef";beef.CanUseBoard=true;
            salt=ScriptableObject.CreateInstance<IngredientDefinition>();salt.Id="salt";salt.IsDoseContainer=true;
        }
        private PrototypeRun NewRun()=>new PrototypeRun(240,1,null,cooking:new CookingSettings(3,30,45,60,.55f,1,1.75f,3),dishware:settings);
        [TearDown] public void Cleanup(){run.Dispose();Object.DestroyImmediate(beef);Object.DestroyImmediate(salt);}
        private FoodPortion Hand(IngredientDefinition d)
        {Assert.That(state.TryMoveBasket(BasketPlacement.Carried,out _),Is.True);Assert.That(state.TryCollect(d,out _),Is.True);Assert.That(state.TryMoveBasket(BasketPlacement.Station,out _),Is.True);Assert.That(state.TryUnload(out _),Is.True);Assert.That(state.TryTakeTray(0,out _),Is.True);return state.Held;}
        [Test] public void ReplacingFilledDishDiscardsRemainingFoodAndChargesOnePenalty()
        {
            for(int i=0;i<8;i++){Hand(beef);Assert.That(state.TryPlaceServing(out _),Is.True);}
            Hand(salt);state.TrySeasonPlate(out _);state.TryPutInTray(out _);var before=state.CaptureDish();
            Assert.That(state.TryTakeDishware("kosushka",out _),Is.True);Assert.That(state.TryPlaceDishware(out _),Is.True);
            Assert.That(state.CurrentDishware.Id,Is.EqualTo("kosushka"));Assert.That(state.Served.Count,Is.Zero);Assert.That(state.Portions.Count(p=>p.Location==PortionLocation.Trash),Is.EqualTo(8));
            Assert.That(state.PlateSaltDoses,Is.Zero);Assert.That(state.PlateFillRatio,Is.Zero);Assert.That(state.PresentationPenalty,Is.EqualTo(1));Assert.That(before.PresentationPenalty,Is.Zero);Assert.That(before.Dishware.Id,Is.EqualTo("small_flat"));
            Assert.That(before.Portions.Count,Is.EqualTo(8));Assert.That(before.SaltDoses,Is.EqualTo(1));Assert.That(state.Portions.Where(p=>p.Location==PortionLocation.Trash).All(p=>p.Operations.Any(h=>h.Action=="discarded_with_dishware")),Is.True);
        }
        [Test] public void PlateAndPortionMixtureAmountUseQuantityWithoutDuplicatingVisualPieces()
        {
            var food=Hand(beef);state.TryPlaceSocket(0,out _);for(int i=0;i<6;i++)state.TryChop(ChefShow.Ingredients.KitchenToolKind.Knife,out _);
            state.TryTakeSocket(0,out _);state.TryPlaceServing(out _);
            Assert.That(state.CaptureDish().Quantity,Is.EqualTo(1));Assert.That(state.PlateFillRatio,Is.EqualTo(1f/6).Within(.001f));
            state.TryTakeServing(0,out _);state.TryPlaceSocket(0,out _);state.TryTakeDishware("bowl",out _);state.TryPlaceDishware(out _);state.TryTakeSocket(0,out _);state.TryPlaceServing(out _);Assert.That(state.CaptureDish().FillRatio,Is.EqualTo(.1f));Assert.That(state.PresentationPenalty,Is.EqualTo(1));
        }
        [Test] public void DishwareOccupiesLeftHandAndBasketOrFoodCannotDisappear()
        {
            Assert.That(state.TryTakeDishware("bowl",out _),Is.True);Assert.That(state.TryMoveBasket(BasketPlacement.Carried,out _),Is.False);
            Assert.That(state.TryTakeServing(0,out _),Is.False);Assert.That(state.TryTakeDishware("kosushka",out _),Is.False);
            Assert.That(state.TryReturnDishware(out _),Is.True);Assert.That(state.CurrentDishware.Id,Is.EqualTo("small_flat"));
            var food=Hand(beef);Assert.That(state.TryTakeDishware("bowl",out _),Is.False);Assert.That(state.Held,Is.SameAs(food));
        }
        [Test] public void PauseSubmittedAndTimeoutRejectDishwareMutation()
        {
            state.TryTakeDishware("bowl",out _);run.SetPaused(true);Assert.That(state.TryPlaceDishware(out _),Is.False);Assert.That(state.TryReturnDishware(out _),Is.False);
            run.SetPaused(false);state.TryPlaceDishware(out _);state.TrySubmitDish(out _);Assert.That(state.TryTakeDishware("kosushka",out _),Is.False);
            Assert.That(state.SubmittedDish.Dishware.Id,Is.EqualTo("bowl"));Assert.That(state.SubmittedDish.Quantity,Is.Zero);
            run.SetRemaining(.01f);run.Tick(1);Assert.That(state.TryPlaceDishware(out _),Is.False);
        }
        [Test] public void CarryingPlateThenReplacingItChargesOnceForEachDistinctActionAndPreservesBoardFood()
        {
            var food=Hand(beef);state.TryPlaceServing(out _);state.TryTakeServing(0,out _);state.TryPlaceSocket(0,out _);
            Assert.That(state.TryTakePlacedDishware(out _),Is.True);Assert.That(state.CurrentDishware,Is.Null);Assert.That(state.PresentationPenalty,Is.EqualTo(1));
            var empty=state.CaptureDish();Assert.That(empty.Dishware,Is.Null);Assert.That(empty.FillRatio,Is.Zero);Assert.That(state.Socket(0),Is.SameAs(food));
            Assert.That(state.TryTakeSocket(0,out _),Is.False);Assert.That(state.Socket(0),Is.SameAs(food));
            Assert.That(state.TryReturnDishware(out _),Is.True);Assert.That(state.CurrentDishware.Id,Is.EqualTo("small_flat"));Assert.That(state.PresentationPenalty,Is.EqualTo(1));
            state.TryTakeSocket(0,out _);Assert.That(state.TryPlaceServing(out _),Is.True);Assert.That(state.Served.Single(),Is.SameAs(food));
            state.TryTakeServing(0,out _);state.TryPlaceSocket(0,out _);
            state.TryTakeDishware("bowl",out _);Assert.That(state.TryPlaceDishware(out _),Is.True);Assert.That(state.PresentationPenalty,Is.EqualTo(2));
            state.TryTakeSocket(0,out _);state.TryPlaceServing(out _);Assert.That(state.Served.Single(),Is.SameAs(food));
            state.TrySubmitDish(out _);Assert.That(state.SubmittedDish.PresentationPenalty,Is.EqualTo(2));Assert.That(state.TryTakePlacedDishware(out _),Is.False);
            Assert.That(state.TryTakeServing(0,out _),Is.False);Assert.That(state.TryTakeDishware("bowl",out _),Is.False);Assert.That(state.PresentationPenalty,Is.EqualTo(2));
        }
        [Test] public void EmptyPlateCarryRestoreCountsOnceButSupplyPickupAndFailedCommandsDoNot()
        {
            state.TryTakeDishware("bowl",out _);state.TryReturnDishware(out _);Assert.That(state.PresentationPenalty,Is.Zero);
            run.SetPaused(true);Assert.That(state.TryTakePlacedDishware(out _),Is.False);run.SetPaused(false);
            state.TryTakePlacedDishware(out _);Assert.That(state.PresentationPenalty,Is.EqualTo(1));
            Assert.That(state.TryTakePlacedDishware(out _),Is.False);Assert.That(state.PresentationPenalty,Is.EqualTo(1));
            Assert.That(state.TryReturnDishware(out _),Is.True);Assert.That(state.CurrentDishware.Id,Is.EqualTo("small_flat"));Assert.That(state.PresentationPenalty,Is.EqualTo(1));
            run.SetRemaining(.01f);run.Tick(1);Assert.That(state.SubmittedDish.Dishware.Id,Is.EqualTo("small_flat"));Assert.That(state.SubmittedDish.Quantity,Is.Zero);Assert.That(state.SubmittedDish.PresentationPenalty,Is.EqualTo(1));
            using(var fresh=NewRun()){Assert.That(fresh.Inventory.PresentationPenalty,Is.Zero);Assert.That(fresh.Inventory.CurrentDishware.Id,Is.EqualTo("small_flat"));}
        }
        [Test] public void ProfilesAreCapturedAndNewRunRestoresStartingDish()
        {
            var definition=ScriptableObject.CreateInstance<DishwareDefinition>();definition.Id="deep";definition.DisplayName="Р“Р»СѓР±РѕРєР°СЏ";definition.NominalCapacity=8;definition.Diameter=.6f;definition.Depth=.15f;definition.SupportsLiquid=true;
            var captured=definition.Capture();definition.NominalCapacity=100;definition.SupportsLiquid=false;
            Assert.That(captured.NominalCapacity,Is.EqualTo(8));Assert.That(captured.SupportsLiquid,Is.True);Object.DestroyImmediate(definition);
            state.TryTakeDishware("bowl",out _);state.TryPlaceDishware(out _);using(var another=NewRun()){Assert.That(another.Inventory.CurrentDishware.Id,Is.EqualTo("small_flat"));Assert.That(another.Inventory.HeldDishware,Is.Null);}
        }
    }
}
