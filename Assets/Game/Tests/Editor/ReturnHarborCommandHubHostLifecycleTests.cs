using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class ReturnHarborCommandHubHostLifecycleTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        private static ReturnFinaleFlowContext ObservedFinaleScenes()
        {
            return new ReturnFinaleFlowContext
            {
                AtJingwanAssembly = () => true,
                ThreeRecordsMatched = () => true,
                CarriesM02BattleReport = () => true
            };
        }

        private static void SeedThroughM09(Dictionary<string, int> values)
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
            Assert.That(M09BerthTableFacts.TryRecord(new M09BerthTableRecord(
                20, 23, M09BerthExchangeFlags.HanQiaoCivilianVoyage,
                M09RuiyaOutcome.Rescued, false, false), read, write), Is.True);
        }

        [Test]
        public void ExternalLedgerViewDisposalClearsActiveModeAndAllowsCrossHubSwitch()
        {
            var root = new GameObject("CommandHubExternalDisposeRoot");
            var values = new Dictionary<string, int>();
            SeedThroughM09(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            try
            {
                using (var hub = new ReturnHarborCommandHubHost(
                    root.transform, read, write, finaleContext: ObservedFinaleScenes()))
                {
                    Assert.That(hub.TryOpenStoryLedger(), Is.True);
                    Assert.That(hub.ActiveMode, Is.EqualTo(ReturnCommandHubMode.StoryLedger));
                    Assert.That(hub.LedgerHost.ActiveView, Is.Not.Null);

                    hub.LedgerHost.ActiveView.Dispose();
                    Assert.That(hub.ActiveMode, Is.EqualTo(ReturnCommandHubMode.None));
                    Assert.That(hub.HasAnyActivePanel, Is.False);

                    Assert.That(hub.TryOpenNextFinaleStage(), Is.True);
                    Assert.That(hub.ActiveMode, Is.EqualTo(ReturnCommandHubMode.FinaleFlow));
                    Assert.That(hub.FinaleHub.ActiveM10Host.ActiveView, Is.Not.Null);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DestroyedFinaleCanvasClearsActiveModeAndAllowsSwitchingToStoryLedger()
        {
            var root = new GameObject("CommandHubCanvasDestroyRoot");
            var values = new Dictionary<string, int>();
            SeedThroughM09(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            try
            {
                using (var hub = new ReturnHarborCommandHubHost(
                    root.transform, read, write, finaleContext: ObservedFinaleScenes()))
                {
                    Assert.That(hub.TryOpenNextFinaleStage(), Is.True);
                    var finaleView = hub.FinaleHub.ActiveM10Host.ActiveView;
                    Assert.That(finaleView, Is.Not.Null);

                    UnityEngine.Object.DestroyImmediate(finaleView.Panel.parent.gameObject);
                    Assert.That(hub.ActiveMode, Is.EqualTo(ReturnCommandHubMode.None));
                    Assert.That(hub.HasAnyActivePanel, Is.False);

                    Assert.That(hub.TryOpenStoryLedger(), Is.True);
                    Assert.That(hub.ActiveMode, Is.EqualTo(ReturnCommandHubMode.StoryLedger));
                    Assert.That(hub.LedgerHost.ActiveView, Is.Not.Null);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DestroyedUiParentFailsClosedAcrossAllHubEntryPointsWithoutThrowing()
        {
            var root = new GameObject("CommandHubParentDestroyRoot");
            var values = new Dictionary<string, int>();
            SeedThroughM09(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            ReturnHarborCommandHubHost hub = null;
            try
            {
                hub = new ReturnHarborCommandHubHost(
                    root.transform, read, write, finaleContext: ObservedFinaleScenes());
                UnityEngine.Object.DestroyImmediate(root);
                root = null;

                Assert.That(hub.TryOpenStoryLedger(), Is.False);
                Assert.That(hub.TryOpenRosterCase(ReturnRosterCaseId.RC01ChenYan), Is.False);
                Assert.That(hub.TryOpenRegionalStory(ReturnRegionalStoryId.ST02RiverLanterns), Is.False);
                Assert.That(hub.TryOpenNextFinaleStage(), Is.False);
                Assert.That(hub.HasAnyActivePanel, Is.False);
                Assert.That(hub.ActiveMode, Is.EqualTo(ReturnCommandHubMode.None));
            }
            finally
            {
                hub?.Dispose();
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
