using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    public sealed class M06FamilyLetterStatusMaskValidationTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        [Test]
        public void MismatchedPerRecipientStatusBitsFailClosedAcrossFactsAndRescuePlanGate()
        {
            var values = new Dictionary<string, int>();
            M06FamilyLetterDeliveryFactsTests.SeedArrival(values, M05LetterDecision.TakePacket);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (key, value) => values[key] = value;

            int currentIdentity = read(M05LandingFacts.ArrivalSequenceKey) + 1;
            values[M06FamilyLetterDeliveryFacts.ArrivalIdentityKey] = currentIdentity;

            var rescueRecord = new M06ReunionRecord(
                M06RescuePlan.OfficialHandover,
                true,
                M06LetterDeliveryOutcome.DeliveredWithPending,
                true,
                M06ShenResponse.LetHerSpeak);

            // Each corrupt mask uses <= 63 bits but assigns an impossible outcome to at least one recipient.
            // 54 = (2 << 0) | (1 << 2) | (3 << 4): wrong statuses for TaoJi and LedgerMiner while PendingMiner is 3.
            int[] mismatchedMasks =
            {
                (int)M06FamilyLetterStatus.DeliveredUnread << 0,
                (int)M06FamilyLetterStatus.ReturnedPending << 0,
                (int)M06FamilyLetterStatus.DeliveredAndReadTwice << 2,
                (int)M06FamilyLetterStatus.ReturnedPending << 2,
                (int)M06FamilyLetterStatus.DeliveredAndReadTwice << 4,
                (int)M06FamilyLetterStatus.DeliveredUnread << 4,
                ((int)M06FamilyLetterStatus.DeliveredUnread << 0) |
                    ((int)M06FamilyLetterStatus.DeliveredAndReadTwice << 2) |
                    ((int)M06FamilyLetterStatus.ReturnedPending << 4)
            };

            foreach (int corruptMask in mismatchedMasks)
            {
                values[M06FamilyLetterDeliveryFacts.StatusMaskKey] = corruptMask;
                Assert.That(M06FamilyLetterDeliveryFacts.TryRead(read, out _), Is.False,
                    $"TryRead must reject mismatched recipient status mask {corruptMask}.");
                Assert.That(M06FamilyLetterDeliveryFacts.TryRecord(
                    M06FamilyLetterRecipient.TaoJi, read, write), Is.False,
                    $"TryRecord must reject mismatched recipient status mask {corruptMask}.");
                Assert.That(M06RescuePlanFacts.CanRecord(rescueRecord, read), Is.False,
                    $"M06RescuePlanFacts must not accept mismatched letter mask {corruptMask}.");
            }

            // Valid authored mask: TaoJi=1, LedgerMiner=2, PendingMiner=3 -> 57.
            int validMask =
                ((int)M06FamilyLetterStatus.DeliveredAndReadTwice << 0) |
                ((int)M06FamilyLetterStatus.DeliveredUnread << 2) |
                ((int)M06FamilyLetterStatus.ReturnedPending << 4);
            values[M06FamilyLetterDeliveryFacts.StatusMaskKey] = validMask;
            Assert.That(M06FamilyLetterDeliveryFacts.TryRead(read, out var snapshot), Is.True);
            Assert.That(snapshot.AllKnownRecipientsHandled, Is.True);
            Assert.That(snapshot.HasPendingReturn, Is.True);
            Assert.That(M06RescuePlanFacts.CanRecord(rescueRecord, read), Is.True);
        }
    }
}
