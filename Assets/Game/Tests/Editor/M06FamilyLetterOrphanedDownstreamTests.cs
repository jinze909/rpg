using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    public sealed class M06FamilyLetterOrphanedDownstreamTests
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
            int letterWrites = 0;
            Action<string, int> write = (key, value) =>
            {
                if (key == M06FamilyLetterDeliveryFacts.StatusMaskKey ||
                    key == M06FamilyLetterDeliveryFacts.ArrivalIdentityKey)
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
                Assert.That(M06FamilyLetterDeliveryFacts.TryRecord(
                    M06FamilyLetterRecipient.TaoJi, read, write), Is.False, orphanKey);
                Assert.That(letterWrites, Is.Zero, orphanKey);
                values[orphanKey] = 0;
            }

            Assert.That(M06FamilyLetterDeliveryFacts.TryRecord(
                M06FamilyLetterRecipient.TaoJi, read, write), Is.True);
            Assert.That(M06FamilyLetterDeliveryFacts.TryRecord(
                M06FamilyLetterRecipient.LedgerMiner, read, write), Is.True);
            Assert.That(M06FamilyLetterDeliveryFacts.TryRecord(
                M06FamilyLetterRecipient.PendingMiner, read, write), Is.True);
            Assert.That(M06FamilyLetterDeliveryFacts.TryRead(read, out var snapshot), Is.True);
            Assert.That(snapshot.AllKnownRecipientsHandled, Is.True);
            Assert.That(snapshot.HasPendingReturn, Is.True);
        }
    }
}
