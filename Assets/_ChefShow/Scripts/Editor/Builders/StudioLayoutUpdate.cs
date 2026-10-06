using System;
using System.IO;
using System.Linq;
using ChefShow.Contestants;
using ChefShow.Core;
using ChefShow.Data;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ChefShow.Editor
{
    /// <summary>Явная пространственная правка D-008 без замены рабочей сцены.</summary>
    public static class StudioLayoutUpdate
    {
        [MenuItem("Tools/Chef Show/Apply Studio Layout Update")]
        public static void ApplyToWorkingScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("Сначала остановите Play Mode и дождитесь компиляции.");
            var scene = SceneManager.GetActiveScene();
            if (scene.path != PrototypeSceneBuilder.EditableScene)
                throw new InvalidOperationException("Откройте рабочую ChefShow_Prototype.unity перед пространственной правкой.");
            if (scene.isDirty)
                throw new InvalidOperationException("Сначала сохраните свои изменения рабочей сцены; правка не закрывает несохранённые сцены.");
            var transforms = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
            Transform Named(string name) => transforms.Single(t => t.name == name);
            var bootstrap = transforms.Select(t => t.GetComponent<GameBootstrap>()).Single(b => b != null);
            var actors = transforms.Select(t => t.GetComponent<PrototypeActor>()).Where(a => a != null).ToArray();
            // Проверить все обязательные объекты до первой мутации.
            foreach (string name in new[] { "Floor", "Back Wall", "Front Wall", "Pantry", "Judging Table", "TeamDishSlots", "FinalDishSlots" }) Named(name);
            foreach (var actor in actors.Where(a => a.Kind != PrototypeActorKind.Chef)) Named("Station_" + actor.StableId);
            if (transforms.Count(t => t.name == "Side Wall") != 2) throw new InvalidOperationException("Нужны две боковые стены.");
            if (actors.Count(a => a.Kind != PrototypeActorKind.Chef) != 12) throw new InvalidOperationException("Нужны 12 участников.");
            Directory.CreateDirectory("TestResults");
            File.Copy(scene.path, "TestResults/layout-working-before-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + ".unity", false);

            Undo.RecordObjects(transforms.Cast<UnityEngine.Object>().Concat(new UnityEngine.Object[] { bootstrap.Config })
                .Concat(actors.Select(a => (UnityEngine.Object)a.GetComponent<Collider>()).Where(c => c != null)).ToArray(), "Studio layout D-008");
            bootstrap.Config.ArenaWidth = 60;
            bootstrap.Config.ArenaDepth = 44;
            EditorUtility.SetDirty(bootstrap.Config);
            WorldSize(Named("Floor"), new Vector3(60, 0.3f, 44));
            foreach (string name in new[] { "Back Wall", "Front Wall" })
            {
                var wall = Named(name);
                wall.position = new Vector3(wall.position.x, wall.position.y, name == "Back Wall" ? 22 : -22);
                WorldSize(wall, new Vector3(60, 5, 0.3f));
            }
            foreach (var wall in transforms.Where(t => t.name == "Side Wall"))
            {
                wall.position = new Vector3(Mathf.Sign(wall.position.x) * 30, wall.position.y, wall.position.z);
                WorldSize(wall, new Vector3(0.3f, 5, 44));
            }
            foreach (var actor in actors.Where(a => a.Kind != PrototypeActorKind.Chef))
            {
                float sign = actor.Team == TeamId.A ? -1 : 1;
                var station = Named("Station_" + actor.StableId);
                // Центры всех столов сохраняются: шаг ряда остаётся прежним.
                WorldSize(station, new Vector3(2.3f, station.lossyScale.y, 2.3f));
                var marker = station.Find("Team Marker");
                if (marker != null)
                {
                    marker.position = new Vector3(station.position.x + sign * 1.2f, station.GetComponent<BoxCollider>().bounds.min.y + TableHeightUpdate.MarkerCenterY, station.position.z);
                    WorldSize(marker, new Vector3(0.1f, TableHeightUpdate.MarkerHeight, 1.8f));
                }
                foreach (var label in station.GetComponentsInChildren<TextMesh>(true))
                {
                    WorldSize(label.transform, Vector3.one);
                    label.transform.localPosition = new Vector3(sign * 0.1f, TableHeightUpdate.LabelLocalY, 0);
                    label.transform.rotation = Quaternion.Euler(0, -sign * 90, 0);
                }
                actor.transform.position = new Vector3(station.position.x + sign * 2, actor.transform.position.y, station.position.z);
                actor.transform.rotation = Quaternion.Euler(0, -sign * 90, 0);
                if (actor.Kind == PrototypeActorKind.Npc) actor.GetComponent<Collider>().enabled = true;
            }
            MoveZ(Named("Pantry"), 12);
            float judgingDelta = -12 - Named("Judging Table").position.z;
            MoveZ(Named("Judging Table"), -12);
            foreach (string group in new[] { "TeamDishSlots", "FinalDishSlots" })
                foreach (Transform slot in Named(group)) slot.position += Vector3.forward * judgingDelta;
            foreach (var chef in actors.Where(a => a.Kind == PrototypeActorKind.Chef)) MoveZ(chef.transform, -10);
            Physics.SyncTransforms();
            PrototypeValidator.ValidateScene(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Не удалось сохранить пространственную правку.");
            AssetDatabase.SaveAssets();
            // Регенерация только по этой явной команде. Рабочая сцена не заменяется.
            PrototypeSceneBuilder.BuildPrototypeScene();
            var generated = SceneManager.GetSceneByPath(PrototypeSceneBuilder.GeneratedScene);
            SceneManager.SetActiveScene(scene);
            if (generated.IsValid() && generated.isLoaded) EditorSceneManager.CloseScene(generated, true);
            Debug.Log("Chef Show layout D-008: APPLIED — 60 × 44 м, квадратные столы 2.3 м, участники снаружи, обходы с двух концов.");
            File.WriteAllText("TestResults/layout-applied.txt", "D-008 APPLIED " + DateTime.UtcNow.ToString("O") + "\n" + scene.path);
        }

        private static void MoveZ(Transform target, float z) => target.position = new Vector3(target.position.x, target.position.y, z);
        private static void WorldSize(Transform target, Vector3 size)
        {
            var parent = target.parent == null ? Vector3.one : target.parent.lossyScale;
            target.localScale = new Vector3(size.x / parent.x, size.y / parent.y, size.z / parent.z);
        }
    }
}
