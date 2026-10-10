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
        public float PlateFillRatio=>served.Sum(p=>(float)p.Quantity)/CurrentDishware.NominalCapacity;
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
            if(HeldDishware==null){reason="Возьмите посуду на длинном столе.";return false;}
            CurrentDishware=HeldDishware;HeldDishware=null;Version++;
            run.Events.Publish(new DishChanged(run,"dishware_selected",this));return true;
        }
        public bool TryReturnDishware(out string reason)
        {
            if(!Active(out reason))return false;
            if(HeldDishware==null){reason="В руке нет посуды.";return false;}
            HeldDishware=null;Version++;run.Events.Publish(new DishChanged(run,"dishware_returned",this));return true;
        }
    }
}
