using System.Collections;
using System.Linq;
using ChefShow.Cooking;
using ChefShow.Inventory;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
namespace ChefShow.Tests
{
    public sealed partial class InventoryPlayTests
    {
        [UnityTest] public IEnumerator MouseDirectPantryPickupUsesOneLeftItemWithRightToolAndKeepsBasketRoute()
        {
            Teleport(new Vector3(-9.7f,.05f,-6.25f));yield return RecognitionTool(0);
            var knife=bootstrap.Tools.EquippedObject;Assert.That(knife,Is.Not.Null);
            Teleport(new Vector3(Stock("potato").x,.05f,10.3f));yield return Aim(Stock("potato"));yield return HandClick();
            var potato=State.Held;Assert.That(potato,Is.Not.Null);Assert.That(potato.Ingredient.Id,Is.EqualTo("potato"));Assert.That(State.Basket,Is.Empty);
            Assert.That(inventory.HeldDisplay.Visual.enabled,Is.True);Assert.That(inventory.FoodInLeftHand,Is.True);Assert.That(bootstrap.Tools.EquippedObject,Is.SameAs(knife));Capture("direct-pantry-left-hand.png");
            Teleport(new Vector3(Stock("egg").x,.05f,10.3f));yield return Aim(Stock("egg"));yield return HandClick();
            Assert.That(State.Held,Is.SameAs(potato));Assert.That(State.Portions.Count,Is.EqualTo(1));
            yield return Press(Key.Backspace);Assert.That(State.Held,Is.Null);Assert.That(potato.Location,Is.EqualTo(PortionLocation.Returned));
            bootstrap.SetPaused(true);yield return HandClick();Assert.That(State.Held,Is.Null);bootstrap.SetPaused(false);yield return HandClick();
            var egg=State.Held;Assert.That(egg.Ingredient.Id,Is.EqualTo("egg"));Teleport(new Vector3(-9.7f,.05f,-6.25f));yield return Aim(inventory.TrayDisplays[0].transform.parent.parent.position);yield return HandClick();
            Assert.That(State.Tray.Single(),Is.SameAs(egg));Assert.That(State.Held,Is.Null);Assert.That(State.Basket,Is.Empty);
            yield return TakeBasket();Teleport(new Vector3(Stock("beef").x,.05f,10.3f));yield return Aim(Stock("beef"));yield return HandClick();
            Assert.That(State.Held,Is.Null);Assert.That(State.Basket.Single().Ingredient.Id,Is.EqualTo("beef"));Assert.That(bootstrap.Tools.EquippedObject,Is.SameAs(knife));
            bootstrap.RestartShow();yield return null;Assert.That(State.Held,Is.Null);Assert.That(State.Tray,Is.Empty);Assert.That(State.Basket,Is.Empty);
        }
        [UnityTest] public IEnumerator MouseFilledPlateCarriesFoodDosesAndReturnsWithoutLossThenSubmits()
        {
            yield return MouseTrip("beef","egg","salt");yield return Aim(TrayPoint(0));yield return HandClick();var beef=State.Held;
            var pan=Appliance(CookerKind.Pan);yield return Aim(pan.transform.position+Vector3.up*.025f);yield return HandClick();yield return HeatTwice(pan);yield return RecognitionReady(beef);
            yield return Aim(CookFoodPoint(pan,0));yield return HandClick();yield return Aim(Socket(1));yield return HandClick();
            yield return Aim(TrayPoint(0));yield return HandClick();var egg=State.Held;yield return Aim(Socket(1));yield return HandClick();
            yield return Aim(TrayPoint(0));yield return HandClick();yield return Aim(Socket(1));yield return HandClick();yield return Press(Key.Backspace);
            var station=bootstrap.Serving.Stations.Single(s=>s.StationId=="A1");var original=State.CaptureDish();var ids=State.Served.Select(p=>p.Id).ToArray();
            yield return Aim(Socket(1));yield return HandClick();Assert.That(State.CurrentDishware,Is.Null);Assert.That(State.Served,Is.Empty);Assert.That(State.HeldServed.Select(p=>p.Id),Is.EqualTo(ids));
            Assert.That(State.HeldPlateSaltDoses,Is.EqualTo(1));Assert.That(State.PlateSaltDoses,Is.Zero);Assert.That(State.PresentationPenalty,Is.EqualTo(1));
            Assert.That(station.Food.Take(2).All(f=>f.Visual.enabled&&!f.GetComponent<BoxCollider>().enabled),Is.True);Assert.That(bootstrap.Dishware.HeldView.Base.enabled,Is.True);Capture("filled-plate-in-hand.png");
            var before=station.Food[0].transform.position;InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W));yield return new WaitForSecondsRealtime(.2f);InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;
            Assert.That(Vector3.Distance(before,station.Food[0].transform.position),Is.GreaterThan(.05f));Assert.That(State.HeldServed.Select(p=>p.Id),Is.EqualTo(ids));
            bootstrap.SetPaused(true);yield return HandClick();Assert.That(State.HeldDishware,Is.Not.Null);bootstrap.SetPaused(false);
            var foreign=bootstrap.Serving.Stations.Single(s=>s.StationId=="B1");Teleport(new Vector3(9.7f,.05f,-6.25f));yield return Aim(foreign.PlateView.transform.position);yield return HandClick();Assert.That(State.HeldServed.Count,Is.EqualTo(2));
            Teleport(new Vector3(-9.7f,.05f,-6.25f));yield return Aim(Socket(1));yield return HandClick();
            Assert.That(State.HeldDishware,Is.Null);Assert.That(State.HeldServed,Is.Empty);Assert.That(State.Served.Select(p=>p.Id),Is.EqualTo(ids));Assert.That(State.PlateSaltDoses,Is.EqualTo(1));
            Assert.That(beef.Cooking,Is.EqualTo(CookState.Cooked));Assert.That(egg.Cooking,Is.EqualTo(CookState.Raw));Assert.That(State.Portions.All(p=>p.Location!=PortionLocation.Trash),Is.True);
            Assert.That(State.PresentationPenalty,Is.EqualTo(1));Assert.That(station.Food.Take(2).All(f=>f.Visual.enabled&&f.GetComponent<BoxCollider>().enabled),Is.True);Capture("filled-plate-returned.png");
            yield return Aim(Socket(1));yield return HandClick();yield return Press(Key.Backspace);Assert.That(State.Served.Select(p=>p.Id),Is.EqualTo(ids));Assert.That(State.PresentationPenalty,Is.EqualTo(2));
            yield return JudgingSubmit();Assert.That(State.SubmittedDish.Portions.Select(p=>p.Id),Is.EqualTo(ids));Assert.That(State.SubmittedDish.SaltDoses,Is.EqualTo(1));Assert.That(original.SaltDoses,Is.EqualTo(1));Assert.That(original.PresentationPenalty,Is.Zero);
            yield return Aim(Socket(1));yield return HandClick();Assert.That(State.HeldDishware,Is.Null);
            bootstrap.RestartShow();yield return null;Assert.That(State.HeldServed,Is.Empty);Assert.That(State.Served,Is.Empty);Assert.That(State.PresentationPenalty,Is.Zero);
        }
    }
}
