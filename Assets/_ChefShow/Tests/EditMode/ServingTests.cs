using System.Linq;
using ChefShow.Core;
using ChefShow.Data;
using ChefShow.Inventory;
using ChefShow.Cooking;
using NUnit.Framework;
using UnityEngine;
namespace ChefShow.Tests
{
    public sealed class ServingTests
    {
        private PrototypeRun run; private InventoryState state; private IngredientDefinition beef,egg,salt,oil,package;
        [SetUp] public void Setup()
        {
            run=new PrototypeRun(240,1,null,cooking:new CookingSettings(3,30,45,60,.55f,1,1.75f,3));state=run.Inventory;
            beef=Ingredient("beef");egg=Ingredient("egg");salt=Ingredient("salt");salt.IsDoseContainer=true;oil=Ingredient("oil");oil.IsDoseContainer=true;
            package=Ingredient("egg_carton");package.Contents=egg;package.ContentsQuantity=6;package.CanUseBoard=false;
        }
        [TearDown] public void Cleanup(){run.Dispose();foreach(var d in new[]{beef,egg,salt,oil,package})Object.DestroyImmediate(d);}
        private IngredientDefinition Ingredient(string id){var d=ScriptableObject.CreateInstance<IngredientDefinition>();d.Id=id;d.CanUseBoard=true;return d;}
        private FoodPortion Hand(IngredientDefinition d)
        {Assert.That(state.TryMoveBasket(BasketPlacement.Carried,out _),Is.True);Assert.That(state.TryCollect(d,out _),Is.True);Assert.That(state.TryMoveBasket(BasketPlacement.Station,out _),Is.True);Assert.That(state.TryUnload(out _),Is.True);Assert.That(state.TryTakeTray(0,out _),Is.True);return state.Held;}
        [Test] public void RepeatedAndMixedFoodsAddWithoutReplacingAndCanBeTakenIndividually()
        {
            var a=Hand(beef);Assert.That(state.TryPlaceServing(out _),Is.True);var b=Hand(egg);state.TryPlaceServing(out _);
            for(int i=0;i<12;i++){Hand(beef);Assert.That(state.TryPlaceServing(out _),Is.True);}
            Assert.That(state.Served.Count,Is.EqualTo(14));Assert.That(state.Served.Select(p=>p.Id).Distinct().Count(),Is.EqualTo(14));
            var snapshot=state.CaptureDish();Assert.That(state.TryTakeServing(1,out _),Is.True);Assert.That(state.Held,Is.SameAs(b));
            state.TryCancelHeld(out _);Assert.That(state.Served[1],Is.SameAs(b));Assert.That(state.Served[0],Is.SameAs(a));Assert.That(snapshot.Portions.Count,Is.EqualTo(14));
            Assert.That(state.Portions.Count(p=>p.Location==PortionLocation.Station),Is.EqualTo(state.Served.Count));
        }
        [Test] public void DishDosesAreOnePerClickAndDoNotMultiplyOrRewritePortionDoses()
        {
            Hand(beef);state.TryPlaceServing(out _);Hand(egg);state.TryPlaceServing(out _);var before=state.CaptureDish();var source=Hand(salt);
            Assert.That(state.TrySeasonPlate(out _),Is.True);Assert.That(state.TrySeasonPlate(out _),Is.True);Assert.That(state.Held,Is.SameAs(source));
            Assert.That(state.PlateSaltDoses,Is.EqualTo(2));Assert.That(state.Served.Sum(p=>p.SaltDoses),Is.Zero);Assert.That(before.SaltDoses,Is.Zero);
            state.TryPutInTray(out _);state.TryTakeTray(0,out _);state.TryPutInTray(out _);
        }
        [Test] public void BoardCanBeSeasonedBeforeHeatAndCountersFollowTheOriginalFood()
        {
            var food=Hand(beef);state.TryPlaceSocket(0,out _);Hand(salt);Assert.That(state.TrySeasonBoard(out _),Is.True);Assert.That(food.SaltDoses,Is.EqualTo(1));
            state.TryPutInTray(out _);state.TryTakeSocket(0,out _);state.TryPlaceCooker(CookerKind.Pan,out _);state.TryCycleHeat(CookerKind.Pan,out _);state.TryCycleHeat(CookerKind.Pan,out _);run.Tick(30);
            Assert.That(food.Cooking,Is.EqualTo(CookState.Cooked));state.TryTakeCooker(CookerKind.Pan,0,out _);state.TryPlaceServing(out _);Assert.That(state.Served[0].SaltDoses,Is.EqualTo(1));
        }
        [Test] public void SubmitLocksDishAndTimeoutCapturesOnlyWhatIsAlreadyPlated()
        {
            var food=Hand(beef);state.TryPlaceServing(out _);var unplated=Hand(egg);state.TryPlaceCooker(CookerKind.Pan,out _);run.SetRemaining(.1f);run.Tick(1);
            Assert.That(state.SubmittedDish.Quantity,Is.EqualTo(1));Assert.That(state.SubmittedDish.Portions.Single().Id,Is.EqualTo(food.Id));
            Assert.That(state.TryTakeServing(0,out _),Is.False);Assert.That(state.TrySubmitDish(out _),Is.False);Assert.That(unplated.Location,Is.EqualTo(PortionLocation.Appliance));
        }
        [Test] public void RejectedPackagesPauseAndDoseOnEmptyPlateDoNotLoseAnything()
        {
            var food=Hand(package);Assert.That(state.TryPlaceServing(out _),Is.False);Assert.That(state.Held,Is.SameAs(food));state.TryPutInTray(out _);
            state.TryTakeTray(0,out _);state.TryRemove(false,out _);var source=Hand(oil);Assert.That(state.TrySeasonPlate(out _),Is.False);Assert.That(state.Held,Is.SameAs(source));
            run.SetPaused(true);Assert.That(state.TryPlaceServing(out _),Is.False);Assert.That(state.PlateOilDoses,Is.Zero);
        }
    }
}
