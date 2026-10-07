using System;
using System.IO;
using System.Linq;
using ChefShow.Core;
using ChefShow.Ingredients;
using ChefShow.Inventory;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ChefShow.Editor
{
    public static class FoodPreparationInstaller
    {
        [MenuItem("Tools/Chef Show/Install Food Preparation Stage")]
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
                    File.Copy(scene.path, "TestResults/preparation-before-" + scene.name + "-" + stamp + ".unity", false);
                foreach (var scene in new[] { working, generated })
                {
                    SceneManager.SetActiveScene(scene); AddToScene(scene); PrototypeValidator.ValidateScene(scene);
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
        public static void AddToScene(Scene scene)
        {
            var roots = scene.GetRootGameObjects();
            var bootstrap = roots.SelectMany(r => r.GetComponentsInChildren<GameBootstrap>(true)).Single();
            if (bootstrap.Inventory == null || bootstrap.Tools == null) throw new InvalidOperationException("Сначала нужны продукты и инструменты.");
            var controller = bootstrap.Preparation;
            if (controller == null)
            {
                controller = Undo.AddComponent<PreparationController>(bootstrap.gameObject);
                Undo.RecordObject(bootstrap, "Food preparation reference"); bootstrap.Preparation = controller;
            }
            var views = bootstrap.Inventory.BasketDisplays.Concat(bootstrap.Inventory.TrayDisplays)
                .Concat(bootstrap.Inventory.SocketDisplays).Append(bootstrap.Inventory.HeldDisplay);
            foreach (var view in views)
            {
                if (view.CutPieces != null && view.CutPieces.Length == InventoryState.RequiredChopPresses + 1 && view.CutPieces.All(r => r != null)) continue;
                Undo.RecordObject(view, "Saved food pieces");
                view.CutPieces = Enumerable.Range(0, InventoryState.RequiredChopPresses + 1).Select(i =>
                {
                    string name = "Cut Piece " + (i + 1);
                    var child = view.transform.Find(name);
                    if (child == null)
                    {
                        var piece = GameObject.CreatePrimitive(PrimitiveType.Cube); piece.name = name;
                        Undo.RegisterCreatedObjectUndo(piece, "Saved food piece");
                        UnityEngine.Object.DestroyImmediate(piece.GetComponent<Collider>());
                        piece.transform.SetParent(view.transform, false); child = piece.transform;
                        child.localPosition = new Vector3((i - 3) * .035f, 0, 0); child.localScale = Vector3.one * .025f;
                        child.gameObject.layer = LayerMask.NameToLayer("Ignore Raycast");
                    }
                    var renderer = child.GetComponent<Renderer>(); renderer.sharedMaterial = bootstrap.Inventory.Catalog.Ingredients[0].VisualMaterial;
                    renderer.enabled = false; return renderer;
                }).ToArray();
                EditorUtility.SetDirty(view);
            }
            Undo.RecordObject(controller, "Chopping board references");
            controller.Boards = roots.SelectMany(r => r.GetComponentsInChildren<InventoryInteractable>(true))
                .Where(t => t.Kind == InventoryTargetKind.Socket && t.Index == 0).OrderBy(t => t.StationId).Select(target =>
                {
                    var board = target.GetComponent<ChoppingBoard>();
                    if (board == null) board = Undo.AddComponent<ChoppingBoard>(target.gameObject);
                    Undo.RecordObject(board, "Chopping board references");
                    board.Target = target; board.Caption = target.transform.Find("Label").GetComponent<TextMesh>();
                    Undo.RecordObject(board.Caption, "Readable board caption"); Undo.RecordObject(board.Caption.transform, "Board caption edge");
                    float side = target.StationId.StartsWith("A", StringComparison.Ordinal) ? -1 : 1;
                    var bounds = target.GetComponent<BoxCollider>().bounds;
                    board.Caption.transform.position = new Vector3(bounds.center.x + side * .24f, bounds.max.y + .035f, bounds.center.z);
                    board.Caption.characterSize = .009f; EditorUtility.SetDirty(board.Caption);
                    if (board.KnifeContactPoint == null)
                    {
                        var node = new GameObject("Knife Contact Point"); Undo.RegisterCreatedObjectUndo(node, "Knife contact point");
                        node.transform.SetParent(target.transform, false);
                        node.transform.position = target.GetComponent<BoxCollider>().bounds.center + Vector3.up * .14f;
                        board.KnifeContactPoint = node.transform;
                    }
                    board.Present(null); EditorUtility.SetDirty(board); return board;
                }).ToArray();
            foreach (var text in bootstrap.Hud.GetComponentsInChildren<Text>(true))
                if (text.name == "Controls")
                {
                    Undo.RecordObject(text, "Preparation controls");
                    text.text = "WASD — ходьба · Shift — бег · мышь — обзор\nTab — корзина · E — взять / положить / выгрузить\nЛКМ — нарезать на доске · G — уронить · RMB — отмена\nQ — задание · Esc — пауза";
                    text.rectTransform.sizeDelta = new Vector2(800, 92); EditorUtility.SetDirty(text);
                }
            Undo.RecordObject(bootstrap.Hud.TaskCard, "Preparation task");
            bootstrap.Hud.TaskCard.text = "ПРОДУКТЫ И НАРЕЗКА\nTab — взять корзину, E по продуктам — набрать.\nНа своём столе: Tab поставить, E по корзине — выгрузить.\nE — взять нож из открытого ящика и продукт из лотка.\nE по доске — положить продукт. ЛКМ — нарезать (6 кликов).\nE — перенести всю порцию; нагрев будет следующим этапом.";
            EditorUtility.SetDirty(bootstrap.Hud.TaskCard); EditorUtility.SetDirty(controller); EditorUtility.SetDirty(bootstrap);
            string error = controller.Validate(bootstrap.Inventory); if (error != null) throw new InvalidOperationException(error);
            Physics.SyncTransforms();
        }
    }
}
