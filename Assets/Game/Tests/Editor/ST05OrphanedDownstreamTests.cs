using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    public sealed class ST05OrphanedDownstreamTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        [Test]
        public void AllST05WritersFailClosedWhenM15OrM16FactsExistAndRecoverOnceCleared()
        {
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            int st05Writes = 0;
            Action<string, int> write = (key, value) =>
            {
                if (key.StartsWith("rpg.return.st05.", StringComparison.Ordinal))
                    st05Writes++;
                values[key] = value;
            };

            var home = new ST05HouseholdRecord(
                ST05BedResolution.ConvertBedToCourtyardBenches,
                ST05HomeArrangement.MutuallyAgreedTrialSharedHomeAndStudio,
                mutualTrialConsent: true);

            Assert.That(ST05TwoDoorsFacts.HasOrphanedDownstreamFacts(null), Is.True);
            Assert.That(ST05TwoDoorsFacts.HasOrphanedDownstreamFacts(read), Is.False);

            string[] endgameKeys =
            {
                M15FinalRollCallFacts.ConfirmedKey,
                M16EpilogueFacts.ConfirmedKey
            };

            foreach (string key in endgameKeys)
            {
                values[key] = 1;
                Assert.That(ST05TwoDoorsFacts.HasOrphanedDownstreamFacts(read), Is.True, key);
                Assert.That(ST05TwoDoorsFacts.CanRecordHome(home, read), Is.False, key);
                Assert.That(ST05TwoDoorsFacts.TryRecordHome(home, read, write), Is.False, key);
                Assert.That(st05Writes, Is.Zero, key);
                values[key] = 0;
            }

            Assert.That(ST05TwoDoorsFacts.TryRecordHome(home, read, write), Is.True);
            int writesAfterHome = st05Writes;

            foreach (string key in endgameKeys)
            {
                values[key] = 1;
                Assert.That(ST05TwoDoorsFacts.TryRecordAssignment(
                    ST05ChengAssignment.AcceptOutsideSurvey, read, write), Is.False, key);
                Assert.That(st05Writes, Is.EqualTo(writesAfterHome), key);
                values[key] = 0;
            }

            Assert.That(ST05TwoDoorsFacts.TryRecordAssignment(
                ST05ChengAssignment.AcceptOutsideSurvey, read, write), Is.True);
            int writesAfterAssignment = st05Writes;

            foreach (string key in endgameKeys)
            {
                values[key] = 1;
                Assert.That(ST05TwoDoorsFacts.TryStartOutsideWork(read, write), Is.False, key);
                Assert.That(ST05TwoDoorsFacts.TryMarkYuheM15Arrival(true, read, write), Is.False, key);
                Assert.That(ST05TwoDoorsFacts.TryMarkChengM15Arrival(true, read, write), Is.False, key);
                Assert.That(st05Writes, Is.EqualTo(writesAfterAssignment), key);
                values[key] = 0;
            }

            Assert.That(ST05TwoDoorsFacts.TryStartOutsideWork(read, write), Is.True);
            int writesAfterWork = st05Writes;

            foreach (string key in endgameKeys)
            {
                values[key] = 1;
                Assert.That(ST05TwoDoorsFacts.TryMarkLetterSent(read, write), Is.False, key);
                Assert.That(st05Writes, Is.EqualTo(writesAfterWork), key);
                values[key] = 0;
            }

            Assert.That(ST05TwoDoorsFacts.TryMarkLetterSent(read, write), Is.True);
            int writesAfterSent = st05Writes;

            foreach (string key in endgameKeys)
            {
                values[key] = 1;
                Assert.That(ST05TwoDoorsFacts.TryMarkLetterDelivered(read, write), Is.False, key);
                Assert.That(st05Writes, Is.EqualTo(writesAfterSent), key);
                values[key] = 0;
            }

            Assert.That(ST05TwoDoorsFacts.TryMarkLetterDelivered(read, write), Is.True);
            Assert.That(ST05TwoDoorsFacts.TryMarkYuheM15Arrival(true, read, write), Is.True);
            Assert.That(ST05TwoDoorsFacts.TryMarkChengM15Arrival(true, read, write), Is.True);
            Assert.That(ST05TwoDoorsFacts.TryRead(read, out ST05TwoDoorsOutcome? saved), Is.True);
            Assert.That(saved.HasValue, Is.True);
            Assert.That(saved.Value.ShowM15Bread, Is.True);
            Assert.That(saved.Value.ShowM15HandedMap, Is.True);
        }
    }
}
