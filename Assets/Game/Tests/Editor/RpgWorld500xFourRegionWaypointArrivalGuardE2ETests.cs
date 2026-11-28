using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    [TestFixture]
    public sealed class RpgWorld500xFourRegionWaypointArrivalGuardE2ETests
    {
        [Test]
        public void AllFourRegions_InvalidArrivalThresholds_NeverMarkWaypointsAsArrivedAndKeepValidDistances()
        {
            var specs = RpgExpandedWorldScale.AllSpecs;
            Assert.AreEqual(4, specs.Count, "Expected exactly 4 regions in 500x world scale.");

            float[] invalidThresholds = { float.PositiveInfinity, float.NegativeInfinity, float.NaN };

            foreach (RpgExpandedMapSpec spec in specs)
            {
                RpgWorld500xMapLayout layout = RpgWorld500xMapLayout.Build(spec.MapId);
                RpgMapLandmarkMarker target = layout.Markers[0];
                var atTarget = new Vector2(target.Tile.x + 0.5f, target.Tile.y + 0.5f);
                var distantPlayer = atTarget + new Vector2(20f, 0f);

                foreach (float threshold in invalidThresholds)
                {
                    RpgWorld500xWaypointGuidance direct = RpgWorld500xWaypointNavigator.BuildGuidance(
                        layout.MapId,
                        distantPlayer,
                        RpgWorld500xWaypointCategory.UndiscoveredLandmark,
                        target.Id,
                        target.TitleZh,
                        target.Tile,
                        threshold);

                    Assert.IsFalse(direct.IsArrived, $"Invalid threshold {threshold} must not mark arrival in {spec.MapId}.");
                    Assert.AreEqual(20f, direct.DistanceTiles, 0.01f);
                    StringAssert.Contains("20 格", direct.HudReadoutZh);

                    Assert.IsTrue(RpgWorld500xWaypointNavigator.TryFindNearestTarget(
                        layout,
                        atTarget,
                        RpgWorld500xWaypointCategory.UndiscoveredLandmark,
                        out RpgWorld500xWaypointGuidance nearest,
                        arrivalThresholdTiles: threshold));
                    Assert.IsFalse(nearest.IsArrived);

                    var nearby = RpgWorld500xWaypointNavigator.FindTopNearbyPointsOfInterest(
                        layout,
                        distantPlayer,
                        5,
                        arrivalThresholdTiles: threshold);
                    Assert.Greater(nearby.Count, 0);
                    Assert.IsTrue(nearby.TrueForAll(g => !g.IsArrived));
                }

                RpgWorld500xWaypointGuidance arrived = RpgWorld500xWaypointNavigator.BuildGuidance(
                    layout.MapId,
                    atTarget,
                    RpgWorld500xWaypointCategory.UndiscoveredLandmark,
                    target.Id,
                    target.TitleZh,
                    target.Tile,
                    8f);
                Assert.IsTrue(arrived.IsArrived, $"Valid threshold at target should report arrived in {spec.MapId}.");
            }
        }
    }
}
