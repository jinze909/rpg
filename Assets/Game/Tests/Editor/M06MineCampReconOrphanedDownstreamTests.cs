using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    public sealed class M06MineCampReconOrphanedDownstreamTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        [Test]
        public void TryRecordFailsClosedWhenOrphanedDownstreamFactsExistAndRecoversOnceCleared()
        {
            var values = new Dictionary<string, int>();
            M06FamilyLetterDeliveryFactsTests.SeedArrival(values, M05LetterDecision.TakePacket);
            Func<string, int> read = Reader(values);
            int reconWrites = 0;
            Action<string, int> write = (key, value) =>
            {
                if (key == M06MineCampReconFacts.ObservedMaskKey ||
                    key == M06MineCampReconFacts.ObservedArrivalIdentityKey)
                    reconWrites++;
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
                Assert.That(M06MineCampReconFacts.TryRecord(
                    M06MineCampLead.EscortSchedule, read, write), Is.False, orphanKey);
                Assert.That(reconWrites, Is.Zero, orphanKey);
                values[orphanKey] = 0;
            }

            Assert.That(M06MineCampReconSession.TryLoad(read, out var session), Is.True);
            Assert.That(session.TryInspectLead(M06MineCampLead.EscortSchedule, read, write), Is.True);
            Assert.That(session.ObservedCount, Is.EqualTo(1));
            Assert.That(M06MineCampReconFacts.TryRead(read, out int mask), Is.True);
            Assert.That(M06MineCampReconFacts.Contains(mask, M06MineCampLead.EscortSchedule), Is.True);
        }
    }
}
