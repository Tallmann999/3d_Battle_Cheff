using System.Collections;
using System.IO;
using System.Linq;
using ChefShow.Core;
using ChefShow.Inventory;
using ChefShow.Ingredients;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ChefShow.Tests
{
    public sealed class InventoryPlayTests
    {
        private GameBootstrap bootstrap;
        private InventoryController inventory;
        private Keyboard keyboard;
        private Mouse mouse;
        private InputSettings savedSettings, testSettings;
        private InventoryState State => bootstrap.Run.Inventory;

        [UnitySetUp]
        public IEnumerator Setup()
        {
            savedSettings = InputSystem.settings; testSettings = Object.Instantiate(savedSettings);
            testSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            testSettings.updateMode = InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;
#if UNITY_EDITOR
            testSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            InputSystem.settings = testSettings;
            keyboard = InputSystem.AddDevice<Keyboard>(); mouse = InputSystem.AddDevice<Mouse>();
            yield return SceneManager.LoadSceneAsync("ChefShow_Prototype", LoadSceneMode.Single);
            yield return null;
            bootstrap = Object.FindFirstObjectByType<GameBootstrap>(); inventory = bootstrap.Inventory;
            // Изолируем runtime-копию actions от мыши автора, работающего рядом с MCP-тестом.
            bootstrap.UiInput.actionsAsset.devices = new InputDevice[] { keyboard, mouse };
            Assert.That(inventory, Is.Not.Null); Assert.That(inventory.Validate(), Is.Null);
            bootstrap.AutoPauseOnFocusLoss = false;
            bootstrap.SetPaused(false);
        }
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            InputSystem.RemoveDevice(keyboard); InputSystem.RemoveDevice(mouse);
            InputSystem.settings = savedSettings; Object.Destroy(testSettings); yield return null;
        }
        private void Teleport(Vector3 position)
        {
            var controller = bootstrap.Player.GetComponent<CharacterController>();
            controller.enabled = false; bootstrap.Player.transform.position = position; controller.enabled = true;
            Physics.SyncTransforms();
        }
        private IEnumerator Aim(Vector3 point, bool exactPoint = true)
        {
            // После teleport даём CharacterController опуститься на пол прежде,
            // чем рассчитывать угол. В игре игрок доходит до цели уже на земле.
            yield return null; yield return null;
            var camera = bootstrap.Player.ViewCamera.transform;
            for (int correction = 0; correction < 3; correction++)
            {
                var desired = Quaternion.LookRotation(point - camera.position).eulerAngles;
                var current = camera.eulerAngles;
                InputSystem.QueueDeltaStateEvent(mouse.delta, new Vector2(Mathf.DeltaAngle(current.y, desired.y), -Mathf.DeltaAngle(current.x, desired.x)) / bootstrap.Config.LookSensitivity);
                yield return null; yield return null;
                if (Vector3.Angle(point - camera.position, camera.forward) < .2f) break;
            }
            bootstrap.Player.RefreshTarget();
            if (exactPoint) Assert.That(Vector3.Angle(point - camera.position, camera.forward), Is.LessThan(0.3f),
                "Наведение камеры: цель=" + point + " камера=" + camera.position + " взгляд=" + camera.forward + " объект=" + bootstrap.Player.Target?.name);
        }
        private IEnumerator Press(Key key)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key)); yield return null; yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null; yield return null;
        }
        private IEnumerator Click()
        {
            InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Left)); yield return null; yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState()); yield return null; yield return null;
        }
        private IEnumerator Cancel()
        {
            InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Right)); yield return null; yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState()); yield return null; yield return null;
        }
        private Vector3 Stock(string id) => GameObject.Find("Stock_" + id).transform.position;
        private Vector3 Socket(int index) => inventory.SocketDisplays[index].transform.parent.position;
        private Vector3 TrayPoint(int index)
        {
            // После переноса лотка назад центр дальнего продукта может быть
            // закрыт ближним. Наводим реальный прицел на видимую верхнюю часть.
            var bounds = inventory.TrayDisplays[index].Visual.bounds;
            return bounds.center + Vector3.up * bounds.extents.y * 0.8f;
        }
        private IEnumerator TakeBasket()
        {
            Teleport(new Vector3(-4.08f, .05f, 10.3f)); yield return Aim(inventory.BasketBody.position);
            yield return Press(Key.Tab); Assert.That(State.Placement, Is.EqualTo(BasketPlacement.Carried));
        }
        private IEnumerator DockAndDump()
        {
            Teleport(new Vector3(-9.7f, .05f, -6.25f)); yield return Aim(inventory.StationDock.parent.position);
            Assert.That(bootstrap.Player.Target?.name, Is.EqualTo("Basket Dock"));
            yield return Press(Key.Tab); Assert.That(State.Placement, Is.EqualTo(BasketPlacement.Station));
            // Проверяем попадание в реальную корзину; её центр под продуктами
            // не является отдельной игровой целью с требованием точности 0.3°.
            yield return Aim(inventory.BasketBody.transform.position, false);
            Assert.That(bootstrap.IsPaused, Is.False);
            Assert.That(bootstrap.Player.Target?.name, Is.EqualTo("InventoryBasket"), inventory.Describe(bootstrap.Player.Target));
            yield return Press(Key.E); Assert.That(State.Basket.Count, Is.Zero, inventory.Describe(bootstrap.Player.Target));
        }

        [UnityTest]
        public IEnumerator RealInputsCollectTenRejectEleventhAndCompleteTwoTrips()
        {
            yield return TakeBasket(); yield return Aim(Stock("beef"));
            for (int i = 0; i < 10; i++) yield return Press(Key.E);
            Assert.That(State.Basket.Count, Is.EqualTo(10)); var ids = State.Basket.Select(p => p.Id).ToArray();
            yield return Press(Key.E); CollectionAssert.AreEqual(ids, State.Basket.Select(p => p.Id));
            yield return new WaitForSecondsRealtime(.25f); Capture("inventory-pantry.png");
            yield return DockAndDump(); Assert.That(State.Tray.Count, Is.EqualTo(10));
            yield return Aim(inventory.BasketBody.position); yield return Press(Key.Tab);
            Assert.That(State.Placement, Is.EqualTo(BasketPlacement.Carried));
            Teleport(new Vector3(-2, .05f, 10.3f)); yield return Aim(Stock("potato"));
            Assert.That(bootstrap.Player.Target?.name, Is.EqualTo("Stock_potato"));
            for (int i = 0; i < 10; i++) yield return Press(Key.E);
            Assert.That(State.Basket.Count, Is.EqualTo(10));
            // Чужой стол не принимает корзину даже при реальном Tab под прицелом.
            Teleport(new Vector3(9.7f, .05f, -7.1f));
            var foreign = Object.FindObjectsByType<InventoryInteractable>(FindObjectsSortMode.None).Single(t => t.StationId == "B1" && t.Kind == InventoryTargetKind.BasketDock);
            yield return Aim(foreign.transform.position); yield return Press(Key.Tab);
            Assert.That(State.Placement, Is.EqualTo(BasketPlacement.Carried)); Assert.That(State.Basket.Count, Is.EqualTo(10));
            yield return DockAndDump();
            Assert.That(State.Tray.Count, Is.EqualTo(20)); Assert.That(State.Portions.Select(p => p.Id).Distinct().Count(), Is.EqualTo(20));
            yield return Aim(TrayPoint(10));
            yield return new WaitForSecondsRealtime(.25f); Capture("inventory-station.png");
        }

        [UnityTest]
        public IEnumerator PackagesDropPausePickupAndRestartKeepOneOwner()
        {
            yield return TakeBasket(); Teleport(new Vector3(2.5f, .05f, 10.75f));
            yield return Aim(Stock("potato_sack")); yield return Press(Key.E);
            yield return Aim(Stock("egg_carton")); yield return Press(Key.E);
            Assert.That(State.Basket.Count, Is.EqualTo(2)); Assert.That(State.Basket.All(p => p.Ingredient.Contents != null), Is.True);
            var ids = State.Basket.Select(p => p.Id).ToArray();
            Teleport(new Vector3(0, .05f, 8)); yield return Press(Key.G); yield return new WaitForFixedUpdate();
            Assert.That(State.Placement, Is.EqualTo(BasketPlacement.Floor)); Assert.That(inventory.BasketBody.isKinematic, Is.False);
            bootstrap.SetPaused(true); yield return null; yield return null;
            var frozen = inventory.BasketBody.position; Assert.That(inventory.BasketBody.isKinematic, Is.True);
            yield return new WaitForSecondsRealtime(.15f); Assert.That(Vector3.Distance(frozen, inventory.BasketBody.position), Is.LessThan(.001f));
            bootstrap.SetPaused(false); yield return new WaitForSecondsRealtime(.7f);
            Teleport(new Vector3(inventory.BasketBody.position.x, .05f, inventory.BasketBody.position.z - 1));
            yield return Aim(inventory.BasketBody.position); yield return Press(Key.Tab);
            Assert.That(State.Placement, Is.EqualTo(BasketPlacement.Carried), inventory.Describe(bootstrap.Player.Target) + " · target=" + bootstrap.Player.Target?.name); CollectionAssert.AreEqual(ids, State.Basket.Select(p => p.Id));
            var old = bootstrap.Run; bootstrap.RestartShow(); yield return null;
            Assert.That(old.Disposed, Is.True); Assert.That(State.Basket.Count + State.Tray.Count, Is.Zero);
            Assert.That(State.Placement, Is.EqualTo(BasketPlacement.Pantry));
            Assert.That(Vector3.Distance(inventory.BasketBody.position, inventory.PantryRest.position), Is.LessThan(.001f));
            Assert.That(inventory.BasketCollider.enabled, Is.True);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Q)); yield return null; yield return null;
            Assert.That(bootstrap.Hud.TaskCard.gameObject.activeSelf, Is.True);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null; yield return null;
            Assert.That(bootstrap.Hud.TaskCard.gameObject.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator RealInputsTransferRejectOccupiedAndIncompatibleThenReturnAndDiscard()
        {
            yield return TakeBasket(); Teleport(new Vector3(-2, .05f, 10.3f)); yield return Aim(Stock("potato")); yield return Press(Key.E);
            Assert.That(bootstrap.Player.Target?.name, Is.EqualTo("Stock_potato"));
            Teleport(new Vector3(3, .05f, 10.3f)); yield return Aim(Stock("flour")); yield return Press(Key.E);
            Assert.That(State.Basket.Count, Is.EqualTo(2)); yield return DockAndDump();
            yield return Aim(TrayPoint(0));
            Assert.That(bootstrap.Player.Target?.GetComponent<InventoryInteractable>()?.Index, Is.EqualTo(0));
            yield return Press(Key.E);
            Assert.That(State.Held.Ingredient.Id, Is.EqualTo("potato"),
                "Лоток: " + inventory.Describe(bootstrap.Player.Target) + " target=" + bootstrap.Player.Target?.name
                + " camera=" + bootstrap.Player.ViewCamera.transform.position
                + " forward=" + bootstrap.Player.ViewCamera.transform.forward
                + " point=" + inventory.TrayDisplays[0].transform.position
                + " tray=" + string.Join(",", State.Tray.Select(p => p.Ingredient.Id)));
            string id = State.Held.Id;
            yield return Aim(Socket(0)); yield return Press(Key.E);
            Assert.That(State.Socket(0)?.Id, Is.EqualTo(id));
            yield return Aim(TrayPoint(0)); yield return Press(Key.E);
            Assert.That(State.Held.Ingredient.Id, Is.EqualTo("flour"));
            yield return Aim(Socket(0)); yield return Press(Key.E);
            Assert.That(State.Held.Ingredient.Id, Is.EqualTo("flour")); yield return Cancel();
            yield return Aim(Socket(0)); yield return Press(Key.E);
            yield return Aim(Socket(1)); yield return Press(Key.E);
            Assert.That(State.Socket(1)?.Id, Is.EqualTo(id));
            yield return Aim(TrayPoint(0)); yield return Press(Key.E);
            yield return Aim(Socket(0)); yield return Press(Key.E);
            Assert.That(State.Held.Ingredient.Id, Is.EqualTo("flour")); Assert.That(State.Socket(0), Is.Null); yield return Cancel();
            yield return Aim(Socket(1)); yield return Press(Key.E);
            var trash = Object.FindObjectsByType<InventoryInteractable>(FindObjectsSortMode.None).Single(t => t.StationId == "A1" && t.Kind == InventoryTargetKind.Trash);
            yield return Aim(trash.transform.position); yield return Press(Key.E);
            Assert.That(State.Portions.Count(p => p.Location == PortionLocation.Trash), Is.EqualTo(1));
            yield return Aim(TrayPoint(0)); yield return Press(Key.E);
            Teleport(new Vector3(0, .05f, 10));
            var returns = Object.FindObjectsByType<InventoryInteractable>(FindObjectsSortMode.None).Single(t => t.Kind == InventoryTargetKind.PantryReturn);
            yield return Aim(returns.transform.position); yield return Press(Key.E);
            Assert.That(State.Portions.Count(p => p.Location == PortionLocation.Returned), Is.EqualTo(1)); Assert.That(State.Held, Is.Null);
        }

        [UnityTest]
        public IEnumerator CloseWorkspaceFocusPreservesFoodPauseAndRestoresCamera()
        {
            foreach (var station in Object.FindObjectsByType<ChefShow.Player.PrototypeInteractable>(FindObjectsSortMode.None)
                .Where(t => t.name.StartsWith("Station_")))
            {
                var group = station.transform.Find("Inventory");
                float side = station.name.StartsWith("Station_A") ? -1 : 1;
                float Near(string name) => side * (group.Find(name).position.x - station.transform.position.x);
                Assert.That(Near("Board"), Is.GreaterThan(Near("Basket Dock") + 0.9f));
                Assert.That(Near("Work Surface"), Is.GreaterThan(Near("Ingredient Tray") + 0.9f));
                Assert.That(station.FocusPoint, Is.Not.Null);
            }
            yield return TakeBasket(); Teleport(new Vector3(-2, .05f, 10.3f));
            yield return Aim(Stock("potato")); yield return Press(Key.E); yield return DockAndDump();
            yield return Aim(TrayPoint(0)); yield return Press(Key.E);
            var id = State.Held.Id;
            yield return Aim(Socket(0)); yield return Press(Key.E);
            Assert.That(State.Socket(0)?.Id, Is.EqualTo(id));
            var ownStation = GameObject.Find("Station_A1").GetComponent<ChefShow.Player.PrototypeInteractable>();
            yield return Aim(ownStation.FocusPoint.position);
            Assert.That(bootstrap.Player.Target, Is.SameAs(ownStation));
            var camera = bootstrap.Player.ViewCamera;
            var home = camera.transform.localPosition;
            float ordinaryFov = camera.fieldOfView;
            float ordinaryDistance = Vector3.Distance(camera.transform.position, inventory.SocketDisplays[0].transform.position);
            Assert.That(ordinaryFov, Is.EqualTo(60).Within(.01f));
            Capture("workspace-normal.png");
            yield return Press(Key.E); yield return new WaitForSecondsRealtime(.25f);
            Assert.That(bootstrap.Player.Focused, Is.True);
            Assert.That(Vector3.Distance(camera.transform.position, inventory.SocketDisplays[0].transform.position), Is.LessThan(ordinaryDistance - .15f));
            Assert.That(camera.fieldOfView, Is.EqualTo(52).Within(.01f));
            Capture("workspace-focus.png");
            yield return Aim(Socket(0)); yield return Press(Key.E);
            Assert.That(State.Held?.Id, Is.EqualTo(id));
            yield return Aim(Socket(1)); yield return Press(Key.E);
            Assert.That(State.Socket(1)?.Id, Is.EqualTo(id));
            bootstrap.SetPaused(true);
            var frozen = camera.transform.localPosition; float frozenFov = camera.fieldOfView;
            float remaining = bootstrap.Run.RemainingSeconds;
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(camera.transform.localPosition, Is.EqualTo(frozen));
            Assert.That(camera.fieldOfView, Is.EqualTo(frozenFov));
            Assert.That(bootstrap.Run.RemainingSeconds, Is.EqualTo(remaining));
            bootstrap.SetPaused(false); yield return Cancel(); yield return new WaitForSecondsRealtime(.25f);
            Assert.That(bootstrap.Player.Focused, Is.False);
            Assert.That(Vector3.Distance(camera.transform.localPosition, home), Is.LessThan(.001f));
            Assert.That(camera.fieldOfView, Is.EqualTo(ordinaryFov).Within(.001f));
            Assert.That(State.Socket(1)?.Id, Is.EqualTo(id));
            yield return Aim(ownStation.FocusPoint.position); yield return Press(Key.E);
            yield return new WaitForSecondsRealtime(.25f);
            bootstrap.RestartShow(); yield return null;
            Assert.That(bootstrap.Player.Focused, Is.False);
            Assert.That(Vector3.Distance(camera.transform.localPosition, home), Is.LessThan(.001f));
            Assert.That(camera.fieldOfView, Is.EqualTo(ordinaryFov).Within(.001f));
            Assert.That(Mathf.Abs(bootstrap.Player.transform.position.x), Is.EqualTo(9.65f).Within(.01f));
            Assert.That(State.Socket(0), Is.Null); Assert.That(State.Socket(1), Is.Null);
        }

        [UnityTest]
        public IEnumerator MirroredFocusUsesVisibleStationAnchorAndCannotMoveThroughObstacle()
        {
            // Изолированная проверка камеры B на реальной геометрии. Конфиг и
            // action asset клонируются; полный инвентарь B здесь не симулируется.
            bootstrap.SetPaused(true);
            var settings = Object.Instantiate(bootstrap.Config); settings.PlayerTeam = ChefShow.Data.TeamId.B;
            var actions = Object.Instantiate(bootstrap.InputDefinition);
            var rig = bootstrap.Player;
            var home = rig.ViewCamera.transform.localPosition;
            var station = GameObject.Find("Station_B1").GetComponent<ChefShow.Player.PrototypeInteractable>();
            bool wasPlayerStation = station.IsPlayerStation;
            var ownNpc = GameObject.Find("NPC_B1").GetComponent<Collider>();
            bool npcColliderEnabled = ownNpc.enabled;
            GameObject blocker = null;
            try
            {
                Teleport(new Vector3(9.65f, .05f, station.transform.position.z));
                rig.transform.rotation = Quaternion.Euler(0, -90, 0);
                rig.ViewCamera.transform.localRotation = Quaternion.Euler(35, 0, 0);
                rig.Initialize(settings, actions); station.IsPlayerStation = true;
                // При реальном выборе B Builder не создаёт NPC на месте игрока.
                ownNpc.enabled = false; Physics.SyncTransforms();
                var clock = new GameClock();
                actions.FindActionMap("Gameplay", true).Enable();
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E)); yield return null;
                clock.Tick(.02f); rig.Step(clock, true);
                Assert.That(rig.Focused, Is.True);
                actions.FindActionMap("Gameplay", true).Disable(); actions.FindActionMap("Station", true).Enable();
                InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null; yield return null;
                for (int i = 0; i < 5; i++) { clock.Tick(.05f); rig.Step(clock, true); }
                float freeOffset = Vector3.Distance(rig.ViewCamera.transform.localPosition, home);
                Assert.That(freeOffset, Is.GreaterThan(.25f));
                Assert.That(rig.ViewCamera.transform.position.x, Is.LessThan(9.65f));
                Assert.That(rig.ViewCamera.fieldOfView, Is.EqualTo(52).Within(.01f));
                Capture("workspace-focus-b.png");
                blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
                blocker.transform.position = rig.transform.TransformPoint(home + new Vector3(0, -.18f, .22f) * .95f);
                blocker.transform.localScale = Vector3.one * .06f;
                Physics.SyncTransforms(); clock.Tick(.05f); rig.Step(clock, true);
                Assert.That(Vector3.Distance(rig.ViewCamera.transform.localPosition, home), Is.LessThan(freeOffset - .05f));
                Assert.That(blocker.GetComponent<Collider>().bounds.Contains(rig.ViewCamera.transform.position), Is.False);
                rig.ResetRig();
                Assert.That(rig.Focused, Is.False);
                Assert.That(Vector3.Distance(rig.ViewCamera.transform.localPosition, home), Is.LessThan(.001f));
                Assert.That(rig.ViewCamera.fieldOfView, Is.EqualTo(60).Within(.001f));
            }
            finally
            {
                station.IsPlayerStation = wasPlayerStation;
                ownNpc.enabled = npcColliderEnabled;
                if (blocker != null) Object.Destroy(blocker);
                actions.Disable(); Object.Destroy(actions); Object.Destroy(settings);
            }
        }

        private IEnumerator AimDrawer(ToolDrawer drawer)
        {
            var bounds = drawer.GetComponent<BoxCollider>().bounds;
            var point = drawer.IsOpen ? drawer.Tray.TransformPoint(new Vector3(.23f, -.02f, .76f))
                : bounds.center + Vector3.up * bounds.extents.y * .9f;
            yield return Aim(point);
            var aimed = bootstrap.Player.Target;
            var actual = aimed == null ? null : aimed.GetComponent<ToolDrawer>();
            if (actual == null && aimed != null) actual = aimed.GetComponent<ToolDrawerTarget>()?.Drawer;
            Assert.That(actual, Is.SameAs(drawer), "Drawer aim=" + aimed?.name);
        }
        private IEnumerator OpenDrawer(ToolDrawer drawer)
        {
            yield return AimDrawer(drawer); yield return Press(Key.E);
            Assert.That(drawer.IsOpen, Is.True);
            yield return new WaitForSecondsRealtime(drawer.SlideSeconds + .05f);
        }
        private IEnumerator AimTool(KitchenTool tool)
        {
            yield return Aim(tool.PickupCollider.bounds.center);
            Assert.That(bootstrap.Player.Target?.GetComponent<KitchenTool>(), Is.SameAs(tool), "Tool=" + tool.transform.position + " collider=" + tool.PickupCollider.bounds + " camera=" + bootstrap.Player.ViewCamera.transform.position + " aimed=" + bootstrap.Player.Target?.name);
        }
        private KitchenTool Tool(ToolDrawer drawer, int index) => drawer.Tools[index].GetComponent<KitchenTool>();

        [UnityTest]
        public IEnumerator DirectDrawerSelectsFourWorldToolsAndKeepsMovementLookAndTimer()
        {
            var tools = bootstrap.Tools; Assert.That(tools.Validate(), Is.Null);
            var drawer = tools.Drawers.Single(d => d.StationId == inventory.PlayerStationId);
            Assert.That(bootstrap.Hud.transform.Find("Tool Drawer Menu"), Is.Null);
            var facts = new System.Collections.Generic.List<KitchenToolChanged>();
            using (bootstrap.Run.Events.Subscribe<KitchenToolChanged>(facts.Add))
            {
                yield return AimDrawer(drawer);
                Assert.That(bootstrap.Hud.InteractionKey.enabled, Is.True);
                Assert.That(bootstrap.Hud.Context.text, Is.EqualTo("Открыть ящик"));
                float alpha = bootstrap.Hud.InteractionKey.color.a;
                yield return new WaitForSecondsRealtime(.15f);
                Assert.That(Mathf.Abs(bootstrap.Hud.InteractionKey.color.a - alpha), Is.GreaterThan(.01f));
                yield return Press(Key.E);
                Assert.That(tools.Equipped, Is.EqualTo(KitchenToolKind.None));
                Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.Locked));
                var position = bootstrap.Player.transform.position; float time = bootstrap.Run.RemainingSeconds;
                yield return Press(Key.A);
                Assert.That(Vector3.Distance(bootstrap.Player.transform.position, position), Is.GreaterThan(.001f));
                Assert.That(bootstrap.Run.RemainingSeconds, Is.LessThan(time));
                yield return new WaitForSecondsRealtime(drawer.SlideSeconds + .05f);
                for (int i = 0; i < 4; i++)
                {
                    var tool = Tool(drawer, i);
                    yield return AimTool(tool);
                    Assert.That(bootstrap.Hud.Context.text, Is.EqualTo("Взять " + ToolDrawerController.Accusative(tool.Kind)));
                    yield return Press(Key.E);
                    Assert.That(tools.EquippedObject, Is.SameAs(tool));
                    Assert.That(tool.transform.parent, Is.SameAs(tools.RightHand));
                    Assert.That(tool.PickupCollider.enabled, Is.False);
                    Assert.That(tool.GetComponentsInChildren<Renderer>().All(r => r.enabled), Is.True);
                    Assert.That(drawer.IsOpen, Is.True, "Выбор предмета не закрывает ящик.");
                    if (i > 0) Assert.That(Tool(drawer, i-1).transform.parent, Is.SameAs(drawer.Compartments[i-1]));
                }
                Assert.That(State.Portions.Count, Is.Zero); Assert.That(facts.Count, Is.EqualTo(4));
                Assert.That(facts[0].RunId, Is.EqualTo(bootstrap.Run.RunId)); Assert.That(facts[0].ActorId, Is.EqualTo("A1"));
                yield return AimDrawer(drawer);
                Assert.That(bootstrap.Hud.Context.text, Is.EqualTo("Закрыть ящик"));
                yield return Press(Key.E); Assert.That(drawer.IsOpen, Is.False);
                yield return new WaitForSecondsRealtime(drawer.SlideSeconds + .05f);
                Assert.That(drawer.Tray.localPosition, Is.EqualTo(drawer.ClosedPosition));
            }
        }

        [UnityTest]
        public IEnumerator FoodMovesToLeftHandAndTransfersWithKnifeWithoutChangingId()
        {
            var tools = bootstrap.Tools; var drawer = tools.Drawers.Single(d => d.StationId == inventory.PlayerStationId);
            yield return TakeBasket(); yield return Aim(Stock("potato")); yield return Press(Key.E); yield return DockAndDump();
            yield return Aim(TrayPoint(0));
            Assert.That(bootstrap.Hud.Context.text, Is.EqualTo("Взять картошку"));
            Assert.That(bootstrap.Hud.InteractionKey.enabled, Is.True);
            yield return Press(Key.E); var id = State.Held.Id;
            var originalParent = inventory.HeldDisplay.transform.parent;
            Assert.That(inventory.FoodInLeftHand, Is.False);
            yield return OpenDrawer(drawer); var knife = Tool(drawer, 0);
            yield return AimTool(knife); yield return Press(Key.E);
            Assert.That(State.Held.Id, Is.EqualTo(id)); Assert.That(State.Portions.Count, Is.EqualTo(1));
            Assert.That(inventory.HeldDisplay.transform.parent, Is.SameAs(tools.LeftFoodHand));
            Assert.That(inventory.HeldDisplay.Visual.enabled, Is.True); Assert.That(inventory.FoodInLeftHand, Is.True);
            Assert.That(knife.transform.parent, Is.SameAs(tools.RightHand));
            Capture("direct-tools-two-hands.png");
            yield return Aim(Socket(0)); yield return Press(Key.E);
            Assert.That(State.Socket(0).Id, Is.EqualTo(id)); Assert.That(State.Held, Is.Null);
            Assert.That(tools.EquippedObject, Is.SameAs(knife));
            yield return Press(Key.E); Assert.That(State.Held.Id, Is.EqualTo(id));
            Assert.That(inventory.HeldDisplay.transform.parent, Is.SameAs(tools.LeftFoodHand));
            yield return Cancel(); Assert.That(State.Socket(0).Id, Is.EqualTo(id));
            yield return Press(Key.E); Assert.That(State.Held.Id, Is.EqualTo(id));
            yield return Press(Key.G);
            Assert.That(tools.Equipped, Is.EqualTo(KitchenToolKind.None));
            Assert.That(knife.Placement, Is.EqualTo(KitchenToolPlacement.Dropped));
            Assert.That(State.Held.Id, Is.EqualTo(id)); Assert.That(inventory.HeldDisplay.transform.parent, Is.SameAs(originalParent));
            Assert.That(State.Placement, Is.EqualTo(BasketPlacement.Station), "G инструмента не роняет корзину.");
            yield return Cancel();
            var oldRun = bootstrap.Run; bootstrap.RestartShow(); yield return null;
            Assert.That(oldRun.Disposed, Is.True); Assert.That(State.Portions.Count, Is.Zero);
            Assert.That(knife.transform.parent, Is.SameAs(drawer.Compartments[0])); Assert.That(drawer.IsOpen, Is.False);
        }

        [UnityTest]
        public IEnumerator DroppedKnifeUsesSameObjectPausePickupTimeoutAndRestart()
        {
            var tools = bootstrap.Tools; var drawer = tools.Drawers.Single(d => d.StationId == inventory.PlayerStationId);
            var knife = Tool(drawer, 0); var objectId = knife.GetInstanceID();
            yield return OpenDrawer(drawer); yield return AimTool(knife); yield return Press(Key.E); yield return Press(Key.G);
            Assert.That(knife.Body.isKinematic, Is.False); Assert.That(knife.PickupCollider.isTrigger, Is.False);
            bootstrap.SetPaused(true); yield return null; var frozen = knife.transform.position;
            yield return new WaitForSecondsRealtime(.1f);
            Assert.That(knife.transform.position, Is.EqualTo(frozen)); Assert.That(knife.Body.isKinematic, Is.True);
            yield return Press(Key.E); Assert.That(tools.EquippedObject, Is.Null);
            bootstrap.SetPaused(false); yield return new WaitForSecondsRealtime(.8f);
            Teleport(new Vector3(knife.transform.position.x - 1.1f, .05f, knife.transform.position.z));
            yield return AimTool(knife); yield return Press(Key.E);
            Assert.That(tools.EquippedObject, Is.SameAs(knife)); Assert.That(knife.GetInstanceID(), Is.EqualTo(objectId));
            Assert.That(knife.Body.isKinematic, Is.True); Assert.That(knife.transform.parent, Is.SameAs(tools.RightHand));
            Capture("direct-tools-knife.png");
            yield return Press(Key.G); bootstrap.Run.SetRemaining(.02f); yield return new WaitForSecondsRealtime(.4f);
            Assert.That(drawer.IsOpen, Is.False); Assert.That(drawer.Tray.localPosition, Is.EqualTo(drawer.ClosedPosition));
            Assert.That(knife.Body.isKinematic, Is.True); yield return Press(Key.E); Assert.That(tools.EquippedObject, Is.Null);
            bootstrap.RestartShow(); yield return null;
            Assert.That(knife.Placement, Is.EqualTo(KitchenToolPlacement.Stored)); Assert.That(knife.transform.parent, Is.SameAs(drawer.Compartments[0]));
            Assert.That(Object.FindObjectsByType<KitchenTool>(FindObjectsSortMode.None).Length, Is.EqualTo(48));
        }

        [UnityTest]
        public IEnumerator BasketLeftHandConflictRejectsWithoutLosingFoodOrTool()
        {
            var tools = bootstrap.Tools; var drawer = tools.Drawers.Single(d => d.StationId == inventory.PlayerStationId);
            yield return TakeBasket(); yield return Aim(Stock("potato")); yield return Press(Key.E); yield return DockAndDump();
            yield return OpenDrawer(drawer);
            yield return Aim(inventory.BasketCollider.bounds.center + Vector3.up * inventory.BasketCollider.bounds.extents.y * .7f);
            Assert.That(bootstrap.Player.Target?.GetComponent<InventoryInteractable>()?.Kind, Is.EqualTo(InventoryTargetKind.Basket), "Basket aim=" + bootstrap.Player.Target?.name);
            yield return Press(Key.Tab);
            Assert.That(State.Placement, Is.EqualTo(BasketPlacement.Carried));
            yield return Aim(TrayPoint(0)); yield return Press(Key.E); var id = State.Held.Id;
            var knife = Tool(drawer, 0); yield return AimTool(knife); yield return Press(Key.E);
            Assert.That(tools.Equipped, Is.EqualTo(KitchenToolKind.None)); Assert.That(State.Held.Id, Is.EqualTo(id));
            Assert.That(tools.Describe(bootstrap.Player.Target), Does.Contain("корзина"));
            yield return Cancel();
            yield return Aim(TrayPoint(0)); yield return Press(Key.Tab); Assert.That(State.Placement, Is.EqualTo(BasketPlacement.Station));
            yield return AimTool(knife); yield return Press(Key.E); Assert.That(tools.EquippedObject, Is.SameAs(knife),
                "held=" + State.Held?.Id + " basket=" + State.Placement + " drawer=" + drawer.IsOpen + " ready=" + drawer.CanTakeTools
                + " target=" + bootstrap.Player.Target?.name + " paused=" + bootstrap.IsPaused + " reason=" + tools.Describe(bootstrap.Player.Target));
            yield return Aim(inventory.BasketCollider.bounds.center + Vector3.up * inventory.BasketCollider.bounds.extents.y * .7f);
            Assert.That(bootstrap.Player.Target?.GetComponent<InventoryInteractable>()?.Kind, Is.EqualTo(InventoryTargetKind.Basket), "Basket aim=" + bootstrap.Player.Target?.name);
            yield return Press(Key.Tab);
            yield return Aim(TrayPoint(0)); yield return Press(Key.E);
            Assert.That(State.Held, Is.Null); Assert.That(State.Tray[0].Id, Is.EqualTo(id)); Assert.That(tools.EquippedObject, Is.SameAs(knife));
            yield return Press(Key.Tab); Assert.That(State.Placement, Is.EqualTo(BasketPlacement.Station));
            yield return Press(Key.E); Assert.That(State.Held.Id, Is.EqualTo(id)); Assert.That(inventory.FoodInLeftHand, Is.True);
        }

        [UnityTest]
        public IEnumerator ForeignDrawerAndWorldToolCannotBeTaken()
        {
            var tools = bootstrap.Tools; var drawer = tools.Drawers.Single(d => d.StationId == "A2");
            Teleport(new Vector3(-9.65f, .05f, -3)); yield return AimDrawer(drawer); yield return Press(Key.E);
            Assert.That(drawer.IsOpen, Is.False); Assert.That(tools.Equipped, Is.EqualTo(KitchenToolKind.None));
            drawer.AnimateOpen(true); yield return new WaitForSecondsRealtime(drawer.SlideSeconds + .05f);
            var foreign = Tool(drawer, 0); Teleport(new Vector3(-10.4f, .05f, -4.3f)); yield return AimTool(foreign); yield return Press(Key.E);
            Assert.That(tools.Equipped, Is.EqualTo(KitchenToolKind.None)); Assert.That(foreign.Placement, Is.EqualTo(KitchenToolPlacement.Stored));
        }

        [UnityTest]
        public IEnumerator DrawerMovesFourCellsAndKeepsTriggerAvailableInBothRows()
        {
            foreach (var drawer in bootstrap.Tools.Drawers)
            {
                Assert.That(drawer.Compartments.Length, Is.EqualTo(4));
                Assert.That(drawer.transform.IsChildOf(drawer.Tray), Is.True);
                for (int i = 0; i < 4; i++) Assert.That(drawer.Tools[i].parent, Is.EqualTo(drawer.Compartments[i]));
                var front = drawer.transform.position; var cells = drawer.Compartments.Select(c => c.position).ToArray();
                drawer.AnimateOpen(true); yield return null;
                Assert.That(Vector3.Distance(drawer.transform.position, front), Is.GreaterThan(0));
                bootstrap.SetPaused(true); var halfway = drawer.Tray.position;
                yield return new WaitForSecondsRealtime(.05f); Assert.That(drawer.Tray.position, Is.EqualTo(halfway));
                bootstrap.SetPaused(false); yield return new WaitForSecondsRealtime(drawer.SlideSeconds + .05f);
                var expected = drawer.OpenOffset;
                Assert.That(Vector3.Distance(drawer.transform.position - front, expected), Is.LessThan(.001f));
                Assert.That(Mathf.Sign(expected.x), Is.EqualTo(drawer.StationId.StartsWith("A") ? -1 : 1));
                for (int i = 0; i < 4; i++) Assert.That(Vector3.Distance(drawer.Compartments[i].position - cells[i], expected), Is.LessThan(.001f));
                Assert.That(drawer.GetComponent<Collider>().enabled, Is.True); Assert.That(drawer.GetComponent<Collider>().isTrigger, Is.True);
                drawer.AnimateOpen(false); yield return new WaitForSecondsRealtime(drawer.SlideSeconds + .05f);
                Assert.That(Vector3.Distance(drawer.transform.position, front), Is.LessThan(.001f));
            }
        }
        [UnityTest]
        public IEnumerator ECollectsAndTransfersWhileSixClicksCutOneBeefPortionInNormalAndFocus()
        {
            Assert.That(bootstrap.Preparation.Validate(inventory), Is.Null);
            yield return TakeBasket(); yield return Aim(Stock("beef"));
            Assert.That(bootstrap.Hud.Context.text, Is.EqualTo("Взять говядину в корзину"));
            Assert.That(bootstrap.Hud.InteractionKey.enabled, Is.True);
            yield return Click(); Assert.That(State.Basket.Count, Is.Zero, "ЛКМ больше не подбирает продукты.");
            yield return Press(Key.E); Assert.That(State.Basket.Count, Is.EqualTo(1)); yield return DockAndDump();
            yield return Aim(TrayPoint(0)); yield return Click(); Assert.That(State.Held, Is.Null);
            yield return Press(Key.E); var food = State.Held; string id = food.Id;
            var drawer = bootstrap.Tools.Drawers.Single(d => d.StationId == inventory.PlayerStationId);
            yield return OpenDrawer(drawer); yield return AimTool(Tool(drawer, 0)); yield return Press(Key.E);
            Assert.That(inventory.FoodInLeftHand, Is.True); Assert.That(State.Held.Id, Is.EqualTo(id));
            yield return Aim(Socket(0)); yield return Press(Key.E);
            Assert.That(bootstrap.Hud.Context.text, Does.Contain("ЛКМ — нарезать · 0/6"));
            var facts = new System.Collections.Generic.List<PreparationChanged>();
            using (bootstrap.Run.Events.Subscribe<PreparationChanged>(facts.Add))
            {
                InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Left)); yield return null; yield return null;
                Assert.That(food.ChopPresses, Is.EqualTo(1));
                var knife = bootstrap.Tools.EquippedObject; var frozen = knife.transform.position;
                bootstrap.SetPaused(true); yield return new WaitForSecondsRealtime(.1f);
                Assert.That(knife.transform.position, Is.EqualTo(frozen)); Assert.That(food.ChopPresses, Is.EqualTo(1));
                yield return Click(); Assert.That(food.ChopPresses, Is.EqualTo(1));
                bootstrap.SetPaused(false);
                yield return Click(); yield return Click(); Assert.That(food.ChopPresses, Is.EqualTo(3));
                Assert.That(inventory.SocketDisplays[0].CutPieces.Count(r => r.enabled), Is.EqualTo(4));
                Capture("chopping-progress.png");
                yield return Press(Key.E); Assert.That(State.Held.Id, Is.EqualTo(id));
                Assert.That(inventory.HeldDisplay.CutPieces.Count(r => r.enabled), Is.EqualTo(4));
                yield return Aim(Socket(1)); yield return Press(Key.E); yield return Click();
                Assert.That(food.ChopPresses, Is.EqualTo(3), "Место продукта не является доской для нарезки.");
                yield return Press(Key.E); yield return Aim(Socket(0)); yield return Press(Key.E);
                var halfway = food.Snapshot();
                var station = GameObject.Find("Station_A1").GetComponent<ChefShow.Player.PrototypeInteractable>();
                yield return Aim(station.FocusPoint.position); yield return Press(Key.E); yield return new WaitForSecondsRealtime(.25f);
                Assert.That(bootstrap.Player.Focused, Is.True); yield return Aim(Socket(0));
                yield return Click(); yield return Click(); yield return Click();
                Assert.That(food.ChopPresses, Is.EqualTo(6)); Assert.That(food.Preparation, Is.EqualTo(PreparationState.Chopped));
                Assert.That(food.Cooking, Is.EqualTo(CookState.Raw)); Assert.That(food.HeatProgress, Is.Zero);
                Assert.That(food.Quantity, Is.EqualTo(1)); Assert.That(State.Portions.Count, Is.EqualTo(1));
                Assert.That(halfway.ChopPresses, Is.EqualTo(3)); Assert.That(facts.Count, Is.EqualTo(6));
                Assert.That(facts.Count(f => f.Action == "preparation_completed"), Is.EqualTo(1));
                Assert.That(inventory.SocketDisplays[0].Visual.enabled, Is.False);
                Assert.That(inventory.SocketDisplays[0].CutPieces.Count(r => r.enabled), Is.EqualTo(7));
                Capture("chopping-complete.png");
                yield return Click(); Assert.That(facts.Count, Is.EqualTo(6));
                yield return Press(Key.E); Assert.That(State.Held.Id, Is.EqualTo(id));
                Assert.That(State.Held.ChopPresses, Is.EqualTo(6));
                Assert.That(inventory.HeldDisplay.CutPieces.Count(r => r.enabled), Is.EqualTo(7));
                yield return Cancel(); Assert.That(State.Socket(0).Id, Is.EqualTo(id));
                Assert.That(State.Socket(0).Preparation, Is.EqualTo(PreparationState.Chopped));
            }
        }

        [UnityTest]
        public IEnumerator CuttingRejectsWrongToolAndForeignBoardThenFreezesAtTimeoutAndRestartsClean()
        {
            yield return TakeBasket(); yield return Aim(Stock("potato")); yield return Press(Key.E); yield return DockAndDump();
            yield return Aim(TrayPoint(0)); yield return Press(Key.E); yield return Aim(Socket(0)); yield return Press(Key.E);
            var food = State.Socket(0); yield return Click(); Assert.That(food.ChopPresses, Is.Zero);
            var drawer = bootstrap.Tools.Drawers.Single(d => d.StationId == inventory.PlayerStationId);
            yield return OpenDrawer(drawer); yield return AimTool(Tool(drawer, 1)); yield return Press(Key.E);
            yield return Aim(Socket(0)); yield return Click(); Assert.That(food.ChopPresses, Is.Zero);
            yield return AimTool(Tool(drawer, 0)); yield return Press(Key.E);
            var foreign = bootstrap.Preparation.Boards.Single(b => b.StationId == "B1");
            Teleport(new Vector3(9.7f, .05f, -7.1f)); yield return Aim(foreign.transform.position);
            Assert.That(bootstrap.Player.Target.GetComponent<ChoppingBoard>(), Is.SameAs(foreign));
            yield return Click(); Assert.That(food.ChopPresses, Is.Zero);
            Teleport(new Vector3(-9.65f, .05f, -6.25f)); yield return Aim(Socket(0)); yield return Click();
            Assert.That(food.ChopPresses, Is.EqualTo(1)); bootstrap.Run.SetRemaining(.01f);
            yield return new WaitForSecondsRealtime(.25f); var knife = bootstrap.Tools.EquippedObject;
            var frozen = knife.transform.position; yield return Click(); yield return Press(Key.E);
            Assert.That(food.ChopPresses, Is.EqualTo(1)); Assert.That(State.Held, Is.Null);
            Assert.That(food.Preparation, Is.EqualTo(PreparationState.Whole)); Assert.That(knife.transform.position, Is.EqualTo(frozen));
            var old = bootstrap.Run; bootstrap.RestartShow(); yield return null;
            Assert.That(old.Disposed, Is.True); Assert.That(State.Portions.Count, Is.Zero);
            Assert.That(inventory.SocketDisplays.Concat(inventory.TrayDisplays).Append(inventory.HeldDisplay)
                .All(v => v.CutPieces.All(r => !r.enabled)), Is.True);
            Assert.That(bootstrap.Tools.EquippedObject, Is.Null); Assert.That(knife.Placement, Is.EqualTo(KitchenToolPlacement.Stored));
            Assert.That(bootstrap.Preparation.Boards.All(b => b.Caption.text == "ДОСКА"), Is.True);
        }

        private void Capture(string filename)
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return;
            var target = new RenderTexture(1280, 720, 24); target.Create(); var previous = RenderTexture.active;
            var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            try
            {
                Canvas.ForceUpdateCanvases();
                RenderPipeline.SubmitRenderRequest(bootstrap.Player.ViewCamera, new RenderPipeline.StandardRequest { destination = target });
                RenderTexture.active = target; image.ReadPixels(new Rect(0,0,1280,720),0,0); image.Apply();
                Directory.CreateDirectory("TestResults"); File.WriteAllBytes("TestResults/" + filename, image.EncodeToPNG());
            }
            finally { RenderTexture.active = previous; target.Release(); Object.Destroy(target); Object.Destroy(image); }
        }
    }
}
