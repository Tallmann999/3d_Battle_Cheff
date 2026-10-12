using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ChefShow.Chefs;
using ChefShow.Cooking;
using ChefShow.Inventory;
using ChefShow.Judging;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace ChefShow.Tests
{
    public sealed partial class InventoryPlayTests
    {
        private IEnumerator AwaitReaction(ReactionCue cue, string subject = null, float timeout = 5)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            while ((bootstrap.Reactions.Active == null || bootstrap.Reactions.Active.Cue != cue
                || (subject != null && bootstrap.Reactions.Active.SubjectKey != subject)) && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(bootstrap.Reactions.Active, Is.Not.Null, "Expected " + cue + "; pending=" + string.Join(",", bootstrap.Reactions.Queue.Pending.Select(p => p.Cue)));
            Assert.That(bootstrap.Reactions.Active.Cue, Is.EqualTo(cue));
            if (subject != null) Assert.That(bootstrap.Reactions.Active.SubjectKey, Is.EqualTo(subject));
        }
        private void ReactionTextFits()
        {
            Canvas.ForceUpdateCanvases();
            foreach (var text in new[] { bootstrap.Reactions.SpeakerText, bootstrap.Reactions.SubtitleText })
                Assert.That(text.preferredHeight, Is.LessThanOrEqualTo(text.rectTransform.rect.height), text.text + " clipped");
            Assert.That(bootstrap.Reactions.Bindings.Count(b => b.SpeakingMarker != null && b.SpeakingMarker.enabled), Is.LessThanOrEqualTo(1));
        }

        [UnityTest] public IEnumerator MouseBurnedFoodStartsOneFactualChefSequenceAndPauseBookPreserveClockRules()
        {
            Assert.That(bootstrap.Reactions, Is.Not.Null); Assert.That(bootstrap.Reactions.Validate(), Is.Null);
            var started = new List<ReactionStarted>();
            using (bootstrap.Run.Events.Subscribe<ReactionStarted>(fact => { Assert.That(fact.RunId, Is.EqualTo(bootstrap.Run.RunId)); started.Add(fact); }))
            {
                yield return MouseTrip("beef"); yield return Aim(TrayPoint(0)); yield return HandClick(); var beef = State.Held;
                var pan = Appliance(CookerKind.Pan); yield return Aim(pan.transform.position + Vector3.up * .025f); yield return HandClick(); yield return HeatTwice(pan);
                bootstrap.Run.Clock.SetSpeed(20); float deadline = Time.realtimeSinceStartup + 7;
                while (beef.Cooking != CookState.Burned && Time.realtimeSinceStartup < deadline) yield return null;
                bootstrap.Run.Clock.SetSpeed(1); Assert.That(beef.Cooking, Is.EqualTo(CookState.Burned)); Assert.That(pan.Smoke.All(r => r.enabled), Is.True);
                yield return AwaitReaction(ReactionCue.FoodBurned, beef.Id);
                var active = bootstrap.Reactions.Active;
                Assert.That(active.Chef, Is.EqualTo(ChefSpeaker.Savory)); Assert.That(active.SubjectName, Is.EqualTo(InventoryController.FoodName(beef.Ingredient)));
                Assert.That(bootstrap.Reactions.Panel.activeInHierarchy, Is.True); Assert.That(bootstrap.Reactions.SpeakerText.text, Is.EqualTo(bootstrap.Reactions.Settings.SavoryName));
                ReactionTextFits(); Capture("reaction-burned-food.png");
                var now = bootstrap.Reactions.Queue.Now; var heat = beef.HeatProgress; var actor = bootstrap.Reactions.Bindings.Single(b => b.Speaker == ChefSpeaker.Savory).Actor;
                var rotation = actor.rotation; bootstrap.SetPaused(true); yield return new WaitForSecondsRealtime(.2f);
                Assert.That(bootstrap.Reactions.Queue.Now, Is.EqualTo(now)); Assert.That(bootstrap.Reactions.Active, Is.SameAs(active));
                Assert.That(actor.rotation, Is.EqualTo(rotation)); Assert.That(bootstrap.Reactions.Panel.activeInHierarchy, Is.False); Assert.That(beef.HeatProgress, Is.EqualTo(heat));
                bootstrap.SetPaused(false); yield return Press(Key.I); now = bootstrap.Reactions.Queue.Now; var gameTime = bootstrap.Run.Clock.SimulationTime;
                yield return new WaitForSecondsRealtime(.2f); Assert.That(bootstrap.Reactions.Queue.Now, Is.GreaterThan(now)); Assert.That(bootstrap.Run.Clock.SimulationTime, Is.GreaterThan(gameTime));
                Assert.That(bootstrap.Reactions.Panel.activeInHierarchy, Is.False); yield return Press(Key.I);
                int version = State.Version; float remaining = bootstrap.Run.RemainingSeconds; gameTime = bootstrap.Run.Clock.SimulationTime;
                bootstrap.Run.Clock.SetSpeed(20); deadline = Time.realtimeSinceStartup + 3;
                while (bootstrap.Reactions.Queue.IsSequenceActive && Time.realtimeSinceStartup < deadline) yield return null;
                bootstrap.Run.Clock.SetSpeed(1);
                Assert.That(started.Where(f => f.Playback.Cue == ReactionCue.FoodBurned && f.Playback.SubjectKey == beef.Id).Select(f => f.Playback.SequenceId).Distinct().Count(), Is.EqualTo(1));
                Assert.That(started.Where(f => f.Playback.Cue == ReactionCue.FoodReady && f.Playback.SubjectKey == beef.Id).Select(f => f.Playback.SequenceId).Distinct().Count(), Is.LessThanOrEqualTo(1));
                Assert.That(State.Version, Is.EqualTo(version)); Assert.That(State.Cooker(CookerKind.Pan).Single(), Is.SameAs(beef)); Assert.That(beef.HeatProgress, Is.EqualTo(heat));
                Assert.That(remaining - bootstrap.Run.RemainingSeconds, Is.EqualTo(bootstrap.Run.Clock.SimulationTime - gameTime).Within(.02f));
            }
        }

        [UnityTest] public IEnumerator MouseRawThenCookedTastingDishChangesCommentWithoutChangingEitherScore()
        {
            var started = new List<ReactionStarted>(); int judged = 0;
            using (bootstrap.Run.Events.Subscribe<ReactionStarted>(started.Add))
            using (bootstrap.Run.Events.Subscribe<DishJudged>(fact => judged++))
            {
                yield return MouseTrip("beef"); yield return Aim(TrayPoint(0)); yield return HandClick(); var beef = State.Held;
                yield return Aim(Socket(1)); yield return HandClick(); yield return JudgingSubmit(); yield return AwaitReaction(ReactionCue.JudgingRaw);
                var rawDish = State.SubmittedDish; var raw = bootstrap.Judging.Current; double rawTotal = raw.UnroundedTotal;
                Assert.That(raw.SafetyIssues & ScoreSafetyIssues.RawMandatoryProtein, Is.EqualTo(ScoreSafetyIssues.RawMandatoryProtein)); Assert.That(rawTotal, Is.LessThanOrEqualTo(45));
                Assert.That(bootstrap.Reactions.Phase, Is.EqualTo(ReactionPhase.Judging)); Assert.That(bootstrap.Reactions.Active.Chef, Is.EqualTo(ChefSpeaker.Savory));
                ReactionTextFits(); Capture("reaction-raw-tasting.png");
                yield return JudgingReset(); Assert.That(bootstrap.Reactions.Phase, Is.EqualTo(ReactionPhase.Cooking));
                yield return Aim(ServingFoodPoint()); yield return HandClick(); var pan = Appliance(CookerKind.Pan);
                yield return Aim(pan.transform.position + Vector3.up * .025f); yield return HandClick(); yield return HeatTwice(pan); yield return RecognitionReady(beef);
                yield return Aim(CookFoodPoint(pan, 0)); yield return HandClick(); yield return Aim(Socket(1)); yield return HandClick(); yield return JudgingSubmit();
                yield return AwaitReaction(ReactionCue.JudgingGood); var good = bootstrap.Judging.Current; double goodTotal = good.UnroundedTotal;
                Assert.That(goodTotal, Is.GreaterThanOrEqualTo(bootstrap.Reactions.Settings.GoodScoreThreshold)); Assert.That(good.SafetyIssues, Is.EqualTo(ScoreSafetyIssues.None));
                Assert.That(State.SubmittedDish.Portions.Single().Id, Is.EqualTo(beef.Id)); Assert.That(rawDish.Portions.Single().Cooking, Is.EqualTo(CookState.Raw));
                Assert.That(raw.UnroundedTotal, Is.EqualTo(rawTotal)); Assert.That(judged, Is.EqualTo(2)); Assert.That(bootstrap.Reactions.Panel.activeInHierarchy, Is.True);
                Assert.That(started.Count(f => f.Playback.Cue == ReactionCue.DishSubmitted), Is.Zero);
                Assert.That(bootstrap.Reactions.Queue.Pending.All(p => p.Phase != ReactionPhase.Cooking || ReactionQueue.IsTimer(p.Cue)), Is.True);
                yield return null; yield return null; Assert.That(bootstrap.Judging.Current.UnroundedTotal, Is.EqualTo(goodTotal)); Assert.That(judged, Is.EqualTo(2));
                ReactionTextFits(); Capture("reaction-good-tasting.png");
            }
        }

        [UnityTest] public IEnumerator TimerJumpUsesUrgentSystemSpeechAndFinishesAfterZeroThenRestartClearsPresentation()
        {
            var started = new List<ReactionStarted>(); var cancelled = new List<ReactionCancelled>(); int judged = 0;
            using (bootstrap.Run.Events.Subscribe<ReactionStarted>(started.Add))
            using (bootstrap.Run.Events.Subscribe<ReactionCancelled>(cancelled.Add))
            using (bootstrap.Run.Events.Subscribe<DishJudged>(fact => judged++))
            {
                bootstrap.Run.SetRemaining(29); yield return null; yield return null; yield return AwaitReaction(ReactionCue.Time30);
                var thirty = bootstrap.Reactions.Active;
                bootstrap.Run.SetRemaining(9); yield return null; yield return null; yield return AwaitReaction(ReactionCue.Time10); var ten = bootstrap.Reactions.Active;
                Assert.That(cancelled.Count(f => f.Playback.SequenceId == thirty.SequenceId), Is.EqualTo(1));
                bootstrap.Run.SetRemaining(.01f); yield return null; yield return null; yield return AwaitReaction(ReactionCue.TimeUp);
                Assert.That(cancelled.Count(f => f.Playback.SequenceId == ten.SequenceId), Is.EqualTo(1)); Assert.That(State.SubmittedDish.Quantity, Is.Zero); Assert.That(judged, Is.EqualTo(1));
                Assert.That(bootstrap.Reactions.Phase, Is.EqualTo(ReactionPhase.Judging));
                Assert.That(started.Any(f => f.Playback.Cue == ReactionCue.Time60), Is.False);
                foreach (var cue in new[] { ReactionCue.Time30, ReactionCue.Time10, ReactionCue.TimeUp })
                    Assert.That(started.Where(f => f.Playback.Cue == cue).Select(f => f.Playback.SequenceId).Distinct().Count(), Is.EqualTo(1));
                var frozenSimulation = bootstrap.Run.Clock.SimulationTime; var speechTime = bootstrap.Reactions.Queue.Now;
                bootstrap.SetPaused(true); yield return new WaitForSecondsRealtime(.2f); Assert.That(bootstrap.Reactions.Queue.Now, Is.EqualTo(speechTime));
                bootstrap.SetPaused(false); yield return Press(Key.I); yield return new WaitForSecondsRealtime(.2f);
                Assert.That(bootstrap.Reactions.Panel.activeInHierarchy, Is.False); Assert.That(bootstrap.Reactions.Queue.Now, Is.GreaterThan(speechTime));
                Assert.That(bootstrap.Run.Clock.SimulationTime, Is.EqualTo(frozenSimulation)); yield return Press(Key.I); ReactionTextFits(); Capture("reaction-time-up.png");
                float deadline = Time.realtimeSinceStartup + bootstrap.Reactions.Settings.MaximumDuration + 2;
                while (bootstrap.Reactions.Active != null && bootstrap.Reactions.Active.Cue == ReactionCue.TimeUp && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(bootstrap.Reactions.Active == null || bootstrap.Reactions.Active.Cue != ReactionCue.TimeUp, Is.True);
                Assert.That(bootstrap.Run.RemainingSeconds, Is.Zero); Assert.That(bootstrap.Run.Clock.SimulationTime, Is.EqualTo(frozenSimulation)); Assert.That(judged, Is.EqualTo(1));
            }
            var oldQueue = bootstrap.Reactions.Queue; string runId = bootstrap.Run.RunId;
            bootstrap.RestartShow(); yield return null;
            Assert.That(bootstrap.Run.RunId, Is.Not.EqualTo(runId)); Assert.That(bootstrap.Reactions.Queue, Is.Not.SameAs(oldQueue));
            Assert.That(bootstrap.Reactions.Active, Is.Null); Assert.That(bootstrap.Reactions.Queue.Pending, Is.Empty); Assert.That(bootstrap.Reactions.Queue.History, Is.Empty);
            Assert.That(bootstrap.Reactions.Panel.activeInHierarchy, Is.False); Assert.That(bootstrap.Reactions.SubtitleText.text, Is.Empty);
            Assert.That(bootstrap.Reactions.Bindings.All(b => b.SpeakingMarker == null || !b.SpeakingMarker.enabled), Is.True);
        }
        private FoodPortion ReactionFixtureCookerFood(string id)
        {
            Assert.That(State.TryCollect(inventory.Catalog.Ingredients.Single(i => i.Id == id), out var reason), Is.True, reason);
            var food = State.Held; Assert.That(State.TryPlaceCooker(CookerKind.Pan, out reason), Is.True, reason); return food;
        }

        [UnityTest] public IEnumerator RealDisposalEventsCancelOnlyArchivedFoodCandidatesAndPreserveOtherFood()
        {
            // Stage real inventory operations synchronously so the existing speech/cooldown
            // keeps the later factual candidates pending. No event or quality state is fabricated.
            var queue = bootstrap.Reactions.Queue; var anchor = ReactionFixtureCookerFood("beef");
            while (State.Heat(CookerKind.Pan) != HeatLevel.Medium) Assert.That(State.TryCycleHeat(CookerKind.Pan, out _), Is.True);
            bootstrap.Run.Tick(State.CookingRules.Ready); yield return AwaitReaction(ReactionCue.FoodReady, anchor.Id);
            Assert.That(State.TryTakeCooker(CookerKind.Pan, 0, out _), Is.True); Assert.That(State.TryPutInTray(out _), Is.True);
            var discarded = ReactionFixtureCookerFood("beef"); var returned = ReactionFixtureCookerFood("egg"); var plated = ReactionFixtureCookerFood("potato");
            bootstrap.Run.Tick(State.CookingRules.Ready); bootstrap.Run.Tick(State.CookingRules.Burned - State.CookingRules.Ready);
            foreach (var food in new[] { discarded, returned, plated })
            {
                Assert.That(food.Cooking, Is.EqualTo(CookState.Burned));
                Assert.That(queue.Pending.Any(c => c.Cue == ReactionCue.FoodBurned && c.SubjectKey == food.Id), Is.True);
            }
            Assert.That(State.TryTakeCooker(CookerKind.Pan, 2, out _), Is.True); Assert.That(State.TryPlaceServing(out _), Is.True);
            var unrelated = ReactionFixtureCookerFood("carrot"); bootstrap.Run.Tick(State.CookingRules.Ready);
            Assert.That(State.TryTakeCooker(CookerKind.Pan, 2, out _), Is.True); Assert.That(State.TryPutInTray(out _), Is.True);
            Assert.That(queue.Pending.Any(c => c.Cue == ReactionCue.FoodReady && c.SubjectKey == unrelated.Id), Is.True);
            var untouched = unrelated.Snapshot(); var score = bootstrap.Judging.Current; Assert.That(score, Is.Null);

            Assert.That(State.TryTakeCooker(CookerKind.Pan, 0, out _), Is.True); int version = State.Version;
            Assert.That(State.TryRemove(false, out _), Is.True); Assert.That(State.Version, Is.EqualTo(version + 1));
            Assert.That(discarded.Location, Is.EqualTo(PortionLocation.Trash)); Assert.That(queue.Pending.Any(c => c.SubjectKey == discarded.Id), Is.False);
            Assert.That(queue.Pending.Any(c => c.SubjectKey == returned.Id), Is.True); Assert.That(queue.Pending.Any(c => c.SubjectKey == unrelated.Id), Is.True);
            Assert.That(State.TryTakeCooker(CookerKind.Pan, 0, out _), Is.True); version = State.Version;
            Assert.That(State.TryRemove(true, out _), Is.True); Assert.That(State.Version, Is.EqualTo(version + 1));
            Assert.That(returned.Location, Is.EqualTo(PortionLocation.Returned)); Assert.That(queue.Pending.Any(c => c.SubjectKey == returned.Id), Is.False);

            var oldPlate = State.CaptureDish(); Assert.That(State.TryTakePlacedDishware(out _), Is.True);
            Assert.That(queue.Pending.Any(c => c.SubjectKey == plated.Id), Is.True); Assert.That(State.TryDiscardHeldDishware(out _), Is.True);
            Assert.That(plated.Location, Is.EqualTo(PortionLocation.Trash)); Assert.That(queue.Pending.Any(c => c.SubjectKey == plated.Id), Is.False);
            Assert.That(queue.Pending.Any(c => c.SubjectKey == unrelated.Id), Is.True); Assert.That(State.HeldDishware, Is.Null);

            // Direct replacement publishes dishware_selected; it must perform the same cleanup.
            Assert.That(State.TryTakeDishware("small_flat", out _), Is.True); Assert.That(State.TryPlaceDishware(out _), Is.True);
            var replaced = ReactionFixtureCookerFood("beef"); bootstrap.Run.Tick(State.CookingRules.Ready);
            Assert.That(State.TryTakeCooker(CookerKind.Pan, 0, out _), Is.True); Assert.That(State.TryPlaceServing(out _), Is.True);
            Assert.That(queue.Pending.Any(c => c.Cue == ReactionCue.FoodReady && c.SubjectKey == replaced.Id), Is.True);
            Assert.That(State.TryTakeDishware("bowl", out _), Is.True); Assert.That(State.TryPlaceDishware(out _), Is.True);
            Assert.That(replaced.Location, Is.EqualTo(PortionLocation.Trash)); Assert.That(queue.Pending.Any(c => c.SubjectKey == replaced.Id), Is.False);
            Assert.That(queue.Pending.Any(c => c.SubjectKey == unrelated.Id), Is.True); Assert.That(bootstrap.Judging.Current, Is.SameAs(score));
            Assert.That(State.Tray.Any(p => ReferenceEquals(p, unrelated)), Is.True); Assert.That(unrelated.Cooking, Is.EqualTo(untouched.Cooking));
            Assert.That(unrelated.HeatProgress, Is.EqualTo(untouched.HeatProgress)); Assert.That(unrelated.Operations, Is.EqualTo(untouched.Operations));
            Assert.That(anchor.Cooking, Is.EqualTo(CookState.Cooked)); Assert.That(oldPlate.Portions.Single().Id, Is.EqualTo(plated.Id));
            Assert.That(oldPlate.Portions.Single().Cooking, Is.EqualTo(CookState.Burned));
            yield return null;
            Assert.That(queue.Pending.Any(c => new[] { discarded.Id, returned.Id, plated.Id, replaced.Id }.Contains(c.SubjectKey)), Is.False);
            Assert.That(queue.Pending.Any(c => c.SubjectKey == unrelated.Id), Is.True); Assert.That(bootstrap.Judging.Current, Is.SameAs(score));
        }
    }
}
