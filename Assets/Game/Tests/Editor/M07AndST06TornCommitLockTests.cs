using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    public sealed class M07AndST06TornCommitLockTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        private static void SeedThroughM06(Dictionary<string, int> values)
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
        }

        [Test]
        public void M07InterruptedExtractionLocksOutCompetingSurvivorChoicesAndAllowsIdenticalRetry()
        {
            var values = new Dictionary<string, int>();
            SeedThroughM06(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            var chosen = new M07ExtractionRecord(
                M07ExecutedRoute.OfficialHandover,
                M07PumpShutdownState.KeptRunning,
                M07RouteCostFlags.HarborSupplyDebt,
                boardedFirstBatchCount: 12,
                lowerLevelEvacuatedCount: 0,
                M07RusFate.Arrested, M07LedgerState.Preserved,
                M07LiangChoice.PersuadedToLeave,
                M07YidaChoice.HandToInspector,
                taoJiRescued: true);
            var competing = new M07ExtractionRecord(
                M07ExecutedRoute.OfficialHandover,
                M07PumpShutdownState.KeptRunning,
                M07RouteCostFlags.HarborSupplyDebt,
                boardedFirstBatchCount: 12,
                lowerLevelEvacuatedCount: 0,
                M07RusFate.Fled, M07LedgerState.PartiallyBurned,
                M07LiangChoice.ForcedToBoard,
                M07YidaChoice.LeaveBehind,
                taoJiRescued: false);

            Assert.Throws<InvalidOperationException>(() => M07ExtractionFacts.TryRecord(
                chosen, read, (k, v) =>
                {
                    if (k == M07ExtractionFacts.ConfirmedKey)
                        throw new InvalidOperationException("Interrupted before M07 confirm");
                    values[k] = v;
                }));

            Assert.That(M07ExtractionFacts.CanRecord(competing, read), Is.False);
            Assert.That(M07ExtractionFacts.TryRecord(competing, read, write), Is.False);

            Assert.That(M07ExtractionFacts.TryRecord(chosen, read, write), Is.True);
            Assert.That(M07ExtractionFacts.TryRead(read, out M07ExtractionRecord? saved), Is.True);
            Assert.That(saved.HasValue, Is.True);
            Assert.That(saved.Value.LiangChoice, Is.EqualTo(M07LiangChoice.PersuadedToLeave));
            Assert.That(saved.Value.TaoJiRescued, Is.True);
        }

        [Test]
        public void ST06InterruptedHomecomingLocksOutCompetingResolutionAndAllowsIdenticalRetry()
        {
            var values = new Dictionary<string, int>();
            SeedThroughM06(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

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

            var chosen = new ST06HomecomingRecord(
                ST06ChairResolution.RefundAndRebuild,
                coverUpRemediated: false,
                welcomeDinnerHeld: true,
                taoJiTraveledFar: true,
                ST06LetterStatus.Delivered,
                marketTicketEnclosed: true);
            var competing = new ST06HomecomingRecord(
                ST06ChairResolution.CoverUp,
                coverUpRemediated: false,
                welcomeDinnerHeld: true,
                taoJiTraveledFar: false,
                ST06LetterStatus.NotSent,
                marketTicketEnclosed: false);

            Assert.Throws<InvalidOperationException>(() => ST06TaoJiHomecomingFacts.TryRecord(
                chosen, read, (k, v) =>
                {
                    if (k == ST06TaoJiHomecomingFacts.ConfirmedKey)
                        throw new InvalidOperationException("Interrupted before ST06 confirm");
                    values[k] = v;
                }));

            Assert.That(ST06TaoJiHomecomingFacts.CanRecord(competing, read), Is.False);
            Assert.That(ST06TaoJiHomecomingFacts.TryRecord(competing, read, write), Is.False);

            Assert.That(ST06TaoJiHomecomingFacts.TryRecord(chosen, read, write), Is.True);
            Assert.That(ST06TaoJiHomecomingFacts.TryRead(read, out ST06HomecomingRecord? saved), Is.True);
            Assert.That(saved.HasValue, Is.True);
            Assert.That(saved.Value.ChairResolution,
                Is.EqualTo(ST06ChairResolution.RefundAndRebuild));
            Assert.That(saved.Value.MarketTicketEnclosed, Is.True);
        }
    }
}
