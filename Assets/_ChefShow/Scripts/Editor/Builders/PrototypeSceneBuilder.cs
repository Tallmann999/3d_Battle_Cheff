using System;
using System.IO;
using System.Linq;
using ChefShow.Contestants;
using ChefShow.Core;
using ChefShow.Data;
using ChefShow.Player;
using ChefShow.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ChefShow.Editor
{
    public static class PrototypeSceneBuilder
    {
        public const string Root = "Assets/_ChefShow";
        public const string GeneratedScene = Root + "/Generated/Scenes/ChefShow_Prototype_Generated.unity";
        public const string EditableScene = Root + "/Scenes/ChefShow_Prototype.unity";
        public const string ConfigPath = Root + "/Generated/Data/PrototypeGameConfig.asset";
        public const string InputPath = Root + "/Input/ChefShowInput.inputactions";
        private static Font font;

        [MenuItem("Tools/Chef Show/Build Prototype Scene")]
        public static void BuildPrototypeScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("Дождитесь завершения Play Mode/компиляции/import.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!Application.isBatchMode)
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    var open = SceneManager.GetSceneAt(i);
                    if (string.IsNullOrEmpty(open.path) && !EditorSceneManager.SaveScene(open)) return;
                }
            if (Application.isBatchMode && Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty))
                throw new InvalidOperationException("Batch Builder не закрывает несохранённые сцены.");
            CreateMissingDefaultData();
            var config = AssetDatabase.LoadAssetAtPath<PrototypeGameConfig>(ConfigPath);
            var error = config.Validate();
            if (error != null) throw new InvalidOperationException(error);
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            RequireCyrillic(font);
            var existing = SceneManager.GetSceneByPath(GeneratedScene);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                // Сначала открыть новую сцену: Unity не позволяет закрыть последнюю
                // и не разрешает сохранить путь, пока старая generated-сцена открыта.
                if (existing.IsValid() && existing.isLoaded && !EditorSceneManager.CloseScene(existing, true))
                    throw new IOException("Не удалось закрыть предыдущую generated-сцену.");
                var arena = new GameObject("Arena").transform;
                var floor = Material("Floor", new Color(0.12f, 0.16f, 0.2f));
                var surfaces = Material("Worktop", new Color(0.52f, 0.56f, 0.6f));
                var a = Material("Team_A", new Color(0.95f, 0.36f, 0.1f));
                var b = Material("Team_B", new Color(0.55f, 0.23f, 0.85f));
                var neutral = Material("Neutral", new Color(0.88f, 0.85f, 0.74f));
                Box("Floor", arena, new Vector3(0, -0.15f, 0), new Vector3(config.ArenaWidth, 0.3f, config.ArenaDepth), floor);
                Box("Back Wall", arena, new Vector3(0, 2.5f, config.ArenaDepth / 2), new Vector3(config.ArenaWidth, 5, 0.3f), floor);
                foreach (int side in new[] { -1, 1 })
                    Box("Side Wall", arena, new Vector3(side * config.ArenaWidth / 2, 2.5f, 0), new Vector3(0.3f, 5, config.ArenaDepth), floor);
                Box("Front Wall", arena, new Vector3(0, 2.5f, -config.ArenaDepth / 2), new Vector3(config.ArenaWidth, 5, 0.3f), floor);
                var pantry = Box("Pantry", arena, new Vector3(0, 0.8f, 8), new Vector3(9, 1.6f, 1.5f), neutral);
                var pantryTarget = pantry.AddComponent<PrototypeInteractable>();
                pantryTarget.DisplayName = "Общая кладовая";
                pantryTarget.Description = "Подбор продуктов появится на следующем этапе.";
                Label("ОБЩАЯ КЛАДОВАЯ", pantry.transform, new Vector3(0, 1.35f, -0.8f), Quaternion.identity, 0.035f);
                var actors = new GameObject("Contestants").transform;
                for (int teamIndex = 0; teamIndex < 2; teamIndex++)
                {
                    var team = (TeamId)teamIndex;
                    float sign = team == TeamId.A ? -1 : 1;
                    var stations = new GameObject("Team" + team + "Stations").transform;
                    stations.SetParent(arena);
                    for (int i = 0; i < 6; i++)
                    {
                        string id = $"{team}{i + 1}";
                        float z = -6.25f + i * 2.5f;
                        var station = Box("Station_" + id, stations, new Vector3(sign * 8, 0.75f, z), new Vector3(3, 1.5f, 2), surfaces);
                        Box("Team Marker", station.transform, new Vector3(sign * 6.45f, 0.85f, z), new Vector3(0.1f, 0.9f, 1.8f), team == TeamId.A ? a : b);
                        bool own = team == config.PlayerTeam && i == 0;
                        var target = station.AddComponent<PrototypeInteractable>();
                        target.DisplayName = "Станция " + id;
                        target.IsPlayerStation = own;
                        target.Team = team;
                        target.Description = own ? "E — фокус станции" : "Станция другого участника. Только осмотр.";
                        Label(id + (own ? " · ВЫ" : ""), station.transform, new Vector3(-sign * 0.1f, 0.7f, 0), Quaternion.Euler(0, sign * 90, 0), 0.025f);
                        if (own) continue;
                        var npc = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                        npc.name = "NPC_" + id;
                        npc.transform.SetParent(actors);
                        npc.transform.position = new Vector3(sign * 6, 1, z);
                        npc.GetComponent<Renderer>().sharedMaterial = team == TeamId.A ? a : b;
                        npc.GetComponent<Collider>().enabled = false;
                        var actor = npc.AddComponent<PrototypeActor>();
                        actor.StableId = id;
                        actor.Team = team;
                        actor.Kind = PrototypeActorKind.Npc;
                    }
                }
                var judging = Box("Judging Table", arena, new Vector3(0, 0.7f, -8.5f), new Vector3(12, 1.4f, 1.5f), neutral);
                Label("ДЕГУСТАЦИЯ", judging.transform, new Vector3(0, 1.25f, 0.8f), Quaternion.Euler(0, 180, 0), 0.035f);
                var teamSlots = new GameObject("TeamDishSlots").transform;
                teamSlots.SetParent(arena);
                for (int i = 0; i < 12; i++)
                {
                    var slot = new GameObject("DishSlot_" + (i < 6 ? "A" : "B") + (i % 6 + 1)).transform;
                    slot.SetParent(teamSlots);
                    slot.position = new Vector3(-5.5f + i, 1.45f, -8.5f);
                }
                var finalSlots = new GameObject("FinalDishSlots").transform;
                finalSlots.SetParent(arena);
                for (int i = 0; i < 4; i++)
                {
                    var slot = new GameObject("FinalSlot_" + (i + 1)).transform;
                    slot.SetParent(finalSlots);
                    slot.position = new Vector3(-2.25f + i * 1.5f, 1.45f, -7.8f);
                }
                var chefs = new GameObject("Chefs").transform;
                for (int i = 0; i < 2; i++)
                {
                    var chef = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    chef.name = i == 0 ? "CHEF_SAVORY" : "CHEF_PASTRY";
                    chef.transform.SetParent(chefs);
                    chef.transform.position = new Vector3(i == 0 ? -2 : 2, 1, -6.5f);
                    chef.transform.localScale = i == 0 ? new Vector3(0.9f, 1.15f, 0.9f) : new Vector3(1.1f, 1, 1.1f);
                    chef.GetComponent<Renderer>().sharedMaterial = neutral;
                    chef.GetComponent<Collider>().enabled = false;
                    var actor = chef.AddComponent<PrototypeActor>();
                    actor.StableId = chef.name;
                    actor.Kind = PrototypeActorKind.Chef;
                }
                var routes = new GameObject("ChefWaypoints").transform;
                routes.SetParent(arena);
                for (int i = 0; i < 6; i++)
                {
                    var waypoint = new GameObject("Waypoint_" + i).transform;
                    waypoint.SetParent(routes);
                    waypoint.position = new Vector3(i < 3 ? -3 : 3, 0, (i % 3 - 1) * 5);
                }
                var lightObject = new GameObject("Lighting");
                var light = lightObject.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.4f;
                lightObject.transform.rotation = Quaternion.Euler(45, -35, 0);
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.5f, 0.55f, 0.65f);
                var player = CreatePlayer(config);
                var hud = CreateHud(player.ViewCamera);
                var eventObject = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                var uiModule = eventObject.GetComponent<InputSystemUIInputModule>();
                var bootstrap = new GameObject("Systems").AddComponent<GameBootstrap>();
                bootstrap.Config = config;
                bootstrap.InputDefinition = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputPath);
                bootstrap.Player = player;
                bootstrap.Hud = hud;
                bootstrap.UiInput = uiModule;
                PrototypeValidator.ValidateScene(scene);
                EnsureFolder(Path.GetDirectoryName(GeneratedScene));
                if (!EditorSceneManager.SaveScene(scene, GeneratedScene)) throw new IOException("Не удалось сохранить generated-сцену.");
                AssetDatabase.SaveAssets();
                Debug.Log("Chef Show: создана " + GeneratedScene + "; этап 1: арена/управление, готовка ещё не реализована.");
            }
            catch
            {
                if (SceneManager.sceneCount > 1) EditorSceneManager.CloseScene(scene, true);
                else if (File.Exists(GeneratedScene)) EditorSceneManager.OpenScene(GeneratedScene, OpenSceneMode.Single);
                throw;
            }
        }

        private static FirstPersonRig CreatePlayer(PrototypeGameConfig config)
        {
            var player = new GameObject("Player");
            float sign = config.PlayerTeam == TeamId.A ? -1 : 1;
            player.transform.position = new Vector3(sign * 6, 0.05f, -6.25f);
            player.transform.rotation = Quaternion.Euler(0, sign * 90, 0);
            var controller = player.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.3f;
            controller.center = Vector3.up * 0.9f;
            var cameraObject = new GameObject("MainCamera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(player.transform);
            cameraObject.transform.localPosition = Vector3.up * 1.65f;
            cameraObject.transform.localRotation = Quaternion.Euler(20, 0, 0);
            var camera = cameraObject.GetComponent<Camera>();
            camera.nearClipPlane = 0.08f;
            camera.farClipPlane = 100;
            camera.fieldOfView = 75;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.08f, 0.1f, 0.15f);
            var marker = player.AddComponent<PrototypeActor>();
            marker.Kind = PrototypeActorKind.Player;
            marker.Team = config.PlayerTeam;
            marker.StableId = config.PlayerTeam + "1";
            var rig = player.AddComponent<FirstPersonRig>();
            rig.Configure(camera);
            return rig;
        }

        private static PrototypeHud CreateHud(Camera camera)
        {
            var root = new GameObject("UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 0.5f;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 0.5f;
            var hud = root.AddComponent<PrototypeHud>();
            hud.Status = Text("Status", root.transform, new Vector2(20, -20), new Vector2(850, 110), 25, "CHEF SHOW · ЭТАП 1", TextAnchor.UpperLeft);
            hud.Context = Text("Context", root.transform, new Vector2(350, -570), new Vector2(800, 80), 24, "", TextAnchor.MiddleCenter);
            Text("Crosshair", root.transform, new Vector2(620, -340), new Vector2(40, 40), 32, "+", TextAnchor.MiddleCenter);
            Text("Controls", root.transform, new Vector2(20, -630), new Vector2(1100, 75), 20,
                "WASD — ходьба · мышь — обзор · Shift — бег\nE — фокус своей станции · RMB — выйти · Tab — задание · Esc — пауза · F1 — debug", TextAnchor.UpperLeft);
            hud.TaskCard = Text("Task Card", root.transform, new Vector2(320, -160), new Vector2(650, 250), 26,
                "Первый технический этап\nОсмотрите две команды, кладовую и дегустационный стол.\nГотовка и настоящий раунд появятся на следующем срезе.", TextAnchor.MiddleCenter);
            hud.PausePanel = Panel("Pause", root.transform);
            Text("Pause Title", hud.PausePanel.transform, new Vector2(30, -20), new Vector2(500, 60), 32, "Пауза", TextAnchor.MiddleCenter);
            hud.Resume = Button("Продолжить", hud.PausePanel.transform, 100);
            hud.Restart = Button("Начать выпуск заново", hud.PausePanel.transform, 170);
            Text("Sensitivity Label", hud.PausePanel.transform, new Vector2(50, -255), new Vector2(450, 40), 23, "Чувствительность мыши", TextAnchor.MiddleCenter);
            var sliderObject = new GameObject("Sensitivity", typeof(RectTransform), typeof(Image), typeof(Slider));
            sliderObject.transform.SetParent(hud.PausePanel.transform, false);
            Rect(sliderObject.GetComponent<RectTransform>(), new Vector2(80, -310), new Vector2(400, 25));
            sliderObject.GetComponent<Image>().color = new Color(0.25f, 0.3f, 0.38f);
            var handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            handle.transform.SetParent(sliderObject.transform, false);
            handle.GetComponent<RectTransform>().sizeDelta = new Vector2(24, 35);
            handle.GetComponent<Image>().color = Color.white;
            hud.Sensitivity = sliderObject.GetComponent<Slider>();
            hud.Sensitivity.minValue = 0.01f;
            hud.Sensitivity.maxValue = 0.5f;
            hud.Sensitivity.handleRect = handle.GetComponent<RectTransform>();
            hud.Sensitivity.targetGraphic = handle.GetComponent<Image>();
            hud.DebugPanel = Panel("Debug", root.transform);
            Text("Debug Title", hud.DebugPanel.transform, new Vector2(30, -20), new Vector2(500, 60), 26, "DEBUG · ЭТАП 1", TextAnchor.MiddleCenter);
            hud.DebugRestart = Button("Restart", hud.DebugPanel.transform, 90);
            hud.Timer30 = Button("Пробный таймер: 30 с", hud.DebugPanel.transform, 155);
            hud.Timer60 = Button("Пробный таймер: 60 с", hud.DebugPanel.transform, 220);
            hud.Timer240 = Button("Пробный таймер: 240 с", hud.DebugPanel.transform, 285);
            hud.PausePanel.SetActive(false);
            hud.DebugPanel.SetActive(false);
            hud.TaskCard.gameObject.SetActive(false);
            return hud;
        }

        private static GameObject Panel(string name, Transform parent)
        {
            var panel = new GameObject(name, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            Rect(panel.GetComponent<RectTransform>(), new Vector2(360, -150), new Vector2(560, 410));
            panel.GetComponent<Image>().color = new Color(0.035f, 0.055f, 0.09f, 0.98f);
            return panel;
        }

        private static Button Button(string caption, Transform parent, float y)
        {
            var node = new GameObject(caption, typeof(RectTransform), typeof(Image), typeof(Button));
            node.transform.SetParent(parent, false);
            Rect(node.GetComponent<RectTransform>(), new Vector2(50, -y), new Vector2(460, 55));
            node.GetComponent<Image>().color = new Color(0.22f, 0.3f, 0.42f);
            var button = node.GetComponent<Button>();
            button.targetGraphic = node.GetComponent<Image>();
            var label = Text("Label", node.transform, Vector2.zero, new Vector2(460, 55), 24, caption, TextAnchor.MiddleCenter);
            label.raycastTarget = false;
            return button;
        }

        private static Text Text(string name, Transform parent, Vector2 offset, Vector2 size, int fontSize, string content, TextAnchor align)
        {
            var node = new GameObject(name, typeof(RectTransform), typeof(Text));
            node.transform.SetParent(parent, false);
            Rect(node.GetComponent<RectTransform>(), offset, size);
            var text = node.GetComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.text = content;
            text.color = Color.white;
            text.alignment = align;
            text.raycastTarget = false;
            return text;
        }

        private static void Rect(RectTransform rect, Vector2 offset, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = offset;
            rect.sizeDelta = size;
        }

        private static GameObject Box(string name, Transform parent, Vector3 position, Vector3 size, Material material)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent);
            box.transform.position = position;
            var scale = parent.lossyScale;
            box.transform.localScale = new Vector3(size.x / scale.x, size.y / scale.y, size.z / scale.z);
            box.GetComponent<Renderer>().sharedMaterial = material;
            return box;
        }

        private static void Label(string content, Transform parent, Vector3 offset, Quaternion rotation, float characterSize)
        {
            var node = new GameObject("Label");
            node.transform.SetParent(parent, false);
            // Родительские примитивы масштабированы; держим масштаб букв постоянным.
            node.transform.localPosition = offset;
            node.transform.rotation = rotation;
            node.transform.localScale = new Vector3(1 / parent.lossyScale.x, 1 / parent.lossyScale.y, 1 / parent.lossyScale.z);
            var text = node.AddComponent<TextMesh>();
            text.text = content;
            text.font = font;
            text.fontSize = 48;
            text.characterSize = characterSize;
            text.anchor = TextAnchor.MiddleCenter;
            node.GetComponent<MeshRenderer>().sharedMaterial = font.material;
        }

        private static Material Material(string name, Color color)
        {
            string path = Root + "/Generated/Materials/" + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("URP Lit shader не найден.");
            var material = new Material(shader) { name = name, color = color };
            EnsureFolder(Path.GetDirectoryName(path));
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        public static void RequireCyrillic(Font candidate)
        {
            const string chars = "АБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯабвгдеёжзийклмнопрстуфхцчшщъыьэюя";
            if (candidate == null || chars.Any(c => !candidate.HasCharacter(c)))
                throw new InvalidOperationException("Шрифт прототипа должен поддерживать полную кириллицу.");
        }

        [MenuItem("Tools/Chef Show/Create Missing Default Data")]
        public static void CreateMissingDefaultData()
        {
            if (AssetDatabase.LoadAssetAtPath<PrototypeGameConfig>(ConfigPath) == null)
            {
                EnsureFolder(Path.GetDirectoryName(ConfigPath));
                AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<PrototypeGameConfig>(), ConfigPath);
            }
            if (AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputPath) != null) return;
            var asset = ScriptableObject.CreateInstance<InputActionAsset>();
            asset.name = "ChefShowInput";
            var game = asset.AddActionMap("Gameplay");
            game.AddAction("Move", InputActionType.Value, expectedControlLayout: "Vector2")
                .AddCompositeBinding("2DVector").With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            game.AddAction("Look", InputActionType.Value, "<Mouse>/delta", expectedControlLayout: "Vector2");
            game.AddAction("Sprint", InputActionType.Button, "<Keyboard>/leftShift");
            game.AddAction("Interact", InputActionType.Button, "<Keyboard>/e");
            game.AddAction("Task", InputActionType.Button, "<Keyboard>/tab");
            var station = asset.AddActionMap("Station");
            station.AddAction("Look", InputActionType.Value, "<Mouse>/delta", expectedControlLayout: "Vector2");
            station.AddAction("Cancel", InputActionType.Button, "<Mouse>/rightButton");
            station.AddAction("Task", InputActionType.Button, "<Keyboard>/tab");
            var ui = asset.AddActionMap("UI");
            ui.AddAction("Point", InputActionType.PassThrough, "<Mouse>/position", expectedControlLayout: "Vector2");
            ui.AddAction("Click", InputActionType.PassThrough, "<Mouse>/leftButton");
            ui.AddAction("ScrollWheel", InputActionType.PassThrough, "<Mouse>/scroll", expectedControlLayout: "Vector2");
            ui.AddAction("Navigate", InputActionType.PassThrough, expectedControlLayout: "Vector2")
                .AddCompositeBinding("2DVector").With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
            ui.AddAction("Submit", InputActionType.Button, "<Keyboard>/enter");
            ui.AddAction("Cancel", InputActionType.Button, "<Keyboard>/escape");
            asset.AddActionMap("Debug").AddAction("Toggle", InputActionType.Button, "<Keyboard>/f1");
            EnsureFolder(Path.GetDirectoryName(InputPath));
            File.WriteAllText(InputPath, asset.ToJson());
            UnityEngine.Object.DestroyImmediate(asset);
            AssetDatabase.ImportAsset(InputPath, ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Tools/Chef Show/Create Editable Scene Copy")]
        public static void CreateEditableSceneCopy()
        {
            if (!File.Exists(GeneratedScene)) throw new InvalidOperationException("Сначала Build Prototype Scene.");
            EnsureFolder(Path.GetDirectoryName(EditableScene));
            string path = File.Exists(EditableScene) ? AssetDatabase.GenerateUniqueAssetPath(EditableScene) : EditableScene;
            if (!AssetDatabase.CopyAsset(GeneratedScene, path)) throw new IOException("Не удалось создать рабочую копию.");
            if (!EditorBuildSettings.scenes.Any(s => s.path == path))
                EditorBuildSettings.scenes = EditorBuildSettings.scenes.Concat(new[] { new EditorBuildSettingsScene(path, true) }).ToArray();
            Debug.Log("Chef Show: редактируемая копия " + path);
        }

        public static void BuildBatch()
        {
            // Новый headless Editor начинает с временной Untitled; открываем сохранённую
            // сцену перед additive-генерацией, не сохраняя и не изменяя её содержимое.
            if (string.IsNullOrEmpty(SceneManager.GetActiveScene().path)
                && !SceneManager.GetActiveScene().isDirty)
                EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Single);
            BuildPrototypeScene();
            if (!File.Exists(EditableScene)) CreateEditableSceneCopy();
        }

        public static void UpdateStageOneLabelsBatch()
        {
            // Узкая миграция только исходных позиций подписей первой версии.
            // Изменённые автором положения сохраняются; рабочая сцена не регенерируется.
            var scene = EditorSceneManager.OpenScene(EditableScene, OpenSceneMode.Single);
            foreach (var label in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<TextMesh>(true)))
            {
                var parent = label.transform.parent;
                if (parent == null || label.fontSize != 48) continue;
                if (parent.name.StartsWith("Station_"))
                {
                    float sign = parent.name.StartsWith("Station_A") ? -1 : 1;
                    if (Vector3.Distance(label.transform.localPosition, new Vector3(-sign * 0.1f, 1.25f, 0)) < 0.001f
                        && Quaternion.Angle(label.transform.rotation, Quaternion.Euler(0, sign * -90, 0)) < 0.1f)
                    {
                        var position = label.transform.localPosition;
                        position.y = 0.7f;
                        label.transform.localPosition = position;
                        label.transform.rotation = Quaternion.Euler(0, sign * 90, 0);
                    }
                    if (Mathf.Abs(label.characterSize - 0.1f) < 0.001f) label.characterSize = 0.025f;
                }
                else if ((parent.name == "Pantry" && Mathf.Abs(label.characterSize - 0.11f) < 0.001f)
                    || (parent.name == "Judging Table" && Mathf.Abs(label.characterSize - 0.1f) < 0.001f))
                    label.characterSize = 0.035f;
            }
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Не удалось сохранить миграцию подписей.");
            BuildPrototypeScene();
        }

        private static void EnsureFolder(string path)
        {
            path = path.Replace('\\', '/');
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
