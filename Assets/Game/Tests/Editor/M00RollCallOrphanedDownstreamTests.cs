using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    public sealed class M00RollCallOrphanedDownstreamTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        private static M00RollCallSequence SettledRollCall()
        {
            var sequence = new M00RollCallSequence();
            sequence.Begin();
            sequence.Respond(M00RollCallChoice.Answer);
            Assert.That(sequence.IsComplete, Is.True);
            return sequence;
        }

        [Test]
        public void TryRecordFailsClosedOnNullDelegatesOrOrphanedDownstreamFacts()
        {
            M00RollCallSequence sequence = SettledRollCall();
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            int rollCallWrites = 0;
            Action<string, int> write = (key, value) =>
            {
                if (key == M00RollCallFacts.IdentityRevealKey)
                    rollCallWrites++;
                values[key] = value;
            };

            Assert.That(M00RollCallFacts.TryRecord(sequence, null, write), Is.False);
            Assert.That(M00RollCallFacts.TryRecord(sequence, read, null), Is.False);
            Assert.That(M00RollCallFacts.TryRead(null, out M00RollCallOutcome missing), Is.False);
            Assert.That(missing, Is.EqualTo(M00RollCallOutcome.Silent));
            Assert.That(rollCallWrites, Is.Zero);

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
                Assert.That(M00RollCallFacts.TryRecord(sequence, read, write), Is.False, orphanKey);
                Assert.That(rollCallWrites, Is.Zero, orphanKey);
                values[orphanKey] = 0;
            }

            Assert.That(M00RollCallFacts.TryRecord(sequence, read, write), Is.True);
            Assert.That(M00RollCallFacts.TryRead(read, out M00RollCallOutcome saved), Is.True);
            Assert.That(saved, Is.EqualTo(M00RollCallOutcome.AnsweredFirst));
        }
    }
}
