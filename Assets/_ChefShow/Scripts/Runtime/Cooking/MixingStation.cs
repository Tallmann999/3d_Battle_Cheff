using ChefShow.Ingredients;
using ChefShow.Inventory;
using UnityEngine;
namespace ChefShow.Cooking
{
    public sealed class MixingStation : MonoBehaviour
    {
        public string StationId; public FoodDisplay[] Food; public TextMesh Status; public Transform Contact;
        public void Present(InventoryState state,bool own)
        {
            var foods=own?state.Bowl:null;
            for(int i=0;i<Food.Length;i++)
            {bool occupied=foods!=null && i<foods.Count;Food[i].PresentPortion(occupied?foods[i]:null);Food[i].GetComponent<BoxCollider>().enabled=occupied;}
            Status.text=foods==null || foods.Count==0?"МИСКА":foods.Count==1 && foods[0].Preparation==PreparationState.Mixed?"СМЕСЬ ГОТОВА · "+foods[0].Quantity+" компонентов":"СМЕШИВАНИЕ "+Mathf.FloorToInt(state.MixProgress/state.CookingRules.MixSeconds*100)+"%";
        }
    }
}
