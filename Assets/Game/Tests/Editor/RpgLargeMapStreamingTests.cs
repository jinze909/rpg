using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgLargeMapStreamingTests
    {
        private GameObject root;
        private RpgLargeMapChunkRenderer renderer;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("StreamingChunkRendererTestRoot");
        }

        [TearDown]
        public void TearDown()
        {
            renderer?.Dispose();
            renderer = null;
            if (root != null)
                UnityEngine.Object.DestroyImmediate(root);
        }

        [Test]
        public void Streaming500xMapBoundsActiveChunksUnloadsOldTexturesAndCullsLightHalos()
        {
            const int width = 3136;
            const int height = 1888;
            var lights = new[]
            {
                new RpgLight2DAnchor("light.west", new Vector2Int(360, 540), 10f, 1f, Color.yellow),
                new RpgLight2DAnchor("light.east", new Vector2Int(2800, 1600), 10f, 1f, Color.yellow)
            };

            renderer = RpgLargeMapChunkRenderer.BuildStreaming(
                root.transform,
                "Harbor 500x Streaming",
                width,
                height,
                (x, y) => (x + y) % 2 == 0 ? (int)HarborTileKind.QuayStone : (int)HarborTileKind.WaterCalm,
                useHarborPalette: true,
                lights: lights,
                initialFocusTile: new Vector2(360f, 540f),
                streamingRadiusChunks: 1,
                tileSlices: new Sprite[] { null });

            Assert.That(renderer.IsStreaming, Is.True);
            Assert.That(renderer.TotalPossibleChunks, Is.EqualTo(98 * 59));
            Assert.That(renderer.ChunkCount, Is.LessThanOrEqualTo(9),
                "Radius 1 streaming window must keep at most 3x3 = 9 active chunks on a 500x map.");
            Assert.That(renderer.ChunkCount, Is.EqualTo(9));

            var initialTextures = new List<Texture2D>();
            var initialSprites = new List<Sprite>();
            foreach (SpriteRenderer sr in renderer.RootObject.GetComponentsInChildren<SpriteRenderer>(false))
            {
                if (sr.gameObject.name.StartsWith("Chunk_", StringComparison.Ordinal) && sr.sprite != null)
                {
                    initialSprites.Add(sr.sprite);
                    if (sr.sprite.texture != null)
                        initialTextures.Add(sr.sprite.texture);
                }
            }
            Assert.That(initialTextures.Count, Is.EqualTo(9));

            Transform westHalo = renderer.RootObject.transform.Find("Halo_light.west");
            Transform eastHalo = renderer.RootObject.transform.Find("Halo_light.east");
            Assert.That(westHalo, Is.Not.Null);
            Assert.That(eastHalo, Is.Not.Null);
            Assert.That(westHalo.gameObject.activeSelf, Is.True);
            Assert.That(eastHalo.gameObject.activeSelf, Is.False);

            // Non-finite focus must be rejected without disturbing active chunks.
            Assert.That(renderer.UpdateStreamingFocus(new Vector2(float.NaN, 100f)), Is.False);
            Assert.That(renderer.UpdateStreamingFocus(new Vector2(100f, float.PositiveInfinity)), Is.False);
            Assert.That(renderer.ChunkCount, Is.EqualTo(9));

            // Fast-travel across 2,500+ tiles must destroy all distant chunk textures and swap visible halos.
            Assert.That(renderer.UpdateStreamingFocus(new Vector2(2800f, 1600f)), Is.True);
            Assert.That(renderer.ChunkCount, Is.EqualTo(9));
            foreach (Texture2D oldTex in initialTextures)
                Assert.That(oldTex == null, Is.True, "Unloaded chunk Texture2D must be destroyed.");
            foreach (Sprite oldSprite in initialSprites)
                Assert.That(oldSprite == null, Is.True, "Unloaded chunk Sprite must be destroyed.");
            Assert.That(westHalo.gameObject.activeSelf, Is.False);
            Assert.That(eastHalo.gameObject.activeSelf, Is.True);

            renderer.Dispose();
            Assert.That(renderer.IsDisposed, Is.True);
            Assert.That(renderer.ChunkCount, Is.Zero);
            Assert.That(renderer.UpdateStreamingFocus(new Vector2(360f, 540f)), Is.False);
        }

        [Test]
        public void BuildStreamingRejectsInvalidArgumentsWithoutLeakingChildren()
        {
            Assert.Throws<ArgumentNullException>(() => RpgLargeMapChunkRenderer.BuildStreaming(
                null, "x", 64, 64, (x, y) => 0, false, null, Vector2.zero));
            Assert.Throws<ArgumentNullException>(() => RpgLargeMapChunkRenderer.BuildStreaming(
                root.transform, "x", 64, 64, null, false, null, Vector2.zero));
            Assert.Throws<ArgumentOutOfRangeException>(() => RpgLargeMapChunkRenderer.BuildStreaming(
                root.transform, "x", 0, 64, (x, y) => 0, false, null, Vector2.zero));
            Assert.Throws<ArgumentOutOfRangeException>(() => RpgLargeMapChunkRenderer.BuildStreaming(
                root.transform, "x", 64, 64, (x, y) => 0, false, null, Vector2.zero, streamingRadiusChunks: -1));
            Assert.That(root.transform.childCount, Is.Zero);
        }
    }
}
