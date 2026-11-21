using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    /// <summary>
    /// End-to-end live-Host integration tests verifying that ReturnMidgameFlowHost
    /// (M08-M09), ReturnRosterCasesHubHost (RC-01..RC-05), ReturnRegionalStoriesHubHost
    /// (ST-02/03/05/06), ReturnFinaleFlowHost (M10-M16), and ReturnStoryLedger
    /// stay synchronized across full playthrough progression and voyage replay reset.
    /// </summary>
    public sealed class ReturnFullHubEndToEndTests
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
        public void MidgameToFinaleFullHubPlaythroughSynchronizesLedgerAndResetsCleanlyForNewVoyage()
        {
            var root = new GameObject("FullHubE2ERoot");
            var values = new Dictionary<string, int>();
            SeedM07(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            try
            {
                using (var midgame = new ReturnMidgameFlowHost(
                    root.transform, new ReturnMidgameFlowContext
                    {
                        // Simulated observed facts for this full-voyage integration test.
                        // Production defaults remain unobserved and fail closed.
                        RealDaybreakObserved = () => true,
                        AtSecondBatchTable = () => true,
                        ShenLiPresentAtTable = () => true,
                        BaseBerths = () => 14,
                        PendingNames = () => 23,
                        RuiyaOutcome = () => M09RuiyaOutcome.Rescued
                    }, read, write))
                using (var rosterHub = new ReturnRosterCasesHubHost(
                    root.transform, new ReturnRosterCasesHubContext
                    {
                        // This E2E fixture explicitly reaches RC-01's ferry.
                        AtWhiteReedFerry = () => true
                    }, read, write))
                using (var regionalHub = new ReturnRegionalStoriesHubHost(
                    root.transform, new ReturnRegionalStoriesHubContext
                    {
                        // Observed Luting arrival in this integration fixture.
                        AtLutingDock = () => true
                    }, read, write))
                using (var finale = new ReturnFinaleFlowHost(
                    root.transform, new ReturnFinaleFlowContext
                    {
                        // Explicit observed Act-3 scene state for full-voyage E2E coverage.
                        AtJingwanAssembly = () => true,
                        ThreeRecordsMatched = () => true,
                        CarriesM02BattleReport = () => true,
                        AtHuilanPass = () => true,
                        ShenLiAccompanying = () => true,
                        OulinParticipating = () => true,
                        AtBellTowerCamp = () => true,
                        M03PriorConfession = () => true,
                        LinSuPresent = () => true,
                        FleetBriefingReady = () => true,
                        SafetyPrepComplete = () => true,
                        CanDepartNow = () => true,
                        AtStraitAction = () => true,
                        ValidSupervisionCredentials = () => true,
                        ValidPortGuarantee = () => true,
                        ThreeOpenDoorBellsRung = () => true,
                        FogRelayCompleted = () => true,
                        ReadCivilianCounts = () => new M14CivilianCounts(12, 2, 1),
                        CeremonyReady = () => true,
                        ConsentedVerifiedNamesCount = () => 12,
                        AtLinchaoAfternoon = () => true,
                        ShenLiWillingToMeet = () => true
                    }, read, write))
                {
                    // Before M08 notice: finale is locked, roster cases are locked.
                    Assert.That(finale.EvaluateNextStage(), Is.EqualTo(ReturnFinaleStage.None));
                    Assert.That(rosterHub.IsCaseAvailable(ReturnRosterCaseId.RC01ChenYan), Is.False);

                    // M08 Workshop -> M08 First World Notice.
                    Assert.That(midgame.TryOpenNextRequiredStage(), Is.True);
                    Assert.That(midgame.CurrentStage, Is.EqualTo(ReturnMidgameStage.M08WorkshopOrder));
                    var winch = new M08FirstWinchOrderSession();
                    Assert.That(winch.TryAcquirePart(M08WinchPart.ScrapGear), Is.True);
                    Assert.That(winch.TryAcquirePart(M08WinchPart.DockCable), Is.True);
                    Assert.That(winch.TryAssemble(), Is.True);
                    Assert.That(winch.TryTightenCable(), Is.True);
                    Assert.That(winch.TryTestLift(), Is.True);
                    Assert.That(M08WorkshopDepositFacts.TryConfirm(winch, read, write), Is.True);

                    Assert.That(midgame.TryOpenNextRequiredStage(), Is.True);
                    Assert.That(midgame.CurrentStage, Is.EqualTo(ReturnMidgameStage.M08FirstWorldNotice));
                    Assert.That(M08FirstWorldNoticeFacts.TryRecord(true, read, write), Is.True);

                    // Roster cases now open; settle RC-01 and permanent regional tale ST-02.
                    Assert.That(rosterHub.IsCaseAvailable(ReturnRosterCaseId.RC01ChenYan), Is.True);
                    Assert.That(rosterHub.TryOpenCase(ReturnRosterCaseId.RC01ChenYan), Is.True);
                    Assert.That(RC01ChenYanFacts.TryRecord(new RC01ChenYanRecord(
                        RC01FerryClues.All, RC01TalkOrder.LinSuFirst,
                        RC01ChenYanOutcome.KeepMarkedDead, true), read, write), Is.True);
                    rosterHub.CloseActiveCase();

                    Assert.That(regionalHub.TryOpenStory(ReturnRegionalStoryId.ST02RiverLanterns), Is.True);
                    Assert.That(ST02RiverLanternsFacts.TryRecord(new ST02RiverLanternsRecord(
                        ST02InspectionFlags.All, true, true,
                        ST02TrialDisclosure.PublishToBoth,
                        ST02BidMode.JointBidWithWrittenTerms,
                        ST02LockRescueMethod.BraceHullStressPoint), read, write), Is.True);
                    regionalHub.CloseActiveStory();

                    // Complete M09 -> Midgame reaches ReadyForActThree and Finale unlocks at M10.
                    Assert.That(midgame.TryOpenNextRequiredStage(), Is.True);
                    Assert.That(midgame.CurrentStage, Is.EqualTo(ReturnMidgameStage.M09BerthTable));
                    Assert.That(M09BerthTableFacts.TryRecord(new M09BerthTableRecord(
                        20, 23, M09BerthExchangeFlags.HanQiaoCivilianVoyage,
                        M09RuiyaOutcome.Rescued, false, false), read, write), Is.True);
                    Assert.That(midgame.TryOpenNextRequiredStage(), Is.False);
                    Assert.That(midgame.CurrentStage, Is.EqualTo(ReturnMidgameStage.ReadyForActThree));

                    Assert.That(finale.EvaluateNextStage(), Is.EqualTo(ReturnFinaleStage.M10Assembly));
                    Assert.That(ReturnStoryLedger.TryBuildSnapshot(read, out var atM10), Is.True);
                    Assert.That(atM10.ActiveChapter, Is.EqualTo(ReturnMainChapter.M10Assembly));
                    Assert.That(atM10.SettledRosterCaseCount, Is.EqualTo(1));
                    Assert.That(atM10.SettledRegionalStoryCount, Is.EqualTo(1));

                    // Drive Finale M10 -> M16 through live Hosts.
                    Assert.That(finale.TryOpenNextStage(), Is.True);
                    var m10 = finale.ActiveM10Host.Session;
                    Assert.That(m10.TryObserveWorldNoticeThree(), Is.True);
                    Assert.That(m10.TryReconcileThreeRecords(true), Is.True);
                    Assert.That(m10.TrySecureBattleReportCopy(M10BattleReportSource.CarriedFromM02), Is.True);
                    Assert.That(m10.TryDecideHanLedgerUse(M10HanLedgerUse.KeepConfidential), Is.True);
                    Assert.That(m10.TrySignSupplyContract(M10SupplyContractStatus.SignedFullCoverage), Is.True);
                    Assert.That(m10.TryReceiveBellTowerSeizureReport(), Is.True);
                    Assert.That(m10.TryCommit(read, write), Is.True);

                    Assert.That(finale.TryOpenNextStage(), Is.True);
                    var m11 = finale.ActiveM11Host.Session;
                    Assert.That(m11.TryObserveThreeBellsAndEnter(M11PassEntryMethod.LandGateNegotiation), Is.True);
                    Assert.That(m11.TryObserveConscriptionAndCoverReturnees(), Is.True);
                    Assert.That(m11.TryHearShenLiFogCall(), Is.True);
                    Assert.That(m11.TryHearShenLiFogCall(), Is.True);
                    Assert.That(m11.TryHearShenLiFogCall(), Is.True);
                    Assert.That(m11.TryAnswerPresentForShenLiExtraction(), Is.True);
                    Assert.That(m11.TryChooseDivisionPact(M11ShenDivisionPact.MutualConsultAndDivision), Is.True);
                    Assert.That(m11.TryCommit(read, write), Is.True);

                    Assert.That(finale.TryOpenNextStage(), Is.True);
                    var m12 = finale.ActiveM12Host.Session;
                    Assert.That(m12.TryConfrontLuJun(M12LuConfrontationReply.YouSaidThatLastTime), Is.True);
                    Assert.That(m12.TryHearStatement(M12StatementFlags.JiangShenHeard), Is.True);
                    Assert.That(m12.TryHearStatement(M12StatementFlags.HanAndYiLeiHeard), Is.True);
                    Assert.That(m12.TryHearStatement(M12StatementFlags.ShenLiHeard), Is.True);
                    Assert.That(m12.TryDecideTruthAndCommit(M12TruthDisclosure.PublishTruth, read, write), Is.True);

                    Assert.That(finale.TryOpenNextStage(), Is.True);
                    Assert.That(finale.ActiveM13Host.Session.TryConfirmDeparture(
                        M13ApproachRoute.RouteASupervisedCivilian, read, write), Is.True);

                    Assert.That(finale.TryOpenNextStage(), Is.True);
                    var m14 = finale.ActiveM14Host.Session;
                    Assert.That(m14.TryResolveRouteASupervisedSquare(
                        validSupervisionCredentials: true,
                        validPortGuarantee: true,
                        luJunDisposition: M14LuJunDisposition.RelievedOfCommand), Is.True);
                    Assert.That(m14.TryVerifyCivilianExit(12, 2, 1), Is.True);
                    Assert.That(m14.TrySettleAyuanAndCommit(M14AyuanOutcome.Safe, read, write), Is.True);

                    Assert.That(finale.TryOpenNextStage(), Is.True);
                    Assert.That(finale.ActiveM15Host.Session.TryCompleteRollCall(
                        M15RollCallReader.Protagonist,
                        M15FinalIdentityResponse.PublicTrueName,
                        M15SteleInscription.CarveConsentedVerifiedNames,
                        read, write), Is.True);

                    Assert.That(finale.TryOpenNextStage(), Is.True);
                    var m16View = finale.ActiveM16Host.ActiveView;
                    m16View.ProceedToDeskButton.onClick.Invoke();
                    m16View.StepOutsideButton.onClick.Invoke();
                    m16View.PierButton.onClick.Invoke();
                    Assert.That(finale.TryOpenNextStage(), Is.False);
                    Assert.That(finale.CurrentStage, Is.EqualTo(ReturnFinaleStage.Completed));

                    Assert.That(ReturnStoryLedger.TryBuildSnapshot(read, out var completed), Is.True);
                    Assert.That(completed.ActiveChapter, Is.EqualTo(ReturnMainChapter.StoryCompleted));

                    // Full downstream voyage reset clears M06-M16 and RC-01..RC-05 while preserving permanent ST-02.
                    Assert.That(ReturnStoryLedger.TryResetAllDownstreamVoyageFacts(write), Is.True);
                    Assert.That(midgame.EvaluateNextRequiredStage(), Is.EqualTo(ReturnMidgameStage.None));
                    Assert.That(finale.EvaluateNextStage(), Is.EqualTo(ReturnFinaleStage.None));
                    Assert.That(rosterHub.CountSettledCases(), Is.EqualTo(0));
                    Assert.That(regionalHub.IsStorySettled(ReturnRegionalStoryId.ST02RiverLanterns), Is.True);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
