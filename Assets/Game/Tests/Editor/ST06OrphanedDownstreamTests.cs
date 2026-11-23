using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    public sealed class ST06OrphanedDownstreamTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        private static void SeedM07(Dictionary<string, int> values)
        {
            Func<string, int> read = Reader(values);
            Action<string, int> write = (key, value) => values[key] = value;

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
                    M06RescuePlan.OfficialHandover,
                    true,
                    M06LetterDeliveryOutcome.NotCarried,
                    true),
                read, write), Is.True);
            Assert.That(M07ExtractionFacts.TryRecord(
                new M07ExtractionRecord(
                    M07ExecutedRoute.OfficialHandover,
                    M07PumpShutdownState.KeptRunning,
                    M07RouteCostFlags.HarborSupplyDebt,
                    boardedFirstBatchCount: 300,
                    lowerLevelEvacuatedCount: 0,
                    M07RusFate.Arrested,
                    M07LedgerState.Preserved,
                    M07LiangChoice.StayOnIsland,
                    M07YidaChoice.HandToInspector,
                    taoJiRescued: true),
                read, write), Is.True);
        }

        [Test]
        public void TryRecordAndSessionFailClosedWhenM15OrM16FactsExistAndRecoverOnceCleared()
        {
            var values = new Dictionary<string, int>();
            SeedM07(values);
            Func<string, int> read = Reader(values);
            int st06Writes = 0;
            Action<string, int> write = (key, value) =>
            {
                if (key.StartsWith("rpg.return.st06.", StringComparison.Ordinal))
                    st06Writes++;
                values[key] = value;
            };

            var record = new ST06HomecomingRecord(
                ST06ChairResolution.RefundAndRebuild,
                coverUpRemediated: false,
                welcomeDinnerHeld: true,
                taoJiTraveledFar: true,
                ST06LetterStatus.Delivered,
                marketTicketEnclosed: true);

            Assert.That(ST06TaoJiHomecomingSession.TryCreateFromSavedFacts(read, out var session), Is.True);
            session.ResolveBrokenChair(ST06ChairResolution.RefundAndRebuild);
            session.RecordWelcomeDinner();
            session.SetPostReturnLetterProgress(true, ST06LetterStatus.Delivered, true);

            Assert.That(ST06TaoJiHomecomingFacts.HasOrphanedDownstreamFacts(null), Is.True);
            Assert.That(ST06TaoJiHomecomingFacts.HasOrphanedDownstreamFacts(read), Is.False);
            Assert.That(ST06TaoJiHomecomingFacts.CanRecord(record, read), Is.True);

            string[] endgameKeys =
            {
                M15FinalRollCallFacts.ConfirmedKey,
                M16EpilogueFacts.ConfirmedKey
            };

            foreach (string key in endgameKeys)
            {
                values[key] = 1;
                Assert.That(ST06TaoJiHomecomingFacts.HasOrphanedDownstreamFacts(read), Is.True, key);
                Assert.That(ST06TaoJiHomecomingFacts.CanRecord(record, read), Is.False, key);
                Assert.That(ST06TaoJiHomecomingFacts.TryRecord(record, read, write), Is.False, key);
                Assert.That(session.TrySettle(read, write), Is.False, key);
                Assert.That(session.IsSettled, Is.False, key);
                Assert.That(st06Writes, Is.Zero, key);
                values[key] = 0;
            }

            Assert.That(session.TrySettle(read, write), Is.True);
            Assert.That(session.IsSettled, Is.True);
            Assert.That(ST06TaoJiHomecomingFacts.TryRead(read, out ST06HomecomingRecord? saved), Is.True);
            Assert.That(saved.HasValue, Is.True);
            Assert.That(saved.Value.ShouldPlaceLetterOnM16Desk, Is.True);
            Assert.That(saved.Value.MarketTicketEnclosed, Is.True);
        }
    }
}
