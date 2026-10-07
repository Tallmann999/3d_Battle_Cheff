using System;
using System.IO;
using System.Linq;
using ChefShow.Core;
using ChefShow.Data;
using ChefShow.Inventory;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ChefShow.Editor
{
    public static class PackagePreparationInstaller
    {
        [MenuItem("Tools/Chef Show/Install Package Preparation Stage")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("Нужен Edit Mode после компиляции.");
            var working = SceneManager.GetActiveScene();
            if (working.path != PrototypeSceneBuilder.EditableScene || working.isDirty)
                throw new InvalidOperationException("Сохраните рабочую сцену ChefShow_Prototype.");
            var generated = SceneManager.GetSceneByPath(PrototypeSceneBuilder.GeneratedScene);
            bool opened = !generated.IsValid() || !generated.isLoaded;
            if (!opened && generated.isDirty) throw new InvalidOperationException("Сохраните generated-сцену.");
            if (opened) generated = EditorSceneManager.OpenScene(PrototypeSceneBuilder.GeneratedScene, OpenSceneMode.Additive);
            try
            {
                Directory.CreateDirectory("TestResults");
                string stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
                foreach (var scene in new[] { working, generated })
                    File.Copy(scene.path, "TestResults/package-before-" + scene.name + "-" + stamp + ".unity", false);
                var catalogs = new[] { working, generated }.Select(s => s.GetRootGameObjects()
                    .SelectMany(r => r.GetComponentsInChildren<GameBootstrap>(true)).Single().Inventory.Catalog).Distinct();
                foreach (var catalog in catalogs)
                    foreach (var definition in catalog.Ingredients.Where(d => d.Id == "potato_sack" || d.Id == "egg_carton"))
                    {
                        string path = AssetDatabase.GetAssetPath(definition);
                        File.Copy(path, "TestResults/package-before-" + definition.Id + "-" + stamp + ".asset", false);
                        Undo.RecordObject(definition, "Package contents quantity");
                        definition.ContentsQuantity = definition.Id == "potato_sack" ? 5 : 6;
                        EditorUtility.SetDirty(definition); AssetDatabase.SaveAssetIfDirty(definition);
                    }
                foreach (var scene in new[] { working, generated })
                {
                    SceneManager.SetActiveScene(scene); ApplyScene(scene); PrototypeValidator.ValidateScene(scene);
                    Undo.FlushUndoRecordObjects(); EditorSceneManager.MarkSceneDirty(scene);
                    if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Не удалось сохранить " + scene.path);
                }
            }
            finally
            {
                SceneManager.SetActiveScene(working);
                if (opened) EditorSceneManager.CloseScene(generated, true);
            }
        }
        public static void ApplyScene(Scene scene)
        {
            var bootstrap = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<GameBootstrap>(true)).Single();
            if (bootstrap.Preparation == null) throw new InvalidOperationException("Сначала нужен этап нарезки.");
            foreach (var surface in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<InventoryInteractable>(true))
                .Where(t => t.Kind == InventoryTargetKind.Socket && t.Index == (int)StationSocketKind.WorkSurface))
            {
                var caption = surface.transform.Find("Label").GetComponent<TextMesh>();
                Undo.RecordObject(caption, "Readable package surface caption"); Undo.RecordObject(caption.transform, "Package caption edge");
                var bounds = surface.GetComponent<BoxCollider>().bounds;
                float side = surface.StationId.StartsWith("A", StringComparison.Ordinal) ? -1 : 1;
                caption.transform.position = new Vector3(bounds.center.x + side * .24f, bounds.max.y + .035f, bounds.center.z);
                caption.characterSize = .009f; EditorUtility.SetDirty(caption);
            }
            foreach (var text in bootstrap.Hud.GetComponentsInChildren<Text>(true).Where(t => t.name == "Controls"))
            {
                Undo.RecordObject(text, "Package controls");
                text.text = "WASD — ходьба · Shift — бег · мышь — обзор\nTab — корзина · E — взять / положить / выгрузить\nЛКМ — нарезать / распаковать · G — уронить · RMB — отмена\nQ — задание · Esc — пауза";
                EditorUtility.SetDirty(text);
            }
            Undo.RecordObject(bootstrap.Hud.TaskCard, "Package task");
            bootstrap.Hud.TaskCard.text = "ПОДГОТОВКА ПРОДУКТОВ\nTab — корзина; E — сбор, выгрузка, взять / положить.\nНож справа + продукт на доске: 6 ЛКМ — нарезать.\nУпаковка на Месте продукта: ЛКМ — распаковать в лоток.\nМешок — 5 картофелин; коробка — 6 яиц.\nНе хватает места — упаковка остаётся целой.\nE — перенести порцию; нагрев будет следующим этапом.";
            EditorUtility.SetDirty(bootstrap.Hud.TaskCard);
        }
    }
}
