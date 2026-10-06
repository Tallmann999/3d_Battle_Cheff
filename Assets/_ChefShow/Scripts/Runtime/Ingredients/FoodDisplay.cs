using ChefShow.Data;
using UnityEngine;

namespace ChefShow.Ingredients
{
    public sealed class FoodDisplay : MonoBehaviour
    {
        public MeshFilter Mesh;
        public Renderer Visual;
        public TextMesh Caption;
        public float SizeMultiplier = 1;
        public IngredientDefinition Ingredient;
        public string PortionId;

        public void Present(IngredientDefinition definition, string portionId = null, bool showName = false)
        {
            Ingredient = definition; PortionId = portionId;
            Visual.enabled = definition != null;
            if (Caption != null) Caption.text = definition != null && showName ? definition.DisplayName : "";
            if (definition == null) return;
            Mesh.sharedMesh = definition.VisualMesh;
            Visual.sharedMaterial = definition.VisualMaterial;
            Mesh.transform.localScale = definition.VisualScale * SizeMultiplier;
        }
    }
}
