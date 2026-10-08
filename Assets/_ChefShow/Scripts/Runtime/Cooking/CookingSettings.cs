using System;
using System.Collections.Generic;
using ChefShow.Core;
using ChefShow.Data;
using ChefShow.Inventory;

namespace ChefShow.Cooking
{
    public enum CookerKind { Pan, Pot }
    public enum HeatLevel { Off, Low, Medium, High }
    public sealed class CookingSettings
    {
        public readonly int Capacity, PotStirs;
        public readonly float Ready, Overcooked, Burned, LowRate, MediumRate, HighRate;
        private readonly HashSet<string> pan, pot;
        public CookingSettings(int capacity, float ready, float over, float burned, float low, float medium, float high,
            int stirs, IEnumerable<string> panIds, IEnumerable<string> potIds)
        {
            if (capacity < 1 || capacity > 3 || stirs < 0 || stirs > 10 || !Valid(ready) || !Valid(over) || !Valid(burned)
                || !(ready < over && over < burned) || !Valid(low) || !Valid(medium) || !Valid(high) || !(low < medium && medium < high)
                || panIds == null || potIds == null) throw new ArgumentException("Некорректные параметры готовки.");
            Capacity = capacity; Ready = ready; Overcooked = over; Burned = burned;
            LowRate = low; MediumRate = medium; HighRate = high; PotStirs = stirs;
            pan = new HashSet<string>(panIds); pot = new HashSet<string>(potIds);
            if (pan.Count == 0 || pot.Count == 0 || pan.Contains(null) || pot.Contains(null) || pan.Contains("") || pot.Contains(""))
                throw new ArgumentException("Нужны ID продуктов.");
        }
        private static bool Valid(float n) => n > 0 && !float.IsNaN(n) && !float.IsInfinity(n);
        public bool Accepts(CookerKind kind, IngredientDefinition ingredient) => ingredient != null
            && (kind == CookerKind.Pan ? pan : pot).Contains(ingredient.Id);
        public float Rate(HeatLevel level) => level == HeatLevel.Low ? LowRate : level == HeatLevel.Medium ? MediumRate : level == HeatLevel.High ? HighRate : 0;
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
