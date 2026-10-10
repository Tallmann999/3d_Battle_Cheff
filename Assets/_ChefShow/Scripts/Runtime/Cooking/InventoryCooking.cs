using System;
using System.Collections.Generic;
using ChefShow.Cooking;
using ChefShow.Ingredients;

namespace ChefShow.Inventory
{
    // Inventory remains the only owner of portions, including food in appliances.
    public sealed partial class InventoryState
    {
        private readonly List<FoodPortion>[] cookers = { new List<FoodPortion>(), new List<FoodPortion>(), new List<FoodPortion>() };
        private readonly HeatLevel[] heat = new HeatLevel[3];
        public bool OvenDoorOpen { get; private set; } = true;
        public bool TryToggleOvenDoor(out string reason)
        { if(!CookerActive(CookerKind.Oven,out reason))return false;OvenDoorOpen=!OvenDoorOpen;Version++;return true; }
        private CookingSettings cooking;
        private int originFoodIndex;
        public bool CookingEnabled => cooking != null;
        public CookingSettings CookingRules => cooking;
        public IReadOnlyList<FoodPortion> Cooker(CookerKind kind) => cookers[(int)kind].AsReadOnly();
        public HeatLevel Heat(CookerKind kind) => heat[(int)kind];
        private bool CookerActive(CookerKind kind, out string reason)
        {
            if (!Active(out reason)) return false;
            if (cooking == null || !Enum.IsDefined(typeof(CookerKind), kind)) { reason = "Прибор не настроен."; return false; }
            return true;
        }
        public bool TryPlaceCooker(CookerKind kind, out string reason)
        {
            if (!CookerActive(kind, out reason)) return false;
            if(kind==CookerKind.Oven && !OvenDoorOpen){reason="Откройте дверцу духовки.";return false;}
            if (Held == null) { reason = "Сначала возьмите продукт."; return false; }
            if (Held.PackedIngredient != null)
            { reason = "Это закрытая упаковка. Откройте её ПКМ в лотке."; return false; }
            if (Held.Ingredient.IsDoseContainer)
            { reason = "Контейнер остаётся в руке. ЛКМ по еде добавляет одну дозу."; return false; }
            if (!cooking.Accepts(kind, Held.Ingredient))
            { reason = "В прибор можно положить отдельный продукт."; return false; }
            var list = cookers[(int)kind];
            if (list.Count >= cooking.CapacityFor(kind)) { reason = "Прибор заполнен: " + cooking.CapacityFor(kind) + "/" + cooking.CapacityFor(kind) + "."; return false; }
            var food = Held; food.Location = PortionLocation.Appliance; food.SocketIndex = (int)kind;
            if (kind == CookerKind.Pot) food.RequiredStirs = Math.Max(food.RequiredStirs, cooking.PotStirs);
            list.Add(food); Held = null; Fact("ingredient_transferred", food); return true;
        }
        public bool TryTakeCooker(CookerKind kind, int index, out string reason)
        {
            if (!CookerActive(kind, out reason) || !FreeHand(out reason)) return false;
            if(kind==CookerKind.Oven && !OvenDoorOpen){reason="Откройте дверцу духовки.";return false;}
            var list = cookers[(int)kind];
            if (index < 0 || index >= list.Count) { reason = "В приборе нет этой порции."; return false; }
            Held = list[index]; list.RemoveAt(index); origin = PortionLocation.Appliance;
            originIndex = (int)kind; originFoodIndex = index;
            Held.Location = PortionLocation.Hand; Held.SocketIndex = -1; Fact("ingredient_transferred", Held); return true;
        }
        public bool TryCycleHeat(CookerKind kind, out string reason)
        {
            if (!CookerActive(kind, out reason)) return false;
            heat[(int)kind] = (HeatLevel)(((int)heat[(int)kind] + 1) % 4); Version++;
            run.Events.Publish(new CookingChanged(run, "heat_changed", kind, heat[(int)kind], null, CookState.Raw)); return true;
        }
        public bool TryApplySeasoning(CookerKind kind, int index, out string reason)
        {
            if (!CookerActive(kind, out reason)) return false;
            if(kind==CookerKind.Oven && !OvenDoorOpen){reason="Откройте дверцу духовки.";return false;}
            SeasoningKind seasoning;
            if (Held == null || Held.PackedIngredient != null || !cooking.TrySeasoning(Held.Ingredient, out seasoning))
            { reason = "Возьмите соль или масло из лотка: ЛКМ."; return false; }
            if (seasoning == SeasoningKind.Oil && kind != CookerKind.Pan)
            { reason = "Масло добавляется только в сковороду."; return false; }
            var list = cookers[(int)kind];
            if (index < 0 || index >= list.Count)
            { reason = "Наведите прицел на конкретную порцию в приборе."; return false; }
            var food = list[index]; var source = Held;
            int current = seasoning == SeasoningKind.Salt ? food.SaltDoses : food.OilDoses;
            if (current > int.MaxValue - cooking.DosesPerPress)
            { reason = "Больше доз добавить нельзя."; return false; }
            if (seasoning == SeasoningKind.Salt) food.SaltDoses += cooking.DosesPerPress;
            else food.OilDoses += cooking.DosesPerPress;
            food.Contaminated |= source.Contaminated;
            food.RecordOperation("seasoning_applied", run.Clock.SimulationTime, kind);
            source.RecordOperation("seasoning_dispensed", run.Clock.SimulationTime);
            Version++;
            // Commit first; callbacks cannot alter the independent fact.
            run.Events.Publish(new SeasoningApplied(run, kind, seasoning, cooking.DosesPerPress, food, source));
            return true;
        }
        public bool TryStir(CookerKind kind, KitchenToolKind tool, out string reason)
        {
            if (!CookerActive(kind, out reason)) return false;
            if(kind==CookerKind.Oven){reason="Духовка печёт в форме; перемешивайте в миске.";return false;}
            if (Held != null) { reason = "Сначала положите продукт из руки."; return false; }
            if (tool != KitchenToolKind.Spatula) { reason = "Возьмите деревянную лопатку: ЛКМ."; return false; }
            if (Heat(kind) == HeatLevel.Off) { reason = "Сначала включите нагрев отдельной ручкой: ЛКМ."; return false; }
            var facts = new List<CookingChanged>();
            foreach (var food in cookers[(int)kind])
            {
                int required = kind == CookerKind.Pot ? cooking.PotStirs : 1;
                if (food.Cooking == CookState.Burned || food.StirPresses >= required) continue;
                var previous = food.Cooking; food.StirPresses++;
                Recalculate(food, kind); food.RecordOperation("food_stirred", run.Clock.SimulationTime, kind);
                facts.Add(new CookingChanged(run, "food_stirred", kind, Heat(kind), food, previous));
                if (food.Cooking != previous)
                {
                    food.RecordOperation("food_state_changed", run.Clock.SimulationTime, kind);
                    facts.Add(new CookingChanged(run, "food_state_changed", kind, Heat(kind), food, previous));
                }
            }
            if (facts.Count == 0) { reason = "Перемешивание не требуется или прибор пуст."; return false; }
            Version++; foreach (var fact in facts) run.Events.Publish(fact); return true;
        }
        internal void TickCooking(float delta)
        {
            if (cooking == null || delta <= 0 || run.Disposed || run.Clock.Paused) return;
            var facts = new List<CookingChanged>(); bool changed = false;
            for (int i = 0; i < 3; i++)
            {
                var kind = (CookerKind)i; if(kind==CookerKind.Oven && OvenDoorOpen)continue; float rate = cooking.Rate(heat[i]); if (rate == 0) continue;
                foreach (var food in cookers[i])
                {
                    if (food.Cooking == CookState.Burned) continue;
                    var previous = food.Cooking; bool start = food.HeatProgress == 0 || food.LastCooker != kind;
                    food.HeatProgress = Math.Min(kind==CookerKind.Oven?cooking.OvenBurned:cooking.Burned, food.HeatProgress + delta * rate);
                    food.LastCooker = kind; Recalculate(food, kind); changed = true;
                    if (start) { food.RecordOperation("cook_started", run.Clock.SimulationTime, kind); facts.Add(new CookingChanged(run, "cook_started", kind, heat[i], food, previous)); }
                    if (food.Cooking != previous)
                    {
                        string action = food.Cooking == CookState.Burned ? "food_burned" : "food_state_changed";
                        food.RecordOperation(action, run.Clock.SimulationTime, kind); facts.Add(new CookingChanged(run, action, kind, heat[i], food, previous));
                    }
                }
            }
            if (changed) Version++;
            // All data has been committed before callbacks can move/reset food.
            foreach (var fact in facts) run.Events.Publish(fact);
        }
        private void Recalculate(FoodPortion food, CookerKind kind)
        {
            if (food.Cooking == CookState.Burned) return;
            float burned=kind==CookerKind.Oven?cooking.OvenBurned:cooking.Burned;
            float over=kind==CookerKind.Oven?cooking.OvenOvercooked:cooking.Overcooked;
            food.Cooking = food.HeatProgress >= burned ? CookState.Burned
                : food.HeatProgress < cooking.ReadyFor(kind) || (kind == CookerKind.Pot && food.StirPresses < food.RequiredStirs) ? CookState.Cooking
                : food.HeatProgress >= over ? CookState.Overcooked : CookState.Cooked;
        }
    }
}
