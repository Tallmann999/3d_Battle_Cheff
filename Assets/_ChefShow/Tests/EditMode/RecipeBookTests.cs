using System;
using System.Collections.Generic;
using System.Linq;
using ChefShow.Data;
using ChefShow.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
namespace ChefShow.Tests
{
    public sealed class RecipeBookTests
    {
        private RecipeBookCatalog Catalog=>AssetDatabase.LoadAssetAtPath<RecipeBookCatalog>("Assets/_ChefShow/Data/Recipes/RecipeBookCatalog.asset");
        [Test] public void CatalogContainsTwelveDistinctEditableReferenceProfilesAndIllustratedMethods()
        {
            var catalog=Catalog;Assert.That(catalog,Is.Not.Null);Assert.That(catalog.Validate(),Is.Null);
            var pages=catalog.Capture();Assert.That(pages.Select(x=>x.Id).Distinct().Count(),Is.EqualTo(12));
            foreach(var page in pages)foreach(var ingredient in page.Ingredients)Assert.DoesNotThrow(()=>RecipeBookController.SymbolFor(ingredient.Id));
            Assert.That(pages.Single(x=>x.Id=="beef_steak").Ingredients.Count,Is.EqualTo(3));
            Assert.That(pages.Single(x=>x.Id=="steak_vegetables").Steps.Any(x=>x.Symbol==RecipeSymbol.Pot),Is.True);
            Assert.That(pages.Single(x=>x.Id=="braised_beef_onion").Steps.Any(x=>x.Symbol==RecipeSymbol.Braise&&x.FutureProcess),Is.True);
            Assert.That(pages.Any(x=>x.Steps.Any(s=>s.Symbol==RecipeSymbol.Oven)),Is.True);
            Assert.That(pages.All(x=>x.Proposed),Is.True);
        }
        [Test] public void EditingDefinitionsDoesNotRewriteCapturedIngredientsOrSteps()
        {
            var copy=UnityEngine.Object.Instantiate(Catalog.Recipes[0]);
            try
            {
                var page=copy.Capture();int before=page.Ingredients[0].Amount;string caption=page.Steps[0].Caption;
                copy.Ingredients[0].Amount=before+5;copy.Steps[0].Caption="Changed after capture";copy.DisplayName="Changed";
                Assert.That(page.Ingredients[0].Amount,Is.EqualTo(before));Assert.That(page.Steps[0].Caption,Is.EqualTo(caption));
                Assert.That(page.Title,Is.Not.EqualTo(copy.DisplayName));
                Assert.Throws<NotSupportedException>(()=>((IList<RecipeIngredientPage>)page.Ingredients)[0]=null);
            }
            finally{UnityEngine.Object.DestroyImmediate(copy);}
        }
        [Test] public void InvalidReferenceDefinitionsAndDuplicateIdsFailBeforeOpening()
        {
            var copy=UnityEngine.Object.Instantiate(Catalog);var recipe=UnityEngine.Object.Instantiate(copy.Recipes[0]);
            try
            {
                copy.Recipes[1]=copy.Recipes[0];Assert.That(copy.Validate(),Is.Not.Null);
                recipe.Ingredients[0].Amount=0;Assert.Throws<InvalidOperationException>(()=>recipe.Capture());
                recipe.Ingredients[0].Amount=1;recipe.Ingredients[0].Ingredient=null;Assert.That(recipe.Validate(),Is.Not.Null);
            }
            finally{UnityEngine.Object.DestroyImmediate(recipe);UnityEngine.Object.DestroyImmediate(copy);}
        }
    }
}
