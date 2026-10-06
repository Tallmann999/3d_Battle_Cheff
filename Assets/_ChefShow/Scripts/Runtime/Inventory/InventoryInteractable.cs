using ChefShow.Data;
using UnityEngine;

namespace ChefShow.Inventory
{
    public enum InventoryTargetKind { Pickup, Basket, BasketDock, PantryRest, Tray, TrayItem, Socket, Trash, PantryReturn }
    public sealed class InventoryInteractable : MonoBehaviour
    {
        public InventoryTargetKind Kind;
        public IngredientDefinition Ingredient;
        public string StationId;
        public int Index;
    }
}
