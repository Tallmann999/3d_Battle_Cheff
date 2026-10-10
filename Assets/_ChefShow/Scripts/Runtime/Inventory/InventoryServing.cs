using System;
using System.Collections.Generic;
using System.Linq;
using ChefShow.Cooking;
using ChefShow.Core;

namespace ChefShow.Inventory
{
    public sealed class DishSnapshot
    {
        public ChefShow.Recipes.DishRecognition Recognition {get;}
        public bool IsSubmitted {get;}
        public IReadOnlyList<FoodPortionSnapshot> Portions { get; }
        public ChefShow.Data.DishwareSnapshot Dishware {get;}
        public float FillRatio=>Dishware==null?0:Quantity/(float)Dishware.NominalCapacity;
        public int PresentationPenalty {get;}
        public int SaltDoses { get; }
        public int OilDoses { get; }
        public int Quantity => Portions.Sum(p => p.Quantity);
        public bool Contaminated { get; }
        internal DishSnapshot(InventoryState state, bool submitted=false)
        {
            IsSubmitted=submitted;
            Dishware=state.CurrentDishware;PresentationPenalty=state.PresentationPenalty;
            Portions = Array.AsReadOnly(state.Served.Select(p => p.Snapshot()).ToArray());
            SaltDoses = state.PlateSaltDoses; OilDoses = state.PlateOilDoses; Contaminated = state.PlateContaminated;
            Recognition=state.RecognitionRules?.Resolve(this);
        }
    }
    public readonly struct DishChanged
    {
        public readonly string RunId, ActorId, Action;
        public readonly float SimulationTime;
        public readonly DishSnapshot Dish;
        internal DishChanged(PrototypeRun run, string action, InventoryState state)
        { RunId=run.RunId; ActorId=run.PlayerTeam+"1"; Action=action; SimulationTime=run.Clock.SimulationTime; Dish=state.SubmittedDish??new DishSnapshot(state); }
    }
    public sealed partial class InventoryState
    {
        private readonly List<FoodPortion> served = new List<FoodPortion>();
        public IReadOnlyList<FoodPortion> Served => served.AsReadOnly();
        public int PlateSaltDoses { get; private set; }
        public int PlateOilDoses { get; private set; }
        public bool PlateContaminated { get; private set; }
        public DishSnapshot SubmittedDish { get; private set; }
        public DishSnapshot CaptureDish() => new DishSnapshot(this);
        private bool PlateActive(out string reason)
        {
            if (!Active(out reason)) return false;
            if (SubmittedDish != null) { reason="Блюдо уже подано."; return false; }
            return true;
        }
        public bool TryPlaceServing(out string reason)
        {
            if (!PlateActive(out reason)) return false;
            if(CurrentDishware==null){reason="Сначала поставьте посуду на место блюда.";return false;}
            if (!CanPlaceOnServingSurface(Held)) { reason="Упаковки и контейнеры остаются в лотке. На тарелку можно класть еду."; return false; }
            var food=Held; Held=null; food.Location=PortionLocation.Station; food.SocketIndex=1;
            served.Add(food); Fact("food_plated",food); run.Events.Publish(new DishChanged(run,"food_plated",this)); return true;
        }
        public bool TryTakeServing(int index, out string reason)
        {
            if (!PlateActive(out reason) || !FreeHand(out reason)) return false;
            if (index<0 || index>=served.Count) { reason="На тарелке нет этой порции."; return false; }
            Held=served[index]; served.RemoveAt(index); origin=PortionLocation.Station; originIndex=1; originFoodIndex=index;
            Held.Location=PortionLocation.Hand; Held.SocketIndex=-1; Fact("ingredient_transferred",Held); return true;
        }
        public bool TrySeasonPlate(out string reason)
        {
            if (!PlateActive(out reason)) return false;
            SeasoningKind kind;
            if (cooking==null || Held==null || !cooking.TrySeasoning(Held.Ingredient,out kind)) { reason="Возьмите соль или масло левой рукой."; return false; }
            if (served.Count==0) { reason="Сначала положите еду на тарелку."; return false; }
            int current=kind==SeasoningKind.Salt?PlateSaltDoses:PlateOilDoses;
            if(current>int.MaxValue-cooking.DosesPerPress) { reason="Больше доз добавить нельзя.";return false; }
            if(kind==SeasoningKind.Salt) PlateSaltDoses+=cooking.DosesPerPress; else PlateOilDoses+=cooking.DosesPerPress;
            PlateContaminated |= Held.Contaminated; Held.RecordOperation("seasoning_dispensed",run.Clock.SimulationTime);
            Version++; run.Events.Publish(new DishChanged(run,"dish_seasoned",this)); return true;
        }
        public bool TrySeasonBoard(out string reason)
        {
            if(!Active(out reason)) return false;
            return SeasonFood(Socket(0),out reason);
        }
        private bool SeasonFood(FoodPortion food,out string reason)
        {
            reason=null;
            SeasoningKind kind;
            if(cooking==null || Held==null || !cooking.TrySeasoning(Held.Ingredient,out kind)) { reason="Возьмите соль или масло левой рукой.";return false; }
            if(food==null || !CanPlaceOnServingSurface(food)) { reason="Наведите на еду.";return false; }
            int current=kind==SeasoningKind.Salt?food.SaltDoses:food.OilDoses;
            if(current>int.MaxValue-cooking.DosesPerPress) { reason="Больше доз добавить нельзя.";return false; }
            if(kind==SeasoningKind.Salt) food.SaltDoses+=cooking.DosesPerPress; else food.OilDoses+=cooking.DosesPerPress;
            food.Contaminated |= Held.Contaminated; food.RecordOperation("seasoning_applied",run.Clock.SimulationTime);
            Held.RecordOperation("seasoning_dispensed",run.Clock.SimulationTime); Fact("seasoning_applied",food);return true;
        }
        public bool TrySubmitDish(out string reason)
        {
            if(!PlateActive(out reason)) return false;
            SubmitDish(); return true;
        }
        // Prototype-only UI calls this; the timer and all dish data remain unchanged.
        public bool TryResetSubmission(out string reason)
        {
            if(!Active(out reason)) return false;
            if(SubmittedDish==null){reason="Блюдо ещё не подано.";return false;}
            SubmittedDish=null;Version++;run.Events.Publish(new DishChanged(run,"submission_reset",this));return true;
        }
        internal void SubmitAtTimeup() { if(SubmittedDish==null) SubmitDish(); }
        private void SubmitDish()
        {
            SubmittedDish=new DishSnapshot(this,true); Version++; run.Events.Publish(new DishChanged(run,"dish_submitted",this));
        }
    }
}
