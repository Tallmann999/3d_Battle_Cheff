using System.Collections.Generic;
using System.Linq;
using ChefShow.Data;
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
        public Transform TastingMount;
        public DishwareView TastingPlateView;
        public ServingTarget TastingTarget;
        public TextMesh TastingStatus;
        public Renderer SubmitButton;
        public Light SubmitLight;
        private MaterialPropertyBlock submitBlock;
        private Vector3[] stationPositions;
        private Quaternion[] stationRotations, plateRelativeRotations;
        private ServingTarget[] foodTargets;
        private BoxCollider[] foodColliders;
        private InventoryState presentedState;
        private Transform presentedHeldMount;
        private int presentedVersion=-1, displayedCount;
        private bool presentedOwn, displayedHeld;
        private DishwareSnapshot displayedProfile;
        [Min(1)] public int NominalCapacity = 6;
        public void Present(InventoryState state,bool own,Transform heldPlateMount=null)
        {
            RememberStationTransforms();
            bool first=presentedState==null;
            bool changed=first || own!=presentedOwn || (own && (!ReferenceEquals(presentedState,state) || presentedVersion!=state.Version || presentedHeldMount!=heldPlateMount));
            if(!changed)
            {
                // Carried food follows the hand; static renderers and labels need no refresh.
                if(displayedHeld && heldPlateMount!=null)
                    for(int i=0;i<Mathf.Min(displayedCount,Food.Length);i++)PositionFood(i,heldPlateMount,displayedProfile,true);
                return;
            }
            presentedState=state;presentedVersion=state.Version;presentedOwn=own;presentedHeldMount=heldPlateMount;
            var stationFood=own?state.Served:null;int stationCount=stationFood==null?0:stationFood.Count;
            var profile=own?state.CurrentDishware:DishwareSnapshot.Default;
            var tastingProfile=own?state.TastingDishware:null;
            if(PlateView!=null)PlateView.Present(profile);
            if(TastingPlateView!=null)TastingPlateView.Present(tastingProfile);
            bool held=own && state.HeldDishwareFromStation && heldPlateMount!=null;
            bool tasting=!held && tastingProfile!=null && TastingMount!=null;
            IReadOnlyList<FoodPortion> food=held?state.HeldServed:tasting?state.TastingServed:stationFood;
            int count=food==null?0:food.Count;
            var foodProfile=held?state.HeldDishware:tasting?tastingProfile:profile;
            var mount=held?heldPlateMount:tasting?TastingMount:PlateView==null?null:PlateView.transform;
            displayedCount=count;displayedProfile=foodProfile;displayedHeld=held;
            for(int i=0;i<Food.Length;i++)
            {
                int index=i==Food.Length-1 && count>Food.Length?count-1:i;
                var portion=index<count?food[index]:null;
                if(mount!=null && foodProfile!=null)PositionFood(i,mount,foodProfile,held || tasting);
                else
                {
                    Food[i].transform.localPosition=stationPositions[i];
                    Food[i].transform.localRotation=stationRotations[i];
                }
                // Avoid resetting seven invisible cut-piece renderers for an unchanged empty slot.
                if(first || portion!=null || Food[i].Ingredient!=null)Food[i].PresentPortion(portion);
                var target=foodTargets[i];target.Index=index;
                var collider=foodColliders[i];collider.enabled=portion!=null && !held && !tasting;
                if(portion!=null && !held && !tasting)
                {
                    var bounds=Food[i].Visual.bounds;
                    if(!Food[i].Visual.enabled)foreach(var r in Food[i].CutPieces)if(r.enabled)bounds.Encapsulate(r.bounds);
                    collider.center=target.transform.InverseTransformPoint(bounds.center);var size=bounds.size;
                    collider.size=Vector3.Max(new Vector3(size.x/target.transform.lossyScale.x,size.y/target.transform.lossyScale.y,size.z/target.transform.lossyScale.z),new Vector3(.07f,.07f,.07f));
                }
            }
            bool submitted=own && state.SubmittedDish!=null;
            bool workingSubmitted=submitted && tastingProfile==null;
            Status.text=workingSubmitted?"ПОДАНО · "+stationCount+" порций":profile==null?"Место блюда · нет посуды":stationCount==0?profile.DisplayName:stationCount+" порций · "+profile.DisplayName+(own && state.PlateFillRatio>1?" · горка":"");
            if(own)Status.text+="\nПрезентабельность: −"+state.PresentationPenalty+" балл.\nКоличество "+state.Served.Sum(p=>p.Quantity)+" / номинал "+(profile==null?0:profile.NominalCapacity)+"\nСоль "+state.PlateSaltDoses+" · масло "+state.PlateOilDoses;
            if(held)Status.text+="\nТарелка в левой руке · "+state.HeldServed.Count+" порций";
            if(!held && !tasting && count>Food.Length)Status.text+="\nЕщё "+(count-Food.Length)+" в горке";
            if(TastingStatus!=null)
            {
                TastingStatus.text=tastingProfile==null?"ДЕГУСТАЦИЯ\nПоставьте тарелку с едой":"ПОДАНО · "+state.TastingServed.Count+" порций\n"+tastingProfile.DisplayName+"\nСоль "+state.TastingSaltDoses+" · масло "+state.TastingOilDoses+"\nЛКМ — забрать тарелку";
                if(tasting && count>Food.Length)TastingStatus.text+="\nЕщё "+(count-Food.Length)+" в горке";
            }
            // Serialized compatibility only; the stand installer disables the old buttons.
            if(SubmitButton!=null && TastingTarget==null)
            {
                submitBlock=submitBlock??new MaterialPropertyBlock();var color=submitted?new Color(1,.03f,.015f):new Color(.5f,.015f,.008f);
                submitBlock.SetColor("_BaseColor",color);submitBlock.SetColor("_Color",color);submitBlock.SetColor("_EmissionColor",submitted?Color.red*3:Color.black);SubmitButton.SetPropertyBlock(submitBlock);
            }
            if(SubmitLight!=null)SubmitLight.enabled=TastingTarget==null && submitted;
        }
        private void PositionFood(int index,Transform mount,DishwareSnapshot profile,bool followRotation)
        {
            int layer=index/6;float angle=index%6*Mathf.PI/3+layer*.45f;
            float radius=profile.Diameter*(layer==0?.29f:.22f);
            var offset=new Vector3(Mathf.Cos(angle)*radius,profile.Depth+.04f+layer*.065f,Mathf.Sin(angle)*radius);
            Food[index].transform.position=followRotation?mount.TransformPoint(offset):mount.position+offset;
            if(followRotation)Food[index].transform.rotation=mount.rotation*plateRelativeRotations[index];
            else Food[index].transform.localRotation=stationRotations[index];
        }
        private void RememberStationTransforms()
        {
            if(stationPositions!=null && stationPositions.Length==Food.Length)return;
            stationPositions=new Vector3[Food.Length];stationRotations=new Quaternion[Food.Length];plateRelativeRotations=new Quaternion[Food.Length];
            foodTargets=new ServingTarget[Food.Length];foodColliders=new BoxCollider[Food.Length];
            var plateRotation=PlateView==null?Quaternion.identity:PlateView.transform.rotation;
            for(int i=0;i<Food.Length;i++)
            {
                stationPositions[i]=Food[i].transform.localPosition;stationRotations[i]=Food[i].transform.localRotation;
                plateRelativeRotations[i]=Quaternion.Inverse(plateRotation)*Food[i].transform.rotation;
                foodTargets[i]=Food[i].GetComponent<ServingTarget>();foodColliders[i]=foodTargets[i].GetComponent<BoxCollider>();
            }
        }
    }
}
