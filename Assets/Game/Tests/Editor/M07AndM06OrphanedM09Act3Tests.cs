using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    public sealed class M07AndM06OrphanedM09Act3Tests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        private static void SeedThroughM05(Dictionary<string, int> values)
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
        }

        [Test]
        public void M07ExtractionRejectsOrphanedM09AndTransitiveAct3FactsUntilReset()
        {
            var values = new Dictionary<string, int>();
            SeedThroughM05(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            Assert.That(M09BerthTableFacts.HasAnyPersistedFacts(null), Is.True);

            Assert.That(M06RescuePlanFacts.TryRecord(
                new M06ReunionRecord(
                    M06RescuePlan.OfficialHandover, true,
                    M06LetterDeliveryOutcome.NotCarried, true), read, write), Is.True);

            var m07 = new M07ExtractionRecord(
                M07ExecutedRoute.OfficialHandover,
                M07PumpShutdownState.KeptRunning,
                M07RouteCostFlags.HarborSupplyDebt,
                boardedFirstBatchCount: 10,
                lowerLevelEvacuatedCount: 0,
                M07RusFate.Arrested, M07LedgerState.Preserved,
                M07LiangChoice.StayOnIsland,
                M07YidaChoice.HandToInspector,
                taoJiRescued: true);

            foreach (string m09Key in new[]
            {
                M09BerthTableFacts.ConfirmedKey,
                M09BerthTableFacts.PendingSignatureKey,
                M09BerthTableFacts.TotalBerthsKey,
                M09BerthTableFacts.PendingNamesKey,
                M09BerthTableFacts.ExchangesKey,
                M09BerthTableFacts.RuiyaOutcomeKey,
                M09BerthTableFacts.QiDebtDisclosedKey,
                M09BerthTableFacts.ShenReconciledKey,
                M09BerthTableFacts.ShipwrightFeePaidKey
            })
            {
                values[m09Key] = 1;
                Assert.That(M09BerthTableFacts.HasAnyPersistedFacts(read), Is.True, m09Key);
                Assert.That(M07ExtractionFacts.HasOrphanedM08SuccessorFacts(read), Is.True, m09Key);
                Assert.That(M07ExtractionFacts.CanRecord(m07, read), Is.False, m09Key);
                Assert.That(M07ExtractionFacts.TryRecord(m07, read, write), Is.False, m09Key);
                Assert.That(M09BerthTableFacts.TryResetForNewVoyage(write), Is.True, m09Key);
            }

            // Transitive Act-3 keys (M10, M16) must also block M07.
            values[M10AssemblyFacts.HanLedgerUseKey] = (int)M10HanLedgerUse.PublishDraft;
            Assert.That(M07ExtractionFacts.HasOrphanedM08SuccessorFacts(read), Is.True);
            Assert.That(M07ExtractionFacts.TryRecord(m07, read, write), Is.False);
            Assert.That(M10AssemblyFacts.TryResetForNewVoyage(write), Is.True);

            values[M16EpilogueFacts.ConfirmedKey] = 1;
            Assert.That(M07ExtractionFacts.HasOrphanedM08SuccessorFacts(read), Is.True);
            Assert.That(M07ExtractionFacts.TryRecord(m07, read, write), Is.False);
            Assert.That(M16EpilogueFacts.TryResetForNewVoyage(write), Is.True);

            Assert.That(M07ExtractionFacts.HasOrphanedM08SuccessorFacts(read), Is.False);
            Assert.That(M07ExtractionFacts.TryRecord(m07, read, write), Is.True);
        }

        [Test]
        public void M06ReunionAndFactsTransitivelyRejectOrphanedM09AndM16FactsUntilReset()
        {
            var values = new Dictionary<string, int>();
            SeedThroughM05(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            var m06 = new M06ReunionRecord(
                M06RescuePlan.OfficialHandover, true,
                M06LetterDeliveryOutcome.NotCarried, true);

            values[M09BerthTableFacts.RuiyaOutcomeKey] = (int)M09RuiyaOutcome.Rescued;
            Assert.That(M06RescuePlanFacts.HasOrphanedDownstreamFacts(read), Is.True);
            Assert.That(M06RescuePlanFacts.TryRecord(m06, read, write), Is.False);
            Assert.That(M06PumpRoomReunionSession.TryCreateFromSavedFacts(read, out _), Is.False);
            Assert.That(M09BerthTableFacts.TryResetForNewVoyage(write), Is.True);

            values[M16EpilogueFacts.StandingSpotKey] = (int)M16StandingSpot.PierSeaBreeze;
            Assert.That(M06RescuePlanFacts.HasOrphanedDownstreamFacts(read), Is.True);
            Assert.That(M06RescuePlanFacts.TryRecord(m06, read, write), Is.False);
            Assert.That(M06PumpRoomReunionSession.TryCreateFromSavedFacts(read, out _), Is.False);
            Assert.That(M16EpilogueFacts.TryResetForNewVoyage(write), Is.True);

            Assert.That(M06PumpRoomReunionSession.TryCreateFromSavedFacts(read, out var session), Is.True);
            Assert.That(session, Is.Not.Null);
            Assert.That(M06RescuePlanFacts.TryRecord(m06, read, write), Is.True);
        }
    }
}
