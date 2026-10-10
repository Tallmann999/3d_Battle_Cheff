using System;
using System.Collections.Generic;
using System.Linq;
using ChefShow.Cooking;
using ChefShow.Data;
using ChefShow.Recipes;
using ChefShow.Judging;
using UnityEngine;

namespace ChefShow.Data
{
    [CreateAssetMenu(menuName = "Chef Show/Judging Config")]
    public sealed class JudgingConfig : ScriptableObject
    {
        public JudgingRecipeProfile[] Profiles;
        [Header("D-005: fixed category maxima")]
        public float Composition = 25, ProcessTaste = 30, Doneness = 20, Preparation = 10, Plating = 10, Cleanliness = 5;
        [Header("Editable proposed category splits")]
        public float MethodMaximum = 15, SeasoningMaximum = 10, CompletionMaximum = 5;
        public float QuantityMaximum = 5, DishwareMaximum = 5;
        [Header("D-005: fixed score caps")]
        public float RawProteinCap = 45, BurnedMainCap = 50;
        [Header("Editable proposed judging parameters")]
        [Range(0, 1)] public float IncompatibleExtraPenalty = .15f;
        [Range(0, 1)] public float ExperimentalIngredientMaximum = .5f;
        [Range(0, 1)] public float MainRoleWeight = .6f, GarnishRoleWeight = .25f, VegetableRoleWeight = .15f;
        public string Validate()
        {
            var maxima = new[] { Composition, ProcessTaste, Doneness, Preparation, Plating, Cleanliness, RawProteinCap, BurnedMainCap };
            var fixedValues = new float[] { 25, 30, 20, 10, 10, 5, 45, 50 };
            if (maxima.Where((value, i) => !JudgingRange.Finite(value) || value != fixedValues[i]).Any())
                return "Веса D-005 должны оставаться 25/30/20/10/10/5, пределы 45/50.";
            if (new[] { MethodMaximum, SeasoningMaximum, CompletionMaximum, QuantityMaximum, DishwareMaximum }
                .Any(value => !JudgingRange.Finite(value) || value < 0)
                || Math.Abs(MethodMaximum + SeasoningMaximum + CompletionMaximum - ProcessTaste) > .0001f
                || Math.Abs(QuantityMaximum + DishwareMaximum - Plating) > .0001f)
                return "Доли процесса должны суммироваться в 30, доли подачи — в 10; отрицательные доли недопустимы.";
            if (!Unit(IncompatibleExtraPenalty) || !Unit(ExperimentalIngredientMaximum)
                || !Unit(MainRoleWeight) || !Unit(GarnishRoleWeight) || !Unit(VegetableRoleWeight)
                || Math.Abs(MainRoleWeight + GarnishRoleWeight + VegetableRoleWeight - 1) > .0001f)
                return "Проверьте штраф добавок, экспериментальный предел и веса ролей с суммой 1.";
            if (Profiles == null || Profiles.Length == 0 || Profiles.Any(p => p == null)) return "Не назначены профили оценки.";
            foreach (var profile in Profiles) { var error = profile.Validate(); if (error != null) return error; }
            if (Profiles.Select(p => p.Reference.Id).Distinct().Count() != Profiles.Length) return "Повтор ID профиля оценки.";
            return null;
        }
        private static bool Unit(float value) => JudgingRange.Finite(value) && value >= 0 && value <= 1;
        public JudgingSettings Capture(RecipeIdentitySettings recognition, CookingSettings cooking)
        {
            var error = Validate(); if (error != null) throw new InvalidOperationException(error);
            if (recognition == null || cooking == null) throw new InvalidOperationException("Оценке нужны захваченные правила рецептов и нагрева.");
            if (Profiles.Length != recognition.Recipes.Count || Profiles.Any(p => !recognition.Recipes.Any(r => r.Id == p.Reference.Id)))
                throw new InvalidOperationException("Профили оценки должны совпадать с каталогом распознавания.");
            return new JudgingSettings(this, recognition, cooking);
        }
    }
}

namespace ChefShow.Judging
{
    public sealed class JudgingRangeSettings
    {
        public double ZeroBelow { get; } public double IdealMinimum { get; }
        public double IdealMaximum { get; } public double ZeroAbove { get; }
        internal JudgingRangeSettings(JudgingRange range)
        { ZeroBelow = range.ZeroBelow; IdealMinimum = range.IdealMinimum; IdealMaximum = range.IdealMaximum; ZeroAbove = range.ZeroAbove; }
        internal JudgingRangeSettings(double zeroBelow, double minimum, double maximum, double zeroAbove)
        { ZeroBelow = zeroBelow; IdealMinimum = minimum; IdealMaximum = maximum; ZeroAbove = zeroAbove; }
        public double Quality(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < ZeroBelow || value >= ZeroAbove) return 0;
            if (value < IdealMinimum) return IdealMinimum == ZeroBelow ? 0 : (value - ZeroBelow) / (IdealMinimum - ZeroBelow);
            if (value <= IdealMaximum) return 1;
            return (ZeroAbove - value) / (ZeroAbove - IdealMaximum);
        }
    }
    public sealed class JudgingCoreSettings
    {
        public RecipeIdentityCore Identity { get; }
        public double ReferenceQuantity { get; }
        public JudgingRole Role { get; }
        public bool MandatoryProtein { get; } public bool MainComponent { get; }
        public JudgingRangeSettings Heat { get; }
        internal JudgingCoreSettings(JudgingCorePortion portion, RecipeIdentityCore identity, CookingSettings cooking)
        {
            Identity = identity; ReferenceQuantity = portion.ReferenceQuantity; Role = portion.Role;
            MandatoryProtein = portion.MandatoryProtein; MainComponent = portion.MainComponent;
            if (portion.UseApplianceHeatDefaults)
            {
                bool oven = identity.AllowedCookers.Count > 0 && identity.AllowedCookers.All(k => k == CookerKind.Oven);
                double ready = oven ? cooking.OvenReady : cooking.Ready;
                double over = oven ? cooking.OvenOvercooked : cooking.Overcooked;
                double burn = oven ? cooking.OvenBurned : cooking.Burned;
                Heat = new JudgingRangeSettings(0, ready, Math.Max(ready, over - .001), burn);
            }
            else Heat = new JudgingRangeSettings(portion.Heat);
        }
    }
    public sealed class JudgingRecipeSettings
    {
        public string RecipeId { get; } public string Name { get; }
        public RecipeIdentityRule Identity { get; }
        public IReadOnlyList<JudgingCoreSettings> Core { get; }
        public JudgingRangeSettings Salt { get; } public JudgingRangeSettings Oil { get; }
        public bool LiquidFood { get; }
        public double IdealFillMinimum { get; } public double IdealFillMaximum { get; } public double ZeroFillAt { get; }
        public double ReferenceQuantity => Core.Sum(c => c.ReferenceQuantity);
        internal JudgingRecipeSettings(JudgingRecipeProfile profile, RecipeIdentityRule identity, CookingSettings cooking)
        {
            if (profile.Core.Length != identity.Core.Count || profile.Core.Any(c => c.RecognitionCoreIndex >= identity.Core.Count))
                throw new InvalidOperationException("Нужен один количественный профиль каждой обязательной группы: " + identity.Id);
            RecipeId = identity.Id; Name = identity.Name; Identity = identity;
            Core = Array.AsReadOnly(profile.Core.OrderBy(c => c.RecognitionCoreIndex)
                .Select(c => new JudgingCoreSettings(c, identity.Core[c.RecognitionCoreIndex], cooking)).ToArray());
            Salt = new JudgingRangeSettings(profile.Salt); Oil = new JudgingRangeSettings(profile.Oil);
            LiquidFood = profile.LiquidFood; IdealFillMinimum = profile.IdealFillMinimum;
            IdealFillMaximum = profile.IdealFillMaximum; ZeroFillAt = profile.ZeroFillAt;
        }
    }
    public sealed class JudgingSettings
    {
        public IReadOnlyList<JudgingRecipeSettings> Profiles { get; }
        public RecipeIdentitySettings Recognition { get; }
        public CookingSettings Cooking { get; }
        public double Composition { get; } public double ProcessTaste { get; } public double Doneness { get; }
        public double Preparation { get; } public double Plating { get; } public double Cleanliness { get; }
        public double MethodMaximum { get; } public double SeasoningMaximum { get; } public double CompletionMaximum { get; }
        public double QuantityMaximum { get; } public double DishwareMaximum { get; }
        public double RawProteinCap { get; } public double BurnedMainCap { get; }
        public double IncompatibleExtraPenalty { get; } public double ExperimentalIngredientMaximum { get; }
        public double MainRoleWeight { get; } public double GarnishRoleWeight { get; } public double VegetableRoleWeight { get; }
        internal JudgingSettings(JudgingConfig config, RecipeIdentitySettings recognition, CookingSettings cooking)
        {
            Recognition = recognition; Cooking = cooking;
            Profiles = Array.AsReadOnly(config.Profiles.Select(p => new JudgingRecipeSettings(p,
                recognition.Recipes.Single(r => r.Id == p.Reference.Id), cooking)).OrderBy(p => p.RecipeId, StringComparer.Ordinal).ToArray());
            Composition = config.Composition; ProcessTaste = config.ProcessTaste; Doneness = config.Doneness;
            Preparation = config.Preparation; Plating = config.Plating; Cleanliness = config.Cleanliness;
            // Validation accepts only tiny float summation differences in the editable splits.
            // Normalize their captured doubles so perfect factors cannot exceed the fixed category maximum.
            double processSplit = (double)config.MethodMaximum + config.SeasoningMaximum + config.CompletionMaximum;
            double platingSplit = (double)config.QuantityMaximum + config.DishwareMaximum;
            MethodMaximum = ProcessTaste * config.MethodMaximum / processSplit;
            SeasoningMaximum = ProcessTaste * config.SeasoningMaximum / processSplit;
            CompletionMaximum = ProcessTaste * config.CompletionMaximum / processSplit;
            QuantityMaximum = Plating * config.QuantityMaximum / platingSplit;
            DishwareMaximum = Plating * config.DishwareMaximum / platingSplit;
            RawProteinCap = config.RawProteinCap; BurnedMainCap = config.BurnedMainCap;
            IncompatibleExtraPenalty = config.IncompatibleExtraPenalty; ExperimentalIngredientMaximum = config.ExperimentalIngredientMaximum;
            MainRoleWeight = config.MainRoleWeight; GarnishRoleWeight = config.GarnishRoleWeight; VegetableRoleWeight = config.VegetableRoleWeight;
        }
        public double RoleWeight(JudgingRole role) => role == JudgingRole.Main ? MainRoleWeight
            : role == JudgingRole.Garnish ? GarnishRoleWeight : VegetableRoleWeight;
    }
}
