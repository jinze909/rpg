using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    /// <summary>
    /// Four-region E2E regression tests for RpgWorld500xPropVisualSpawner verifying
    /// shared-atlas sprite spawning, same-chunk loot sprite swapping without respawn,
    /// invalid focus/radius rejection, map-sized window coverage and culling, and
    /// zero Texture2D/Sprite leaks across Soundwell, RedSaltHarbor, WhitewaterCrossing,
    /// and MineCamp.
    /// </summary>
    public sealed class RpgWorld500xFourRegionPropVisualSpawnerE2ETests
    {
        [Test]
        public void AllFourRegions_PropVisualSpawner_SwapsLootedSpritesGuardsInvalidFocusAndDisposesWithoutLeaks()
        {
            IReadOnlyList<RpgExpandedMapSpec> specs = RpgExpandedWorldScale.AllSpecs;
            Assert.AreEqual(4, specs.Count);

            int texBefore = Resources.FindObjectsOfTypeAll<Texture2D>().Length;
            int spriteBefore = Resources.FindObjectsOfTypeAll<Sprite>().Length;

            using (var sharedAtlas = new RpgWorld500xProceduralPropAtlas())
            {
                for (int r = 0; r < specs.Count; r++)
                {
                    RpgExpandedMapSpec spec = specs[r];
                    var root = new GameObject("FourRegionPropSpawner_" + spec.MapId);
                    RpgWorld500xPropVisualSpawner spawner = null;

                    try
                    {
                        var store = new Dictionary<string, int>(StringComparer.Ordinal);
                        RpgWorld500xMapLayout layout = RpgWorld500xMapLayout.Build(spec.MapId);
                        var ctrl = new RpgWorld500xCachesAndShortcutsController(
                            layout,
                            k => store.TryGetValue(k, out int v) ? v : 0,
                            (k, v) => store[k] = v);

                        spawner = new RpgWorld500xPropVisualSpawner(layout, root.transform, ctrl, sharedAtlas);
                        Assert.Greater(spawner.TotalIndexedPropCount, 0,
                            $"Expected indexed props on {spec.MapId}.");

                        RpgSupplyCacheDef firstCache = RpgWorld500xContentCatalog.GetCaches(spec.MapId)[0];
                        var cachePos = new Vector2(firstCache.Tile.x + 0.5f, firstCache.Tile.y + 0.5f);

                        Assert.IsTrue(spawner.UpdateFocus(cachePos, streamingRadiusChunks: 1),
                            $"Initial focus failed on {spec.MapId}.");
                        Assert.Greater(spawner.ActiveVisualCount, 0);
                        Assert.IsTrue(spawner.TryGetActiveRenderer(firstCache.Id, out SpriteRenderer cacheRenderer),
                            $"Expected active renderer for cache {firstCache.Id} on {spec.MapId}.");
                        Sprite beforeLoot = cacheRenderer.sprite;
                        Assert.IsNotNull(beforeLoot);

                        // Loot cache and verify same-chunk UpdateFocus swaps sprite without respawning GameObject.
                        Assert.IsTrue(ctrl.TryLootNearbyCache(cachePos, 6f, out _),
                            $"Failed to loot cache {firstCache.Id} on {spec.MapId}.");
                        Assert.IsTrue(spawner.UpdateFocus(cachePos, streamingRadiusChunks: 1));
                        Assert.IsTrue(spawner.TryGetActiveRenderer(firstCache.Id, out SpriteRenderer sameRenderer));
                        Assert.AreSame(cacheRenderer, sameRenderer,
                            $"Same-chunk refresh respawned renderer on {spec.MapId}.");
                        Assert.IsFalse(ReferenceEquals(beforeLoot, cacheRenderer.sprite),
                            $"Looted cache sprite did not swap on {spec.MapId}.");

                        // Invalid focus coordinates or radii must not alter the active window.
                        int retainedCount = spawner.ActiveVisualCount;
                        Assert.IsFalse(spawner.UpdateFocus(new Vector2(float.NaN, cachePos.y), 1));
                        Assert.IsFalse(spawner.UpdateFocus(new Vector2(-0.1f, cachePos.y), 1));
                        Assert.IsFalse(spawner.UpdateFocus(new Vector2(spec.ExpandedWidth, cachePos.y), 1));
                        Assert.IsFalse(spawner.UpdateFocus(new Vector2(cachePos.x, spec.ExpandedHeight), 1));
                        Assert.IsFalse(spawner.UpdateFocus(cachePos, int.MaxValue));
                        Assert.AreEqual(retainedCount, spawner.ActiveVisualCount);
                        Assert.IsTrue(spawner.TryGetActiveRenderer(firstCache.Id, out SpriteRenderer stillRetained));
                        Assert.AreSame(cacheRenderer, stillRetained);

                        // Map-sized window spawns all indexed props; shrinking back to radius 1 culls cleanly.
                        int mapRadius = Math.Max(spec.ExpandedWidth, spec.ExpandedHeight) /
                            RpgLargeMapChunkRenderer.ChunkSizeTiles;
                        Assert.IsTrue(spawner.UpdateFocus(cachePos, mapRadius));
                        Assert.AreEqual(spawner.TotalIndexedPropCount, spawner.ActiveVisualCount,
                            $"Map-sized window did not activate all indexed props on {spec.MapId}.");
                        Assert.IsTrue(spawner.UpdateFocus(cachePos, streamingRadiusChunks: 1));
                        Assert.AreEqual(retainedCount, spawner.ActiveVisualCount,
                            $"Shrinking window back to radius 1 did not restore active count on {spec.MapId}.");

                        // Idempotent disposal leaves shared atlas intact for subsequent regions.
                        spawner.Dispose();
                        spawner.Dispose();
                        Assert.IsTrue(spawner.IsDisposed);
                        Assert.AreEqual(0, spawner.ActiveVisualCount);
                        Assert.IsFalse(spawner.UpdateFocus(cachePos, 1));
                        spawner = null;
                    }
                    finally
                    {
                        spawner?.Dispose();
                        UnityEngine.Object.DestroyImmediate(root);
                    }
                }
            }

            Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Texture2D>().Length, texBefore,
                "Texture2D leak detected after four-region prop visual spawner sweep.");
            Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Sprite>().Length, spriteBefore,
                "Sprite leak detected after four-region prop visual spawner sweep.");
        }
    }
}
