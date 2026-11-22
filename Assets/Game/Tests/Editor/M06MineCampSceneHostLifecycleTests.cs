using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class M06MineCampSceneHostLifecycleTests
    {
        private static void SeedVerifiedLanding(Dictionary<string, int> save)
        {
            Func<string, int> read = key =>
                save.TryGetValue(key, out int value) ? value : 0;
            Action<string, int> write = (key, value) => save[key] = value;

            var manifest = new M04DepartureManifest(M03VoyageChoice.QiQueMerchant,
                "Verified civilian hull", "A Yuan", 4, "Shallow tender",
                true, 16, 8, "Named receiver", false, false);
            Assert.That(manifest.ConfirmDeparture(), Is.True);
            Assert.That(M04DepartureFacts.TryRecord(manifest, write), Is.True);
            Assert.That(M04DepartureFacts.TryMarkSailed(read, write), Is.True);

            var fog = new FogListeningSession(
                new[] { new FogListeningCue(FogBearing.Ahead, FogCueKind.ClearWater, 80) },
                maxListenFocus: 1, maxHullIntegrity: 2);
            fog.ApplyHelmAction(FogHelmAction.HoldCourse);
            Assert.That(fog.IsComplete && fog.Succeeded, Is.True);
            Assert.That(FogListeningFacts.TryRecord(fog, write), Is.True);
            Assert.That(M05LandingFacts.TryRecord(
                new M05LandingReport(3, 8, true, true), read, write), Is.True);
        }

        private static GameObject CreateTestOrthographicCamera(out Camera camera)
        {
            var camObj = new GameObject("M06 scene host lifecycle camera", typeof(Camera));
            camera = camObj.GetComponent<Camera>();
            camera.orthographic = true;
            return camObj;
        }

        [Test]
        public void ExternalExplorationDisposalReportsIsDisposedAndExitsOnceOnTick()
        {
            var root = new GameObject("M06 scene host external exploration dispose");
            var camObj = CreateTestOrthographicCamera(out Camera camera);
            var save = new Dictionary<string, int>();
            SeedVerifiedLanding(save);
            Func<string, int> read = key => save.TryGetValue(key, out int v) ? v : 0;
            Action<string, int> write = (key, value) => save[key] = value;
            int exitCount = 0;
            M06MineCampSceneHost host = null;
            try
            {
                host = new M06MineCampSceneHost(
                    root.transform, camera, read, write, () => exitCount++);
                Assert.That(host.IsDisposed, Is.False);

                host.Exploration.Dispose();
                Assert.That(host.IsDisposed, Is.True);

                Assert.DoesNotThrow(() => host.Tick(0.016f));
                Assert.DoesNotThrow(() => host.Tick(0.016f));
                Assert.That(exitCount, Is.EqualTo(1));
                Assert.That(host.Exploration, Is.Null);
                Assert.That(host.CameraRig, Is.Null);
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(camObj);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DestroyedParentReportsIsDisposedAndExitsOnceOnTick()
        {
            var root = new GameObject("M06 scene host destroyed parent");
            var camObj = CreateTestOrthographicCamera(out Camera camera);
            var save = new Dictionary<string, int>();
            SeedVerifiedLanding(save);
            Func<string, int> read = key => save.TryGetValue(key, out int v) ? v : 0;
            Action<string, int> write = (key, value) => save[key] = value;
            int exitCount = 0;
            M06MineCampSceneHost host = null;
            try
            {
                host = new M06MineCampSceneHost(
                    root.transform, camera, read, write, () => exitCount++);
                Assert.That(host.IsDisposed, Is.False);

                UnityEngine.Object.DestroyImmediate(root);
                Assert.That(host.IsDisposed, Is.True);

                Assert.DoesNotThrow(() => host.Tick(0.016f));
                Assert.DoesNotThrow(() => host.Tick(0.016f));
                Assert.That(exitCount, Is.EqualTo(1));
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(camObj);
                if (root != null)
                    UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TickIgnoresNonFiniteAndNonPositiveDeltaSecondsWithoutPoisoningPosition()
        {
            var root = new GameObject("M06 scene host non-finite delta");
            var camObj = CreateTestOrthographicCamera(out Camera camera);
            var save = new Dictionary<string, int>();
            SeedVerifiedLanding(save);
            Func<string, int> read = key => save.TryGetValue(key, out int v) ? v : 0;
            Action<string, int> write = (key, value) => save[key] = value;
            int exitCount = 0;
            M06MineCampSceneHost host = null;
            try
            {
                host = new M06MineCampSceneHost(
                    root.transform, camera, read, write, () => exitCount++);
                Vector2 initialPosition = host.Exploration.PlayerPosition;

                foreach (float delta in new[]
                    { float.NaN, float.PositiveInfinity, float.NegativeInfinity, -1f, 0f })
                {
                    Assert.DoesNotThrow(() => host.Tick(delta));
                    Vector2 pos = host.Exploration.PlayerPosition;
                    Assert.That(!float.IsNaN(pos.x) && !float.IsInfinity(pos.x), Is.True);
                    Assert.That(!float.IsNaN(pos.y) && !float.IsInfinity(pos.y), Is.True);
                    Assert.That(pos, Is.EqualTo(initialPosition));
                }

                Assert.That(host.IsDisposed, Is.False);
                Assert.That(exitCount, Is.Zero);
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(camObj);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ExitAndDisposeAreIdempotentAndInvokeOnExitAtMostOnce()
        {
            var root = new GameObject("M06 scene host idempotent exit");
            var camObj = CreateTestOrthographicCamera(out Camera camera);
            var save = new Dictionary<string, int>();
            SeedVerifiedLanding(save);
            Func<string, int> read = key => save.TryGetValue(key, out int v) ? v : 0;
            Action<string, int> write = (key, value) => save[key] = value;
            int exitCount = 0;
            M06MineCampSceneHost host = null;
            try
            {
                host = new M06MineCampSceneHost(
                    root.transform, camera, read, write, () => exitCount++);
                host.Exit();
                host.Exit();
                host.Dispose();
                host.Dispose();

                Assert.That(exitCount, Is.EqualTo(1));
                Assert.That(host.IsDisposed, Is.True);
                Assert.That(host.Exploration, Is.Null);
                Assert.That(host.CameraRig, Is.Null);
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(camObj);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
