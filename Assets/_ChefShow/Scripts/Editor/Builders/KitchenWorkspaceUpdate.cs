using System;
using System.IO;
using System.Linq;
using ChefShow.Core;
using ChefShow.Data;
using ChefShow.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ChefShow.Editor
{
    /// <summary>Узкая правка D-012/D-013, без замены/регенерации рабочей сцены.</summary>
    public static class KitchenWorkspaceUpdate
    {
        public const float PlayerDistance = 1.65f;
        public const float NormalFieldOfView = 60;
        public const float InitialPitch = 35;

        [MenuItem("Tools/Chef Show/Apply Close Kitchen Workspace")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("Дождитесь Edit Mode и окончания компиляции.");
            var working = SceneManager.GetActiveScene();
            if (working.path != PrototypeSceneBuilder.EditableScene || working.isDirty)
                throw new InvalidOperationException("Откройте сохранённую рабочую ChefShow_Prototype.unity.");
            var generated = SceneManager.GetSceneByPath(PrototypeSceneBuilder.GeneratedScene);
            bool opened = !generated.IsValid() || !generated.isLoaded;
            if (!opened && generated.isDirty) throw new InvalidOperationException("Сначала сохраните generated-сцену.");
            if (opened) generated = EditorSceneManager.OpenScene(PrototypeSceneBuilder.GeneratedScene, OpenSceneMode.Additive);
            try
            {
                foreach (var scene in new[] { working, generated }) CheckScene(scene);
                Directory.CreateDirectory("TestResults");
                string stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
                foreach (var scene in new[] { working, generated })
                    File.Copy(scene.path, "TestResults/workspace-before-" + scene.name + "-" + stamp + ".unity", false);
                foreach (var scene in new[] { working, generated })
                {
                    SceneManager.SetActiveScene(scene);
                    ConfigureScene(scene);
                    PrototypeValidator.ValidateScene(scene);
                    Undo.FlushUndoRecordObjects();
                    EditorSceneManager.MarkSceneDirty(scene);
                    if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Не удалось сохранить " + scene.path);
                }
                File.WriteAllText("TestResults/workspace-applied.txt", "D-012/D-013 APPLIED " + DateTime.UtcNow.ToString("O")
                    + "\n12 stations: front board/work; back basket/tray; player distance 1.65; FOV 60/52; focus offset (0,-0.18,0.22).");
                Debug.Log("Chef Show D-012/D-013: близкая камера, передняя доска/место продукта; корзина и лоток дальше. Сцены сохранены.");
            }
            finally
            {
                SceneManager.SetActiveScene(working);
                if (opened) EditorSceneManager.CloseScene(generated, true);
            }
        }

        private static Transform[] CheckScene(Scene scene)
        {
            var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
            var stations = all.Where(t => t.name.StartsWith("Station_", StringComparison.Ordinal)).ToArray();
            var bootstrap = all.Select(t => t.GetComponent<GameBootstrap>()).Single(b => b != null);
            if (stations.Length != 12 || bootstrap.Inventory == null || bootstrap.Inventory.Validate() != null || bootstrap.Player.ViewCamera == null)
                throw new InvalidOperationException("Нужны 12 станций и установленные камера/инвентарь: " + scene.path);
            foreach (var station in stations)
            {
                if (station.GetComponent<BoxCollider>() == null || station.GetComponent<PrototypeInteractable>() == null)
                    throw new InvalidOperationException("Нет поверхности/интеракции: " + station.name);
                foreach (string name in new[] { "Board", "Work Surface", "Basket Dock", "Ingredient Tray" })
                    if (station.Find("Inventory/" + name) == null) throw new InvalidOperationException("Нет " + name + " в " + station.name);
            }
            return stations;
        }

        // Builder вызывает это только при явной генерации новой generated-сцены.
        public static void ConfigureScene(Scene scene)
        {
            var stations = CheckScene(scene);
            foreach (var station in stations)
            {
                float sign = station.name.StartsWith("Station_A", StringComparison.Ordinal) ? -1 : 1;
                foreach (string name in new[] { "Board", "Work Surface", "Basket Dock", "Ingredient Tray" })
                {
                    var pad = station.Find("Inventory/" + name);
                    Undo.RecordObject(pad, "Kitchen workspace D-013");
                    // Дочерние продукты, подписи и snap-точки следуют своей зоне.
                    float x = name == "Board" || name == "Work Surface" ? 0.65f : -0.35f;
                    pad.position = new Vector3(station.position.x + sign * x, pad.position.y, pad.position.z);
                }
                var anchor = station.Find("Station Focus");
                if (anchor == null)
                {
                    var node = new GameObject("Station Focus");
                    Undo.RegisterCreatedObjectUndo(node, "Station focus D-012");
                    anchor = node.transform;
                    anchor.SetParent(station, false);
                }
                Undo.RecordObject(anchor, "Station focus D-012");
                anchor.position = new Vector3(station.position.x + sign * 0.55f,
                    station.GetComponent<BoxCollider>().bounds.max.y + 0.12f, station.position.z);
                var target = station.GetComponent<PrototypeInteractable>();
                Undo.RecordObject(target, "Station focus D-012");
                target.FocusPoint = anchor;
                EditorUtility.SetDirty(target);
            }
            var bootstrap = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<GameBootstrap>(true)).Single();
            var player = bootstrap.Player;
            var actor = player.GetComponent<ChefShow.Contestants.PrototypeActor>();
            var ownStation = stations.Single(t => t.name == "Station_" + actor.StableId);
            float side = actor.Team == TeamId.A ? -1 : 1;
            Undo.RecordObject(player.transform, "Close kitchen view D-012");
            player.transform.position = new Vector3(ownStation.position.x + side * PlayerDistance,
                player.transform.position.y, player.transform.position.z);
            var camera = player.ViewCamera;
            Undo.RecordObjects(new UnityEngine.Object[] { camera, camera.transform, player }, "Close kitchen view D-012");
            camera.fieldOfView = NormalFieldOfView;
            camera.transform.localRotation = Quaternion.Euler(InitialPitch, 0, 0);
            var serialized = new SerializedObject(player);
            serialized.FindProperty("focusCameraOffset").vector3Value = new Vector3(0, -0.18f, 0.22f);
            serialized.FindProperty("focusFieldOfView").floatValue = 52;
            serialized.FindProperty("focusTransitionSeconds").floatValue = 0.18f;
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(player);
            Physics.SyncTransforms();
        }
    }
}
