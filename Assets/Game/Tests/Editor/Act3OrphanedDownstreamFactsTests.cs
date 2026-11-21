using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    public sealed class Act3OrphanedDownstreamFactsTests
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
        public void M10ThroughM12RejectRecordingOverOrphanedDownstreamAct3FactsAndRecoverAfterReset()
        {
            var values = new Dictionary<string, int>();
            SeedThroughM09(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            var m10 = new M10AssemblyRecord(
                true, M10RosterAccuracy.VerifiedFirstPass,
                M10BattleReportSource.CarriedFromM02,
                M10HanLedgerUse.KeepConfidential,
                M10SupplyContractStatus.SignedFullCoverage,
                false, false, true);
            var m11 = new M11ReverseRescueRecord(
                true, M11PassEntryMethod.LandGateNegotiation, true,
                M11ThirdSquadConscriptionStance.NotInPass,
                M11RescueProvider.FallbackPaidArtisans, 0, false,
                M11ShenDivisionPact.NoneShenAbsent);
            var m12 = new M12BridgeTruthRecord(
                false, false, M12LuConfrontationReply.SomeWantNoMoreWar,
                false, M12StatementFlags.RequiredBase, false,
                false, M12TruthDisclosure.SealTruth);

            // Orphaned M16 key must transitively block M10 recording.
            values[M16EpilogueFacts.StandingSpotKey] = (int)M16StandingSpot.PierSeaBreeze;
            Assert.That(M10AssemblyFacts.HasOrphanedDownstreamFacts(read), Is.True);
            Assert.That(M10AssemblyFacts.CanRecord(m10, read), Is.False);
            Assert.That(M10AssemblyFacts.TryRecord(m10, read, write), Is.False);
            Assert.That(M16EpilogueFacts.TryResetForNewVoyage(write), Is.True);
            Assert.That(M10AssemblyFacts.TryRecord(m10, read, write), Is.True);

            // Orphaned M12 key must block M11 recording until cleared.
            values[M12BridgeTruthFacts.DisclosureKey] = (int)M12TruthDisclosure.PublishTruth;
            Assert.That(M11ReverseRescueFacts.HasOrphanedDownstreamFacts(read), Is.True);
            Assert.That(M11ReverseRescueFacts.CanRecord(m11, read), Is.False);
            Assert.That(M11ReverseRescueFacts.TryRecord(m11, read, write), Is.False);
            Assert.That(M12BridgeTruthFacts.TryResetForNewVoyage(write), Is.True);
            Assert.That(M11ReverseRescueFacts.TryRecord(m11, read, write), Is.True);

            // Orphaned M13 key must block M12 recording until cleared.
            values[M13DepartureRiskFacts.RouteKey] = (int)M13ApproachRoute.RouteASupervisedCivilian;
            Assert.That(M12BridgeTruthFacts.HasOrphanedDownstreamFacts(read), Is.True);
            Assert.That(M12BridgeTruthFacts.CanRecord(m12, read), Is.False);
            Assert.That(M12BridgeTruthFacts.TryRecord(m12, read, write), Is.False);
            Assert.That(M13DepartureRiskFacts.TryResetForNewVoyage(write), Is.True);
            Assert.That(M12BridgeTruthFacts.TryRecord(m12, read, write), Is.True);
        }

        [Test]
        public void M13ThroughM15RejectRecordingOverOrphanedDownstreamFactsAndRecoverAfterReset()
        {
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            var m13 = new M13DepartureRecord(
                M13ApproachRoute.RouteASupervisedCivilian, true, false, false, false);
            var m14 = new M14FinalPassageRecord(
                M13ApproachRoute.RouteASupervisedCivilian,
                M14PassageOutcome.RouteASupervisedCivilianEstablished,
                true, 12, 2, 1, M14BorderForceAction.NotRouteB,
                M14LuJunDisposition.RelievedOfCommand, M14AyuanOutcome.Safe, false);
            var m15 = new M15FinalRollCallRecord(
                M15RollCallReader.Protagonist,
                M15FinalIdentityResponse.PublicTrueName,
                M15PensionClosure.StoppedAndReconciled,
                M15SteleInscription.CarveConsentedVerifiedNames,
                12, false);

            // Orphaned M14 AyuanOutcomeKey must block M13 departure recording.
            values[M13DepartureRiskFacts.AyuanOutcomeKey] = (int)M14AyuanOutcome.Safe;
            Assert.That(M13DepartureRiskFacts.HasOrphanedDownstreamFacts(read), Is.True);
            Assert.That(M13DepartureRiskFacts.CanRecordDeparture(m13, read), Is.False);
            Assert.That(M13DepartureRiskFacts.TryRecordDeparture(m13, read, write), Is.False);
            Assert.That(M13DepartureRiskFacts.TryResetForNewVoyage(write), Is.True);
            Assert.That(M13DepartureRiskFacts.TryRecordDeparture(m13, read, write), Is.True);

            // Orphaned M15 key must block M14 passage recording until cleared.
            values[M15FinalRollCallFacts.IdentityResponseKey] =
                (int)M15FinalIdentityResponse.PublicTrueName;
            Assert.That(M14FinalPassageFacts.HasOrphanedDownstreamFacts(read), Is.True);
            Assert.That(M14FinalPassageFacts.CanRecord(m14, read), Is.False);
            Assert.That(M14FinalPassageFacts.TryRecord(m14, read, write), Is.False);
            Assert.That(M15FinalRollCallFacts.TryResetForNewVoyage(write), Is.True);
            Assert.That(M14FinalPassageFacts.TryRecord(m14, read, write), Is.True);

            // Orphaned M16 key must block M15 roll-call recording until cleared.
            values[M16EpilogueFacts.ConfirmedKey] = 1;
            Assert.That(M15FinalRollCallFacts.HasOrphanedDownstreamFacts(read), Is.True);
            Assert.That(M15FinalRollCallFacts.CanRecord(m15, read), Is.False);
            Assert.That(M15FinalRollCallFacts.TryRecord(m15, read, write), Is.False);
            Assert.That(M16EpilogueFacts.TryResetForNewVoyage(write), Is.True);
            Assert.That(M15FinalRollCallFacts.TryRecord(m15, read, write), Is.True);
        }
    }
}
