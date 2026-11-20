using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgPixelArtManifestTests
    {
        [Test]
        public void DefaultManifestLoadsAllAuthoredTilesetsAndStoryCastSprites()
        {
            RpgPixelArtManifest manifest = RpgPixelArtManifest.LoadDefault();
            Assert.That(manifest, Is.Not.Null,
                "RpgPixelArtManifest must load from Resources/Soundwell/RpgPixelArtManifest.");

            Assert.That(manifest.SoundwellTiles, Is.Not.Null);
            Assert.That(manifest.SoundwellTiles.Length, Is.EqualTo(16));
            for (int i = 0; i < manifest.SoundwellTiles.Length; i++)
            {
                Sprite s = manifest.SoundwellTiles[i];
                Assert.That(s, Is.Not.Null, "Soundwell tile slice " + i);
                Assert.That(s.texture.isReadable, Is.True, "Soundwell tileset must be readable for chunk blitting.");
                Assert.That(Mathf.RoundToInt(s.textureRect.width), Is.EqualTo(RpgWorldMapScale.PixelsPerUnit));
                Assert.That(Mathf.RoundToInt(s.textureRect.height), Is.EqualTo(RpgWorldMapScale.PixelsPerUnit));
            }

            Assert.That(manifest.HarborTiles, Is.Not.Null);
            Assert.That(manifest.HarborTiles.Length, Is.EqualTo(16));
            for (int i = 0; i < manifest.HarborTiles.Length; i++)
            {
                Sprite s = manifest.HarborTiles[i];
                Assert.That(s, Is.Not.Null, "Harbor tile slice " + i);
                Assert.That(s.texture.isReadable, Is.True, "Harbor tileset must be readable for chunk blitting.");
                Assert.That(Mathf.RoundToInt(s.textureRect.width), Is.EqualTo(RpgWorldMapScale.PixelsPerUnit));
                Assert.That(Mathf.RoundToInt(s.textureRect.height), Is.EqualTo(RpgWorldMapScale.PixelsPerUnit));
            }

            Assert.That(manifest.FerryGuideIdle, Is.Not.Null);
            Assert.That(manifest.FerryGuideWalk1, Is.Not.Null);
            Assert.That(manifest.FerryGuideWalk2, Is.Not.Null);
            Assert.That(manifest.FerryGuideBrace, Is.Not.Null);
            Assert.That(manifest.RescueRiggerIdle, Is.Not.Null);
            Assert.That(manifest.SoundBorrowingBeastIdle, Is.Not.Null);
            Assert.That(manifest.SoundBorrowingBeastAlert, Is.Not.Null);
            Assert.That(manifest.BridgeGuardIdle, Is.Not.Null);
            Assert.That(manifest.BridgeGuardThrust, Is.Not.Null);
            Assert.That(manifest.ShenLiBridgeIdle, Is.Not.Null);
            Assert.That(manifest.ShenLiBraceCable, Is.Not.Null);
            Assert.That(manifest.SeranLetterCarrier, Is.Not.Null);
            Assert.That(manifest.HarborRepairer, Is.Not.Null);
            Assert.That(manifest.OuterHarborFisher, Is.Not.Null);
            Assert.That(manifest.MiningFamilySaltVendor, Is.Not.Null);
        }

        [Test]
        public void AllThreeLargeMapChunkRenderersUseRealPixelArtSlicesByDefault()
        {
            var root = new GameObject("Real art chunk renderer test");
            RpgLargeMapChunkRenderer soundwell = null;
            RpgLargeMapChunkRenderer harbor = null;
            RpgLargeMapChunkRenderer crossing = null;
            try
            {
                soundwell = RpgLargeMapChunkRenderer.BuildForSoundwell(
                    root.transform, SoundwellLargeMapLayout.Build());
                harbor = RpgLargeMapChunkRenderer.BuildForRedSaltHarbor(
                    root.transform, RedSaltHarborMapLayout.Build());
                crossing = RpgLargeMapChunkRenderer.BuildForWhitewaterCrossing(
                    root.transform, WhitewaterCrossingMapLayout.Build());

                Assert.That(soundwell.UsedRealArtSlices, Is.True);
                Assert.That(harbor.UsedRealArtSlices, Is.True);
                Assert.That(crossing.UsedRealArtSlices, Is.True);
            }
            finally
            {
                soundwell?.Dispose();
                harbor?.Dispose();
                crossing?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
