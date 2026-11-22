using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgLargeMapChunkRendererLifecycleTests
    {
        private GameObject root;
        private RpgLargeMapChunkRenderer renderer;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("MapChunkRendererLifecycleRoot");
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
        public void ExternalParentTeardown_SetsIsDisposedImmediately_AndDisposeReleasesNativeAssets()
        {
            renderer = RpgLargeMapChunkRenderer.BuildForWhitewaterCrossing(
                root.transform, WhitewaterCrossingMapLayout.Build());

            var sprites = new List<Sprite>();
            var textures = new List<Texture2D>();
            foreach (SpriteRenderer sr in renderer.RootObject.GetComponentsInChildren<SpriteRenderer>())
            {
                if (sr.sprite != null)
                {
                    sprites.Add(sr.sprite);
                    if (sr.sprite.texture != null)
                        textures.Add(sr.sprite.texture);
                }
            }

            Assert.That(sprites.Count, Is.GreaterThan(0));
            Assert.That(textures.Count, Is.GreaterThan(0));
            Assert.That(renderer.IsDisposed, Is.False);

            UnityEngine.Object.DestroyImmediate(root);
            root = null;
            Assert.That(renderer.IsDisposed, Is.True);

            Assert.DoesNotThrow(() => renderer.Dispose());
            foreach (Sprite sprite in sprites)
                Assert.That(sprite == null, Is.True, "Owned Sprite must be destroyed even after external parent teardown.");
            foreach (Texture2D texture in textures)
                Assert.That(texture == null, Is.True, "Owned Texture2D must be destroyed even after external parent teardown.");
        }

        [Test]
        public void Dispose_IsIdempotent_AndClearsChunkAndHaloCounts()
        {
            renderer = RpgLargeMapChunkRenderer.BuildForWhitewaterCrossing(
                root.transform, WhitewaterCrossingMapLayout.Build());

            Assert.That(renderer.ChunkCount, Is.EqualTo(6));
            Assert.That(renderer.LightHaloCount, Is.EqualTo(4));

            Assert.DoesNotThrow(() => renderer.Dispose());
            Assert.DoesNotThrow(() => renderer.Dispose());
            Assert.That(renderer.IsDisposed, Is.True);
            Assert.That(renderer.ChunkCount, Is.Zero);
            Assert.That(renderer.LightHaloCount, Is.Zero);
            Assert.That(root.transform.childCount, Is.Zero);
        }

        [Test]
        public void FactoryNullArguments_ThrowWithoutLeakingChildren()
        {
            Assert.Throws<ArgumentNullException>(() =>
                RpgLargeMapChunkRenderer.BuildForWhitewaterCrossing(null, WhitewaterCrossingMapLayout.Build()));
            Assert.Throws<ArgumentNullException>(() =>
                RpgLargeMapChunkRenderer.BuildForWhitewaterCrossing(root.transform, null));
            Assert.That(root.transform.childCount, Is.Zero);
        }

        [Test]
        public void BuildForWhitewaterCrossing_WithNullSlicesArray_UsesFallbackPaletteCleanly()
        {
            renderer = RpgLargeMapChunkRenderer.BuildForWhitewaterCrossing(
                root.transform, WhitewaterCrossingMapLayout.Build(), new Sprite[] { null });

            Assert.That(renderer.UsedRealArtSlices, Is.False);
            Assert.That(renderer.ChunkCount, Is.EqualTo(6));
            foreach (SpriteRenderer sr in renderer.RootObject.GetComponentsInChildren<SpriteRenderer>())
            {
                Assert.That(sr.sprite, Is.Not.Null);
                Assert.That(sr.sprite.texture.filterMode, Is.EqualTo(FilterMode.Point));
            }
        }
    }
}
