using System;
using System.Collections.Generic;
using System.IO;
using ChefShow.Core;
using ChefShow.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace ChefShow.Tests
{
    public sealed class FoundationTests
    {
        [Test]
        public void PauseAndSpeedAffectAllSimulationTime()
        {
            using (var run = new PrototypeRun(60, 5, null))
            {
                run.Tick(1);
                Assert.That(run.RemainingSeconds, Is.EqualTo(59));
                run.SetPaused(true);
                run.Tick(10);
                Assert.That(run.RemainingSeconds, Is.EqualTo(59));
                Assert.That(run.Clock.SimulationTime, Is.EqualTo(1));
                run.Clock.SetSpeed(2);
                run.SetPaused(false);
                run.Tick(1);
                Assert.That(run.RemainingSeconds, Is.EqualTo(57));
                Assert.That(run.Clock.SimulationTime, Is.EqualTo(3));
            }
        }

        [Test]
        public void NestedFactsAreFifoAndUnsubscriptionDoesNotBreakSnapshot()
        {
            var order = new List<string>();
            using (var bus = new GameEventBus(null))
            {
                IDisposable first = null;
                first = bus.Subscribe<int>(number =>
                {
                    order.Add("first:" + number);
                    first.Dispose();
                    bus.Publish("nested");
                });
                bus.Subscribe<int>(number => order.Add("second:" + number));
                bus.Subscribe<string>(value => order.Add(value));
                bus.Publish(1);
                bus.Publish(2);
            }
            CollectionAssert.AreEqual(new[] { "first:1", "second:1", "nested", "second:2" }, order);
        }

        [Test]
        public void HandlerFailureIsReportedAndOtherViewsStillReceiveFact()
        {
            int errors = 0;
            int received = 0;
            using (var bus = new GameEventBus((type, error) => errors++))
            {
                bus.Subscribe<int>(_ => throw new InvalidOperationException("test"));
                bus.Subscribe<int>(_ => received++);
                bus.Publish(1);
            }
            Assert.That(errors, Is.EqualTo(1));
            Assert.That(received, Is.EqualTo(1));
        }

        [Test]
        public void DisposedRunCannotDeliverOldFactsOrAdvance()
        {
            var run = new PrototypeRun(60, 5, null);
            int received = 0;
            var token = run.Events.Subscribe<int>(_ => received++);
            run.Dispose();
            run.Events.Publish(1);
            run.Tick(10);
            token.Dispose();
            token.Dispose();
            Assert.That(received, Is.Zero);
            Assert.That(run.RemainingSeconds, Is.EqualTo(60));
            Assert.Throws<ObjectDisposedException>(() => run.Events.Subscribe<int>(_ => { }));
        }

        [Test]
        public void RebuildPreservesEditableSceneAndExistingDefaultGuids()
        {
            byte[] editable = File.ReadAllBytes(PrototypeSceneBuilder.EditableScene);
            string configGuid = AssetDatabase.AssetPathToGUID(PrototypeSceneBuilder.ConfigPath);
            string inputGuid = AssetDatabase.AssetPathToGUID(PrototypeSceneBuilder.InputPath);
            EditorSceneManager.OpenScene(PrototypeSceneBuilder.GeneratedScene, OpenSceneMode.Single);
            PrototypeSceneBuilder.BuildPrototypeScene();
            PrototypeValidator.ValidateScene(SceneManager.GetActiveScene());
            CollectionAssert.AreEqual(editable, File.ReadAllBytes(PrototypeSceneBuilder.EditableScene));
            Assert.That(AssetDatabase.AssetPathToGUID(PrototypeSceneBuilder.ConfigPath), Is.EqualTo(configGuid));
            Assert.That(AssetDatabase.AssetPathToGUID(PrototypeSceneBuilder.InputPath), Is.EqualTo(inputGuid));
        }
    }
}
