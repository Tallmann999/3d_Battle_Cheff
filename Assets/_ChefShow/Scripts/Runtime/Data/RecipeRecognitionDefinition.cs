using System;
using System.Linq;
using ChefShow.Cooking;
using ChefShow.Inventory;
using ChefShow.Recipes;
using UnityEngine;
namespace ChefShow.Data
{
    public enum RecipePreparation { Any, Whole, Chopped }
    [Serializable] public sealed class RecipeCoreRule
    {
        public IngredientDefinition[] Alternatives;
        public RecipePreparation Preparation;
        public CookerKind[] AllowedCookers=Array.Empty<CookerKind>();
        [Min(0),Tooltip("0: unmixed ingredient; same positive group: must share an actually heated mixture.")] public int MixedGroup;
        [Tooltip("An unheated addition plated after this core ingredient finished heating.")] public string AddedAfterIngredient;
    }
    [CreateAssetMenu(menuName="Chef Show/Recipe Recognition Profile")]
    public sealed class RecipeRecognitionDefinition : ScriptableObject
    {
        public RecipeDefinition Reference;
        public RecipeCoreRule[] Core;
        public IngredientDefinition[] Optional=Array.Empty<IngredientDefinition>();
        [TextArea] public string UnavailableProcess;
        public string Validate()
        {
            if(Reference==null||Reference.Validate()!=null)return "Не назначен справочный рецепт.";
            if(Core==null||Core.Length==0||Core.Any(r=>r==null||r.Alternatives==null||r.Alternatives.Length==0||r.Alternatives.Any(a=>a==null||string.IsNullOrWhiteSpace(a.Id)||a.IsDoseContainer)||r.Alternatives.Select(a=>a.Id).Distinct().Count()!=r.Alternatives.Length||!Enum.IsDefined(typeof(RecipePreparation),r.Preparation)||r.MixedGroup<0||r.AllowedCookers==null||(r.MixedGroup>0&&r.AllowedCookers.Length==0)||r.AllowedCookers.Any(k=>!Enum.IsDefined(typeof(CookerKind),k))))return "Неверное ядро распознавания: "+Reference.Id;
            if(Core.Any(r=>!string.IsNullOrEmpty(r.AddedAfterIngredient)&&!Core.Any(c=>c!=r&&c.Alternatives.Any(a=>a.Id==r.AddedAfterIngredient)&&string.IsNullOrEmpty(c.AddedAfterIngredient))))return "Добавке нужен основной продукт: "+Reference.Id;
            if(Optional==null||Optional.Any(a=>a==null||string.IsNullOrWhiteSpace(a.Id)))return "Пустая опциональная добавка: "+Reference.Id;
            return null;
        }
        public RecipeIdentityRule Capture()
        {var error=Validate();if(error!=null)throw new InvalidOperationException(error);return new RecipeIdentityRule(this);}
    }
}
