using System.Collections.Generic;
using ChefShow.Core;

namespace ChefShow.Inventory
{
    public sealed partial class InventoryState
    {
        private readonly List<FoodPortion> tastingServed = new List<FoodPortion>();
        public IReadOnlyList<FoodPortion> TastingServed => tastingServed.AsReadOnly();
        public ChefShow.Data.DishwareSnapshot TastingDishware { get; private set; }
        public int TastingSaltDoses { get; private set; }
        public int TastingOilDoses { get; private set; }
        public bool TastingContaminated { get; private set; }

        public bool TryPlaceTastingDish(out string reason)
        {
            if (!Active(out reason)) return false;
            if (TastingDishware != null || SubmittedDish != null)
            { reason = "Подставка занята. Сначала заберите оценённое блюдо."; return false; }
            if (HeldDishware == null)
            { reason = "Возьмите целую тарелку со своего стола."; return false; }
            if (!HeldDishwareFromStation || CurrentDishware != null || served.Count != 0)
            { reason = "Сначала соберите блюдо на своём столе и возьмите эту тарелку целиком."; return false; }
            TastingDishware = HeldDishware;
            tastingServed.AddRange(heldServed);
            foreach (var food in tastingServed) { food.Location = PortionLocation.TastingStand; food.SocketIndex = -1; }
            TastingSaltDoses = HeldPlateSaltDoses; TastingOilDoses = HeldPlateOilDoses;
            TastingContaminated = HeldPlateContaminated;
            ClearHeldDishware();
            // Commit ownership and the immutable score input before any event can observe it.
            SubmittedDish = new DishSnapshot(this, true, true);
            Version++; run.Events.Publish(new DishChanged(run, "dish_submitted", this));
            return true;
        }

        public bool TryTakeTastingDish(out string reason)
        {
            if (!FreeHand(out reason)) return false;
            if (Placement == BasketPlacement.Carried) { reason = "Сначала поставьте корзину Tab."; return false; }
            if (TastingDishware == null) { reason = "На подставке нет тарелки."; return false; }
            HeldDishware = TastingDishware; HeldDishwareFromStation = true;
            heldServed.AddRange(tastingServed);
            foreach (var food in heldServed) { food.Location = PortionLocation.HeldDishware; food.SocketIndex = -1; }
            HeldPlateSaltDoses = TastingSaltDoses; HeldPlateOilDoses = TastingOilDoses;
            HeldPlateContaminated = TastingContaminated;
            tastingServed.Clear(); TastingDishware = null;
            TastingSaltDoses = TastingOilDoses = 0; TastingContaminated = false;
            SubmittedDish = null;
            Version++; run.Events.Publish(new DishChanged(run, "tasting_dish_retrieved", this));
            return true;
        }

        public bool TryDiscardHeldDishware(out string reason)
        {
            if (!Active(out reason)) return false;
            if (HeldDishware == null) { reason = "Возьмите тарелку в левую руку."; return false; }
            foreach (var food in heldServed)
            {
                food.Location = PortionLocation.Trash; food.SocketIndex = -1;
                food.RecordOperation("discarded_with_dishware", run.Clock.SimulationTime);
            }
            ClearHeldDishware();
            if (CurrentDishware == null && TastingDishware == null) PresentationPenalty = 0;
            Version++; run.Events.Publish(new DishChanged(run, "dishware_discarded", this));
            return true;
        }

        public DishSnapshot CaptureDisplayedDish() => HeldDishwareFromStation
            ? new DishSnapshot(this, false, false, true)
            : TastingDishware != null ? new DishSnapshot(this, false, true) : CaptureDish();
    }
}
