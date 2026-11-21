using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    /// <summary>
    /// Verifies that Act-3 Hosts (M10, M11, M12, M14, M16) bind open views to
    /// M05LandingFacts.ArrivalSequenceKey so stale UI panels left open across
    /// an identical replayed voyage can never commit into the new voyage.
    /// </summary>
    public sealed class M10ToM16VoyageIdentityTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        private static void SeedThroughM09(Dictionary<string, int> values, int arrivalSequence)
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
            values[M05LandingFacts.ArrivalSequenceKey] = arrivalSequence;

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

        // Simulate the actual downstream facts of a replayed landing.
        // M06 now signs the arrival identity: changing only the sequence
        // while retaining last voyage's M06/M07/M08 facts is not a fresh voyage.
        private static void ReseedRescueThroughNoticeForCurrentArrival(
            Dictionary<string, int> values)
        {
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            Assert.That(M08FirstWorldNoticeFacts.TryResetForNewVoyage(write), Is.True);
            Assert.That(M08WorkshopDepositFacts.TryResetForNewVoyage(write), Is.True);
            Assert.That(M07ExtractionFacts.TryResetForNewVoyage(write), Is.True);
            Assert.That(M06RescuePlanFacts.TryResetForNewVoyage(write), Is.True);

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
        public void StaleM12AndM16HostsCannotCommitAfterArrivalSequenceAdvances()
        {
            var root = new GameObject("Act3VoyageIdentityRoot");
            var values = new Dictionary<string, int>();
            SeedThroughM09(values, arrivalSequence: 1);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            Assert.That(M10AssemblyFacts.TryRecord(new M10AssemblyRecord(
                true, M10RosterAccuracy.VerifiedFirstPass, M10BattleReportSource.CarriedFromM02,
                M10HanLedgerUse.KeepConfidential, M10SupplyContractStatus.SignedFullCoverage,
                false, false, true), read, write), Is.True);
            Assert.That(M11ReverseRescueFacts.TryRecord(new M11ReverseRescueRecord(
                true, M11PassEntryMethod.LandGateNegotiation, true,
                M11ThirdSquadConscriptionStance.NotInPass,
                M11RescueProvider.FallbackPaidArtisans, 0, false,
                M11ShenDivisionPact.NoneShenAbsent), read, write), Is.True);

            try
            {
                // Open M12 on arrivalSequence == 1, then advance voyage to arrivalSequence == 2.
                using (var staleM12 = new M12BridgeTruthHost(
                    root.transform, () => true, () => true, () => false,
                    () => false, () => false, read, write))
                {
                    Assert.That(staleM12.TryOpen(), Is.True);
                    Assert.That(staleM12.Session.TryConfrontLuJun(
                        M12LuConfrontationReply.YouSaidThatLastTime), Is.True);
                    Assert.That(staleM12.Session.TryHearStatement(M12StatementFlags.JiangShenHeard), Is.True);
                    Assert.That(staleM12.Session.TryHearStatement(M12StatementFlags.HanAndYiLeiHeard), Is.True);
                    staleM12.ActiveView.Refresh();

                    // Simulate identical replayed voyage advancing arrival sequence.
                    values[M05LandingFacts.ArrivalSequenceKey] = 2;
                    staleM12.ActiveView.PublishTruthButton.onClick.Invoke();
                    Assert.That(staleM12.IsCompleted, Is.False);
                    Assert.That(staleM12.ActiveView, Is.Not.Null);
                }

                // M09's confirmed berth facts now bind to the actual arrival.
                // Replay the same observed second-batch outcome for voyage 2;
                // a stale signature from voyage 1 must not unlock Act 3.
                Assert.That(M09BerthTableFacts.TryRead(read, out M09BerthTableRecord? staleBerths),
                    Is.True);
                Assert.That(staleBerths, Is.Null);
                Assert.That(M09BerthTableFacts.TryResetForNewVoyage(write), Is.True);
                ReseedRescueThroughNoticeForCurrentArrival(values);
                Assert.That(M09BerthTableFacts.TryRecord(new M09BerthTableRecord(
                    20, 23, M09BerthExchangeFlags.HanQiaoCivilianVoyage,
                    M09RuiyaOutcome.Rescued, false, false), read, write), Is.True);

                // Fresh M12 host opened on arrivalSequence == 2 commits cleanly.
                using (var freshM12 = new M12BridgeTruthHost(
                    root.transform, () => true, () => true, () => false,
                    () => false, () => false, read, write))
                {
                    Assert.That(freshM12.TryOpen(), Is.True);
                    Assert.That(freshM12.Session.TryConfrontLuJun(
                        M12LuConfrontationReply.YouSaidThatLastTime), Is.True);
                    Assert.That(freshM12.Session.TryHearStatement(M12StatementFlags.JiangShenHeard), Is.True);
                    Assert.That(freshM12.Session.TryHearStatement(M12StatementFlags.HanAndYiLeiHeard), Is.True);
                    freshM12.ActiveView.Refresh();
                    freshM12.ActiveView.PublishTruthButton.onClick.Invoke();
                    Assert.That(freshM12.IsCompleted, Is.True);
                }

                // Seed M15 on arrivalSequence == 2, open M16, then advance to arrivalSequence == 3.
                Assert.That(M15FinalRollCallFacts.TryRecord(new M15FinalRollCallRecord(
                    M15RollCallReader.Protagonist, M15FinalIdentityResponse.PublicTrueName,
                    M15PensionClosure.StoppedAndReconciled, M15SteleInscription.KeepOriginal,
                    0, false), read, write), Is.True);

                using (var staleM16 = new M16EpilogueHost(
                    root.transform, () => true, () => true,
                    () => M16RespectAgencyTone.RespectedOthersChoices, read, write))
                {
                    Assert.That(staleM16.TryOpen(), Is.True);
                    staleM16.ActiveView.ProceedToDeskButton.onClick.Invoke();
                    staleM16.ActiveView.StepOutsideButton.onClick.Invoke();

                    values[M05LandingFacts.ArrivalSequenceKey] = 3;
                    staleM16.ActiveView.PierButton.onClick.Invoke();
                    Assert.That(staleM16.IsCompleted, Is.False);
                    Assert.That(staleM16.ActiveView, Is.Not.Null);
                }

                // The third replay also requires an M09 manifest signed for
                // its own arrival. Retain the same factual scenario while
                // exercising the M16 host's independent stale-view guard.
                Assert.That(M09BerthTableFacts.TryResetForNewVoyage(write), Is.True);
                ReseedRescueThroughNoticeForCurrentArrival(values);
                Assert.That(M09BerthTableFacts.TryRecord(new M09BerthTableRecord(
                    20, 23, M09BerthExchangeFlags.HanQiaoCivilianVoyage,
                    M09RuiyaOutcome.Rescued, false, false), read, write), Is.True);

                using (var freshM16 = new M16EpilogueHost(
                    root.transform, () => true, () => true,
                    () => M16RespectAgencyTone.RespectedOthersChoices, read, write))
                {
                    Assert.That(freshM16.TryOpen(), Is.True);
                    freshM16.ActiveView.ProceedToDeskButton.onClick.Invoke();
                    freshM16.ActiveView.StepOutsideButton.onClick.Invoke();
                    freshM16.ActiveView.PierButton.onClick.Invoke();
                    Assert.That(freshM16.IsCompleted, Is.True);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
