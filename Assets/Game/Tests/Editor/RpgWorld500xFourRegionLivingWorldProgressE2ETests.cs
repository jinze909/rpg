using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    /// <summary>
    /// Four-region E2E regression tests for RpgWorld500xLivingWorldService verifying
    /// read-only progress summaries, nearest unheard-rumor and uninspected-story-spot
    /// queries, first-hearing vs repeat-dialogue semantics, and full progressive drain
    /// to zero across Soundwell, RedSaltHarbor, WhitewaterCrossing, and MineCamp.
    /// </summary>
    public sealed class RpgWorld500xFourRegionLivingWorldProgressE2ETests
    {
        [Test]
        public void AllFourRegions_LivingWorldProgressSummaryAndNearestQueries_DrainAllRumorsAndStorySpotsToZero()
        {
            IReadOnlyList<RpgExpandedMapSpec> specs = RpgExpandedWorldScale.AllSpecs;
            Assert.AreEqual(4, specs.Count);

            int totalRumorsAcrossWorld = 0;
            int totalStorySpotsAcrossWorld = 0;
            int totalAmbientZonesAcrossWorld = 0;

            for (int r = 0; r < specs.Count; r++)
            {
                RpgExpandedMapSpec spec = specs[r];
                var store = new Dictionary<string, int>(StringComparer.Ordinal);
                Func<string, int> read = k => store.TryGetValue(k, out int v) ? v : 0;
                Action<string, int> write = (k, v) => store[k] = v;

                var tracker = new RpgMapExplorationTracker(spec.MapId);
                var service = new RpgWorld500xLivingWorldService(spec.MapId, tracker, read, write);

                RpgWorld500xLivingWorldProgressSummary initial = service.BuildLivingWorldProgressSummary();
                Assert.AreEqual(spec.MapId, initial.MapId);
                Assert.AreEqual(0, initial.HeardRumorCount);
                Assert.Greater(initial.TotalRumorCount, 0, $"Region {spec.MapId} must have field NPCs.");
                Assert.AreEqual(initial.TotalRumorCount, initial.RemainingRumorCount);
                Assert.AreEqual(0, initial.InspectedStorySpotCount);
                Assert.Greater(initial.TotalStorySpotCount, 0, $"Region {spec.MapId} must have story spots.");
                Assert.AreEqual(initial.TotalStorySpotCount, initial.RemainingStorySpotCount);
                Assert.Greater(initial.AmbientZoneCount, 0, $"Region {spec.MapId} must have ambient zones.");
                StringAssert.Contains("【风土与传闻】", initial.SummaryZh);

                // Read-only queries and invalid-coordinate guards must not mutate store.
                Assert.IsFalse(service.TryFindNearestUnheardRumorNpc(new Vector2(float.NaN, 0f), out _, out _));
                Assert.IsFalse(service.TryFindNearestUninspectedStorySpot(new Vector2(0f, float.NegativeInfinity), out _, out _));
                Assert.AreEqual(0, store.Count, $"Read-only living-world queries mutated store on {spec.MapId}.");

                // Drain all unheard rumors via nearest-unheard query loop.
                var origin = new Vector2(0f, 0f);
                int heardCount = 0;
                while (service.TryFindNearestUnheardRumorNpc(origin, out RpgFieldNpcDef nearestNpc, out float dist))
                {
                    Assert.GreaterOrEqual(dist, 0f);
                    var npcPos = new Vector2(nearestNpc.Tile.x + 0.5f, nearestNpc.Tile.y + 0.5f);

                    Assert.IsTrue(service.TryTalkToNearbyNpc(
                        npcPos, 6f, out RpgFieldNpcDef talkedNpc, out string firstLine, out _, out bool firstHearing),
                        $"Failed to talk to {nearestNpc.Id} on {spec.MapId}.");
                    Assert.AreEqual(nearestNpc.Id, talkedNpc.Id);
                    Assert.IsFalse(string.IsNullOrWhiteSpace(firstLine));
                    Assert.IsTrue(firstHearing, $"First conversation with {nearestNpc.Id} must set firstHearing=true.");

                    // Repeat conversation must not re-flag firstHearing or double-count progress.
                    Assert.IsTrue(service.TryTalkToNearbyNpc(
                        npcPos, 6f, out RpgFieldNpcDef repeatNpc, out string repeatLine, out _, out bool repeatHearing));
                    Assert.AreEqual(nearestNpc.Id, repeatNpc.Id);
                    Assert.IsFalse(string.IsNullOrWhiteSpace(repeatLine));
                    Assert.IsFalse(repeatHearing, $"Repeat conversation with {nearestNpc.Id} must set firstHearing=false.");

                    heardCount++;
                    RpgWorld500xLivingWorldProgressSummary mid = service.BuildLivingWorldProgressSummary();
                    Assert.AreEqual(heardCount, mid.HeardRumorCount);
                    Assert.AreEqual(initial.TotalRumorCount - heardCount, mid.RemainingRumorCount);
                }

                Assert.AreEqual(initial.TotalRumorCount, heardCount, $"Not all rumors drained on {spec.MapId}.");

                // Drain all uninspected story spots via nearest-uninspected query loop.
                int inspectedCount = 0;
                while (service.TryFindNearestUninspectedStorySpot(origin, out RpgStorySpotDef nearestSpot, out float spotDist))
                {
                    Assert.GreaterOrEqual(spotDist, 0f);
                    var spotPos = new Vector2(nearestSpot.Tile.x + 0.5f, nearestSpot.Tile.y + 0.5f);

                    Assert.IsTrue(service.TryInspectNearbyStorySpot(
                        spotPos, 6f, out RpgStorySpotDef inspectedSpot, out _),
                        $"Failed to inspect {nearestSpot.Id} on {spec.MapId}.");
                    Assert.AreEqual(nearestSpot.Id, inspectedSpot.Id);

                    inspectedCount++;
                    RpgWorld500xLivingWorldProgressSummary mid = service.BuildLivingWorldProgressSummary();
                    Assert.AreEqual(inspectedCount, mid.InspectedStorySpotCount);
                    Assert.AreEqual(initial.TotalStorySpotCount - inspectedCount, mid.RemainingStorySpotCount);
                }

                Assert.AreEqual(initial.TotalStorySpotCount, inspectedCount, $"Not all story spots drained on {spec.MapId}.");

                RpgWorld500xLivingWorldProgressSummary finalSummary = service.BuildLivingWorldProgressSummary();
                Assert.AreEqual(0, finalSummary.RemainingRumorCount);
                Assert.AreEqual(0, finalSummary.RemainingStorySpotCount);
                Assert.IsFalse(service.TryFindNearestUnheardRumorNpc(origin, out _, out _));
                Assert.IsFalse(service.TryFindNearestUninspectedStorySpot(origin, out _, out _));

                totalRumorsAcrossWorld += finalSummary.TotalRumorCount;
                totalStorySpotsAcrossWorld += finalSummary.TotalStorySpotCount;
                totalAmbientZonesAcrossWorld += finalSummary.AmbientZoneCount;
            }

            Assert.AreEqual(24, totalRumorsAcrossWorld);
            Assert.AreEqual(16, totalStorySpotsAcrossWorld);
            Assert.AreEqual(12, totalAmbientZonesAcrossWorld);
        }
    }
}
