using System.Collections;
using System.Linq;
using ChefShow.Inventory;
using ChefShow.Cooking;
using ChefShow.Data;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace ChefShow.Tests
{
    public sealed partial class InventoryPlayTests
    {
        [UnityTest] public IEnumerator MouseDishwareReplacementDiscardsRemainingFoodAndShowsPenalty()
        {
            yield return MouseTrip("beef","egg");for(int i=0;i<2;i++){yield return Aim(TrayPoint(0));yield return HandClick();yield return Aim(Socket(1));yield return HandClick();}
            var ids=State.Served.Select(p=>p.Id).ToArray();var target=bootstrap.Dishware.Supply.First(t=>t.Team==TeamId.A && t.Definition.Id=="bowl");
            Teleport(new Vector3(-10.7f,.05f,target.transform.position.z));yield return Aim(target.transform.position+Vector3.up*.07f);yield return HandClick();
            Assert.That(State.HeldDishware.Id,Is.EqualTo("bowl"));Assert.That(State.Held,Is.Null);Assert.That(bootstrap.Dishware.HeldView.Base.enabled,Is.True);Assert.That(bootstrap.Hud.Status.text,Does.Contain("Левая рука: Миска"));Capture("dishware-held.png");
            yield return Aim(TrayPoint(0));yield return HandClick();Assert.That(State.HeldDishware,Is.Not.Null);
            Teleport(new Vector3(-9.7f,.05f,-6.25f));yield return Aim(Socket(1));yield return HandClick();
            Assert.That(State.CurrentDishware.Id,Is.EqualTo("bowl"));Assert.That(State.HeldDishware,Is.Null);Assert.That(State.Served.Count,Is.Zero);Assert.That(State.Portions.Where(p=>ids.Contains(p.Id)).All(p=>p.Location==PortionLocation.Trash),Is.True);Assert.That(State.PresentationPenalty,Is.EqualTo(1));Assert.That(bootstrap.Serving.Stations.Single(t=>t.StationId=="A1").Status.text,Does.Contain("−1"));
            Capture("dishware-replacement-discarded.png");var small=bootstrap.Dishware.Supply.First(t=>t.Team==TeamId.A && t.Definition.Id=="kosushka");
            Teleport(new Vector3(-10.7f,.05f,small.transform.position.z));yield return Aim(small.transform.position+Vector3.up*.07f);yield return HandClick();
            yield return Press(UnityEngine.InputSystem.Key.Backspace);Assert.That(State.HeldDishware,Is.Null);Assert.That(State.CurrentDishware.Id,Is.EqualTo("bowl"));
            bootstrap.RestartShow();yield return null;Assert.That(State.CurrentDishware.Id,Is.EqualTo("small_flat"));Assert.That(State.Served.Count,Is.Zero);
        }
        [UnityTest] public IEnumerator MouseAllFiveDishwareTypesAndForeignTeamGuardsUseSavedTargets()
        {
            foreach(var id in bootstrap.Dishware.Config.Types.Select(t=>t.Id))
            {
                var source=bootstrap.Dishware.Supply.First(t=>t.Team==TeamId.A && t.Definition.Id==id);
                Teleport(new Vector3(-10.7f,.05f,source.transform.position.z));yield return Aim(source.transform.position+Vector3.up*.07f);yield return HandClick();Assert.That(State.HeldDishware.Id,Is.EqualTo(id));
                Teleport(new Vector3(-9.7f,.05f,-6.25f));yield return Aim(Socket(1));yield return HandClick();Assert.That(State.CurrentDishware.Id,Is.EqualTo(id));
            }
            var foreign=bootstrap.Dishware.Supply.First(t=>t.Team==TeamId.B);Teleport(new Vector3(10.7f,.05f,foreign.transform.position.z));yield return Aim(foreign.transform.position+Vector3.up*.07f);yield return HandClick();Assert.That(State.HeldDishware,Is.Null);
            Assert.That(bootstrap.Hud.Context.text,Does.Contain("другой команды"));bootstrap.SetPaused(true);yield return HandClick();Assert.That(State.HeldDishware,Is.Null);bootstrap.SetPaused(false);
            Assert.That(bootstrap.Dishware.Supply.Length,Is.EqualTo(60));Assert.That(bootstrap.Dishware.Config.Types.Length,Is.EqualTo(5));
        }
        [UnityTest] public IEnumerator MouseCanSaveFoodToBoardRemovePlateAndSubmitWithRedButtonOnLeft()
        {
            yield return MouseTrip("beef","salt");yield return Aim(TrayPoint(0));yield return HandClick();var food=State.Held;yield return Aim(Socket(1));yield return HandClick();
            var station=bootstrap.Serving.Stations.Single(t=>t.StationId=="A1");
            yield return Aim(station.Food[0].Visual.bounds.center);yield return HandClick();Assert.That(State.Held,Is.SameAs(food));yield return Aim(Socket(0));yield return HandClick();
            yield return Aim(Socket(1));yield return HandClick();Assert.That(State.CurrentDishware,Is.Null);Assert.That(State.HeldDishware,Is.Not.Null);Assert.That(State.PresentationPenalty,Is.EqualTo(1));Assert.That(station.PlateView.Base.enabled,Is.False);Assert.That(bootstrap.Hud.Status.text,Does.Contain("Презентабельность: −1"));
            yield return Press(UnityEngine.InputSystem.Key.Backspace);var source=bootstrap.Dishware.Supply.First(t=>t.Team==TeamId.A && t.Definition.Id=="bowl");
            Teleport(new Vector3(-10.7f,.05f,source.transform.position.z));yield return Aim(source.transform.position+Vector3.up*.07f);yield return HandClick();
            Teleport(new Vector3(-9.7f,.05f,-6.25f));yield return Aim(Socket(1));yield return HandClick();Assert.That(State.PresentationPenalty,Is.EqualTo(1));
            yield return Aim(Socket(0));yield return HandClick();yield return Aim(Socket(1));yield return HandClick();Assert.That(State.Served.Single(),Is.SameAs(food));
            var button=station.transform.Find("Plate Contents/Submit Dish");Assert.That(button.position.z,Is.GreaterThan(station.PlateView.transform.position.z));Assert.That(station.SubmitLight.enabled,Is.False);Assert.That(station.SubmitButton.sharedMaterial.IsKeywordEnabled("_EMISSION"),Is.True);
            yield return Aim(button.position);bootstrap.SetPaused(true);yield return HandClick();Assert.That(State.SubmittedDish,Is.Null);bootstrap.SetPaused(false);
            yield return Aim(button.position);Assert.That(bootstrap.Player.Target.GetComponent<ServingTarget>().Submit,Is.True);yield return HandClick();Assert.That(State.SubmittedDish.PresentationPenalty,Is.EqualTo(1));Assert.That(station.SubmitLight.enabled,Is.True);Capture("submission-red-locked.png");
            yield return Aim(station.Food[0].Visual.bounds.center);yield return HandClick();Assert.That(State.Served.Single(),Is.SameAs(food));Assert.That(State.Held,Is.Null);
            yield return Aim(TrayPoint(0));yield return HandClick();yield return Aim(Socket(1));yield return HandClick();Assert.That(State.PlateSaltDoses,Is.Zero);
            yield return Press(UnityEngine.InputSystem.Key.Backspace);Assert.That(State.SubmittedDish.Portions.Single().Id,Is.EqualTo(food.Id));
            bootstrap.RestartShow();yield return null;Assert.That(station.SubmitLight.enabled,Is.False);Assert.That(State.PresentationPenalty,Is.Zero);Assert.That(State.CurrentDishware.Id,Is.EqualTo("small_flat"));
        }
    }
}
