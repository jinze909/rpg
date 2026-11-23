using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    public sealed class M06FamilyLetterDeliveryFactsTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        private static Action<string, int> Writer(Dictionary<string, int> values)
        {
            return (key, value) => values[key] = value;
        }

        internal static void SeedArrival(
            Dictionary<string, int> values,
            M05LetterDecision decision = M05LetterDecision.TakePacket)
        {
            Func<string, int> read = Reader(values);
            Action<string, int> write = Writer(values);
            var manifest = new M04DepartureManifest(
                M03VoyageChoice.QiQueMerchant, "Verified hull", "A Yuan", 4,
                "Shallow tender", true, 16, 8, "Named receiver", false, false);
            Assert.That(manifest.ConfirmDeparture(), Is.True);
            Assert.That(M04DepartureFacts.TryRecord(manifest, write), Is.True);
            Assert.That(M04DepartureFacts.TryMarkSailed(read, write), Is.True);
            var fog = new FogListeningSession(new[]
            {
                new FogListeningCue(FogBearing.Ahead, FogCueKind.ClearWater, 80)
            }, maxListenFocus: 1, maxHullIntegrity: 2);
            fog.ApplyHelmAction(FogHelmAction.HoldCourse);
            Assert.That(FogListeningFacts.TryRecord(fog, write), Is.True);
            Assert.That(M05LandingFacts.TryRecord(
                new M05LandingReport(3, 10, true, true), read, write), Is.True);
            Assert.That(M05LettersFacts.TryRecord(decision, read, write), Is.True);
        }

        [Test]
        public void DeliveryRequiresVerifiedLandingAndActuallyCarriedPacket()
        {
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = Writer(values);

            Assert.That(M06FamilyLetterDeliveryFacts.TryRecord(
                M06FamilyLetterRecipient.TaoJi, read, write), Is.False);

            SeedArrival(values, M05LetterDecision.LeaveForNextBoat);
            Assert.That(M06FamilyLetterDeliveryFacts.TryRecord(
                M06FamilyLetterRecipient.TaoJi, read, write), Is.False);
            Assert.That(values.ContainsKey(M06FamilyLetterDeliveryFacts.StatusMaskKey), Is.False);
        }

        [Test]
        public void ThreeKnownLettersPersistTheirDistinctAuthoredOutcomes()
        {
            var values = new Dictionary<string, int>();
            SeedArrival(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = Writer(values);

            Assert.That(M06FamilyLetterDeliveryFacts.TryRecord(
                M06FamilyLetterRecipient.TaoJi, read, write), Is.True);
            Assert.That(M06FamilyLetterDeliveryFacts.TryRecord(
                M06FamilyLetterRecipient.LedgerMiner, read, write), Is.True);
            Assert.That(M06FamilyLetterDeliveryFacts.TryRecord(
                M06FamilyLetterRecipient.PendingMiner, read, write), Is.True);

            Assert.That(M06FamilyLetterDeliveryFacts.TryRead(
                read, out M06FamilyLetterDeliverySnapshot snapshot), Is.True);
            Assert.That(snapshot.AllKnownRecipientsHandled, Is.True);
            Assert.That(snapshot.HandledCount, Is.EqualTo(3));
            Assert.That(snapshot.StatusFor(M06FamilyLetterRecipient.TaoJi),
                Is.EqualTo(M06FamilyLetterStatus.DeliveredAndReadTwice));
            Assert.That(snapshot.StatusFor(M06FamilyLetterRecipient.LedgerMiner),
                Is.EqualTo(M06FamilyLetterStatus.DeliveredUnread));
            Assert.That(snapshot.StatusFor(M06FamilyLetterRecipient.PendingMiner),
                Is.EqualTo(M06FamilyLetterStatus.ReturnedPending));
            Assert.That(snapshot.HasPendingReturn, Is.True);

            int writes = 0;
            Assert.That(M06FamilyLetterDeliveryFacts.TryRecord(
                M06FamilyLetterRecipient.TaoJi, read,
                (key, value) => { writes++; write(key, value); }), Is.True);
            Assert.That(writes, Is.Zero, "Repeating a completed handoff must be idempotent.");
        }

        [Test]
        public void IdenticalNewVoyageCannotInheritPreviousLetterHandoffs()
        {
            var values = new Dictionary<string, int>();
            SeedArrival(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = Writer(values);
            Assert.That(M06FamilyLetterDeliveryFacts.TryRecord(
                M06FamilyLetterRecipient.TaoJi, read, write), Is.True);
            int oldSequence = read(M05LandingFacts.ArrivalSequenceKey);

            Assert.That(ReturnOpeningContinuation.TryResetPreviousOutboundFacts(write), Is.True);
            SeedArrival(values);
            Assert.That(read(M05LandingFacts.ArrivalSequenceKey), Is.GreaterThan(oldSequence));
            Assert.That(M06FamilyLetterDeliveryFacts.TryRead(
                read, out M06FamilyLetterDeliverySnapshot beforeNewDelivery), Is.True);
            Assert.That(beforeNewDelivery.HandledCount, Is.Zero);

            Assert.That(M06FamilyLetterDeliveryFacts.TryRecord(
                M06FamilyLetterRecipient.LedgerMiner, read, write), Is.True);
            Assert.That(M06FamilyLetterDeliveryFacts.TryRead(
                read, out M06FamilyLetterDeliverySnapshot current), Is.True);
            Assert.That(current.HandledCount, Is.EqualTo(1));
            Assert.That(current.IsHandled(M06FamilyLetterRecipient.TaoJi), Is.False);
            Assert.That(current.IsHandled(M06FamilyLetterRecipient.LedgerMiner), Is.True);
        }

        [Test]
        public void CorruptStatusMaskOrArrivalIdentityFailsClosed()
        {
            var values = new Dictionary<string, int>();
            SeedArrival(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = Writer(values);
            values[M06FamilyLetterDeliveryFacts.StatusMaskKey] = 64;
            Assert.That(M06FamilyLetterDeliveryFacts.TryRead(
                read, out M06FamilyLetterDeliverySnapshot invalidMask), Is.False);
            Assert.That(M06FamilyLetterDeliveryFacts.TryRecord(
                M06FamilyLetterRecipient.TaoJi, read, write), Is.False);

            values[M06FamilyLetterDeliveryFacts.StatusMaskKey] = 0;
            values[M06FamilyLetterDeliveryFacts.ArrivalIdentityKey] = -1;
            Assert.That(M06FamilyLetterDeliveryFacts.TryRead(
                read, out M06FamilyLetterDeliverySnapshot invalidIdentity), Is.False);
        }
    }
}
