using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    public sealed class M02QiQueDebtOrphanedDownstreamTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        [Test]
        public void TryRecordDiscoveredFailsClosedOnNullDelegatesOrOrphanedDownstreamFacts()
        {
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            int debtWrites = 0;
            Action<string, int> write = (key, value) =>
            {
                if (key == M02QiQueDebtFacts.DebtKnownKey)
                    debtWrites++;
                values[key] = value;
            };

            Assert.That(M02QiQueDebtFacts.TryRecordDiscovered(null, write), Is.False);
            Assert.That(M02QiQueDebtFacts.TryRecordDiscovered(read, null), Is.False);
            Assert.That(M02QiQueDebtFacts.TryRead(null, out bool missing), Is.False);
            Assert.That(missing, Is.False);
            Assert.That(debtWrites, Is.Zero);

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
                Assert.That(M02QiQueDebtFacts.TryRecordDiscovered(read, write),
                    Is.False, orphanKey);
                Assert.That(debtWrites, Is.Zero, orphanKey);
                values[orphanKey] = 0;
            }

            Assert.That(M02QiQueDebtFacts.TryRecordDiscovered(read, write), Is.True);
            Assert.That(M02QiQueDebtFacts.TryRead(read, out bool debtKnown), Is.True);
            Assert.That(debtKnown, Is.True);
        }
    }
}
