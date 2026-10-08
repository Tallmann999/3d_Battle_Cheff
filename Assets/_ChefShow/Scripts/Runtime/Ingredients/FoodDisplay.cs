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
        private MaterialPropertyBlock tint;

        public void PresentPortion(FoodPortion portion)
        {
            Present(portion?.Ingredient, portion?.Id);
            if (portion == null) return;
            ApplyTint(Visual, portion.Cooking);
            if (portion.ChopPresses == 0) return;
            Visual.enabled = false;
            int count = Mathf.Min(portion.ChopPresses + 1, CutPieces.Length);
            var size = portion.Ingredient.VisualScale * SizeMultiplier;
            for (int i = 0; i < count; i++)
            {
                var piece = CutPieces[i]; piece.enabled = true; piece.sharedMaterial = portion.Ingredient.VisualMaterial;
                ApplyTint(piece, portion.Cooking);
                piece.transform.localPosition = Mesh.transform.localPosition + Mesh.transform.localRotation *
                    new Vector3((i - (count - 1) * .5f) * (size.x / count + .006f * SizeMultiplier), 0, 0);
                piece.transform.localRotation = Mesh.transform.localRotation;
                piece.transform.localScale = new Vector3(size.x / count * .9f, size.y * .8f, size.z * .9f);
            }
        }

        private void ApplyTint(Renderer view, CookState cooking)
        {
            if (view.sharedMaterial == null) return;
            var color = view.sharedMaterial.GetColor("_BaseColor");
            if (cooking == CookState.Cooked) color = Color.Lerp(color, new Color(.55f,.28f,.10f), .55f);
            else if (cooking == CookState.Overcooked) color = Color.Lerp(color, new Color(.26f,.12f,.04f), .75f);
            else if (cooking == CookState.Burned) color = new Color(.07f,.06f,.05f);
            tint = tint ?? new MaterialPropertyBlock(); tint.SetColor("_BaseColor", color); view.SetPropertyBlock(tint);
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
            Visual.SetPropertyBlock(null);
            Mesh.transform.localScale = definition.VisualScale * SizeMultiplier;
        }
    }
}
