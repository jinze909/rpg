using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class ReturnStoryLedgerTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
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
                taoJiRescued: true), read, write), Is.True);

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
        public void SnapshotTracksMainStoryProgressionAndFailsClosedOnMissingUpstream()
        {
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            Assert.That(ReturnStoryLedger.TryBuildSnapshot(read, out var initial), Is.True);
            Assert.That(initial.ActiveChapter, Is.EqualTo(ReturnMainChapter.BeforeM08));
            Assert.That(initial.RC01State, Is.EqualTo(ReturnRosterCaseState.Locked));

            SeedThroughM08Notice(values);
            Assert.That(ReturnStoryLedger.TryBuildSnapshot(read, out var atM09), Is.True);
            Assert.That(atM09.ActiveChapter, Is.EqualTo(ReturnMainChapter.M09BerthTable));
            Assert.That(atM09.RC01State, Is.EqualTo(ReturnRosterCaseState.Available));

            Assert.That(M09BerthTableFacts.TryRecord(new M09BerthTableRecord(
                20, 23, M09BerthExchangeFlags.HanQiaoCivilianVoyage,
                M09RuiyaOutcome.Rescued, false, false), read, write), Is.True);
            Assert.That(M10AssemblyFacts.TryRecord(new M10AssemblyRecord(
                true, M10RosterAccuracy.VerifiedFirstPass, M10BattleReportSource.CarriedFromM02,
                M10HanLedgerUse.KeepConfidential, M10SupplyContractStatus.SignedFullCoverage,
                false, false, true), read, write), Is.True);
            Assert.That(M11ReverseRescueFacts.TryRecord(new M11ReverseRescueRecord(
                true, M11PassEntryMethod.LandGateNegotiation, true,
                M11ThirdSquadConscriptionStance.NotInPass,
                M11RescueProvider.FallbackPaidArtisans, 0, false,
                M11ShenDivisionPact.NoneShenAbsent), read, write), Is.True);
            Assert.That(M12BridgeTruthFacts.TryRecord(new M12BridgeTruthRecord(
                false, false, M12LuConfrontationReply.SomeWantNoMoreWar,
                false, M12StatementFlags.RequiredBase, false, false,
                M12TruthDisclosure.SealTruth), read, write), Is.True);
            Assert.That(M13DepartureRiskFacts.TryRecordDeparture(new M13DepartureRecord(
                M13ApproachRoute.RouteASupervisedCivilian, true, false, false, false),
                read, write), Is.True);
            Assert.That(M14FinalPassageFacts.TryRecord(new M14FinalPassageRecord(
                M13ApproachRoute.RouteASupervisedCivilian,
                M14PassageOutcome.RouteASupervisedCivilianEstablished,
                true, 12, 2, 1, M14BorderForceAction.NotRouteB,
                M14LuJunDisposition.RelievedOfCommand, M14AyuanOutcome.Safe, false),
                read, write), Is.True);
            Assert.That(M15FinalRollCallFacts.TryRecord(new M15FinalRollCallRecord(
                M15RollCallReader.Protagonist, M15FinalIdentityResponse.PublicTrueName,
                M15PensionClosure.StoppedAndReconciled, M15SteleInscription.KeepOriginal,
                0, false), read, write), Is.True);
            Assert.That(M16EpilogueFacts.TryRecord(new M16EpilogueRecord(
                M16WalkStops.None, M16DeliveredLettersFlags.None, false,
                M16StandingSpot.PierSeaBreeze, true, M16RespectAgencyTone.NeutralMixedExperience),
                read, write), Is.True);

            Assert.That(ReturnStoryLedger.TryBuildSnapshot(read, out var completed), Is.True);
            Assert.That(completed.ActiveChapter, Is.EqualTo(ReturnMainChapter.StoryCompleted));

            // Corrupting M10 must fail-close progress back to M10 instead of reporting StoryCompleted.
            write(M10AssemblyFacts.ConfirmedKey, 0);
            Assert.That(ReturnStoryLedger.TryBuildSnapshot(read, out var corrupted), Is.True);
            Assert.That(corrupted.ActiveChapter, Is.EqualTo(ReturnMainChapter.M10Assembly));
        }

        [Test]
        public void SnapshotReflectsRosterCaseAvailabilityOutcomesAndRegionalTales()
        {
            var values = new Dictionary<string, int>();
            SeedThroughM08Notice(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            Assert.That(RC01ChenYanFacts.TryRecord(new RC01ChenYanRecord(
                RC01FerryClues.All, RC01TalkOrder.LinSuFirst,
                RC01ChenYanOutcome.KeepMarkedDead, true), read, write), Is.True);
            Assert.That(RC03BaijiMineFacts.TryRecord(new RC03BaijiMineRecord(
                RC03IslandVisits.All, RC03Resolution.AmendCharterWithSuccessors,
                true, true, false, true), read, write), Is.True);
            Assert.That(ST02RiverLanternsFacts.TryRecord(new ST02RiverLanternsRecord(
                ST02InspectionFlags.All, true, true,
                ST02TrialDisclosure.PublishToBoth,
                ST02BidMode.JointBidWithWrittenTerms,
                ST02LockRescueMethod.BraceHullStressPoint), read, write), Is.True);
            Assert.That(ST06TaoJiHomecomingFacts.TryRecord(new ST06HomecomingRecord(
                ST06ChairResolution.RefundAndRebuild, false, true, false,
                ST06LetterStatus.Delivered, false), read, write), Is.True);

            Assert.That(ReturnStoryLedger.TryBuildSnapshot(read, out var snap), Is.True);
            Assert.That(snap.RC01State, Is.EqualTo(ReturnRosterCaseState.Settled));
            Assert.That(snap.RC02State, Is.EqualTo(ReturnRosterCaseState.Available));
            Assert.That(snap.RC03State, Is.EqualTo(ReturnRosterCaseState.Settled));
            Assert.That(snap.RC04State, Is.EqualTo(ReturnRosterCaseState.Available));
            Assert.That(snap.RC05State, Is.EqualTo(ReturnRosterCaseState.Available));
            Assert.That(snap.SettledRosterCaseCount, Is.EqualTo(2));
            Assert.That(snap.ST02Record.HasValue, Is.True);
            Assert.That(snap.ST06Record.HasValue, Is.True);
            Assert.That(snap.SettledRegionalStoryCount, Is.EqualTo(2));
        }

        [Test]
        public void ResetAllDownstreamVoyageFactsClearsM06ThroughM16AndRosterCasesWithoutWipingPermanentRegionalTales()
        {
            var values = new Dictionary<string, int>();
            SeedThroughM08Notice(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            Assert.That(RC01ChenYanFacts.TryRecord(new RC01ChenYanRecord(
                RC01FerryClues.All, RC01TalkOrder.QiuniangFirst,
                RC01ChenYanOutcome.RestoreLivingName, true), read, write), Is.True);
            Assert.That(RC05ThirdSquadFacts.TryRecord(new RC05ThirdSquadRecord(
                true, RC05Resolution.CivilianPardon, false, true, true), read, write), Is.True);
            Assert.That(ST02RiverLanternsFacts.TryRecord(new ST02RiverLanternsRecord(
                ST02InspectionFlags.All, true, true,
                ST02TrialDisclosure.PublishToBoth,
                ST02BidMode.JointBidWithWrittenTerms,
                ST02LockRescueMethod.BraceHullStressPoint), read, write), Is.True);
            Assert.That(ST03BathhouseFacts.TryRecord(new ST03BathhouseRecord(
                ST03BathhouseChoice.ReopenWithSubsidizedHours, true, true, true, true),
                read, write), Is.True);
            Assert.That(ST05TwoDoorsFacts.TryRecordHome(new ST05HouseholdRecord(
                ST05BedResolution.ConvertBedToCourtyardBenches,
                ST05HomeArrangement.KeepTwoDoorsAndCourtyardTable, false), read, write), Is.True);
            Assert.That(ST05TwoDoorsFacts.TryRecordAssignment(
                ST05ChengAssignment.StayLocal, read, write), Is.True);
            Assert.That(ST06TaoJiHomecomingFacts.TryRecord(new ST06HomecomingRecord(
                ST06ChairResolution.TeachJointTogether, false, true, false,
                ST06LetterStatus.Delivered, false), read, write), Is.True);
            Assert.That(M09BerthTableFacts.TryRecord(new M09BerthTableRecord(
                20, 23, M09BerthExchangeFlags.HanQiaoCivilianVoyage,
                M09RuiyaOutcome.Rescued, false, false), read, write), Is.True);
            Assert.That(M10AssemblyFacts.TryRecord(new M10AssemblyRecord(
                true, M10RosterAccuracy.VerifiedFirstPass, M10BattleReportSource.CarriedFromM02,
                M10HanLedgerUse.KeepConfidential, M10SupplyContractStatus.SignedFullCoverage,
                false, false, true), read, write), Is.True);

            // Simulate a torn write after the 20 marker-invalidation writes.
            int writes = 0;
            Assert.Throws<InvalidOperationException>(() =>
                ReturnStoryLedger.TryResetAllDownstreamVoyageFacts((k, v) =>
                {
                    if (writes >= 20)
                        throw new InvalidOperationException("Simulated torn reset");
                    writes++;
                    values[k] = v;
                }));

            // Even after a torn reset, all voyage-bound chapters and roster cases must already be unreadable.
            Assert.That(ReturnStoryLedger.TryBuildSnapshot(read, out var afterTorn), Is.True);
            Assert.That(afterTorn.ActiveChapter, Is.EqualTo(ReturnMainChapter.BeforeM08));
            Assert.That(afterTorn.SettledRosterCaseCount, Is.EqualTo(0));
            Assert.That(afterTorn.ST06Record, Is.Null);

            // Complete reset cleanly and verify permanent regional tales survived intact.
            Assert.That(ReturnStoryLedger.TryResetAllDownstreamVoyageFacts(write), Is.True);
            Assert.That(ReturnStoryLedger.TryBuildSnapshot(read, out var afterClean), Is.True);
            Assert.That(afterClean.ST02Record.HasValue, Is.True);
            Assert.That(afterClean.ST03Record.HasValue, Is.True);
            Assert.That(afterClean.ST05Outcome.HasValue, Is.True);
            Assert.That(afterClean.SettledRegionalStoryCount, Is.EqualTo(3));
        }

        [Test]
        public void StoryLedgerHostAndViewSwitchTabsRefreshLocaleAndDisposeCleanly()
        {
            var root = new GameObject("LedgerTestRoot");
            string previousLanguage = SoundwellUiLocalization.Language;
            try
            {
                var values = new Dictionary<string, int>();
                SeedThroughM08Notice(values);
                Func<string, int> read = Reader(values);

                using (var host = new ReturnStoryLedgerHost(root.transform, read))
                {
                    bool closedCalled = false;
                    Assert.That(host.TryOpenOrRefresh(() => closedCalled = true), Is.True);
                    Assert.That(host.IsOpen, Is.True);

                    var view = host.ActiveView;
                    SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
                    Assert.That(view.Heading.text, Does.Contain("主线名册"));
                    Assert.That(view.Body.text, Does.Contain("M09"));

                    view.RosterTabButton.onClick.Invoke();
                    Assert.That(view.ActiveTab, Is.EqualTo(ReturnStoryLedgerTab.RosterCases));
                    Assert.That(view.Body.text, Does.Contain("可调查"));

                    SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                    Assert.That(view.Heading.text, Does.Contain("ROSTER CASES"));
                    Assert.That(view.Body.text, Does.Contain("Available"));

                    view.RegionalTabButton.onClick.Invoke();
                    Assert.That(view.ActiveTab, Is.EqualTo(ReturnStoryLedgerTab.RegionalTales));
                    view.LayoutForViewportAspect(16f / 9f);

                    view.CloseButton.onClick.Invoke();
                    Assert.That(closedCalled, Is.True);
                    Assert.That(host.IsOpen, Is.False);
                }
            }
            finally
            {
                SoundwellUiLocalization.SetLanguage(previousLanguage);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
