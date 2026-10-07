using System;
using System.IO;
using System.Linq;
using ChefShow.Core;
using ChefShow.Data;
using ChefShow.Ingredients;
using ChefShow.Inventory;
using ChefShow.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ChefShow.Editor
{
    public static class InventorySceneInstaller
    {
        public const string CatalogPath = PrototypeSceneBuilder.Root + "/Generated/Data/IngredientCatalog.asset";
        private static Font font;

        [MenuItem("Tools/Chef Show/Install Inventory Stage")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("Остановите Play Mode и дождитесь компиляции.");
            var working = SceneManager.GetActiveScene();
            if (working.path != PrototypeSceneBuilder.EditableScene || working.isDirty)
                throw new InvalidOperationException("Откройте сохранённую рабочую сцену ChefShow_Prototype.");
            var generated = SceneManager.GetSceneByPath(PrototypeSceneBuilder.GeneratedScene);
            bool opened = !generated.IsValid() || !generated.isLoaded;
            if (!opened && generated.isDirty) throw new InvalidOperationException("Сохраните изменения generated-сцены.");
            if (opened) generated = EditorSceneManager.OpenScene(PrototypeSceneBuilder.GeneratedScene, OpenSceneMode.Additive);
            try
            {
                foreach (var scene in new[] { working, generated }) PrototypeValidator.ValidateScene(scene);
                Directory.CreateDirectory("TestResults");
                string stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
                foreach (var scene in new[] { working, generated })
                    File.Copy(scene.path, "TestResults/inventory-before-" + scene.name + "-" + stamp + ".unity", false);
                foreach (var scene in new[] { working, generated })
                {
                    SceneManager.SetActiveScene(scene);
                    AddToScene(scene);
                    PrototypeValidator.ValidateScene(scene);
                    Undo.FlushUndoRecordObjects();
                    EditorSceneManager.MarkSceneDirty(scene);
                    if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Не удалось сохранить " + scene.path);
                }
                AssetDatabase.SaveAssets();
                File.WriteAllText("TestResults/inventory-installed.txt", "F-004 INSTALLED " + DateTime.UtcNow.ToString("O"));
                Debug.Log("Chef Show F-004: продукты, одна корзина 10 мест, лоток 24 места, snap-перенос установлены.");
            }
            finally
            {
                SceneManager.SetActiveScene(working);
                if (opened) EditorSceneManager.CloseScene(generated, true);
            }
        }

        public static void AddToScene(Scene scene)
        {
            var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
            var bootstrap = all.Select(t => t.GetComponent<GameBootstrap>()).Single(b => b != null);
            if (bootstrap.Inventory != null)
            {
                var existingError = bootstrap.Inventory.Validate();
                if (existingError != null) throw new InvalidOperationException(existingError);
                return; // Повтор команды сохраняет ручные положения и ссылки.
            }
            Transform Named(string name) => all.Single(t => t.name == name);
            var stations = all.Where(t => t.name.StartsWith("Station_", StringComparison.Ordinal)).ToArray();
            if (stations.Length != 12 || all.Any(t => t.name == "PantryStock" || t.name == "InventoryBasket"))
                throw new InvalidOperationException("Проверьте 12 станций и отсутствие незавершённой установки инвентаря.");
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            PrototypeSceneBuilder.RequireCyrillic(font);
            var catalog = EnsureCatalog();
            bootstrap.InputDefinition = EnsureActions();
            var arena = Named("Arena");
            var pantry = Named("Pantry");
            var inventory = Undo.AddComponent<InventoryController>(bootstrap.gameObject);
            Undo.RecordObject(bootstrap, "Install inventory"); bootstrap.Inventory = inventory;
            inventory.Catalog = catalog;
            inventory.PlayerStationId = bootstrap.Player.GetComponent<Contestants.PrototypeActor>().StableId;
            inventory.WorldRoot = arena;
            var camera = bootstrap.Player.ViewCamera.transform;
            inventory.BasketMount = Node("Basket Mount", camera, camera.TransformPoint(new Vector3(-0.38f, -0.32f, 0.65f)));
            inventory.BasketMount.localRotation = Quaternion.identity;
            inventory.HeldDisplay = Display("Held Ingredient", camera, camera.TransformPoint(new Vector3(0.32f, -0.22f, 0.55f)), 1);
            inventory.HeldDisplay.gameObject.layer = LayerMask.NameToLayer("Ignore Raycast"); inventory.HeldDisplay.transform.localRotation = Quaternion.identity;
            var flights = Node("Pickup Flights", arena, Vector3.zero);
            inventory.Flights = Enumerable.Range(0, 4).Select(i => Display("Flight_" + i, flights, Vector3.zero, 1)).ToArray();
            foreach (var view in inventory.Flights) view.gameObject.layer = LayerMask.NameToLayer("Ignore Raycast");

            float pantryTop = pantry.GetComponent<BoxCollider>().bounds.max.y;
            var stock = Node("PantryStock", pantry, pantry.position);
            for (int i = 0; i < catalog.Ingredients.Length; i++)
            {
                var ingredient = catalog.Ingredients[i];
                var point = new Vector3(pantry.position.x - 3 + (i % 7), pantryTop + 0.13f, pantry.position.z - 0.45f + (i / 7) * 0.6f);
                var target = Target("Stock_" + ingredient.Id, stock, point, new Vector3(0.65f, 0.3f, 0.45f), InventoryTargetKind.Pickup);
                target.Ingredient = ingredient;
                target.GetComponent<PrototypeInteractable>().DisplayName = ingredient.DisplayName;
                var display = Display("Product", target.transform, point, 1);
                display.Caption = Caption(ingredient.DisplayName, target.transform, point + Vector3.up * 0.21f, Quaternion.identity, 0.013f);
                display.Present(ingredient, showName: true);
            }
            var restPoint = new Vector3(pantry.position.x - 4.08f, pantryTop + 0.02f, pantry.position.z - 0.15f);
            var rest = Pad("Basket Rest", pantry, restPoint, new Vector3(0.65f, 0.03f, 0.5f), InventoryTargetKind.PantryRest, "МЕСТО КОРЗИНЫ", Quaternion.identity);
            inventory.PantryRest = Node("Basket Home", rest.transform, restPoint + Vector3.up * 0.13f);
            var pantryReturn = Pad("Return Ingredients", pantry, new Vector3(pantry.position.x, pantryTop + 0.02f, pantry.position.z - 0.7f),
                new Vector3(1.1f, 0.03f, 0.2f), InventoryTargetKind.PantryReturn, "ВОЗВРАТ", Quaternion.identity);
            if (pantry.Find("Label") != null) pantry.Find("Label").position += Vector3.up * 0.35f;

            var basket = Node("InventoryBasket", arena, inventory.PantryRest.position);
            var proto = Undo.AddComponent<PrototypeInteractable>(basket.gameObject); proto.DisplayName = "Корзина";
            var basketTarget = Undo.AddComponent<InventoryInteractable>(basket.gameObject); basketTarget.Kind = InventoryTargetKind.Basket;
            inventory.BasketCollider = Undo.AddComponent<BoxCollider>(basket.gameObject); inventory.BasketCollider.size = new Vector3(0.52f, 0.25f, 0.37f);
            inventory.BasketBody = Undo.AddComponent<Rigidbody>(basket.gameObject);
            inventory.BasketBody.isKinematic = true; inventory.BasketBody.mass = 1; inventory.BasketBody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            var wood = Material("Basket", new Color(0.6f, 0.31f, 0.12f));
            Box("Bottom", basket, basket.position + Vector3.down * 0.1f, new Vector3(0.5f, 0.04f, 0.35f), wood, false);
            for (int sign = -1; sign <= 1; sign += 2)
            {
                Box("Side", basket, basket.position + Vector3.right * sign * 0.24f, new Vector3(0.03f, 0.22f, 0.35f), wood, false);
                Box("Rim", basket, basket.position + Vector3.forward * sign * 0.17f, new Vector3(0.5f, 0.22f, 0.03f), wood, false);
            }
            inventory.BasketDisplays = Enumerable.Range(0, 10).Select(i => Display("Basket Item_" + (i + 1), basket,
                basket.position + new Vector3(-0.17f + (i % 5) * 0.085f, -0.02f + (i / 5) * 0.055f, (i / 5 == 0 ? -0.08f : 0.08f)), 0.45f)).ToArray();

            foreach (var station in stations)
            {
                string id = station.name.Substring("Station_".Length);
                float sign = id.StartsWith("A", StringComparison.Ordinal) ? -1 : 1;
                float top = station.GetComponent<BoxCollider>().bounds.max.y;
                var group = Node("Inventory", station, station.position);
                Vector3 At(float x, float z, float y = 0.03f) => new Vector3(station.position.x + sign * x, top + y, station.position.z + z);
                var rotation = Quaternion.Euler(0, -sign * 90, 0);
                var dock = Pad("Basket Dock", group, At(-0.35f, -0.72f), new Vector3(0.65f, 0.05f, 0.55f), InventoryTargetKind.BasketDock, "КОРЗИНА", rotation, id);
                var dockMount = Node("Basket Snap", dock.transform, dock.transform.position + Vector3.up * 0.15f);
                var tray = Pad("Ingredient Tray", group, At(-0.35f, 0.65f), new Vector3(0.65f, 0.04f, 0.94f), InventoryTargetKind.Tray, "ЛОТОК", rotation, id);
                var board = Pad("Board", group, At(0.65f, -0.6f), new Vector3(0.65f, 0.06f, 0.6f), InventoryTargetKind.Socket, "ДОСКА", rotation, id); board.Index = 0;
                var surface = Pad("Work Surface", group, At(0.65f, 0.6f), new Vector3(0.65f, 0.06f, 0.6f), InventoryTargetKind.Socket, "МЕСТО ПРОДУКТА", rotation, id); surface.Index = 1;
                Pad("Trash", group, At(-0.8f, 0, 0.07f), new Vector3(0.3f, 0.14f, 0.4f), InventoryTargetKind.Trash, "МУСОР", rotation, id);
                if (id != inventory.PlayerStationId) continue;
                inventory.StationDock = dockMount;
                inventory.TrayDisplays = new FoodDisplay[24];
                for (int i = 0; i < 24; i++)
                {
                    var point = At(-0.57f + (i % 3) * 0.2f, 0.25f + (i / 3) * 0.115f, 0.12f);
                    var item = Target("Tray Item_" + (i + 1), tray.transform, point, new Vector3(0.16f, 0.1f, 0.1f), InventoryTargetKind.TrayItem, id);
                    item.Index = i;
                    item.GetComponent<BoxCollider>().enabled = false;
                    inventory.TrayDisplays[i] = Display("Food", item.transform, point, 0.5f);
                }
                inventory.SocketDisplays = new[] { Display("Food", board.transform, At(0.65f, -0.6f, 0.16f), 1), Display("Food", surface.transform, At(0.65f, 0.6f, 0.16f), 1) };
            }
            foreach (var text in bootstrap.Hud.GetComponentsInChildren<Text>(true))
                if (text.name == "Controls")
                {
                    text.text = "WASD — ходьба · Shift — бег · мышь — обзор\nTab — корзина · ЛКМ — взять/выгрузить · G — уронить\nE — перенос · RMB — отмена · Q — задание · Esc — пауза";
                    text.fontSize = 16; text.rectTransform.anchoredPosition = new Vector2(470, -646); text.rectTransform.sizeDelta = new Vector2(800, 72);
                }
            bootstrap.Hud.TaskCard.text = "Продукты и перенос\nВозьмите корзину Tab у кладовой. Наберите продукты ЛКМ.\nНа своей станции: Tab поставить, ЛКМ по корзине выгрузить.\nE — взять из лотка и положить на доску/рабочее место.\nПриготовление и распаковка появятся на следующем этапе.";
            EditorUtility.SetDirty(bootstrap.Config);
            EditorUtility.SetDirty(inventory); EditorUtility.SetDirty(bootstrap.Hud); EditorUtility.SetDirty(bootstrap);
            var error = inventory.Validate(); if (error != null) throw new InvalidOperationException(error);
            Physics.SyncTransforms();
        }

        private static IngredientCatalog EnsureCatalog()
        {
            Folder(Path.GetDirectoryName(CatalogPath));
            var catalog = AssetDatabase.LoadAssetAtPath<IngredientCatalog>(CatalogPath);
            if (catalog != null) return catalog;
            string[] ids = { "beef", "potato", "carrot", "onion", "egg", "cheese", "flour", "butter", "sugar", "apple", "salt", "oil", "potato_sack", "egg_carton" };
            string[] names = { "Говядина", "Картофель", "Морковь", "Лук", "Яйцо", "Сыр", "Мука", "Сливочное масло", "Сахар", "Яблоко", "Соль", "Растительное масло", "Мешок картошки", "Коробка яиц" };
            Color[] colors = { new Color(.55f,.13f,.12f), new Color(.64f,.46f,.21f), new Color(1,.35f,.05f), new Color(.75f,.57f,.36f),
                new Color(.95f,.87f,.67f), new Color(1,.76f,.1f), new Color(.8f,.75f,.61f), new Color(1,.88f,.35f),
                new Color(.94f,.86f,.96f), new Color(.7f,.08f,.07f), new Color(.64f,.85f,1), new Color(.67f,.72f,.05f),
                new Color(.5f,.32f,.12f), new Color(.58f,.65f,.55f) };
            var definitions = new IngredientDefinition[ids.Length];
            string directory = PrototypeSceneBuilder.Root + "/Generated/Data/Ingredients"; Folder(directory);
            for (int i = 0; i < ids.Length; i++)
            {
                string path = directory + "/" + ids[i] + ".asset";
                var definition = AssetDatabase.LoadAssetAtPath<IngredientDefinition>(path);
                if (definition == null)
                {
                    definition = ScriptableObject.CreateInstance<IngredientDefinition>();
                    definition.Id = ids[i]; definition.DisplayName = names[i];
                    definition.CanUseBoard = new[] { 0, 1, 2, 3, 5, 9 }.Contains(i);
                    definition.IsDoseContainer = i == 10 || i == 11;
                    var shape = i == 1 || i == 3 || i == 4 || i == 9 ? PrimitiveType.Sphere : i == 10 || i == 11 || i == 12 ? PrimitiveType.Cylinder : PrimitiveType.Cube;
                    var temporary = GameObject.CreatePrimitive(shape);
                    definition.VisualMesh = temporary.GetComponent<MeshFilter>().sharedMesh;
                    UnityEngine.Object.DestroyImmediate(temporary);
                    definition.VisualMaterial = Material("Ingredient_" + ids[i], colors[i]);
                    definition.VisualScale = shape == PrimitiveType.Cylinder ? new Vector3(.19f,.12f,.19f)
                        : i == 13 ? new Vector3(.34f,.12f,.22f) : new Vector3(.2f,.17f,.2f);
                    if (i >= 12)
                    { definition.Contents = definitions[i == 12 ? 1 : 4]; definition.ContentsQuantity = i == 12 ? 5 : 6; }
                    AssetDatabase.CreateAsset(definition, path);
                }
                definitions[i] = definition;
            }
            catalog = ScriptableObject.CreateInstance<IngredientCatalog>(); catalog.Ingredients = definitions;
            AssetDatabase.CreateAsset(catalog, CatalogPath); return catalog;
        }
        private static InputActionAsset EnsureActions()
        {
            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(PrototypeSceneBuilder.InputPath);
            string before = asset.ToJson();
            foreach (string name in new[] { "Gameplay", "Station" })
            {
                var map = asset.FindActionMap(name, true);
                Add("Interact", "<Keyboard>/e"); Add("Primary", "<Mouse>/leftButton");
                Add("Basket", "<Keyboard>/tab"); Add("DropBasket", "<Keyboard>/g"); Add("Cancel", "<Mouse>/rightButton");
                var task = map.FindAction("Task", true);
                for (int i = 0; i < task.bindings.Count; i++)
                    if (task.bindings[i].path == "<Keyboard>/tab") task.ChangeBinding(i).WithPath("<Keyboard>/q");
                if (!task.bindings.Any(b => b.path == "<Keyboard>/q")) task.AddBinding("<Keyboard>/q");
                void Add(string actionName, string binding)
                {
                    if (map.FindAction(actionName) == null) map.AddAction(actionName, InputActionType.Button, binding);
                }
            }
            string json = asset.ToJson();
            if (json != before)
            {
                File.WriteAllText(PrototypeSceneBuilder.InputPath, json);
                AssetDatabase.ImportAsset(PrototypeSceneBuilder.InputPath, ImportAssetOptions.ForceSynchronousImport);
            }
            return AssetDatabase.LoadAssetAtPath<InputActionAsset>(PrototypeSceneBuilder.InputPath);
        }
        private static InventoryInteractable Pad(string name, Transform parent, Vector3 point, Vector3 size,
            InventoryTargetKind kind, string caption, Quaternion facing, string stationId = null)
        {
            var node = Box(name, parent, point, size, Material("Inventory Surface", new Color(.28f,.36f,.39f)), true);
            var proto = Undo.AddComponent<PrototypeInteractable>(node); proto.DisplayName = caption;
            var target = Undo.AddComponent<InventoryInteractable>(node); target.Kind = kind; target.StationId = stationId;
            Caption(caption, node.transform, point + Vector3.up * (size.y / 2 + .08f), facing, .012f);
            return target;
        }
        private static InventoryInteractable Target(string name, Transform parent, Vector3 point, Vector3 size, InventoryTargetKind kind, string stationId = null)
        {
            var node = Node(name, parent, point).gameObject;
            Undo.AddComponent<BoxCollider>(node).size = size;
            var proto = Undo.AddComponent<PrototypeInteractable>(node); proto.DisplayName = name;
            var target = Undo.AddComponent<InventoryInteractable>(node); target.Kind = kind; target.StationId = stationId; return target;
        }
        private static FoodDisplay Display(string name, Transform parent, Vector3 point, float size)
        {
            var node = Node(name, parent, point);
            var mesh = Node("Mesh", node, point).gameObject;
            var view = Undo.AddComponent<FoodDisplay>(node.gameObject);
            view.Mesh = Undo.AddComponent<MeshFilter>(mesh);
            view.Visual = Undo.AddComponent<MeshRenderer>(mesh); view.SizeMultiplier = size; view.Visual.enabled = false;
            return view;
        }
        private static TextMesh Caption(string text, Transform parent, Vector3 point, Quaternion rotation, float size)
        {
            var node = Node("Label", parent, point); node.rotation = rotation;
            var label = Undo.AddComponent<TextMesh>(node.gameObject); label.text = text; label.font = font;
            label.fontSize = 48; label.characterSize = size; label.anchor = TextAnchor.MiddleCenter;
            node.GetComponent<MeshRenderer>().sharedMaterial = font.material; return label;
        }
        private static Transform Node(string name, Transform parent, Vector3 point)
        {
            var node = new GameObject(name); Undo.RegisterCreatedObjectUndo(node, "Install inventory");
            node.transform.SetParent(parent, false); node.transform.position = point;
            node.transform.localScale = new Vector3(1/parent.lossyScale.x, 1/parent.lossyScale.y, 1/parent.lossyScale.z); return node.transform;
        }
        private static GameObject Box(string name, Transform parent, Vector3 point, Vector3 size, Material material, bool collider)
        {
            var node = GameObject.CreatePrimitive(PrimitiveType.Cube); node.name = name;
            Undo.RegisterCreatedObjectUndo(node, "Install inventory"); node.transform.SetParent(parent, false); node.transform.position = point;
            node.transform.localScale = new Vector3(size.x/parent.lossyScale.x, size.y/parent.lossyScale.y, size.z/parent.lossyScale.z);
            node.GetComponent<Renderer>().sharedMaterial = material;
            if (!collider) UnityEngine.Object.DestroyImmediate(node.GetComponent<Collider>());
            return node;
        }
        private static Material Material(string name, Color color)
        {
            string path = PrototypeSceneBuilder.Root + "/Generated/Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path); if (material != null) return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name, color = color };
            AssetDatabase.CreateAsset(material, path); return material;
        }
        private static void Folder(string path)
        {
            path = path.Replace('\\','/'); if (AssetDatabase.IsValidFolder(path)) return;
            Folder(Path.GetDirectoryName(path)); AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\','/'), Path.GetFileName(path));
        }
    }
}
