using ChefShow.Inventory;
using UnityEngine;

namespace ChefShow.Ingredients
{
    public sealed class ChoppingBoard : MonoBehaviour
    {
        public InventoryInteractable Target;
        public TextMesh Caption;
        public Transform KnifeContactPoint;
        public string StationId => Target == null ? null : Target.StationId;
        public void Present(FoodPortion portion)
        {
            Caption.text = "ДОСКА";
        }
    }
}
