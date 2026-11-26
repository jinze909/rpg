using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFastTravelRoutePlannerTests
    {
        [Test]
        public void PlanRoute_RecommendsDiscoveredBeaconWhenFarAndDirectWalkWhenClose()
        {
            var tracker = new RpgMapExplorationTracker(RpgWorldMapId.RedSaltHarbor);
            Assert.IsTrue(tracker.TryDiscoverMarker("rsh.salt-market"));

            // Player far west (200, 520), destination near salt-market (1740, 1280)
            RpgWorld500xRoutePlan farPlan = RpgWorld500xFastTravelRoutePlanner.PlanRoute(
                RpgWorldMapId.RedSaltHarbor,
                new Vector2(200f, 520f),
                new Vector2Int(1740, 1280),
                tracker,
                moveSpeedMultiplier: 0.90f);

            Assert.IsTrue(farPlan.RecommendFastTravel);
            Assert.AreEqual("rsh.salt-market", farPlan.BestBeaconId);
            Assert.Greater(farPlan.TilesSavedByFastTravel, 1000f);
            Assert.Less(farPlan.WalkAfterBeaconTiles, 100f);
            Assert.Greater(farPlan.EstimatedWalkSeconds, farPlan.EstimatedSprintSeconds);
            Assert.IsTrue(farPlan.SummaryZh.Contains("建议传送至"));

            // Player already standing right next to destination -> recommends direct walk
            RpgWorld500xRoutePlan closePlan = RpgWorld500xFastTravelRoutePlanner.PlanRoute(
                RpgWorldMapId.RedSaltHarbor,
                new Vector2(1735f, 1278f),
                new Vector2Int(1740, 1280),
                tracker);

            Assert.IsFalse(closePlan.RecommendFastTravel);
            Assert.AreEqual(0f, closePlan.TilesSavedByFastTravel);
            Assert.IsTrue(closePlan.SummaryZh.Contains("直行更近"));
        }

        [Test]
        public void PlanRoute_FlagsDangerZoneCrossingAlongRecommendedLeg()
        {
            var tracker = new RpgMapExplorationTracker(RpgWorldMapId.Soundwell);
            IReadOnlyList<RpgMapLandmarkMarker> markers = RpgWorldLandmarkCatalog.GetMarkers(RpgWorldMapId.Soundwell);

            RpgMapLandmarkMarker danger = default;
            bool found = false;
            for (int i = 0; i < markers.Count; i++)
            {
                if (markers[i].Category == RpgMapMarkerCategory.DangerZone)
                {
                    danger = markers[i];
                    found = true;
                    break;
                }
            }

            Assert.IsTrue(found, "Soundwell must contain at least one DangerZone marker.");

            // Walk directly across the DangerZone marker (short segment so direct walk is recommended)
            Vector2 start = new Vector2(danger.Tile.x - 10f, danger.Tile.y);
            Vector2Int end = new Vector2Int(danger.Tile.x + 10, danger.Tile.y);
            RpgWorld500xRoutePlan plan = RpgWorld500xFastTravelRoutePlanner.PlanRoute(
                RpgWorldMapId.Soundwell, start, end, tracker);

            Assert.IsFalse(plan.RecommendFastTravel);
            Assert.IsTrue(plan.CrossesDangerZone);
            Assert.IsTrue(plan.SummaryZh.Contains("途经危险区"));
        }
    }
}
