using System.Collections;
using System.IO;
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

        [UnitySetUp]
        public IEnumerator LoadPrototype()
        {
            // Batch Editor не имеет сфокусированного Game View. Только тестовая
            // копия настроек принимает синтетические устройства независимо от фокуса.
            savedInputSettings = InputSystem.settings;
            testInputSettings = Object.Instantiate(savedInputSettings);
            testInputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            testInputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            InputSystem.settings = testInputSettings;
            yield return SceneManager.LoadSceneAsync("ChefShow_Prototype", LoadSceneMode.Single);
            yield return null;
            bootstrap = Object.FindFirstObjectByType<GameBootstrap>();
            Assert.That(bootstrap, Is.Not.Null);
            Assert.That(bootstrap.Run, Is.Not.Null);
            bootstrap.SetPaused(false);
        }

        [UnityTearDown]
        public IEnumerator RestoreInputSettings()
        {
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
                    File.WriteAllBytes("TestResults/stage1-preview.png", image.EncodeToPNG());
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
            GameObject blocker = null;
            try
            {
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
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
                yield return null;
                yield return null;
                Assert.That(bootstrap.Player.Focused, Is.True);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Right));
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
    }
}
