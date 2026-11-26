using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xPropVisualSpawnerTests
    {
        [Test]
        public void UpdateFocus_SpawnsWindowedPropsSwapsLootedSpriteAndDisposesWithZeroLeak()
        {
            var root = new GameObject("PropVisualSpawnerTestRoot");
            RpgWorld500xPropVisualSpawner spawner = null;
            try
            {
                int texBefore = Resources.FindObjectsOfTypeAll<Texture2D>().Length;
                int spriteBefore = Resources.FindObjectsOfTypeAll<Sprite>().Length;

                var store = new Dictionary<string, int>(StringComparer.Ordinal);
                RpgWorld500xMapLayout soundwell = RpgWorld500xMapLayout.Build(RpgWorldMapId.Soundwell);
                var ctrl = new RpgWorld500xCachesAndShortcutsController(
                    soundwell,
                    k => store.TryGetValue(k, out int v) ? v : 0,
                    (k, v) => store[k] = v);

                spawner = new RpgWorld500xPropVisualSpawner(soundwell, root.transform, ctrl);
                Assert.GreaterOrEqual(spawner.TotalIndexedPropCount, 29);

                // Focus near sw_c03 (290, 780)
                Assert.IsTrue(spawner.UpdateFocus(new Vector2(290.5f, 780.5f), streamingRadiusChunks: 2));
                Assert.Greater(spawner.ActiveVisualCount, 0);
                Assert.Less(spawner.ActiveVisualCount, spawner.TotalIndexedPropCount);

                Assert.IsTrue(spawner.TryGetActiveRenderer("sw_c03", out SpriteRenderer relicRenderer));
                Sprite beforeLoot = relicRenderer.sprite;
                Assert.IsNotNull(beforeLoot);

                // Loot cache and refresh sprite -> should switch to RelicShrineLooted
                Assert.IsTrue(ctrl.TryLootNearbyCache(new Vector2(290.5f, 780.5f), 6f, out _));
                // Same-chunk focus must refresh state without respawning the prop.
                Assert.IsTrue(spawner.UpdateFocus(new Vector2(291.5f, 780.5f), 2));
                Assert.IsTrue(spawner.TryGetActiveRenderer("sw_c03", out SpriteRenderer sameRenderer));
                Assert.AreSame(relicRenderer, sameRenderer);
                Assert.IsFalse(ReferenceEquals(beforeLoot, relicRenderer.sprite));

                // Pan far away -> sw_c03 should be culled
                Assert.IsTrue(spawner.UpdateFocus(new Vector2(1940.5f, 620.5f), streamingRadiusChunks: 1));
                Assert.IsFalse(spawner.TryGetActiveRenderer("sw_c03", out _));
                Assert.IsTrue(spawner.TryGetActiveRenderer("sw_c11", out _));

                // Invalid coordinates or radii cannot disturb the current window.
                int retainedCount = spawner.ActiveVisualCount;
                Assert.IsTrue(spawner.TryGetActiveRenderer("sw_c11", out SpriteRenderer retained));
                Assert.IsFalse(spawner.UpdateFocus(new Vector2(float.NaN, 0f), 1));
                Assert.IsFalse(spawner.UpdateFocus(new Vector2(float.MaxValue, 620f), 1));
                Assert.IsFalse(spawner.UpdateFocus(new Vector2(-0.1f, 620f), 1));
                Assert.IsFalse(spawner.UpdateFocus(new Vector2(290f, 780f), int.MaxValue));
                Assert.AreEqual(retainedCount, spawner.ActiveVisualCount);
                Assert.IsTrue(spawner.TryGetActiveRenderer("sw_c11", out SpriteRenderer stillRetained));
                Assert.AreSame(retained, stillRetained);

                // A valid map-sized window enumerates only catalogued props.
                int mapRadius = Math.Max(soundwell.Spec.ExpandedWidth,
                    soundwell.Spec.ExpandedHeight) / RpgLargeMapChunkRenderer.ChunkSizeTiles;
                Assert.IsTrue(spawner.UpdateFocus(new Vector2(1940.5f, 620.5f), mapRadius));
                Assert.AreEqual(spawner.TotalIndexedPropCount, spawner.ActiveVisualCount);
                Assert.IsTrue(spawner.UpdateFocus(new Vector2(1940.5f, 620.5f), 1));
                Assert.AreEqual(retainedCount, spawner.ActiveVisualCount);

                spawner.Dispose();
                spawner.Dispose();
                Assert.IsTrue(spawner.IsDisposed);
                Assert.AreEqual(0, spawner.ActiveVisualCount);
                spawner = null;

                Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Texture2D>().Length, texBefore);
                Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Sprite>().Length, spriteBefore);
            }
            finally
            {
                spawner?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
