using System;
using System.Collections.Generic;
using System.Linq;
using ChefShow.Core;
using ChefShow.Data;
using ChefShow.Inventory;
using ChefShow.Judging;
using UnityEngine;
using UnityEngine.UI;

namespace ChefShow.Chefs
{
    [Serializable]
    public sealed class ChefReactionBinding
    {
        public ChefSpeaker Speaker;
        public Transform Actor;
        public Renderer SpeakingMarker;
        public AudioSource Voice;
    }

    public readonly struct ReactionStarted
    {
        public readonly string RunId;
        public readonly float SimulationTime, PresentationTime;
        public readonly ReactionPlayback Playback;
        public ReactionStarted(string run, float simulation, float presentation, ReactionPlayback playback)
        { RunId=run;SimulationTime=simulation;PresentationTime=presentation;Playback=playback; }
    }
    public readonly struct ReactionFinished
    {
        public readonly string RunId;
        public readonly float SimulationTime, PresentationTime;
        public readonly ReactionPlayback Playback;
        public ReactionFinished(string run, float simulation, float presentation, ReactionPlayback playback)
        { RunId=run;SimulationTime=simulation;PresentationTime=presentation;Playback=playback; }
    }
    public readonly struct ReactionCancelled
    {
        public readonly string RunId;
        public readonly float SimulationTime, PresentationTime;
        public readonly ReactionPlayback Playback;
        public ReactionCancelled(string run, float simulation, float presentation, ReactionPlayback playback)
        { RunId=run;SimulationTime=simulation;PresentationTime=presentation;Playback=playback; }
    }

    public sealed class ReactionController : MonoBehaviour
    {
        public ReactionConfig Config;
        public ChefReactionBinding[] Bindings;
        public GameObject Panel;
        public Text SpeakerText, SubtitleText;
        public ReactionQueue Queue { get; private set; }
        public ReactionSettings Settings { get; private set; }
        public ReactionPlayback Active => Queue?.Active;
        public ReactionPhase Phase => Queue == null ? ReactionPhase.Cooking : Queue.Phase;
        private GameBootstrap owner;
        private readonly List<IDisposable> subscriptions=new List<IDisposable>();
        private readonly HashSet<string> readySeen=new HashSet<string>(), burnedSeen=new HashSet<string>();
        private Dictionary<string,string> foodNames;
        private Quaternion[] homeRotations;
        private Vector3[] markerPositions;
        private ReactionPlayback presented;
        private DishSnapshot observedDish;
        private string dishKey;
        private int submissionSerial;
        private float previousRemaining;
        private bool timeUpSeen, audioPaused;
        private readonly HashSet<int> announcedThresholds=new HashSet<int>();

        public string Validate()
        {
            if(Config==null)return "Не назначен конфиг реакций шефов.";
            var error=Config.Validate();if(error!=null)return error;
            if(Bindings==null||Bindings.Length!=2||Bindings.Any(b=>b==null||b.Actor==null||!Enum.IsDefined(typeof(ChefSpeaker),b.Speaker))
                ||Bindings.Select(b=>b.Speaker).Distinct().Count()!=2)
                return "Не сохранены два разных шефа для реакций.";
            if(Panel==null||SpeakerText==null||SubtitleText==null)return "Не сохранены субтитры шефов.";
            return null;
        }
        public void Initialize(GameBootstrap bootstrap)
        {
            owner=bootstrap;
            homeRotations=Bindings.Select(b=>b.Actor.rotation).ToArray();
            markerPositions=Bindings.Select(b=>b.SpeakingMarker==null?Vector3.zero:b.SpeakingMarker.transform.localPosition).ToArray();
        }
        public void ResetRun()
        {
            Unsubscribe();StopPresentation();
            Settings=Config.Capture();Queue=new ReactionQueue(Settings,owner.Run.Seed);
            readySeen.Clear();burnedSeen.Clear();announcedThresholds.Clear();
            observedDish=null;dishKey=null;submissionSerial=0;timeUpSeen=false;
            previousRemaining=owner.Run.RemainingSeconds;
            foodNames=owner.Inventory.Catalog.Ingredients.ToDictionary(i=>i.Id,i=>InventoryController.FoodName(i));
            var run=owner.Run;var queue=Queue;
            queue.Started+=p=>{if(owner.Run==run&&Queue==queue)run.Events.Publish(new ReactionStarted(run.RunId,run.Clock.SimulationTime,queue.Now,p));};
            queue.Finished+=p=>{if(owner.Run==run&&Queue==queue)run.Events.Publish(new ReactionFinished(run.RunId,run.Clock.SimulationTime,queue.Now,p));};
            queue.Cancelled+=p=>{if(owner.Run==run&&Queue==queue)run.Events.Publish(new ReactionCancelled(run.RunId,run.Clock.SimulationTime,queue.Now,p));};
            subscriptions.Add(run.Events.Subscribe<ChefShow.Cooking.CookingChanged>(OnCooking));
            subscriptions.Add(run.Events.Subscribe<InventoryChanged>(OnInventory));
            subscriptions.Add(run.Events.Subscribe<DishChanged>(OnDish));
            subscriptions.Add(run.Events.Subscribe<DishJudged>(OnJudged));
        }
        public static ChefSpeaker ResponsibleChef(TeamId team)=>team==TeamId.B?ChefSpeaker.Pastry:ChefSpeaker.Savory;
        public static ReactionCue JudgingCue(ScoreBreakdown score,float goodThreshold)
        {
            if((score.SafetyIssues&ScoreSafetyIssues.RawMandatoryProtein)!=0)return ReactionCue.JudgingRaw;
            if((score.SafetyIssues&ScoreSafetyIssues.BurnedMainComponent)!=0)return ReactionCue.JudgingBurned;
            if((score.SafetyIssues&ScoreSafetyIssues.EmptyDish)!=0)return ReactionCue.JudgingWeak;
            if(score.IsExperimental)return ReactionCue.JudgingExperimental;
            return score.UnroundedTotal>=goodThreshold?ReactionCue.JudgingGood:ReactionCue.JudgingWeak;
        }
        private bool CurrentRun(string id)=>owner!=null&&owner.Run!=null&&!owner.Run.Disposed&&id==owner.Run.RunId;
        private void OnCooking(ChefShow.Cooking.CookingChanged fact)
        {
            if(!CurrentRun(fact.RunId)||fact.Team!=owner.Run.PlayerTeam||fact.ActorId!=owner.Run.PlayerTeam+"1"
                ||fact.Food==null||owner.Run.Inventory.SubmittedDish!=null)return;
            string food=foodNames.TryGetValue(fact.Food.IngredientId,out var name)?name:fact.Food.IngredientId;
            var chef=ResponsibleChef(fact.Team);
            if(fact.Action=="food_burned"&&fact.Food.Cooking==CookState.Burned&&burnedSeen.Add(fact.Food.Id))
            {
                Queue.CancelPending(ReactionCue.FoodReady,fact.Food.Id);
                Queue.Enqueue(ReactionCue.FoodBurned,fact.Food.Id,chef,food);
            }
            else if(fact.Action=="food_state_changed"&&fact.Food.Cooking==CookState.Cooked
                &&fact.Previous!=CookState.Cooked&&readySeen.Add(fact.Food.Id))
                Queue.Enqueue(ReactionCue.FoodReady,fact.Food.Id,chef,food);
        }
        private void EnsureDishKey(DishSnapshot dish)
        {
            if(ReferenceEquals(observedDish,dish))return;
            observedDish=dish;dishKey="submission-"+(++submissionSerial);
        }
        private void CancelFoodPending(string portionId)
        {
            if(string.IsNullOrEmpty(portionId))return;
            Queue.CancelPending(ReactionCue.FoodReady,portionId);
            Queue.CancelPending(ReactionCue.FoodBurned,portionId);
        }
        private void OnInventory(InventoryChanged fact)
        {
            if(!CurrentRun(fact.RunId)||fact.ActorId!=owner.Run.PlayerTeam+"1")return;
            if(fact.Action=="ingredient_discarded"||fact.Action=="ingredient_returned")CancelFoodPending(fact.PortionId);
        }
        private void CancelArchivedFoodPending()
        {
            // Only queued food facts need an ownership check; their quality stays captured.
            var keys=new HashSet<string>(Queue.Pending.Where(p=>p.Cue==ReactionCue.FoodReady||p.Cue==ReactionCue.FoodBurned).Select(p=>p.SubjectKey));
            foreach(var food in owner.Run.Inventory.Portions)
                if(keys.Contains(food.Id)&&(food.Location==PortionLocation.Trash||food.Location==PortionLocation.Returned))CancelFoodPending(food.Id);
        }
        private void OnDish(DishChanged fact)
        {
            if(!CurrentRun(fact.RunId)||fact.ActorId!=owner.Run.PlayerTeam+"1")return;
            if(fact.Action=="dishware_discarded"||fact.Action=="dishware_selected")CancelArchivedFoodPending();
            if(fact.Action=="dish_submitted"&&ReferenceEquals(fact.Dish,owner.Run.Inventory.SubmittedDish))
            {
                EnsureDishKey(fact.Dish);Queue.ChangePhase(ReactionPhase.Judging);
                Queue.Enqueue(ReactionCue.DishSubmitted,dishKey,ResponsibleChef(owner.Run.PlayerTeam));
            }
            else if(fact.Action=="tasting_dish_retrieved"||fact.Action=="submission_reset")
            { observedDish=null;dishKey=null;Queue.ChangePhase(ReactionPhase.Cooking); }
        }
        private void OnJudged(DishJudged fact)
        {
            if(!CurrentRun(fact.RunId)||!ReferenceEquals(fact.Dish,owner.Run.Inventory.SubmittedDish)
                ||owner.Judging==null||!ReferenceEquals(fact.Score,owner.Judging.Current))return;
            EnsureDishKey(fact.Dish);Queue.ChangePhase(ReactionPhase.Judging);
            Queue.CancelPending(ReactionCue.DishSubmitted,dishKey);
            Queue.Enqueue(JudgingCue(fact.Score,Settings.GoodScoreThreshold),dishKey,
                ResponsibleChef(owner.Run.PlayerTeam),fact.Score.RecipeName,"Итог: "+fact.Score.Total+"/100.");
        }
        public void Step(float realDelta)
        {
            if(owner==null||owner.Run==null||owner.Run.Disposed||Queue==null)return;
            float delta=owner.IsPaused?0:Mathf.Max(0,realDelta)*owner.Run.Clock.Speed;
            if(!owner.IsPaused)
            {
                UpdateTimer();
                // Cooking time stops at zero. Speech still has to finish during judging.
                Queue.Tick(delta);
            }
            Present(delta);
        }
        private void UpdateTimer()
        {
            float remaining=owner.Run.RemainingSeconds;
            if(remaining<=0&&!timeUpSeen)
            {
                timeUpSeen=true;
                Queue.ChangePhase(ReactionPhase.Judging);
                foreach(int n in new[]{60,30,10})Queue.CancelPending(TimerCue(n),"timer"+n);
                Queue.Enqueue(ReactionCue.TimeUp,"timer0",ResponsibleChef(owner.Run.PlayerTeam));
            }
            else if(!timeUpSeen)
            {
                int urgent=0;
                foreach(int n in new[]{60,30,10})
                    if(previousRemaining>n&&remaining<=n&&announcedThresholds.Add(n))urgent=n;
                if(urgent!=0)
                {
                    foreach(int n in new[]{60,30,10})if(n>urgent)Queue.CancelPending(TimerCue(n),"timer"+n);
                    Queue.Enqueue(TimerCue(urgent),"timer"+urgent,ResponsibleChef(owner.Run.PlayerTeam));
                }
            }
            previousRemaining=remaining;
        }
        private static ReactionCue TimerCue(int threshold)=>threshold==60?ReactionCue.Time60:threshold==30?ReactionCue.Time30:ReactionCue.Time10;
        private void Present(float delta)
        {
            var current=Queue.Active;
            if(!ReferenceEquals(current,presented))
            {
                foreach(var b in Bindings)if(b.Voice!=null)b.Voice.Stop();
                presented=current;audioPaused=false;
                SpeakerText.text=current==null?"":current.Chef==ChefSpeaker.Savory?Settings.SavoryName:Settings.PastryName;
                SpeakerText.color=current!=null&&current.Chef==ChefSpeaker.Pastry?new Color(.76f,.46f,1):new Color(1,.60f,.18f);
                SubtitleText.text=current?.Text??"";
                if(current!=null&&current.Clip!=null)
                {
                    var voice=Bindings.Single(b=>b.Speaker==current.Chef).Voice;
                    if(voice!=null){voice.clip=current.Clip;voice.pitch=owner.Run.Clock.Speed;voice.Play();}
                }
            }
            bool paused=owner.IsPaused;
            if(audioPaused!=paused)
            {
                foreach(var b in Bindings)if(b.Voice!=null){if(paused)b.Voice.Pause();else b.Voice.UnPause();}
                audioPaused=paused;
            }
            Panel.SetActive(current!=null&&!paused&&(owner.RecipeBook==null||!owner.RecipeBook.IsOpen));
            for(int i=0;i<Bindings.Length;i++)
            {
                var b=Bindings[i];bool talking=current!=null&&current.Chef==b.Speaker;
                if(b.SpeakingMarker!=null)
                {
                    b.SpeakingMarker.enabled=talking;
                    b.SpeakingMarker.transform.localPosition=markerPositions[i]+(talking?Vector3.up*Mathf.Sin(Queue.Now*5)*.025f:Vector3.zero);
                }
                if(!talking)b.Actor.rotation=homeRotations[i];
                else if(!paused&&delta>0)
                {
                    var direction=owner.Player.transform.position-b.Actor.position;direction.y=0;
                    if(direction.sqrMagnitude>.001f)
                    {
                        var look=Quaternion.LookRotation(direction)*Quaternion.Euler(Mathf.Sin(Queue.Now*4)*3,0,0);
                        b.Actor.rotation=Quaternion.Slerp(b.Actor.rotation,look,Mathf.Clamp01(delta*6));
                    }
                    if(b.Voice!=null)b.Voice.pitch=owner.Run.Clock.Speed;
                }
            }
        }
        private void StopPresentation()
        {
            presented=null;audioPaused=false;
            if(Panel!=null)Panel.SetActive(false);
            if(SpeakerText!=null)SpeakerText.text="";
            if(SubtitleText!=null)SubtitleText.text="";
            if(Bindings==null)return;
            for(int i=0;i<Bindings.Length;i++)
            {
                var b=Bindings[i];if(b==null)continue;
                if(b.Voice!=null)b.Voice.Stop();
                if(b.Actor!=null&&homeRotations!=null&&i<homeRotations.Length)b.Actor.rotation=homeRotations[i];
                if(b.SpeakingMarker!=null)
                { b.SpeakingMarker.enabled=false;if(markerPositions!=null&&i<markerPositions.Length)b.SpeakingMarker.transform.localPosition=markerPositions[i]; }
            }
        }
        private void Unsubscribe(){foreach(var s in subscriptions)s.Dispose();subscriptions.Clear();}
        private void OnDestroy(){Unsubscribe();StopPresentation();}
    }
}
