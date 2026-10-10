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
            settings=new DishwareSettings(new[]{DishwareSnapshot.Default,new DishwareSnapshot("bowl","Миска",10,.61f,.18f,true,Color.green),new DishwareSnapshot("kosushka","Косушка",4,.44f,.13f,true,Color.magenta)},"small_flat");
            run=NewRun();state=run.Inventory;beef=ScriptableObject.CreateInstance<IngredientDefinition>();beef.Id="beef";beef.CanUseBoard=true;
            salt=ScriptableObject.CreateInstance<IngredientDefinition>();salt.Id="salt";salt.IsDoseContainer=true;
        }
        private PrototypeRun NewRun()=>new PrototypeRun(240,1,null,cooking:new CookingSettings(3,30,45,60,.55f,1,1.75f,3),dishware:settings);
        [TearDown] public void Cleanup(){run.Dispose();Object.DestroyImmediate(beef);Object.DestroyImmediate(salt);}
        private FoodPortion Hand(IngredientDefinition d)
        {Assert.That(state.TryMoveBasket(BasketPlacement.Carried,out _),Is.True);Assert.That(state.TryCollect(d,out _),Is.True);Assert.That(state.TryMoveBasket(BasketPlacement.Station,out _),Is.True);Assert.That(state.TryUnload(out _),Is.True);Assert.That(state.TryTakeTray(0,out _),Is.True);return state.Held;}
        [Test] public void ReplacingFilledDishKeepsPortionsStatesAndWholeDishDoses()
        {
            for(int i=0;i<8;i++){Hand(beef);Assert.That(state.TryPlaceServing(out _),Is.True);}
            Hand(salt);state.TrySeasonPlate(out _);state.TryPutInTray(out _);var before=state.CaptureDish();
            Assert.That(state.TryTakeDishware("kosushka",out _),Is.True);Assert.That(state.TryPlaceDishware(out _),Is.True);
            Assert.That(state.CurrentDishware.Id,Is.EqualTo("kosushka"));Assert.That(state.Served.Select(p=>p.Id),Is.EqualTo(before.Portions.Select(p=>p.Id)));
            Assert.That(state.PlateSaltDoses,Is.EqualTo(1));Assert.That(state.PlateFillRatio,Is.EqualTo(2));Assert.That(before.Dishware.Id,Is.EqualTo("small_flat"));
            Assert.That(before.Portions.Count,Is.EqualTo(8));
        }
        [Test] public void PlateAndPortionMixtureAmountUseQuantityWithoutDuplicatingVisualPieces()
        {
            var food=Hand(beef);state.TryPlaceSocket(0,out _);for(int i=0;i<6;i++)state.TryChop(ChefShow.Ingredients.KitchenToolKind.Knife,out _);
            state.TryTakeSocket(0,out _);state.TryPlaceServing(out _);
            Assert.That(state.CaptureDish().Quantity,Is.EqualTo(1));Assert.That(state.PlateFillRatio,Is.EqualTo(1f/6).Within(.001f));
            state.TryTakeDishware("bowl",out _);state.TryPlaceDishware(out _);Assert.That(state.CaptureDish().FillRatio,Is.EqualTo(.1f));
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
        [Test] public void ProfilesAreCapturedAndNewRunRestoresStartingDish()
        {
            var definition=ScriptableObject.CreateInstance<DishwareDefinition>();definition.Id="deep";definition.DisplayName="Глубокая";definition.NominalCapacity=8;definition.Diameter=.6f;definition.Depth=.15f;definition.SupportsLiquid=true;
            var captured=definition.Capture();definition.NominalCapacity=100;definition.SupportsLiquid=false;
            Assert.That(captured.NominalCapacity,Is.EqualTo(8));Assert.That(captured.SupportsLiquid,Is.True);Object.DestroyImmediate(definition);
            state.TryTakeDishware("bowl",out _);state.TryPlaceDishware(out _);using(var another=NewRun()){Assert.That(another.Inventory.CurrentDishware.Id,Is.EqualTo("small_flat"));Assert.That(another.Inventory.HeldDishware,Is.Null);}
        }
    }
}
