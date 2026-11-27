using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionBeaconNetworkExhaustionE2ETests
    {
        [Test]
        public void FourRegion_FastTravelRoutePlanner_ExhaustsAllUndiscoveredBeaconsAcrossAllFourRegions()
        {
            IReadOnlyList<RpgExpandedMapSpec> specs = RpgExpandedWorldScale.AllSpecs;
            Assert.AreEqual(4, specs.Count);

            var litBeaconIds = new HashSet<string>(StringComparer.Ordinal);

            for (int r = 0; r < specs.Count; r++)
            {
                RpgWorldMapId region = specs[r].MapId;
                var tracker = new RpgMapExplorationTracker(region);

                RpgWorld500xBeaconNetworkSummary initial =
                    RpgWorld500xFastTravelRoutePlanner.BuildBeaconNetworkSummary(region, tracker);
                Assert.AreEqual(region, initial.MapId);
                Assert.Greater(initial.TotalBeaconCount, 1, $"Expected multiple fast-travel beacons on {region}.");
                Assert.Greater(initial.UndiscoveredBeaconCount, 0);
                Assert.AreEqual(initial.TotalBeaconCount - initial.DiscoveredBeaconCount, initial.UndiscoveredBeaconCount);
                StringAssert.Contains("【传送烽火】", initial.SummaryZh);

                Vector2 cursor = new Vector2(100f, 100f);
                int remainingToLight = initial.UndiscoveredBeaconCount;

                for (int i = 0; i < remainingToLight; i++)
                {
                    Assert.IsTrue(
                        RpgWorld500xFastTravelRoutePlanner.TryFindNearestUndiscoveredBeacon(
                            region, cursor, tracker, out RpgMapLandmarkMarker nextBeacon, out float dist),
                        $"Expected undiscovered beacon #{i + 1} on {region}.");
                    Assert.IsTrue(nextBeacon.SupportsFastTravel);
                    Assert.GreaterOrEqual(dist, 0f);
                    Assert.IsFalse(tracker.IsMarkerDiscovered(nextBeacon.Id));

                    Assert.IsTrue(tracker.TryDiscoverMarker(nextBeacon.Id));
                    Assert.IsTrue(litBeaconIds.Add(nextBeacon.Id), $"Duplicate beacon ID {nextBeacon.Id}.");

                    Vector2 beaconCenter = new Vector2(nextBeacon.Tile.x + 0.5f, nextBeacon.Tile.y + 0.5f);
                    Assert.IsTrue(
                        RpgWorld500xFastTravelRoutePlanner.TryFindNearestDiscoveredBeacon(
                            region, beaconCenter, tracker, out RpgMapLandmarkMarker nearestDisc, out float discDist));
                    Assert.AreEqual(nextBeacon.Id, nearestDisc.Id);
                    Assert.AreEqual(0f, discDist, 0.01f);

                    cursor = beaconCenter;
                }

                RpgWorld500xBeaconNetworkSummary finalSummary =
                    RpgWorld500xFastTravelRoutePlanner.BuildBeaconNetworkSummary(region, tracker);
                Assert.AreEqual(finalSummary.TotalBeaconCount, finalSummary.DiscoveredBeaconCount);
                Assert.AreEqual(0, finalSummary.UndiscoveredBeaconCount);
                Assert.AreEqual(1f, finalSummary.CoverageRatio, 0.0001f);
                StringAssert.Contains("已全部点亮", finalSummary.SummaryZh);
                Assert.IsFalse(
                    RpgWorld500xFastTravelRoutePlanner.TryFindNearestUndiscoveredBeacon(
                        region, cursor, tracker, out _, out _));
            }

            Assert.GreaterOrEqual(litBeaconIds.Count, 12);
        }

        [Test]
        public void FourRegion_FastTravelRoutePlanner_RejectsInvalidMapNullTrackerAndNonFiniteCoordinates()
        {
            var soundwellTracker = new RpgMapExplorationTracker(RpgWorldMapId.Soundwell);
            RpgWorld500xBeaconNetworkSummary invalidSummary =
                RpgWorld500xFastTravelRoutePlanner.BuildBeaconNetworkSummary((RpgWorldMapId)255, soundwellTracker);
            Assert.AreEqual(0, invalidSummary.TotalBeaconCount);
            StringAssert.Contains("未知大区", invalidSummary.SummaryZh);

            Assert.IsFalse(RpgWorld500xFastTravelRoutePlanner.TryFindNearestDiscoveredBeacon(
                (RpgWorldMapId)255, Vector2.zero, soundwellTracker, out _, out _));
            Assert.IsFalse(RpgWorld500xFastTravelRoutePlanner.TryFindNearestUndiscoveredBeacon(
                (RpgWorldMapId)255, Vector2.zero, soundwellTracker, out _, out _));

            IReadOnlyList<RpgExpandedMapSpec> specs = RpgExpandedWorldScale.AllSpecs;
            for (int r = 0; r < specs.Count; r++)
            {
                RpgWorldMapId region = specs[r].MapId;
                var tracker = new RpgMapExplorationTracker(region);

                Assert.IsFalse(RpgWorld500xFastTravelRoutePlanner.TryFindNearestDiscoveredBeacon(
                    region, Vector2.zero, null, out _, out _));
                Assert.IsFalse(RpgWorld500xFastTravelRoutePlanner.TryFindNearestUndiscoveredBeacon(
                    region, Vector2.zero, null, out _, out _));
                Assert.IsFalse(RpgWorld500xFastTravelRoutePlanner.TryFindNearestDiscoveredBeacon(
                    region, new Vector2(float.NaN, 10f), tracker, out _, out _));
                Assert.IsFalse(RpgWorld500xFastTravelRoutePlanner.TryFindNearestUndiscoveredBeacon(
                    region, new Vector2(10f, float.PositiveInfinity), tracker, out _, out _));
                Assert.IsFalse(RpgWorld500xFastTravelRoutePlanner.TryFindNearestUndiscoveredBeacon(
                    region, new Vector2(float.NegativeInfinity, 10f), tracker, out _, out _));
            }
        }
    }
}
