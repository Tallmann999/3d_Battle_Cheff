using System;
using ChefShow.Core;
using ChefShow.Data;
using ChefShow.Inventory;

namespace ChefShow.Cooking
{
    public enum CookerKind { Pan, Pot, Oven }
    public enum HeatLevel { Off, Low, Medium, High }
    public enum SeasoningKind { Salt, Oil }
    public sealed class CookingSettings
    {
        public readonly int Capacity, PotStirs, MixingCapacity;
        public readonly float MixSeconds, OvenReady, OvenOvercooked, OvenBurned;
        public int CapacityFor(CookerKind kind) => kind==CookerKind.Oven?1:Capacity;
        public float ReadyFor(CookerKind kind) => kind==CookerKind.Oven?OvenReady:Ready;
        public readonly int DosesPerPress;
        public readonly string SaltIngredientId, OilIngredientId;
        public readonly float Ready, Overcooked, Burned, LowRate, MediumRate, HighRate;
        public CookingSettings(int capacity, float ready, float over, float burned, float low, float medium, float high,
            int stirs,
            int dosesPerPress = 1, string saltId = "salt", string oilId = "oil",
            int mixingCapacity=6,float mixSeconds=3,float ovenReady=45,float ovenOver=60,float ovenBurned=75)
        {
            if (capacity < 1 || capacity > 3 || stirs < 0 || stirs > 10 || !Valid(ready) || !Valid(over) || !Valid(burned)
                || !(ready < over && over < burned) || !Valid(low) || !Valid(medium) || !Valid(high) || !(low < medium && medium < high)) throw new ArgumentException("Некорректные параметры готовки.");
            Capacity = capacity; Ready = ready; Overcooked = over; Burned = burned;
            LowRate = low; MediumRate = medium; HighRate = high; PotStirs = stirs;
            if (dosesPerPress < 1 || dosesPerPress > 10 || string.IsNullOrWhiteSpace(saltId)
                || string.IsNullOrWhiteSpace(oilId) || saltId == oilId) throw new ArgumentException("Некорректные дозы или ID контейнеров.");
            DosesPerPress = dosesPerPress; SaltIngredientId = saltId; OilIngredientId = oilId;
            if(mixingCapacity<1 || mixingCapacity>6 || !Valid(mixSeconds) || !Valid(ovenReady) || !(ovenReady<ovenOver && ovenOver<ovenBurned) || !Valid(ovenOver) || !Valid(ovenBurned)) throw new ArgumentException("Некорректная миска/духовка.");
            MixingCapacity=mixingCapacity;MixSeconds=mixSeconds;OvenReady=ovenReady;OvenOvercooked=ovenOver;OvenBurned=ovenBurned;
        }
        public bool TrySeasoning(IngredientDefinition ingredient, out SeasoningKind kind)
        {
            kind = SeasoningKind.Salt;
            if (ingredient == null || !ingredient.IsDoseContainer) return false;
            if (ingredient.Id == SaltIngredientId) return true;
            kind = SeasoningKind.Oil;
            return ingredient.Id == OilIngredientId;
        }
        private static bool Valid(float n) => n > 0 && !float.IsNaN(n) && !float.IsInfinity(n);
        // Recipe correctness belongs to judging; appliances accept all unpacked food.
        public bool Accepts(CookerKind kind, IngredientDefinition ingredient) => ingredient != null
            && (kind == CookerKind.Pan || kind == CookerKind.Pot || kind==CookerKind.Oven)
            && ingredient.Contents == null && !ingredient.IsDoseContainer;
        public float Rate(HeatLevel level) => level == HeatLevel.Low ? LowRate : level == HeatLevel.Medium ? MediumRate : level == HeatLevel.High ? HighRate : 0;
    }

    public readonly struct SeasoningApplied
    {
        public readonly string RunId, RoundId, ActorId, Action;
        public readonly TeamId Team;
        public readonly float SimulationTime;
        public readonly CookerKind Cooker;
        public readonly SeasoningKind Kind;
        public readonly int AddedDoses;
        public readonly FoodPortionSnapshot Food, Source;
        internal SeasoningApplied(PrototypeRun run, CookerKind cooker, SeasoningKind kind, int doses,
            FoodPortion food, FoodPortion source)
        {
            RunId = run.RunId; RoundId = "prototype"; ActorId = run.PlayerTeam + "1"; Team = run.PlayerTeam;
            Action = "seasoning_applied"; SimulationTime = run.Clock.SimulationTime;
            Cooker = cooker; Kind = kind; AddedDoses = doses; Food = food.Snapshot(); Source = source.Snapshot();
        }
    }

    public readonly struct CookingChanged
    {
        public readonly string RunId, ActorId, Action;
        public readonly TeamId Team;
        public readonly float SimulationTime;
        public readonly CookerKind Cooker;
        public readonly HeatLevel Level;
        public readonly CookState Previous;
        public readonly FoodPortionSnapshot Food;
        internal CookingChanged(PrototypeRun run, string action, CookerKind cooker, HeatLevel level, FoodPortion food, CookState previous)
        {
            RunId = run.RunId; Team = run.PlayerTeam; ActorId = Team + "1"; SimulationTime = run.Clock.SimulationTime;
            Action = action; Cooker = cooker; Level = level; Previous = previous; Food = food?.Snapshot();
        }
    }
}
