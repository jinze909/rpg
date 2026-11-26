using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionPropAtlasAndSpawnerE2ETests
    {
        [Test]
        public void SharedProceduralPropAtlasAndSpawners_StreamLootSwapAndDisposeAcrossAllFourRegionsWithZeroLeak()
        {
            var root = new GameObject("World500xFourRegionPropSpawnerE2ERoot");
            try
            {
                int texBefore = Resources.FindObjectsOfTypeAll<Texture2D>().Length;
                int spriteBefore = Resources.FindObjectsOfTypeAll<Sprite>().Length;

                var store = new Dictionary<string, int>(StringComparer.Ordinal);
                Func<string, int> read = k => store.TryGetValue(k, out int v) ? v : 0;
                Action<string, int> write = (k, v) => store[k] = v;

                using (var sharedAtlas = new RpgWorld500xProceduralPropAtlas())
                {
                    foreach (RpgExpandedMapSpec spec in RpgExpandedWorldScale.AllSpecs)
                    {
                        RpgWorld500xMapLayout layout = RpgWorld500xMapLayout.Build(spec.MapId);
                        var ctrl = new RpgWorld500xCachesAndShortcutsController(layout, read, write);
                        IReadOnlyList<RpgSupplyCacheDef> caches = RpgWorld500xContentCatalog.GetCaches(spec.MapId);
                        Assert.Greater(caches.Count, 0);
                        RpgSupplyCacheDef firstCache = caches[0];

                        using (var spawner = new RpgWorld500xPropVisualSpawner(layout, root.transform, ctrl, sharedAtlas))
                        {
                            Assert.Greater(spawner.TotalIndexedPropCount, 0);

                            Vector2 cachePos = new Vector2(firstCache.Tile.x + 0.5f, firstCache.Tile.y + 0.5f);
                            Assert.IsTrue(spawner.UpdateFocus(cachePos, streamingRadiusChunks: 2));
                            Assert.Greater(spawner.ActiveVisualCount, 0);
                            Assert.IsTrue(spawner.TryGetActiveRenderer(firstCache.Id, out SpriteRenderer cacheRenderer));
                            Sprite closedSprite = cacheRenderer.sprite;
                            Assert.IsNotNull(closedSprite);

                            // Looting the cache swaps its sprite in place on same-chunk UpdateFocus.
                            Assert.IsTrue(ctrl.TryLootNearbyCache(cachePos, 6f, out _));
                            Assert.IsTrue(spawner.UpdateFocus(cachePos, streamingRadiusChunks: 2));
                            Assert.IsTrue(spawner.TryGetActiveRenderer(firstCache.Id, out SpriteRenderer sameRenderer));
                            Assert.AreSame(cacheRenderer, sameRenderer);
                            Assert.IsFalse(ReferenceEquals(closedSprite, sameRenderer.sprite));

                            // Expanding to full map radius streams every catalogued prop cleanly.
                            int mapRadius = Math.Max(spec.ExpandedWidth, spec.ExpandedHeight) /
                                            RpgLargeMapChunkRenderer.ChunkSizeTiles;
                            Assert.IsTrue(spawner.UpdateFocus(cachePos, mapRadius));
                            Assert.AreEqual(spawner.TotalIndexedPropCount, spawner.ActiveVisualCount);
                        }

                        // Non-owning spawner disposal must leave shared atlas intact and remove all child GameObjects.
                        Assert.IsFalse(sharedAtlas.IsDisposed);
                        Assert.AreEqual(0, root.transform.childCount);
                    }

                    Assert.Greater(sharedAtlas.CachedTextureCount, 0);
                }

                Assert.AreEqual(0, root.transform.childCount);
                Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Texture2D>().Length, texBefore);
                Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Sprite>().Length, spriteBefore);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
