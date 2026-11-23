using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    public sealed class M00HanRansomOrphanedDownstreamTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        [Test]
        public void TryRecordConfirmedCountFailsClosedOnNullDelegatesOrOrphanedDownstreamFacts()
        {
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            int ransomWrites = 0;
            Action<string, int> write = (key, value) =>
            {
                if (key == M00HanRansomFacts.CountKey)
                    ransomWrites++;
                values[key] = value;
            };

            Assert.That(M00HanRansomFacts.TryRecordConfirmedCount(null, write), Is.False);
            Assert.That(M00HanRansomFacts.TryRecordConfirmedCount(read, null), Is.False);
            Assert.That(M00HanRansomFacts.TryRead(null, out int missing), Is.False);
            Assert.That(missing, Is.EqualTo(M00HanRansomFacts.ConfirmedCount));
            Assert.That(ransomWrites, Is.Zero);

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
                Assert.That(M00HanRansomFacts.TryRecordConfirmedCount(read, write),
                    Is.False, orphanKey);
                Assert.That(ransomWrites, Is.Zero, orphanKey);
                values[orphanKey] = 0;
            }

            Assert.That(M00HanRansomFacts.TryRecordConfirmedCount(read, write), Is.True);
            Assert.That(values[M00HanRansomFacts.CountKey],
                Is.EqualTo(M00HanRansomFacts.ConfirmedCount));
            Assert.That(M00HanRansomFacts.TryRead(read, out int saved), Is.True);
            Assert.That(saved, Is.EqualTo(M00HanRansomFacts.ConfirmedCount));
        }
    }
}
