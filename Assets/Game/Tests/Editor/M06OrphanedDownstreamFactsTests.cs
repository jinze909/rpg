using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    public sealed class M06OrphanedDownstreamFactsTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        private static void SeedVerifiedM05Arrival(Dictionary<string, int> values)
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
        public void M06FactsRejectRecordingOverOrphanedM07AndTransitiveSuccessorsUntilReset()
        {
            var values = new Dictionary<string, int>();
            SeedVerifiedM05Arrival(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            Assert.That(M07ExtractionFacts.HasAnyPersistedFacts(null), Is.True);
            Assert.That(M06RescuePlanFacts.HasOrphanedDownstreamFacts(null), Is.True);

            var record = new M06ReunionRecord(
                M06RescuePlan.OfficialHandover,
                reportedUsableCapacity: true,
                M06LetterDeliveryOutcome.NotCarried,
                retaliationHistoryHeard: true);

            foreach (string m07Key in new[]
            {
                M07ExtractionFacts.ConfirmedKey,
                M07ExtractionFacts.ExecutedRouteKey,
                M07ExtractionFacts.PumpStateKey,
                M07ExtractionFacts.RouteCostsKey,
                M07ExtractionFacts.BoardedFirstBatchKey,
                M07ExtractionFacts.LowerLevelEvacuatedKey,
                M07ExtractionFacts.RusFateKey,
                M07ExtractionFacts.LedgerStateKey,
                M07ExtractionFacts.LiangChoiceKey,
                M07ExtractionFacts.YidaChoiceKey,
                M07ExtractionFacts.TaoJiRescuedKey
            })
            {
                values[m07Key] = 1;
                Assert.That(M07ExtractionFacts.HasAnyPersistedFacts(read), Is.True, m07Key);
                Assert.That(M06RescuePlanFacts.HasOrphanedDownstreamFacts(read), Is.True, m07Key);
                Assert.That(M06RescuePlanFacts.CanRecord(record, read), Is.False, m07Key);
                Assert.That(M06RescuePlanFacts.TryRecord(record, read, write), Is.False, m07Key);
                Assert.That(M07ExtractionFacts.TryResetForNewVoyage(write), Is.True, m07Key);
            }

            foreach (string transitiveKey in new[]
            {
                M08WorkshopDepositFacts.CompletedKey,
                M08FirstWorldNoticeFacts.ConfirmedKey,
                ST06TaoJiHomecomingFacts.ConfirmedKey,
                RC01ChenYanFacts.ConfirmedKey,
                RC05ThirdSquadFacts.ConfirmedKey
            })
            {
                values[transitiveKey] = 1;
                Assert.That(M06RescuePlanFacts.HasOrphanedDownstreamFacts(read), Is.True, transitiveKey);
                Assert.That(M06RescuePlanFacts.CanRecord(record, read), Is.False, transitiveKey);
                Assert.That(M06RescuePlanFacts.TryRecord(record, read, write), Is.False, transitiveKey);
                values[transitiveKey] = 0;
            }

            Assert.That(M06RescuePlanFacts.HasOrphanedDownstreamFacts(read), Is.False);
            Assert.That(M06RescuePlanFacts.TryRecord(record, read, write), Is.True);
            Assert.That(M06RescuePlanFacts.TryRead(read, out M06ReunionRecord? saved), Is.True);
            Assert.That(saved.HasValue, Is.True);
        }

        [Test]
        public void M06ReunionSessionTryCreateRejectsOrphanedM07AndTransitiveSuccessorsUntilReset()
        {
            var values = new Dictionary<string, int>();
            SeedVerifiedM05Arrival(values);
            Func<string, int> read = Reader(values);

            foreach (string orphanKey in new[]
            {
                M07ExtractionFacts.ExecutedRouteKey,
                M08FirstWorldNoticeFacts.ConfirmedKey,
                ST06TaoJiHomecomingFacts.ChairResolutionKey,
                RC04DingYueReefFacts.PendingSignatureKey
            })
            {
                values[orphanKey] = 1;
                Assert.That(M06PumpRoomReunionSession.TryCreateFromSavedFacts(
                    read, out M06PumpRoomReunionSession blocked), Is.False, orphanKey);
                Assert.That(blocked, Is.Null, orphanKey);
                values[orphanKey] = 0;
            }

            Assert.That(M06PumpRoomReunionSession.TryCreateFromSavedFacts(
                read, out M06PumpRoomReunionSession session), Is.True);
            Assert.That(session, Is.Not.Null);
        }
    }
}
