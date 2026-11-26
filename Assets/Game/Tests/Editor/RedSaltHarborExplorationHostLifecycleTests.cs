using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RedSaltHarborExplorationHostLifecycleTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        private static void SeedCompletedCrossing(Dictionary<string, int> values)
        {
            Action<string, int> write = (key, value) => values[key] = value;
            Func<string, int> read = Reader(values);
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
        }

        private static void WalkTo(RedSaltHarborExplorationHost host, float x, float y)
        {
            Vector2 target = new Vector2(x, y);
            for (int axis = 0; axis < 2; axis++)
            {
                for (int i = 0; i < 900; i++)
                {
                    float delta = axis == 0
                        ? target.x - host.PlayerPosition.x
                        : target.y - host.PlayerPosition.y;
                    if (Mathf.Abs(delta) <= 0.25f) break;
                    Vector2 input = axis == 0
                        ? new Vector2(Mathf.Sign(delta), 0f)
                        : new Vector2(0f, Mathf.Sign(delta));
                    host.StepExploration(input, 0.03f);
                }
            }
            Assert.That(Vector2.Distance(host.PlayerPosition, target), Is.LessThan(0.5f));
        }

        [Test]
        public void ExternallyDisposedLandingSeranAndHarborWalkViewsUnblockMovementAndReopen()
        {
            var root = new GameObject("RedSalt external view teardown");
            var values = new Dictionary<string, int>();
            SeedCompletedCrossing(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (key, value) => values[key] = value;
            var landing = new M05LandingReport(3, 10, true, true);
            RedSaltHarborExplorationHost host = null;
            try
            {
                host = new RedSaltHarborExplorationHost(root.transform, read, write, landing);

                // 1. Landing check view externally disposed unblocks movement and reopens.
                WalkTo(host, 22.5f, 24.5f);
                Assert.That(host.TryInteractAtCurrentPosition(), Is.True);
                M05LandingCheckView firstLanding = host.ActiveLandingView;
                Assert.That(firstLanding, Is.Not.Null);
                firstLanding.Dispose();

                Assert.That(host.ActiveLandingView, Is.Null);
                Assert.That(host.IsDialogueOpen, Is.False);
                Assert.That(host.TryInteractAtCurrentPosition(), Is.True);
                Assert.That(host.ActiveLandingView, Is.Not.Null);
                Assert.That(host.ActiveLandingView, Is.Not.SameAs(firstLanding));
                host.ActiveLandingView.ConfirmButton.onClick.Invoke();
                Assert.That(host.IsDialogueOpen, Is.False);

                // 2. Seran letters view externally disposed unblocks and reopens.
                WalkTo(host, 50.5f, 26.5f);
                Assert.That(host.TryInteractAtCurrentPosition(), Is.True);
                M05SeranLettersView firstSeran = host.ActiveSeranView;
                Assert.That(firstSeran, Is.Not.Null);
                firstSeran.Dispose();

                Assert.That(host.ActiveSeranView, Is.Null);
                Assert.That(host.IsDialogueOpen, Is.False);
                Assert.That(host.TryInteractAtCurrentPosition(), Is.True);
                Assert.That(host.ActiveSeranView, Is.Not.Null);
                Assert.That(host.ActiveSeranView, Is.Not.SameAs(firstSeran));
                host.ActiveSeranView.TakeButton.onClick.Invoke();
                host.ActiveSeranView.ContinueButton.onClick.Invoke();
                Assert.That(host.IsDialogueOpen, Is.False);

                // 3. Harbor walk view externally disposed unblocks and reopens.
                WalkTo(host, 84.5f, 30.5f);
                Assert.That(host.TryInteractAtCurrentPosition(), Is.True);
                M05HarborWalkView firstWalk = host.ActiveHarborWalkView;
                Assert.That(firstWalk, Is.Not.Null);
                firstWalk.Dispose();

                Assert.That(host.ActiveHarborWalkView, Is.Null);
                Assert.That(host.IsDialogueOpen, Is.False);
                Assert.That(host.TryInteractAtCurrentPosition(), Is.True);
                Assert.That(host.ActiveHarborWalkView, Is.Not.Null);
                Assert.That(host.ActiveHarborWalkView, Is.Not.SameAs(firstWalk));
                host.ActiveHarborWalkView.AdvanceButton.onClick.Invoke();
                Assert.That(host.IsDialogueOpen, Is.False);
            }
            finally
            {
                host?.Dispose();
                if (root != null)
                    UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void NonFiniteInputsAndDestroyedParentFailClosedSafely()
        {
            var root = new GameObject("RedSalt finite and parent teardown");
            var values = new Dictionary<string, int>();
            SeedCompletedCrossing(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (key, value) => values[key] = value;
            var landing = new M05LandingReport(3, 10, true, true);
            RedSaltHarborExplorationHost host = null;
            try
            {
                host = new RedSaltHarborExplorationHost(root.transform, read, write, landing);
                Vector2 initialPos = host.PlayerPosition;

                Assert.That(host.StepExploration(Vector2.right, float.NaN), Is.False);
                Assert.That(host.StepExploration(Vector2.right, float.PositiveInfinity), Is.False);
                Assert.That(host.StepExploration(Vector2.right, -0.1f), Is.False);
                Assert.That(host.StepExploration(new Vector2(float.NaN, 0f), 0.1f), Is.False);
                Assert.That(host.StepExploration(new Vector2(0f, float.NegativeInfinity), 0.1f), Is.False);
                Assert.That(host.PlayerPosition, Is.EqualTo(initialPos));

                Vector3 camPos = new Vector3(16f, 24f, -10f);
                Assert.That(host.StepCamera(camPos, float.NaN), Is.EqualTo(camPos));
                Assert.That(host.StepCamera(camPos, -0.1f), Is.EqualTo(camPos));
                Vector3 nanCam = new Vector3(float.NaN, 24f, -10f);
                Assert.That(float.IsNaN(host.StepCamera(nanCam, 0.1f).x), Is.True);

                // Walk onto landing trigger, then destroy parent externally.
                WalkTo(host, 22.5f, 24.5f);
                UnityEngine.Object.DestroyImmediate(root);
                root = null;

                Assert.That(host.StepExploration(Vector2.right, 0.1f), Is.False);
                Assert.That(host.TryInteractAtCurrentPosition(), Is.False);
                Assert.That(host.IsDialogueOpen, Is.False);
                Assert.DoesNotThrow(() => host.RefreshHud());
                Assert.DoesNotThrow(() => host.Dispose());
                Assert.That(host.IsDisposed, Is.True);
            }
            finally
            {
                host?.Dispose();
                if (root != null)
                    UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
