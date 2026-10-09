using System.Collections;
using System.Linq;
using ChefShow.Core;
using ChefShow.Cooking;
using ChefShow.Inventory;
using ChefShow.Ingredients;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
namespace ChefShow.Tests
{
    public sealed partial class InventoryPlayTests
    {
        private IEnumerator HandClick(bool right=false)
        {InputSystem.QueueStateEvent(mouse,new MouseState().WithButton(right?MouseButton.Right:MouseButton.Left));yield return null;yield return null;InputSystem.QueueStateEvent(mouse,new MouseState());yield return null;yield return null;}
        private IEnumerator RawKeyE()
        {InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.E));yield return null;yield return null;InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;yield return null;}
        private IEnumerator MouseTrip(params string[] ids)
        {
            yield return TakeBasket();foreach(var id in ids){Teleport(new Vector3(Stock(id).x,.05f,10.3f));yield return Aim(Stock(id));yield return HandClick();}
            Teleport(new Vector3(-9.7f,.05f,-6.25f));yield return Aim(inventory.StationDock.parent.position);yield return Press(Key.Tab);yield return Aim(inventory.BasketBody.position,false);yield return HandClick();
        }
        [UnityTest] public IEnumerator MouseHandsUseLeftForFoodRightForToolsAndSixSeparateCuts()
        {
            yield return MouseTrip("beef","salt");yield return Aim(TrayPoint(0));InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.E));yield return null;yield return null;InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;Assert.That(State.Held,Is.Null);
            yield return HandClick();var food=State.Held;Assert.That(inventory.FoodInLeftHand,Is.True);Assert.That(inventory.HeldDisplay.transform.parent,Is.SameAs(bootstrap.Tools.LeftFoodHand));
            var drawer=bootstrap.Tools.Drawers.Single(d=>d.StationId=="A1");yield return AimDrawer(drawer);yield return HandClick();yield return new WaitForSecondsRealtime(.4f);
            var knife=Tool(drawer,0);yield return AimTool(knife);yield return HandClick();Assert.That(bootstrap.Tools.EquippedObject,Is.Null);
            yield return HandClick(true);Assert.That(bootstrap.Tools.EquippedObject,Is.SameAs(knife));Assert.That(State.Held,Is.SameAs(food));
            yield return Aim(Socket(0));yield return HandClick();Assert.That(State.Socket(0),Is.SameAs(food));
            for(int i=0;i<6;i++)yield return HandClick(true);
            Assert.That(food.ChopPresses,Is.EqualTo(6));Assert.That(State.Held,Is.Null);Assert.That(food.Preparation,Is.EqualTo(PreparationState.Chopped));
            yield return Aim(TrayPoint(0));yield return HandClick();yield return Aim(Socket(0));yield return HandClick();Assert.That(food.SaltDoses,Is.EqualTo(1));
            Assert.That(bootstrap.Hud.HandIcon.enabled,Is.True);Assert.That(bootstrap.Hud.InteractionKey.text,Is.EqualTo("ЛКМ"));
            yield return Aim(inventory.TrayDisplays[0].transform.parent.parent.position);yield return HandClick();yield return Aim(Socket(0));yield return HandClick();
            var pan=Appliance(CookerKind.Pan,"A1");yield return Aim(pan.transform.position+Vector3.up*.025f);yield return HandClick();Assert.That(State.Cooker(CookerKind.Pan).Single(),Is.SameAs(food));
            yield return AimCell(drawer,0);yield return HandClick(true);Assert.That(bootstrap.Tools.EquippedObject,Is.Null);
        }
        [UnityTest] public IEnumerator MousePlateAcceptsMeatEggRepeatAndDosesWithoutReplacingFood()
        {
            yield return MouseTrip("beef","egg","beef","salt","oil");var dish=bootstrap.Serving.Stations.Single(s=>s.StationId=="A1");
            for(int i=0;i<3;i++){yield return Aim(TrayPoint(0));yield return HandClick();yield return Aim(Socket(1));yield return HandClick();}
            Assert.That(State.Served.Count,Is.EqualTo(3));Assert.That(State.Served.Select(p=>p.Id).Distinct().Count(),Is.EqualTo(3));
            Assert.That(dish.Food.Take(3).All(f=>f.Visual.enabled),Is.True);
            yield return Aim(TrayPoint(0));yield return HandClick();yield return Aim(Socket(1));yield return HandClick();Assert.That(State.PlateSaltDoses,Is.EqualTo(1));
            yield return Aim(inventory.TrayDisplays[0].transform.parent.parent.position);yield return HandClick();yield return Aim(TrayPoint(0));yield return HandClick();
            yield return Aim(Socket(1));yield return HandClick();Assert.That(State.PlateOilDoses,Is.EqualTo(1));Assert.That(State.Served.Count,Is.EqualTo(3));
            Capture("mouse-plate-components.png");
            var snapshot=State.CaptureDish();bootstrap.SetPaused(true);yield return HandClick();Assert.That(State.PlateOilDoses,Is.EqualTo(1));bootstrap.SetPaused(false);
            yield return Aim(dish.GetComponentsInChildren<ServingTarget>().Single(t=>t.Submit).transform.position);yield return HandClick();
            Assert.That(State.SubmittedDish.Quantity,Is.EqualTo(3));Assert.That(snapshot.OilDoses,Is.EqualTo(1));
        }

        [UnityTest] public IEnumerator MouseBowlMixesAndWallOvenBakesThenReturnsTheSameMixtureToPlate()
        {
            yield return MouseTrip("flour","butter","sugar","apple");var bowl=bootstrap.Mixing.Stations.Single(s=>s.StationId=="A1");
            for(int i=0;i<4;i++){yield return Aim(TrayPoint(0));yield return HandClick();yield return Aim(bowl.transform.position);yield return HandClick();}
            Assert.That(State.Bowl.Count,Is.EqualTo(4));var ids=State.Bowl.Select(p=>p.Id).ToArray();
            var drawer=bootstrap.Tools.Drawers.Single(d=>d.StationId=="A1");yield return AimDrawer(drawer);yield return HandClick();yield return new WaitForSecondsRealtime(.4f);yield return AimTool(Tool(drawer,2));yield return HandClick(true);
            yield return Aim(bowl.transform.position);Assert.That(bootstrap.Player.Target.GetComponent<MixingTarget>(),Is.Not.Null);Assert.That(bootstrap.Tools.Equipped,Is.EqualTo(KitchenToolKind.Spoon));InputSystem.QueueStateEvent(mouse,new MouseState().WithButton(MouseButton.Right));yield return new WaitForSecondsRealtime(1.25f);
            bootstrap.SetPaused(true);float progress=State.MixProgress;yield return new WaitForSecondsRealtime(.15f);Assert.That(State.MixProgress,Is.EqualTo(progress));bootstrap.SetPaused(false);
            InputSystem.QueueStateEvent(mouse,new MouseState());yield return null;InputSystem.QueueStateEvent(mouse,new MouseState().WithButton(MouseButton.Right));
            float mixDeadline=Time.realtimeSinceStartup+4;while(State.Bowl.Count>1 && Time.realtimeSinceStartup<mixDeadline)yield return null;InputSystem.QueueStateEvent(mouse,new MouseState());yield return null;
            Assert.That(State.Bowl.Count,Is.EqualTo(1),"progress="+State.MixProgress+" target="+bootstrap.Player.Target?.name+" HUD="+bootstrap.Hud.Context.text);
            var mixed=State.Bowl.Single();Assert.That(mixed.Quantity,Is.EqualTo(4));Assert.That(mixed.Components.Select(c=>c.Id),Is.EquivalentTo(ids));
            yield return Aim(bowl.Food[0].Visual.bounds.center);yield return HandClick();Assert.That(State.Held,Is.SameAs(mixed));
            var oven=Appliance(CookerKind.Oven,"A1");Assert.That(oven.transform.position.x,Is.LessThan(-28));
            Teleport(new Vector3(-27,.05f,-6.25f));yield return Aim(oven.transform.Find("Bake Form").position);yield return HandClick();Assert.That(State.Cooker(CookerKind.Oven).Single(),Is.SameAs(mixed));
            yield return Aim(oven.transform.Find("Heat Knob").position);yield return HandClick();yield return HandClick();bootstrap.Run.Clock.SetSpeed(20);
            yield return new WaitForSecondsRealtime(.15f);Assert.That(mixed.HeatProgress,Is.Zero);bootstrap.Run.Clock.SetSpeed(1);
            yield return Aim(oven.transform.Find("Door Handle").position);yield return HandClick();Assert.That(State.OvenDoorOpen,Is.False);
            bootstrap.Run.Clock.SetSpeed(20);float deadline=Time.realtimeSinceStartup+4;while(mixed.Cooking!=CookState.Cooked && Time.realtimeSinceStartup<deadline)yield return null;bootstrap.Run.Clock.SetSpeed(1);
            Assert.That(mixed.Cooking,Is.EqualTo(CookState.Cooked));Capture("wall-oven-ready.png");yield return HandClick();Assert.That(State.OvenDoorOpen,Is.True);
            yield return Aim(oven.Food[0].Visual.bounds.center);yield return HandClick();Assert.That(State.Held,Is.SameAs(mixed));
            Teleport(new Vector3(-9.7f,.05f,-6.25f));yield return Aim(Socket(1));yield return HandClick();Assert.That(State.Served.Single(),Is.SameAs(mixed));Assert.That(State.Served.Single().Components.Count,Is.EqualTo(4));
        }
        [UnityTest] public IEnumerator OvenBurnsVisiblyFreezesOnPauseAndResetOpensAllDoors()
        {
            yield return MouseTrip("egg");yield return Aim(TrayPoint(0));yield return HandClick();var egg=State.Held;var oven=Appliance(CookerKind.Oven,"A1");
            Teleport(new Vector3(-27,.05f,-6.25f));yield return Aim(oven.transform.Find("Bake Form").position);yield return HandClick();
            yield return Aim(oven.transform.Find("Heat Knob").position);yield return HandClick();yield return HandClick();yield return Aim(oven.transform.Find("Door Handle").position);yield return HandClick();
            bootstrap.SetPaused(true);var frozen=egg.HeatProgress;yield return new WaitForSecondsRealtime(.15f);Assert.That(egg.HeatProgress,Is.EqualTo(frozen));bootstrap.SetPaused(false);
            bootstrap.Run.Clock.SetSpeed(30);float deadline=Time.realtimeSinceStartup+4;while(egg.Cooking!=CookState.Burned && Time.realtimeSinceStartup<deadline)yield return null;bootstrap.Run.Clock.SetSpeed(1);
            Assert.That(egg.Cooking,Is.EqualTo(CookState.Burned));Assert.That(oven.Smoke.All(r=>r.enabled),Is.True);Capture("wall-oven-burned.png");
            bootstrap.RestartShow();yield return null;Assert.That(State.OvenDoorOpen,Is.True);Assert.That(State.Cooker(CookerKind.Oven).Count,Is.Zero);Assert.That(oven.Smoke.All(r=>!r.enabled),Is.True);
        }

        [UnityTest] public IEnumerator MouseHeapKeepsSixteenPortionsAndTakesOneTargetedComponent()
        {
            for(int trip=0;trip<2;trip++)
            {
                Assert.That(State.TryMoveBasket(BasketPlacement.Carried,out _),Is.True);
                for(int i=0;i<8;i++)Assert.That(State.TryCollect(inventory.Catalog.Ingredients.Single(d=>d.Id==(i%2==0?"beef":"egg")),out _),Is.True);
                Assert.That(State.TryMoveBasket(BasketPlacement.Station,out _),Is.True);Assert.That(State.TryUnload(out _),Is.True);
            }
            Teleport(new Vector3(-9.7f,.05f,-6.25f));
            for(int i=0;i<16;i++){yield return Aim(TrayPoint(0));yield return HandClick();yield return Aim(Socket(1));yield return HandClick();}
            var dish=bootstrap.Serving.Stations.Single(s=>s.StationId=="A1");Assert.That(State.Served.Count,Is.EqualTo(16));Assert.That(dish.Food.Take(16).All(f=>f.Visual.enabled),Is.True);
            Assert.That(dish.Food[15].transform.position.y,Is.GreaterThan(dish.Food[0].transform.position.y+.1f)); // separate vertical layers
            var selected=State.Served[15];yield return Aim(dish.Food[15].Visual.bounds.center);Assert.That(bootstrap.Player.Target.GetComponent<ServingTarget>().Index,Is.EqualTo(15));yield return HandClick();
            Assert.That(State.Held,Is.SameAs(selected));Assert.That(State.Served.Count,Is.EqualTo(15));Assert.That(State.Served.Select(p=>p.Id).Distinct().Count(),Is.EqualTo(15));Capture("plated-food-heap.png");
        }
    }
}
