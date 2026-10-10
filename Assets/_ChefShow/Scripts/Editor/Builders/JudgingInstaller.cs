using System;
using System.Linq;
using ChefShow.Core;
using ChefShow.Data;
using ChefShow.Judging;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ChefShow.Editor
{
    public static class JudgingInstaller
    {
        private const string Folder = "Assets/_ChefShow/Data/Judging";

        [MenuItem("Tools/Chef Show/Install Submitted Dish Judging")]
        public static void Install() => HandServingInstaller.UpdateScenes(AddToScene, "judging");

        public static void AddToScene(Scene scene)
        {
            var bootstrap = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<GameBootstrap>(true)).Single();
            if (bootstrap.Recipes == null || bootstrap.Recipes.Config == null || bootstrap.RecipeBook == null
                || bootstrap.RecipeBook.Catalog == null || bootstrap.Cooking == null)
                throw new InvalidOperationException("Судейству нужны сохранённые профили рецептов, книга и готовка.");

            var config = CreateConfig(bootstrap);
            var controller = bootstrap.Judging;
            if (controller == null)
            {
                controller = bootstrap.GetComponent<JudgingController>();
                if (controller == null) controller = Undo.AddComponent<JudgingController>(bootstrap.gameObject);
            }
            Undo.RecordObject(controller, "Saved judging references");
            if (controller.Config == null) controller.Config = config;
            if (controller.Panel == null)
            {
                var existing = bootstrap.Hud.transform.Find("Judging Result");
                if (existing != null)
                {
                    controller.Panel = existing.gameObject;
                    controller.Total = controller.Total ?? FindText(existing, "Score Total");
                    controller.Breakdown = controller.Breakdown ?? FindText(existing, "Score Categories");
                    controller.Reasons = controller.Reasons ?? FindText(existing, "Score Reasons");
                }
                else
                {
                    var panel = Rect("Judging Result", bootstrap.Hud.transform, new Vector2(1, 0),
                        new Vector2(-20, 25), new Vector2(430, 365));
                    var background = Undo.AddComponent<Image>(panel.gameObject);
                    background.color = new Color(.035f, .065f, .085f, .94f);
                    background.raycastTarget = false;
                    var edge = Undo.AddComponent<Outline>(panel.gameObject);
                    edge.effectColor = new Color(.92f, .76f, .30f, .85f);
                    edge.effectDistance = new Vector2(1, -1);
                    var book = bootstrap.Hud.transform.Find("Recipe Book");
                    if (book != null) panel.SetSiblingIndex(book.GetSiblingIndex());
                    controller.Panel = panel.gameObject;
                    controller.Total = Text("Score Total", panel, new Vector2(16, -12), new Vector2(398, 72),
                        28, new Color(1, .85f, .35f), "ИТОГ: — / 100", bootstrap);
                    controller.Breakdown = Text("Score Categories", panel, new Vector2(16, -92), new Vector2(398, 145),
                        18, new Color(.94f, .96f, 1), "", bootstrap);
                    controller.Reasons = Text("Score Reasons", panel, new Vector2(16, -247), new Vector2(398, 100),
                        16, new Color(.94f, .91f, .80f), "", bootstrap);
                    controller.Panel.SetActive(false);
                    EditorUtility.SetDirty(background);
                    EditorUtility.SetDirty(edge);
                }
            }
            var error = controller.Validate();
            if (error != null) throw new InvalidOperationException(error);
            // Validate the native data links against both saved runtime rule sets before saving the scenes.
            controller.Config.Capture(bootstrap.Recipes.Config.Capture(), bootstrap.Cooking.Config.Capture());
            Undo.RecordObject(bootstrap, "Judging controller reference");
            bootstrap.Judging = controller;
            EditorUtility.SetDirty(controller);
            EditorUtility.SetDirty(bootstrap);
        }

        private static JudgingConfig CreateConfig(GameBootstrap bootstrap)
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/_ChefShow/Data", "Judging");
            var profiles = bootstrap.Recipes.Config.Recipes.Select(recognition => MakeProfile(bootstrap, recognition)).ToArray();
            var path = Folder + "/JudgingConfig.asset";
            var config = AssetDatabase.LoadAssetAtPath<JudgingConfig>(path);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<JudgingConfig>();
                config.Profiles = profiles;
                AssetDatabase.CreateAsset(config, path);
            }
            var error = config.Validate();
            if (error != null) throw new InvalidOperationException(error);
            return config;
        }

        private static JudgingRecipeProfile MakeProfile(GameBootstrap bootstrap, RecipeRecognitionDefinition recognition)
        {
            var path = Folder + "/" + recognition.Reference.Id + ".asset";
            var profile = AssetDatabase.LoadAssetAtPath<JudgingRecipeProfile>(path);
            if (profile != null) return profile;
            profile = ScriptableObject.CreateInstance<JudgingRecipeProfile>();
            profile.Reference = recognition.Reference;
            profile.Core = recognition.Core.Select((core, index) =>
            {
                var reference = recognition.Reference.Ingredients.FirstOrDefault(ingredient =>
                    core.Alternatives.Any(alternative => alternative.Id == ingredient.Ingredient.Id));
                var id = core.Alternatives[0].Id;
                bool compositeTart = recognition.Reference.Id == "apple_tart";
                bool main = index == 0 || compositeTart;
                var role = main ? JudgingRole.Main : id == "onion" || id == "carrot"
                    ? JudgingRole.Vegetable : JudgingRole.Garnish;
                return new JudgingCorePortion
                {
                    RecognitionCoreIndex = index,
                    ReferenceQuantity = reference == null ? 1 : reference.Amount,
                    Role = role,
                    MandatoryProtein = core.Alternatives.Any(ingredient => ingredient.Id == "beef" || ingredient.Id == "egg"),
                    MainComponent = main,
                    UseApplianceHeatDefaults = true,
                    Heat = new JudgingRange(0, 30, 45, 60)
                };
            }).ToArray();
            var quantity = profile.Core.Sum(core => core.ReferenceQuantity);
            profile.Salt = DoseRange(recognition.Reference, bootstrap.Cooking.Config.SaltIngredientId, quantity);
            profile.Oil = DoseRange(recognition.Reference, bootstrap.Cooking.Config.OilIngredientId, quantity);
            profile.LiquidFood = false;
            profile.IdealFillMinimum = 0;
            profile.IdealFillMaximum = 1;
            profile.ZeroFillAt = 2.5f;
            AssetDatabase.CreateAsset(profile, path);
            return profile;
        }

        private static JudgingRange DoseRange(RecipeDefinition reference, string id, float quantity)
        {
            var ingredient = reference.Ingredients.FirstOrDefault(item => item.Ingredient.Id == id);
            float amount = ingredient == null ? 0 : ingredient.Amount;
            // Temporary starter norm: zero up to the reference dose stays in the ideal range.
            return new JudgingRange(0, 0, amount / quantity, (amount + 1) / quantity);
        }

        private static Text FindText(Transform panel, string name)
        {
            var child = panel.Find(name);
            return child == null ? null : child.GetComponent<Text>();
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 point, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            Undo.RegisterCreatedObjectUndo(go, "Saved judging result UI");
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = point;
            rect.sizeDelta = size;
            return rect;
        }

        private static Text Text(string name, Transform parent, Vector2 point, Vector2 size, int fontSize,
            Color color, string value, GameBootstrap bootstrap)
        {
            var rect = Rect(name, parent, new Vector2(0, 1), point, size);
            var text = Undo.AddComponent<Text>(rect.gameObject);
            text.font = bootstrap.Hud.Status.font;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = TextAnchor.UpperLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            text.supportRichText = true;
            text.text = value;
            EditorUtility.SetDirty(text);
            return text;
        }
    }
}
