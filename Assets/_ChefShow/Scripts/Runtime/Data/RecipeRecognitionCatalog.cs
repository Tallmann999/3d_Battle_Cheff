using System;
using System.Linq;
using ChefShow.Cooking;
using ChefShow.Inventory;
using ChefShow.Recipes;
using UnityEngine;
namespace ChefShow.Data
{
    [CreateAssetMenu(menuName="Chef Show/Recipe Recognition Catalog")]
    public sealed class RecipeRecognitionCatalog : ScriptableObject
    {
        public RecipeRecognitionDefinition[] Recipes;
        public string Validate()
        {
            if(Recipes==null||Recipes.Length!=12||Recipes.Any(r=>r==null))return "Нужны 12 профилей распознавания.";
            foreach(var r in Recipes){var error=r.Validate();if(error!=null)return error;}
            return Recipes.Select(r=>r.Reference.Id).Distinct().Count()!=12?"Повтор ID распознавания.":null;
        }
        public RecipeIdentitySettings Capture()
        {var error=Validate();if(error!=null)throw new InvalidOperationException(error);return new RecipeIdentitySettings(Recipes.Select(r=>r.Capture()));}
    }
}
