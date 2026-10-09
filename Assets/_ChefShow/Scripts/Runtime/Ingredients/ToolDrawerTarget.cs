using UnityEngine;

namespace ChefShow.Ingredients
{
    /// <summary>Доступная цель закрытия на свободной части дна ящика.</summary>
    public sealed class ToolDrawerTarget : MonoBehaviour
    {
        public ToolDrawer Drawer;
        [Tooltip("-1: панель/дно; 0–3: ячейка ножа, вилки, ложки, лопатки.")]
        public int CompartmentIndex = -1;
    }
}
