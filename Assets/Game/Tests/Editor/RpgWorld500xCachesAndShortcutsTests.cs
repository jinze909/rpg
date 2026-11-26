using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xCachesAndShortcutsTests
    {
        [Test]
        public void All42CachesAnd16ShortcutsAreUniqueInBoundsWalkableAndCollisionFree()
        {
            var globalIds = new HashSet<string>(StringComparer.Ordinal);
            var globalKeys = new HashSet<string>(StringComparer.Ordinal);
            int totalCaches = 0;
            int totalShortcuts = 0;

            foreach (RpgExpandedMapSpec spec in RpgExpandedWorldScale.AllSpecs)
            {
                RpgWorld500xMapLayout map = RpgWorld500xMapLayout.Build(spec.MapId);
                var markerTiles = new HashSet<Vector2Int>();
                for (int i = 0; i < map.Markers.Count; i++)
                    markerTiles.Add(map.Markers[i].Tile);

                IReadOnlyList<RpgSupplyCacheDef> caches = RpgWorld500xContentCatalog.GetCaches(spec.MapId);
                IReadOnlyList<RpgWorldShortcutDef> shortcuts = RpgWorld500xContentCatalog.GetShortcuts(spec.MapId);

                Assert.That(caches.Count, Is.GreaterThanOrEqualTo(8), spec.DisplayNameEn);
                Assert.That(shortcuts.Count, Is.EqualTo(4), spec.DisplayNameEn);

                foreach (RpgSupplyCacheDef c in caches)
                {
                    Assert.That(globalIds.Add(c.Id), Is.True, c.Id);
                    Assert.That(globalKeys.Add(c.MakinomKey), Is.True, c.MakinomKey);
                    Assert.That(c.MakinomKey.StartsWith("rpg.return.world.cache.", StringComparison.Ordinal), Is.True);
                    Assert.That(map.IsWalkable(c.Tile.x, c.Tile.y, gateOpen: true), Is.True,
                        spec.DisplayNameEn + " cache must sit on walkable tile: " + c.Id);
                    Assert.That(markerTiles.Contains(c.Tile), Is.False,
                        "Cache must not collide with a landmark tile: " + c.Id);
                    Assert.That(c.SaltSilverReward, Is.GreaterThan(0), c.Id);
                }

                foreach (RpgWorldShortcutDef sc in shortcuts)
                {
                    Assert.That(globalIds.Add(sc.Id), Is.True, sc.Id);
                    Assert.That(globalKeys.Add(sc.MakinomKey), Is.True, sc.MakinomKey);
                    Assert.That(sc.MakinomKey.StartsWith("rpg.return.world.shortcut.", StringComparison.Ordinal), Is.True);
                    Assert.That(map.IsWalkable(sc.TriggerTile.x, sc.TriggerTile.y, gateOpen: true), Is.True,
                        spec.DisplayNameEn + " shortcut trigger must be walkable: " + sc.Id);
                    Assert.That(sc.UnlockSpan.width, Is.GreaterThan(0), sc.Id);
                    Assert.That(sc.UnlockSpan.height, Is.GreaterThan(0), sc.Id);
                }

                totalCaches += caches.Count;
                totalShortcuts += shortcuts.Count;
            }

            Assert.That(totalCaches, Is.EqualTo(42));
            Assert.That(totalShortcuts, Is.EqualTo(16));
        }

        [Test]
        public void CachesLootIdempotentlyAndShortcutsMutate500xGridAndPersistAcrossReload()
        {
            var store = new Dictionary<string, int>(StringComparer.Ordinal);
            Func<string, int> read = k => store.TryGetValue(k, out int v) ? v : 0;
            Action<string, int> write = (k, v) => store[k] = v;

            RpgWorld500xMapLayout soundwell = RpgWorld500xMapLayout.Build(RpgWorldMapId.Soundwell);
            int rewardCalls = 0;
            var ctrl = new RpgWorld500xCachesAndShortcutsController(
                soundwell, read, write, _ => rewardCalls++);

            // Verify shortcut wall is initially blocked, then opens permanently on unlock.
            Assert.That(soundwell.IsWalkable(368, 860, gateOpen: false), Is.False);
            Assert.That(ctrl.TryUnlockNearbyShortcut(new Vector2(380.5f, 920.5f), 8f, out RpgWorldShortcutDef sc), Is.True);
            Assert.That(sc.Id, Is.EqualTo("sw_sc_entry_north"));
            Assert.That(soundwell.IsWalkable(368, 860, gateOpen: false), Is.True);
            Assert.That(ctrl.TryUnlockNearbyShortcut(new Vector2(380.5f, 920.5f), 8f, out _), Is.False);

            // Loot cache once, reject duplicate loot.
            Assert.That(ctrl.TryLootNearbyCache(new Vector2(290.5f, 780.5f), 6f, out RpgSupplyCacheDef relic), Is.True);
            Assert.That(relic.Id, Is.EqualTo("sw_c03"));
            Assert.That(relic.Kind, Is.EqualTo(RpgSupplyCacheKind.FieldRelic));
            Assert.That(rewardCalls, Is.EqualTo(1));
            Assert.That(ctrl.TryLootNearbyCache(new Vector2(290.5f, 780.5f), 6f, out _), Is.False);
            Assert.That(rewardCalls, Is.EqualTo(1));

            // Reload on a fresh 500x Soundwell map restores both looted cache and unlocked shortcut tiles.
            RpgWorld500xMapLayout reloadedMap = RpgWorld500xMapLayout.Build(RpgWorldMapId.Soundwell);
            var reloadedCtrl = new RpgWorld500xCachesAndShortcutsController(
                reloadedMap, read, write, _ => rewardCalls++);
            Assert.That(reloadedCtrl.IsCacheLooted("sw_c03"), Is.True);
            Assert.That(reloadedCtrl.IsShortcutUnlocked("sw_sc_entry_north"), Is.True);
            Assert.That(reloadedMap.IsWalkable(368, 860, gateOpen: false), Is.True);
            Assert.That(rewardCalls, Is.EqualTo(1));
        }

        [Test]
        public void ProgressSummaryAndNearestQueries_AreReadOnlyAndTrackLootAndUnlocks()
        {
            var store = new Dictionary<string, int>(StringComparer.Ordinal);
            Func<string, int> read = k => store.TryGetValue(k, out int v) ? v : 0;
            Action<string, int> write = (k, v) => store[k] = v;

            RpgWorld500xMapLayout soundwell = RpgWorld500xMapLayout.Build(RpgWorldMapId.Soundwell);
            var ctrl = new RpgWorld500xCachesAndShortcutsController(soundwell, read, write);

            RpgWorld500xCacheShortcutProgressSummary before = ctrl.BuildProgressSummary();
            Assert.AreEqual(RpgWorldMapId.Soundwell, before.MapId);
            Assert.AreEqual(0, before.LootedCacheCount);
            Assert.AreEqual(12, before.TotalCacheCount);
            Assert.AreEqual(12, before.RemainingCacheCount);
            Assert.Greater(before.RemainingCacheSaltSilver, 0);
            Assert.AreEqual(0, before.UnlockedShortcutCount);
            Assert.AreEqual(4, before.TotalShortcutCount);
            Assert.AreEqual(4, before.LockedShortcutCount);
            StringAssert.Contains("【秘藏与捷径】", before.SummaryZh);

            // Reject non-finite player coordinates safely without mutating state.
            Assert.IsFalse(ctrl.TryFindNearestUnlootedCache(new Vector2(float.NaN, 0f), out _, out _));
            Assert.IsFalse(ctrl.TryFindNearestLockedShortcut(new Vector2(0f, float.PositiveInfinity), out _, out _));

            // Nearest unlooted cache to (290.5, 780.5) is sw_c03 (saltSilver = 30).
            Assert.IsTrue(ctrl.TryFindNearestUnlootedCache(new Vector2(290.5f, 780.5f), out RpgSupplyCacheDef nearestCache, out float cacheDist));
            Assert.AreEqual("sw_c03", nearestCache.Id);
            Assert.AreEqual(0f, cacheDist, 0.01f);

            // Nearest locked shortcut to (380.5, 920.5) is sw_sc_entry_north.
            Assert.IsTrue(ctrl.TryFindNearestLockedShortcut(new Vector2(380.5f, 920.5f), out RpgWorldShortcutDef nearestSc, out float scDist));
            Assert.AreEqual("sw_sc_entry_north", nearestSc.Id);
            Assert.AreEqual(0f, scDist, 0.01f);
            Assert.AreEqual(0, store.Count);

            // Loot sw_c03 and unlock sw_sc_entry_north -> summary and nearest targets advance.
            Assert.IsTrue(ctrl.TryLootNearbyCache(new Vector2(290.5f, 780.5f), 6f, out _));
            Assert.IsTrue(ctrl.TryUnlockNearbyShortcut(new Vector2(380.5f, 920.5f), 6f, out _));

            RpgWorld500xCacheShortcutProgressSummary after = ctrl.BuildProgressSummary();
            Assert.AreEqual(1, after.LootedCacheCount);
            Assert.AreEqual(11, after.RemainingCacheCount);
            Assert.AreEqual(before.RemainingCacheSaltSilver - 30, after.RemainingCacheSaltSilver);
            Assert.AreEqual(1, after.UnlockedShortcutCount);
            Assert.AreEqual(3, after.LockedShortcutCount);

            Assert.IsTrue(ctrl.TryFindNearestUnlootedCache(new Vector2(290.5f, 780.5f), out RpgSupplyCacheDef nextCache, out _));
            Assert.AreNotEqual("sw_c03", nextCache.Id);
            Assert.IsTrue(ctrl.TryFindNearestLockedShortcut(new Vector2(380.5f, 920.5f), out RpgWorldShortcutDef nextSc, out _));
            Assert.AreNotEqual("sw_sc_entry_north", nextSc.Id);
        }
    }
}
