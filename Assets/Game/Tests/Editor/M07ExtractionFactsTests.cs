using System;
using System.Collections.Generic;
using GamingIsLove.Makinom;
using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    public sealed class M07ExtractionFactsTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        private static void SeedVerifiedM06Plan(
            Dictionary<string, int> values,
            M06RescuePlan plan)
        {
            Action<string, int> write = (key, value) => values[key] = value;
            Func<string, int> read = Reader(values);

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
                    plan,
                    reportedUsableCapacity: true,
                    M06LetterDeliveryOutcome.NotCarried,
                    retaliationHistoryHeard: true),
                read, write), Is.True);
        }

        [Test]
        public void ExtractionCannotRecordWithoutVerifiedM06Plan()
        {
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (key, value) => values[key] = value;

            var record = new M07ExtractionRecord(
                M07ExecutedRoute.OfficialHandover,
                M07PumpShutdownState.KeptRunning,
                M07RouteCostFlags.HarborSupplyDebt,
                boardedFirstBatchCount: 300,
                lowerLevelEvacuatedCount: 0,
                M07RusFate.Arrested,
                M07LedgerState.Preserved,
                M07LiangChoice.StayOnIsland,
                M07YidaChoice.HandToInspector,
                taoJiRescued: true);

            Assert.That(M07ExtractionFacts.TryRecord(record, read, write), Is.False);
            Assert.That(values, Is.Empty);
        }

        [Test]
        public void OnlyMinerShutdownRecordsThreeHourPumpStopAndFiveHundredUpperEvacuation()
        {
            var shutdownSave = new Dictionary<string, int>();
            SeedVerifiedM06Plan(shutdownSave, M06RescuePlan.MinerShutdown);
            Func<string, int> readShutdown = Reader(shutdownSave);
            Action<string, int> writeShutdown = (k, v) => shutdownSave[k] = v;

            // Cannot conflate 500 lower-level evacuees with boarded off-island count.
            Assert.That(M07ExtractionFacts.TryRecord(
                new M07ExtractionRecord(
                    M07ExecutedRoute.MinerShutdown,
                    M07PumpShutdownState.SafelyStoppedThreeHours,
                    M07RouteCostFlags.PumpOverhaulShiftCost,
                    boardedFirstBatchCount: 500,
                    lowerLevelEvacuatedCount: 500,
                    M07RusFate.LostContract,
                    M07LedgerState.PartiallyBurned,
                    M07LiangChoice.StayOnIsland,
                    M07YidaChoice.TakeAlong,
                    taoJiRescued: true),
                readShutdown, writeShutdown), Is.False);

            Assert.That(M07ExtractionFacts.TryRecord(
                new M07ExtractionRecord(
                    M07ExecutedRoute.MinerShutdown,
                    M07PumpShutdownState.SafelyStoppedThreeHours,
                    M07RouteCostFlags.PumpOverhaulShiftCost,
                    boardedFirstBatchCount: 300,
                    lowerLevelEvacuatedCount: 500,
                    M07RusFate.LostContract,
                    M07LedgerState.PartiallyBurned,
                    M07LiangChoice.ForcedToBoard,
                    M07YidaChoice.TakeAlong,
                    taoJiRescued: true),
                readShutdown, writeShutdown), Is.True);

            Assert.That(M07ExtractionFacts.TryRead(readShutdown, out M07ExtractionRecord? saved), Is.True);
            Assert.That(saved.HasValue, Is.True);
            Assert.That(saved.Value.PumpState, Is.EqualTo(M07PumpShutdownState.SafelyStoppedThreeHours));
            Assert.That(saved.Value.LowerLevelEvacuatedCount, Is.EqualTo(500));
            Assert.That(saved.Value.BoardedFirstBatchCount, Is.EqualTo(300));
            Assert.That(saved.Value.StrainsShenRelation, Is.True);

            // Non-shutdown route cannot borrow the 3-hour pump shutdown.
            var cargoSave = new Dictionary<string, int>();
            SeedVerifiedM06Plan(cargoSave, M06RescuePlan.CargoCover);
            Func<string, int> readCargo = Reader(cargoSave);
            Action<string, int> writeCargo = (k, v) => cargoSave[k] = v;

            Assert.That(M07ExtractionFacts.TryRecord(
                new M07ExtractionRecord(
                    M07ExecutedRoute.CargoCover,
                    M07PumpShutdownState.SafelyStoppedThreeHours,
                    M07RouteCostFlags.FreightCapacityLoss,
                    boardedFirstBatchCount: 240,
                    lowerLevelEvacuatedCount: 500,
                    M07RusFate.Fled,
                    M07LedgerState.PartiallyBurned,
                    M07LiangChoice.StayOnIsland,
                    M07YidaChoice.HandToInspector,
                    taoJiRescued: true),
                readCargo, writeCargo), Is.False);

            Assert.That(M07ExtractionFacts.TryRecord(
                new M07ExtractionRecord(
                    M07ExecutedRoute.CargoCover,
                    M07PumpShutdownState.KeptRunning,
                    M07RouteCostFlags.FreightCapacityLoss,
                    boardedFirstBatchCount: 240,
                    lowerLevelEvacuatedCount: 0,
                    M07RusFate.Fled,
                    M07LedgerState.PartiallyBurned,
                    M07LiangChoice.PersuadedToLeave,
                    M07YidaChoice.HandToInspector,
                    taoJiRescued: true),
                readCargo, writeCargo), Is.True);
        }

        [Test]
        public void FallbackOfficialHandoverPreservesOriginalRouteCostPlusFallbackCost()
        {
            var values = new Dictionary<string, int>();
            SeedVerifiedM06Plan(values, M06RescuePlan.ForceBreakout);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            // Missing fallback cost or missing original breakout cost must be rejected.
            Assert.That(M07ExtractionFacts.TryRecord(
                new M07ExtractionRecord(
                    M07ExecutedRoute.FallbackOfficialHandover,
                    M07PumpShutdownState.KeptRunning,
                    M07RouteCostFlags.FallbackTransferCost,
                    boardedFirstBatchCount: 180,
                    lowerLevelEvacuatedCount: 0,
                    M07RusFate.Sheltered,
                    M07LedgerState.Preserved,
                    M07LiangChoice.StayOnIsland,
                    M07YidaChoice.LeaveBehind,
                    taoJiRescued: true),
                read, write), Is.False);

            // Executing a mismatched non-fallback route is also rejected.
            Assert.That(M07ExtractionFacts.TryRecord(
                new M07ExtractionRecord(
                    M07ExecutedRoute.CargoCover,
                    M07PumpShutdownState.KeptRunning,
                    M07RouteCostFlags.FreightCapacityLoss,
                    boardedFirstBatchCount: 180,
                    lowerLevelEvacuatedCount: 0,
                    M07RusFate.Sheltered,
                    M07LedgerState.Preserved,
                    M07LiangChoice.StayOnIsland,
                    M07YidaChoice.LeaveBehind,
                    taoJiRescued: true),
                read, write), Is.False);

            var validFallback = new M07ExtractionRecord(
                M07ExecutedRoute.FallbackOfficialHandover,
                M07PumpShutdownState.KeptRunning,
                M07RouteCostFlags.PierRepairAndInjuryCost | M07RouteCostFlags.FallbackTransferCost,
                boardedFirstBatchCount: 180,
                lowerLevelEvacuatedCount: 0,
                M07RusFate.Sheltered,
                M07LedgerState.Preserved,
                M07LiangChoice.StayOnIsland,
                M07YidaChoice.LeaveBehind,
                taoJiRescued: true);

            Assert.That(M07ExtractionFacts.TryRecord(validFallback, read, write), Is.True);
            Assert.That(M07ExtractionFacts.TryRead(read, out M07ExtractionRecord? saved), Is.True);
            Assert.That(saved.HasValue, Is.True);
            Assert.That(saved.Value.ExecutedRoute, Is.EqualTo(M07ExecutedRoute.FallbackOfficialHandover));
            Assert.That(saved.Value.RouteCosts.HasFlag(M07RouteCostFlags.PierRepairAndInjuryCost), Is.True);
            Assert.That(saved.Value.RouteCosts.HasFlag(M07RouteCostFlags.FallbackTransferCost), Is.True);
        }

        [Test]
        public void InterruptedWriteCanRetryAndUpstreamResetInvalidatesM07Readback()
        {
            var values = new Dictionary<string, int>();
            SeedVerifiedM06Plan(values, M06RescuePlan.OfficialHandover);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            var record = new M07ExtractionRecord(
                M07ExecutedRoute.OfficialHandover,
                M07PumpShutdownState.KeptRunning,
                M07RouteCostFlags.HarborSupplyDebt,
                boardedFirstBatchCount: 260,
                lowerLevelEvacuatedCount: 0,
                M07RusFate.Arrested,
                M07LedgerState.Preserved,
                M07LiangChoice.StayOnIsland,
                M07YidaChoice.HandToInspector,
                taoJiRescued: true);

            int failOnce = 0;
            Assert.Throws<InvalidOperationException>(() =>
                M07ExtractionFacts.TryRecord(record, read, (k, v) =>
                {
                    if (k == M07ExtractionFacts.RusFateKey && failOnce++ == 0)
                        throw new InvalidOperationException("Interrupted M07 write");
                    write(k, v);
                }));
            Assert.That(M07ExtractionFacts.TryRead(read, out M07ExtractionRecord? partial), Is.True);
            Assert.That(partial, Is.Null);

            Assert.That(M07ExtractionFacts.TryRecord(record, read, write), Is.True);
            Assert.That(M07ExtractionFacts.TryRead(read, out M07ExtractionRecord? confirmed), Is.True);
            Assert.That(confirmed.HasValue, Is.True);

            // Resetting M06 invalidates stale M07 readback.
            Assert.That(M06RescuePlanFacts.TryResetForNewVoyage(write), Is.True);
            Assert.That(M07ExtractionFacts.TryRead(read, out M07ExtractionRecord? stale), Is.True);
            Assert.That(stale, Is.Null);

            Assert.That(M07ExtractionFacts.TryResetForNewVoyage(write), Is.True);
            Assert.That(values[M07ExtractionFacts.ConfirmedKey], Is.Zero);
        }

        [Test]
        public void MissingSaveAuthorityOrNullDelegatesNeverFabricateM07Facts()
        {
            var record = new M07ExtractionRecord(
                M07ExecutedRoute.OfficialHandover,
                M07PumpShutdownState.KeptRunning,
                M07RouteCostFlags.HarborSupplyDebt,
                boardedFirstBatchCount: 200,
                lowerLevelEvacuatedCount: 0,
                M07RusFate.Arrested,
                M07LedgerState.Preserved,
                M07LiangChoice.StayOnIsland,
                M07YidaChoice.HandToInspector,
                taoJiRescued: true);

            Assert.That(M07ExtractionFacts.TryRead(null, out M07ExtractionRecord? missing), Is.False);
            Assert.That(missing, Is.Null);
            Assert.That(M07ExtractionFacts.TryRecord(record, null, (_, __) => { }), Is.False);
            Assert.That(M07ExtractionFacts.TryRecord(record, _ => 0, null), Is.False);
            Assert.That(M07ExtractionFacts.TryResetForNewVoyage(null), Is.False);

            if (!Maki.Initialized || Maki.Game?.Variables == null)
            {
                Assert.That(M07ExtractionFacts.TryRead(out M07ExtractionRecord? unbound), Is.False);
                Assert.That(unbound, Is.Null);
                Assert.That(M07ExtractionFacts.TryRecord(record), Is.False);
                Assert.That(M07ExtractionFacts.TryResetForNewVoyage(), Is.False);
            }
        }
    }
}
