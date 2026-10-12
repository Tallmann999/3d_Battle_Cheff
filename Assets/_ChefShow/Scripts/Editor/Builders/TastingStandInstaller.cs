using System;
using System.Linq;
using ChefShow.Core;
using ChefShow.Data;
using ChefShow.Inventory;
using ChefShow.Player;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ChefShow.Editor
{
    public static class TastingStandInstaller
    {
        [MenuItem("Tools/Chef Show/Install Tasting Stands")]
        public static void Install() => HandServingInstaller.UpdateScenes(AddToScene, "tasting-stands");
        public static void InstallLabelFacing() => HandServingInstaller.UpdateScenes(AlignLabels, "tasting-label-facing");

        private static void AlignLabels(Scene scene)
        {
            var bootstrap=scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<GameBootstrap>(true)).Single();
            foreach(var station in bootstrap.Serving.Stations)
            {
                if(station.TastingStatus==null)throw new InvalidOperationException("Нет подписи подставки: "+station.StationId);
                Undo.RecordObject(station.TastingStatus.transform,"Tasting label faces the centre aisle");
                station.TastingStatus.transform.rotation=Quaternion.Euler(0,station.StationId.StartsWith("A")?-90:90,0);
            }
        }

        public static void AddToScene(Scene scene)
        {
            var bootstrap = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<GameBootstrap>(true)).Single();
            var wood = HandServingInstaller.Material("Tasting Stand Wood", new Color(.28f, .21f, .15f));
            var topMaterial = HandServingInstaller.Material("Tasting Stand Top", new Color(.78f, .69f, .47f));
            foreach (var station in bootstrap.Serving.Stations.OrderBy(s => s.StationId))
            {
                var table = station.GetComponentsInParent<Transform>()
                    .Single(transform => transform.name == "Station_" + station.StationId);
                float side = station.StationId.StartsWith("A") ? -1 : 1;
                var stand = table.Find("Tasting Stand");
                if (stand == null)
                {
                    // Inner table edge is at |x|=6.85m. The .82m stand stays .19m clear;
                    // its centre-facing edge leaves 1.34m beside the pantry's |x|=4.5m edge.
                    // The Z offset keeps each stand inside the row end and away from its focus centre.
                    var point = new Vector3(table.position.x - side * 1.75f, 0, table.position.z - side * .70f);
                    stand = HandServingInstaller.Node("Tasting Stand", table, point);
                    HandServingInstaller.Shape("Pedestal Base", stand, PrimitiveType.Cube,
                        point + Vector3.up * .055f, new Vector3(.50f, .11f, .50f), wood);
                    HandServingInstaller.Shape("Pedestal Stem", stand, PrimitiveType.Cube,
                        point + Vector3.up * .48f, new Vector3(.14f, .75f, .14f), wood);
                    HandServingInstaller.Shape("Tasting Surface", stand, PrimitiveType.Cube,
                        point + Vector3.up * .895f, new Vector3(.82f, .08f, .82f), topMaterial);
                }

                var mount = stand.Find("Tasting Mount");
                if (mount == null)
                    mount = HandServingInstaller.Node("Tasting Mount", stand, stand.position + Vector3.up * .97f);
                var target = mount.GetComponent<ServingTarget>();
                if (target == null) target = Undo.AddComponent<ServingTarget>(mount.gameObject);
                Undo.RecordObject(target, "Saved tasting target");
                target.Station = station;
                target.Index = -1;
                target.Submit = false;
                target.ResetSubmission = false;
                var interaction = mount.GetComponent<PrototypeInteractable>();
                if (interaction == null) interaction = Undo.AddComponent<PrototypeInteractable>(mount.gameObject);
                Undo.RecordObject(interaction, "Tasting stand interaction");
                interaction.DisplayName = "Подставка дегустации · " + station.StationId;
                var collider = mount.GetComponent<BoxCollider>();
                if (collider == null)
                {
                    collider = Undo.AddComponent<BoxCollider>(mount.gameObject);
                    collider.center = new Vector3(0, .035f, 0);
                    collider.size = new Vector3(.82f, .12f, .82f);
                    collider.isTrigger = true;
                }
                else if (!collider.isTrigger)
                    throw new InvalidOperationException("У подставки найден твёрдый коллайдер: " + station.StationId);

                var plate = mount.Find("Tasting Plate");
                var plateView = plate == null ? null : plate.GetComponent<DishwareView>();
                if (plateView == null)
                {
                    if (plate != null) throw new InvalidOperationException("Tasting Plate без DishwareView: " + station.StationId);
                    plate = HandServingInstaller.Node("Tasting Plate", mount, mount.position);
                    plateView = BuildDishwareView(plate);
                    plateView.Present(null);
                }
                var labelRoot = stand.Find("Label");
                var label = labelRoot == null ? null : labelRoot.GetComponent<TextMesh>();
                if (label == null)
                {
                    if (labelRoot != null) throw new InvalidOperationException("Подпись подставки без TextMesh: " + station.StationId);
                    label = HandServingInstaller.Label("ДЕГУСТАЦИЯ · " + station.StationId, stand,
                        stand.position + new Vector3(-side * .43f, 1.11f, 0), -side, .0045f);
                }
                Undo.RecordObject(station, "Saved tasting stand references");
                station.TastingMount = mount;
                station.TastingPlateView = plateView;
                station.TastingTarget = target;
                station.TastingStatus = label;
                EditorUtility.SetDirty(station);
                EditorUtility.SetDirty(target);
                EditorUtility.SetDirty(interaction);
                EditorUtility.SetDirty(collider);
                PreserveInactiveButtons(station);
            }
            UpdateInstructions(bootstrap);
            Physics.SyncTransforms();
        }

        private static DishwareView BuildDishwareView(Transform plate)
        {
            var view = Undo.AddComponent<DishwareView>(plate.gameObject);
            var material = HandServingInstaller.Material("Dishware", Color.white);
            view.Base = HandServingInstaller.Shape("Dishware Base", plate, PrimitiveType.Sphere,
                plate.position, Vector3.one, material).GetComponent<Renderer>();
            view.Rim = Enumerable.Range(0, 12).Select(index =>
                HandServingInstaller.Shape("Dishware Rim " + (index + 1), plate, PrimitiveType.Cube,
                    plate.position, Vector3.one, material).GetComponent<Renderer>()).ToArray();
            EditorUtility.SetDirty(view);
            return view;
        }

        private static void PreserveInactiveButtons(ServingStation station)
        {
            var content = station.transform.Find("Plate Contents");
            if (content == null) throw new InvalidOperationException("Нет исходного Plate Contents: " + station.StationId);
            foreach (string name in new[] { "Submit Dish", "Reset Submission" })
            {
                var button = content.Find(name);
                if (button == null) throw new InvalidOperationException("Нет исходной кнопки " + name + ": " + station.StationId);
                if (!button.gameObject.activeSelf) continue;
                Undo.RecordObject(button.gameObject, "Preserve inactive legacy submission button");
                button.gameObject.SetActive(false);
                EditorUtility.SetDirty(button.gameObject);
            }
        }

        private static void UpdateInstructions(GameBootstrap bootstrap)
        {
            var task = bootstrap.Hud.TaskCard;
            if (task.text.Contains("ДЕГУСТАЦИЯ:")) return;
            Undo.RecordObject(task, "Tasting stand instructions");
            task.text = string.Join("\n", task.text.Split('\n').Where(line =>
                !line.Contains("красной кноп") && !line.Contains("зелёной кноп")
                && !line.Contains("Синяя кноп") && !line.Contains("синяя кноп")
                && !line.Contains("синюю кноп")));
            task.text += "\nДЕГУСТАЦИЯ: ЛКМ по своей тарелке — взять вместе с едой."
                + "\nОбойдите ряд; ЛКМ по своей подставке — выставить блюдо."
                + "\nЛКМ по блюду на подставке — забрать до 00:00."
                + "\nЛКМ по мусору с тарелкой — выкинуть всю тарелку вместе с едой."
                + "\nНа 00:00 неподанное блюдо оценивается с рабочей тарелки; тарелка в руке исключена."
                + "\nВыставленное на подставку блюдо уже подано; его оценка сохраняется.";
            EditorUtility.SetDirty(task);
        }
    }
}
