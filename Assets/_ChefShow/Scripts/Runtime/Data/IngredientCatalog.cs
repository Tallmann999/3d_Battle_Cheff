using System.Linq;
using UnityEngine;

namespace ChefShow.Data
{
    [CreateAssetMenu(menuName = "Chef Show/Ingredient Catalog")]
    public sealed class IngredientCatalog : ScriptableObject
    {
        public IngredientDefinition[] Ingredients;
        public string Validate()
        {
            if (Ingredients == null || Ingredients.Length < 12 || Ingredients.Any(d => d == null))
                return "В каталоге нужны 12 базовых продуктов; упаковки могут быть отдельными вариантами.";
            if (Ingredients.Any(d => string.IsNullOrWhiteSpace(d.Id) || string.IsNullOrWhiteSpace(d.DisplayName)
                || d.VisualMesh == null || d.VisualMaterial == null || d.VisualScale.x <= 0 || d.VisualScale.y <= 0 || d.VisualScale.z <= 0)
                || Ingredients.Select(d => d.Id).Distinct().Count() != Ingredients.Length)
                return "Проверьте уникальные ID, названия и визуалы продуктов.";
            return null;
        }
    }
}
