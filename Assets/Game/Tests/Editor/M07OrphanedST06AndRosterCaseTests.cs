using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    public sealed class M07OrphanedST06AndRosterCaseTests
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
        public void M07ExtractionRejectsRecordingOverOrphanedST06OrRosterCaseFactsUntilReset()
        {
            var values = new Dictionary<string, int>();
            SeedThroughM06(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            var record = new M07ExtractionRecord(
                M07ExecutedRoute.OfficialHandover,
                M07PumpShutdownState.KeptRunning,
                M07RouteCostFlags.HarborSupplyDebt,
                boardedFirstBatchCount: 10,
                lowerLevelEvacuatedCount: 0,
                M07RusFate.Arrested, M07LedgerState.Preserved,
                M07LiangChoice.StayOnIsland,
                M07YidaChoice.HandToInspector,
                taoJiRescued: true);

            string[] orphanedKeys =
            {
                ST06TaoJiHomecomingFacts.ConfirmedKey,
                ST06TaoJiHomecomingFacts.ChairResolutionKey,
                ST06TaoJiHomecomingFacts.LetterStatusKey,
                RC01ChenYanFacts.ConfirmedKey,
                RC04DingYueReefFacts.PendingSignatureKey,
                RC05ThirdSquadFacts.ConfirmedKey
            };

            foreach (string key in orphanedKeys)
            {
                values[key] = 1;
                Assert.That(M07ExtractionFacts.HasOrphanedM08SuccessorFacts(read), Is.True,
                    $"Orphaned downstream key '{key}' must block M07.");
                Assert.That(M07ExtractionFacts.CanRecord(record, read), Is.False);
                Assert.That(M07ExtractionFacts.TryRecord(record, read, write), Is.False);
                values[key] = 0;
            }

            Assert.That(M07ExtractionFacts.HasOrphanedM08SuccessorFacts(read), Is.False);
            Assert.That(M07ExtractionFacts.TryRecord(record, read, write), Is.True);
            Assert.That(M07ExtractionFacts.TryRead(read, out M07ExtractionRecord? saved), Is.True);
            Assert.That(saved.HasValue, Is.True);
            Assert.That(saved.Value.TaoJiRescued, Is.True);
        }
    }
}
