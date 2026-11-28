using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    [TestFixture]
    public sealed class RpgWorld500xFourRegionCachesShortcutsPersistenceE2ETests
    {
        [Test]
        public void AllFourRegions_CacheLootAndShortcutUnlock_PersistInSharedStoreWithoutCrossRegionLeakage()
        {
            var specs = RpgExpandedWorldScale.AllSpecs;
            Assert.AreEqual(4, specs.Count, "Expected exactly 4 regions in 500x world scale.");

            var store = new Dictionary<string, int>(StringComparer.Ordinal);
            Func<string, int> read = k => store.TryGetValue(k, out int v) ? v : 0;
            Action<string, int> write = (k, v) => store[k] = v;

            var lootedCacheIdByRegion = new Dictionary<RpgWorldMapId, string>();
            var unlockedShortcutIdByRegion = new Dictionary<RpgWorldMapId, string>();
            var expectedRemainingSilverByRegion = new Dictionary<RpgWorldMapId, int>();

            foreach (RpgExpandedMapSpec spec in specs)
            {
                RpgWorld500xMapLayout map = RpgWorld500xMapLayout.Build(spec.MapId);
                int callbackSilver = 0;
                var ctrl = new RpgWorld500xCachesAndShortcutsController(
                    map, read, write, c => callbackSilver += c.SaltSilverReward);

                RpgWorld500xCacheShortcutProgressSummary before = ctrl.BuildProgressSummary();
                Assert.AreEqual(spec.MapId, before.MapId);
                Assert.AreEqual(0, before.LootedCacheCount, $"Region {spec.MapId} should start with 0 looted caches in shared store.");
                Assert.AreEqual(0, before.UnlockedShortcutCount, $"Region {spec.MapId} should start with 0 unlocked shortcuts in shared store.");
                Assert.Greater(before.TotalCacheCount, 0, $"Region {spec.MapId} must define caches.");
                Assert.Greater(before.TotalShortcutCount, 0, $"Region {spec.MapId} must define shortcuts.");

                var spawn = new Vector2(map.PlayerSpawn.x + 0.5f, map.PlayerSpawn.y + 0.5f);
                Assert.IsTrue(ctrl.TryFindNearestUnlootedCache(spawn, out RpgSupplyCacheDef nearestCache, out float cacheDist));
                Assert.IsTrue(ctrl.TryLootNearbyCache(spawn, cacheDist + 1f, out RpgSupplyCacheDef looted));
                Assert.AreEqual(nearestCache.Id, looted.Id);
                Assert.IsTrue(ctrl.IsCacheLooted(nearestCache.Id));
                Assert.AreEqual(1, store[nearestCache.MakinomKey]);
                Assert.AreEqual(nearestCache.SaltSilverReward, callbackSilver);

                Assert.IsTrue(ctrl.TryFindNearestLockedShortcut(spawn, out RpgWorldShortcutDef nearestShortcut, out float shortcutDist));
                Assert.IsTrue(ctrl.TryUnlockNearbyShortcut(spawn, shortcutDist + 1f, out RpgWorldShortcutDef unlocked));
                Assert.AreEqual(nearestShortcut.Id, unlocked.Id);
                Assert.IsTrue(ctrl.IsShortcutUnlocked(nearestShortcut.Id));
                Assert.AreEqual(1, store[nearestShortcut.MakinomKey]);

                lootedCacheIdByRegion[spec.MapId] = nearestCache.Id;
                unlockedShortcutIdByRegion[spec.MapId] = nearestShortcut.Id;
                expectedRemainingSilverByRegion[spec.MapId] = before.RemainingCacheSaltSilver - nearestCache.SaltSilverReward;
            }

            foreach (RpgExpandedMapSpec spec in specs)
            {
                RpgWorld500xMapLayout map = RpgWorld500xMapLayout.Build(spec.MapId);
                var reloaded = new RpgWorld500xCachesAndShortcutsController(map, read, write);
                Assert.IsTrue(reloaded.IsCacheLooted(lootedCacheIdByRegion[spec.MapId]), $"Looted cache should persist on reload for {spec.MapId}.");
                Assert.IsTrue(reloaded.IsShortcutUnlocked(unlockedShortcutIdByRegion[spec.MapId]), $"Unlocked shortcut should persist on reload for {spec.MapId}.");

                RpgWorld500xCacheShortcutProgressSummary after = reloaded.BuildProgressSummary();
                Assert.AreEqual(1, after.LootedCacheCount);
                Assert.AreEqual(1, after.UnlockedShortcutCount);
                Assert.AreEqual(expectedRemainingSilverByRegion[spec.MapId], after.RemainingCacheSaltSilver);
            }
        }

        [Test]
        public void AllFourRegions_InvalidInputsAndOutOfRangeInteractions_RejectSafelyWithoutMutatingState()
        {
            var specs = RpgExpandedWorldScale.AllSpecs;
            Assert.AreEqual(4, specs.Count, "Expected exactly 4 regions in 500x world scale.");

            foreach (RpgExpandedMapSpec spec in specs)
            {
                var store = new Dictionary<string, int>(StringComparer.Ordinal);
                int callbacks = 0;
                RpgWorld500xMapLayout map = RpgWorld500xMapLayout.Build(spec.MapId);
                var ctrl = new RpgWorld500xCachesAndShortcutsController(
                    map,
                    k => store.TryGetValue(k, out int v) ? v : 0,
                    (k, v) => store[k] = v,
                    _ => callbacks++);

                var origin = new Vector2(0.5f, 0.5f);
                Assert.IsFalse(ctrl.TryFindNearestUnlootedCache(new Vector2(float.NaN, 0f), out _, out _));
                Assert.IsFalse(ctrl.TryFindNearestUnlootedCache(new Vector2(0f, float.PositiveInfinity), out _, out _));
                Assert.IsFalse(ctrl.TryFindNearestLockedShortcut(new Vector2(float.NaN, 0f), out _, out _));
                Assert.IsFalse(ctrl.TryFindNearestLockedShortcut(new Vector2(0f, float.NegativeInfinity), out _, out _));

                Assert.IsFalse(ctrl.TryLootNearbyCache(origin, 0f, out _));
                Assert.IsFalse(ctrl.TryLootNearbyCache(origin, -5f, out _));
                Assert.IsFalse(ctrl.TryLootNearbyCache(origin, float.NaN, out _));
                Assert.IsFalse(ctrl.TryLootNearbyCache(origin, float.PositiveInfinity, out _));
                Assert.IsFalse(ctrl.TryUnlockNearbyShortcut(origin, 0f, out _));
                Assert.IsFalse(ctrl.TryUnlockNearbyShortcut(origin, -5f, out _));
                Assert.IsFalse(ctrl.TryUnlockNearbyShortcut(origin, float.NaN, out _));
                Assert.IsFalse(ctrl.TryUnlockNearbyShortcut(origin, float.PositiveInfinity, out _));

                Assert.IsTrue(ctrl.TryFindNearestUnlootedCache(origin, out _, out float cacheDist));
                if (cacheDist > 1f)
                {
                    Assert.IsFalse(ctrl.TryLootNearbyCache(origin, cacheDist - 1f, out _));
                }

                Assert.IsTrue(ctrl.TryFindNearestLockedShortcut(origin, out _, out float shortcutDist));
                if (shortcutDist > 1f)
                {
                    Assert.IsFalse(ctrl.TryUnlockNearbyShortcut(origin, shortcutDist - 1f, out _));
                }

                Assert.AreEqual(0, store.Count, $"Invalid/out-of-range interactions must not write facts in {spec.MapId}.");
                Assert.AreEqual(0, callbacks, $"Invalid/out-of-range interactions must not fire reward callbacks in {spec.MapId}.");
            }
        }
    }
}
