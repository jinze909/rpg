using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    public sealed class M02BridgeEvidenceOrphanedDownstreamTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        [Test]
        public void TryRecordObservedFailsClosedOnNullDelegatesOrOrphanedDownstreamFacts()
        {
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            int evidenceWrites = 0;
            Action<string, int> write = (key, value) =>
            {
                if (key == M02BridgeEvidenceFacts.EvidenceKey)
                    evidenceWrites++;
                values[key] = value;
            };

            Assert.That(M02BridgeEvidenceFacts.TryRecordObserved(
                M02BridgeEvidence.CleanCableCut, null, write), Is.False);
            Assert.That(M02BridgeEvidenceFacts.TryRecordObserved(
                M02BridgeEvidence.CleanCableCut, read, null), Is.False);
            Assert.That(M02BridgeEvidenceFacts.TryRead(
                null, out M02BridgeEvidence missing), Is.False);
            Assert.That(missing, Is.EqualTo(M02BridgeEvidence.None));
            Assert.That(evidenceWrites, Is.Zero);

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
                Assert.That(M02BridgeEvidenceFacts.TryRecordObserved(
                    M02BridgeEvidence.CleanCableCut, read, write), Is.False, orphanKey);
                Assert.That(evidenceWrites, Is.Zero, orphanKey);
                values[orphanKey] = 0;
            }

            Assert.That(M02BridgeEvidenceFacts.TryRecordObserved(
                M02BridgeEvidence.CleanCableCut, read, write), Is.True);
            Assert.That(M02BridgeEvidenceFacts.TryRecordObserved(
                M02BridgeEvidence.FerrymanLog, read, write), Is.True);
            Assert.That(M02BridgeEvidenceFacts.TryRead(
                read, out M02BridgeEvidence saved), Is.True);
            Assert.That(saved, Is.EqualTo(
                M02BridgeEvidence.CleanCableCut | M02BridgeEvidence.FerrymanLog));
        }
    }
}
