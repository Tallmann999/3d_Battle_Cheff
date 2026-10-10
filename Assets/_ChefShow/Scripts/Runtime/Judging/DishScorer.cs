using System;
using System.Collections.Generic;
using System.Linq;
using ChefShow.Cooking;
using ChefShow.Data;
using ChefShow.Inventory;
using ChefShow.Recipes;

namespace ChefShow.Judging
{
    // Pure calculation: reads only immutable snapshots and captured settings.
    public static class DishScorer
    {
        private sealed class Evidence
        {
            public FoodPortionSnapshot Leaf;
            public FoodPortionSnapshot[] Chain;
            public long Quantity => Leaf.Quantity;
            public FoodOperation[] HeatFacts => Chain.SelectMany(OwnHeat).ToArray();
            public bool Burned => Chain.Any(p => p.Cooking == CookState.Burned
                || p.Operations.Any(o => o.SourcePortionId == p.Id && o.Action == "food_burned"));
            public FoodPortionSnapshot Heated
            {
                get
                {
                    var heated = Chain.Where(p => OwnHeat(p).Any()).ToArray();
                    // Reheating a cooked ingredient does not make it raw again. A cooked outer mixture
                    // supplies doneness to previously cold inputs; burned inputs always remain burned.
                    return heated.LastOrDefault(p => p.Cooking == CookState.Cooked || p.Cooking == CookState.Overcooked)
                        ?? heated.LastOrDefault() ?? Leaf;
                }
            }
            public bool Ready => !Burned && (Heated.Cooking == CookState.Cooked || Heated.Cooking == CookState.Overcooked);
        }
        private static IEnumerable<FoodOperation> OwnHeat(FoodPortionSnapshot food)
            => food.Operations.Where(o => o.Action == "cook_started" && o.SourcePortionId == food.Id && o.Cooker.HasValue);
        private static void Flatten(FoodPortionSnapshot food, List<FoodPortionSnapshot> ancestors, List<Evidence> leaves)
        {
            if (food.Components.Count == 0)
            {
                leaves.Add(new Evidence { Leaf = food, Chain = new[] { food }.Concat(ancestors.AsEnumerable().Reverse()).ToArray() });
                return;
            }
            ancestors.Add(food);
            foreach (var component in food.Components) Flatten(component, ancestors, leaves);
            ancestors.RemoveAt(ancestors.Count - 1);
        }
        private static Evidence[] Matches(JudgingCoreSettings core, IEnumerable<Evidence> leaves)
            => leaves.Where(e => core.Identity.Alternatives.Contains(e.Leaf.IngredientId)).OrderBy(e => e.Leaf.Id, StringComparer.Ordinal).ToArray();
        private static double Clamp(double value) => double.IsNaN(value) ? 0 : Math.Max(0, Math.Min(1, value));
        private static double Average(Evidence[] leaves, Func<Evidence, double> measure)
        {
            double amount = leaves.Sum(e => (double)e.Quantity);
            return amount <= 0 ? 0 : Clamp(leaves.Sum(e => e.Quantity * Clamp(measure(e))) / amount);
        }
        private static double Preparation(JudgingCoreSettings core, Evidence evidence)
        {
            var requirement = core.Identity.Preparation;
            if (requirement == RecipePreparation.Any) return 1;
            if (requirement == RecipePreparation.Whole)
                return evidence.Leaf.Preparation == PreparationState.Whole && evidence.Leaf.ChopPresses == 0 ? 1 : 0;
            var own = evidence.Leaf.Operations.ToArray();
            int chopped = Array.FindIndex(own, o => o.SourcePortionId == evidence.Leaf.Id && o.Preparation == PreparationState.Chopped);
            int heat = Array.FindIndex(own, o => o.SourcePortionId == evidence.Leaf.Id && o.Action == "cook_started" && o.Cooker.HasValue);
            if (heat >= 0 && (chopped < 0 || heat < chopped)) return 0;
            if (chopped >= 0 && evidence.HeatFacts.Length > 0 && own[chopped].SimulationTime > evidence.HeatFacts[0].SimulationTime) return 0;
            return Clamp(evidence.Leaf.ChopPresses / (double)InventoryState.RequiredChopPresses);
        }
        private static double Method(JudgingCoreSettings core, Evidence evidence, JudgingRecipeSettings profile, List<Evidence> all)
        {
            if (!string.IsNullOrEmpty(profile.Identity.UnavailableProcess)) return 0;
            var methods = evidence.HeatFacts;
            if (core.Identity.AllowedCookers.Count > 0)
                return methods.Length == 0 ? 0 : methods.Count(o => core.Identity.AllowedCookers.Contains(o.Cooker.Value)) / (double)methods.Length;
            if (string.IsNullOrEmpty(core.Identity.AddedAfterIngredient)) return 1;
            if (methods.Length != 0) return 0;
            var main = profile.Core.FirstOrDefault(c => c.Identity.Alternatives.Contains(core.Identity.AddedAfterIngredient)
                && string.IsNullOrEmpty(c.Identity.AddedAfterIngredient));
            if (main == null) return 0;
            var ready = Matches(main, all).Where(e => Method(main, e, profile, all) >= 1)
                .SelectMany(e => e.Chain).SelectMany(p => p.Operations.Where(o => o.SourcePortionId == p.Id
                    && o.Cooker.HasValue && (o.Cooking == CookState.Cooked || o.Cooking == CookState.Overcooked || o.Cooking == CookState.Burned)))
                .Select(o => o.SimulationTime).ToArray();
            var plated = evidence.Leaf.Operations.Where(o => o.SourcePortionId == evidence.Leaf.Id && o.Action == "food_plated").ToArray();
            return ready.Length > 0 && plated.Length > 0 && plated.Last().SimulationTime >= ready.Min() ? 1 : 0;
        }
        private static double Doneness(JudgingCoreSettings core, Evidence evidence)
        {
            if (evidence.Burned) return 0;
            if (core.Identity.AllowedCookers.Count == 0) return 1;
            if (evidence.HeatFacts.Length == 0) return 0;
            return core.Heat.Quality(evidence.Heated.HeatProgress);
        }
        private static double MixedTogether(IEnumerable<JudgingCoreSettings> group, List<Evidence> leaves)
        {
            var cores = group.ToArray();
            HashSet<string> common = null;
            foreach (var core in cores)
            {
                var mixtures = new HashSet<string>(Matches(core, leaves).SelectMany(e => e.Chain.Skip(1))
                    .Where(p =>
                    {
                        if (p.Preparation != PreparationState.Mixed) return false;
                        var own = p.Operations.Where(o => o.SourcePortionId == p.Id).ToArray();
                        int completed = Array.FindIndex(own, o => o.Action == "mixture_completed");
                        return completed >= 0 && own.Skip(completed + 1).Any(o => o.Action == "cook_started"
                            && o.Cooker.HasValue && core.Identity.AllowedCookers.Contains(o.Cooker.Value));
                    })
                    .Select(p => p.Id));
                if (common == null) common = mixtures; else common.IntersectWith(mixtures);
            }
            if (common == null || common.Count == 0) return 0;
            // A valid mixture proves the required group, but does not excuse cold or unmixed
            // repetitions elsewhere on the plate. Every actual matching leaf retains its quantity.
            var actual = cores.SelectMany(core => Matches(core, leaves))
                .GroupBy(e => e.Leaf.Id, StringComparer.Ordinal).Select(g => g.First()).ToArray();
            return Average(actual, evidence => evidence.Chain.Skip(1).Any(mixture => common.Contains(mixture.Id)) ? 1 : 0);
        }
        private static double Completion(JudgingRecipeSettings profile, List<Evidence> leaves, JudgingSettings settings, double method, double coverage)
        {
            if (!string.IsNullOrEmpty(profile.Identity.UnavailableProcess)) return 0;
            var operations = new List<double>();
            foreach (var group in profile.Core.Where(c => c.Identity.MixedGroup > 0).GroupBy(c => c.Identity.MixedGroup))
                operations.Add(MixedTogether(group, leaves));
            foreach (var core in profile.Core.Where(c => c.Identity.AllowedCookers.Contains(CookerKind.Pot)))
            {
                if (settings.Cooking.PotStirs == 0) continue;
                var actual = Matches(core, leaves);
                bool potOnly = core.Identity.AllowedCookers.All(kind => kind == CookerKind.Pot);
                // An alternative Pan/Pot profile must not require Pot stirs for a valid Pan route.
                if (!potOnly && !actual.Any(e => e.Chain.Any(p => OwnHeat(p).Any(h => h.Cooker == CookerKind.Pot)))) continue;
                operations.Add(Average(actual, e =>
                {
                    var pot = e.Chain.LastOrDefault(p => p.LastCooker == CookerKind.Pot || OwnHeat(p).Any(h => h.Cooker == CookerKind.Pot));
                    return pot == null ? (potOnly ? 0 : 1) : Clamp(pot.StirPresses / (double)settings.Cooking.PotStirs);
                }));
            }
            return operations.Count == 0 ? (method >= 1 && coverage >= 1 ? 1 : 0) : operations.Average();
        }
        private static double RoleDoneness(JudgingRecipeSettings profile, List<Evidence> leaves, JudgingSettings settings)
        {
            double points = 0, weights = 0;
            foreach (var group in profile.Core.GroupBy(c => c.Role))
            {
                double roleWeight = settings.RoleWeight(group.Key);
                double reference = group.Sum(c => c.ReferenceQuantity);
                // Every profile core is mandatory. Missing cores keep their reference weight and score zero.
                double quality = group.Sum(c => c.ReferenceQuantity * Average(Matches(c, leaves), e => Doneness(c, e))) / reference;
                points += roleWeight * quality; weights += roleWeight;
            }
            return weights == 0 ? 0 : Clamp(points / weights);
        }
        private static double Quantity(JudgingRecipeSettings profile, List<Evidence> leaves)
        {
            var present = profile.Core.Where(core => Matches(core, leaves).Length > 0).ToArray();
            // Missing ingredient groups are penalized only by composition. This category measures
            // the amounts of groups that actually exist, including repetitions and alternatives.
            return present.Length == 0 ? 1 : present.Average(core =>
            {
                double actual = Matches(core, leaves).Sum(e => (double)e.Quantity);
                return actual <= 0 ? 0 : Math.Min(actual / core.ReferenceQuantity, core.ReferenceQuantity / actual);
            });
        }
        private static double Dishware(DishSnapshot dish, JudgingRecipeSettings profile, double quantity)
        {
            var dishware = dish.Dishware;
            if (dishware == null || dishware.NominalCapacity <= 0 || (profile.LiquidFood && !dishware.SupportsLiquid)) return 0;
            double fill = quantity / dishware.NominalCapacity;
            if (fill < profile.IdealFillMinimum) return profile.IdealFillMinimum == 0 ? 1 : Clamp(fill / profile.IdealFillMinimum);
            if (fill <= profile.IdealFillMaximum) return 1;
            return Clamp((profile.ZeroFillAt - fill) / (profile.ZeroFillAt - profile.IdealFillMaximum));
        }
        private static ScoreCategory[] Categories(JudgingSettings settings, double ingredients, double process, double heat,
            double preparation, double plating, double cleanliness)
        {
            return new[]
            {
                new ScoreCategory(ScoreCategoryId.Composition, "Состав", ingredients, settings.Composition),
                new ScoreCategory(ScoreCategoryId.ProcessTaste, "Приготовление и вкус", process, settings.ProcessTaste),
                new ScoreCategory(ScoreCategoryId.Doneness, "Готовность", heat, settings.Doneness),
                new ScoreCategory(ScoreCategoryId.Preparation, "Подготовка", preparation, settings.Preparation),
                new ScoreCategory(ScoreCategoryId.Plating, "Подача", plating, settings.Plating),
                new ScoreCategory(ScoreCategoryId.Cleanliness, "Чистота и завершённость", cleanliness, settings.Cleanliness)
            };
        }
        public static ScoreBreakdown Evaluate(DishSnapshot dish, JudgingSettings settings, string assignedRecipeId = null)
        {
            if (dish == null || settings == null) throw new ArgumentNullException(dish == null ? nameof(dish) : nameof(settings));
            var leaves = new List<Evidence>();
            foreach (var portion in dish.Portions) Flatten(portion, new List<FoodPortionSnapshot>(), leaves);
            var identity = dish.Recognition ?? settings.Recognition.Resolve(dish);
            if (leaves.Count == 0)
                return new ScoreBreakdown("no_dish", "Нет блюда", "no_dish", "Нет блюда", false,
                    Categories(settings, 0, 0, 0, 0, 0, 0), new[] { "На тарелке нет еды: 0 баллов." }, 0);
            bool assigned = !string.IsNullOrEmpty(assignedRecipeId);
            string target = assigned ? assignedRecipeId : identity.Recognized ? identity.RecipeId : identity.SuggestedRecipeId;
            var profile = settings.Profiles.FirstOrDefault(p => p.RecipeId == target);
            if (profile == null)
            {
                if (assigned) throw new ArgumentException("Неизвестный рецепт задания: " + assignedRecipeId, nameof(assignedRecipeId));
                profile = settings.Profiles.First();
            }
            var reasons = new List<string>();
            double cap = 100;
            foreach (var core in profile.Core)
            {
                var actual = Matches(core, leaves);
                if (core.MandatoryProtein && actual.Any(e => !e.Ready && !e.Burned))
                { cap = Math.Min(cap, settings.RawProteinCap); reasons.Add("Сырой обязательный белковый продукт: предел 45."); }
                if (core.MainComponent && actual.Any(e => e.Burned))
                { cap = Math.Min(cap, settings.BurnedMainCap); reasons.Add("Сгоревший главный продукт: предел 50."); }
            }
            double coverage = profile.Core.Count(c => Matches(c, leaves).Length > 0) / (double)profile.Core.Count;
            var allowed = profile.Identity.Core.SelectMany(c => c.Alternatives).Concat(profile.Identity.Optional).ToArray();
            var extras = leaves.Select(e => e.Leaf.IngredientId).Distinct().Where(id => !allowed.Contains(id)).OrderBy(id => id, StringComparer.Ordinal).ToArray();
            double ingredientFit = Clamp(coverage - settings.IncompatibleExtraPenalty * extras.Length);
            if (!assigned && !identity.Recognized) ingredientFit = Math.Min(ingredientFit, settings.ExperimentalIngredientMaximum);
            if (coverage < 1) reasons.Add("Не хватает обязательных продуктов рецепта.");
            if (extras.Length > 0) reasons.Add("Есть добавки вне рецепта: состав снижен.");
            if (!assigned && !identity.Recognized) reasons.Add("Экспериментальное блюдо: состав ограничен эталонным профилем.");
            if (assigned && identity.RecipeId != target) reasons.Add("Оценка по выданному заданию: " + profile.Name + ".");
            double method = profile.Core.Average(c => Average(Matches(c, leaves), e => Method(c, e, profile, leaves)));
            double preparation = profile.Core.Average(c => Average(Matches(c, leaves), e => Preparation(c, e)));
            double completion = Completion(profile, leaves, settings, method, coverage);
            double totalQuantity = leaves.Sum(e => (double)e.Quantity);
            // Roots already contain all inherited doses of nested mixtures. Never add Components again.
            double salt = dish.SaltDoses + dish.Portions.Sum(p => (double)p.SaltDoses);
            double oil = dish.OilDoses + dish.Portions.Sum(p => (double)p.OilDoses);
            double seasoning = (profile.Salt.Quality(salt / totalQuantity) + profile.Oil.Quality(oil / totalQuantity)) / 2;
            double doneness = RoleDoneness(profile, leaves, settings);
            double quantity = Quantity(profile, leaves);
            double dishware = Dishware(dish, profile, totalQuantity);
            if (method < 1) reasons.Add("Неподходящий или незавершённый способ приготовления.");
            if (preparation < 1) reasons.Add("Подготовка продукта не соответствует рецепту.");
            if (completion < 1) reasons.Add("Не завершено требуемое смешивание или перемешивание.");
            if (profile.Salt.Quality(salt / totalQuantity) < 1) reasons.Add(salt / totalQuantity > profile.Salt.IdealMaximum ? "Избыток соли." : "Недостаток соли.");
            if (profile.Oil.Quality(oil / totalQuantity) < 1) reasons.Add(oil / totalQuantity > profile.Oil.IdealMaximum ? "Избыток масла." : "Недостаток масла.");
            if (doneness < 1) reasons.Add("Не все обязательные компоненты попали в нужную готовность.");
            if (quantity < 1) reasons.Add("Количество продуктов отличается от эталонной порции.");
            if (dishware < 1) reasons.Add(profile.LiquidFood && (dish.Dishware == null || !dish.Dishware.SupportsLiquid)
                ? "Для этой консистенции нужна глубокая посуда." : "Выбранная посуда не соответствует объёму еды.");
            double plating = Math.Max(0, settings.QuantityMaximum * quantity + settings.DishwareMaximum * dishware - dish.PresentationPenalty);
            if (dish.PresentationPenalty > 0) reasons.Add("Презентабельность: −" + dish.PresentationPenalty + " из подачи один раз.");
            bool dirty = dish.Contaminated || leaves.Any(e => e.Chain.Any(p => p.Contaminated));
            if (dirty) reasons.Add("Загрязнение еды снижает гигиену.");
            // No edge-cleaning state exists yet. Edge quality is an explicit provisional baseline 2.
            double cleanliness = 2 + (dirty ? 0 : 2) + (dish.IsSubmitted ? 1 : 0);
            if (!dish.IsSubmitted) reasons.Add("Блюдо ещё не подано.");
            if (reasons.Count == 0) reasons.Add("Хороший состав, приготовление и подача.");
            return new ScoreBreakdown(identity.RecipeId, identity.Name, profile.RecipeId, profile.Name, !identity.Recognized,
                Categories(settings, settings.Composition * ingredientFit,
                    settings.MethodMaximum * method + settings.SeasoningMaximum * seasoning + settings.CompletionMaximum * completion,
                    settings.Doneness * doneness, settings.Preparation * preparation, plating, cleanliness), reasons, cap);
        }
    }
}
