using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ChefShow.Editor
{
    /// <summary>Явная правка D-010: столешницы в 90 см от пола, без регенерации сцен.</summary>
    public static class TableHeightUpdate
    {
        public const float Height = 0.9f;
        public const float LabelLocalY = (Height / 2 + 0.3f) / Height;
        public const float MarkerHeight = 0.54f;
        public const float MarkerCenterY = 0.51f;

        [MenuItem("Tools/Chef Show/Set Table Height 90 cm")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("Сначала остановите Play Mode и дождитесь компиляции.");
            var working = SceneManager.GetActiveScene();
            if (working.path != PrototypeSceneBuilder.EditableScene || working.isDirty)
                throw new InvalidOperationException("Откройте сохранённую рабочую ChefShow_Prototype.unity.");
            var generated = SceneManager.GetSceneByPath(PrototypeSceneBuilder.GeneratedScene);
            bool openedGenerated = !generated.IsValid() || !generated.isLoaded;
            if (!openedGenerated && generated.isDirty)
                throw new InvalidOperationException("Сначала сохраните свои изменения generated-сцены.");
            if (openedGenerated) generated = EditorSceneManager.OpenScene(PrototypeSceneBuilder.GeneratedScene, OpenSceneMode.Additive);
            try
            {
                // Проверяем обе сцены до изменения, сохраняем исходные файлы для восстановления.
                foreach (var scene in new[] { working, generated }) Tables(scene);
                Directory.CreateDirectory("TestResults");
                string stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
                foreach (var scene in new[] { working, generated })
                    File.Copy(scene.path, "TestResults/height-before-" + scene.name + "-" + stamp + ".unity", false);
                foreach (var scene in new[] { working, generated })
                {
                    LowerTables(scene);
                    PrototypeValidator.ValidateScene(scene);
                    // Завершить отложенную запись Undo до сохранения, чтобы сцена
                    // не стала dirty снова на следующем update Editor.
                    Undo.FlushUndoRecordObjects();
                    EditorSceneManager.MarkSceneDirty(scene);
                    if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Не удалось сохранить высоту столов: " + scene.path);
                }
                File.WriteAllText("TestResults/height-applied.txt", "D-010 APPLIED " + DateTime.UtcNow.ToString("O") + "\n14 tables: top 0.9 m; player unchanged.");
                Debug.Log("Chef Show D-010: все 14 столешниц на высоте 0.9 м; рост игрока сохранён.");
            }
            finally
            {
                SceneManager.SetActiveScene(working);
                if (openedGenerated) EditorSceneManager.CloseScene(generated, true);
            }
        }

        private static Transform[] Tables(Scene scene)
        {
            var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
            var tables = all.Where(t => t.name.StartsWith("Station_", StringComparison.Ordinal)
                || t.name == "Pantry" || t.name == "Judging Table").ToArray();
            if (tables.Length != 14 || tables.Any(t => t.GetComponent<BoxCollider>() == null)
                || all.Count(t => t.name == "Floor") != 1 || all.Single(t => t.name == "Floor").GetComponent<BoxCollider>() == null)
                throw new InvalidOperationException("Нужны 12 станций, pantry, стол дегустации и пол: " + scene.path);
            return tables;
        }

        private static void LowerTables(Scene scene)
        {
            var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
            Undo.RecordObjects(all, "Table height D-010");
            float floorY = all.Single(t => t.name == "Floor").GetComponent<BoxCollider>().bounds.max.y;
            float judgingDelta = 0;
            foreach (var table in Tables(scene))
            {
                float oldTop = table.GetComponent<BoxCollider>().bounds.max.y;
                float delta = floorY + Height - oldTop;
                if (table.name == "Judging Table") judgingDelta = delta;
                // Содержимое сохраняет мировой размер и опускается вместе со столешницей.
                var children = table.Cast<Transform>().Select(t => new { Target = t, Position = t.position, Size = t.lossyScale }).ToArray();
                var size = table.lossyScale;
                table.position = new Vector3(table.position.x, floorY + Height / 2, table.position.z);
                WorldSize(table, new Vector3(size.x, Height, size.z));
                foreach (var child in children)
                {
                    child.Target.position = child.Position + Vector3.up * delta;
                    WorldSize(child.Target, child.Size);
                }
                var label = table.Find("Label");
                if (label != null) label.position = new Vector3(label.position.x, floorY + Height + 0.3f, label.position.z);
                var marker = table.Find("Team Marker");
                if (marker != null)
                {
                    marker.position = new Vector3(marker.position.x, floorY + MarkerCenterY, marker.position.z);
                    WorldSize(marker, new Vector3(marker.lossyScale.x, MarkerHeight, marker.lossyScale.z));
                }
            }
            foreach (var group in all.Where(t => t.name == "TeamDishSlots" || t.name == "FinalDishSlots"))
                foreach (Transform slot in group) slot.position += Vector3.up * judgingDelta;
            Physics.SyncTransforms();
        }

        private static void WorldSize(Transform target, Vector3 size)
        {
            var parent = target.parent == null ? Vector3.one : target.parent.lossyScale;
            target.localScale = new Vector3(size.x / parent.x, size.y / parent.y, size.z / parent.z);
        }
    }
}
