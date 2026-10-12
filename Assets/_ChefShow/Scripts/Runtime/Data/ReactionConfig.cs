using System;
using System.Collections.Generic;
using System.Linq;
using ChefShow.Chefs;
using UnityEngine;

namespace ChefShow.Data
{
    [Serializable]
    public sealed class ReactionLineDefinition
    {
        public string Id;
        public ChefSpeaker Chef;
        [TextArea] public string Text;
        public AudioClip Clip;
    }
    [Serializable]
    public sealed class ReactionDefinition
    {
        public string Id;
        public ReactionCue Cue;
        public ReactionPriority Priority;
        public ChefSpeaker PrimaryChef;
        public ReactionLineDefinition[] Lines = Array.Empty<ReactionLineDefinition>();
    }
    [CreateAssetMenu(menuName="Chef Show/Reactive Chefs Config")]
    public sealed class ReactionConfig : ScriptableObject
    {
        public string SavoryName="Шеф горячей кухни", PastryName="Шеф-кондитер";
        [Min(0)] public float GlobalCooldown=7, PersonalCooldown=15, RepeatCooldown=60;
        [Range(1,64)] public int QueueCapacity=6;
        [Range(1,128)] public int HistoryCapacity=128;
        [Min(.01f)] public float AmbientTtl=5, NormalTtl=8, HighTtl=12;
        [Min(.01f)] public float WordsPerSecond=2.8f, MinimumDuration=2, MaximumDuration=6;
        [Min(0)] public float ReplyGap=.35f;
        [Range(0,100)] public float GoodScoreThreshold=80;
        public ReactionDefinition[] Definitions=Array.Empty<ReactionDefinition>();
        public string Validate()
        {
            if(string.IsNullOrWhiteSpace(SavoryName)||string.IsNullOrWhiteSpace(PastryName))return "Не назначены имена говорящих шефов.";
            if(!FiniteNonnegative(GlobalCooldown)||!FiniteNonnegative(PersonalCooldown)||!FiniteNonnegative(RepeatCooldown)
                ||!FiniteNonnegative(ReplyGap)||!FiniteNonnegative(GoodScoreThreshold)||GoodScoreThreshold>100)
                return "Неверные интервалы или порог оценки реакций.";
            if(QueueCapacity<1||QueueCapacity>64||HistoryCapacity<1||HistoryCapacity>128)
                return "Неверная вместимость очереди или истории реакций.";
            if(!Positive(AmbientTtl)||!Positive(NormalTtl)||!Positive(HighTtl)||!Positive(WordsPerSecond)
                ||!Positive(MinimumDuration)||!Positive(MaximumDuration)||MinimumDuration>MaximumDuration)
                return "Неверные сроки актуальности или длительности реакций.";
            if(Definitions==null)return "Не назначен массив реакций; пустой массив допустим.";
            var ids=new HashSet<string>(StringComparer.Ordinal);
            var lines=new HashSet<string>(StringComparer.Ordinal);
            foreach(var definition in Definitions)
            {
                if(definition==null||string.IsNullOrWhiteSpace(definition.Id)||!ids.Add(definition.Id)
                    ||!Enum.IsDefined(typeof(ReactionCue),definition.Cue)||!Enum.IsDefined(typeof(ReactionPriority),definition.Priority)
                    ||!Enum.IsDefined(typeof(ChefSpeaker),definition.PrimaryChef))return "Неверный или повторный профиль реакции.";
                if(definition.Lines==null||definition.Lines.Length<1||definition.Lines.Length>4)return "Реакции нужны от одной до четырёх реплик: "+definition.Id;
                foreach(var line in definition.Lines)
                    if(line==null||string.IsNullOrWhiteSpace(line.Id)||!lines.Add(line.Id)||string.IsNullOrWhiteSpace(line.Text)
                        ||!Enum.IsDefined(typeof(ChefSpeaker),line.Chef)||(line.Clip!=null&&!FiniteNonnegative(line.Clip.length)))
                        return "Неверная или повторная реплика: "+definition.Id;
                if(definition.Lines[0].Chef!=definition.PrimaryChef)return "Первую реплику должен произнести основной шеф: "+definition.Id;
            }
            return null;
        }
        private static bool FiniteNonnegative(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value)&&value>=0;
        private static bool Positive(float value)=>FiniteNonnegative(value)&&value>0;
        public ReactionSettings Capture()
        {var error=Validate();if(error!=null)throw new InvalidOperationException(error);return new ReactionSettings(this);}
    }
    public sealed class ReactionLineSettings
    {
        public string Id {get;}
        public ChefSpeaker Chef {get;}
        public string Text {get;}
        public AudioClip Clip {get;}
        public float Duration {get;}
        public float AudioDuration {get;}
        internal ReactionLineSettings(ReactionLineDefinition source,ReactionSettings settings)
        {
            Id=source.Id;Chef=source.Chef;Text=source.Text;Clip=source.Clip;
            AudioDuration=Clip==null?0:Clip.length;
            Duration=AudioDuration>0?AudioDuration:settings.TextDuration(Text);
        }
    }
    public sealed class ReactionDefinitionSettings
    {
        public string Id {get;}
        public ReactionCue Cue {get;}
        public ReactionPriority Priority {get;}
        public ChefSpeaker PrimaryChef {get;}
        public IReadOnlyList<ReactionLineSettings> Lines {get;}
        internal ReactionDefinitionSettings(ReactionDefinition source,ReactionSettings settings)
        {Id=source.Id;Cue=source.Cue;Priority=source.Priority;PrimaryChef=source.PrimaryChef;Lines=Array.AsReadOnly(source.Lines.Select(line=>new ReactionLineSettings(line,settings)).ToArray());}
    }
    public sealed class ReactionSettings
    {
        public string SavoryName {get;} public string PastryName {get;}
        public float GlobalCooldown {get;} public float PersonalCooldown {get;} public float RepeatCooldown {get;}
        public int QueueCapacity {get;} public int HistoryCapacity {get;}
        public float AmbientTtl {get;} public float NormalTtl {get;} public float HighTtl {get;}
        public float WordsPerSecond {get;} public float MinimumDuration {get;} public float MaximumDuration {get;}
        public float ReplyGap {get;} public float GoodScoreThreshold {get;}
        public IReadOnlyList<ReactionDefinitionSettings> Definitions {get;}
        internal ReactionSettings(ReactionConfig source)
        {
            SavoryName=source.SavoryName;PastryName=source.PastryName;
            GlobalCooldown=source.GlobalCooldown;PersonalCooldown=source.PersonalCooldown;RepeatCooldown=source.RepeatCooldown;
            QueueCapacity=source.QueueCapacity;HistoryCapacity=source.HistoryCapacity;
            AmbientTtl=source.AmbientTtl;NormalTtl=source.NormalTtl;HighTtl=source.HighTtl;
            WordsPerSecond=source.WordsPerSecond;MinimumDuration=source.MinimumDuration;MaximumDuration=source.MaximumDuration;
            ReplyGap=source.ReplyGap;GoodScoreThreshold=source.GoodScoreThreshold;
            Definitions=Array.AsReadOnly(source.Definitions.Select(definition=>new ReactionDefinitionSettings(definition,this)).ToArray());
        }
        public float TextDuration(string text)
        {
            int words=(text??"").Split((char[])null,StringSplitOptions.RemoveEmptyEntries).Length;
            return Math.Max(MinimumDuration,Math.Min(MaximumDuration,words/WordsPerSecond));
        }
        public string SpeakerName(ChefSpeaker chef)=>chef==ChefSpeaker.Savory?SavoryName:PastryName;
        public float Ttl(ReactionPriority priority)=>priority==ReactionPriority.Ambient?AmbientTtl:priority==ReactionPriority.Normal?NormalTtl:HighTtl;
    }
}
