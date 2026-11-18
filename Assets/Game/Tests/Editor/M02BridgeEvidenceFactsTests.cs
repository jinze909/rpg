using System;
using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    public sealed class M02BridgeEvidenceFactsTests
    {
        [Test]
        public void MissingAndCorruptedValuesCannotFabricateClues()
        {
            Assert.That(M02BridgeEvidenceFacts.Decode(0), Is.EqualTo(M02BridgeEvidence.None));
            Assert.That(M02BridgeEvidenceFacts.Decode(-1), Is.EqualTo(M02BridgeEvidence.None));
            Assert.That(M02BridgeEvidenceFacts.Decode(16), Is.EqualTo(M02BridgeEvidence.None));
            Assert.That(M02BridgeEvidenceFacts.Decode(17), Is.EqualTo(M02BridgeEvidence.None));
        }

        [Test]
        public void EveryCombinationOfTheFourKnownCluesRoundTripsExactly()
        {
            for (int mask = 0; mask <= 15; mask++)
            {
                var clues = (M02BridgeEvidence)mask;
                Assert.That(M02BridgeEvidenceFacts.Decode(
                    M02BridgeEvidenceFacts.Encode(clues)), Is.EqualTo(clues));
            }
        }

        [Test]
        public void NewDiscoveriesAccumulateWithoutErasingPriorEvidence()
        {
            var found = M02BridgeEvidence.None;
            found = M02BridgeEvidenceFacts.MergeObserved(found, M02BridgeEvidence.CleanCableCut);
            found = M02BridgeEvidenceFacts.MergeObserved(found, M02BridgeEvidence.FerrymanLog);
            found = M02BridgeEvidenceFacts.MergeObserved(found, M02BridgeEvidence.CleanCableCut);
            Assert.That(found, Is.EqualTo(M02BridgeEvidence.CleanCableCut |
                M02BridgeEvidence.FerrymanLog));
            Assert.That(M02BridgeEvidenceFacts.MergeObserved(
                found, M02BridgeEvidence.WarReportDraft),
                Is.EqualTo(found | M02BridgeEvidence.WarReportDraft));
        }

        [Test]
        public void InvalidOrSyntheticSingleCluesCannotBecomeSavedFacts()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                M02BridgeEvidenceFacts.Encode((M02BridgeEvidence)16));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                M02BridgeEvidenceFacts.MergeObserved(
                    M02BridgeEvidence.None, M02BridgeEvidence.None));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                M02BridgeEvidenceFacts.MergeObserved(
                    M02BridgeEvidence.None, M02BridgeEvidence.CleanCableCut |
                        M02BridgeEvidence.ThirdBellSignal));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                M02BridgeEvidenceFacts.TryRecordObserved((M02BridgeEvidence)32));
        }
    }
}
