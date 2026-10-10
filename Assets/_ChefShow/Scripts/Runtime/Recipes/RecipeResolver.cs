using System;
using System.Collections.Generic;
using System.Linq;
using ChefShow.Cooking;
using ChefShow.Data;
using ChefShow.Inventory;
namespace ChefShow.Recipes
{
    public sealed class RecipeIdentityCore
    {
        public IReadOnlyList<string> Alternatives {get;}
        public IReadOnlyList<string> AlternativeNames {get;}
        public string IngredientName(string id) {int i=Alternatives.ToList().IndexOf(id);return i>=0?AlternativeNames[i]:id;}
        public RecipePreparation Preparation {get;}
        public IReadOnlyList<CookerKind> AllowedCookers {get;}
        public int MixedGroup {get;}
        public string AddedAfterIngredient {get;}
        internal RecipeIdentityCore(RecipeCoreRule r)
        {Alternatives=Array.AsReadOnly(r.Alternatives.Select(a=>a.Id).ToArray());AlternativeNames=Array.AsReadOnly(r.Alternatives.Select(a=>a.DisplayName).ToArray());Preparation=r.Preparation;AllowedCookers=Array.AsReadOnly(r.AllowedCookers.ToArray());MixedGroup=r.MixedGroup;AddedAfterIngredient=r.AddedAfterIngredient;}
    }
    public sealed class RecipeIdentityRule
    {
        public string Id {get;} public string Name {get;} public string UnavailableProcess {get;}
        public IReadOnlyList<RecipeIdentityCore> Core {get;} public IReadOnlyList<string> Optional {get;}
        public RecipeIdentityRule(RecipeRecognitionDefinition d)
        {Id=d.Reference.Id;Name=d.Reference.DisplayName;UnavailableProcess=d.UnavailableProcess;Core=Array.AsReadOnly(d.Core.Select(r=>new RecipeIdentityCore(r)).ToArray());Optional=Array.AsReadOnly(d.Optional.Select(i=>i.Id).ToArray());}
    }
    public readonly struct DishIngredientAmount
    {
        public readonly string Id; public readonly long Amount;
        public DishIngredientAmount(string id,long amount){Id=id;Amount=amount;}
    }
    public sealed class DishRecognition
    {
        public string RecipeId {get;} public string Name {get;} public string SuggestedRecipeId {get;} public string Reason {get;}
        public bool Recognized=>RecipeId!="no_dish" && RecipeId!="experimental";
        public IReadOnlyList<DishIngredientAmount> Ingredients {get;}
        public IReadOnlyList<string> Issues {get;}
        internal DishRecognition(string id,string name,string suggestion,string reason,IEnumerable<DishIngredientAmount> ingredients,IEnumerable<string> issues)
        {RecipeId=id;Name=name;SuggestedRecipeId=suggestion;Reason=reason;Ingredients=Array.AsReadOnly(ingredients.ToArray());Issues=Array.AsReadOnly(issues.Distinct().ToArray());}
    }
    public sealed class RecipeIdentitySettings
    {
        public IReadOnlyList<RecipeIdentityRule> Recipes {get;}
        public RecipeIdentitySettings(IEnumerable<RecipeIdentityRule> recipes)
        {var copy=recipes.ToArray();if(copy.Select(r=>r.Id).Distinct().Count()!=copy.Length)throw new ArgumentException("Duplicate recipe IDs");Recipes=Array.AsReadOnly(copy);}
        private sealed class Evidence
        {
            public FoodPortionSnapshot Leaf;
            public FoodPortionSnapshot[] Chain;
            public FoodOperation[] HeatFacts=>Chain.SelectMany(OwnHeat).ToArray();
            public CookerKind[] Methods=>HeatFacts.Select(o=>o.Cooker.Value).ToArray();
        }
        private sealed class Candidate
        {
            public RecipeIdentityRule Rule;public int Present,Extra;public string Failure;
            public bool Valid=>Failure==null;
        }
        private static IEnumerable<FoodOperation> OwnHeat(FoodPortionSnapshot food)
            =>food.Operations.Where(o=>o.Action=="cook_started" && o.Cooker.HasValue && o.SourcePortionId==food.Id);
        private static void Flatten(FoodPortionSnapshot food,List<FoodPortionSnapshot> ancestors,List<Evidence> list)
        {
            if(food.Components.Count==0){list.Add(new Evidence{Leaf=food,Chain=new[]{food}.Concat(ancestors.AsEnumerable().Reverse()).ToArray()});return;}
            ancestors.Add(food);foreach(var child in food.Components)Flatten(child,ancestors,list);ancestors.RemoveAt(ancestors.Count-1);
        }
        private static string PreparationFailure(RecipeIdentityCore rule,Evidence food)
        {
            if(rule.Preparation==RecipePreparation.Whole && (food.Leaf.Preparation!=PreparationState.Whole || food.Leaf.ChopPresses!=0))return "Нужен целый продукт: "+rule.IngredientName(food.Leaf.IngredientId);
            if(rule.Preparation==RecipePreparation.Chopped && food.Leaf.Preparation!=PreparationState.Chopped)return "Нужна нарезка: "+rule.IngredientName(food.Leaf.IngredientId);
            if(rule.Preparation==RecipePreparation.Chopped)
            {
                var chopped=food.Leaf.Operations.Where(o=>o.SourcePortionId==food.Leaf.Id && o.Preparation==PreparationState.Chopped).ToArray();
                var heat=food.HeatFacts;
                int chopIndex=food.Leaf.Operations.ToList().FindIndex(o=>o.SourcePortionId==food.Leaf.Id && o.Preparation==PreparationState.Chopped);
                int heatIndex=food.Leaf.Operations.ToList().FindIndex(o=>o.SourcePortionId==food.Leaf.Id && o.Action=="cook_started" && o.Cooker.HasValue);
                if(chopped.Length==0 || (heatIndex>=0 && heatIndex<chopIndex) || (heat.Length>0 && chopped[0].SimulationTime>heat[0].SimulationTime))return "Нарезать нужно до нагрева: "+rule.IngredientName(food.Leaf.IngredientId);
            }
            return null;
        }
        private static string MethodFailure(RecipeIdentityCore rule,Evidence food,List<Evidence> all,RecipeIdentityRule recipe)
        {
            var preparation=PreparationFailure(rule,food);if(preparation!=null)return preparation;
            if(rule.MixedGroup==0 && food.Chain.Length>1)return "Здесь нужен отдельный продукт, а не смесь.";
            if(rule.MixedGroup>0 && food.Chain.Length==1)return "Нужна настоящая смесь в миске.";
            if(rule.AllowedCookers.Count>0)
            {
                var methods=food.Methods;
                if(methods.Length==0)return "Нужен нагрев: "+string.Join(" / ",rule.AllowedCookers.Select(MethodName));
                if(methods.Any(m=>!rule.AllowedCookers.Contains(m)))return "Неподходящий способ: "+rule.IngredientName(food.Leaf.IngredientId);
                if(rule.MixedGroup>0 && !food.Chain.Skip(1).Any(m=>OwnHeat(m).Any(h=>rule.AllowedCookers.Contains(h.Cooker.Value))))return "Смесь нужно приготовить после смешивания.";
            }
            if(!string.IsNullOrEmpty(rule.AddedAfterIngredient))
            {
                if(food.Methods.Length>0)return "Добавку внесите после нагрева.";
                var plated=food.Leaf.Operations.Where(o=>o.SourcePortionId==food.Leaf.Id && o.Action=="food_plated").ToArray();
                var baseRule=recipe.Core.FirstOrDefault(c=>c.Alternatives.Contains(rule.AddedAfterIngredient)&&string.IsNullOrEmpty(c.AddedAfterIngredient));
                var ready=all.Where(e=>e.Leaf.IngredientId==rule.AddedAfterIngredient&&baseRule!=null&&MethodFailure(baseRule,e,all,recipe)==null).SelectMany(e=>e.Chain).SelectMany(f=>f.Operations.Where(o=>o.SourcePortionId==f.Id&&o.Cooker.HasValue&&(o.Cooking==CookState.Cooked||o.Cooking==CookState.Overcooked||o.Cooking==CookState.Burned))).ToArray();
                if(plated.Length==0||ready.Length==0||plated.Last().SimulationTime<ready.Min(o=>o.SimulationTime))return "Добавку внесите после готовности основного продукта.";
            }
            return null;
        }
        public static string MethodName(CookerKind kind)=>kind==CookerKind.Pan?"сковорода":kind==CookerKind.Pot?"кастрюля":"духовка";
        private static Candidate Check(RecipeIdentityRule recipe,List<Evidence> food)
        {
            var result=new Candidate{Rule=recipe};var matches=new List<Evidence[]>();
            foreach(var core in recipe.Core)
            {
                var present=food.Where(e=>core.Alternatives.Contains(e.Leaf.IngredientId)).OrderBy(e=>e.Leaf.Id,StringComparer.Ordinal).ToArray();
                if(present.Length>0)result.Present++;
                var valid=present.Where(e=>MethodFailure(core,e,food,recipe)==null).ToArray();matches.Add(valid);
                if(valid.Length==0 && result.Failure==null)result.Failure=present.Length==0?"Не хватает: "+string.Join(" / ",core.AlternativeNames):MethodFailure(core,present[0],food,recipe);
            }
            foreach(int group in recipe.Core.Select(c=>c.MixedGroup).Where(g=>g>0).Distinct())
            {
                HashSet<string> common=null;
                for(int i=0;i<recipe.Core.Count;i++)if(recipe.Core[i].MixedGroup==group)
                {
                    var candidates=new HashSet<string>(matches[i].SelectMany(e=>e.Chain.Skip(1)).Where(m=>OwnHeat(m).Any(h=>recipe.Core[i].AllowedCookers.Contains(h.Cooker.Value))).Select(m=>m.Id));
                    if(common==null)common=candidates;else common.IntersectWith(candidates);
                }
                if(common==null||common.Count==0)result.Failure=result.Failure??"Обязательные продукты нужно смешать вместе до нагрева.";
            }
            if(!string.IsNullOrEmpty(recipe.UnavailableProcess))result.Failure=recipe.UnavailableProcess;
            var accepted=recipe.Core.SelectMany(c=>c.Alternatives).Concat(recipe.Optional).ToArray();
            result.Extra=food.Select(e=>e.Leaf.IngredientId).Distinct().Count(id=>!accepted.Contains(id));return result;
        }
        public DishRecognition Resolve(DishSnapshot dish)
        {
            var food=new List<Evidence>();foreach(var portion in dish.Portions)Flatten(portion,new List<FoodPortionSnapshot>(),food);
            var ingredients=food.GroupBy(e=>e.Leaf.IngredientId).OrderBy(g=>g.Key,StringComparer.Ordinal).Select(g=>new DishIngredientAmount(g.Key,g.Sum(e=>(long)e.Leaf.Quantity))).ToArray();
            if(food.Count==0)return new DishRecognition("no_dish","Нет блюда",null,null,ingredients,Array.Empty<string>());
            var issues=new List<string>();
            if(dish.Contaminated||food.Any(e=>e.Chain.Any(p=>p.Contaminated)))issues.Add("Есть загрязнение");
            if(food.Any(e=>e.Chain.Any(p=>p.Cooking==CookState.Burned)))issues.Add("Есть сгоревшие продукты");
            if(food.Any(e=>e.Chain.Any(p=>p.Cooking==CookState.Overcooked)))issues.Add("Есть пережаренные продукты");
            var candidates=Recipes.Select(r=>Check(r,food)).ToArray();
            var chosen=candidates.Where(c=>c.Valid).OrderBy(c=>c.Extra).ThenBy(c=>c.Rule.Id,StringComparer.Ordinal).FirstOrDefault();
            if(food.Any(e=>!e.Chain.Any(p=>p.Cooking==CookState.Cooked||p.Cooking==CookState.Overcooked||p.Cooking==CookState.Burned)
                && !(chosen!=null&&chosen.Rule.Core.Any(c=>c.Alternatives.Contains(e.Leaf.IngredientId)&&!string.IsNullOrEmpty(c.AddedAfterIngredient)&&MethodFailure(c,e,food,chosen.Rule)==null))))issues.Add("Есть неготовые продукты");
            if(chosen!=null)
            {
                if(chosen.Extra>0)issues.Add("Есть добавки вне рецепта");
                return new DishRecognition(chosen.Rule.Id,chosen.Rule.Name,null,null,ingredients,issues);
            }
            var suggestion=candidates.OrderByDescending(c=>(float)c.Present/c.Rule.Core.Count).ThenBy(c=>c.Extra).ThenBy(c=>c.Rule.Id,StringComparer.Ordinal).FirstOrDefault();
            return new DishRecognition("experimental","Экспериментальное блюдо",suggestion?.Rule.Id,suggestion==null?"Нет настроенного рецепта.":suggestion.Rule.Name+": "+suggestion.Failure,ingredients,issues);
        }
    }
}
