using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionLivingWorldNearestQueriesE2ETests
    {
        [Test]
        public void FourRegion_LivingWorldService_ExhaustsAllRumorsAndStorySpotsViaNearestQueries()
        {
            IReadOnlyList<RpgExpandedMapSpec> specs = RpgExpandedWorldScale.AllSpecs;
            Assert.AreEqual(4, specs.Count);

            var sharedStore = new Dictionary<string, int>(StringComparer.Ordinal);
            Func<string, int> read = k => sharedStore.TryGetValue(k, out int v) ? v : 0;
            Action<string, int> write = (k, v) => sharedStore[k] = v;

            var heardNpcIds = new HashSet<string>(StringComparer.Ordinal);
            var inspectedSpotIds = new HashSet<string>(StringComparer.Ordinal);

            for (int r = 0; r < specs.Count; r++)
            {
                RpgWorldMapId region = specs[r].MapId;
                var tracker = new RpgMapExplorationTracker(region);
                var service = new RpgWorld500xLivingWorldService(region, tracker, read, write);

                RpgWorld500xLivingWorldProgressSummary initial = service.BuildLivingWorldProgressSummary();
                Assert.AreEqual(region, initial.MapId);
                Assert.AreEqual(0, initial.HeardRumorCount);
                Assert.AreEqual(6, initial.TotalRumorCount);
                Assert.AreEqual(6, initial.RemainingRumorCount);
                Assert.AreEqual(0, initial.InspectedStorySpotCount);
                Assert.AreEqual(4, initial.TotalStorySpotCount);
                Assert.AreEqual(4, initial.RemainingStorySpotCount);
                Assert.AreEqual(3, initial.AmbientZoneCount);
                StringAssert.Contains("【风土与传闻】", initial.SummaryZh);

                Vector2 cursor = new Vector2(100f, 100f);

                // Exhaust all 6 regional field NPCs via nearest-unheard query chain.
                for (int i = 0; i < initial.TotalRumorCount; i++)
                {
                    int keysBeforeQuery = sharedStore.Count;
                    Assert.IsTrue(service.TryFindNearestUnheardRumorNpc(cursor, out RpgFieldNpcDef npc, out float dist),
                        $"Expected unheard NPC #{i + 1} on {region}.");
                    Assert.GreaterOrEqual(dist, 0f);
                    Assert.AreEqual(keysBeforeQuery, sharedStore.Count,
                        "Nearest-unheard NPC query must be strictly read-only.");

                    Vector2 npcPos = new Vector2(npc.Tile.x + 0.5f, npc.Tile.y + 0.5f);
                    Assert.IsTrue(service.TryTalkToNearbyNpc(npcPos, 6f, out RpgFieldNpcDef talked, out string lineZh, out _));
                    Assert.AreEqual(npc.Id, talked.Id);
                    Assert.IsFalse(string.IsNullOrEmpty(lineZh));
                    Assert.IsTrue(heardNpcIds.Add(npc.Id), $"Duplicate NPC rumor ID {npc.Id}.");

                    cursor = npcPos;
                    RpgWorld500xLivingWorldProgressSummary mid = service.BuildLivingWorldProgressSummary();
                    Assert.AreEqual(i + 1, mid.HeardRumorCount);
                    Assert.AreEqual(initial.TotalRumorCount - (i + 1), mid.RemainingRumorCount);
                }

                Assert.IsFalse(service.TryFindNearestUnheardRumorNpc(cursor, out _, out _));

                // Exhaust all 4 regional story spots via nearest-uninspected query chain.
                for (int i = 0; i < initial.TotalStorySpotCount; i++)
                {
                    int keysBeforeQuery = sharedStore.Count;
                    Assert.IsTrue(service.TryFindNearestUninspectedStorySpot(cursor, out RpgStorySpotDef spot, out float dist),
                        $"Expected uninspected story spot #{i + 1} on {region}.");
                    Assert.GreaterOrEqual(dist, 0f);
                    Assert.AreEqual(keysBeforeQuery, sharedStore.Count,
                        "Nearest-uninspected story spot query must be strictly read-only.");

                    Vector2 spotPos = new Vector2(spot.Tile.x + 0.5f, spot.Tile.y + 0.5f);
                    Assert.IsTrue(service.TryInspectNearbyStorySpot(spotPos, 6f, out RpgStorySpotDef inspected, out string loreZh));
                    Assert.AreEqual(spot.Id, inspected.Id);
                    Assert.IsFalse(string.IsNullOrEmpty(loreZh));
                    Assert.IsTrue(inspectedSpotIds.Add(spot.Id), $"Duplicate story spot ID {spot.Id}.");

                    cursor = spotPos;
                    RpgWorld500xLivingWorldProgressSummary mid = service.BuildLivingWorldProgressSummary();
                    Assert.AreEqual(i + 1, mid.InspectedStorySpotCount);
                    Assert.AreEqual(initial.TotalStorySpotCount - (i + 1), mid.RemainingStorySpotCount);
                }

                Assert.IsFalse(service.TryFindNearestUninspectedStorySpot(cursor, out _, out _));
            }

            Assert.AreEqual(24, heardNpcIds.Count);
            Assert.AreEqual(16, inspectedSpotIds.Count);
        }

        [Test]
        public void FourRegion_LivingWorldService_RejectsNonFiniteQueriesWithoutMutatingStore()
        {
            IReadOnlyList<RpgExpandedMapSpec> specs = RpgExpandedWorldScale.AllSpecs;
            Assert.AreEqual(4, specs.Count);

            for (int r = 0; r < specs.Count; r++)
            {
                RpgWorldMapId region = specs[r].MapId;
                var store = new Dictionary<string, int>(StringComparer.Ordinal);
                var tracker = new RpgMapExplorationTracker(region);
                var service = new RpgWorld500xLivingWorldService(
                    region,
                    tracker,
                    getInt: k => store.TryGetValue(k, out int v) ? v : 0,
                    setInt: (k, v) => store[k] = v);

                Assert.IsFalse(service.TryFindNearestUnheardRumorNpc(new Vector2(float.NaN, 10f), out _, out _));
                Assert.IsFalse(service.TryFindNearestUnheardRumorNpc(new Vector2(10f, float.PositiveInfinity), out _, out _));
                Assert.IsFalse(service.TryFindNearestUninspectedStorySpot(new Vector2(float.NegativeInfinity, 10f), out _, out _));
                Assert.IsFalse(service.TryFindNearestUninspectedStorySpot(new Vector2(10f, float.NaN), out _, out _));
                Assert.AreEqual(0, store.Count);
            }
        }
    }
}
