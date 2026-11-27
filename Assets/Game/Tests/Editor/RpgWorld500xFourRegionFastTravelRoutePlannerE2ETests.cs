using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionFastTravelRoutePlannerE2ETests
    {
        [Test]
        public void AllFourRegions_BeaconNetworkSummaryAndNearestBeaconQueriesReachFullCoverage()
        {
            int verifiedRegions = 0;

            foreach (RpgExpandedMapSpec spec in RpgExpandedWorldScale.AllSpecs)
            {
                var tracker = new RpgMapExplorationTracker(spec.MapId);
                RpgWorld500xBeaconNetworkSummary initial =
                    RpgWorld500xFastTravelRoutePlanner.BuildBeaconNetworkSummary(spec.MapId, tracker);

                Assert.AreEqual(spec.MapId, initial.MapId);
                Assert.Greater(initial.TotalBeaconCount, 1, "Expected multiple fast-travel beacons in " + spec.MapId);
                Assert.AreEqual(initial.TotalBeaconCount - initial.DiscoveredBeaconCount, initial.UndiscoveredBeaconCount);
                StringAssert.Contains("【传送烽火】", initial.SummaryZh);

                // Non-finite probe coordinates fail closed.
                Assert.IsFalse(RpgWorld500xFastTravelRoutePlanner.TryFindNearestUndiscoveredBeacon(
                    spec.MapId, new Vector2(float.NaN, 0f), tracker, out _, out _));
                Assert.IsFalse(RpgWorld500xFastTravelRoutePlanner.TryFindNearestDiscoveredBeacon(
                    spec.MapId, new Vector2(0f, float.PositiveInfinity), tracker, out _, out _));

                int previousDiscovered = initial.DiscoveredBeaconCount;
                float previousRatio = initial.CoverageRatio;
                int safetyGuard = initial.TotalBeaconCount + 2;
                Vector2 probe = new Vector2(500f, 500f);

                while (RpgWorld500xFastTravelRoutePlanner.TryFindNearestUndiscoveredBeacon(
                    spec.MapId, probe, tracker, out RpgMapLandmarkMarker targetBeacon, out float distToTarget))
                {
                    Assert.Greater(safetyGuard--, 0, "Exceeded expected beacon count in " + spec.MapId);
                    Assert.IsTrue(targetBeacon.SupportsFastTravel);
                    Assert.GreaterOrEqual(distToTarget, 0f);
                    Assert.IsFalse(tracker.IsMarkerDiscovered(targetBeacon.Id));

                    Assert.IsTrue(tracker.TryDiscoverMarker(targetBeacon.Id));
                    Vector2 beaconCenter = new Vector2(targetBeacon.Tile.x + 0.5f, targetBeacon.Tile.y + 0.5f);

                    Assert.IsTrue(RpgWorld500xFastTravelRoutePlanner.TryFindNearestDiscoveredBeacon(
                        spec.MapId, beaconCenter, tracker, out RpgMapLandmarkMarker nearestDisc, out float discDist));
                    Assert.AreEqual(targetBeacon.Id, nearestDisc.Id);
                    Assert.AreEqual(0f, discDist, 0.01f);

                    RpgWorld500xBeaconNetworkSummary progress =
                        RpgWorld500xFastTravelRoutePlanner.BuildBeaconNetworkSummary(spec.MapId, tracker);
                    Assert.AreEqual(previousDiscovered + 1, progress.DiscoveredBeaconCount);
                    Assert.Greater(progress.CoverageRatio, previousRatio);

                    previousDiscovered = progress.DiscoveredBeaconCount;
                    previousRatio = progress.CoverageRatio;
                    probe = beaconCenter;
                }

                RpgWorld500xBeaconNetworkSummary finalSummary =
                    RpgWorld500xFastTravelRoutePlanner.BuildBeaconNetworkSummary(spec.MapId, tracker);
                Assert.AreEqual(initial.TotalBeaconCount, finalSummary.DiscoveredBeaconCount);
                Assert.AreEqual(0, finalSummary.UndiscoveredBeaconCount);
                Assert.AreEqual(1f, finalSummary.CoverageRatio, 0.0001f);
                StringAssert.Contains("已全部点亮", finalSummary.SummaryZh);

                verifiedRegions++;
            }

            Assert.AreEqual(4, verifiedRegions);
        }
    }
}
