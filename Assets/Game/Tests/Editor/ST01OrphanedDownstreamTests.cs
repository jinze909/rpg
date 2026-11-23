using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    public sealed class ST01OrphanedDownstreamTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        [Test]
        public void AllST01WritersFailClosedWhenM15OrM16FactsExistAndRecoverOnceCleared()
        {
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            int st01Writes = 0;
            Action<string, int> write = (key, value) =>
            {
                if (key == ST01RegionalStoryFacts.DepartureConfirmedKey ||
                    key == ST01RegionalStoryFacts.DepartureChoiceKey ||
                    key == ST01RegionalStoryFacts.TheaterConfirmedKey ||
                    key == ST01RegionalStoryFacts.TheaterChoiceKey ||
                    key == ST01RegionalStoryFacts.TicketStatusKey)
                    st01Writes++;
                values[key] = value;
            };

            Assert.That(ST01RegionalStoryFacts.HasOrphanedDownstreamFacts(null), Is.True);
            Assert.That(ST01RegionalStoryFacts.HasOrphanedDownstreamFacts(read), Is.False);

            string[] endgameKeys =
            {
                M15FinalRollCallFacts.ConfirmedKey,
                M16EpilogueFacts.ConfirmedKey
            };

            foreach (string key in endgameKeys)
            {
                values[key] = 1;
                Assert.That(ST01RegionalStoryFacts.HasOrphanedDownstreamFacts(read), Is.True, key);
                Assert.That(ST01RegionalStoryFacts.TryRecordDeparture(
                    ST01DepartureChoice.TalkTogether, read, write), Is.False, key);
                Assert.That(ST01RegionalStoryFacts.TryRecordTheater(
                    ST01TheaterChoice.RewritePlayWithFundingRisk, read, write), Is.False, key);
                Assert.That(st01Writes, Is.Zero, key);
                values[key] = 0;
            }

            Assert.That(ST01RegionalStoryFacts.TryRecordDeparture(
                ST01DepartureChoice.TalkTogether, read, write), Is.True);
            Assert.That(ST01RegionalStoryFacts.TryRecordTheater(
                ST01TheaterChoice.RewritePlayWithFundingRisk, read, write), Is.True);

            int writesAfterTheater = st01Writes;
            foreach (string key in endgameKeys)
            {
                values[key] = 1;
                Assert.That(ST01RegionalStoryFacts.TryMarkTicketSent(read, write), Is.False, key);
                Assert.That(ST01RegionalStoryFacts.TryMarkTicketDelivered(read, write), Is.False, key);
                Assert.That(st01Writes, Is.EqualTo(writesAfterTheater), key);
                values[key] = 0;
            }

            Assert.That(ST01RegionalStoryFacts.TryMarkTicketSent(read, write), Is.True);
            Assert.That(ST01RegionalStoryFacts.TryMarkTicketDelivered(read, write), Is.True);
            Assert.That(ST01RegionalStoryFacts.TryRead(read, out ST01RegionalOutcome saved), Is.True);
            Assert.That(saved.ShowM15Ticket, Is.True);
        }
    }
}
