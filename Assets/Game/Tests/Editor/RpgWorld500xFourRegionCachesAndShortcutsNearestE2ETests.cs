using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionCachesAndShortcutsNearestE2ETests
    {
        [Test]
        public void AllFourRegions_CacheAndShortcutInteractionsMatchNearestGuidanceAcrossChunkBoundaries()
        {
            int verifiedRegions = 0;

            foreach (RpgExpandedMapSpec spec in RpgExpandedWorldScale.AllSpecs)
            {
                var store = new Dictionary<string, int>(StringComparer.Ordinal);
                var map = RpgWorld500xMapLayout.Build(spec.MapId);
                int rewards = 0;
                var ctrl = new RpgWorld500xCachesAndShortcutsController(
                    map,
                    k => store.TryGetValue(k, out int v) ? v : 0,
                    (k, v) => store[k] = v,
                    _ => rewards++);

                var player = new Vector2(0.5f, 0.5f);

                // Nearest unlooted cache boundary: just under fails, just over loots exact same cache.
                Assert.IsTrue(
                    ctrl.TryFindNearestUnlootedCache(player, out var cache, out float cacheDistance),
                    "Expected unlooted cache in " + spec.MapId);
                Assert.Greater(cacheDistance, 1f);
                Assert.IsFalse(ctrl.TryLootNearbyCache(player, cacheDistance - 1f, out _));
                Assert.IsTrue(ctrl.TryLootNearbyCache(player, cacheDistance + 1f, out var looted));
                Assert.AreEqual(cache.Id, looted.Id);
                Assert.AreEqual(1, store[cache.MakinomKey]);
                Assert.AreEqual(1, rewards);
                Assert.IsTrue(ctrl.TryFindNearestUnlootedCache(player, out var nextCache, out _));
                Assert.AreNotEqual(cache.Id, nextCache.Id);

                // Nearest locked shortcut boundary: just under fails, just over unlocks exact same shortcut.
                Assert.IsTrue(
                    ctrl.TryFindNearestLockedShortcut(player, out var shortcut, out float shortcutDistance),
                    "Expected locked shortcut in " + spec.MapId);
                Assert.Greater(shortcutDistance, 1f);
                Assert.IsFalse(ctrl.TryUnlockNearbyShortcut(player, shortcutDistance - 1f, out _));
                Assert.IsTrue(ctrl.TryUnlockNearbyShortcut(player, shortcutDistance + 1f, out var unlocked));
                Assert.AreEqual(shortcut.Id, unlocked.Id);
                Assert.AreEqual(1, store[shortcut.MakinomKey]);
                Assert.IsTrue(ctrl.TryFindNearestLockedShortcut(player, out var nextShortcut, out _));
                Assert.AreNotEqual(shortcut.Id, nextShortcut.Id);

                // Non-finite inputs fail closed without mutating persisted state or reward counts.
                Assert.IsFalse(ctrl.TryLootNearbyCache(new Vector2(float.NaN, 0f), 1000f, out _));
                Assert.IsFalse(ctrl.TryUnlockNearbyShortcut(player, float.PositiveInfinity, out _));
                Assert.AreEqual(2, store.Count);
                Assert.AreEqual(1, rewards);

                verifiedRegions++;
            }

            Assert.AreEqual(4, verifiedRegions);
        }
    }
}
