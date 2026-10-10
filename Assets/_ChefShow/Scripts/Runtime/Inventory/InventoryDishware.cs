using System.Linq;
using ChefShow.Data;
namespace ChefShow.Inventory
{
    public sealed partial class InventoryState
    {
        private DishwareSettings dishware;
        public DishwareSnapshot CurrentDishware {get;private set;}=DishwareSnapshot.Default;
        public DishwareSnapshot HeldDishware {get;private set;}
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
            if(CurrentDishware!=null) RemoveInstalledDishware();
            CurrentDishware=HeldDishware;HeldDishware=null;Version++;
            run.Events.Publish(new DishChanged(run,"dishware_selected",this));return true;
        }
        public bool TryTakePlacedDishware(out string reason)
        {
            if(!PlateActive(out reason) || !FreeHand(out reason))return false;
            if(Placement==BasketPlacement.Carried){reason="Сначала поставьте корзину Tab.";return false;}
            if(CurrentDishware==null){reason="Здесь нет посуды.";return false;}
            HeldDishware=CurrentDishware;RemoveInstalledDishware();Version++;
            run.Events.Publish(new DishChanged(run,"dishware_removed",this));return true;
        }
        private void RemoveInstalledDishware()
        {
            // Commit the whole removal before callbacks: no food remains owned by the plate.
            foreach(var food in served){food.Location=PortionLocation.Trash;food.SocketIndex=-1;food.RecordOperation("discarded_with_dishware",run.Clock.SimulationTime);}
            served.Clear();PlateSaltDoses=PlateOilDoses=0;PlateContaminated=false;CurrentDishware=null;
            if(PresentationPenalty<int.MaxValue)PresentationPenalty++;
        }
        public bool TryReturnDishware(out string reason)
        {
            if(!Active(out reason))return false;
            if(HeldDishware==null){reason="В руке нет посуды.";return false;}
            HeldDishware=null;Version++;run.Events.Publish(new DishChanged(run,"dishware_returned",this));return true;
        }
    }
}
