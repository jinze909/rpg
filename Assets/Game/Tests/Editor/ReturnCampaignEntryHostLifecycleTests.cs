using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class ReturnCampaignEntryHostLifecycleTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        private static ReturnRosterCasesHubContext ObservedRosterScenes()
        {
            return new ReturnRosterCasesHubContext
            {
                AtWhiteReedFerry = () => true,
                AtReefListeningPoint = () => true,
                CanApproachReef = () => true,
                VerifiedReefSceneConditions = _ => true
            };
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
        public void ExternalChildViewDisposalClearsActiveModeAndAllowsCrossModeSwitch()
        {
            var root = new GameObject("CampaignEntryExternalDisposeRoot");
            var values = new Dictionary<string, int>();
            SeedThroughM09(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            try
            {
                using (var entry = new ReturnCampaignEntryHost(
                    root.transform, read, write,
                    rosterContext: ObservedRosterScenes(),
                    finaleContext: ObservedFinaleScenes()))
                {
                    Assert.That(entry.TryOpenStoryLedger(), Is.True);
                    Assert.That(entry.ActiveMode, Is.EqualTo(ReturnCampaignEntryMode.StoryLedger));
                    Assert.That(entry.CommandHub.LedgerHost.ActiveView, Is.Not.Null);

                    entry.CommandHub.LedgerHost.ActiveView.Dispose();
                    Assert.That(entry.ActiveMode, Is.EqualTo(ReturnCampaignEntryMode.None));
                    Assert.That(entry.HasAnyActivePanel, Is.False);

                    Assert.That(entry.TryOpenRosterCase(ReturnRosterCaseId.RC01ChenYan), Is.True);
                    Assert.That(entry.ActiveMode, Is.EqualTo(ReturnCampaignEntryMode.RosterCase));
                    Assert.That(entry.CommandHub.RosterHub.ActiveRC01Host.ActiveView, Is.Not.Null);
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
            var root = new GameObject("CampaignEntryCanvasDestroyRoot");
            var values = new Dictionary<string, int>();
            SeedThroughM09(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            try
            {
                using (var entry = new ReturnCampaignEntryHost(
                    root.transform, read, write,
                    rosterContext: ObservedRosterScenes(),
                    finaleContext: ObservedFinaleScenes()))
                {
                    Assert.That(entry.TryOpenNextMainStoryStage(), Is.True);
                    Assert.That(entry.ActiveMode, Is.EqualTo(ReturnCampaignEntryMode.FinaleFlow));
                    var finaleView = entry.CommandHub.FinaleHub.ActiveM10Host.ActiveView;
                    Assert.That(finaleView, Is.Not.Null);

                    UnityEngine.Object.DestroyImmediate(finaleView.Panel.parent.gameObject);
                    Assert.That(entry.ActiveMode, Is.EqualTo(ReturnCampaignEntryMode.None));
                    Assert.That(entry.HasAnyActivePanel, Is.False);

                    Assert.That(entry.TryOpenStoryLedger(), Is.True);
                    Assert.That(entry.ActiveMode, Is.EqualTo(ReturnCampaignEntryMode.StoryLedger));
                    Assert.That(entry.CommandHub.LedgerHost.ActiveView, Is.Not.Null);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DestroyedUiParentFailsClosedAcrossAllEntryPointsWithoutThrowing()
        {
            var root = new GameObject("CampaignEntryParentDestroyRoot");
            var values = new Dictionary<string, int>();
            SeedThroughM09(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            ReturnCampaignEntryHost entry = null;
            try
            {
                entry = new ReturnCampaignEntryHost(
                    root.transform, read, write,
                    rosterContext: ObservedRosterScenes(),
                    finaleContext: ObservedFinaleScenes());
                UnityEngine.Object.DestroyImmediate(root);
                root = null;

                Assert.That(entry.TryOpenNextMainStoryStage(), Is.False);
                Assert.That(entry.TryOpenOptionalM08DockTruth(), Is.False);
                Assert.That(entry.TryOpenStoryLedger(), Is.False);
                Assert.That(entry.TryOpenRosterCase(ReturnRosterCaseId.RC01ChenYan), Is.False);
                Assert.That(entry.TryOpenRegionalStory(ReturnRegionalStoryId.ST02RiverLanterns), Is.False);
                Assert.That(entry.HasAnyActivePanel, Is.False);
                Assert.That(entry.ActiveMode, Is.EqualTo(ReturnCampaignEntryMode.None));
            }
            finally
            {
                entry?.Dispose();
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
