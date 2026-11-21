using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class M09OrphanedAct3FactsTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        private static void SeedVerifiedM07(Dictionary<string, int> values)
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
        public void M09FactsRejectRecordingOverOrphanedM10ThroughM16FactsAndRecoverAfterReset()
        {
            var values = new Dictionary<string, int>();
            SeedVerifiedM07(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            Assert.That(M10AssemblyFacts.HasAnyPersistedFacts(null), Is.True);
            Assert.That(M09BerthTableFacts.HasOrphanedDownstreamFacts(null), Is.True);

            var record = new M09BerthTableRecord(
                20, 23, M09BerthExchangeFlags.HanQiaoCivilianVoyage,
                M09RuiyaOutcome.Rescued, false, false);

            foreach (string m10Key in new[]
            {
                M10AssemblyFacts.ConfirmedKey,
                M10AssemblyFacts.WorldNoticeThreeKey,
                M10AssemblyFacts.RosterAccuracyKey,
                M10AssemblyFacts.BattleReportSourceKey,
                M10AssemblyFacts.HanLedgerUseKey,
                M10AssemblyFacts.SupplyContractKey,
                M10AssemblyFacts.LeverageWeakenedRC02Key,
                M10AssemblyFacts.CoversRC05FamiliesKey,
                M10AssemblyFacts.BellTowerSeizureKey
            })
            {
                values[m10Key] = 1;
                Assert.That(M10AssemblyFacts.HasAnyPersistedFacts(read), Is.True, m10Key);
                Assert.That(M09BerthTableFacts.HasOrphanedDownstreamFacts(read), Is.True, m10Key);
                Assert.That(M09BerthTableFacts.CanRecord(record, read), Is.False, m10Key);
                Assert.That(M09BerthTableFacts.TryRecord(record, read, write), Is.False, m10Key);
                Assert.That(M10AssemblyFacts.TryResetForNewVoyage(write), Is.True, m10Key);
            }

            // Transitive downstream Act-3 keys (M11, M14 AyuanOutcome, M16) must also block M09.
            values[M11ReverseRescueFacts.RescueProviderKey] = (int)M11RescueProvider.ShenLiShallowBoat;
            Assert.That(M09BerthTableFacts.HasOrphanedDownstreamFacts(read), Is.True);
            Assert.That(M09BerthTableFacts.TryRecord(record, read, write), Is.False);
            Assert.That(M11ReverseRescueFacts.TryResetForNewVoyage(write), Is.True);

            values[M13DepartureRiskFacts.AyuanOutcomeKey] = (int)M14AyuanOutcome.Safe;
            Assert.That(M09BerthTableFacts.HasOrphanedDownstreamFacts(read), Is.True);
            Assert.That(M09BerthTableFacts.TryRecord(record, read, write), Is.False);
            Assert.That(M13DepartureRiskFacts.TryResetForNewVoyage(write), Is.True);

            values[M16EpilogueFacts.ConfirmedKey] = 1;
            Assert.That(M09BerthTableFacts.HasOrphanedDownstreamFacts(read), Is.True);
            Assert.That(M09BerthTableFacts.TryRecord(record, read, write), Is.False);
            Assert.That(M16EpilogueFacts.TryResetForNewVoyage(write), Is.True);

            Assert.That(M09BerthTableFacts.HasOrphanedDownstreamFacts(read), Is.False);
            Assert.That(M09BerthTableFacts.TryRecord(record, read, write), Is.True);
            Assert.That(M09BerthTableFacts.TryRead(read, out M09BerthTableRecord? saved), Is.True);
            Assert.That(saved.HasValue, Is.True);
        }

        [Test]
        public void M09HostTryOpenRejectsOrphanedM10AndTransitiveAct3FactsUntilReset()
        {
            var root = new GameObject("M09 orphaned Act3 host guard");
            var values = new Dictionary<string, int>();
            SeedVerifiedM07(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            M09BerthTableHost host = null;
            try
            {
                host = new M09BerthTableHost(
                    root.transform,
                    () => true,
                    () => true,
                    () => false,
                    () => 18,
                    () => 23,
                    () => M09RuiyaOutcome.Deferred,
                    read,
                    write);

                values[M10AssemblyFacts.HanLedgerUseKey] = (int)M10HanLedgerUse.PublishDraft;
                Assert.That(host.TryOpen(), Is.False);
                Assert.That(host.ActiveView, Is.Null);
                Assert.That(M10AssemblyFacts.TryResetForNewVoyage(write), Is.True);

                values[M16EpilogueFacts.StandingSpotKey] = (int)M16StandingSpot.PierSeaBreeze;
                Assert.That(host.TryOpen(), Is.False);
                Assert.That(host.ActiveView, Is.Null);
                Assert.That(M16EpilogueFacts.TryResetForNewVoyage(write), Is.True);

                Assert.That(host.TryOpen(), Is.True);
                Assert.That(host.ActiveView, Is.Not.Null);
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
