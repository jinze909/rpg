using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionWaypointGuidanceE2ETests
    {
        [Test]
        public void AllFourRegions_WaypointGuidancePrefersLiveCacheAndShortcutStateAndTracksLandmarks()
        {
            int verifiedRegions = 0;

            foreach (RpgExpandedMapSpec spec in RpgExpandedWorldScale.AllSpecs)
            {
                RpgWorld500xMapLayout layout = RpgWorld500xMapLayout.Build(spec.MapId);
                var caches = RpgWorld500xContentCatalog.GetCaches(layout.MapId);
                var shortcuts = RpgWorld500xContentCatalog.GetShortcuts(layout.MapId);
                Assert.IsNotEmpty(caches, "Expected supply caches in " + spec.MapId);
                Assert.IsNotEmpty(shortcuts, "Expected shortcuts in " + spec.MapId);

                var cache = caches[0];
                var shortcut = shortcuts[0];
                var live = new Dictionary<string, int>(StringComparer.Ordinal);
                var stale = new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    [cache.MakinomKey] = 1,
                    [shortcut.MakinomKey] = 1
                };
                Func<string, int> readLive = k => live.TryGetValue(k, out int v) ? v : 0;
                Func<string, int> readStale = k => stale.TryGetValue(k, out int v) ? v : 0;

                var controller = new RpgWorld500xCachesAndShortcutsController(
                    layout, readLive, (k, v) => live[k] = v);
                var cacheCenter = new Vector2(cache.Tile.x + 0.5f, cache.Tile.y + 0.5f);
                var shortcutCenter = new Vector2(shortcut.TriggerTile.x + 0.5f, shortcut.TriggerTile.y + 0.5f);

                // Mounted controller state takes precedence over stale persisted flags (e.g. after save rollback).
                Assert.IsTrue(RpgWorld500xWaypointNavigator.TryFindNearestTarget(
                    layout, cacheCenter, RpgWorld500xWaypointCategory.UnlootedCache,
                    out var cacheTarget, cachesCtrl: controller, getInt: readStale));
                Assert.AreEqual(cache.Id, cacheTarget.EntityId);
                Assert.IsFalse(string.IsNullOrEmpty(cacheTarget.HudReadoutZh));

                Assert.IsTrue(RpgWorld500xWaypointNavigator.TryFindNearestTarget(
                    layout, shortcutCenter, RpgWorld500xWaypointCategory.LockedShortcut,
                    out var shortcutTarget, cachesCtrl: controller, getInt: readStale));
                Assert.AreEqual(shortcut.Id, shortcutTarget.EntityId);
                Assert.IsFalse(string.IsNullOrEmpty(shortcutTarget.HudReadoutZh));

                var nearbyLive = RpgWorld500xWaypointNavigator.FindTopNearbyPointsOfInterest(
                    layout, cacheCenter, int.MaxValue, cachesCtrl: controller, getInt: readStale);
                Assert.IsTrue(nearbyLive.Exists(p => p.EntityId == cache.Id &&
                    p.Category == RpgWorld500xWaypointCategory.UnlootedCache));
                Assert.IsTrue(nearbyLive.Exists(p => p.EntityId == shortcut.Id &&
                    p.Category == RpgWorld500xWaypointCategory.LockedShortcut));

                // Standalone guidance without a controller falls back to the persisted reader.
                var standalone = RpgWorld500xWaypointNavigator.FindTopNearbyPointsOfInterest(
                    layout, cacheCenter, int.MaxValue, getInt: readStale);
                Assert.IsFalse(standalone.Exists(p => p.EntityId == cache.Id &&
                    p.Category == RpgWorld500xWaypointCategory.UnlootedCache));
                Assert.IsFalse(standalone.Exists(p => p.EntityId == shortcut.Id &&
                    p.Category == RpgWorld500xWaypointCategory.LockedShortcut));

                // Looting and unlocking removes both targets even when the separate reader is stale at zero.
                stale.Clear();
                Assert.IsTrue(controller.TryLootNearbyCache(cacheCenter, 0.5f, out var looted));
                Assert.AreEqual(cache.Id, looted.Id);
                Assert.IsTrue(controller.TryUnlockNearbyShortcut(shortcutCenter, 0.5f, out var unlocked));
                Assert.AreEqual(shortcut.Id, unlocked.Id);

                var nearbyAfter = RpgWorld500xWaypointNavigator.FindTopNearbyPointsOfInterest(
                    layout, cacheCenter, int.MaxValue, cachesCtrl: controller, getInt: readStale);
                Assert.IsFalse(nearbyAfter.Exists(p => p.EntityId == cache.Id &&
                    p.Category == RpgWorld500xWaypointCategory.UnlootedCache));
                Assert.IsFalse(nearbyAfter.Exists(p => p.EntityId == shortcut.Id &&
                    p.Category == RpgWorld500xWaypointCategory.LockedShortcut));

                // Undiscovered landmark guidance resolves cleanly with Chinese HUD readout in every region.
                Assert.IsTrue(RpgWorld500xWaypointNavigator.TryFindNearestTarget(
                    layout, cacheCenter, RpgWorld500xWaypointCategory.UndiscoveredLandmark,
                    out var landmarkTarget, arrivalThresholdTiles: 8f));
                Assert.IsFalse(string.IsNullOrEmpty(landmarkTarget.HudReadoutZh));

                verifiedRegions++;
            }

            Assert.AreEqual(4, verifiedRegions);
        }
    }
}
