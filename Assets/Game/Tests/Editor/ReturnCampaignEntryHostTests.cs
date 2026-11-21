using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class ReturnCampaignEntryHostTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        private static void SeedM07(Dictionary<string, int> values)
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
        }

        [Test]
        public void RoutesMainStorySeamlesslyFromM08ThroughM10AndEnforcesMidgameToCommandHubExclusion()
        {
            var root = new GameObject("CampaignEntryRouteRoot");
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            try
            {
                using (var entry = new ReturnCampaignEntryHost(root.transform, read, write))
                {
                    // Before M07: main-story routing fails closed.
                    Assert.That(entry.TryOpenNextMainStoryStage(), Is.False);
                    Assert.That(entry.ActiveMode, Is.EqualTo(ReturnCampaignEntryMode.None));

                    SeedM07(values);

                    // Routes automatically to M08 Workshop Order and blocks Story Ledger while open.
                    Assert.That(entry.TryOpenNextMainStoryStage(), Is.True);
                    Assert.That(entry.ActiveMode, Is.EqualTo(ReturnCampaignEntryMode.MidgameFlow));
                    Assert.That(entry.MidgameHub.CurrentStage, Is.EqualTo(ReturnMidgameStage.M08WorkshopOrder));
                    Assert.That(entry.TryOpenStoryLedger(), Is.False);

                    // Complete M08 Workshop -> M08 Notice -> M09 Berth Table.
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

                    // Close stale M08 panel, then TryOpenNextMainStoryStage transitions automatically into M10 Finale Flow!
                    entry.CloseActivePanel();
                    Assert.That(entry.TryOpenNextMainStoryStage(), Is.True);
                    Assert.That(entry.ActiveMode, Is.EqualTo(ReturnCampaignEntryMode.FinaleFlow));
                    Assert.That(entry.CommandHub.FinaleHub.CurrentStage, Is.EqualTo(ReturnFinaleStage.M10Assembly));

                    // While Finale Flow is open, optional M08 dock truth and Story Ledger are blocked.
                    Assert.That(entry.TryOpenOptionalM08DockTruth(), Is.False);
                    Assert.That(entry.TryOpenStoryLedger(), Is.False);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void NewVoyageResetClosesActivePanelClearsVoyageFactsAndPreservesPermanentRegionalTales()
        {
            var root = new GameObject("CampaignEntryResetRoot");
            var values = new Dictionary<string, int>();
            SeedM07(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            Assert.That(ST02RiverLanternsFacts.TryRecord(new ST02RiverLanternsRecord(
                ST02InspectionFlags.All, true, true,
                ST02TrialDisclosure.PublishToBoth,
                ST02BidMode.JointBidWithWrittenTerms,
                ST02LockRescueMethod.BraceHullStressPoint), read, write), Is.True);

            try
            {
                using (var entry = new ReturnCampaignEntryHost(root.transform, read, write))
                {
                    Assert.That(entry.TryOpenNextMainStoryStage(), Is.True);
                    Assert.That(entry.HasAnyActivePanel, Is.True);

                    Assert.That(entry.TryStartNewVoyageReset(), Is.True);
                    Assert.That(entry.HasAnyActivePanel, Is.False);
                    Assert.That(entry.ActiveMode, Is.EqualTo(ReturnCampaignEntryMode.None));

                    Assert.That(entry.TryBuildProgressSnapshot(out var snap), Is.True);
                    Assert.That(snap.ActiveChapter, Is.EqualTo(ReturnMainChapter.BeforeM08));
                    Assert.That(snap.ST02Record.HasValue, Is.True);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
