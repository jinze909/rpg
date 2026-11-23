using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    public sealed class ReturnStoryLedgerM06FamilyLettersResetTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        [Test]
        public void DownstreamVoyageResetInvalidatesAndClearsM06FamilyLetterDeliveryFacts()
        {
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            var manifest = new M04DepartureManifest(
                M03VoyageChoice.QiQueMerchant,
                "Confirmed hull", "A Yuan", 4, "Shallow tender",
                true, 16, 8, "Named receiver", false, false);
            Assert.That(manifest.ConfirmDeparture(), Is.True);
            Assert.That(M04DepartureFacts.TryRecord(manifest, write), Is.True);
            Assert.That(M04DepartureFacts.TryMarkSailed(read, write), Is.True);

            var fog = new FogListeningSession(
                new[] { new FogListeningCue(FogBearing.Ahead, FogCueKind.ClearWater, 80) },
                maxListenFocus: 1, maxHullIntegrity: 2);
            fog.ApplyHelmAction(FogHelmAction.HoldCourse);
            Assert.That(FogListeningFacts.TryRecord(fog, write), Is.True);
            Assert.That(M05LandingFacts.TryRecord(
                new M05LandingReport(3, 10, true, true), read, write), Is.True);
            Assert.That(M05LettersFacts.TryRecord(
                M05LetterDecision.TakePacket, read, write), Is.True);

            Assert.That(M06FamilyLetterDeliveryFacts.TryRecord(
                M06FamilyLetterRecipient.TaoJi, read, write), Is.True);
            Assert.That(M06FamilyLetterDeliveryFacts.TryRecord(
                M06FamilyLetterRecipient.LedgerMiner, read, write), Is.True);
            Assert.That(M06FamilyLetterDeliveryFacts.TryRecord(
                M06FamilyLetterRecipient.PendingMiner, read, write), Is.True);

            Assert.That(M06FamilyLetterDeliveryFacts.TryRead(
                read, out M06FamilyLetterDeliverySnapshot before), Is.True);
            Assert.That(before.AllKnownRecipientsHandled, Is.True);
            Assert.That(before.HasPendingReturn, Is.True);

            // Torn reset after the 21 marker-invalidation writes must already clear StatusMaskKey.
            int writes = 0;
            Assert.Throws<InvalidOperationException>(() =>
                ReturnStoryLedger.TryResetAllDownstreamVoyageFacts((k, v) =>
                {
                    if (writes >= 21)
                        throw new InvalidOperationException("Simulated torn reset");
                    writes++;
                    values[k] = v;
                }));

            Assert.That(read(M06FamilyLetterDeliveryFacts.StatusMaskKey), Is.Zero);
            Assert.That(M06FamilyLetterDeliveryFacts.TryRead(
                read, out M06FamilyLetterDeliverySnapshot afterTorn), Is.True);
            Assert.That(afterTorn.HandledCount, Is.Zero);

            // Full clean reset clears both StatusMaskKey and ArrivalIdentityKey.
            Assert.That(ReturnStoryLedger.TryResetAllDownstreamVoyageFacts(write), Is.True);
            Assert.That(read(M06FamilyLetterDeliveryFacts.StatusMaskKey), Is.Zero);
            Assert.That(read(M06FamilyLetterDeliveryFacts.ArrivalIdentityKey), Is.Zero);
            Assert.That(M06FamilyLetterDeliveryFacts.TryRead(
                read, out M06FamilyLetterDeliverySnapshot afterClean), Is.True);
            Assert.That(afterClean.HandledCount, Is.Zero);
        }
    }
}
