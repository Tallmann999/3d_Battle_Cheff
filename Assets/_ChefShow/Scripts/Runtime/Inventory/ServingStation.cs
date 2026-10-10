using System.Linq;
using ChefShow.Ingredients;
using UnityEngine;
namespace ChefShow.Inventory
{
    public sealed class ServingStation : MonoBehaviour
    {
        public string StationId;
        public FoodDisplay[] Food;
        public TextMesh Status;
        public DishwareView PlateView;
        [Min(1)] public int NominalCapacity = 6;
        public void Present(InventoryState state,bool own)
        {
            var food=own?state.Served:null; int count=food==null?0:food.Count;
            var profile=own?state.CurrentDishware:ChefShow.Data.DishwareSnapshot.Default;
            if(PlateView!=null)PlateView.Present(profile);
            for(int i=0;i<Food.Length;i++)
            {
                int index=i==Food.Length-1 && count>Food.Length ? count-1 : i;
                var portion=index<count?food[index]:null;
                if(PlateView!=null){int layer=i/6;float angle=i%6*Mathf.PI/3+layer*.45f;float radius=profile.Diameter*(layer==0?.29f:.22f);
                    Food[i].transform.position=PlateView.transform.position+new Vector3(Mathf.Cos(angle)*radius,profile.Depth+.04f+layer*.065f,Mathf.Sin(angle)*radius);}
                Food[i].PresentPortion(portion);
                var target=Food[i].GetComponent<ServingTarget>(); target.Index=index;
                var collider=target.GetComponent<BoxCollider>(); collider.enabled=portion!=null;
                if(portion!=null)
                { var bounds=Food[i].Visual.bounds; if(!Food[i].Visual.enabled) foreach(var r in Food[i].CutPieces) if(r.enabled) bounds.Encapsulate(r.bounds);
                  collider.center=target.transform.InverseTransformPoint(bounds.center); var size=bounds.size;
                  collider.size=Vector3.Max(new Vector3(size.x/target.transform.lossyScale.x,size.y/target.transform.lossyScale.y,size.z/target.transform.lossyScale.z),new Vector3(.07f,.07f,.07f)); }
            }
            Status.text=own && state.SubmittedDish!=null?"ПОДАНО · "+count+" порций"
                :count==0?profile.DisplayName:count+" порций · "+profile.DisplayName+(own && state.PlateFillRatio>1?" · горка":"")
                +(own?"\nКоличество "+state.Served.Sum(p=>p.Quantity)+" / номинал "+profile.NominalCapacity+"\nСоль "+state.PlateSaltDoses+" · масло "+state.PlateOilDoses:"")
                +(count>Food.Length?"\nЕщё "+(count-Food.Length)+" в горке":"");
        }
    }
}
