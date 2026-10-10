using System;
using System.Linq;
using ChefShow.Cooking;
using ChefShow.Recipes;
using UnityEngine;

namespace ChefShow.Data
{
    public enum JudgingRole { Main, Garnish, Vegetable }

    [Serializable] public sealed class JudgingRange
    {
        public float ZeroBelow, IdealMinimum, IdealMaximum, ZeroAbove;
        public JudgingRange() : this(0, 0, 0, 1) { }
        public JudgingRange(float zeroBelow, float idealMinimum, float idealMaximum, float zeroAbove)
        { ZeroBelow = zeroBelow; IdealMinimum = idealMinimum; IdealMaximum = idealMaximum; ZeroAbove = zeroAbove; }
        public string Validate()
        {
            return !Finite(ZeroBelow) || !Finite(IdealMinimum) || !Finite(IdealMaximum) || !Finite(ZeroAbove)
                || ZeroBelow < 0 || ZeroBelow > IdealMinimum || IdealMinimum > IdealMaximum || IdealMaximum >= ZeroAbove
                ? "Диапазон должен возрастать: нижняя граница ≤ идеальный минимум ≤ идеальный максимум < верхняя граница." : null;
        }
        internal static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    [Serializable] public sealed class JudgingCorePortion
    {
        [Min(0)] public int RecognitionCoreIndex;
        [Min(.01f)] public float ReferenceQuantity = 1;
        public JudgingRole Role = JudgingRole.Main;
        public bool MandatoryProtein;
        public bool MainComponent;
        public bool UseApplianceHeatDefaults = true;
        public JudgingRange Heat = new JudgingRange(0, 30, 45, 60);
    }

    [CreateAssetMenu(menuName = "Chef Show/Judging Recipe Profile")]
    public sealed class JudgingRecipeProfile : ScriptableObject
    {
        public RecipeDefinition Reference;
        public JudgingCorePortion[] Core;
        [Tooltip("Doses per actual leaf unit; not per mixture container. Proposed until balancing is approved.")]
        public JudgingRange Salt = new JudgingRange();
        public JudgingRange Oil = new JudgingRange();
        public bool LiquidFood;
        [Range(0, 1)] public float IdealFillMinimum = 0;
        [Min(0)] public float IdealFillMaximum = 1;
        [Min(0)] public float ZeroFillAt = 2.5f;
        public string Validate()
        {
            if (Reference == null || Reference.Validate() != null) return "Профилю оценки нужен справочный рецепт.";
            if (Core == null || Core.Length == 0 || Core.Any(c => c == null || c.RecognitionCoreIndex < 0
                || !JudgingRange.Finite(c.ReferenceQuantity) || c.ReferenceQuantity <= 0
                || !Enum.IsDefined(typeof(JudgingRole), c.Role) || c.Heat == null || c.Heat.Validate() != null)
                || Core.Select(c => c.RecognitionCoreIndex).Distinct().Count() != Core.Length)
                return "Проверьте ядро, количества, роли и нагрев оценки: " + Reference.Id;
            if (Salt == null || Oil == null || Salt.Validate() != null || Oil.Validate() != null)
                return "Проверьте нормированные диапазоны соли и масла: " + Reference.Id;
            if (!JudgingRange.Finite(IdealFillMinimum) || !JudgingRange.Finite(IdealFillMaximum)
                || !JudgingRange.Finite(ZeroFillAt) || IdealFillMinimum < 0
                || IdealFillMinimum > IdealFillMaximum || IdealFillMaximum >= ZeroFillAt)
                return "Проверьте границы заполнения посуды: " + Reference.Id;
            return null;
        }
    }
}
