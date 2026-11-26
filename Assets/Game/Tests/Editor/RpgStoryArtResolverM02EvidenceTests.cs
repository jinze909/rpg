using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgStoryArtResolverM02EvidenceTests
    {
        [Test]
        public void ResolveM02EvidenceSprite_FerrymanLog_ReturnsBridgeKeeperOpenLedger()
        {
            RpgPixelArtManifest manifest = RpgPixelArtManifest.LoadDefault();
            Assert.That(manifest, Is.Not.Null);

            Sprite sprite = RpgStoryArtResolver.ResolveM02EvidenceSprite(
                M02BridgeEvidence.FerrymanLog, manifest);
            Assert.That(sprite, Is.Not.Null);
            Assert.That(sprite, Is.EqualTo(manifest.M02BridgeKeeperOpenLedger));
            Assert.That(sprite.texture.filterMode, Is.EqualTo(FilterMode.Point));
        }

        [Test]
        public void ResolveM02EvidenceSprite_PreservesCableCutWarReportAndUnmappedClues()
        {
            RpgPixelArtManifest manifest = RpgPixelArtManifest.LoadDefault();
            Assert.That(manifest, Is.Not.Null);

            Assert.That(
                RpgStoryArtResolver.ResolveM02EvidenceSprite(M02BridgeEvidence.CleanCableCut, manifest),
                Is.EqualTo(manifest.M02CableFlatUncharredCut));
            Assert.That(
                RpgStoryArtResolver.ResolveM02EvidenceSprite(M02BridgeEvidence.WarReportDraft, manifest),
                Is.EqualTo(manifest.M02OverwrittenBattleReport));
            Assert.That(
                RpgStoryArtResolver.ResolveM02EvidenceSprite(M02BridgeEvidence.None, manifest),
                Is.Null);
            Assert.That(
                RpgStoryArtResolver.ResolveM02EvidenceSprite(M02BridgeEvidence.ThirdBellSignal, manifest),
                Is.Null);
        }

        [Test]
        public void ResolveM02EvidenceSprite_EmptyManifest_ReturnsNull()
        {
            var emptyManifest = ScriptableObject.CreateInstance<RpgPixelArtManifest>();
            try
            {
                Assert.That(
                    RpgStoryArtResolver.ResolveM02EvidenceSprite(M02BridgeEvidence.FerrymanLog, emptyManifest),
                    Is.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(emptyManifest);
            }
        }
    }
}
