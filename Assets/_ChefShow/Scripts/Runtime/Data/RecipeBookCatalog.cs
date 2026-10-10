using System;
using System.Linq;
using UnityEngine;
namespace ChefShow.Data
{
    [CreateAssetMenu(menuName="Chef Show/Recipe Book Catalog")]
    public sealed class RecipeBookCatalog : ScriptableObject
    {
        public RecipeDefinition[] Recipes;
        public string Validate()
        {
            if(Recipes==null||Recipes.Length!=12||Recipes.Any(r=>r==null))return "В книге должны быть 12 рецептов.";
            if(Recipes.Select(r=>r.Id).Distinct().Count()!=12)return "IDs рецептов должны быть уникальными.";
            return Recipes.Select(r=>r.Validate()).FirstOrDefault(e=>e!=null);
        }
        public RecipePage[] Capture()
        {var error=Validate();if(error!=null)throw new InvalidOperationException(error);return Recipes.Select(r=>r.Capture()).ToArray();}
    }
}
