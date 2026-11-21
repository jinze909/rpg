using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    /// <summary>
    /// End-to-end cross-chapter continuity and replay-isolation regression
    /// covering M04 through M16 alongside RC-01/RC-02/RC-05 and permanent
    /// regional tales (ST-02, ST-03, ST-05) on a single shared Makinom store.
    /// </summary>
    public sealed class ReturnEndToEndStoryChainTests
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
        public void FullMainStoryChainM04ThroughM16PropagatesRC02M10M13FactsAccurately()
        {
            var values = new Dictionary<string, int>();
            SeedThroughM08Notice(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            // Complete RC-01, RC-02 (AllowByException), and ST-02; leave RC-05 unstarted.
            Assert.That(RC01ChenYanFacts.TryRecord(new RC01ChenYanRecord(
                RC01FerryClues.All, RC01TalkOrder.LinSuFirst,
                RC01ChenYanOutcome.KeepMarkedDead, linSuRegisterNoteRecorded: true),
                read, write), Is.True);
            Assert.That(RC02ZhengYuanFacts.TryRecord(new RC02ZhengYuanRecord(
                RC02IntakeChecks.All, true, RC02Resolution.AllowByException,
                motherCalledShiLeng: false, shiLengConsented: false,
                realZhengYuanFound: false), read, write), Is.True);
            Assert.That(ST02RiverLanternsFacts.TryRecord(new ST02RiverLanternsRecord(
                ST02InspectionFlags.All, true, true,
                ST02TrialDisclosure.PublishToBoth,
                ST02BidMode.JointBidWithWrittenTerms,
                ST02LockRescueMethod.BraceHullStressPoint), read, write), Is.True);

            // M09 -> M10 (RC-02 weakens leverage; PublishDraft exposes Han Qiao).
            Assert.That(M09BerthTableFacts.TryRecord(new M09BerthTableRecord(
                20, 23, M09BerthExchangeFlags.HanQiaoCivilianVoyage,
                M09RuiyaOutcome.Rescued, false, false), read, write), Is.True);

            Assert.That(M10AssemblySession.TryCreateFromSavedFacts(read, out var m10), Is.True);
            Assert.That(m10.TryObserveWorldNoticeThree(), Is.True);
            Assert.That(m10.TryReconcileThreeRecords(true), Is.True);
            Assert.That(m10.TrySecureBattleReportCopy(M10BattleReportSource.CarriedFromM02), Is.True);
            Assert.That(m10.TryDecideHanLedgerUse(M10HanLedgerUse.PublishDraft), Is.True);
            Assert.That(m10.TrySignSupplyContract(M10SupplyContractStatus.PartialGapToM13), Is.True);
            Assert.That(m10.TryReceiveBellTowerSeizureReport(), Is.True);
            Assert.That(m10.TryCommit(read, write), Is.True);
            Assert.That(m10.CompletedRecord.Value.LeverageWeakenedByRC02, Is.True);
            Assert.That(m10.CompletedRecord.Value.CarriesSupplyGapToM13, Is.True);

            // M11 (Shen Li present, Oulin absent -> shore boatman takes helm).
            Assert.That(M11ReverseRescueSession.TryCreateFromSavedFacts(
                read, shenLiAccompanying: true, oulinParticipating: false, out var m11), Is.True);
            Assert.That(m11.TryObserveThreeBellsAndEnter(M11PassEntryMethod.LandGateNegotiation), Is.True);
            Assert.That(m11.TryObserveConscriptionAndCoverReturnees(), Is.True);
            Assert.That(m11.TryHearShenLiFogCall(), Is.True);
            Assert.That(m11.TryHearShenLiFogCall(), Is.True);
            Assert.That(m11.TryHearShenLiFogCall(), Is.True);
            Assert.That(m11.TryAnswerPresentForShenLiExtraction(), Is.True);
            Assert.That(m11.TryChooseDivisionPact(M11ShenDivisionPact.MutualConsultAndDivision), Is.True);
            Assert.That(m11.TryCommit(read, write), Is.True);
            Assert.That(m11.CompletedRecord.Value.ShoreBoatmanTookHelm, Is.True);

            // M12 (Sealing M12 preserves M10's irrevocable PublishDraft & RC-01 Lin Su echo).
            Assert.That(M12BridgeTruthSession.TryCreateFromSavedFacts(
                read, m03PriorConfession: true, m03SecrecyPromised: true,
                shenLiParticipating: true, linSuPresent: true, out var m12), Is.True);
            Assert.That(m12.TryConfrontLuJun(M12LuConfrontationReply.YouSaidThatLastTime), Is.True);
            Assert.That(m12.TryHearLinSuEcho(), Is.True);
            Assert.That(m12.TryHearStatement(M12StatementFlags.JiangShenHeard), Is.True);
            Assert.That(m12.TryHearStatement(M12StatementFlags.HanAndYiLeiHeard), Is.True);
            Assert.That(m12.TryHearStatement(M12StatementFlags.ShenLiHeard), Is.True);
            Assert.That(m12.TryDecideTruthAndCommit(M12TruthDisclosure.SealTruth, read, write), Is.True);
            Assert.That(m12.CompletedRecord.Value.HanQiaoAlreadyOutOfOffice, Is.True);
            Assert.That(m12.CompletedRecord.Value.M10PublishedDraftRemainsIrrevocable, Is.True);
            Assert.That(m12.CompletedRecord.Value.BreaksLuSecrecyPromise, Is.False);

            // M13 -> M14 (Route B high-risk consent syncs A Yuan outcome back to M13).
            Assert.That(M13DepartureRiskFacts.TryRecordDeparture(new M13DepartureRecord(
                M13ApproachRoute.RouteBBellTowerAssault,
                safetyPreparationComplete: false, qiQueWarningGiven: true,
                explicitHighRiskConsent: true, threeBellReverseSignalAgreed: true),
                read, write), Is.True);
            Assert.That(M14FinalPassageSession.TryCreateFromSavedFacts(read, out var m14), Is.True);
            Assert.That(m14.TryResolveRouteBBellTowerAssault(true, M14BorderForceAction.StopAtEscort), Is.True);
            Assert.That(m14.TryVerifyCivilianExit(19, 3, 2), Is.True);
            Assert.That(m14.TrySettleAyuanAndCommit(
                M14AyuanOutcome.SeverelyInjuredRetired, read, write), Is.True);
            Assert.That(M13DepartureRiskFacts.TryReadAyuanOutcome(read, out M14AyuanOutcome? ayuan), Is.True);
            Assert.That(ayuan, Is.EqualTo(M14AyuanOutcome.SeverelyInjuredRetired));

            // M15 -> M16 (Eligible letters include RC01|RC02|ST02 and exclude unstarted RC05).
            Assert.That(M15FinalRollCallFacts.TryRecord(new M15FinalRollCallRecord(
                M15RollCallReader.Protagonist,
                M15FinalIdentityResponse.PublicTrueName,
                M15PensionClosure.StoppedAndReconciled,
                M15SteleInscription.CarveConsentedVerifiedNames,
                consentedVerifiedNamesCount: 12,
                xiaoqiCalledByNumberSeventyTwo: false), read, write), Is.True);
            Assert.That(M16EpilogueSession.TryCreateFromSavedFacts(
                read, shenLiWillingToMeet: true,
                M16RespectAgencyTone.RespectedOthersChoices, out var m16), Is.True);
            Assert.That(m16.EligibleLetters, Is.EqualTo(
                M16DeliveredLettersFlags.RC01ChenYan |
                M16DeliveredLettersFlags.RC02ZhengYuan |
                M16DeliveredLettersFlags.ST02RiverLanterns));
            Assert.That(m16.TryProceedToDesk(), Is.True);
            Assert.That(m16.TryReadLetter(M16DeliveredLettersFlags.RC05ThirdSquad), Is.False);
            Assert.That(m16.TryReadLetter(M16DeliveredLettersFlags.RC01ChenYan), Is.True);
            Assert.That(m16.TryStepOutsideForEvening(), Is.True);
            Assert.That(m16.TryChooseStandingSpotAndCommit(
                M16StandingSpot.PierSeaBreeze, read, write), Is.True);
            Assert.That(m16.IsCompleted, Is.True);
        }

        [Test]
        public void RC05EnlistUnderLuJunPropagatesIntoM11StanceAndM16LetterEligibility()
        {
            var values = new Dictionary<string, int>();
            SeedThroughM08Notice(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            Assert.That(RC05ThirdSquadFacts.TryRecord(new RC05ThirdSquadRecord(
                trialDeliveryVerified: true, RC05Resolution.EnlistUnderLuJun,
                skirmishOccurred: false, realZhengYuanVerified: true,
                oldWomanStoneMentioned: true), read, write), Is.True);
            Assert.That(M09BerthTableFacts.TryRecord(new M09BerthTableRecord(
                20, 23, M09BerthExchangeFlags.HanQiaoCivilianVoyage,
                M09RuiyaOutcome.Rescued, false, false), read, write), Is.True);

            Assert.That(M10AssemblySession.TryCreateFromSavedFacts(read, out var m10), Is.True);
            Assert.That(m10.TryObserveWorldNoticeThree(), Is.True);
            Assert.That(m10.TryReconcileThreeRecords(true), Is.True);
            Assert.That(m10.TrySecureBattleReportCopy(M10BattleReportSource.CarriedFromM02), Is.True);
            Assert.That(m10.TryDecideHanLedgerUse(M10HanLedgerUse.KeepConfidential), Is.True);
            Assert.That(m10.TrySignSupplyContract(M10SupplyContractStatus.SignedFullCoverage), Is.True);
            Assert.That(m10.TryReceiveBellTowerSeizureReport(), Is.True);
            Assert.That(m10.TryCommit(read, write), Is.True);
            Assert.That(m10.CompletedRecord.Value.CoversRC05MountainFamilies, Is.False);

            Assert.That(M11ReverseRescueSession.TryCreateFromSavedFacts(
                read, shenLiAccompanying: false, oulinParticipating: false, out var m11), Is.True);
            Assert.That(m11.RC05EnlistedInPass, Is.True);
            Assert.That(m11.TryObserveThreeBellsAndEnter(M11PassEntryMethod.ReliefSupplyPass), Is.True);
            Assert.That(m11.TryObserveConscriptionAndCoverReturnees(
                M11ThirdSquadConscriptionStance.NotInPass), Is.False);
            Assert.That(m11.TryObserveConscriptionAndCoverReturnees(
                M11ThirdSquadConscriptionStance.RefusedForcedConscription), Is.True);
            Assert.That(m11.TryExtractViaFallbackArtisans(), Is.True);
            Assert.That(m11.TryCommit(read, write), Is.True);

            Assert.That(M15FinalRollCallFacts.TryRecord(new M15FinalRollCallRecord(
                M15RollCallReader.HanQiao,
                M15FinalIdentityResponse.PrivateCustodianResponse,
                M15PensionClosure.UnchangedOrPrivate,
                M15SteleInscription.KeepOriginal, 0, true), read, write), Is.True);
            Assert.That(M16EpilogueFacts.EvaluateEligibleLetters(read),
                Is.EqualTo(M16DeliveredLettersFlags.RC05ThirdSquad));
        }

        [Test]
        public void VoyageReplayResetsInvalidateDownstreamMainStoryWithoutWipingRegionalTales()
        {
            var values = new Dictionary<string, int>();
            SeedThroughM08Notice(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            // Record permanent regional tales ST-02, ST-03, and ST-05.
            Assert.That(ST02RiverLanternsFacts.TryRecord(new ST02RiverLanternsRecord(
                ST02InspectionFlags.All, true, true,
                ST02TrialDisclosure.PublishToBoth,
                ST02BidMode.JointBidWithWrittenTerms,
                ST02LockRescueMethod.BraceHullStressPoint), read, write), Is.True);
            Assert.That(ST03BathhouseFacts.TryRecord(new ST03BathhouseRecord(
                ST03BathhouseChoice.ReopenWithSubsidizedHours,
                true, true, true, true), read, write), Is.True);
            Assert.That(ST05TwoDoorsFacts.TryRecordHome(new ST05HouseholdRecord(
                ST05BedResolution.ConvertBedToCourtyardBenches,
                ST05HomeArrangement.KeepTwoDoorsAndCourtyardTable,
                mutualTrialConsent: false), read, write), Is.True);
            Assert.That(ST05TwoDoorsFacts.TryRecordAssignment(
                ST05ChengAssignment.StayLocal, read, write), Is.True);

            // Seed downstream main story M09 -> M10 -> M11 -> M13 -> M14 -> M15 -> M16.
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
                M16WalkStops.None, M16DeliveredLettersFlags.ST02RiverLanterns, false,
                M16StandingSpot.PierSeaBreeze, true, M16RespectAgencyTone.NeutralMixedExperience),
                read, write), Is.True);

            // Resetting voyage-bound upstream chapters invalidates downstream main story readbacks.
            Assert.That(M09BerthTableFacts.TryResetForNewVoyage(write), Is.True);
            Assert.That(M13DepartureRiskFacts.TryResetForNewVoyage(write), Is.True);
            Assert.That(M15FinalRollCallFacts.TryResetForNewVoyage(write), Is.True);

            Assert.That(M10AssemblyFacts.TryRead(read, out M10AssemblyRecord? staleM10), Is.True);
            Assert.That(staleM10, Is.Null);
            Assert.That(M11ReverseRescueFacts.TryRead(read, out M11ReverseRescueRecord? staleM11), Is.True);
            Assert.That(staleM11, Is.Null);
            Assert.That(M14FinalPassageFacts.TryRead(read, out M14FinalPassageRecord? staleM14), Is.True);
            Assert.That(staleM14, Is.Null);
            Assert.That(M16EpilogueFacts.TryRead(read, out M16EpilogueRecord? staleM16), Is.True);
            Assert.That(staleM16, Is.Null);

            // Permanent regional tales survive voyage replay untouched.
            Assert.That(ST02RiverLanternsFacts.TryRead(read, out ST02RiverLanternsRecord? st02), Is.True);
            Assert.That(st02.HasValue, Is.True);
            Assert.That(ST03BathhouseFacts.TryRead(read, out ST03BathhouseRecord? st03), Is.True);
            Assert.That(st03.HasValue, Is.True);
            Assert.That(ST05TwoDoorsFacts.TryRead(read, out ST05TwoDoorsOutcome? st05), Is.True);
            Assert.That(st05.HasValue, Is.True);
            Assert.That(st05.Value.Assignment, Is.EqualTo(ST05ChengAssignment.StayLocal));

            // Only explicit TryResetForNewGame clears regional tales.
            Assert.That(ST02RiverLanternsFacts.TryResetForNewGame(write), Is.True);
            Assert.That(ST03BathhouseFacts.TryResetForNewGame(write), Is.True);
            Assert.That(ST05TwoDoorsFacts.TryResetForNewGame(write), Is.True);
            Assert.That(ST02RiverLanternsFacts.TryRead(read, out ST02RiverLanternsRecord? cleared02), Is.True);
            Assert.That(cleared02, Is.Null);
            Assert.That(ST03BathhouseFacts.TryRead(read, out ST03BathhouseRecord? cleared03), Is.True);
            Assert.That(cleared03, Is.Null);
            Assert.That(ST05TwoDoorsFacts.TryRead(read, out ST05TwoDoorsOutcome? cleared05), Is.True);
            Assert.That(cleared05, Is.Null);
        }
    }
}
