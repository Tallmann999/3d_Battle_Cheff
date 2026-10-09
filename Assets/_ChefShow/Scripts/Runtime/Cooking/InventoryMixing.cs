using System;
using System.Collections.Generic;
using System.Linq;
using ChefShow.Data;
using ChefShow.Ingredients;

namespace ChefShow.Inventory
{
    public sealed partial class InventoryState
    {
        private readonly List<FoodPortion> bowl=new List<FoodPortion>();
        public IReadOnlyList<FoodPortion> Bowl => bowl.AsReadOnly();
        public float MixProgress { get; private set; }
        public bool TryPlaceBowl(out string reason)
        {
            if(!Active(out reason))return false;
            if(cooking==null){reason="Миска не настроена.";return false;}
            if(!CanPlaceOnServingSurface(Held)){reason="В миску кладётся еда, упаковки открываются в лотке.";return false;}
            if(bowl.Count>=cooking.MixingCapacity){reason="Миска заполнена.";return false;}
            var food=Held;Held=null;food.Location=PortionLocation.MixingBowl;food.SocketIndex=-1;bowl.Add(food);MixProgress=0;Fact("ingredient_transferred",food);return true;
        }
        public bool TryTakeBowl(int index,out string reason)
        {
            if(!FreeHand(out reason))return false;
            if(index<0 || index>=bowl.Count){reason="Миска пуста.";return false;}
            Held=bowl[index];bowl.RemoveAt(index);origin=PortionLocation.MixingBowl;originIndex=index;Held.Location=PortionLocation.Hand;MixProgress=0;Fact("ingredient_transferred",Held);return true;
        }
        public bool TrySeasonBowl(int index,out string reason)
        {if(!Active(out reason))return false;return SeasonFood(index>=0 && index<bowl.Count?bowl[index]:null,out reason);}
        public bool TryAdvanceMix(float delta,KitchenToolKind tool,IngredientDefinition definition,out string reason)
        {
            if(!Active(out reason))return false;
            if(delta<0 || float.IsNaN(delta) || float.IsInfinity(delta)){reason="Некорректное время смешивания.";return false;}
            if(cooking==null || definition==null || definition.Id!="mixture"){reason="Смесь не настроена.";return false;}
            if(Held!=null){reason="Сначала положите продукт из левой руки.";return false;}
            if(tool!=KitchenToolKind.Spoon && tool!=KitchenToolKind.Spatula){reason="Возьмите ложку или деревянную лопатку правой рукой.";return false;}
            if(bowl.Count<2){reason=bowl.Count==1 && bowl[0].Preparation==PreparationState.Mixed?"Смесь готова — возьмите её левой рукой.":"Положите в миску хотя бы два продукта.";return false;}
            if(bowl.Sum(p=>(long)p.Quantity)>int.MaxValue || bowl.Sum(p=>(long)p.SaltDoses)>int.MaxValue || bowl.Sum(p=>(long)p.OilDoses)>int.MaxValue){reason="Смесь слишком большая.";return false;}
            MixProgress=Math.Min(cooking.MixSeconds,MixProgress+delta);Version++;
            if(MixProgress<cooking.MixSeconds)return true;
            var inputs=bowl.ToArray();var mixed=new FoodPortion(run.RunId+"_p"+ ++serial,definition){Location=PortionLocation.MixingBowl};
            mixed.InheritMixture(inputs);mixed.RecordOperation("mixture_completed",run.Clock.SimulationTime);
            foreach(var food in inputs){food.Location=PortionLocation.Mixed;food.SocketIndex=-1;food.RecordOperation("mixture_consumed",run.Clock.SimulationTime);}
            bowl.Clear();bowl.Add(mixed);portions.Add(mixed);Fact("mixture_completed",mixed);return true;
        }
    }
}
