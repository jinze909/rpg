using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class ReturnRosterCasesHubHostLifecycleTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        private static ReturnRosterCasesHubContext ObservedScenes()
        {
            return new ReturnRosterCasesHubContext
            {
                AtWhiteReedFerry = () => true,
                AtReefListeningPoint = () => true,
                CanApproachReef = () => true,
                VerifiedReefSceneConditions = _ => true
            };
        }

        private static void SeedThroughM08Notice(Dictionary<string, int> values)
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
        public void ExternalChildViewDisposalClearsHasActiveCaseAndAllowsReopenOrSwitch()
        {
            var root = new GameObject("RosterHubExternalDisposeRoot");
            var values = new Dictionary<string, int>();
            SeedThroughM08Notice(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            try
            {
                using (var hub = new ReturnRosterCasesHubHost(
                    root.transform, ObservedScenes(), read, write))
                {
                    Assert.That(hub.TryOpenCase(ReturnRosterCaseId.RC01ChenYan), Is.True);
                    var firstHost = hub.ActiveRC01Host;
                    Assert.That(hub.HasActiveCase, Is.True);
                    Assert.That(firstHost.ActiveView, Is.Not.Null);

                    firstHost.ActiveView.Dispose();
                    Assert.That(hub.HasActiveCase, Is.False);

                    Assert.That(hub.TryOpenCase(ReturnRosterCaseId.RC04DingYueReef), Is.True);
                    Assert.That(hub.HasActiveCase, Is.True);
                    Assert.That(hub.ActiveCase, Is.EqualTo(ReturnRosterCaseId.RC04DingYueReef));
                    Assert.That(hub.ActiveRC04Host.ActiveListeningView, Is.Not.Null);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DestroyedChildCanvasClearsHasActiveCaseAndAllowsCleanReopen()
        {
            var root = new GameObject("RosterHubCanvasDestroyRoot");
            var values = new Dictionary<string, int>();
            SeedThroughM08Notice(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            try
            {
                using (var hub = new ReturnRosterCasesHubHost(
                    root.transform, ObservedScenes(), read, write))
                {
                    Assert.That(hub.TryOpenCase(ReturnRosterCaseId.RC04DingYueReef), Is.True);
                    var listeningView = hub.ActiveRC04Host.ActiveListeningView;
                    Assert.That(listeningView, Is.Not.Null);

                    UnityEngine.Object.DestroyImmediate(listeningView.Panel.parent.gameObject);
                    Assert.That(hub.HasActiveCase, Is.False);

                    Assert.That(hub.TryOpenCase(ReturnRosterCaseId.RC01ChenYan), Is.True);
                    Assert.That(hub.HasActiveCase, Is.True);
                    Assert.That(hub.ActiveRC01Host.ActiveView, Is.Not.Null);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DestroyedUiParentFailsClosedOnTryOpenCaseWithoutThrowing()
        {
            var root = new GameObject("RosterHubParentDestroyRoot");
            var values = new Dictionary<string, int>();
            SeedThroughM08Notice(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            ReturnRosterCasesHubHost hub = null;
            try
            {
                hub = new ReturnRosterCasesHubHost(
                    root.transform, ObservedScenes(), read, write);
                UnityEngine.Object.DestroyImmediate(root);
                root = null;

                Assert.That(hub.TryOpenCase(ReturnRosterCaseId.RC01ChenYan), Is.False);
                Assert.That(hub.HasActiveCase, Is.False);
            }
            finally
            {
                hub?.Dispose();
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
