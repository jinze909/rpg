using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class M05OuterHarborArrivalSessionLifecycleTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values) =>
            key => values.TryGetValue(key, out int value) ? value : 0;

        private static void SeedVerifiedLanding(Dictionary<string, int> values)
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
            Assert.That(M05LandingFacts.TryRecord(
                new M05LandingReport(3, 8, true, true), read, write), Is.True);
        }

        [Test]
        public void ExternalSeranViewDisposalClearsSeranViewPropertyAndSurvivesLayout()
        {
            var root = new GameObject("M05 arrival external Seran dispose");
            var values = new Dictionary<string, int>();
            SeedVerifiedLanding(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (key, value) => values[key] = value;
            M05OuterHarborArrivalSession session = null;
            try
            {
                Assert.That(M05OuterHarborArrivalSession.TryOpen(
                    root.transform, read, write, _ => { }, out session), Is.True);
                M05SeranLettersView seran = session.SeranView;
                Assert.That(seran, Is.Not.Null);

                seran.Dispose();
                Assert.That(seran.IsDisposed, Is.True);
                Assert.That(session.SeranView, Is.Null);
                Assert.DoesNotThrow(() => session.LayoutForViewportAspect(16f / 9f));
            }
            finally
            {
                session?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ExternalHarborWalkCanvasDestructionClearsHarborWalkViewProperty()
        {
            var root = new GameObject("M05 arrival external walk canvas destroy");
            var values = new Dictionary<string, int>();
            SeedVerifiedLanding(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (key, value) => values[key] = value;
            Assert.That(M05LettersFacts.TryRecord(
                M05LetterDecision.TakePacket, read, write), Is.True);
            M05OuterHarborArrivalSession session = null;
            try
            {
                Assert.That(M05OuterHarborArrivalSession.TryOpen(
                    root.transform, read, write, _ => { }, out session), Is.True);
                M05HarborWalkView walk = session.HarborWalkView;
                Assert.That(walk, Is.Not.Null);

                UnityEngine.Object.DestroyImmediate(walk.Panel.parent.gameObject);
                Assert.That(walk.IsDisposed, Is.True);
                Assert.That(session.HarborWalkView, Is.Null);
                Assert.DoesNotThrow(() => session.LayoutForViewportAspect(16f / 9f));
            }
            finally
            {
                session?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DestroyedParentImmediatelyReportsIsDisposed()
        {
            var root = new GameObject("M05 arrival destroyed parent");
            var values = new Dictionary<string, int>();
            SeedVerifiedLanding(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (key, value) => values[key] = value;
            M05OuterHarborArrivalSession session = null;
            try
            {
                Assert.That(M05OuterHarborArrivalSession.TryOpen(
                    root.transform, read, write, _ => { }, out session), Is.True);
                Assert.That(session.IsDisposed, Is.False);

                UnityEngine.Object.DestroyImmediate(root);
                root = null;
                Assert.That(session.IsDisposed, Is.True);
            }
            finally
            {
                session?.Dispose();
                if (root != null)
                    UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void NonFiniteAndNonPositiveAspectDoesNotOverwriteLastValidAspect()
        {
            var root = new GameObject("M05 arrival invalid aspect");
            var values = new Dictionary<string, int>();
            SeedVerifiedLanding(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (key, value) => values[key] = value;
            M05OuterHarborArrivalSession session = null;
            try
            {
                Assert.That(M05OuterHarborArrivalSession.TryOpen(
                    root.transform, read, write, _ => { }, out session), Is.True);
                session.LayoutForViewportAspect(9f / 16f);
                foreach (float bad in new[]
                    { float.NaN, float.PositiveInfinity, float.NegativeInfinity, 0f, -2f })
                {
                    Assert.DoesNotThrow(() => session.LayoutForViewportAspect(bad));
                }

                session.SeranView.TakeButton.onClick.Invoke();
                session.SeranView.ContinueButton.onClick.Invoke();
                Assert.That(session.HarborWalkView, Is.Not.Null);

                Vector3 scaleOnOpen = session.HarborWalkView.Panel.localScale;
                session.HarborWalkView.LayoutForViewportAspect(9f / 16f);
                Assert.That(scaleOnOpen, Is.EqualTo(session.HarborWalkView.Panel.localScale));
            }
            finally
            {
                session?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
