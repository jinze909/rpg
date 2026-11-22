using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RC04DingYueReefHostLifecycleTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        private static void SeedM08RosterCasesAvailable(Dictionary<string, int> values)
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
                boardedFirstBatchCount: 10,
                lowerLevelEvacuatedCount: 0,
                M07RusFate.Arrested, M07LedgerState.Preserved,
                M07LiangChoice.StayOnIsland,
                M07YidaChoice.HandToInspector,
                taoJiRescued: false), read, write), Is.True);

            var winch = new M08FirstWinchOrderSession();
            Assert.That(winch.TryAcquirePart(M08WinchPart.ScrapGear), Is.True);
            Assert.That(winch.TryAcquirePart(M08WinchPart.DockCable), Is.True);
            Assert.That(winch.TryAssemble(), Is.True);
            Assert.That(winch.TryTightenCable(), Is.True);
            Assert.That(winch.TryTestLift(), Is.True);
            Assert.That(M08WorkshopDepositFacts.TryConfirm(winch, read, write), Is.True);
            Assert.That(M08FirstWorldNoticeFacts.TryRecord(true, read, write), Is.True);
        }

        [Test]
        public void ExternalListeningViewDisposalClearsActiveViewAndAllowsCleanReopen()
        {
            var root = new GameObject("RC04 lifecycle external dispose");
            var values = new Dictionary<string, int>();
            SeedM08RosterCasesAvailable(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            RC04DingYueReefHost host = null;
            try
            {
                host = new RC04DingYueReefHost(root.transform, () => true, _ => true, read, write);
                Assert.That(host.TryOpenListening(), Is.True);
                var firstView = host.ActiveListeningView;
                Assert.That(firstView, Is.Not.Null);
                Assert.That(firstView.IsDisposed, Is.False);

                firstView.Dispose();
                Assert.That(firstView.IsDisposed, Is.True);
                Assert.That(host.ActiveListeningView, Is.Null);

                Assert.That(host.TryOpenListening(), Is.True);
                Assert.That(host.ActiveListeningView, Is.Not.Null);
                Assert.That(host.ActiveListeningView, Is.Not.SameAs(firstView));
                Assert.That(host.ActiveListeningView.IsDisposed, Is.False);
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DestroyedResolutionCanvasMarksViewDisposedAndAllowsHostReopen()
        {
            var root = new GameObject("RC04 lifecycle destroyed resolution canvas");
            var values = new Dictionary<string, int>();
            SeedM08RosterCasesAvailable(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            RC04DingYueReefHost host = null;
            try
            {
                host = new RC04DingYueReefHost(root.transform, () => true, _ => true, read, write);
                Assert.That(host.TryOpenListening(), Is.True);
                for (int i = 0; i < 3; i++)
                    host.ActiveListeningView.SlowEchoButton.onClick.Invoke();
                host.ActiveListeningView.ApproachButton.onClick.Invoke();

                var resolutionView = host.ActiveResolutionView;
                Assert.That(resolutionView, Is.Not.Null);
                Assert.That(resolutionView.IsDisposed, Is.False);

                UnityEngine.Object.DestroyImmediate(resolutionView.Panel.parent.gameObject);
                Assert.That(resolutionView.IsDisposed, Is.True);
                Assert.That(host.ActiveResolutionView, Is.Null);

                Assert.That(host.TryOpenListening(), Is.True);
                Assert.That(host.ActiveListeningView, Is.Not.Null);
                Assert.That(host.ActiveListeningView.IsDisposed, Is.False);
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DestroyedParentFailsClosedWithoutThrowingOnTryOpenListening()
        {
            var root = new GameObject("RC04 lifecycle destroyed parent");
            var values = new Dictionary<string, int>();
            SeedM08RosterCasesAvailable(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            RC04DingYueReefHost host = null;
            try
            {
                host = new RC04DingYueReefHost(root.transform, () => true, _ => true, read, write);
                UnityEngine.Object.DestroyImmediate(root);
                root = null;

                bool opened = false;
                Assert.DoesNotThrow(() => opened = host.TryOpenListening());
                Assert.That(opened, Is.False);
                Assert.That(host.ActiveListeningView, Is.Null);
                Assert.That(host.ActiveResolutionView, Is.Null);
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
