using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class M05OuterHarborFlowHostLifecycleTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values) =>
            key => values.TryGetValue(key, out int value) ? value : 0;

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

        [Test]
        public void ExternalLandingCheckViewDisposalClearsPropertyAndSurvivesLayout()
        {
            var root = new GameObject("M05 flow external landing view dispose");
            var values = new Dictionary<string, int>();
            SeedCompletedCrossing(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (key, value) => values[key] = value;
            M05OuterHarborFlowHost host = null;
            try
            {
                Assert.That(M05OuterHarborFlowHost.TryOpen(
                    root.transform, new M05LandingReport(3, 8, true, true),
                    read, write, _ => { }, out host), Is.True);
                M05LandingCheckView view = host.LandingCheckView;
                Assert.That(view, Is.Not.Null);

                view.Dispose();
                Assert.That(view.IsDisposed, Is.True);
                Assert.That(host.LandingCheckView, Is.Null);
                Assert.DoesNotThrow(() => host.LayoutForViewportAspect(16f / 9f));
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ExternalArrivalSessionDisposalClearsPropertyAndSurvivesLayout()
        {
            var root = new GameObject("M05 flow external session dispose");
            var values = new Dictionary<string, int>();
            SeedCompletedCrossing(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (key, value) => values[key] = value;
            Assert.That(M05LandingFacts.TryRecord(
                new M05LandingReport(3, 8, true, true), read, write), Is.True);
            M05OuterHarborFlowHost host = null;
            try
            {
                Assert.That(M05OuterHarborFlowHost.TryOpen(
                    root.transform, observedLanding: null,
                    read, write, _ => { }, out host), Is.True);
                M05OuterHarborArrivalSession session = host.ArrivalSession;
                Assert.That(session, Is.Not.Null);

                session.Dispose();
                Assert.That(session.IsDisposed, Is.True);
                Assert.That(host.ArrivalSession, Is.Null);
                Assert.DoesNotThrow(() => host.LayoutForViewportAspect(16f / 9f));
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DestroyedParentImmediatelyReportsIsDisposed()
        {
            var root = new GameObject("M05 flow destroyed parent");
            var values = new Dictionary<string, int>();
            SeedCompletedCrossing(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (key, value) => values[key] = value;
            M05OuterHarborFlowHost host = null;
            try
            {
                Assert.That(M05OuterHarborFlowHost.TryOpen(
                    root.transform, new M05LandingReport(3, 8, true, true),
                    read, write, _ => { }, out host), Is.True);
                Assert.That(host.IsDisposed, Is.False);

                UnityEngine.Object.DestroyImmediate(root);
                root = null;
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
        public void NonFiniteAndNonPositiveAspectDoesNotOverwriteLastValidAspect()
        {
            var root = new GameObject("M05 flow invalid aspect");
            var values = new Dictionary<string, int>();
            SeedCompletedCrossing(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (key, value) => values[key] = value;
            M05OuterHarborFlowHost host = null;
            try
            {
                Assert.That(M05OuterHarborFlowHost.TryOpen(
                    root.transform, new M05LandingReport(3, 8, true, true),
                    read, write, _ => { }, out host), Is.True);
                host.LayoutForViewportAspect(9f / 16f);
                foreach (float bad in new[]
                    { float.NaN, float.PositiveInfinity, float.NegativeInfinity, 0f, -2f })
                {
                    Assert.DoesNotThrow(() => host.LayoutForViewportAspect(bad));
                }

                host.LandingCheckView.ConfirmButton.onClick.Invoke();
                Assert.That(host.ArrivalSession, Is.Not.Null);
                Assert.That(host.ArrivalSession.SeranView, Is.Not.Null);

                Vector3 scaleOnOpen = host.ArrivalSession.SeranView.Panel.localScale;
                host.ArrivalSession.SeranView.LayoutForViewportAspect(9f / 16f);
                Assert.That(scaleOnOpen, Is.EqualTo(host.ArrivalSession.SeranView.Panel.localScale));
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
