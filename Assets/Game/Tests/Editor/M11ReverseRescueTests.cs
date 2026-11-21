using System;
using System.Collections.Generic;
using GamingIsLove.Makinom;
using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    public sealed class M11ReverseRescueTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        private static void SeedThroughM10Assembly(
            Dictionary<string, int> values,
            bool seedRC05EnlistedUnderLuJun = false)
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

            if (seedRC05EnlistedUnderLuJun)
            {
                var winch = new M08FirstWinchOrderSession();
                Assert.That(winch.TryAcquirePart(M08WinchPart.ScrapGear), Is.True);
                Assert.That(winch.TryAcquirePart(M08WinchPart.DockCable), Is.True);
                Assert.That(winch.TryAssemble(), Is.True);
                Assert.That(winch.TryTightenCable(), Is.True);
                Assert.That(winch.TryTestLift(), Is.True);
                Assert.That(M08WorkshopDepositFacts.TryConfirm(winch, read, write), Is.True);
                Assert.That(M08FirstWorldNoticeFacts.TryRecord(true, read, write), Is.True);
                Assert.That(RC05ThirdSquadFacts.TryRecord(new RC05ThirdSquadRecord(
                    trialDeliveryVerified: true, RC05Resolution.EnlistUnderLuJun,
                    skirmishOccurred: false, realZhengYuanVerified: false,
                    oldWomanStoneMentioned: false), read, write), Is.True);
            }

            Assert.That(M09BerthTableFacts.TryRecord(new M09BerthTableRecord(
                totalBerths: 20,
                pendingNames: 23,
                M09BerthExchangeFlags.HanQiaoCivilianVoyage,
                M09RuiyaOutcome.Rescued,
                qiDebtDisclosedHere: false,
                shenReconciled: false), read, write), Is.True);

            Assert.That(M10AssemblyFacts.TryRecord(new M10AssemblyRecord(
                worldNoticeThreeObserved: true,
                M10RosterAccuracy.VerifiedFirstPass,
                M10BattleReportSource.CarriedFromM02,
                M10HanLedgerUse.PublishDraft,
                M10SupplyContractStatus.SignedFullCoverage,
                leverageWeakenedByRC02: false,
                coversRC05MountainFamilies: false,
                bellTowerSeizureHeard: true), read, write), Is.True);
        }

        [Test]
        public void RescueRequiresM10ThreeFogCallsShoreBoatmanWhenOulinAbsentAndRC05Stance()
        {
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            Assert.That(M11ReverseRescueSession.TryCreateFromSavedFacts(
                read, shenLiAccompanying: true, oulinParticipating: false, out var blocked), Is.False);
            Assert.That(blocked, Is.Null);

            SeedThroughM10Assembly(values, seedRC05EnlistedUnderLuJun: true);
            Assert.That(M11ReverseRescueSession.TryCreateFromSavedFacts(
                read, shenLiAccompanying: true, oulinParticipating: false, out var session), Is.True);

            Assert.That(session.TryObserveThreeBellsAndEnter(M11PassEntryMethod.LandGateNegotiation), Is.True);
            // RC-05 enlisted under Lu Jun requires an explicit stance, not NotInPass.
            Assert.That(session.TryObserveConscriptionAndCoverReturnees(
                M11ThirdSquadConscriptionStance.NotInPass), Is.False);
            Assert.That(session.TryObserveConscriptionAndCoverReturnees(
                M11ThirdSquadConscriptionStance.RefusedForcedConscription), Is.True);

            // Cannot answer before hearing all 3 fog calls, and cannot use fallback when Shen Li is present.
            Assert.That(session.TryExtractViaFallbackArtisans(), Is.False);
            Assert.That(session.TryAnswerPresentForShenLiExtraction(), Is.False);
            Assert.That(session.TryHearShenLiFogCall(), Is.True);
            Assert.That(session.TryHearShenLiFogCall(), Is.True);
            Assert.That(session.TryAnswerPresentForShenLiExtraction(), Is.False);
            Assert.That(session.TryHearShenLiFogCall(), Is.True);
            Assert.That(session.TryHearShenLiFogCall(), Is.False);
            Assert.That(session.TryAnswerPresentForShenLiExtraction(), Is.True);

            Assert.That(session.TryCommit(read, write), Is.False);
            Assert.That(session.TryChooseDivisionPact(M11ShenDivisionPact.MutualConsultAndDivision), Is.True);
            Assert.That(session.TryCommit(read, write), Is.True);

            Assert.That(session.IsCompleted, Is.True);
            Assert.That(session.CompletedRecord.Value.ShoreBoatmanTookHelm, Is.True);
            Assert.That(session.CompletedRecord.Value.OulinJoinedExtraction, Is.False);
            Assert.That(session.CompletedRecord.Value.ImprovesShenRelation, Is.True);
        }

        [Test]
        public void FallbackArtisansRouteWhenShenLiAbsentNeverPlaysHerEchoOrReconciliation()
        {
            var values = new Dictionary<string, int>();
            SeedThroughM10Assembly(values, seedRC05EnlistedUnderLuJun: false);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            Assert.That(M11ReverseRescueSession.TryCreateFromSavedFacts(
                read, shenLiAccompanying: false, oulinParticipating: true, out var session), Is.True);
            Assert.That(session.TryObserveThreeBellsAndEnter(M11PassEntryMethod.ReliefSupplyPass), Is.True);
            Assert.That(session.TryObserveConscriptionAndCoverReturnees(
                M11ThirdSquadConscriptionStance.RefusedForcedConscription), Is.False);
            Assert.That(session.TryObserveConscriptionAndCoverReturnees(), Is.True);

            Assert.That(session.TryHearShenLiFogCall(), Is.False);
            Assert.That(session.TryExtractViaFallbackArtisans(), Is.True);
            Assert.That(session.TryChooseDivisionPact(M11ShenDivisionPact.MutualConsultAndDivision), Is.False);
            Assert.That(session.TryCommit(read, write), Is.True);

            Assert.That(M11ReverseRescueFacts.TryRead(read, out M11ReverseRescueRecord? saved), Is.True);
            Assert.That(saved.HasValue, Is.True);
            Assert.That(saved.Value.UsedHigherCostFallback, Is.True);
            Assert.That(saved.Value.EchoCallsHeard, Is.Zero);
            Assert.That(saved.Value.OulinJoinedExtraction, Is.False);
            Assert.That(saved.Value.DivisionPact, Is.EqualTo(M11ShenDivisionPact.NoneShenAbsent));
            Assert.That(saved.Value.ImprovesShenRelation, Is.False);
        }

        [Test]
        public void CommitRecoversFromInterruptedWriteAndLostAcknowledgementAfterConfirm()
        {
            var values = new Dictionary<string, int>();
            SeedThroughM10Assembly(values);
            Func<string, int> read = Reader(values);
            int preCommitFails = 0;
            int confirmCommits = 0;
            Action<string, int> unstable = (k, v) =>
            {
                if (k == M11ReverseRescueFacts.RescueProviderKey && preCommitFails++ == 0)
                    throw new InvalidOperationException("Interrupted before confirm");
                values[k] = v;
                if (k == M11ReverseRescueFacts.ConfirmedKey && v == 1 && ++confirmCommits == 1)
                    throw new InvalidOperationException("Lost ack after confirm");
            };

            Assert.That(M11ReverseRescueSession.TryCreateFromSavedFacts(
                read, shenLiAccompanying: true, oulinParticipating: true, out var session), Is.True);
            Assert.That(session.TryObserveThreeBellsAndEnter(M11PassEntryMethod.MerchantCaravanSurvey), Is.True);
            Assert.That(session.TryObserveConscriptionAndCoverReturnees(), Is.True);
            Assert.That(session.TryHearShenLiFogCall(), Is.True);
            Assert.That(session.TryHearShenLiFogCall(), Is.True);
            Assert.That(session.TryHearShenLiFogCall(), Is.True);
            Assert.That(session.TryAnswerPresentForShenLiExtraction(), Is.True);
            Assert.That(session.TryChooseDivisionPact(M11ShenDivisionPact.CallItEvenNextTime), Is.True);

            Assert.Throws<InvalidOperationException>(() => session.TryCommit(read, unstable));
            Assert.That(session.IsCompleted, Is.False);

            Assert.Throws<InvalidOperationException>(() => session.TryCommit(read, unstable));
            Assert.That(confirmCommits, Is.EqualTo(1));

            Assert.That(session.TryCommit(read, unstable), Is.True);
            Assert.That(confirmCommits, Is.EqualTo(1));
            Assert.That(session.IsCompleted, Is.True);
            Assert.That(session.CompletedRecord.Value.OulinJoinedExtraction, Is.True);
        }

        [Test]
        public void ForgedFallbackPactCorruptDataAndUpstreamResetFailClosed()
        {
            var values = new Dictionary<string, int>();
            SeedThroughM10Assembly(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            // Forging Shen Li's reconciliation pact on fallback route must be rejected.
            Assert.That(M11ReverseRescueFacts.TryRecord(new M11ReverseRescueRecord(
                threeBellsObserved: true,
                M11PassEntryMethod.LandGateNegotiation,
                conscriptionObserved: true,
                M11ThirdSquadConscriptionStance.NotInPass,
                M11RescueProvider.FallbackPaidArtisans,
                echoCallsHeard: 0,
                oulinJoinedExtraction: false,
                M11ShenDivisionPact.MutualConsultAndDivision), read, write), Is.False);

            Assert.That(M11ReverseRescueFacts.TryRecord(new M11ReverseRescueRecord(
                threeBellsObserved: true,
                M11PassEntryMethod.LandGateNegotiation,
                conscriptionObserved: true,
                M11ThirdSquadConscriptionStance.NotInPass,
                M11RescueProvider.FallbackPaidArtisans,
                echoCallsHeard: 0,
                oulinJoinedExtraction: false,
                M11ShenDivisionPact.NoneShenAbsent), read, write), Is.True);

            values[M11ReverseRescueFacts.RescueProviderKey] = 99;
            Assert.That(M11ReverseRescueFacts.TryRead(read, out M11ReverseRescueRecord? corrupt), Is.True);
            Assert.That(corrupt, Is.Null);

            values[M11ReverseRescueFacts.RescueProviderKey] = (int)M11RescueProvider.FallbackPaidArtisans;
            Assert.That(M10AssemblyFacts.TryResetForNewVoyage(write), Is.True);
            Assert.That(M11ReverseRescueFacts.TryRead(read, out M11ReverseRescueRecord? stale), Is.True);
            Assert.That(stale, Is.Null);

            Assert.That(M11ReverseRescueFacts.TryResetForNewVoyage(write), Is.True);
            Assert.That(values[M11ReverseRescueFacts.ConfirmedKey], Is.Zero);
            Assert.That(M11ReverseRescueFacts.TryRead(null, out _), Is.False);
            Assert.That(M11ReverseRescueFacts.TryResetForNewVoyage(null), Is.False);
            if (!Maki.Initialized || Maki.Game?.Variables == null)
                Assert.That(M11ReverseRescueFacts.TryRead(out _), Is.False);
        }
    }
}
