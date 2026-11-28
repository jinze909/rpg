using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    [TestFixture]
    public sealed class RpgWorld500xFourRegionExplorationPersistenceGuardE2ETests
    {
        [Test]
        public void AllFourRegions_CachesAndShortcuts_RejectUnconfirmedWritesAndRetryCleanly()
        {
            var specs = RpgExpandedWorldScale.AllSpecs;
            Assert.AreEqual(4, specs.Count, "Expected exactly 4 regions in 500x world scale.");

            foreach (RpgExpandedMapSpec spec in specs)
            {
                var store = new Dictionary<string, int>(StringComparer.Ordinal);
                Func<string, int> read = k => store.TryGetValue(k, out int v) ? v : 0;
                bool acceptWrite = false;
                Action<string, int> write = (k, v) =>
                {
                    if (acceptWrite)
                    {
                        store[k] = v;
                    }
                };

                RpgWorld500xMapLayout map = RpgWorld500xMapLayout.Build(spec.MapId);
                RpgSupplyCacheDef cache = RpgWorld500xContentCatalog.GetCaches(spec.MapId)[0];
                RpgWorldShortcutDef shortcut = RpgWorld500xContentCatalog.GetShortcuts(spec.MapId)[0];
                var cachePos = new Vector2(cache.Tile.x + 0.5f, cache.Tile.y + 0.5f);
                var shortcutPos = new Vector2(shortcut.TriggerTile.x + 0.5f, shortcut.TriggerTile.y + 0.5f);

                int rewards = 0;
                var ctrl = new RpgWorld500xCachesAndShortcutsController(map, read, write, _ => rewards++);

                Assert.IsFalse(ctrl.TryLootNearbyCache(cachePos, 0.5f, out _));
                Assert.IsFalse(ctrl.TryUnlockNearbyShortcut(shortcutPos, 0.5f, out _));
                Assert.AreEqual(0, rewards);
                Assert.AreEqual(0, store.Count);
                Assert.AreEqual(0, ctrl.LootedCacheCount);
                Assert.AreEqual(0, ctrl.UnlockedShortcutCount);

                acceptWrite = true;
                Assert.IsTrue(ctrl.TryLootNearbyCache(cachePos, 0.5f, out RpgSupplyCacheDef looted));
                Assert.AreEqual(cache.Id, looted.Id);
                Assert.IsTrue(ctrl.TryUnlockNearbyShortcut(shortcutPos, 0.5f, out RpgWorldShortcutDef unlocked));
                Assert.AreEqual(shortcut.Id, unlocked.Id);
                Assert.AreEqual(1, store[cache.MakinomKey]);
                Assert.AreEqual(1, store[shortcut.MakinomKey]);
                Assert.AreEqual(1, rewards);
                Assert.AreEqual(1, ctrl.LootedCacheCount);
                Assert.AreEqual(1, ctrl.UnlockedShortcutCount);
            }
        }

        [Test]
        public void AllFourRegions_CartographyMilestones_RejectUnconfirmedWritesAndRetryWithoutDuplicateReward()
        {
            var specs = RpgExpandedWorldScale.AllSpecs;
            Assert.AreEqual(4, specs.Count, "Expected exactly 4 regions in 500x world scale.");

            foreach (RpgExpandedMapSpec spec in specs)
            {
                var store = new Dictionary<string, int>(StringComparer.Ordinal);
                Func<string, int> read = k => store.TryGetValue(k, out int v) ? v : 0;
                RpgWorld500xCartographyMilestone[] milestones = RpgWorld500xDistrictCatalog.GetMilestones(spec.MapId);
                Assert.Greater(milestones.Length, 1);

                int expectedTotalSilver = 0;
                for (int i = 0; i < milestones.Length; i++)
                {
                    expectedTotalSilver += milestones[i].SaltSilverReward;
                }

                bool rejectFirst = true;
                int silver = 0;
                int callbacks = 0;
                var tracker = new RpgWorld500xDistrictProgressTracker(
                    spec,
                    read,
                    (k, v) =>
                    {
                        if (!rejectFirst || k != milestones[0].MakinomKey)
                        {
                            store[k] = v;
                        }
                    },
                    m =>
                    {
                        silver += m.SaltSilverReward;
                        callbacks++;
                    });

                List<RpgWorld500xCartographyMilestone> firstPass = tracker.ClaimEligibleMilestones(100);
                Assert.AreEqual(milestones.Length - 1, firstPass.Count);
                Assert.AreEqual(expectedTotalSilver - milestones[0].SaltSilverReward, silver);
                Assert.AreEqual(milestones.Length - 1, callbacks);
                Assert.IsFalse(store.ContainsKey(milestones[0].MakinomKey));

                rejectFirst = false;
                List<RpgWorld500xCartographyMilestone> retryPass = tracker.ClaimEligibleMilestones(100);
                Assert.AreEqual(1, retryPass.Count);
                Assert.AreEqual(milestones[0].MakinomKey, retryPass[0].MakinomKey);
                Assert.AreEqual(expectedTotalSilver, silver);
                Assert.AreEqual(milestones.Length, callbacks);
                Assert.AreEqual(0, tracker.ClaimEligibleMilestones(100).Count);
            }
        }
    }
}
