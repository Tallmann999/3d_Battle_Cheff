using System;
using System.Collections.Generic;
using System.Linq;
using ChefShow.Chefs;
using ChefShow.Cooking;
using ChefShow.Core;
using ChefShow.Data;
using ChefShow.Inventory;
using ChefShow.Judging;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ChefShow.Tests
{
    public sealed class ReactionTests
    {
        private ReactionConfig config;
        private readonly List<AudioClip> clips = new List<AudioClip>();
        [SetUp] public void Setup()
        {
            config = ScriptableObject.CreateInstance<ReactionConfig>();
            config.GlobalCooldown = config.PersonalCooldown = config.RepeatCooldown = 0;
            config.MinimumDuration = config.MaximumDuration = 1; config.ReplyGap = .1f;
        }
        [TearDown] public void Cleanup()
        { Object.DestroyImmediate(config); foreach (var clip in clips) Object.DestroyImmediate(clip); clips.Clear(); }
        private static ReactionDefinition Definition(ReactionCue cue, string id, ReactionPriority priority = ReactionPriority.Normal,
            ChefSpeaker primary = ChefSpeaker.Savory, params ChefSpeaker[] speakers)
        {
            if (speakers.Length == 0) speakers = new[] { primary };
            return new ReactionDefinition { Id = id, Cue = cue, Priority = priority, PrimaryChef = primary,
                Lines = speakers.Select((speaker, i) => new ReactionLineDefinition { Id = id + "-" + i,
                    Chef = speaker, Text = "The dish deserves a clear comment." }).ToArray() };
        }
        private ReactionQueue Queue(params ReactionDefinition[] definitions)
        { config.Definitions = definitions; Assert.That(config.Validate(), Is.Null); return new ReactionQueue(config.Capture(), 1005); }

        [Test] public void TextAndOptionalAudioDurationsAreCapturedWithinTheirOwnRules()
        {
            config.MinimumDuration = 2; config.MaximumDuration = 6; config.WordsPerSecond = 2;
            var shortLine = Definition(ReactionCue.FoodReady, "short"); shortLine.Lines[0].Text = "Good.";
            var longLine = Definition(ReactionCue.FoodBurned, "long"); longLine.Lines[0].Text = string.Join(" ", Enumerable.Repeat("word", 40));
            var audio = Definition(ReactionCue.Time10, "audio", ReactionPriority.System);
            var clip = AudioClip.Create("reaction-test", 22050 * 3, 1, 22050, false); clips.Add(clip); audio.Lines[0].Clip = clip;
            var queue = Queue(shortLine, longLine, audio); var captured = config.Capture();
            Assert.That(captured.Definitions[0].Lines[0].Duration, Is.EqualTo(2)); Assert.That(captured.Definitions[1].Lines[0].Duration, Is.EqualTo(6));
            Assert.That(captured.Definitions[2].Lines[0].Duration, Is.EqualTo(clip.length));
            Assert.That(queue.Enqueue(ReactionCue.FoodReady, "food", ChefSpeaker.Savory), Is.True); Assert.That(queue.Active, Is.Null);
            queue.Tick(0); Assert.That(queue.Active.Clip, Is.Null); Assert.That(queue.Active.Duration, Is.EqualTo(2));
            queue.Tick(2.01f); Assert.That(queue.Active, Is.Null); Assert.That(queue.IsSequenceActive, Is.False);
        }

        [Test] public void SameCueAndSubjectCoalesceWhilePendingAndThroughoutTheActiveSequence()
        {
            var queue = Queue(Definition(ReactionCue.FoodBurned, "burn", ReactionPriority.High, ChefSpeaker.Savory, ChefSpeaker.Savory, ChefSpeaker.Pastry));
            queue.Enqueue(ReactionCue.FoodBurned, "same-food", ChefSpeaker.Savory); queue.Enqueue(ReactionCue.FoodBurned, "same-food", ChefSpeaker.Savory);
            Assert.That(queue.Pending.Count, Is.EqualTo(1)); int starts = 0, finishes = 0;
            queue.Started += playback => starts++; queue.Finished += playback => finishes++;
            queue.Tick(0); var sequence = queue.Active.SequenceId;
            queue.Enqueue(ReactionCue.FoodBurned, "same-food", ChefSpeaker.Savory); Assert.That(queue.Pending, Is.Empty);
            queue.Tick(1.01f); Assert.That(queue.IsSequenceActive, Is.True);
            queue.Enqueue(ReactionCue.FoodBurned, "same-food", ChefSpeaker.Savory); Assert.That(queue.Pending, Is.Empty);
            queue.Tick(.11f); Assert.That(queue.Active.SequenceId, Is.EqualTo(sequence)); queue.Tick(1.01f);
            Assert.That(starts, Is.EqualTo(2)); Assert.That(finishes, Is.EqualTo(2)); Assert.That(queue.History.Select(p => p.SequenceId).Distinct().Count(), Is.EqualTo(1));
        }

        [Test] public void CapacityProtectsHighPriorityAndEqualPriorityKeepsArrivalOrder()
        {
            config.QueueCapacity = 2;
            var queue = Queue(Definition(ReactionCue.FoodReady, "ambient", ReactionPriority.Ambient), Definition(ReactionCue.FoodBurned, "high", ReactionPriority.High));
            queue.Enqueue(ReactionCue.FoodReady, "a", ChefSpeaker.Savory); queue.Enqueue(ReactionCue.FoodReady, "b", ChefSpeaker.Savory);
            queue.Enqueue(ReactionCue.FoodBurned, "critical-food", ChefSpeaker.Savory);
            Assert.That(queue.Pending.Count, Is.EqualTo(2)); Assert.That(queue.Pending.Count(p => p.Cue == ReactionCue.FoodBurned), Is.EqualTo(1));
            queue.Tick(0); Assert.That(queue.Active.Cue, Is.EqualTo(ReactionCue.FoodBurned));
            config.QueueCapacity = 6; queue = Queue(Definition(ReactionCue.FoodReady, "normal"));
            foreach (var key in new[] { "first", "second", "third" }) queue.Enqueue(ReactionCue.FoodReady, key, ChefSpeaker.Savory);
            queue.Tick(0); Assert.That(queue.Active.SubjectKey, Is.EqualTo("first")); queue.Tick(1.01f); Assert.That(queue.Active.SubjectKey, Is.EqualTo("second"));
            queue.Tick(1.01f); Assert.That(queue.Active.SubjectKey, Is.EqualTo("third"));
            config.QueueCapacity = 1; queue = Queue(Definition(ReactionCue.Time30, "thirty", ReactionPriority.System), Definition(ReactionCue.TimeUp, "time-up", ReactionPriority.System));
            queue.Enqueue(ReactionCue.Time30, "timer30", ChefSpeaker.Savory); queue.Enqueue(ReactionCue.TimeUp, "timer0", ChefSpeaker.Savory);
            Assert.That(queue.Pending.Single().Cue, Is.EqualTo(ReactionCue.TimeUp)); queue.Tick(0); Assert.That(queue.Active.Cue, Is.EqualTo(ReactionCue.TimeUp));
        }

        [Test] public void OnlySystemPreemptsActiveSpeechAndCancellationIsPublishedOnce()
        {
            config.GlobalCooldown = config.PersonalCooldown = 100;
            var queue = Queue(Definition(ReactionCue.FoodReady, "normal"), Definition(ReactionCue.FoodBurned, "burn", ReactionPriority.High),
                Definition(ReactionCue.Time10, "timer", ReactionPriority.System));
            var cancelled = new List<ReactionPlayback>(); var started = new List<ReactionPlayback>();
            queue.Cancelled += cancelled.Add; queue.Started += playback => { Assert.That(queue.Active, Is.SameAs(playback)); started.Add(playback); };
            queue.Enqueue(ReactionCue.FoodReady, "ready", ChefSpeaker.Savory); queue.Tick(0); var original = queue.Active;
            queue.Enqueue(ReactionCue.FoodBurned, "burned", ChefSpeaker.Savory); queue.Tick(0); Assert.That(queue.Active, Is.SameAs(original));
            queue.Enqueue(ReactionCue.Time10, "timer10", ChefSpeaker.Savory); Assert.That(queue.Active, Is.SameAs(original));
            queue.Tick(0); Assert.That(queue.Active.Cue, Is.EqualTo(ReactionCue.Time10)); Assert.That(cancelled, Is.EqualTo(new[] { original }));
            queue.Tick(0); Assert.That(cancelled.Count, Is.EqualTo(1)); Assert.That(started.Count, Is.EqualTo(2));
        }

        [Test] public void ExpiredCandidatesNeverStartAndZeroDeltaDoesNotAgeSpeechOrTtl()
        {
            config.NormalTtl = .25f; var queue = Queue(Definition(ReactionCue.FoodReady, "ready"));
            queue.Enqueue(ReactionCue.FoodReady, "active", ChefSpeaker.Savory); queue.Tick(0);
            queue.Enqueue(ReactionCue.FoodReady, "stale", ChefSpeaker.Savory); var pending = queue.Pending.Single(); var active = queue.Active;
            Assert.That(pending.ExpiresAt - pending.EnqueuedAt, Is.EqualTo(.25f));
            for (int i = 0; i < 20; i++) queue.Tick(0);
            Assert.That(queue.Now, Is.Zero); Assert.That(queue.Active, Is.SameAs(active)); Assert.That(queue.Pending.Single(), Is.SameAs(pending));
            queue.Tick(.26f); Assert.That(queue.Pending, Is.Empty); queue.Tick(1);
            Assert.That(queue.History.Any(p => p.SubjectKey == "stale"), Is.False);
        }

        [TestCase("global")] [TestCase("personal")] [TestCase("repeat")]
        public void IndependentCooldownsAndSystemLineRepeatDelayUntilTheirCapturedBoundary(string kind)
        {
            config.GlobalCooldown = kind == "global" ? 2 : 0; config.PersonalCooldown = kind == "personal" ? 2 : 0; config.RepeatCooldown = kind == "repeat" ? 2 : 0;
            var secondChef = kind == "global" ? ChefSpeaker.Pastry : ChefSpeaker.Savory;
            var firstCue = kind == "repeat" ? ReactionCue.Time10 : ReactionCue.FoodReady;
            var queue = Queue(Definition(firstCue, "first", kind == "repeat" ? ReactionPriority.System : ReactionPriority.Normal), Definition(ReactionCue.FoodBurned, "second", ReactionPriority.High, secondChef));
            queue.Enqueue(firstCue, "first-food", ChefSpeaker.Savory); queue.Tick(0);
            queue.Enqueue(kind == "repeat" ? firstCue : ReactionCue.FoodBurned, "second-food", secondChef);
            queue.Tick(1.01f); Assert.That(queue.Active, Is.Null); queue.Tick(.98f); Assert.That(queue.Active, Is.Null);
            queue.Tick(.02f); Assert.That(queue.Active, Is.Not.Null); Assert.That(queue.Active.SubjectKey, Is.EqualTo("second-food"));
        }

        [Test] public void ReplyGapRetainsTheSingleSlotAndSecondChefSharesTheSequence()
        {
            var queue = Queue(Definition(ReactionCue.FoodBurned, "dialogue", ReactionPriority.High, ChefSpeaker.Savory, ChefSpeaker.Savory, ChefSpeaker.Pastry),
                Definition(ReactionCue.FoodReady, "waiting"));
            var started = new List<ReactionPlayback>(); queue.Started += started.Add;
            queue.Enqueue(ReactionCue.FoodBurned, "burn", ChefSpeaker.Savory); queue.Tick(0); var first = queue.Active;
            queue.Enqueue(ReactionCue.FoodReady, "next", ChefSpeaker.Savory); queue.Tick(1.01f);
            Assert.That(queue.Active, Is.Null); Assert.That(queue.IsSequenceActive, Is.True); Assert.That(queue.Pending.Count, Is.EqualTo(1));
            queue.Tick(.05f); Assert.That(queue.Active, Is.Null); queue.Tick(.061f);
            Assert.That(queue.Active.Chef, Is.EqualTo(ChefSpeaker.Pastry)); Assert.That(queue.Active.SequenceId, Is.EqualTo(first.SequenceId));
            queue.Tick(1.01f); Assert.That(queue.Active.SubjectKey, Is.EqualTo("next"));
            Assert.That(started.Select(p => p.SubjectKey), Is.EqualTo(new[] { "burn", "burn", "next" }));
        }

        [Test] public void PhaseChangeCancelsCookingSpeechAndQueueButPreservesSystemTimers()
        {
            var queue = Queue(Definition(ReactionCue.FoodReady, "ready"), Definition(ReactionCue.FoodBurned, "burn", ReactionPriority.High),
                Definition(ReactionCue.Time10, "timer", ReactionPriority.System), Definition(ReactionCue.JudgingGood, "good", ReactionPriority.System));
            int cancelled = 0; queue.Cancelled += playback => cancelled++;
            queue.Enqueue(ReactionCue.FoodReady, "ready", ChefSpeaker.Savory); queue.Tick(0);
            queue.Enqueue(ReactionCue.FoodBurned, "burn", ChefSpeaker.Savory); queue.Enqueue(ReactionCue.Time10, "timer10", ChefSpeaker.Savory);
            queue.ChangePhase(ReactionPhase.Judging); Assert.That(cancelled, Is.EqualTo(1)); Assert.That(queue.Active, Is.Null);
            Assert.That(queue.Pending.Select(p => p.Cue), Is.EqualTo(new[] { ReactionCue.Time10 }));
            queue.Enqueue(ReactionCue.JudgingGood, "judged", ChefSpeaker.Savory); queue.Tick(0); Assert.That(queue.Active.Cue, Is.EqualTo(ReactionCue.Time10));
            queue.ChangePhase(ReactionPhase.Cooking); Assert.That(queue.Pending, Is.Empty); Assert.That(queue.Active.Cue, Is.EqualTo(ReactionCue.Time10));
            Assert.That(queue.History.Any(p => p.Cue == ReactionCue.JudgingGood), Is.False);
        }

        [Test] public void CancellingOnePendingSubjectDoesNotCancelOtherFoodOrCreateSpeech()
        {
            var queue = Queue(Definition(ReactionCue.FoodReady, "ready"));
            queue.Enqueue(ReactionCue.FoodReady, "now-burned", ChefSpeaker.Savory); queue.Enqueue(ReactionCue.FoodReady, "still-ready", ChefSpeaker.Savory);
            queue.CancelPending(ReactionCue.FoodReady, "now-burned"); Assert.That(queue.Pending.Single().SubjectKey, Is.EqualTo("still-ready"));
            queue.Tick(0); Assert.That(queue.Active.SubjectKey, Is.EqualTo("still-ready"));
        }

        [Test] public void CapturedProfilesAndReadOnlyHistoryAreDeterministicAfterSourceMutation()
        {
            config.HistoryCapacity = 3; config.Definitions = Enumerable.Range(0, 4).Select(i => Definition(ReactionCue.FoodReady, "variant" + i)).ToArray();
            var settings = config.Capture(); string text = settings.Definitions[0].Lines[0].Text;
            config.Definitions[0].Lines[0].Text = "Changed after Restart"; config.Definitions[0].Id = "changed"; config.GlobalCooldown = 100;
            Assert.That(settings.Definitions[0].Id, Is.EqualTo("variant0")); Assert.That(settings.Definitions[0].Lines[0].Text, Is.EqualTo(text)); Assert.That(settings.GlobalCooldown, Is.Zero);
            Assert.Throws<NotSupportedException>(() => ((IList<ReactionDefinitionSettings>)settings.Definitions)[0] = null);
            var a = new ReactionQueue(settings, 77); var b = new ReactionQueue(settings, 77); var idsA = new List<string>(); var idsB = new List<string>();
            a.Started += p => idsA.Add(p.LineId); b.Started += p => idsB.Add(p.LineId);
            for (int i = 0; i < 8; i++)
            {
                a.Enqueue(ReactionCue.FoodReady, "food" + i, ChefSpeaker.Savory); b.Enqueue(ReactionCue.FoodReady, "food" + i, ChefSpeaker.Savory);
                a.Tick(0); b.Tick(0); a.Tick(1.01f); b.Tick(1.01f);
            }
            Assert.That(idsA, Is.EqualTo(idsB)); Assert.That(idsA.Count, Is.EqualTo(8)); Assert.That(a.History.Count, Is.EqualTo(3));
            Assert.Throws<NotSupportedException>(() => ((IList<ReactionPlayback>)a.History).Add(a.History[0]));
            Assert.Throws<NotSupportedException>(() => ((IList<ReactionCandidate>)a.Pending).Add(null));
        }

        [Test] public void MissingOptionalContentDoesNotBlockAndInvalidSettingsOrDeltaFailClearly()
        {
            var queue = Queue(); Assert.That(queue.Enqueue(ReactionCue.FoodReady, "food", ChefSpeaker.Savory), Is.False); queue.Tick(100);
            Assert.That(queue.Active, Is.Null); Assert.That(queue.Pending, Is.Empty); Assert.That(queue.History, Is.Empty);
            Assert.Throws<ArgumentOutOfRangeException>(() => queue.Tick(-1)); Assert.Throws<ArgumentOutOfRangeException>(() => queue.Tick(float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => queue.Tick(float.PositiveInfinity));
            config.GlobalCooldown = -1; Assert.Throws<InvalidOperationException>(() => config.Capture()); config.GlobalCooldown = 0;
            config.MinimumDuration = float.NaN; Assert.Throws<InvalidOperationException>(() => config.Capture()); config.MinimumDuration = 1;
            config.QueueCapacity = 0; Assert.Throws<InvalidOperationException>(() => config.Capture()); config.QueueCapacity = 6;
            config.Definitions = new[] { Definition(ReactionCue.FoodReady, "duplicate"), Definition(ReactionCue.FoodBurned, "duplicate") };
            Assert.Throws<InvalidOperationException>(() => config.Capture());
        }

        private static PrototypeRun RealRun()
        {
            var heat = AssetDatabase.LoadAssetAtPath<CookingConfig>("Assets/_ChefShow/Generated/Data/CookingConfig.asset");
            var dishes = AssetDatabase.LoadAssetAtPath<DishwareConfig>("Assets/_ChefShow/Generated/Data/DishwareConfig.asset");
            var recipes = AssetDatabase.LoadAssetAtPath<RecipeRecognitionCatalog>("Assets/_ChefShow/Data/Recipes/Recognition/RecipeRecognitionCatalog.asset");
            Assert.That(heat, Is.Not.Null); Assert.That(dishes, Is.Not.Null); Assert.That(recipes, Is.Not.Null);
            return new PrototypeRun(600, 1005, (type, error) => Assert.Fail(error.ToString()), cooking: heat.Capture(), dishware: dishes.Capture(), recognition: recipes.Capture());
        }
        private static FoodPortion Collect(InventoryState state, string id)
        {
            var ingredient = AssetDatabase.LoadAssetAtPath<IngredientDefinition>("Assets/_ChefShow/Generated/Data/Ingredients/" + id + ".asset");
            Assert.That(ingredient, Is.Not.Null); Assert.That(state.TryCollect(ingredient, out var reason), Is.True, reason); return state.Held;
        }
        private static ScoreBreakdown SubmitScore(PrototypeRun run)
        {
            Assert.That(run.Inventory.TrySubmitDish(out _), Is.True);
            var judging = AssetDatabase.LoadAssetAtPath<JudgingConfig>("Assets/_ChefShow/Data/Judging/JudgingConfig.asset"); Assert.That(judging, Is.Not.Null);
            return DishScorer.Evaluate(run.Inventory.SubmittedDish, judging.Capture(run.Inventory.RecognitionRules, run.Inventory.CookingRules));
        }
        [TestCase("raw", ReactionCue.JudgingRaw, ScoreSafetyIssues.RawMandatoryProtein)]
        [TestCase("burned", ReactionCue.JudgingBurned, ScoreSafetyIssues.BurnedMainComponent)]
        [TestCase("good", ReactionCue.JudgingGood, ScoreSafetyIssues.None)]
        [TestCase("empty", ReactionCue.JudgingWeak, ScoreSafetyIssues.EmptyDish)]
        [TestCase("both", ReactionCue.JudgingRaw, ScoreSafetyIssues.RawMandatoryProtein | ScoreSafetyIssues.BurnedMainComponent)]
        public void JudgingCueReadsSafetyFromRealSubmittedFoodAndKeepsResponsibleChefSeparate(string scenario, ReactionCue expected, ScoreSafetyIssues issue)
        {
            using (var run = RealRun())
            {
                var state = run.Inventory;
                if (scenario != "empty")
                {
                    Collect(state, "beef");
                    if (scenario != "raw")
                    {
                        state.TryPlaceCooker(CookerKind.Pan, out _); while (state.Heat(CookerKind.Pan) != HeatLevel.Medium) state.TryCycleHeat(CookerKind.Pan, out _);
                        run.Tick(scenario == "burned" || scenario == "both" ? state.CookingRules.Burned : state.CookingRules.Ready); state.TryTakeCooker(CookerKind.Pan, 0, out _);
                    }
                    Assert.That(state.TryPlaceServing(out _), Is.True);
                    if (scenario == "both") { Collect(state, "beef"); Assert.That(state.TryPlaceServing(out _), Is.True); }
                }
                var score = SubmitScore(run); int version = state.Version; float remaining = run.RemainingSeconds;
                Assert.That(score.SafetyIssues, Is.EqualTo(issue)); Assert.That(ReactionController.JudgingCue(score, 80), Is.EqualTo(expected));
                Assert.That(ReactionController.ResponsibleChef(TeamId.A), Is.EqualTo(ChefSpeaker.Savory)); Assert.That(ReactionController.ResponsibleChef(TeamId.B), Is.EqualTo(ChefSpeaker.Pastry));
                Assert.That(state.Version, Is.EqualTo(version)); Assert.That(run.RemainingSeconds, Is.EqualTo(remaining));
            }
        }

        [Test] public void CorrectColdButterAfterCookedCarrotsDoesNotProduceRawMandatoryProteinReaction()
        {
            using (var run = RealRun())
            {
                var state = run.Inventory; Collect(state, "carrot"); Assert.That(state.TryPlaceSocket(0, out _), Is.True);
                for (int i = 0; i < 6; i++) Assert.That(state.TryChop(ChefShow.Ingredients.KitchenToolKind.Knife, out _), Is.True);
                state.TryTakeSocket(0, out _); state.TryPlaceCooker(CookerKind.Pot, out _);
                while (state.Heat(CookerKind.Pot) != HeatLevel.Medium) Assert.That(state.TryCycleHeat(CookerKind.Pot, out _), Is.True);
                for (int i = 0; i < state.CookingRules.PotStirs; i++) Assert.That(state.TryStir(CookerKind.Pot, ChefShow.Ingredients.KitchenToolKind.Spatula, out _), Is.True);
                run.Tick(state.CookingRules.Ready); state.TryTakeCooker(CookerKind.Pot, 0, out _); state.TryPlaceServing(out _);
                var butter = Collect(state, "butter"); state.TryPlaceServing(out _); var score = SubmitScore(run);
                Assert.That(run.Inventory.SubmittedDish.Recognition.RecipeId, Is.EqualTo("buttered_carrots")); Assert.That(butter.Cooking, Is.EqualTo(CookState.Raw));
                Assert.That(score.SafetyIssues & ScoreSafetyIssues.RawMandatoryProtein, Is.EqualTo(ScoreSafetyIssues.None));
                Assert.That(ReactionController.JudgingCue(score, 80), Is.Not.EqualTo(ReactionCue.JudgingRaw));
            }
        }
    }
}
