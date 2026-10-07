using UnityEngine;

namespace ChefShow.Data
{
    [CreateAssetMenu(menuName = "Chef Show/Ingredient")]
    public sealed class IngredientDefinition : ScriptableObject
    {
        public string Id;
        public string DisplayName;
        public bool CanUseBoard;
        public bool IsDoseContainer;
        [Tooltip("Для упаковки — продукт внутри. Открытие упаковок относится к F-005.")]
        public IngredientDefinition Contents;
        [Min(1), Tooltip("Число отдельных продуктов в закрытой упаковке. Одна упаковка занимает один слот.")]
        public int ContentsQuantity = 1;
        public Mesh VisualMesh;
        public Material VisualMaterial;
        public Vector3 VisualScale = Vector3.one * 0.14f;
    }
}
