using System.Collections.Generic;
using System.Linq;
using ChefShow.Data;
namespace ChefShow.Inventory
{
    public sealed partial class InventoryState
    {
        private DishwareSettings dishware;
        private readonly List<FoodPortion> heldServed = new List<FoodPortion>();
        public DishwareSnapshot CurrentDishware {get;private set;}=DishwareSnapshot.Default;
        public DishwareSnapshot HeldDishware {get;private set;}
        public IReadOnlyList<FoodPortion> HeldServed => heldServed.AsReadOnly();
        public int HeldPlateSaltDoses {get;private set;}
        public int HeldPlateOilDoses {get;private set;}
        public bool HeldPlateContaminated {get;private set;}
        public bool HeldDishwareFromStation {get;private set;}
        public DishwareSnapshot FindDishware(string id)=>dishware==null?null:dishware.Types.FirstOrDefault(t=>t.Id==id);
        public float PlateFillRatio=>CurrentDishware==null?0:served.Sum(p=>(float)p.Quantity)/CurrentDishware.NominalCapacity;
        public int PresentationPenalty {get;private set;}
        public bool TryTakeDishware(string id,out string reason)
        {
            if(!PlateActive(out reason) || !FreeHand(out reason))return false;
            if(Placement==BasketPlacement.Carried){reason="Сначала поставьте корзину Tab.";return false;}
            var type=dishware==null?null:dishware.Types.FirstOrDefault(t=>t.Id==id);
            if(type==null){reason="Этот вид посуды не настроен.";return false;}
            HeldDishware=type;Version++;run.Events.Publish(new DishChanged(run,"dishware_picked_up",this));return true;
        }
        public bool TryPlaceDishware(out string reason)
        {
            if(!PlateActive(out reason))return false;
            if(HeldDishware==null){reason="Возьмите посуду на полке рядом с духовкой.";return false;}
            if(HeldDishwareFromStation && (CurrentDishware!=null || served.Count!=0))
            {reason="Место блюда занято. Освободите его перед возвратом тарелки.";return false;}
            // Replacing installed dishware with a supply plate still discards its contents.
            if(CurrentDishware!=null) RemoveInstalledDishware();
            CurrentDishware=HeldDishware;
            served.AddRange(heldServed);
            foreach(var food in heldServed){food.Location=PortionLocation.Station;food.SocketIndex=1;}
            PlateSaltDoses=HeldPlateSaltDoses;PlateOilDoses=HeldPlateOilDoses;PlateContaminated=HeldPlateContaminated;
            ClearHeldDishware();Version++;
            run.Events.Publish(new DishChanged(run,"dishware_selected",this));return true;
        }
        public bool TryTakePlacedDishware(out string reason)
        {
            if(!PlateActive(out reason) || !FreeHand(out reason))return false;
            if(Placement==BasketPlacement.Carried){reason="Сначала поставьте корзину Tab.";return false;}
            if(CurrentDishware==null){reason="Здесь нет посуды.";return false;}
            // Transfer ownership before publishing: the station and the hand never share food.
            HeldDishware=CurrentDishware;HeldDishwareFromStation=true;
            heldServed.AddRange(served);
            foreach(var food in heldServed){food.Location=PortionLocation.HeldDishware;food.SocketIndex=-1;}
            HeldPlateSaltDoses=PlateSaltDoses;HeldPlateOilDoses=PlateOilDoses;HeldPlateContaminated=PlateContaminated;
            ClearInstalledDishware();
            if(PresentationPenalty<int.MaxValue)PresentationPenalty++;
            Version++;run.Events.Publish(new DishChanged(run,"dishware_removed",this));return true;
        }
        private void ClearInstalledDishware()
        {
            served.Clear();PlateSaltDoses=PlateOilDoses=0;PlateContaminated=false;CurrentDishware=null;
        }
        private void ClearHeldDishware()
        {
            HeldDishware=null;heldServed.Clear();HeldPlateSaltDoses=HeldPlateOilDoses=0;
            HeldPlateContaminated=false;HeldDishwareFromStation=false;
        }
        private void RemoveInstalledDishware()
        {
            // Commit the whole replacement before callbacks: the old food is explicitly discarded.
            foreach(var food in served){food.Location=PortionLocation.Trash;food.SocketIndex=-1;food.RecordOperation("discarded_with_dishware",run.Clock.SimulationTime);}
            ClearInstalledDishware();
            if(PresentationPenalty<int.MaxValue)PresentationPenalty++;
        }
        public bool TryReturnDishware(out string reason,bool returnToSupply=false)
        {
            if(!PlateActive(out reason))return false;
            if(HeldDishware==null){reason="В руке нет посуды.";return false;}
            if(HeldDishwareFromStation)
            {
                if(returnToSupply){reason="Эту тарелку верните на своё место блюда ЛКМ или Backspace; её еда остаётся в руке.";return false;}
                return TryPlaceDishware(out reason);
            }
            ClearHeldDishware();Version++;run.Events.Publish(new DishChanged(run,"dishware_returned",this));return true;
        }
    }
}
