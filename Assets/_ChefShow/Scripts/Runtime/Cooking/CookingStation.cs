using ChefShow.Ingredients;
using ChefShow.Inventory;
using UnityEngine;

namespace ChefShow.Cooking
{
    public enum CookingTargetKind { Vessel, Food, HeatKnob }
    public sealed class CookingStation : MonoBehaviour
    {
        public string StationId;
        public CookerKind Kind;
        public FoodDisplay[] Food;
        public TextMesh Status;
        public Transform StirPoint;
        public Renderer HeatIndicator;
        public Renderer[] Smoke;
        private MaterialPropertyBlock block;
        public void Present(InventoryState state, bool own)
        {
            var food = own ? state.Cooker(Kind) : null;
            for (int i = 0; i < Food.Length; i++)
            {
                bool occupied = food != null && i < food.Count;
                Food[i].PresentPortion(occupied ? food[i] : null);
                Food[i].GetComponentInParent<CookingTarget>().GetComponent<BoxCollider>().enabled = occupied;
            }
            var level = own ? state.Heat(Kind) : HeatLevel.Off;
            Status.text = CookingController.HeatName(level);
            block = block ?? new MaterialPropertyBlock();
            block.SetColor("_BaseColor", level == HeatLevel.Off ? new Color(.18f,.22f,.25f)
                : Color.Lerp(new Color(1,.63f,.18f), new Color(1,.16f,.03f), (int)level / 3f));
            HeatIndicator.SetPropertyBlock(block);
            bool burned = food != null && System.Linq.Enumerable.Any(food, p => p.Cooking == CookState.Burned);
            foreach (var puff in Smoke) puff.enabled = burned;
        }
    }
}
