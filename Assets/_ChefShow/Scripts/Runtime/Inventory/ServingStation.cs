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
        public Renderer SubmitButton;
        public Light SubmitLight;
        private MaterialPropertyBlock submitBlock;
        private Vector3[] stationPositions;
        private Quaternion[] stationRotations, plateRelativeRotations;
        [Min(1)] public int NominalCapacity = 6;
        public void Present(InventoryState state,bool own,Transform heldPlateMount=null)
        {
            RememberStationTransforms();
            var stationFood=own?state.Served:null;int stationCount=stationFood==null?0:stationFood.Count;
            var profile=own?state.CurrentDishware:ChefShow.Data.DishwareSnapshot.Default;
            if(PlateView!=null)PlateView.Present(profile);
            bool held=own && state.HeldDishwareFromStation && heldPlateMount!=null;
            var food=held?state.HeldServed:stationFood;int count=food==null?0:food.Count;
            var foodProfile=held?state.HeldDishware:profile;
            for(int i=0;i<Food.Length;i++)
            {
                int index=i==Food.Length-1 && count>Food.Length ? count-1 : i;
                var portion=index<count?food[index]:null;
                var mount=held?heldPlateMount:PlateView==null?null:PlateView.transform;
                if(mount!=null && foodProfile!=null)
                {
                    int layer=i/6;float angle=i%6*Mathf.PI/3+layer*.45f;
                    float radius=foodProfile.Diameter*(layer==0?.29f:.22f);
                    var offset=new Vector3(Mathf.Cos(angle)*radius,foodProfile.Depth+.04f+layer*.065f,Mathf.Sin(angle)*radius);
                    Food[i].transform.position=held?mount.TransformPoint(offset):mount.position+offset;
                }
                else Food[i].transform.localPosition=stationPositions[i];
                if(held)Food[i].transform.rotation=heldPlateMount.rotation*plateRelativeRotations[i];
                else Food[i].transform.localRotation=stationRotations[i];
                Food[i].PresentPortion(portion);
                var target=Food[i].GetComponent<ServingTarget>();target.Index=index;
                var collider=target.GetComponent<BoxCollider>();collider.enabled=portion!=null && !held;
                if(portion!=null && !held)
                {
                    var bounds=Food[i].Visual.bounds;
                    if(!Food[i].Visual.enabled)foreach(var r in Food[i].CutPieces)if(r.enabled)bounds.Encapsulate(r.bounds);
                    collider.center=target.transform.InverseTransformPoint(bounds.center);var size=bounds.size;
                    collider.size=Vector3.Max(new Vector3(size.x/target.transform.lossyScale.x,size.y/target.transform.lossyScale.y,size.z/target.transform.lossyScale.z),new Vector3(.07f,.07f,.07f));
                }
            }
            bool submitted=own && state.SubmittedDish!=null;
            Status.text=submitted?"ПОДАНО · "+stationCount+" порций":profile==null?"Место блюда · нет посуды":stationCount==0?profile.DisplayName:stationCount+" порций · "+profile.DisplayName+(own && state.PlateFillRatio>1?" · горка":"");
            if(own)Status.text+="\nПрезентабельность: −"+state.PresentationPenalty+" балл.\nКоличество "+state.Served.Sum(p=>p.Quantity)+" / номинал "+(profile==null?0:profile.NominalCapacity)+"\nСоль "+state.PlateSaltDoses+" · масло "+state.PlateOilDoses;
            if(held)Status.text+="\nТарелка в левой руке · "+state.HeldServed.Count+" порций";
            if(!held && count>Food.Length)Status.text+="\nЕщё "+(count-Food.Length)+" в горке";
            if(SubmitButton!=null)
            {
                submitBlock=submitBlock??new MaterialPropertyBlock();var color=submitted?new Color(1,.03f,.015f):new Color(.5f,.015f,.008f);
                submitBlock.SetColor("_BaseColor",color);submitBlock.SetColor("_Color",color);submitBlock.SetColor("_EmissionColor",submitted?Color.red*3:Color.black);SubmitButton.SetPropertyBlock(submitBlock);
            }
            if(SubmitLight!=null)SubmitLight.enabled=submitted;
        }
        private void RememberStationTransforms()
        {
            if(stationPositions!=null && stationPositions.Length==Food.Length)return;
            stationPositions=new Vector3[Food.Length];stationRotations=new Quaternion[Food.Length];plateRelativeRotations=new Quaternion[Food.Length];
            var plateRotation=PlateView==null?Quaternion.identity:PlateView.transform.rotation;
            for(int i=0;i<Food.Length;i++)
            {
                stationPositions[i]=Food[i].transform.localPosition;stationRotations[i]=Food[i].transform.localRotation;
                plateRelativeRotations[i]=Quaternion.Inverse(plateRotation)*Food[i].transform.rotation;
            }
        }
    }
}
