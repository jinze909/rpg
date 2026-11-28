using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionLivingWorldRumorStoryE2ETests
    {
        [Test]
        public void LivingWorldService_AcrossAllFourRegions_PersistsRumorsAndStorySpotsThroughFreshRehydrationAndMarksRepeatInspections()
        {
            var specs = RpgExpandedWorldScale.AllSpecs;
            Assert.That(specs.Count, Is.EqualTo(4));

            for (int r = 0; r < specs.Count; r++)
            {
                var region = specs[r].MapId;
                var store = new Dictionary<string, int>(StringComparer.Ordinal);
                Func<string, int> read = k => store.TryGetValue(k, out int v) ? v : 0;
                Action<string, int> write = (k, v) => store[k] = v;

                var tracker = new RpgMapExplorationTracker(region);
                var service = new RpgWorld500xLivingWorldService(region, tracker, read, write);

                Assert.IsTrue(service.TryFindNearestUnheardRumorNpc(new Vector2(100f, 100f), out var npc, out _));
                var npcPos = new Vector2(npc.Tile.x + 0.5f, npc.Tile.y + 0.5f);
                Assert.IsTrue(service.TryTalkToNearbyNpc(npcPos, 6f, out var talked, out string lineZh, out _));
                Assert.That(talked.Id, Is.EqualTo(npc.Id));
                Assert.IsFalse(string.IsNullOrEmpty(lineZh));

                Assert.IsTrue(service.TryFindNearestUninspectedStorySpot(new Vector2(100f, 100f), out var spot, out _));
                var spotPos = new Vector2(spot.Tile.x + 0.5f, spot.Tile.y + 0.5f);
                Assert.IsTrue(service.TryInspectNearbyStorySpot(spotPos, 6f, out var inspectedFirst, out bool firstInspection));
                Assert.That(inspectedFirst.Id, Is.EqualTo(spot.Id));
                Assert.IsTrue(firstInspection);

                Assert.IsTrue(service.TryInspectNearbyStorySpot(spotPos, 6f, out var inspectedRepeat, out bool secondInspection));
                Assert.That(inspectedRepeat.Id, Is.EqualTo(spot.Id));
                Assert.IsFalse(secondInspection);

                var restoredTracker = new RpgMapExplorationTracker(region);
                var restoredService = new RpgWorld500xLivingWorldService(region, restoredTracker, read, write);
                var summary = restoredService.BuildLivingWorldProgressSummary();
                Assert.That(summary.MapId, Is.EqualTo(region));
                Assert.That(summary.HeardRumorCount, Is.EqualTo(1));
                Assert.That(summary.RemainingRumorCount, Is.EqualTo(summary.TotalRumorCount - 1));
                Assert.That(summary.InspectedStorySpotCount, Is.EqualTo(1));
                Assert.That(summary.RemainingStorySpotCount, Is.EqualTo(summary.TotalStorySpotCount - 1));

                Assert.IsTrue(restoredService.TryInspectNearbyStorySpot(spotPos, 6f, out _, out bool afterRestoreFirst));
                Assert.IsFalse(afterRestoreFirst);
            }
        }

        [Test]
        public void LivingWorldService_AcrossAllFourRegions_RejectsOutOfRangeOrNonPositiveRadiusWithoutMutatingStore()
        {
            var specs = RpgExpandedWorldScale.AllSpecs;
            Assert.That(specs.Count, Is.EqualTo(4));

            for (int r = 0; r < specs.Count; r++)
            {
                var region = specs[r].MapId;
                var store = new Dictionary<string, int>(StringComparer.Ordinal);
                var tracker = new RpgMapExplorationTracker(region);
                var service = new RpgWorld500xLivingWorldService(
                    region,
                    tracker,
                    getInt: k => store.TryGetValue(k, out int v) ? v : 0,
                    setInt: (k, v) => store[k] = v);

                Assert.IsTrue(service.TryFindNearestUnheardRumorNpc(new Vector2(100f, 100f), out var npc, out _));
                Assert.IsTrue(service.TryFindNearestUninspectedStorySpot(new Vector2(100f, 100f), out var spot, out _));

                var farAway = new Vector2(-5000f, -5000f);
                var npcPos = new Vector2(npc.Tile.x + 0.5f, npc.Tile.y + 0.5f);
                var spotPos = new Vector2(spot.Tile.x + 0.5f, spot.Tile.y + 0.5f);

                Assert.IsFalse(service.TryTalkToNearbyNpc(farAway, 4f, out _, out _, out _));
                Assert.IsFalse(service.TryTalkToNearbyNpc(npcPos, 0f, out _, out _, out _));
                Assert.IsFalse(service.TryTalkToNearbyNpc(npcPos, -2f, out _, out _, out _));

                Assert.IsFalse(service.TryInspectNearbyStorySpot(farAway, 4f, out _, out _));
                Assert.IsFalse(service.TryInspectNearbyStorySpot(spotPos, 0f, out _, out _));
                Assert.IsFalse(service.TryInspectNearbyStorySpot(spotPos, -2f, out _, out _));

                Assert.That(store.Count, Is.EqualTo(0));
            }
        }
    }
}
