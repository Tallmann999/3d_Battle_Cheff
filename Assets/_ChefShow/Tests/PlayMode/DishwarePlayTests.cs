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
        [UnityTest] public IEnumerator MouseDishwareCanReplaceFilledPlateWithoutLosingFoodAndShowsInLeftHand()
        {
            yield return MouseTrip("beef","egg");for(int i=0;i<2;i++){yield return Aim(TrayPoint(0));yield return HandClick();yield return Aim(Socket(1));yield return HandClick();}
            var ids=State.Served.Select(p=>p.Id).ToArray();var target=bootstrap.Dishware.Supply.First(t=>t.Team==TeamId.A && t.Definition.Id=="bowl");
            Teleport(new Vector3(-10.7f,.05f,target.transform.position.z));yield return Aim(target.transform.position+Vector3.up*.07f);yield return HandClick();
            Assert.That(State.HeldDishware.Id,Is.EqualTo("bowl"));Assert.That(State.Held,Is.Null);Assert.That(bootstrap.Dishware.HeldView.Base.enabled,Is.True);Assert.That(bootstrap.Hud.Status.text,Does.Contain("Левая рука: Миска"));Capture("dishware-held.png");
            yield return Aim(TrayPoint(0));yield return HandClick();Assert.That(State.HeldDishware,Is.Not.Null);
            Teleport(new Vector3(-9.7f,.05f,-6.25f));yield return Aim(Socket(1));yield return HandClick();
            Assert.That(State.CurrentDishware.Id,Is.EqualTo("bowl"));Assert.That(State.HeldDishware,Is.Null);Assert.That(State.Served.Select(p=>p.Id),Is.EqualTo(ids));
            Capture("dishware-bowl-filled.png");var small=bootstrap.Dishware.Supply.First(t=>t.Team==TeamId.A && t.Definition.Id=="kosushka");
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
    }
}
