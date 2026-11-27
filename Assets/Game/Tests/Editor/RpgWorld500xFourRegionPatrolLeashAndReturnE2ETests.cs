using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionPatrolLeashAndReturnE2ETests
    {
        [Test]
        public void FourRegion_PatrolDirector_EnforcesLeashPreventsOscillationAndReacquiresAcrossAllFourRegions()
        {
            IReadOnlyList<RpgExpandedMapSpec> specs = RpgExpandedWorldScale.AllSpecs;
            Assert.AreEqual(4, specs.Count);

            Vector2[] cardinalDirs = { Vector2.right, Vector2.left, Vector2.up, Vector2.down };

            for (int r = 0; r < specs.Count; r++)
            {
                RpgWorldMapId region = specs[r].MapId;
                var store = new Dictionary<string, int>(StringComparer.Ordinal);
                RpgWorld500xMapLayout map = RpgWorld500xMapLayout.Build(region);
                var bridge = new RpgWorldOrkExplorationBridge(
                    region,
                    k => store.TryGetValue(k, out int v) ? v : 0,
                    (k, v) => store[k] = v);
                var director = new RpgWorld500xPatrolAndEncounterDirector(map, bridge);

                // Locate a DangerZone marker on this region with a clear walkable corridor to the leash edge.
                string targetMarkerId = null;
                Vector2 anchor = Vector2.zero;
                Vector2 direction = Vector2.zero;

                for (int m = 0; m < map.Markers.Count && direction == Vector2.zero; m++)
                {
                    RpgMapLandmarkMarker marker = map.Markers[m];
                    if (marker.Category != RpgMapMarkerCategory.DangerZone)
                    {
                        continue;
                    }

                    Vector2 candidateAnchor = new Vector2(marker.Tile.x + 0.5f, marker.Tile.y + 0.5f);
                    for (int d = 0; d < cardinalDirs.Length; d++)
                    {
                        bool clear = true;
                        for (int step = 1; step <= 50; step += 2)
                        {
                            Vector2 pt = candidateAnchor + (cardinalDirs[d] * step);
                            if (!map.IsWalkable(Mathf.FloorToInt(pt.x), Mathf.FloorToInt(pt.y), gateOpen: true))
                            {
                                clear = false;
                                break;
                            }
                        }

                        if (clear)
                        {
                            targetMarkerId = marker.Id;
                            anchor = candidateAnchor;
                            direction = cardinalDirs[d];
                            break;
                        }
                    }
                }

                Assert.AreNotEqual(Vector2.zero, direction,
                    $"Expected a DangerZone with a clear walkable pursuit corridor on {region}.");

                // Large frame steps must clamp pursuit to LeashRadiusTiles without overshooting.
                float[] lureDistances = { 18f, 34f, 50f };
                for (int i = 0; i < lureDistances.Length; i++)
                {
                    Assert.IsEmpty(director.Step(anchor + (direction * lureDistances[i]), playerSprinting: true, deltaSeconds: 10f));
                    Assert.IsTrue(director.TryGetNearestActiveThreat(anchor, out RpgWorldPatrolSnapshot threat));
                    Assert.AreEqual(targetMarkerId, threat.MarkerId);
                    Assert.LessOrEqual(Vector2.Distance(anchor, threat.Position),
                        RpgWorld500xPatrolAndEncounterDirector.LeashRadiusTiles + 0.01f);
                }

                Assert.IsTrue(director.TryGetNearestActiveThreat(anchor, out RpgWorldPatrolSnapshot atEdge));
                Assert.AreEqual(RpgWorld500xPatrolAndEncounterDirector.LeashRadiusTiles,
                    Vector2.Distance(anchor, atEdge.Position), 0.01f);

                // Small real-frame steps with player outside leash must return monotonically without oscillation.
                Assert.IsEmpty(director.Step(anchor + (direction * 50f), playerSprinting: true, deltaSeconds: 0.1f));
                Assert.IsTrue(director.TryGetNearestActiveThreat(anchor, out RpgWorldPatrolSnapshot returning));
                Assert.AreEqual(RpgWorldPatrolState.Patrolling, returning.State);
                float prevDist = Vector2.Distance(anchor, returning.Position);
                Assert.Less(prevDist, RpgWorld500xPatrolAndEncounterDirector.LeashRadiusTiles);

                for (int step = 0; step < 10; step++)
                {
                    Assert.IsEmpty(director.Step(anchor + (direction * 50f), playerSprinting: true, deltaSeconds: 0.1f));
                    Assert.IsTrue(director.TryGetNearestActiveThreat(anchor, out returning));
                    Assert.AreEqual(RpgWorldPatrolState.Patrolling, returning.State);
                    float dist = Vector2.Distance(anchor, returning.Position);
                    Assert.LessOrEqual(dist, prevDist + 0.001f);
                    prevDist = dist;
                }

                Assert.IsFalse(bridge.IsInDangerEncounter);

                // Re-entering inside the leash immediately reacquires AlertChasing.
                Assert.IsEmpty(director.Step(anchor + (direction * 18f), playerSprinting: true, deltaSeconds: 0.1f));
                Assert.IsTrue(director.TryGetNearestActiveThreat(anchor, out RpgWorldPatrolSnapshot reacquired));
                Assert.AreEqual(RpgWorldPatrolState.AlertChasing, reacquired.State);
            }
        }

        [Test]
        public void FourRegion_PatrolDirector_RejectsNonFiniteInputsWithoutTriggeringEncounter()
        {
            IReadOnlyList<RpgExpandedMapSpec> specs = RpgExpandedWorldScale.AllSpecs;
            Assert.AreEqual(4, specs.Count);

            for (int r = 0; r < specs.Count; r++)
            {
                RpgWorldMapId region = specs[r].MapId;
                var store = new Dictionary<string, int>(StringComparer.Ordinal);
                RpgWorld500xMapLayout map = RpgWorld500xMapLayout.Build(region);
                var bridge = new RpgWorldOrkExplorationBridge(
                    region,
                    k => store.TryGetValue(k, out int v) ? v : 0,
                    (k, v) => store[k] = v);
                var director = new RpgWorld500xPatrolAndEncounterDirector(map, bridge);

                Assert.IsEmpty(director.Step(new Vector2(float.NaN, 100f), playerSprinting: true, deltaSeconds: 0.1f));
                Assert.IsEmpty(director.Step(new Vector2(100f, float.PositiveInfinity), playerSprinting: true, deltaSeconds: 0.1f));
                Assert.IsEmpty(director.Step(new Vector2(100f, 100f), playerSprinting: false, deltaSeconds: float.NaN));
                Assert.IsFalse(director.TryGetNearestActiveThreat(new Vector2(float.NaN, 100f), out _));
                Assert.IsFalse(bridge.IsInDangerEncounter);
            }
        }
    }
}
