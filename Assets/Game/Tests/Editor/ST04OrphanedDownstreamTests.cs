using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    public sealed class ST04OrphanedDownstreamTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int found) ? found : 0;
        }

        [Test]
        public void AllST04WritersFailClosedWhenM15OrM16FactsExistAndRecoverOnceCleared()
        {
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            int st04Writes = 0;
            Action<string, int> write = (key, value) =>
            {
                if (key.StartsWith("rpg.return.st04.", StringComparison.Ordinal))
                    st04Writes++;
                values[key] = value;
            };

            var record = new ST04EchoValleyRecord(
                ST04ValleyEntry.EnterQuietly,
                ST04ChamberEntry.LureWithBell,
                ST04BeastHandling.ContainedAwayFromClosedRoad,
                ST04RoadResolution.PreserveRainWallAndCloseRoad,
                rescuedSurveyors: 2,
                echoRulesVerified: true,
                companionsMarked: true,
                drainOpened: true,
                longRoadFunded: false,
                alternativeWorksiteReady: true);

            Assert.That(ST04EchoValleyFacts.HasOrphanedDownstreamFacts(null), Is.True);
            Assert.That(ST04EchoValleyFacts.HasOrphanedDownstreamFacts(read), Is.False);

            string[] endgameKeys =
            {
                M15FinalRollCallFacts.ConfirmedKey,
                M16EpilogueFacts.ConfirmedKey
            };

            foreach (string key in endgameKeys)
            {
                values[key] = 1;
                Assert.That(ST04EchoValleyFacts.HasOrphanedDownstreamFacts(read), Is.True, key);
                Assert.That(ST04EchoValleyFacts.CanRecord(record, read), Is.False, key);
                Assert.That(ST04EchoValleyFacts.TryRecord(record, read, write), Is.False, key);
                Assert.That(st04Writes, Is.Zero, key);
                values[key] = 0;
            }

            Assert.That(ST04EchoValleyFacts.TryRecord(record, read, write), Is.True);
            int writesAfterStory = st04Writes;

            foreach (string key in endgameKeys)
            {
                values[key] = 1;
                Assert.That(ST04EchoValleyFacts.TryVerifyAdditionalStoneTransport(read, write), Is.False, key);
                Assert.That(st04Writes, Is.EqualTo(writesAfterStory), key);
                values[key] = 0;
            }

            Assert.That(ST04EchoValleyFacts.TryVerifyAdditionalStoneTransport(read, write), Is.True);
            int writesAfterTransport = st04Writes;

            foreach (string key in endgameKeys)
            {
                values[key] = 1;
                Assert.That(ST04EchoValleyFacts.TryMarkStoneOrderAccepted(read, write), Is.False, key);
                Assert.That(st04Writes, Is.EqualTo(writesAfterTransport), key);
                values[key] = 0;
            }

            Assert.That(ST04EchoValleyFacts.TryMarkStoneOrderAccepted(read, write), Is.True);
            int writesAfterOrder = st04Writes;

            foreach (string key in endgameKeys)
            {
                values[key] = 1;
                Assert.That(ST04EchoValleyFacts.TryMarkStoneDelivered(read, write), Is.False, key);
                Assert.That(st04Writes, Is.EqualTo(writesAfterOrder), key);
                values[key] = 0;
            }

            Assert.That(ST04EchoValleyFacts.TryMarkStoneDelivered(read, write), Is.True);
            Assert.That(ST04EchoValleyFacts.TryRead(read, out ST04EchoValleyOutcome? saved), Is.True);
            Assert.That(saved.HasValue, Is.True);
            Assert.That(saved.Value.ShowZhonglingStoneOnM15Monument, Is.True);
        }
    }
}
