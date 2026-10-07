using System;
using System.IO;
using System.Linq;
using ChefShow.Core;
using ChefShow.Ingredients;
using ChefShow.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ChefShow.Editor
{
    public static class ToolDrawerInstaller
    {
        [MenuItem("Tools/Chef Show/Install Tool Drawer Stage")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("Нужен Edit Mode после компиляции.");
            var working = SceneManager.GetActiveScene();
            if (working.path != PrototypeSceneBuilder.EditableScene || working.isDirty)
                throw new InvalidOperationException("Сохраните рабочую ChefShow_Prototype.unity.");
            var generated = SceneManager.GetSceneByPath(PrototypeSceneBuilder.GeneratedScene);
            bool opened = !generated.IsValid() || !generated.isLoaded;
            if (!opened && generated.isDirty) throw new InvalidOperationException("Сохраните generated-сцену.");
            if (opened) generated = EditorSceneManager.OpenScene(PrototypeSceneBuilder.GeneratedScene, OpenSceneMode.Additive);
            try
            {
                Directory.CreateDirectory("TestResults");
                string stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
                foreach (var scene in new[] { working, generated })
                    File.Copy(scene.path, "TestResults/drawer-before-" + scene.name + "-" + stamp + ".unity", false);
                foreach (var scene in new[] { working, generated })
                {
                    SceneManager.SetActiveScene(scene);
                    AddToScene(scene);
                    PrototypeValidator.ValidateScene(scene);
                    Undo.FlushUndoRecordObjects();
                    EditorSceneManager.MarkSceneDirty(scene);
                    if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Не удалось сохранить " + scene.path);
                }
                File.WriteAllText("TestResults/drawer-installed.txt", "F-005 DRAWERS INSTALLED " + DateTime.UtcNow.ToString("O"));
                Debug.Log("Chef Show F-005: 12 ящиков, прямое взаимодействие E и физические инструменты сохранены. Нарезка ещё не подключена.");
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
            // Поднятая автором камера должна доставать до дальнего края своего лотка.
            if (bootstrap.Player.ViewCamera.transform.localPosition.y > 2 && bootstrap.Config.InteractionDistance < 3.1f)
            {
                Undo.RecordObject(bootstrap.Config, "Interaction reach for raised camera");
                bootstrap.Config.InteractionDistance = 3.1f;
                EditorUtility.SetDirty(bootstrap.Config);
                AssetDatabase.SaveAssetIfDirty(bootstrap.Config);
            }
            if (bootstrap.Tools != null)
            {
                UpgradeVisuals(bootstrap.Tools);
                UpgradeDirectInteraction(bootstrap.Tools);
                string error = bootstrap.Tools.Validate();
                if (error != null) throw new InvalidOperationException(error);
                return; // Повтор установки не сбрасывает ручные позиции/высоту/предпросмотр.
            }
            if (bootstrap.Inventory == null) throw new InvalidOperationException("Сначала установите F-004.");
            var stations = roots.SelectMany(r => r.GetComponentsInChildren<Transform>(true))
                .Where(t => t.name.StartsWith("Station_", StringComparison.Ordinal)).ToArray();
            if (stations.Length != 12 || stations.Any(s => s.Find("Team Marker") == null))
                throw new InvalidOperationException("Нужны 12 станций с Team Marker.");
            var metal = AssetDatabase.LoadAssetAtPath<Material>(PrototypeSceneBuilder.Root + "/Generated/Materials/Neutral.mat");
            var wood = AssetDatabase.LoadAssetAtPath<Material>(PrototypeSceneBuilder.Root + "/Generated/Materials/Basket.mat");
            if (metal == null || wood == null) throw new InvalidOperationException("Нет материалов Neutral/Basket.");
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            PrototypeSceneBuilder.RequireCyrillic(font);
            var controller = Undo.AddComponent<ToolDrawerController>(bootstrap.gameObject);
            Undo.RecordObject(bootstrap, "Install tool drawers"); bootstrap.Tools = controller;
            controller.Drawers = stations.Select(station =>
            {
                float sign = station.name.StartsWith("Station_A", StringComparison.Ordinal) ? -1 : 1;
                var front = station.Find("Team Marker");
                var drawer = Undo.AddComponent<ToolDrawer>(front.gameObject);
                drawer.StationId = station.name.Substring("Station_".Length);
                var target = Undo.AddComponent<PrototypeInteractable>(front.gameObject);
                target.DisplayName = "Ящик с инструментами";
                target.Description = "E — открыть / закрыть ящик";
                // Сохраняем исходную лицевую панель, цвет, габариты и Collider.
                var group = Node("Tool Drawer Contents", front, new Vector3(front.position.x - sign * .18f, .7f, front.position.z));
                var tray = Node("Sliding Tray", group, group.position);
                drawer.Tray = tray; drawer.ClosedPosition = Vector3.zero; drawer.OpenOffset = new Vector3(sign * .28f, 0, 0);
                Shape("Tray Bottom", PrimitiveType.Cube, tray, new Vector3(0, -.04f, 0), new Vector3(.42f, .04f, 1.65f), wood);
                Shape("Handle", PrimitiveType.Cube, front, Vector3.zero, new Vector3(.035f, .04f, .3f), metal,
                    new Vector3(front.position.x + sign * .07f, .68f, front.position.z));
                drawer.Tools = Enumerable.Range(0, 4).Select(i =>
                    BuildTool((KitchenToolKind)(i + 1), tray, new Vector3(0, 0, -.6f + i * .4f), metal, wood)).ToArray();
                var labelNode = Node("Drawer Label", front, new Vector3(front.position.x + sign * .06f, .46f, front.position.z));
                labelNode.rotation = Quaternion.Euler(0, -sign * 90, 0);
                var label = Undo.AddComponent<TextMesh>(labelNode.gameObject);
                label.font = font; label.fontSize = 48; label.characterSize = .02f;
                label.anchor = TextAnchor.MiddleCenter; label.text = "ИНСТРУМЕНТЫ";
                labelNode.GetComponent<MeshRenderer>().sharedMaterial = font.material;
                EditorUtility.SetDirty(drawer);
                return drawer;
            }).ToArray();
            UpgradeVisuals(controller);
            UpgradeDirectInteraction(controller);
            string validation = controller.Validate(); if (validation != null) throw new InvalidOperationException(validation);
            Physics.SyncTransforms();
        }

        private static void UpgradeVisuals(ToolDrawerController controller)
        {
            var metal = AssetDatabase.LoadAssetAtPath<Material>(PrototypeSceneBuilder.Root + "/Generated/Materials/Neutral.mat");
            var wood = AssetDatabase.LoadAssetAtPath<Material>(PrototypeSceneBuilder.Root + "/Generated/Materials/Basket.mat");
            if (metal == null || wood == null) throw new InvalidOperationException("Нет материалов ящика.");
            foreach (var drawer in controller.Drawers)
            {
                if (drawer.LayoutVersion >= 2) continue;
                Undo.RecordObject(drawer, "Upgrade sliding drawer");
                drawer.ShowOpen(false);
                var front = drawer.transform;
                var station = front.parent;
                float sign = drawer.StationId.StartsWith("A", StringComparison.Ordinal) ? -1 : 1;
                var facePosition = front.position;
                var group = front.Find("Tool Drawer Contents");
                var tray = drawer.Tray;
                var handle = front.Find("Handle");
                var label = front.Find("Drawer Label");
                // Сохраняем Team Marker, его GUID, материал и компонент взаимодействия.
                // Переносим нормализованную группу под станцию, затем панель под лоток.
                Undo.SetTransformParent(group, station, "Sliding drawer body");
                Undo.RecordObject(group, "Sliding drawer body");
                group.position = facePosition + new Vector3(-sign * .30f, .19f, 0);
                group.rotation = Quaternion.identity;
                group.localScale = new Vector3(1 / station.lossyScale.x, 1 / station.lossyScale.y, 1 / station.lossyScale.z);
                Undo.RecordObject(tray, "Sliding drawer tray");
                tray.localPosition = Vector3.zero; tray.localRotation = Quaternion.identity; tray.localScale = Vector3.one;
                Undo.SetTransformParent(front, tray, "Moving drawer front");
                Undo.RecordObject(front, "Moving drawer front"); front.position = facePosition;
                if (handle != null) { Undo.RecordObject(handle, "Drawer handle"); handle.position = facePosition + new Vector3(sign * .07f, .12f, 0); }
                if (label != null)
                {
                    Undo.SetTransformParent(label, tray, "Drawer label");
                    Undo.RecordObject(label, "Drawer label");
                    label.position = facePosition + new Vector3(sign * .056f, -.06f, 0);
                    label.rotation = Quaternion.Euler(0, -sign * 90, 0); label.localScale = Vector3.one;
                }
                var bottom = tray.Find("Tray Bottom");
                Undo.RecordObject(bottom, "Drawer bottom");
                bottom.localPosition = new Vector3(0, -.04f, 0); bottom.localScale = new Vector3(.56f, .04f, 1.68f);
                Shape("Back Wall", PrimitiveType.Cube, tray, new Vector3(-sign * .27f, .015f, 0), new Vector3(.02f, .11f, 1.68f), wood);
                Shape("Left Wall", PrimitiveType.Cube, tray, new Vector3(0, .015f, -.83f), new Vector3(.56f, .11f, .02f), wood);
                Shape("Right Wall", PrimitiveType.Cube, tray, new Vector3(0, .015f, .83f), new Vector3(.56f, .11f, .02f), wood);
                drawer.Compartments = new Transform[4];
                for (int i = 0; i < 4; i++)
                {
                    float z = -.615f + i * .41f;
                    var cell = Node("Cell " + (i + 1) + " - " + ToolDrawerController.Name((KitchenToolKind)(i + 1)), tray,
                        tray.TransformPoint(new Vector3(0, 0, z)));
                    Shape("Cell Bottom", PrimitiveType.Cube, cell, new Vector3(0, -.013f, 0), new Vector3(.50f, .012f, .37f), metal);
                    if (i < 3) Shape("Divider " + (i + 1), PrimitiveType.Cube, tray, new Vector3(0, .005f, z + .205f), new Vector3(.54f, .09f, .02f), wood);
                    var tool = drawer.Tools[i];
                    Undo.SetTransformParent(tool, cell, "Tool in compartment");
                    Undo.RecordObject(tool, "Tool in compartment");
                    tool.localPosition = Vector3.zero; tool.localRotation = Quaternion.identity; tool.localScale = Vector3.one;
                    drawer.Compartments[i] = cell;
                }
                drawer.ClosedPosition = Vector3.zero;
                drawer.OpenOffset = new Vector3(sign * .72f, 0, 0);
                drawer.SlideSeconds = .3f; drawer.LayoutVersion = 2;
                drawer.ShowOpen(drawer.PreviewOpen); EditorUtility.SetDirty(drawer);
            }
            EditorUtility.SetDirty(controller); Physics.SyncTransforms();
        }


        private static void UpgradeDirectInteraction(ToolDrawerController controller)
        {
            var bootstrap = controller.GetComponent<GameBootstrap>();
            var camera = bootstrap.Player.ViewCamera.transform;
            var oldMenu = bootstrap.Hud.transform.Find("Tool Drawer Menu");
            if (oldMenu != null) Undo.DestroyObjectImmediate(oldMenu.gameObject);
            var oldHands = camera.Find("Held Kitchen Tools");
            if (oldHands != null) Undo.DestroyObjectImmediate(oldHands.gameObject);
            Undo.RecordObject(controller, "Direct tool interaction references");
            if (controller.RightHand == null)
            {
                controller.RightHand = Node("Right Hand - Kitchen Tool", camera, camera.TransformPoint(new Vector3(.28f, -.25f, .58f)));
                controller.RightHand.localRotation = Quaternion.Euler(0, -75, -20);
            }
            if (controller.LeftFoodHand == null)
            {
                controller.LeftFoodHand = Node("Left Hand - Food", camera, camera.TransformPoint(new Vector3(-.28f, -.25f, .58f)));
                controller.LeftFoodHand.localRotation = Quaternion.identity;
            }
            foreach (var drawer in controller.Drawers)
            {
                var bottom = drawer.Tray.Find("Tray Bottom").gameObject;
                Undo.RecordObject(bottom, "Drawer bottom raycast layer"); bottom.layer = 0;
                var bottomCollider = bottom.GetComponent<BoxCollider>();
                if (bottomCollider == null) bottomCollider = Undo.AddComponent<BoxCollider>(bottom);
                Undo.RecordObject(bottomCollider, "Drawer bottom interaction"); bottomCollider.isTrigger = true;
                var bottomTarget = bottom.GetComponent<ToolDrawerTarget>();
                if (bottomTarget == null) bottomTarget = Undo.AddComponent<ToolDrawerTarget>(bottom);
                Undo.RecordObject(bottomTarget, "Drawer bottom reference"); bottomTarget.Drawer = drawer;
                var bottomPrompt = bottom.GetComponent<PrototypeInteractable>();
                if (bottomPrompt == null) bottomPrompt = Undo.AddComponent<PrototypeInteractable>(bottom);
                Undo.RecordObject(bottomPrompt, "Drawer bottom prompt"); bottomPrompt.DisplayName = "Ящик с инструментами";
                bottomPrompt.Description = "E — открыть / закрыть ящик";
                EditorUtility.SetDirty(bottomCollider); EditorUtility.SetDirty(bottomTarget); EditorUtility.SetDirty(bottomPrompt);
                var panelCollider = drawer.GetComponent<BoxCollider>();
                Undo.RecordObject(panelCollider, "Raycastable drawer panel"); panelCollider.isTrigger = true; panelCollider.enabled = true;
                for (int i = 0; i < drawer.Tools.Length; i++)
                {
                    var root = drawer.Tools[i].gameObject;
                    Undo.RecordObject(root, "Tool raycast layer"); root.layer = 0;
                    var tool = root.GetComponent<KitchenTool>(); if (tool == null) tool = Undo.AddComponent<KitchenTool>(root);
                    Undo.RecordObject(tool, "Physical tool references");
                    tool.Kind = (KitchenToolKind)(i + 1); tool.Drawer = drawer;
                    tool.PickupCollider = root.GetComponent<BoxCollider>(); if (tool.PickupCollider == null) tool.PickupCollider = Undo.AddComponent<BoxCollider>(root);
                    Undo.RecordObject(tool.PickupCollider, "Tool pickup bounds");
                    tool.PickupCollider.center = new Vector3(0, .025f, 0); tool.PickupCollider.size = new Vector3(.43f, .08f, .30f);
                    tool.PickupCollider.isTrigger = true; tool.PickupCollider.enabled = true;
                    tool.Body = root.GetComponent<Rigidbody>(); if (tool.Body == null) tool.Body = Undo.AddComponent<Rigidbody>(root);
                    Undo.RecordObject(tool.Body, "Tool physics"); tool.Body.isKinematic = true; tool.Body.mass = .15f;
                    tool.Body.interpolation = RigidbodyInterpolation.None;
                    tool.Body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                    var target = root.GetComponent<PrototypeInteractable>(); if (target == null) target = Undo.AddComponent<PrototypeInteractable>(root);
                    Undo.RecordObject(target, "Tool prompt"); target.DisplayName = ToolDrawerController.Name(tool.Kind);
                    target.Description = "E — взять в правую руку · G — уронить";
                    EditorUtility.SetDirty(tool); EditorUtility.SetDirty(tool.Body); EditorUtility.SetDirty(tool.PickupCollider); EditorUtility.SetDirty(target);
                }
                EditorUtility.SetDirty(panelCollider);
            }
            var hud = bootstrap.Hud;
            var canvas = hud.GetComponent<Canvas>();
            Undo.RecordObject(canvas, "HUD in front of held objects"); canvas.planeDistance = Mathf.Max(.12f, bootstrap.Player.ViewCamera.nearClipPlane + .02f);
            EditorUtility.SetDirty(canvas);
            Undo.RecordObject(hud, "Interaction badge reference");
            if (hud.InteractionKey == null)
                hud.InteractionKey = Text("Interaction Key E", hud.transform, new Vector2(350, -558), new Vector2(52, 48), 30, "[E]", hud.Context.font);
            hud.InteractionKey.enabled = false;
            ConfigureInteractionPrompt(hud);
            Undo.RecordObject(hud.Status.rectTransform, "Tool hand status height");
            hud.Status.rectTransform.sizeDelta = new Vector2(hud.Status.rectTransform.sizeDelta.x, 150);
            var controls = hud.transform.Find("Controls").GetComponent<Text>();
            Undo.RecordObject(controls, "Direct interaction controls");
            Undo.RecordObject(controls.rectTransform, "Direct interaction controls layout");
            Rect(controls.rectTransform, new Vector2(20, -645), new Vector2(1240, 70)); controls.fontSize = 17;
            controls.text = "WASD — ходьба · мышь — обзор · Shift — бег\nE — действие по прицелу · G — уронить инструмент / корзину · Tab — корзина · Q — задание · RMB — возврат · Esc — пауза";
            Undo.RecordObject(hud.TaskCard, "Direct drawer instructions");
            hud.TaskCard.text = "Подготовка кухни\nTab — взять/поставить корзину; ЛКМ — набрать/выгрузить.\nE по продукту — взять/положить; название видно под прицелом.\nE по панели — открыть/закрыть; E по инструменту — правая рука.\nПродукт переходит в левую руку. G — уронить инструмент, E — поднять.\nНарезка и распаковка — следующий срез F-005.";
            foreach (var dirty in new UnityEngine.Object[] { controller, hud, hud.Context, hud.InteractionKey, hud.TaskCard, controls }) EditorUtility.SetDirty(dirty);
            Physics.SyncTransforms();
        }

        [MenuItem("Tools/Chef Show/Move Interaction Prompt Above Crosshair")]
        public static void InstallPromptLayout()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("Нужен Edit Mode после компиляции.");
            var working = SceneManager.GetActiveScene();
            if (working.path != PrototypeSceneBuilder.EditableScene || working.isDirty)
                throw new InvalidOperationException("Сохраните рабочую ChefShow_Prototype.unity.");
            var generated = SceneManager.GetSceneByPath(PrototypeSceneBuilder.GeneratedScene);
            bool opened = !generated.IsValid() || !generated.isLoaded;
            if (!opened && generated.isDirty) throw new InvalidOperationException("Сохраните generated-сцену.");
            if (opened) generated = EditorSceneManager.OpenScene(PrototypeSceneBuilder.GeneratedScene, OpenSceneMode.Additive);
            try
            {
                Directory.CreateDirectory("TestResults");
                string stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
                foreach (var scene in new[] { working, generated })
                {
                    var owner = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<GameBootstrap>(true)).Single();
                    if (owner.Hud.InteractionKey == null || owner.Hud.Context == null)
                        throw new InvalidOperationException("Не настроена подсказка E.");
                    File.Copy(scene.path, "TestResults/prompt-before-" + scene.name + "-" + stamp + ".unity", false);
                    ConfigureInteractionPrompt(owner.Hud);
                    PrototypeValidator.ValidateScene(scene); Undo.FlushUndoRecordObjects();
                    EditorSceneManager.MarkSceneDirty(scene);
                    if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Не удалось сохранить " + scene.path);
                }
            }
            finally
            {
                SceneManager.SetActiveScene(working);
                if (opened) EditorSceneManager.CloseScene(generated, true);
            }
        }

        public static void ConfigureInteractionPrompt(ChefShow.UI.PrototypeHud hud)
        {
            var crosshair = hud.transform.Find("Crosshair").GetComponent<RectTransform>();
            Undo.RecordObject(crosshair, "Crosshair at screen center");
            CenterPrompt(crosshair, Vector2.zero, crosshair.sizeDelta);
            EditorUtility.SetDirty(crosshair);
            Undo.RecordObject(hud.InteractionKey, "Small E above crosshair");
            Undo.RecordObject(hud.InteractionKey.rectTransform, "E position above crosshair");
            CenterPrompt(hud.InteractionKey.rectTransform, new Vector2(0, 42), new Vector2(32, 26));
            hud.InteractionKey.fontSize = 18; hud.InteractionKey.alignment = TextAnchor.MiddleCenter;
            Undo.RecordObject(hud.Context, "Russian action above crosshair");
            Undo.RecordObject(hud.Context.rectTransform, "Action position above crosshair");
            CenterPrompt(hud.Context.rectTransform, new Vector2(0, 78), new Vector2(720, 46));
            hud.Context.fontSize = 18; hud.Context.alignment = TextAnchor.MiddleCenter;
            EditorUtility.SetDirty(hud.InteractionKey); EditorUtility.SetDirty(hud.Context);
        }

        private static void CenterPrompt(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = position; rect.sizeDelta = size;
        }

        private static Transform BuildTool(KitchenToolKind kind, Transform parent, Vector3 offset, Material metal, Material wood)
        {
            var root = Node(ToolDrawerController.Name(kind), parent, parent.TransformPoint(offset));
            root.localRotation = Quaternion.identity;
            var material = kind == KitchenToolKind.Spatula ? wood : metal;
            Shape("Handle", PrimitiveType.Cube, root, new Vector3(-.08f, .015f, 0), new Vector3(.2f, .025f, .028f), wood);
            if (kind == KitchenToolKind.Knife)
                Shape("Blade", PrimitiveType.Cube, root, new Vector3(.09f, .015f, 0), new Vector3(.18f, .02f, .075f), metal);
            else if (kind == KitchenToolKind.Fork)
            {
                Shape("Head", PrimitiveType.Cube, root, new Vector3(.055f, .015f, 0), new Vector3(.07f, .02f, .07f), metal);
                for (int i = 0; i < 3; i++) Shape("Prong " + (i + 1), PrimitiveType.Cube, root,
                    new Vector3(.12f, .015f, -.028f + .028f * i), new Vector3(.1f, .018f, .012f), metal);
            }
            else Shape("Head", kind == KitchenToolKind.Spoon ? PrimitiveType.Sphere : PrimitiveType.Cube,
                root, new Vector3(.09f, .015f, 0), new Vector3(.16f, .025f, .085f), material);
            return root;
        }

        private static Transform Node(string name, Transform parent, Vector3 position)
        {
            var node = new GameObject(name); Undo.RegisterCreatedObjectUndo(node, "Install tool drawers");
            node.transform.SetParent(parent, false); node.transform.position = position;
            node.transform.localScale = new Vector3(1 / parent.lossyScale.x, 1 / parent.lossyScale.y, 1 / parent.lossyScale.z);
            return node.transform;
        }
        private static void Shape(string name, PrimitiveType type, Transform parent, Vector3 offset, Vector3 scale, Material material, Vector3? worldPosition = null)
        {
            var shape = GameObject.CreatePrimitive(type); shape.name = name;
            Undo.RegisterCreatedObjectUndo(shape, "Tool drawer geometry");
            UnityEngine.Object.DestroyImmediate(shape.GetComponent<Collider>());
            shape.transform.SetParent(parent, false);
            shape.transform.position = worldPosition ?? parent.TransformPoint(offset);
            shape.transform.localScale = new Vector3(scale.x / parent.lossyScale.x, scale.y / parent.lossyScale.y, scale.z / parent.lossyScale.z);
            shape.GetComponent<Renderer>().sharedMaterial = material;
            shape.layer = LayerMask.NameToLayer("Ignore Raycast");
        }
        private static void Rect(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = position; rect.sizeDelta = size;
        }
        private static Text Text(string name, Transform parent, Vector2 position, Vector2 size, int fontSize, string content, Font font)
        {
            var node = new GameObject(name, typeof(RectTransform), typeof(Text));
            Undo.RegisterCreatedObjectUndo(node, "Tool drawer UI"); node.transform.SetParent(parent, false);
            Rect(node.GetComponent<RectTransform>(), position, size);
            var text = node.GetComponent<Text>(); text.font = font; text.fontSize = fontSize;
            text.text = content; text.color = Color.white; text.alignment = TextAnchor.MiddleCenter; text.raycastTarget = false;
            return text;
        }
    }
}
