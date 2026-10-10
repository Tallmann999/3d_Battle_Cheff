using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace ChefShow.Data
{
    public enum RecipeSymbol { Potato, Onion, Beef, Carrot, Egg, Cheese, Flour, Butter, Sugar, Apple, Salt, Oil, Knife, Mix, Pan, Pot, Oven, Braise, Dish, Arrow }
    [Serializable] public sealed class RecipeIngredient
    {
        public IngredientDefinition Ingredient;
        [Min(1), Tooltip("Proposed reference amount, not approved judging balance.")] public int Amount=1;
    }
    [Serializable] public sealed class RecipeStep
    {
        public RecipeSymbol Symbol;
        [Tooltip("Short ingredient grouping, not a paragraph of instructions.")] public string Caption;
        [Tooltip("This process is only documented; gameplay is not implemented yet.")] public bool FutureProcess;
    }
    [CreateAssetMenu(menuName="Chef Show/Recipe Reference Profile")]
    public sealed class RecipeDefinition : ScriptableObject
    {
        public string Id;
        public string DisplayName;
        [Tooltip("These quantities remain proposals until balancing is approved.")] public bool QuantitiesProposed=true;
        public RecipeIngredient[] Ingredients;
        public RecipeStep[] Steps;
        [TextArea] public string ProcessNote;
        public string Validate()
        {
            if(string.IsNullOrWhiteSpace(Id)||string.IsNullOrWhiteSpace(DisplayName))return "Рецепту нужны ID и название.";
            if(Ingredients==null||Ingredients.Length<1||Ingredients.Length>7||Ingredients.Any(x=>x==null||x.Ingredient==null||x.Amount<1)||Ingredients.Select(x=>x.Ingredient.Id).Distinct().Count()!=Ingredients.Length)return "Проверьте продукты и количества: "+Id;
            if(Steps==null||Steps.Length<1||Steps.Length>5||Steps.Any(x=>x==null||!Enum.IsDefined(typeof(RecipeSymbol),x.Symbol)))return "Проверьте этапы рецепта: "+Id;
            return null;
        }
        public RecipePage Capture()
        {
            var error=Validate();if(error!=null)throw new InvalidOperationException(error);
            return new RecipePage(this);
        }
    }
    public sealed class RecipeIngredientPage
    {
        public string Id {get;} public string Name {get;} public int Amount {get;} public bool Dose {get;}
        internal RecipeIngredientPage(RecipeIngredient x){Id=x.Ingredient.Id;Name=x.Ingredient.DisplayName;Amount=x.Amount;Dose=x.Ingredient.IsDoseContainer;}
    }
    public sealed class RecipeStepPage
    {
        public RecipeSymbol Symbol {get;} public string Caption {get;} public bool FutureProcess {get;}
        internal RecipeStepPage(RecipeStep x){Symbol=x.Symbol;Caption=x.Caption;FutureProcess=x.FutureProcess;}
    }
    public sealed class RecipePage
    {
        public string Id {get;} public string Title {get;} public bool Proposed {get;} public string Note {get;}
        public IReadOnlyList<RecipeIngredientPage> Ingredients {get;}
        public IReadOnlyList<RecipeStepPage> Steps {get;}
        internal RecipePage(RecipeDefinition d)
        {
            Id=d.Id;Title=d.DisplayName;Proposed=d.QuantitiesProposed;Note=d.ProcessNote;
            Ingredients=Array.AsReadOnly(d.Ingredients.Select(x=>new RecipeIngredientPage(x)).ToArray());
            Steps=Array.AsReadOnly(d.Steps.Select(x=>new RecipeStepPage(x)).ToArray());
        }
    }
}
