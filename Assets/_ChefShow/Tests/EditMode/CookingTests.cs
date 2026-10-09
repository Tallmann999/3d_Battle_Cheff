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

namespace ChefShow.Tests
{
    public sealed class CookingTests
    {
        private PrototypeRun run;
        private InventoryState state;
        private CookingConfig config;
        private IngredientDefinition beef, potato, carton;
        [SetUp] public void Setup()
        {
            config=ScriptableObject.CreateInstance<CookingConfig>();
            beef=Ingredient("beef"); potato=Ingredient("potato"); carton=Ingredient("egg_carton"); carton.Contents=potato; carton.ContentsQuantity=6; carton.CanUseBoard=false;
            run=new PrototypeRun(240,1005,(t,e)=>Assert.Fail(e.ToString()),cooking:config.Capture()); state=run.Inventory;
        }
        [TearDown] public void Cleanup()
        { run.Dispose(); UnityEngine.Object.DestroyImmediate(config); foreach(var d in new[]{beef,potato,carton}) UnityEngine.Object.DestroyImmediate(d); }
        private IngredientDefinition Ingredient(string id)
        { var d=ScriptableObject.CreateInstance<IngredientDefinition>(); d.Id=id; d.DisplayName=id; d.CanUseBoard=true; return d; }
        private FoodPortion Hand(IngredientDefinition ingredient,bool chopped=false)
        {
            Assert.That(state.TryMoveBasket(BasketPlacement.Carried,out _),Is.True);
            Assert.That(state.TryCollect(ingredient,out _),Is.True); Assert.That(state.TryMoveBasket(BasketPlacement.Station,out _),Is.True);
            Assert.That(state.TryUnload(out _),Is.True); Assert.That(state.TryTakeTray(0,out _),Is.True);
            if(chopped){state.TryPlaceSocket(0,out _);for(int i=0;i<6;i++)Assert.That(state.TryChop(KitchenToolKind.Knife,out _),Is.True);state.TryTakeSocket(0,out _);}
            return state.Held;
        }
        private FoodPortion Add(IngredientDefinition ingredient,CookerKind kind,bool chopped=false)
        { var p=Hand(ingredient,chopped); Assert.That(state.TryPlaceCooker(kind,out var reason),Is.True,reason); return p; }
        private void Medium(CookerKind kind)
        { Assert.That(state.TryCycleHeat(kind,out _),Is.True); Assert.That(state.TryCycleHeat(kind,out _),Is.True); }
        [Test] public void DosesArePerPortionReusableAndPreserveTransfersAndIndependentFacts()
        {
            var salt=Ingredient("salt"); salt.CanUseBoard=false; salt.IsDoseContainer=true;
            var oil=Ingredient("oil"); oil.CanUseBoard=false; oil.IsDoseContainer=true;
            try
            {
                var first=Add(beef,CookerKind.Pan); var second=Add(beef,CookerKind.Pan);
                var before=first.Snapshot(); var source=Hand(salt); var facts=new List<SeasoningApplied>();
                using(run.Events.Subscribe<SeasoningApplied>(f=>{
                    Assert.That(state.Held,Is.Not.Null); Assert.That(first.SaltDoses,Is.EqualTo(f.Food.SaltDoses));
                    facts.Add(f);
                }))
                {
                    Assert.That(state.TryApplySeasoning(CookerKind.Pan,0,out _),Is.True);
                    config.DosesPerPress=4; config.SaltIngredientId="edited_during_run";
                    Assert.That(state.TryApplySeasoning(CookerKind.Pan,0,out _),Is.True);
                }
                Assert.That(state.Held,Is.SameAs(source)); Assert.That(source.Quantity,Is.EqualTo(1));
                Assert.That(first.SaltDoses,Is.EqualTo(2)); Assert.That(second.SaltDoses,Is.Zero);
                Assert.That(before.SaltDoses,Is.Zero); Assert.That(facts[0].Food.SaltDoses,Is.EqualTo(1));
                Assert.That(facts[0].Source.Id,Is.EqualTo(source.Id)); Assert.That(facts[0].AddedDoses,Is.EqualTo(1));
                Assert.That(facts[0].RunId,Is.EqualTo(run.RunId)); Assert.That(facts[0].ActorId,Is.EqualTo("A1"));
                Assert.That(state.TryRemove(false,out _),Is.True); source=Hand(oil);
                Assert.That(state.TryApplySeasoning(CookerKind.Pan,0,out _),Is.True);
                Assert.That(first.OilDoses,Is.EqualTo(1)); Assert.That(second.OilDoses,Is.Zero);
                Assert.That(first.Cooking,Is.EqualTo(CookState.Raw)); Assert.That(first.HeatProgress,Is.Zero);
                Assert.That(state.TryPlaceCooker(CookerKind.Pan,out _),Is.False); Assert.That(state.Held,Is.SameAs(source));
                state.TryRemove(false,out _); state.TryTakeCooker(CookerKind.Pan,0,out _);
                state.TryPutInTray(out _); state.TryTakeTray(0,out _); state.TryPlaceCooker(CookerKind.Pan,out _);
                state.TryTakeCooker(CookerKind.Pan,1,out _); state.TryCancelHeld(out _);
                Assert.That(first.SaltDoses,Is.EqualTo(2)); Assert.That(first.OilDoses,Is.EqualTo(1));
                Assert.That(first.Id,Is.EqualTo(before.Id)); Assert.That(first.Quantity,Is.EqualTo(1));
                Assert.That(first.Operations.Count(o=>o.Action=="seasoning_applied"),Is.EqualTo(3));
                Assert.That(facts[0].Source.Location,Is.EqualTo(PortionLocation.Hand));
                Assert.That(facts[0].Food.Operations.Count(o=>o.Action=="seasoning_applied"),Is.EqualTo(1));
            }
            finally { UnityEngine.Object.DestroyImmediate(salt); UnityEngine.Object.DestroyImmediate(oil); }
        }
        [Test] public void InvalidDoseTargetsPauseTimeoutAndDisposedRunRejectWithoutChanges()
        {
            var oil=Ingredient("oil"); oil.IsDoseContainer=true; oil.CanUseBoard=false;
            try
            {
                var pan=Add(beef,CookerKind.Pan); var pot=Add(potato,CookerKind.Pot,true);
                var source=Hand(oil); int version=state.Version;
                Assert.That(state.TryApplySeasoning(CookerKind.Pot,0,out _),Is.False);
                Assert.That(state.TryApplySeasoning(CookerKind.Pan,-1,out _),Is.False);
                Assert.That(state.TryApplySeasoning(CookerKind.Pan,1,out _),Is.False);
                Assert.That(state.TryApplySeasoning((CookerKind)25,0,out _),Is.False);
                run.SetPaused(true); Assert.That(state.TryApplySeasoning(CookerKind.Pan,0,out _),Is.False); run.SetPaused(false);
                Assert.That(state.Version,Is.EqualTo(version)); Assert.That(state.Held,Is.SameAs(source));
                Assert.That(pot.OilDoses+pan.OilDoses,Is.Zero);
                run.SetRemaining(0); Assert.That(state.TryApplySeasoning(CookerKind.Pan,0,out _),Is.False);
                run.Dispose(); Assert.That(state.TryApplySeasoning(CookerKind.Pan,0,out _),Is.False);
                Assert.That(state.Version,Is.EqualTo(version)); Assert.That(pan.OilDoses,Is.Zero);
            }
            finally { UnityEngine.Object.DestroyImmediate(oil); }
        }
        [Test] public void SaltKeepsContaminationBurnedStateAndValidatesCapturedSettings()
        {
            var salt=Ingredient("salt"); salt.IsDoseContainer=true; salt.CanUseBoard=false;
            var unknown=Ingredient("pepper"); unknown.IsDoseContainer=true;
            try
            {
                var food=Add(potato,CookerKind.Pot,true); Medium(CookerKind.Pot); run.Tick(60);
                var source=Hand(salt); typeof(FoodPortion).GetProperty(nameof(FoodPortion.Contaminated)).SetValue(source,true);
                Assert.That(state.TryApplySeasoning(CookerKind.Pot,0,out _),Is.True);
                Assert.That(food.Contaminated,Is.True); Assert.That(food.SaltDoses,Is.EqualTo(1));
                Assert.That(food.Cooking,Is.EqualTo(CookState.Burned)); Assert.That(food.HeatProgress,Is.EqualTo(60));
                state.TryRemove(false,out _); source=Hand(unknown); int version=state.Version;
                Assert.That(state.TryApplySeasoning(CookerKind.Pot,0,out _),Is.False);
                Assert.That(state.Version,Is.EqualTo(version)); Assert.That(state.Held,Is.SameAs(source));
                config.DosesPerPress=0; Assert.That(config.Validate(),Is.Not.Null); Assert.Throws<InvalidOperationException>(()=>config.Capture());
                config.DosesPerPress=1; config.OilIngredientId=config.SaltIngredientId; Assert.That(config.Validate(),Is.Not.Null);
                Assert.Throws<ArgumentException>(()=>new CookingSettings(3,30,45,60,.5f,1,2,3,new[]{"beef"},new[]{"potato"},0));
            }
            finally { UnityEngine.Object.DestroyImmediate(salt); UnityEngine.Object.DestroyImmediate(unknown); }
        }
        [Test] public void CapacityAndCompatibilityRejectWithoutLosingTheHeldFood()
        {
            var package=Hand(carton); int version=state.Version;
            Assert.That(state.TryPlaceCooker(CookerKind.Pan,out _),Is.False);Assert.That(state.Held,Is.SameAs(package));Assert.That(state.Version,Is.EqualTo(version));state.TryRemove(false,out _);
            var whole=Hand(potato);Assert.That(state.TryPlaceCooker(CookerKind.Pot,out _),Is.False);Assert.That(state.Held,Is.SameAs(whole));state.TryRemove(false,out _);
            for(int i=0;i<3;i++){Add(beef,CookerKind.Pan);Add(potato,CookerKind.Pot,true);}
            var fourth=Hand(beef);version=state.Version;Assert.That(state.TryPlaceCooker(CookerKind.Pan,out _),Is.False);
            Assert.That(state.Held,Is.SameAs(fourth));Assert.That(state.Version,Is.EqualTo(version));Assert.That(state.Cooker(CookerKind.Pan).Count,Is.EqualTo(3));
            Assert.That(state.Portions.Where(p=>p.Location==PortionLocation.Appliance).Select(p=>p.Id).Distinct().Count(),Is.EqualTo(6));
        }
        [Test] public void PanPauseOffTakingAndCancellationPreserveHeatAndSnapshots()
        {
            var food=Add(beef,CookerKind.Pan);Medium(CookerKind.Pan);run.Tick(10);var snapshot=food.Snapshot();
            run.SetPaused(true);run.Tick(100);Assert.That(food.HeatProgress,Is.EqualTo(10));run.SetPaused(false);
            state.TryTakeCooker(CookerKind.Pan,0,out _);run.Tick(5);Assert.That(food.HeatProgress,Is.EqualTo(10));
            Assert.That(state.TryCancelHeld(out _),Is.True);Assert.That(state.Cooker(CookerKind.Pan)[0],Is.SameAs(food));
            run.Tick(20);Assert.That(food.Cooking,Is.EqualTo(CookState.Cooked));Assert.That(snapshot.HeatProgress,Is.EqualTo(10));Assert.That(snapshot.Cooking,Is.EqualTo(CookState.Cooking));
            state.TryCycleHeat(CookerKind.Pan,out _);state.TryCycleHeat(CookerKind.Pan,out _);run.Tick(20);Assert.That(food.HeatProgress,Is.EqualTo(30));
            state.TryTakeCooker(CookerKind.Pan,0,out _);Assert.That(state.TryPlaceSocket(1,out _),Is.True);Assert.That(state.Socket(1),Is.SameAs(food));
            Assert.That(food.OriginComponents.Single().SourcePortionId,Is.EqualTo(food.Id));
        }
        [Test] public void PotNeedsThreeStirsAndKeepsPreparationIdentityAndIndependentState()
        {
            var food=Add(potato,CookerKind.Pot,true);string id=food.Id;Medium(CookerKind.Pot);run.Tick(30);
            Assert.That(food.Cooking,Is.EqualTo(CookState.Cooking));Assert.That(state.TryStir(CookerKind.Pot,KitchenToolKind.Knife,out _),Is.False);
            state.TryTakeCooker(CookerKind.Pot,0,out _);Assert.That(state.TryStir(CookerKind.Pot,KitchenToolKind.Spatula,out _),Is.False);state.TryCancelHeld(out _);
            run.SetPaused(true);Assert.That(state.TryStir(CookerKind.Pot,KitchenToolKind.Spatula,out _),Is.False);run.SetPaused(false);
            for(int i=0;i<3;i++)Assert.That(state.TryStir(CookerKind.Pot,KitchenToolKind.Spatula,out _),Is.True);
            Assert.That(food.Cooking,Is.EqualTo(CookState.Cooked));Assert.That(food.StirPresses,Is.EqualTo(3));Assert.That(food.Id,Is.EqualTo(id));
            Assert.That(food.ChopPresses,Is.EqualTo(6));Assert.That(food.Quantity,Is.EqualTo(1));var snapshot=food.Snapshot();
            Assert.That(state.TryStir(CookerKind.Pot,KitchenToolKind.Spatula,out _),Is.False);run.Tick(20);
            Assert.That(food.Cooking,Is.EqualTo(CookState.Overcooked));Assert.That(snapshot.Cooking,Is.EqualTo(CookState.Cooked));
        }
        [Test] public void BurnedIsIrreversibleAndEventIsPublishedOnceAfterAllPortionsCommit()
        {
            var first=Add(beef,CookerKind.Pan);var second=Add(beef,CookerKind.Pan);Medium(CookerKind.Pan);int events=0;
            using(run.Events.Subscribe<CookingChanged>(f=>{if(f.Action=="food_burned"){events++;Assert.That(second.Cooking,Is.EqualTo(CookState.Burned));Assert.That(f.Food.Cooking,Is.EqualTo(CookState.Burned));}}))
            {run.Tick(60);run.Tick(20);Assert.That(events,Is.EqualTo(2));}
            state.TryTakeCooker(CookerKind.Pan,0,out _);state.TryCancelHeld(out _);run.Tick(10);
            Assert.That(first.Cooking,Is.EqualTo(CookState.Burned));Assert.That(first.HeatProgress,Is.EqualTo(60));
            Assert.That(first.Operations.Count(o=>o.Action=="food_burned"),Is.EqualTo(1));
        }
        [Test] public void FinalFrameClampsAllHeatAndCommandsStopAtTimeoutOrDisposal()
        {
            var food=Add(beef,CookerKind.Pan);Medium(CookerKind.Pan);run.SetRemaining(.2f);run.Tick(10);
            Assert.That(food.HeatProgress,Is.EqualTo(.2f).Within(.0001f));Assert.That(run.Clock.SimulationTime,Is.EqualTo(.2f).Within(.0001f));
            Assert.That(run.RemainingSeconds,Is.Zero);int version=state.Version;run.Tick(100);
            Assert.That(state.Version,Is.EqualTo(version));Assert.That(state.TryTakeCooker(CookerKind.Pan,0,out _),Is.False);
            Assert.That(state.TryCycleHeat(CookerKind.Pan,out _),Is.False);run.Dispose();Assert.That(state.TryStir(CookerKind.Pot,KitchenToolKind.Spatula,out _),Is.False);
        }
        [Test] public void CapturedProfilesAreIndependentAndHigherHeatRunsFaster()
        {
            var pan=Add(beef,CookerKind.Pan);var pot=Add(potato,CookerKind.Pot,true);
            config.ReadySeconds=1000;config.PanIngredients[0]="changed";state.TryCycleHeat(CookerKind.Pan,out _);
            state.TryCycleHeat(CookerKind.Pot,out _);state.TryCycleHeat(CookerKind.Pot,out _);state.TryCycleHeat(CookerKind.Pot,out _);
            run.Tick(10);Assert.That(pan.HeatProgress,Is.EqualTo(5.5f).Within(.001f));Assert.That(pot.HeatProgress,Is.EqualTo(17.5f).Within(.001f));
            Assert.That(state.CookingRules.Ready,Is.EqualTo(30));Assert.That(state.CookingRules.Accepts(CookerKind.Pan,beef),Is.True);
            Assert.Throws<ArgumentException>(()=>new CookingSettings(3,30,20,60,.5f,1,2,3,new[]{"beef"},new[]{"potato"}));
        }
    }
}
