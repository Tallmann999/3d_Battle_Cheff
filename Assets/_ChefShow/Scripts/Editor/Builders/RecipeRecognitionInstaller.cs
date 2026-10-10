using System;
using System.Linq;
using ChefShow.Core;
using ChefShow.Cooking;
using ChefShow.Data;
using ChefShow.Recipes;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ChefShow.Editor
{
    public static class RecipeRecognitionInstaller
    {
        private const string Folder = "Assets/_ChefShow/Data/Recipes/Recognition";

        [MenuItem("Tools/Chef Show/Install Recipe Recognition")]
        public static void Install() => HandServingInstaller.UpdateScenes(AddToScene, "recipe-recognition");

        public static void AddToScene(Scene scene)
        {
            var bootstrap = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<GameBootstrap>(true)).Single();
            if (bootstrap.RecipeBook == null || bootstrap.RecipeBook.Catalog == null)
                throw new InvalidOperationException("Сначала нужен сохранённый справочник 12 рецептов.");
            if (bootstrap.Inventory == null || bootstrap.Inventory.Catalog == null)
                throw new InvalidOperationException("Не назначен каталог ингредиентов.");

            var catalog = CreateCatalog(bootstrap);
            var controller = bootstrap.Recipes;
            if (controller == null)
            {
                controller = bootstrap.GetComponent<RecipeRecognitionController>();
                if (controller == null) controller = Undo.AddComponent<RecipeRecognitionController>(bootstrap.gameObject);
            }
            Undo.RecordObject(controller, "Saved recipe recognition references");
            if (controller.Config == null) controller.Config = catalog;

            if (controller.Panel == null)
            {
                var existing = bootstrap.Hud.transform.Find("Recipe Recognition");
                if (existing != null)
                {
                    controller.Panel = existing.gameObject;
                    if (controller.Label == null) controller.Label = existing.GetComponentInChildren<Text>(true);
                }
                else
                {
                    var panel = Rect("Recipe Recognition", bootstrap.Hud.transform,
                        new Vector2(20, -262), new Vector2(520, 142));
                    var image = Undo.AddComponent<Image>(panel.gameObject);
                    image.color = new Color(.04f, .06f, .08f, .78f);
                    image.raycastTarget = false;
                    controller.Panel = panel.gameObject;
                    var book = bootstrap.Hud.transform.Find("Recipe Book");
                    if (book != null) panel.SetSiblingIndex(book.GetSiblingIndex());

                    var labelRect = Rect("Recognition Label", panel,
                        new Vector2(12, -10), new Vector2(496, 122));
                    var label = Undo.AddComponent<Text>(labelRect.gameObject);
                    label.font = bootstrap.Hud.Status.font;
                    label.fontSize = 18;
                    label.color = new Color(.94f, .96f, 1);
                    label.alignment = TextAnchor.UpperLeft;
                    label.horizontalOverflow = HorizontalWrapMode.Wrap;
                    label.verticalOverflow = VerticalWrapMode.Truncate;
                    label.raycastTarget = false;
                    label.text = "Блюдо: Нет блюда";
                    controller.Label = label;
                    EditorUtility.SetDirty(label);
                    EditorUtility.SetDirty(image);
                }
            }
            if (controller.Label == null)
                throw new InvalidOperationException("У результата распознавания нет сохранённого текста.");

            Undo.RecordObject(bootstrap, "Recipe recognition controller");
            bootstrap.Recipes = controller;
            var validation = controller.Validate();
            if (validation != null) throw new InvalidOperationException(validation);
            EditorUtility.SetDirty(controller);
            EditorUtility.SetDirty(bootstrap);
        }

        private static RecipeRecognitionCatalog CreateCatalog(GameBootstrap bootstrap)
        {
            if (!AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.CreateFolder("Assets/_ChefShow/Data/Recipes", "Recognition");
            var recipes = new[]
            {
                Make(bootstrap, "fried_potatoes", new[]
                {
                    Rule(bootstrap, "potato", RecipePreparation.Chopped, 0, CookerKind.Pan),
                    Rule(bootstrap, "onion", RecipePreparation.Chopped, 0, CookerKind.Pan)
                }),
                Make(bootstrap, "boiled_potatoes", new[]
                {
                    Rule(bootstrap, "potato", RecipePreparation.Any, 0, CookerKind.Pot),
                    After(bootstrap, "butter", "potato")
                }),
                Make(bootstrap, "fried_egg", new[]
                {
                    Rule(bootstrap, "egg", RecipePreparation.Whole, 0, CookerKind.Pan)
                }),
                Make(bootstrap, "cheese_omelet", new[]
                {
                    Rule(bootstrap, "egg", RecipePreparation.Any, 1, CookerKind.Pan),
                    Rule(bootstrap, "cheese", RecipePreparation.Any, 1, CookerKind.Pan)
                }, optional: new[] { Ingredient(bootstrap, "onion") }),
                Make(bootstrap, "scrambled_eggs", new[]
                {
                    Rule(bootstrap, "egg", RecipePreparation.Whole, 1, CookerKind.Pan),
                    Rule(bootstrap, "butter", RecipePreparation.Any, 1, CookerKind.Pan)
                }, unavailable: "Перемешивание при нагреве на сковороде пока не реализовано."),
                Make(bootstrap, "potato_pancakes", new[]
                {
                    Rule(bootstrap, "potato", RecipePreparation.Chopped, 1, CookerKind.Pan),
                    Rule(bootstrap, "egg", RecipePreparation.Any, 1, CookerKind.Pan),
                    Rule(bootstrap, "flour", RecipePreparation.Any, 1, CookerKind.Pan)
                }),
                Make(bootstrap, "steak_vegetables", new[]
                {
                    Rule(bootstrap, "beef", RecipePreparation.Whole, 0, CookerKind.Pan),
                    Rule(bootstrap, "potato", RecipePreparation.Chopped, 0, CookerKind.Pot),
                    Rule(bootstrap, "carrot|onion", RecipePreparation.Chopped, 0, CookerKind.Pan, CookerKind.Pot)
                }),
                Make(bootstrap, "buttered_carrots", new[]
                {
                    Rule(bootstrap, "carrot", RecipePreparation.Any, 0, CookerKind.Pot),
                    After(bootstrap, "butter", "carrot")
                }),
                Make(bootstrap, "caramel_apples", new[]
                {
                    Rule(bootstrap, "apple", RecipePreparation.Chopped, 0, CookerKind.Pan),
                    Rule(bootstrap, "sugar", RecipePreparation.Any, 0, CookerKind.Pan),
                    Rule(bootstrap, "butter", RecipePreparation.Any, 0, CookerKind.Pan)
                }),
                Make(bootstrap, "apple_tart", new[]
                {
                    Rule(bootstrap, "apple", RecipePreparation.Chopped, 1, CookerKind.Oven),
                    Rule(bootstrap, "flour", RecipePreparation.Any, 1, CookerKind.Oven),
                    Rule(bootstrap, "egg", RecipePreparation.Any, 1, CookerKind.Oven),
                    Rule(bootstrap, "butter", RecipePreparation.Any, 1, CookerKind.Oven),
                    Rule(bootstrap, "sugar", RecipePreparation.Any, 1, CookerKind.Oven)
                }),
                Make(bootstrap, "beef_steak", new[]
                {
                    Rule(bootstrap, "beef", RecipePreparation.Whole, 0, CookerKind.Pan)
                }),
                Make(bootstrap, "braised_beef_onion", new[]
                {
                    Rule(bootstrap, "beef", RecipePreparation.Chopped, 0, CookerKind.Pan, CookerKind.Pot),
                    Rule(bootstrap, "onion", RecipePreparation.Chopped, 0, CookerKind.Pan, CookerKind.Pot)
                }, unavailable: "Настоящее тушение с жидкостью, крышкой и историей способов пока не реализовано.")
            };
            var path = Folder + "/RecipeRecognitionCatalog.asset";
            var catalog = AssetDatabase.LoadAssetAtPath<RecipeRecognitionCatalog>(path);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<RecipeRecognitionCatalog>();
                catalog.Recipes = recipes;
                AssetDatabase.CreateAsset(catalog, path);
            }
            var error = catalog.Validate();
            if (error != null) throw new InvalidOperationException(error);
            return catalog;
        }

        private static RecipeRecognitionDefinition Make(GameBootstrap bootstrap, string id,
            RecipeCoreRule[] core, IngredientDefinition[] optional = null, string unavailable = null)
        {
            var path = Folder + "/" + id + ".asset";
            var profile = AssetDatabase.LoadAssetAtPath<RecipeRecognitionDefinition>(path);
            if (profile != null) return profile;
            profile = ScriptableObject.CreateInstance<RecipeRecognitionDefinition>();
            profile.Reference = bootstrap.RecipeBook.Catalog.Recipes.Single(recipe => recipe.Id == id);
            profile.Core = core;
            profile.Optional = new[] { Ingredient(bootstrap, "salt"), Ingredient(bootstrap, "oil") }
                .Concat(optional ?? Array.Empty<IngredientDefinition>()).ToArray();
            profile.UnavailableProcess = unavailable;
            var error = profile.Validate();
            if (error != null) throw new InvalidOperationException(error);
            AssetDatabase.CreateAsset(profile, path);
            return profile;
        }

        private static RecipeCoreRule Rule(GameBootstrap bootstrap, string alternatives,
            RecipePreparation preparation, int mixedGroup, params CookerKind[] cookers)
        {
            return new RecipeCoreRule
            {
                Alternatives = alternatives.Split('|').Select(id => Ingredient(bootstrap, id)).ToArray(),
                Preparation = preparation,
                AllowedCookers = cookers,
                MixedGroup = mixedGroup
            };
        }

        private static RecipeCoreRule After(GameBootstrap bootstrap, string ingredient, string cookedIngredient)
        {
            var rule = Rule(bootstrap, ingredient, RecipePreparation.Any, 0);
            rule.AddedAfterIngredient = cookedIngredient;
            return rule;
        }

        private static IngredientDefinition Ingredient(GameBootstrap bootstrap, string id) =>
            bootstrap.Inventory.Catalog.Ingredients.Single(ingredient => ingredient.Id == id);

        private static RectTransform Rect(string name, Transform parent, Vector2 point, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            Undo.RegisterCreatedObjectUndo(go, "Saved recipe recognition UI");
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = point;
            rect.sizeDelta = size;
            return rect;
        }
    }
}
