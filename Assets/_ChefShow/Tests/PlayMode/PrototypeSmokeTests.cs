using System.Collections;
using System.IO;
using System.Linq;
using ChefShow.Contestants;
using ChefShow.Core;
using ChefShow.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

namespace ChefShow.Tests
{
    public sealed class PrototypeSmokeTests
    {
        private GameBootstrap bootstrap;
        private InputSettings savedInputSettings;
        private InputSettings testInputSettings;
        private Keyboard fixtureKeyboard;
        private Mouse fixtureMouse;

        [UnitySetUp]
        public IEnumerator LoadPrototype()
        {
            // Batch Editor не имеет сфокусированного Game View. Только тестовая
            // копия настроек принимает синтетические устройства независимо от фокуса.
            savedInputSettings = InputSystem.settings;
            testInputSettings = Object.Instantiate(savedInputSettings);
            testInputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            testInputSettings.updateMode = InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;
#if UNITY_EDITOR
            testInputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            InputSystem.settings = testInputSettings;
            fixtureKeyboard = InputSystem.AddDevice<Keyboard>(); fixtureMouse = InputSystem.AddDevice<Mouse>();
            yield return SceneManager.LoadSceneAsync("ChefShow_Prototype", LoadSceneMode.Single);
            yield return null;
            bootstrap = Object.FindFirstObjectByType<GameBootstrap>();
            Assert.That(bootstrap, Is.Not.Null);
            Assert.That(bootstrap.Run, Is.Not.Null);
            bootstrap.UiInput.actionsAsset.devices = new InputDevice[] { fixtureKeyboard, fixtureMouse };
            bootstrap.AutoPauseOnFocusLoss = false;
            bootstrap.SetPaused(false);
        }

        [UnityTearDown]
        public IEnumerator RestoreInputSettings()
        {
            InputSystem.RemoveDevice(fixtureKeyboard); InputSystem.RemoveDevice(fixtureMouse);
            InputSystem.settings = savedInputSettings;
            Object.Destroy(testInputSettings);
            yield return null;
        }

        [UnityTest]
        public IEnumerator PauseResumeAndRestartPreserveDefinitions()
        {
            float sourceDuration = bootstrap.Config.RoundDurationSeconds;
            var sourceInput = bootstrap.InputDefinition;
            var first = bootstrap.Run;
            bootstrap.SetPaused(true);
            float remaining = first.RemainingSeconds;
            yield return new WaitForSecondsRealtime(0.1f);
            Assert.That(first.RemainingSeconds, Is.EqualTo(remaining));
            bootstrap.SetPaused(false);
            yield return null;
            yield return null;
            Assert.That(first.RemainingSeconds, Is.LessThan(remaining));
            bootstrap.RestartShow();
            Assert.That(first.Disposed, Is.True);
            Assert.That(bootstrap.Run.RunId, Is.Not.EqualTo(first.RunId));
            Assert.That(bootstrap.Run.RemainingSeconds, Is.EqualTo(sourceDuration));
            Assert.That(bootstrap.Config.RoundDurationSeconds, Is.EqualTo(sourceDuration));
            Assert.That(bootstrap.InputDefinition, Is.SameAs(sourceInput));
            Assert.That(sourceInput.enabled, Is.False);
            if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null)
            {
                Canvas.ForceUpdateCanvases();
                var target = new RenderTexture(1280, 720, 24);
                target.Create();
                var previous = RenderTexture.active;
                Texture2D image = null;
                try
                {
                    RenderPipeline.SubmitRenderRequest(bootstrap.Player.ViewCamera,
                        new RenderPipeline.StandardRequest { destination = target });
                    RenderTexture.active = target;
                    image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
                    image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                    image.Apply();
                    Directory.CreateDirectory("TestResults");
                    File.WriteAllBytes("TestResults/layout-first-person.png", image.EncodeToPNG());
                }
                finally
                {
                    RenderTexture.active = previous;
                    if (image != null) Object.Destroy(image);
                    target.Release();
                    Object.Destroy(target);
                }
            }
        }

        [UnityTest]
        public IEnumerator RealInputMovesPlayerAndEscapeControlsPause()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var mouse = InputSystem.AddDevice<Mouse>();
            bootstrap.UiInput.actionsAsset.devices = new InputDevice[] { keyboard, mouse };
            try
            {
                var initial = bootstrap.Player.transform.position;
                // Игрок начинает перед столешницей: проверяем движение от неё,
                // чтобы не измерять корректное столкновение CharacterController со столом.
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.S));
                yield return new WaitForSecondsRealtime(0.15f);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null;
                Assert.That(Vector3.Distance(initial, bootstrap.Player.transform.position), Is.GreaterThan(0.1f));
                var rotation = bootstrap.Player.transform.rotation;
                InputSystem.QueueStateEvent(mouse, new MouseState { delta = new Vector2(40, 0) });
                yield return null;
                yield return null;
                Assert.That(Quaternion.Angle(rotation, bootstrap.Player.transform.rotation), Is.GreaterThan(0.1f));
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
                yield return null;
                yield return null;
                Assert.That(bootstrap.IsPaused, Is.True);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Enter));
                yield return null;
                yield return null;
                Assert.That(bootstrap.IsPaused, Is.False);
            }
            finally { InputSystem.RemoveDevice(keyboard); InputSystem.RemoveDevice(mouse); }
        }

        [UnityTest]
        public IEnumerator InteractionIsOccludedAndStationFocusReturns()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var mouse = InputSystem.AddDevice<Mouse>();
            bootstrap.UiInput.actionsAsset.devices = new InputDevice[] { keyboard, mouse };
            GameObject blocker = null;
            try
            {
                var station=GameObject.Find("Station_"+bootstrap.Inventory.PlayerStationId).GetComponent<PrototypeInteractable>();
                var aim=Quaternion.LookRotation(station.FocusPoint.position-bootstrap.Player.ViewCamera.transform.position).eulerAngles;
                var current=bootstrap.Player.ViewCamera.transform.eulerAngles;
                InputSystem.QueueDeltaStateEvent(mouse.delta,new Vector2(Mathf.DeltaAngle(current.y,aim.y),-Mathf.DeltaAngle(current.x,aim.x))/bootstrap.Config.LookSensitivity);
                yield return null;yield return null;
                bootstrap.Player.RefreshTarget();
                Assert.That(bootstrap.Player.Target, Is.Not.Null);
                Assert.That(bootstrap.Player.Target.IsPlayerStation, Is.True);
                var camera = bootstrap.Player.ViewCamera;
                blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
                blocker.transform.position = camera.transform.position + camera.transform.forward * 0.3f;
                blocker.transform.localScale = Vector3.one * 0.15f;
                Physics.SyncTransforms();
                bootstrap.Player.RefreshTarget();
                Assert.That(bootstrap.Player.Target, Is.Null);
                Object.Destroy(blocker);
                yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F));
                yield return null;
                yield return null;
                Assert.That(bootstrap.Player.Focused, Is.True);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F));
                yield return null;
                yield return null;
                Assert.That(bootstrap.Player.Focused, Is.False);
            }
            finally
            {
                if (blocker != null) Object.Destroy(blocker);
                InputSystem.RemoveDevice(keyboard);
                InputSystem.RemoveDevice(mouse);
            }
        }

        [UnityTest]
        public IEnumerator StationRowsBlockGapsAndAllowBothEndRoutes()
        {
            var player = bootstrap.Player.transform;
            var controller = player.GetComponent<CharacterController>();
            var actors = Object.FindObjectsByType<PrototypeActor>(FindObjectsSortMode.None);
            var floor = GameObject.Find("Floor").GetComponent<BoxCollider>().bounds;
            Assert.That(floor.size.x, Is.EqualTo(60).Within(0.01f));
            Assert.That(floor.size.z, Is.EqualTo(44).Within(0.01f));
            foreach (string name in new[] { "Pantry", "Judging Table" })
                Assert.That(GameObject.Find(name).GetComponent<BoxCollider>().bounds.max.y - floor.max.y,
                    Is.EqualTo(0.9f).Within(0.01f), name + " worktop height");
            foreach (var actor in actors.Where(a => a.Kind != PrototypeActorKind.Chef))
            {
                var table = GameObject.Find("Station_" + actor.StableId).GetComponent<BoxCollider>().bounds;
                Assert.That(table.size.x, Is.EqualTo(bootstrap.Cooking == null ? 2.3f : bootstrap.Cooking.Config.TableDepth).Within(0.01f));
                Assert.That(table.size.z, Is.EqualTo(bootstrap.Cooking == null ? 2.3f : bootstrap.Cooking.Config.TableWidth).Within(0.01f));
                Assert.That(table.max.y - floor.max.y, Is.EqualTo(0.9f).Within(0.01f), actor.StableId + " worktop height");
                Assert.That(table.min.y, Is.EqualTo(floor.max.y).Within(0.01f));
                Assert.That(Mathf.Abs(actor.transform.position.x), Is.GreaterThan(Mathf.Abs(table.center.x) + table.extents.x));
                Assert.That(Vector3.Dot(actor.transform.forward, (table.center - actor.transform.position).normalized), Is.GreaterThan(0.9f), actor.StableId + " faces own table");
                if (actor.Kind == PrototypeActorKind.Npc) Assert.That(actor.GetComponent<Collider>().enabled, Is.True);
                if(bootstrap.Cooking!=null)
                {
                    var pan=bootstrap.Cooking.Stations.Single(s=>s.StationId==actor.StableId&&s.Kind==ChefShow.Cooking.CookerKind.Pan);
                    var pot=bootstrap.Cooking.Stations.Single(s=>s.StationId==actor.StableId&&s.Kind==ChefShow.Cooking.CookerKind.Pot);
                    Assert.That(Vector3.Dot(pan.transform.position-table.center,actor.transform.right),Is.GreaterThan(.8f),actor.StableId+" pan is on the right");
                    Assert.That(Vector3.Dot(pot.transform.position-table.center,actor.transform.right),Is.GreaterThan(.8f),actor.StableId+" pot is on the right");
                    Assert.That(Vector3.Dot(pot.transform.position-pan.transform.position,actor.transform.forward),Is.GreaterThan(.8f),actor.StableId+" pot is behind the pan");
                    foreach(var appliance in new[]{pan,pot})
                    {
                        var pad=appliance.GetComponent<BoxCollider>().bounds;
                        Assert.That(pad.min.x,Is.GreaterThan(table.min.x));Assert.That(pad.max.x,Is.LessThan(table.max.x));
                        Assert.That(pad.min.z,Is.GreaterThan(table.min.z));Assert.That(pad.max.z,Is.LessThan(table.max.z));
                    }
                }
            }
            bootstrap.SetPaused(true);
            foreach (string team in new[] { "A", "B" })
            {
                float sign = team == "A" ? -1 : 1;
                for (int i = 1; i < 6; i++)
                {
                    var first = GameObject.Find("Station_" + team + i).GetComponent<BoxCollider>().bounds;
                    var second = GameObject.Find("Station_" + team + (i + 1)).GetComponent<BoxCollider>().bounds;
                    Assert.That(second.center.z - first.center.z, Is.EqualTo(first.size.z+.2f).Within(0.01f));
                    Assert.That(second.min.z - first.max.z, Is.EqualTo(0.2f).Within(0.01f));
                    Teleport(new Vector3(sign * 10, 0.05f, (first.center.z + second.center.z) / 2));
                    WalkToward(new Vector3(sign * 4, 0.05f, player.position.z));
                    Assert.That(Mathf.Abs(player.position.x), Is.GreaterThan(9.2f), team + " gap " + i + " must block the capsule");
                }
                foreach (int end in new[] { -1, 1 })
                {
                    // Идём за спинами NPC, затем вокруг края 1/6, через центр к общей зоне.
                    var firstEnd=GameObject.Find("Station_"+team+"1").GetComponent<BoxCollider>().bounds;
                    var lastEnd=GameObject.Find("Station_"+team+"6").GetComponent<BoxCollider>().bounds;
                    float bypassZ=end<0?firstEnd.min.z-1.3f:lastEnd.max.z+1.3f;
                    Teleport(new Vector3(sign * 11.5f, 0.05f, end<0?lastEnd.center.z:firstEnd.center.z));
                    var targets = new[] {
                        new Vector3(sign * 11.5f, 0.05f, bypassZ),
                        new Vector3(sign * 6, 0.05f, bypassZ),
                        new Vector3(sign * 6, 0.05f, end == 1 ? 10.6f : -10.6f),
                        new Vector3(0, 0.05f, end == 1 ? 10.6f : -10.6f)
                    };
                    foreach (var target in targets)
                    {
                        WalkToward(target);
                        Assert.That(Vector2.Distance(new Vector2(player.position.x, player.position.z), new Vector2(target.x, target.z)),
                            Is.LessThan(0.12f), team + " end " + end + " route to " + target);
                    }
                }
            }
            if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null)
            {
                var camera = bootstrap.Player.ViewCamera;
                var canvas = bootstrap.Hud.GetComponentInParent<Canvas>();
                canvas.enabled = false;
                camera.transform.SetPositionAndRotation(new Vector3(0, 40, 0), Quaternion.Euler(90, 0, 0));
                camera.orthographic = true;
                camera.orthographicSize = 16;
                var target = new RenderTexture(1280, 720, 24);
                target.Create();
                var previous = RenderTexture.active;
                var preview = new Texture2D(1280, 720, TextureFormat.RGB24, false);
                try
                {
                    RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                    RenderTexture.active = target;
                    preview.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                    preview.Apply();
                    Directory.CreateDirectory("TestResults");
                    File.WriteAllBytes("TestResults/layout-overview.png", preview.EncodeToPNG());
                }
                finally
                {
                    RenderTexture.active = previous;
                    target.Release();
                    Object.Destroy(target);
                    Object.Destroy(preview);
                }
            }
            yield return null;

            void Teleport(Vector3 position)
            {
                controller.enabled = false;
                player.position = position;
                controller.enabled = true;
                Physics.SyncTransforms();
            }
            void WalkToward(Vector3 destination)
            {
                for (int step = 0; step < 1000; step++)
                {
                    var delta = destination - player.position;
                    delta.y = 0;
                    if (delta.magnitude < 0.04f) break;
                    controller.Move(Vector3.ClampMagnitude(delta, 0.05f) + Vector3.down * 0.02f);
                }
            }
        }
    }
}
