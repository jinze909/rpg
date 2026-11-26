using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    public sealed class FogListeningOrphanedDownstreamTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        private static FogListeningSession CompletedLeg()
        {
            var session = new FogListeningSession(
                new[] { new FogListeningCue(FogBearing.Ahead, FogCueKind.ClearWater, 80) },
                maxListenFocus: 1, maxHullIntegrity: 2);
            session.ApplyHelmAction(FogHelmAction.HoldCourse);
            Assert.That(session.IsComplete, Is.True);
            return session;
        }

        [Test]
        public void TryRecordFailsClosedWhenOrphanedDownstreamFactsExistAndRecoversOnceCleared()
        {
            var values = new Dictionary<string, int>
            {
                [M04DepartureFacts.SailingSequenceKey] = 1
            };
            Func<string, int> read = Reader(values);
            int fogWrites = 0;
            Action<string, int> write = (key, value) =>
            {
                if (key == FogListeningFacts.CompletedKey ||
                    key == FogListeningFacts.PendingSignatureKey ||
                    key == FogListeningFacts.SucceededKey ||
                    key == FogListeningFacts.HullIntegrityKey ||
                    key == FogListeningFacts.AvoidedHazardsKey ||
                    key == FogListeningFacts.MatchedSignalsKey ||
                    key == FogListeningFacts.RemainingFocusKey)
                    fogWrites++;
                values[key] = value;
            };

            FogListeningSession leg = CompletedLeg();
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
                Assert.That(FogListeningFacts.TryRecord(leg, read, write), Is.False, orphanKey);
                Assert.That(fogWrites, Is.Zero, orphanKey);
                values[orphanKey] = 0;
            }

            Assert.That(FogListeningFacts.TryRecord(leg, read, write), Is.True);
            Assert.That(FogListeningFacts.TryRead(read, out FogListeningRecord? saved), Is.True);
            Assert.That(saved.HasValue, Is.True);
            Assert.That(saved.Value.Succeeded, Is.True);
        }
    }
}
