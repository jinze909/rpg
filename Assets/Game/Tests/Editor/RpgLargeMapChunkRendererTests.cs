using System;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgLargeMapChunkRendererTests
    {
        [Test]
        public void Soundwell96x60BuildsSixPointFilteredChunksAndFourLightHalos()
        {
            var root = new GameObject("Soundwell chunk renderer test");
            RpgLargeMapChunkRenderer renderer = null;
            try
            {
                SoundwellLargeMapLayout layout = SoundwellLargeMapLayout.Build();
                renderer = RpgLargeMapChunkRenderer.BuildForSoundwell(root.transform, layout);

                // 96/32 = 3 columns, ceil(60/32) = 2 rows => 6 chunks.
                Assert.That(renderer.ChunkCount, Is.EqualTo(6));
                Assert.That(renderer.LightHaloCount, Is.EqualTo(layout.PointLights.Count));

                SpriteRenderer[] renderers = renderer.RootObject.GetComponentsInChildren<SpriteRenderer>();
                Assert.That(renderers.Length, Is.EqualTo(6 + layout.PointLights.Count));
                foreach (SpriteRenderer sr in renderers)
                {
                    Assert.That(sr.sprite, Is.Not.Null);
                    Assert.That(sr.sprite.texture.filterMode, Is.EqualTo(FilterMode.Point));
                    Assert.That(sr.sprite.pixelsPerUnit, Is.EqualTo(RpgWorldMapScale.PixelsPerUnit));
                }

                renderer.Dispose();
                Assert.That(renderer.IsDisposed, Is.True);
                Assert.That(root.transform.childCount, Is.Zero);
            }
            finally
            {
                renderer?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void RedSaltHarborAndWhitewaterCrossingBuildExpectedChunkGridsAndCleanUp()
        {
            var root = new GameObject("Harbor and Whitewater chunk test");
            RpgLargeMapChunkRenderer harbor = null;
            RpgLargeMapChunkRenderer crossing = null;
            try
            {
                // 140x84 => ceil(140/32)=5 x ceil(84/32)=3 => 15 chunks.
                harbor = RpgLargeMapChunkRenderer.BuildForRedSaltHarbor(
                    root.transform, RedSaltHarborMapLayout.Build());
                Assert.That(harbor.ChunkCount, Is.EqualTo(15));
                Assert.That(harbor.LightHaloCount, Is.EqualTo(4));

                // 80x44 => ceil(80/32)=3 x ceil(44/32)=2 => 6 chunks.
                crossing = RpgLargeMapChunkRenderer.BuildForWhitewaterCrossing(
                    root.transform, WhitewaterCrossingMapLayout.Build());
                Assert.That(crossing.ChunkCount, Is.EqualTo(6));
                Assert.That(crossing.LightHaloCount, Is.EqualTo(4));

                Assert.Throws<ArgumentNullException>(() =>
                    RpgLargeMapChunkRenderer.BuildForSoundwell(null, SoundwellLargeMapLayout.Build()));
                Assert.Throws<ArgumentNullException>(() =>
                    RpgLargeMapChunkRenderer.BuildForRedSaltHarbor(root.transform, null));
            }
            finally
            {
                harbor?.Dispose();
                crossing?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
