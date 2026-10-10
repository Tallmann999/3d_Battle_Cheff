using System;
using System.Collections.Generic;
using System.Linq;
using ChefShow.Cooking;
using ChefShow.Core;
using ChefShow.Data;
using ChefShow.Inventory;
using ChefShow.Recipes;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ChefShow.Tests
{
    public sealed class RecipeRecognitionConfigTests
    {
        private readonly List<ScriptableObject> assets = new List<ScriptableObject>();
        private IngredientDefinition beef, egg, carrot, potato;
        private PrototypeRun run;

        [SetUp] public void Setup()
        {
            beef = Ingredient("beef"); egg = Ingredient("egg");
            carrot = Ingredient("carrot"); potato = Ingredient("potato");
        }
        [TearDown] public void Cleanup()
        {
            run?.Dispose(); run = null;
            foreach (var asset in assets) Object.DestroyImmediate(asset);
            assets.Clear();
        }
        private T Asset<T>() where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>(); assets.Add(asset); return asset;
        }
        private IngredientDefinition Ingredient(string id)
        {
            var definition = Asset<IngredientDefinition>();
            definition.Id = id; definition.DisplayName = id; definition.CanUseBoard = true;
            return definition;
        }
        private RecipeRecognitionDefinition Profile(string id, params IngredientDefinition[] core)
        {
            var reference = Asset<RecipeDefinition>(); reference.Id = id; reference.DisplayName = id;
            reference.Ingredients = core.Select(ingredient => new RecipeIngredient { Ingredient = ingredient, Amount = 1 }).ToArray();
            reference.Steps = new[] { new RecipeStep { Symbol = RecipeSymbol.Pan } };
            var profile = Asset<RecipeRecognitionDefinition>(); profile.Reference = reference;
            profile.Core = core.Select(ingredient => new RecipeCoreRule
            {
                Alternatives = new[] { ingredient }, Preparation = RecipePreparation.Any,
                AllowedCookers = Array.Empty<CookerKind>()
            }).ToArray();
            Assert.That(profile.Validate(), Is.Null);
            return profile;
        }
        private DishSnapshot ThreeActualIngredients()
        {
            run = new PrototypeRun(240, 1, (type, error) => Assert.Fail(error.ToString()));
            var state = run.Inventory;
            foreach (var ingredient in new[] { beef, egg, carrot })
            {
                Assert.That(state.TryMoveBasket(BasketPlacement.Carried, out _), Is.True);
                Assert.That(state.TryCollect(ingredient, out _), Is.True);
                Assert.That(state.TryMoveBasket(BasketPlacement.Station, out _), Is.True);
                Assert.That(state.TryUnload(out _), Is.True);
                Assert.That(state.TryTakeTray(state.Tray.Count - 1, out _), Is.True);
                Assert.That(state.TryPlaceServing(out _), Is.True);
            }
            return state.CaptureDish();
        }
        private void AssertBothCatalogOrders(DishSnapshot dish, string expected, params RecipeRecognitionDefinition[] profiles)
        {
            var captures = profiles.Select(profile => profile.Capture()).ToArray();
            var forward = new RecipeIdentitySettings(captures).Resolve(dish);
            var reverse = new RecipeIdentitySettings(captures.Reverse()).Resolve(dish);
            Assert.That(forward.RecipeId, Is.EqualTo(expected));
            Assert.That(reverse.RecipeId, Is.EqualTo(expected));
            Assert.That(reverse.Ingredients.Select(ingredient => ingredient.Id),
                Is.EqualTo(forward.Ingredients.Select(ingredient => ingredient.Id)));
            Assert.That(reverse.Issues, Is.EqualTo(forward.Issues));
        }

        [Test] public void InvalidPreparationAndCookerEnumsFailBeforeRulesAreCaptured()
        {
            var profile = Profile("enum_validation", beef);
            profile.Core[0].Preparation = (RecipePreparation)999;
            Assert.That(profile.Validate(), Is.Not.Null);
            Assert.Throws<InvalidOperationException>(() => profile.Capture());
            profile.Core[0].Preparation = RecipePreparation.Whole;
            profile.Core[0].AllowedCookers = new[] { (CookerKind)999 };
            Assert.That(profile.Validate(), Is.Not.Null);
            Assert.Throws<InvalidOperationException>(() => profile.Capture());
            profile.Core[0].AllowedCookers = new[] { CookerKind.Pan };
            Assert.That(profile.Validate(), Is.Null);
        }

        [Test] public void EmptyIngredientIdsInCoreAndOptionalAdditionsFailValidation()
        {
            var profile = Profile("id_validation", beef);
            beef.Id = "  ";
            Assert.That(profile.Validate(), Is.Not.Null);
            Assert.Throws<InvalidOperationException>(() => profile.Capture());
            beef.Id = "beef"; profile.Optional = new[] { egg }; egg.Id = "";
            Assert.That(profile.Validate(), Does.Contain("опциональная"));
            Assert.Throws<InvalidOperationException>(() => profile.Capture());
            egg.Id = "egg";
            Assert.That(profile.Validate(), Is.Null);
        }

        [Test] public void MixedGroupWithoutAnAllowedHeatMethodIsRejected()
        {
            var profile = Profile("mixed_group_validation", beef, egg);
            foreach (var core in profile.Core) core.MixedGroup = 1;
            Assert.That(profile.Validate(), Is.Not.Null);
            Assert.Throws<InvalidOperationException>(() => profile.Capture());
            foreach (var core in profile.Core) core.AllowedCookers = new[] { CookerKind.Pan };
            Assert.That(profile.Validate(), Is.Null);
        }

        [Test] public void FullCoreCoverageWinsOverAnIncompleteRecipeWithFewerExtras()
        {
            var incomplete = Profile("a_incomplete", beef, egg, carrot, potato);
            var complete = Profile("z_complete", beef);
            // Incomplete covers 3/4 groups and has no extras; complete covers 1/1 with two extras.
            AssertBothCatalogOrders(ThreeActualIngredients(), "z_complete", incomplete, complete);
        }

        [Test] public void FewerExtrasWinsBetweenValidRecipesDespiteASmallerCore()
        {
            var larger = Profile("a_larger", beef, egg);
            var smaller = Profile("z_fewer_extras", beef); smaller.Optional = new[] { egg, carrot };
            // Both have full coverage; the larger core leaves carrot outside its recipe.
            AssertBothCatalogOrders(ThreeActualIngredients(), "z_fewer_extras", larger, smaller);
        }

        [Test] public void StableOrdinalIdBreaksValidEqualExtraTieRegardlessOfCoreSizeOrCatalogOrder()
        {
            var larger = Profile("z_larger", beef, egg, carrot);
            var smaller = Profile("a_smaller", beef); smaller.Optional = new[] { egg, carrot };
            AssertBothCatalogOrders(ThreeActualIngredients(), "a_smaller", larger, smaller);
        }
    }
}
