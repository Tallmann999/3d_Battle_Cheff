using System.Collections;
using System.IO;
using System.Linq;
using ChefShow.Core;
using ChefShow.Inventory;
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
#if UNITY_EDITOR
            testSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            InputSystem.settings = testSettings;
            keyboard = InputSystem.AddDevice<Keyboard>(); mouse = InputSystem.AddDevice<Mouse>();
            yield return SceneManager.LoadSceneAsync("ChefShow_Prototype", LoadSceneMode.Single);
            yield return null;
            bootstrap = Object.FindFirstObjectByType<GameBootstrap>(); inventory = bootstrap.Inventory;
            Assert.That(inventory, Is.Not.Null); Assert.That(inventory.Validate(), Is.Null);
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
        private IEnumerator Aim(Vector3 point)
        {
            // После teleport даём CharacterController опуститься на пол прежде,
            // чем рассчитывать угол. В игре игрок доходит до цели уже на земле.
            yield return null; yield return null;
            var camera = bootstrap.Player.ViewCamera.transform;
            var desired = Quaternion.LookRotation(point - camera.position).eulerAngles;
            var current = camera.eulerAngles;
            InputSystem.QueueDeltaStateEvent(mouse.delta, new Vector2(Mathf.DeltaAngle(current.y, desired.y), -Mathf.DeltaAngle(current.x, desired.x)) / bootstrap.Config.LookSensitivity);
            yield return null; yield return null;
            bootstrap.Player.RefreshTarget();
            Assert.That(Vector3.Angle(point - camera.position, camera.forward), Is.LessThan(0.3f),
                "Наведение камеры: цель=" + point + " камера=" + camera.position + " взгляд=" + camera.forward + " объект=" + bootstrap.Player.Target?.name);
        }
        private IEnumerator Press(Key key)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key)); yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
        }
        private IEnumerator Click()
        {
            InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Left)); yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState()); yield return null;
        }
        private IEnumerator Cancel()
        {
            InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Right)); yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState()); yield return null;
        }
        private Vector3 Stock(string id) => GameObject.Find("Stock_" + id).transform.position;
        private Vector3 Socket(int index) => inventory.SocketDisplays[index].transform.parent.position;
        private IEnumerator TakeBasket()
        {
            Teleport(new Vector3(-4.08f, .05f, 10.3f)); yield return Aim(inventory.BasketBody.position);
            yield return Press(Key.Tab); Assert.That(State.Placement, Is.EqualTo(BasketPlacement.Carried));
        }
        private IEnumerator DockAndDump()
        {
            Teleport(new Vector3(-9.7f, .05f, -6.25f)); yield return Aim(inventory.StationDock.position);
            yield return Press(Key.Tab); Assert.That(State.Placement, Is.EqualTo(BasketPlacement.Station));
            yield return Aim(inventory.BasketBody.transform.position);
            Assert.That(bootstrap.Player.Target?.name, Is.EqualTo("InventoryBasket"), inventory.Describe(bootstrap.Player.Target));
            yield return Click(); Assert.That(State.Basket.Count, Is.Zero, inventory.Describe(bootstrap.Player.Target));
        }

        [UnityTest]
        public IEnumerator RealInputsCollectTenRejectEleventhAndCompleteTwoTrips()
        {
            yield return TakeBasket(); yield return Aim(Stock("beef"));
            for (int i = 0; i < 10; i++) yield return Click();
            Assert.That(State.Basket.Count, Is.EqualTo(10)); var ids = State.Basket.Select(p => p.Id).ToArray();
            yield return Click(); CollectionAssert.AreEqual(ids, State.Basket.Select(p => p.Id));
            yield return new WaitForSecondsRealtime(.25f); Capture("inventory-pantry.png");
            yield return DockAndDump(); Assert.That(State.Tray.Count, Is.EqualTo(10));
            yield return Aim(inventory.BasketBody.position); yield return Press(Key.Tab);
            Assert.That(State.Placement, Is.EqualTo(BasketPlacement.Carried));
            Teleport(new Vector3(-2, .05f, 10.3f)); yield return Aim(Stock("potato"));
            Assert.That(bootstrap.Player.Target?.name, Is.EqualTo("Stock_potato"));
            for (int i = 0; i < 10; i++) yield return Click();
            Assert.That(State.Basket.Count, Is.EqualTo(10));
            // Чужой стол не принимает корзину даже при реальном Tab под прицелом.
            Teleport(new Vector3(9.7f, .05f, -7.1f));
            var foreign = Object.FindObjectsByType<InventoryInteractable>(FindObjectsSortMode.None).Single(t => t.StationId == "B1" && t.Kind == InventoryTargetKind.BasketDock);
            yield return Aim(foreign.transform.position); yield return Press(Key.Tab);
            Assert.That(State.Placement, Is.EqualTo(BasketPlacement.Carried)); Assert.That(State.Basket.Count, Is.EqualTo(10));
            yield return DockAndDump();
            Assert.That(State.Tray.Count, Is.EqualTo(20)); Assert.That(State.Portions.Select(p => p.Id).Distinct().Count(), Is.EqualTo(20));
            yield return Aim(inventory.TrayDisplays[10].transform.position);
            yield return new WaitForSecondsRealtime(.25f); Capture("inventory-station.png");
        }

        [UnityTest]
        public IEnumerator PackagesDropPausePickupAndRestartKeepOneOwner()
        {
            yield return TakeBasket(); Teleport(new Vector3(2.5f, .05f, 10.75f));
            yield return Aim(Stock("potato_sack")); yield return Click();
            yield return Aim(Stock("egg_carton")); yield return Click();
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
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
            Assert.That(bootstrap.Hud.TaskCard.gameObject.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator RealInputsTransferRejectOccupiedAndIncompatibleThenReturnAndDiscard()
        {
            yield return TakeBasket(); Teleport(new Vector3(-2, .05f, 10.3f)); yield return Aim(Stock("potato")); yield return Click();
            Assert.That(bootstrap.Player.Target?.name, Is.EqualTo("Stock_potato"));
            Teleport(new Vector3(3, .05f, 10.3f)); yield return Aim(Stock("flour")); yield return Click();
            Assert.That(State.Basket.Count, Is.EqualTo(2)); yield return DockAndDump();
            yield return Aim(inventory.TrayDisplays[0].transform.position); yield return Press(Key.E);
            Assert.That(State.Held.Ingredient.Id, Is.EqualTo("potato"),
                "Лоток: " + inventory.Describe(bootstrap.Player.Target) + " target=" + bootstrap.Player.Target?.name
                + " camera=" + bootstrap.Player.ViewCamera.transform.position
                + " forward=" + bootstrap.Player.ViewCamera.transform.forward
                + " point=" + inventory.TrayDisplays[0].transform.position
                + " tray=" + string.Join(",", State.Tray.Select(p => p.Ingredient.Id)));
            string id = State.Held.Id;
            yield return Aim(Socket(0)); yield return Press(Key.E);
            Assert.That(State.Socket(0)?.Id, Is.EqualTo(id));
            yield return Aim(inventory.TrayDisplays[0].transform.position); yield return Press(Key.E);
            Assert.That(State.Held.Ingredient.Id, Is.EqualTo("flour"));
            yield return Aim(Socket(0)); yield return Press(Key.E);
            Assert.That(State.Held.Ingredient.Id, Is.EqualTo("flour")); yield return Cancel();
            yield return Aim(Socket(0)); yield return Press(Key.E);
            yield return Aim(Socket(1)); yield return Press(Key.E);
            Assert.That(State.Socket(1)?.Id, Is.EqualTo(id));
            yield return Aim(inventory.TrayDisplays[0].transform.position); yield return Press(Key.E);
            yield return Aim(Socket(0)); yield return Press(Key.E);
            Assert.That(State.Held.Ingredient.Id, Is.EqualTo("flour")); Assert.That(State.Socket(0), Is.Null); yield return Cancel();
            yield return Aim(Socket(1)); yield return Press(Key.E);
            var trash = Object.FindObjectsByType<InventoryInteractable>(FindObjectsSortMode.None).Single(t => t.StationId == "A1" && t.Kind == InventoryTargetKind.Trash);
            yield return Aim(trash.transform.position); yield return Press(Key.E);
            Assert.That(State.Portions.Count(p => p.Location == PortionLocation.Trash), Is.EqualTo(1));
            yield return Aim(inventory.TrayDisplays[0].transform.position); yield return Press(Key.E);
            Teleport(new Vector3(0, .05f, 10));
            var returns = Object.FindObjectsByType<InventoryInteractable>(FindObjectsSortMode.None).Single(t => t.Kind == InventoryTargetKind.PantryReturn);
            yield return Aim(returns.transform.position); yield return Press(Key.E);
            Assert.That(State.Portions.Count(p => p.Location == PortionLocation.Returned), Is.EqualTo(1)); Assert.That(State.Held, Is.Null);
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
