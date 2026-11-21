using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    public sealed class Act3SessionOrphanedOpenGateTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
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
        public void M10M11M12AndM14SessionsRejectOpeningOverOrphanedDownstreamFactsUntilReset()
        {
            var values = new Dictionary<string, int>();
            SeedThroughM09(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            // M10 blocked by orphaned M16 fact until reset.
            values[M16EpilogueFacts.ConfirmedKey] = 1;
            Assert.That(M10AssemblySession.TryCreateFromSavedFacts(read, out var blockedM10), Is.False);
            Assert.That(blockedM10, Is.Null);
            Assert.That(M16EpilogueFacts.TryResetForNewVoyage(write), Is.True);
            Assert.That(M10AssemblySession.TryCreateFromSavedFacts(read, out var m10Session), Is.True);
            Assert.That(m10Session, Is.Not.Null);

            Assert.That(M10AssemblyFacts.TryRecord(new M10AssemblyRecord(
                true, M10RosterAccuracy.VerifiedFirstPass,
                M10BattleReportSource.CarriedFromM02,
                M10HanLedgerUse.KeepConfidential,
                M10SupplyContractStatus.SignedFullCoverage,
                false, false, true), read, write), Is.True);

            // M11 blocked by orphaned M12 fact until reset.
            values[M12BridgeTruthFacts.DisclosureKey] = (int)M12TruthDisclosure.PublishTruth;
            Assert.That(M11ReverseRescueSession.TryCreateFromSavedFacts(
                read, shenLiAccompanying: false, oulinParticipating: false, out var blockedM11), Is.False);
            Assert.That(blockedM11, Is.Null);
            Assert.That(M12BridgeTruthFacts.TryResetForNewVoyage(write), Is.True);
            Assert.That(M11ReverseRescueSession.TryCreateFromSavedFacts(
                read, shenLiAccompanying: false, oulinParticipating: false, out var m11Session), Is.True);
            Assert.That(m11Session, Is.Not.Null);

            Assert.That(M11ReverseRescueFacts.TryRecord(new M11ReverseRescueRecord(
                true, M11PassEntryMethod.LandGateNegotiation, true,
                M11ThirdSquadConscriptionStance.NotInPass,
                M11RescueProvider.FallbackPaidArtisans, 0, false,
                M11ShenDivisionPact.NoneShenAbsent), read, write), Is.True);

            // M12 blocked by orphaned M13 fact until reset.
            values[M13DepartureRiskFacts.RouteKey] = (int)M13ApproachRoute.RouteASupervisedCivilian;
            Assert.That(M12BridgeTruthSession.TryCreateFromSavedFacts(
                read, false, false, false, false, out var blockedM12), Is.False);
            Assert.That(blockedM12, Is.Null);
            Assert.That(M13DepartureRiskFacts.TryResetForNewVoyage(write), Is.True);
            Assert.That(M12BridgeTruthSession.TryCreateFromSavedFacts(
                read, false, false, false, false, out var m12Session), Is.True);
            Assert.That(m12Session, Is.Not.Null);

            Assert.That(M13DepartureRiskFacts.TryRecordDeparture(new M13DepartureRecord(
                M13ApproachRoute.RouteASupervisedCivilian, true, false, false, false),
                read, write), Is.True);

            // M14 blocked by orphaned M15 fact until reset.
            values[M15FinalRollCallFacts.IdentityResponseKey] =
                (int)M15FinalIdentityResponse.PublicTrueName;
            Assert.That(M14FinalPassageSession.TryCreateFromSavedFacts(read, out var blockedM14), Is.False);
            Assert.That(blockedM14, Is.Null);
            Assert.That(M15FinalRollCallFacts.TryResetForNewVoyage(write), Is.True);
            Assert.That(M14FinalPassageSession.TryCreateFromSavedFacts(read, out var m14Session), Is.True);
            Assert.That(m14Session, Is.Not.Null);
        }
    }
}
