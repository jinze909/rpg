using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    public sealed class M05LettersOrphanedDownstreamTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        private static void SeedVerifiedLanding(Dictionary<string, int> values)
        {
            Func<string, int> read = Reader(values);
            Action<string, int> write = (key, value) => values[key] = value;

            var manifest = new M04DepartureManifest(
                M03VoyageChoice.QiQueMerchant,
                "Verified hull", "A Yuan", 4, "Shallow tender",
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
        public void TryRecordFailsClosedWhenOrphanedDownstreamFactsExistAndRecoversOnceCleared()
        {
            var values = new Dictionary<string, int>();
            SeedVerifiedLanding(values);
            Func<string, int> read = Reader(values);
            int letterWrites = 0;
            Action<string, int> write = (key, value) =>
            {
                if (key == M05LettersFacts.DecisionKey ||
                    key == M05LettersFacts.DecisionArrivalIdentityKey)
                    letterWrites++;
                values[key] = value;
            };

            string[] orphanKeys =
            {
                M07ExtractionFacts.ExecutedRouteKey,
                M08FirstWorldNoticeFacts.ConfirmedKey,
                M09BerthTableFacts.ConfirmedKey,
                RC05ThirdSquadFacts.ConfirmedKey,
                M10AssemblyFacts.ConfirmedKey,
                M16EpilogueFacts.ConfirmedKey
            };

            foreach (string orphanKey in orphanKeys)
            {
                values[orphanKey] = 1;
                Assert.That(M05LettersFacts.TryRecord(
                    M05LetterDecision.TakePacket, read, write), Is.False, orphanKey);
                Assert.That(letterWrites, Is.Zero, orphanKey);
                values[orphanKey] = 0;
            }

            Assert.That(M05LettersFacts.TryRecord(
                M05LetterDecision.TakePacket, read, write), Is.True);
            Assert.That(M05LettersFacts.TryRead(read, out M05LetterDecision? decision), Is.True);
            Assert.That(decision, Is.EqualTo(M05LetterDecision.TakePacket));
        }
    }
}
