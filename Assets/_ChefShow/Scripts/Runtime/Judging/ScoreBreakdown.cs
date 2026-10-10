using System;
using System.Collections.Generic;
using System.Linq;
using ChefShow.Inventory;

namespace ChefShow.Judging
{
    public enum ScoreCategoryId { Composition, ProcessTaste, Doneness, Preparation, Plating, Cleanliness }
    public sealed class ScoreCategory
    {
        public ScoreCategoryId Id { get; }
        public string Name { get; }
        public double Points { get; }
        public double Maximum { get; }
        public ScoreCategory(ScoreCategoryId id, string name, double points, double maximum)
        {
            if (!Enum.IsDefined(typeof(ScoreCategoryId), id) || string.IsNullOrWhiteSpace(name)
                || !Finite(points) || !Finite(maximum) || maximum <= 0 || points < 0 || points > maximum + .000001)
                throw new ArgumentException("Некорректная категория оценки.");
            Id = id; Name = name; Points = Math.Min(points, maximum); Maximum = maximum;
        }
        internal static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
    public sealed class ScoreBreakdown
    {
        public int Total { get; }
        public double UnroundedTotal { get; }
        public double RawTotal { get; }
        public double ApplicableCap { get; }
        // Actual identity stays independent from an assigned challenge.
        public string RecipeId { get; } public string RecipeName { get; }
        public string EvaluatedRecipeId { get; } public string EvaluatedRecipeName { get; }
        public bool IsExperimental { get; }
        public IReadOnlyList<ScoreCategory> Categories { get; }
        public IReadOnlyList<string> Reasons { get; }
        public ScoreBreakdown(string recipeId, string recipeName, string evaluatedRecipeId, string evaluatedRecipeName,
            bool experimental, IEnumerable<ScoreCategory> categories, IEnumerable<string> reasons, double applicableCap)
        {
            if (categories == null) throw new ArgumentException("Не назначены категории оценки.");
            var unsorted = categories.ToArray();
            UnroundedTotal = Aggregate(unsorted, applicableCap);
            var copy = unsorted.OrderBy(c => c.Id).ToArray();
            RawTotal = copy.Sum(c => c.Points); ApplicableCap = applicableCap;
            Total = (int)Math.Round(UnroundedTotal, MidpointRounding.AwayFromZero);
            RecipeId = recipeId; RecipeName = recipeName; EvaluatedRecipeId = evaluatedRecipeId;
            EvaluatedRecipeName = evaluatedRecipeName; IsExperimental = experimental;
            Categories = Array.AsReadOnly(copy);
            Reasons = Array.AsReadOnly((reasons ?? Enumerable.Empty<string>()).Where(r => !string.IsNullOrWhiteSpace(r)).Distinct().ToArray());
        }
        // The production evaluator and ranking share this exact sum/cap; rounding is only for display.
        public static double Aggregate(IEnumerable<ScoreCategory> categories, double applicableCap)
        {
            if (categories == null || !ScoreCategory.Finite(applicableCap) || applicableCap < 0 || applicableCap > 100)
                throw new ArgumentException("Некорректный предел оценки.");
            var copy = categories.ToArray();
            if (copy.Length != 6 || copy.Any(c => c == null || !Enum.IsDefined(typeof(ScoreCategoryId), c.Id)) || copy.Select(c => c.Id).Distinct().Count() != 6
                || Math.Abs(copy.Sum(c => c.Maximum) - 100) > .000001)
                throw new ArgumentException("Оценке нужны шесть категорий с общей суммой 100.");
            return Math.Max(0, Math.Min(copy.OrderBy(c => c.Id).Sum(c => c.Points), applicableCap));
        }
    }
    public readonly struct DishJudged
    {
        public readonly string RunId;
        public readonly float SimulationTime;
        public readonly DishSnapshot Dish;
        public readonly ScoreBreakdown Score;
        public DishJudged(string runId, float simulationTime, DishSnapshot dish, ScoreBreakdown score)
        {
            if (string.IsNullOrWhiteSpace(runId) || float.IsNaN(simulationTime) || float.IsInfinity(simulationTime)
                || simulationTime < 0 || dish == null || score == null) throw new ArgumentException("Некорректный факт оценки блюда.");
            RunId = runId; SimulationTime = simulationTime; Dish = dish; Score = score;
        }
    }
}
