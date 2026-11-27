using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class ST06TaoJiHomecomingHostLifecycleTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        private static void SeedM07TaoJiRescued(Dictionary<string, int> values)
        {
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            var manifest = new M04DepartureManifest(
                M03VoyageChoice.QiQueMerchant,
                "Confirmed hull", "A Yuan", 4, "Shallow tender",
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
            Assert.That(M06RescuePlanFacts.TryRecord(
                new M06ReunionRecord(
                    M06RescuePlan.OfficialHandover, true,
                    M06LetterDeliveryOutcome.NotCarried, true), read, write), Is.True);
            Assert.That(M07ExtractionFacts.TryRecord(new M07ExtractionRecord(
                M07ExecutedRoute.OfficialHandover,
                M07PumpShutdownState.KeptRunning,
                M07RouteCostFlags.HarborSupplyDebt,
                boardedFirstBatchCount: 300,
                lowerLevelEvacuatedCount: 0,
                M07RusFate.Arrested, M07LedgerState.Preserved,
                M07LiangChoice.StayOnIsland,
                M07YidaChoice.HandToInspector,
                taoJiRescued: true), read, write), Is.True);
        }

        [Test]
        public void ExternalViewDisposalClearsActiveViewAndAllowsCleanReopen()
        {
            var root = new GameObject("ST06 lifecycle external dispose");
            var values = new Dictionary<string, int>();
            SeedM07TaoJiRescued(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            ST06TaoJiHomecomingHost host = null;
            try
            {
                host = new ST06TaoJiHomecomingHost(root.transform, () => true, read, write);
                Assert.That(host.TryOpen(), Is.True);
                var firstView = host.ActiveView;
                Assert.That(firstView, Is.Not.Null);
                Assert.That(firstView.IsDisposed, Is.False);

                firstView.Dispose();
                Assert.That(firstView.IsDisposed, Is.True);
                Assert.That(host.ActiveView, Is.Null);

                Assert.That(host.TryOpen(), Is.True);
                Assert.That(host.ActiveView, Is.Not.Null);
                Assert.That(host.ActiveView, Is.Not.SameAs(firstView));
                Assert.That(host.ActiveView.IsDisposed, Is.False);
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DestroyedCanvasMarksViewDisposedAndAllowsHostReopen()
        {
            var root = new GameObject("ST06 lifecycle destroyed canvas");
            var values = new Dictionary<string, int>();
            SeedM07TaoJiRescued(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            ST06TaoJiHomecomingHost host = null;
            try
            {
                host = new ST06TaoJiHomecomingHost(root.transform, () => true, read, write);
                Assert.That(host.TryOpen(), Is.True);
                var firstView = host.ActiveView;
                Assert.That(firstView, Is.Not.Null);

                UnityEngine.Object.DestroyImmediate(firstView.Panel.parent.gameObject);
                Assert.That(firstView.IsDisposed, Is.True);
                Assert.That(host.ActiveView, Is.Null);

                Assert.That(host.TryOpen(), Is.True);
                Assert.That(host.ActiveView, Is.Not.Null);
                Assert.That(host.ActiveView, Is.Not.SameAs(firstView));
                Assert.That(host.ActiveView.IsDisposed, Is.False);
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void OpeningCallbackDisposesHostWithoutPublishingSessionOrCanvas(bool duringFactRead)
        {
            var root = new GameObject("ST06 retired during opening");
            var values = new Dictionary<string, int>();
            SeedM07TaoJiRescued(values);
            Func<string, int> read = Reader(values);
            ST06TaoJiHomecomingHost host = null;
            try
            {
                host = new ST06TaoJiHomecomingHost(root.transform, () =>
                {
                    if (!duringFactRead) host.Dispose();
                    return true;
                }, key =>
                {
                    if (duringFactRead && key == M05LandingFacts.ArrivalSequenceKey)
                        host.Dispose();
                    return read(key);
                }, (_, __) => Assert.Fail("Opening must not write facts"));

                Assert.That(host.TryOpen(), Is.False);
                Assert.That(host.IsDisposed, Is.True);
                Assert.That(host.Session, Is.Null);
                Assert.That(host.ActiveView, Is.Null);
                Assert.That(root.transform.childCount, Is.Zero);
                Assert.That(host.TryOpen(), Is.False);
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void NestedOpenFromWoodshopCallbackCannotPublishSecondCanvas()
        {
            var root = new GameObject("ST06 nested opening");
            var values = new Dictionary<string, int>();
            SeedM07TaoJiRescued(values);
            ST06TaoJiHomecomingHost host = null;
            bool nestedAttempted = false;
            bool nestedOpened = false;
            try
            {
                host = new ST06TaoJiHomecomingHost(root.transform, () =>
                {
                    if (!nestedAttempted)
                    {
                        nestedAttempted = true;
                        nestedOpened = host.TryOpen();
                    }
                    return true;
                }, Reader(values), (_, __) => Assert.Fail("Opening must not write facts"));

                Assert.That(host.TryOpen(), Is.True);
                Assert.That(nestedAttempted, Is.True);
                Assert.That(nestedOpened, Is.False);
                Assert.That(host.ActiveView, Is.Not.Null);
                Assert.That(root.transform.childCount, Is.EqualTo(1));

                host.ActiveView.Dispose();
                Assert.That(host.TryOpen(), Is.True);
                Assert.That(root.transform.childCount, Is.EqualTo(1));
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DestroyedParentFailsClosedWithoutThrowingOnTryOpen()
        {
            var root = new GameObject("ST06 lifecycle destroyed parent");
            var values = new Dictionary<string, int>();
            SeedM07TaoJiRescued(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            ST06TaoJiHomecomingHost host = null;
            try
            {
                host = new ST06TaoJiHomecomingHost(root.transform, () => true, read, write);
                UnityEngine.Object.DestroyImmediate(root);
                root = null;

                bool opened = false;
                Assert.DoesNotThrow(() => opened = host.TryOpen());
                Assert.That(opened, Is.False);
                Assert.That(host.ActiveView, Is.Null);
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
