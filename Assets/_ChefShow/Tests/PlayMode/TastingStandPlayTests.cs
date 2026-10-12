using System.Collections;
using System.Linq;
using ChefShow.Cooking;
using ChefShow.Inventory;
using ChefShow.Judging;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace ChefShow.Tests
{
    public sealed partial class InventoryPlayTests
    {
        private InventoryInteractable OwnTrash => Object.FindObjectsByType<InventoryInteractable>(FindObjectsSortMode.None)
            .Single(t => t.Kind == InventoryTargetKind.Trash && t.StationId == inventory.PlayerStationId);
        private IEnumerator MouseTrashPlate()
        {
            Teleport(new Vector3(-9.7f, .05f, -6.25f)); yield return Aim(OwnTrash.GetComponent<BoxCollider>().bounds.center);
            Assert.That(bootstrap.Player.Target.GetComponent<InventoryInteractable>(), Is.SameAs(OwnTrash)); yield return HandClick();
            Assert.That(State.HeldDishware, Is.Null); Assert.That(State.HeldServed, Is.Empty);
        }

        [UnityTest] public IEnumerator MouseTastingWholePlateRepeatedSubmissionKeepsIdsAndRemainsResponsive()
        {
            foreach (var station in bootstrap.Serving.Stations)
            {
                Assert.That(station.TastingMount, Is.Not.Null); Assert.That(station.TastingTarget.gameObject.activeInHierarchy, Is.True);
                Assert.That(station.TastingTarget.Station, Is.SameAs(station)); Assert.That(station.TastingTarget.GetComponent<BoxCollider>().isTrigger, Is.True);
                Assert.That(station.TastingPlateView, Is.Not.Null); Assert.That(station.TastingStatus, Is.Not.Null);
                Assert.That(station.transform.Find("Plate Contents/Submit Dish").gameObject.activeInHierarchy, Is.False);
                Assert.That(station.transform.Find("Plate Contents/Reset Submission").gameObject.activeInHierarchy, Is.False);
            }
            yield return MouseTrip("beef", "salt"); yield return Aim(TrayPoint(0)); yield return HandClick(); var beef = State.Held;
            var pan = Appliance(CookerKind.Pan); yield return Aim(pan.transform.position + Vector3.up * .025f); yield return HandClick();
            yield return HeatTwice(pan); yield return RecognitionReady(beef); yield return Aim(CookFoodPoint(pan, 0)); yield return HandClick();
            yield return Aim(Socket(1)); yield return HandClick(); yield return Aim(TrayPoint(0)); yield return HandClick();
            yield return Aim(Socket(1)); yield return HandClick(); yield return Press(Key.Backspace);
            var ids = State.Served.Select(p => p.Id).ToArray(); int count = State.Portions.Count, facts = 0; ScoreBreakdown firstScore = null;
            using (bootstrap.Run.Events.Subscribe<DishJudged>(fact => { facts++; Assert.That(fact.Score, Is.SameAs(bootstrap.Judging.Current)); }))
            {
                var watch = System.Diagnostics.Stopwatch.StartNew(); yield return JudgingSubmit(); var firstDish = State.SubmittedDish; firstScore = bootstrap.Judging.Current;
                Assert.That(OwnServing.TastingPlateView.Base.enabled, Is.True); Assert.That(OwnServing.PlateView.Base.enabled, Is.False);
                Assert.That(OwnServing.Food[0].Visual.enabled, Is.True); Assert.That(OwnServing.Food[0].GetComponent<BoxCollider>().enabled, Is.False);
                Capture("tasting-whole-plate-assessed.png");
                float clock = bootstrap.Run.Clock.SimulationTime; var position = bootstrap.Player.transform.position;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W)); yield return new WaitForSecondsRealtime(.2f);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
                Assert.That(Vector3.Distance(bootstrap.Player.transform.position, position), Is.GreaterThan(.01f));
                Assert.That(bootstrap.Run.Clock.SimulationTime, Is.GreaterThan(clock));
                for (int attempt = 1; attempt < 12; attempt++)
                {
                    yield return JudgingReset(); Assert.That(State.Served.Select(p => p.Id), Is.EqualTo(ids));
                    yield return JudgingSubmit(); yield return null; yield return null;
                    Assert.That(facts, Is.EqualTo(attempt + 1)); Assert.That(State.SubmittedDish.Portions.Select(p => p.Id), Is.EqualTo(ids));
                    Assert.That(State.SubmittedDish.SaltDoses, Is.EqualTo(1)); Assert.That(State.PresentationPenalty, Is.Zero);
                    Assert.That(bootstrap.Judging.Current.UnroundedTotal, Is.EqualTo(firstScore.UnroundedTotal));
                    Assert.That(State.Portions.Count, Is.EqualTo(count)); Assert.That(State.Portions.Any(p => p.Location == PortionLocation.Trash), Is.False);
                    Assert.That(firstDish.Portions.Single().Location, Is.EqualTo(PortionLocation.TastingStand));
                }
                watch.Stop(); TestContext.WriteLine("12 physical submit/retake cycles: " + watch.Elapsed.TotalSeconds.ToString("0.000") + " s; DishJudged=" + facts);
                Assert.That(firstDish.SaltDoses, Is.EqualTo(1)); Assert.That(firstDish.Portions.Single().Id, Is.EqualTo(beef.Id)); JudgingTextFits();
            }
        }

        [UnityTest] public IEnumerator MouseAssessedPlateTrashRemovesWholeDishAndFreshShelfPlateCanBeJudgedAgain()
        {
            yield return MouseTrip("beef", "egg", "salt");
            for (int i = 0; i < 2; i++) { yield return Aim(TrayPoint(0)); yield return HandClick(); yield return Aim(Socket(1)); yield return HandClick(); }
            yield return Aim(TrayPoint(0)); yield return HandClick(); yield return Aim(Socket(1)); yield return HandClick(); yield return Press(Key.Backspace);
            var food = State.Served.ToArray(); int facts = 0;
            using (bootstrap.Run.Events.Subscribe<DishJudged>(fact => facts++))
            {
                yield return JudgingSubmit(); var oldDish = State.SubmittedDish; var oldScore = bootstrap.Judging.Current; double oldTotal = oldScore.UnroundedTotal;
                yield return TastingRetrieve(); Assert.That(State.HeldServed, Is.EqualTo(food)); Assert.That(State.HeldPlateSaltDoses, Is.EqualTo(1));
                yield return MouseTrashPlate(); Assert.That(State.TastingDishware, Is.Null); Assert.That(State.SubmittedDish, Is.Null);
                Assert.That(State.HeldPlateSaltDoses + State.HeldPlateOilDoses, Is.Zero); Assert.That(State.PresentationPenalty, Is.Zero);
                Assert.That(food.All(p => p.Location == PortionLocation.Trash), Is.True);
                Assert.That(food.All(p => p.Operations.Count(o => o.Action == "discarded_with_dishware") == 1), Is.True);
                Assert.That(bootstrap.Dishware.HeldView.Base.enabled, Is.False); Assert.That(OwnServing.Food.All(f => !f.Visual.enabled), Is.True);
                Assert.That(oldDish.Quantity, Is.EqualTo(2)); Assert.That(oldDish.SaltDoses, Is.EqualTo(1)); Assert.That(oldScore.UnroundedTotal, Is.EqualTo(oldTotal));
                var supply = bootstrap.Dishware.Supply.First(t => t.Team == ChefShow.Data.TeamId.A && t.Definition.Id == "small_flat");
                ApproachShelf(supply); yield return Aim(supply.GetComponent<BoxCollider>().bounds.center); yield return HandClick();
                Teleport(new Vector3(-9.7f, .05f, -6.25f)); yield return Aim(Socket(1)); yield return HandClick(); Assert.That(State.CurrentDishware.Id, Is.EqualTo("small_flat"));
                Teleport(new Vector3(Stock("beef").x, .05f, 10.3f)); yield return Aim(Stock("beef")); yield return HandClick(); var fresh = State.Held;
                Assert.That(food.Select(p => p.Id), Does.Not.Contain(fresh.Id)); Teleport(new Vector3(-9.7f, .05f, -6.25f));
                var pan = Appliance(CookerKind.Pan); yield return Aim(pan.transform.position + Vector3.up * .025f); yield return HandClick(); yield return HeatTwice(pan); yield return RecognitionReady(fresh);
                yield return Aim(CookFoodPoint(pan, 0)); yield return HandClick(); yield return Aim(Socket(1)); yield return HandClick(); yield return JudgingSubmit();
                Assert.That(facts, Is.EqualTo(2)); Assert.That(State.SubmittedDish.Portions.Single().Id, Is.EqualTo(fresh.Id)); Assert.That(State.SubmittedDish.SaltDoses, Is.Zero);
                Assert.That(bootstrap.Judging.Current.UnroundedTotal, Is.GreaterThan(oldTotal)); Assert.That(oldDish.Quantity, Is.EqualTo(2));
                Capture("tasting-trash-fresh-attempt.png");
            }
        }

        [UnityTest] public IEnumerator MouseTastingGuardsBookPauseForeignOccupiedHandDisabledRetakeAndHeldTimeout()
        {
            ApproachTasting(OwnServing); yield return Aim(OwnServing.TastingTarget.transform.position); yield return HandClick();
            Assert.That(State.SubmittedDish, Is.Null); Assert.That(State.TastingDishware, Is.Null);
            yield return MouseTrip("beef", "egg"); yield return Aim(TrayPoint(0)); yield return HandClick(); var beef = State.Held;
            yield return Aim(Socket(1)); yield return HandClick(); yield return Aim(Socket(1)); yield return HandClick(); var ids = State.HeldServed.Select(p => p.Id).ToArray();
            ApproachTasting(OwnServing); yield return Aim(OwnServing.TastingTarget.transform.position); yield return Press(Key.I); yield return HandClick();
            Assert.That(State.TastingDishware, Is.Null); Assert.That(State.HeldServed.Select(p => p.Id), Is.EqualTo(ids)); yield return Press(Key.I);
            bootstrap.SetPaused(true); yield return HandClick(); Assert.That(State.TastingDishware, Is.Null); bootstrap.SetPaused(false);
            var foreign = bootstrap.Serving.Stations.Single(s => s.StationId == "B1"); ApproachTasting(foreign); yield return Aim(foreign.TastingTarget.transform.position); yield return HandClick();
            Assert.That(State.HeldServed.Select(p => p.Id), Is.EqualTo(ids)); Assert.That(State.SubmittedDish, Is.Null);
            ApproachTasting(OwnServing); yield return Aim(OwnServing.TastingTarget.transform.position); yield return HandClick(); var submitted = State.SubmittedDish;
            Assert.That(submitted.Portions.Single().Id, Is.EqualTo(beef.Id)); bootstrap.Serving.EnableSubmissionReset = false; yield return HandClick();
            Assert.That(State.SubmittedDish, Is.SameAs(submitted)); Assert.That(State.HeldDishware, Is.Null); bootstrap.Serving.EnableSubmissionReset = true;
            Teleport(new Vector3(-9.7f, .05f, -6.25f)); yield return Aim(TrayPoint(0)); yield return HandClick(); var held = State.Held;
            ApproachTasting(OwnServing); yield return Aim(OwnServing.TastingTarget.transform.position); yield return HandClick();
            Assert.That(State.Held, Is.SameAs(held)); Assert.That(State.SubmittedDish, Is.SameAs(submitted)); yield return Press(Key.Backspace);
            bootstrap.SetPaused(true); yield return HandClick(); Assert.That(State.SubmittedDish, Is.SameAs(submitted)); bootstrap.SetPaused(false);
            yield return TastingRetrieve();
            Teleport(new Vector3(-9.7f, .05f, -6.25f)); yield return Aim(OwnTrash.GetComponent<BoxCollider>().bounds.center);
            Assert.That(bootstrap.Hud.Context.text, Does.Contain("Выбросить"));
            bootstrap.Run.SetRemaining(.01f); yield return null; yield return null;
            Assert.That(State.SubmittedDish.Quantity, Is.Zero); Assert.That(State.HeldServed.Select(p => p.Id), Is.EqualTo(ids)); Assert.That(bootstrap.Judging.Current.UnroundedTotal, Is.Zero);
            yield return HandClick(); Assert.That(State.TastingDishware, Is.Null); Assert.That(State.HeldServed.Count, Is.EqualTo(1));
            Assert.That(State.HeldDishware, Is.Not.Null); Assert.That(beef.Location, Is.EqualTo(PortionLocation.HeldDishware)); Assert.That(submitted.Quantity, Is.EqualTo(1));
            Capture("tasting-timeout-held-excluded.png");
        }
    }
}
