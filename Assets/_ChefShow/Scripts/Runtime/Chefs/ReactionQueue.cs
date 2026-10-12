using System;
using System.Collections.Generic;
using System.Linq;
using ChefShow.Data;
using UnityEngine;

namespace ChefShow.Chefs
{
    public enum ChefSpeaker { Savory, Pastry }
    public enum ReactionCue { FoodReady, FoodBurned, DishSubmitted, JudgingRaw, JudgingBurned, JudgingGood, JudgingWeak, JudgingExperimental, Time60, Time30, Time10, TimeUp }
    public enum ReactionPriority { Ambient, Normal, High, Critical, System }
    public enum ReactionPhase { Cooking, Judging }

    public sealed class ReactionCandidate
    {
        public ReactionCue Cue {get;}
        public string SubjectKey {get;} public string SubjectName {get;} public string Detail {get;}
        public ChefSpeaker Chef {get;}
        public ReactionPhase Phase {get;}
        public ReactionPriority Priority {get;}
        public float EnqueuedAt {get;} public float ExpiresAt {get;}
        internal long Order {get;}
        internal ReactionDefinitionSettings[] Variants {get;}
        internal ReactionCandidate(ReactionCue cue,string key,ChefSpeaker chef,string subject,string detail,
            ReactionPhase phase,ReactionPriority priority,float now,float ttl,long order,ReactionDefinitionSettings[] variants)
        {Cue=cue;SubjectKey=key;Chef=chef;SubjectName=subject;Detail=detail;Phase=phase;Priority=priority;EnqueuedAt=now;ExpiresAt=now+ttl;Order=order;Variants=variants;}
    }
    public sealed class ReactionPlayback
    {
        public ReactionCue Cue {get;}
        public string SubjectKey {get;} public string SubjectName {get;} public string Detail {get;}
        public string SequenceId {get;} public string LineId {get;}
        public ChefSpeaker Chef {get;} public string Text {get;} public AudioClip Clip {get;}
        public float Duration {get;}
        public ReactionPhase Phase {get;}
        public ReactionPriority Priority {get;}
        internal ReactionPlayback(ReactionCandidate candidate,ReactionLineSettings line,string sequenceId,ReactionSettings settings)
        {
            Cue=candidate.Cue;SubjectKey=candidate.SubjectKey;SubjectName=candidate.SubjectName;Detail=candidate.Detail;
            SequenceId=sequenceId;LineId=line.Id;Chef=line.Chef;Clip=line.Clip;
            Text=line.Text.Replace("{food}",SubjectName).Replace("{detail}",Detail);
            Duration=line.AudioDuration>0?line.AudioDuration:settings.TextDuration(Text);
            Phase=candidate.Phase;Priority=candidate.Priority;
        }
    }

    // Own presentation time continues after the cooking clock reaches zero. The caller
    // passes no time while paused; this model never changes gameplay or its event facts.
    public sealed class ReactionQueue
    {
        private sealed class Sequence
        {
            public ReactionCandidate Candidate;
            public ReactionDefinitionSettings Definition;
            public string Id;
            public int Index;
            public float Boundary;
            public ReactionPlayback LastPlayback;
        }
        private readonly ReactionSettings settings;
        private readonly System.Random random;
        private readonly List<ReactionCandidate> pending=new List<ReactionCandidate>();
        private readonly List<ReactionPlayback> history=new List<ReactionPlayback>();
        private readonly Dictionary<ChefSpeaker,float> chefLast=new Dictionary<ChefSpeaker,float>();
        private readonly Dictionary<string,float> lineLast=new Dictionary<string,float>(StringComparer.Ordinal);
        private float globalLast=float.NegativeInfinity;
        private long nextCandidate,nextSequence;
        private Sequence sequence;
        private bool ticking;
        public float Now {get;private set;}
        public ReactionPhase Phase {get;private set;}=ReactionPhase.Cooking;
        public ReactionPlayback Active {get;private set;}
        public bool IsSequenceActive=>sequence!=null;
        public IReadOnlyList<ReactionCandidate> Pending=>pending.AsReadOnly();
        public IReadOnlyList<ReactionPlayback> History=>history.AsReadOnly();
        public event Action<ReactionPlayback> Started,Finished,Cancelled;
        public ReactionQueue(ReactionSettings settings,int seed)
        {this.settings=settings??throw new ArgumentNullException(nameof(settings));random=new System.Random(seed);}
        public static bool IsTimer(ReactionCue cue)=>cue==ReactionCue.Time60||cue==ReactionCue.Time30||cue==ReactionCue.Time10||cue==ReactionCue.TimeUp;
        private static ReactionPhase PhaseFor(ReactionCue cue)=>cue==ReactionCue.FoodReady||cue==ReactionCue.FoodBurned?ReactionPhase.Cooking:ReactionPhase.Judging;
        private static int Urgency(ReactionCue cue)=>cue==ReactionCue.TimeUp?4:cue==ReactionCue.Time10?3:cue==ReactionCue.Time30?2:cue==ReactionCue.Time60?1:0;
        public bool Enqueue(ReactionCue cue,string key,ChefSpeaker chef,string subjectName="",string detail="")
        {
            if(!Enum.IsDefined(typeof(ReactionCue),cue)||!Enum.IsDefined(typeof(ChefSpeaker),chef))throw new ArgumentException("Неверный тип реакции или шеф.");
            if(string.IsNullOrWhiteSpace(key))throw new ArgumentException("Реакции нужен ключ исходного факта.",nameof(key));
            ExpirePending();
            if(!IsTimer(cue)&&PhaseFor(cue)!=Phase)return false;
            if(pending.Any(candidate=>candidate.Cue==cue&&candidate.SubjectKey==key)
                ||(sequence!=null&&sequence.Candidate.Cue==cue&&sequence.Candidate.SubjectKey==key))return false;
            var matching=settings.Definitions.Where(definition=>definition.Cue==cue&&definition.PrimaryChef==chef).ToArray();
            if(matching.Length==0)return false;
            var priority=matching.Max(definition=>definition.Priority);
            var variants=matching.Where(definition=>definition.Priority==priority).ToArray();
            if(pending.Count>=settings.QueueCapacity)
            {
                var weakest=pending.OrderBy(candidate=>candidate.Priority)
                    .ThenBy(candidate=>candidate.Priority==ReactionPriority.System?Urgency(candidate.Cue):0).ThenBy(candidate=>candidate.Order).First();
                if(weakest.Priority>priority || (weakest.Priority==priority
                    &&(priority!=ReactionPriority.System || Urgency(weakest.Cue)>=Urgency(cue))))return false;
                pending.Remove(weakest);
            }
            pending.Add(new ReactionCandidate(cue,key,chef,subjectName??"",detail??"",IsTimer(cue)?Phase:PhaseFor(cue),priority,
                Now,settings.Ttl(priority),++nextCandidate,variants));
            return true;
        }
        public void CancelPending(ReactionCue cue,string key)
        {pending.RemoveAll(candidate=>candidate.Cue==cue&&candidate.SubjectKey==key);}
        public void ClearPending(ReactionPhase? phase=null)
        {pending.RemoveAll(candidate=>!phase.HasValue||candidate.Phase==phase.Value);}
        public void ChangePhase(ReactionPhase phase)
        {
            if(!Enum.IsDefined(typeof(ReactionPhase),phase))throw new ArgumentException("Неверная фаза реакции.",nameof(phase));
            if(Phase==phase)return;
            Phase=phase;
            pending.RemoveAll(candidate=>!IsTimer(candidate.Cue)&&candidate.Phase!=phase);
            if(sequence!=null&&!IsTimer(sequence.Candidate.Cue)&&sequence.Candidate.Phase!=phase)CancelSequence();
        }
        private void ExpirePending()
        {for(int i=pending.Count-1;i>=0;i--)if(Now>=pending[i].ExpiresAt)pending.RemoveAt(i);}
        private IEnumerable<ReactionCandidate> OrderedPending()=>pending.OrderByDescending(candidate=>candidate.Priority)
            .ThenByDescending(candidate=>candidate.Priority==ReactionPriority.System?Urgency(candidate.Cue):0).ThenBy(candidate=>candidate.Order);
        private float EligibleAt(ReactionDefinitionSettings definition,ReactionPriority priority)
        {
            float eligible=Now;
            if(priority!=ReactionPriority.System)
            {
                eligible=Math.Max(eligible,globalLast+settings.GlobalCooldown);
                foreach(var chef in definition.Lines.Select(line=>line.Chef).Distinct())
                    if(chefLast.TryGetValue(chef,out float chefPrevious))eligible=Math.Max(eligible,chefPrevious+settings.PersonalCooldown);
            }
            foreach(var line in definition.Lines)
                if(lineLast.TryGetValue(line.Id,out float linePrevious))eligible=Math.Max(eligible,linePrevious+settings.RepeatCooldown);
            return eligible;
        }
        private void Schedule()
        {
            if(pending.Count==0)return;
            if(sequence!=null)
            {
                var interrupt=OrderedPending().FirstOrDefault(candidate=>candidate.Priority==ReactionPriority.System
                    &&candidate.Variants.Any(definition=>EligibleAt(definition,candidate.Priority)<=Now)
                    &&(sequence.Candidate.Priority!=ReactionPriority.System||Urgency(candidate.Cue)>Urgency(sequence.Candidate.Cue)));
                if(interrupt!=null)CancelSequence();
            }
            if(sequence!=null)return;
            foreach(var candidate in OrderedPending().ToArray())
            {
                var available=candidate.Variants.Where(definition=>EligibleAt(definition,candidate.Priority)<=Now).ToArray();
                if(available.Length==0)continue;
                pending.Remove(candidate);
                sequence=new Sequence{Candidate=candidate,Definition=available[random.Next(available.Length)],Id=(++nextSequence).ToString(System.Globalization.CultureInfo.InvariantCulture)};
                globalLast=Now;
                BeginLine(sequence);
                return;
            }
        }
        private void BeginLine(Sequence current)
        {
            var playback=new ReactionPlayback(current.Candidate,current.Definition.Lines[current.Index],current.Id,settings);
            Active=playback;current.LastPlayback=playback;current.Boundary=Now+playback.Duration;
            chefLast[playback.Chef]=Now;lineLast[playback.LineId]=Now;
            history.Add(playback);if(history.Count>settings.HistoryCapacity)history.RemoveAt(0);
            Started?.Invoke(playback);
        }
        private void FinishLine(Sequence current)
        {
            var finished=Active;Active=null;current.Index++;
            if(current.Index>=current.Definition.Lines.Count)sequence=null;
            else current.Boundary=Now+settings.ReplyGap;
            Finished?.Invoke(finished);
        }
        private void CancelSequence()
        {
            var cancelled=Active??sequence?.LastPlayback;
            sequence=null;Active=null;
            if(cancelled!=null)Cancelled?.Invoke(cancelled);
        }
        private void ProcessBoundary()
        {
            var current=sequence;
            if(current==null||Now<current.Boundary)return;
            if(Active!=null)FinishLine(current);
            else BeginLine(current);
        }
        public void Tick(float delta)
        {
            if(delta<0||float.IsNaN(delta)||float.IsInfinity(delta)||float.IsInfinity(Now+delta))throw new ArgumentOutOfRangeException(nameof(delta));
            if(ticking)throw new InvalidOperationException("Повторный Tick из callback реакции недопустим.");
            float target=Now+delta;
            ticking=true;
            try
            {
                for(int transitions=0;transitions<512;transitions++)
                {
                    ExpirePending();ProcessBoundary();Schedule();
                    // A zero reply gap still shares the sequence slot and starts the reply here.
                    if(sequence!=null&&Active==null&&sequence.Boundary<=Now)continue;
                    if(Now>=target)return;
                    float next=target;
                    if(sequence!=null)next=Math.Min(next,sequence.Boundary);
                    foreach(var candidate in pending)
                    {
                        next=Math.Min(next,candidate.ExpiresAt);
                        if(sequence==null)
                            foreach(var definition in candidate.Variants)
                            {
                                float eligible=EligibleAt(definition,candidate.Priority);
                                if(eligible>Now)next=Math.Min(next,eligible);
                            }
                    }
                    if(next<=Now)throw new InvalidOperationException("Очередь реакции не может продвинуть время.");
                    Now=next;
                }
                throw new InvalidOperationException("Слишком много переходов реакций за один Tick.");
            }
            finally{ticking=false;}
        }
    }
}
