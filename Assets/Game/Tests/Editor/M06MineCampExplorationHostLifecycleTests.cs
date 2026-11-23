using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class M06MineCampExplorationHostLifecycleTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        private static void SeedVerifiedM05Arrival(Dictionary<string, int> values)
        {
            Func<string, int> read = Reader(values);
            Action<string, int> write = (key, value) => values[key] = value;

            var manifest = new M04DepartureManifest(
                M03VoyageChoice.QiQueMerchant,
                "Verified hull", "A Yuan", 4, "Shallow tender",
                true, 16, 8, "Named receiver", false, false);
            Assert.That(manifest.ConfirmDeparture(), Is.True);
            Assert.That(M04DepartureFacts.TryRecord(manifest, write), Is.True);
            Assert.That(M04DepartureFacts.TryMarkSailed(read, write), Is.True);

            var fog = new FogListeningSession(
                new[] { new FogListeningCue(FogBearing.Ahead, FogCueKind.ClearWater, 80) },
                maxListenFocus: 1, maxHullIntegrity: 2);
            fog.ApplyHelmAction(FogHelmAction.HoldCourse);
            Assert.That(FogListeningFacts.TryRecord(fog, write), Is.True);

            Assert.That(M05LandingFacts.TryRecord(
                new M05LandingReport(3, 10, true, true), read, write), Is.True);
            Assert.That(M05LettersFacts.TryRecord(
                M05LetterDecision.TakePacket, read, write), Is.True);
        }

        [Test]
        public void ExternalParentTeardownFailsClosedAcrossMovementInteractionCameraAndHud()
        {
            var root = new GameObject("M06 external parent teardown");
            var values = new Dictionary<string, int>();
            SeedVerifiedM05Arrival(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            M06MineCampExplorationHost host = null;
            try
            {
                host = new M06MineCampExplorationHost(root.transform, read, write);
                Assert.That(host.TryPlaceAtWalkableTile(new Vector2Int(42, 12)), Is.True);
                Assert.That(host.FindNearbyInteractableTriggerId(), Is.EqualTo("m06.recon.escort-schedule"));

                UnityEngine.Object.DestroyImmediate(root);
                root = null;

                Assert.That(host.TryPlaceAtWalkableTile(new Vector2Int(54, 36)), Is.False);
                Assert.That(host.StepMovement(Vector2.right, 0.1f), Is.False);
                Vector3 cam = new Vector3(12f, 12f, -10f);
                Assert.That(host.StepCamera(cam, 0.1f), Is.EqualTo(cam));
                Assert.That(host.FindNearbyInteractableTriggerId(), Is.Null);
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

        [Test]
        public void ExternalMinimapHeroAndBannerTeardownDoNotThrowDuringMovementOrRefresh()
        {
            var root = new GameObject("M06 partial child teardown");
            var values = new Dictionary<string, int>();
            SeedVerifiedM05Arrival(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            M06MineCampExplorationHost host = null;
            try
            {
                host = new M06MineCampExplorationHost(root.transform, read, write);
                host.MinimapHud.Dispose();
                host.HeroVisual.Dispose();
                host.ShenLiVisual.Dispose();

                Assert.DoesNotThrow(() => host.RefreshHud());
                Assert.That(host.TryPlaceAtWalkableTile(new Vector2Int(30, 12)), Is.True);
                Assert.That(host.StepMovement(Vector2.right, 0.1f), Is.True);

                UnityEngine.Object.DestroyImmediate(host.PromptBanner.gameObject);
                Assert.DoesNotThrow(() => host.RefreshHud());
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
