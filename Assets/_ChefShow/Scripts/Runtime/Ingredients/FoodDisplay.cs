using ChefShow.Data;
using ChefShow.Inventory;
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
        [Tooltip("Сохранённые визуальные кусочки; одна логическая порция.")]
        public Renderer[] CutPieces;

        public void PresentPortion(FoodPortion portion)
        {
            Present(portion?.Ingredient, portion?.Id);
            if (portion == null || portion.ChopPresses == 0) return;
            Visual.enabled = false;
            int count = Mathf.Min(portion.ChopPresses + 1, CutPieces.Length);
            var size = portion.Ingredient.VisualScale * SizeMultiplier;
            for (int i = 0; i < count; i++)
            {
                var piece = CutPieces[i]; piece.enabled = true; piece.sharedMaterial = portion.Ingredient.VisualMaterial;
                piece.transform.localPosition = Mesh.transform.localPosition + Mesh.transform.localRotation *
                    new Vector3((i - (count - 1) * .5f) * (size.x / count + .006f * SizeMultiplier), 0, 0);
                piece.transform.localRotation = Mesh.transform.localRotation;
                piece.transform.localScale = new Vector3(size.x / count * .9f, size.y * .8f, size.z * .9f);
            }
        }

        public void Present(IngredientDefinition definition, string portionId = null, bool showName = false)
        {
            Ingredient = definition; PortionId = portionId;
            Visual.enabled = definition != null;
            if (CutPieces != null) foreach (var piece in CutPieces) if (piece != null) piece.enabled = false;
            if (Caption != null) Caption.text = definition != null && showName ? definition.DisplayName : "";
            if (definition == null) return;
            Mesh.sharedMesh = definition.VisualMesh;
            Visual.sharedMaterial = definition.VisualMaterial;
            Mesh.transform.localScale = definition.VisualScale * SizeMultiplier;
        }
    }
}
