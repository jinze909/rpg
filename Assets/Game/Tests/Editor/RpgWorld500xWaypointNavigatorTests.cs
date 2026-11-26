using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xWaypointNavigatorTests
    {
        [Test]
        public void CompassDirections_ResolveAllEightCardinalsAndOrigin()
        {
            Assert.AreEqual("正东", RpgWorld500xWaypointNavigator.ResolveCompassDirectionZh(new Vector2(10f, 0f)));
            Assert.AreEqual("东北", RpgWorld500xWaypointNavigator.ResolveCompassDirectionZh(new Vector2(10f, 10f)));
            Assert.AreEqual("正北", RpgWorld500xWaypointNavigator.ResolveCompassDirectionZh(new Vector2(0f, 10f)));
            Assert.AreEqual("西北", RpgWorld500xWaypointNavigator.ResolveCompassDirectionZh(new Vector2(-10f, 10f)));
            Assert.AreEqual("正西", RpgWorld500xWaypointNavigator.ResolveCompassDirectionZh(new Vector2(-10f, 0f)));
            Assert.AreEqual("西南", RpgWorld500xWaypointNavigator.ResolveCompassDirectionZh(new Vector2(-10f, -10f)));
            Assert.AreEqual("正南", RpgWorld500xWaypointNavigator.ResolveCompassDirectionZh(new Vector2(0f, -10f)));
            Assert.AreEqual("东南", RpgWorld500xWaypointNavigator.ResolveCompassDirectionZh(new Vector2(10f, -10f)));
            Assert.AreEqual("当前位置", RpgWorld500xWaypointNavigator.ResolveCompassDirectionZh(Vector2.zero));
        }

        [Test]
        public void BuildGuidance_RoundsUpFractionalRemainingTilesInsteadOfUnderstatingDistance()
        {
            RpgWorld500xWaypointGuidance guidance = RpgWorld500xWaypointNavigator.BuildGuidance(
                RpgWorldMapId.Soundwell,
                new Vector2(280.1f, 780.5f),
                RpgWorld500xWaypointCategory.UnlootedCache,
                "sw_c03", "补给箱", new Vector2Int(290, 780));

            Assert.AreEqual(10.4f, guidance.DistanceTiles, 0.01f);
            Assert.IsFalse(guidance.IsArrived);
            Assert.IsTrue(guidance.HudReadoutZh.StartsWith("正东 11 格"),
                "Waypoint HUD must not show a shorter remaining distance.");
        }

        [Test]
        public void InvalidArrivalThresholds_NeverMarkDistantWaypointsAsArrived()
        {
            RpgWorld500xMapLayout layout = RpgWorld500xMapLayout.Build(RpgWorldMapId.Soundwell);
            RpgMapLandmarkMarker target = layout.Markers[0];
            Vector2 player = new Vector2(target.Tile.x + 0.5f, target.Tile.y + 0.5f);
            Vector2 distantPlayer = player + new Vector2(20f, 0f);

            foreach (float threshold in new[] { float.PositiveInfinity, float.NegativeInfinity, float.NaN })
            {
                var direct = RpgWorld500xWaypointNavigator.BuildGuidance(
                    layout.MapId, distantPlayer, RpgWorld500xWaypointCategory.UndiscoveredLandmark,
                    target.Id, target.TitleZh, target.Tile, threshold);
                Assert.IsFalse(direct.IsArrived);
                Assert.AreEqual(20f, direct.DistanceTiles, 0.01f);
                StringAssert.Contains("20 格", direct.HudReadoutZh);

                Assert.IsTrue(RpgWorld500xWaypointNavigator.TryFindNearestTarget(
                    layout, player, RpgWorld500xWaypointCategory.UndiscoveredLandmark,
                    out var nearest, arrivalThresholdTiles: threshold));
                Assert.IsFalse(nearest.IsArrived);
                var nearby = RpgWorld500xWaypointNavigator.FindTopNearbyPointsOfInterest(
                    layout, distantPlayer, 5, arrivalThresholdTiles: threshold);
                Assert.IsNotEmpty(nearby);
                Assert.IsTrue(nearby.TrueForAll(g => !g.IsArrived));
            }

            Assert.IsTrue(RpgWorld500xWaypointNavigator.BuildGuidance(
                layout.MapId, player, RpgWorld500xWaypointCategory.UndiscoveredLandmark,
                target.Id, target.TitleZh, target.Tile, 8f).IsArrived);
        }

        [Test]
        public void LandmarkGuidanceUsesPlayerFacingTitlesAndPreservesEntityIds()
        {
            foreach (RpgExpandedMapSpec spec in RpgExpandedWorldScale.AllSpecs)
            {
                RpgWorld500xMapLayout layout = RpgWorld500xMapLayout.Build(spec.MapId);
                Assert.Greater(layout.Markers.Count, 0);
                RpgMapLandmarkMarker target = layout.Markers[0];
                Vector2 player = new Vector2(target.Tile.x + 0.5f, target.Tile.y + 0.5f);
                var tracker = new RpgMapExplorationTracker(spec.MapId);

                Assert.IsTrue(RpgWorld500xWaypointNavigator.TryFindNearestTarget(
                    layout, player, RpgWorld500xWaypointCategory.UndiscoveredLandmark,
                    out RpgWorld500xWaypointGuidance nearest, tracker));
                Assert.AreEqual(target.Id, nearest.EntityId);
                Assert.AreEqual(target.TitleZh, nearest.TargetLabelZh);
                Assert.IsTrue(nearest.HudReadoutZh.Contains(target.TitleZh));

                var nearby = RpgWorld500xWaypointNavigator.FindTopNearbyPointsOfInterest(
                    layout, player, 10, tracker);
                Assert.IsTrue(nearby.Exists(p => p.Category == RpgWorld500xWaypointCategory.UndiscoveredLandmark &&
                    p.EntityId == target.Id && p.TargetLabelZh == target.TitleZh &&
                    p.HudReadoutZh.Contains(target.TitleZh)));
            }
        }

        [Test]
        public void LandmarkGuidance_UsesSavedDiscoveryWhenLiveTrackerIsAbsent()
        {
            RpgWorld500xMapLayout layout = RpgWorld500xMapLayout.Build(RpgWorldMapId.Soundwell);
            RpgMapLandmarkMarker target = layout.Markers[0];
            Vector2 player = new Vector2(target.Tile.x + 0.5f, target.Tile.y + 0.5f);
            var store = new Dictionary<string, int>(StringComparer.Ordinal);
            Func<string, int> read = k => store.TryGetValue(k, out int v) ? v : 0;

            Assert.IsTrue(RpgWorld500xWaypointNavigator.TryFindNearestTarget(
                layout, player, RpgWorld500xWaypointCategory.UndiscoveredLandmark,
                out RpgWorld500xWaypointGuidance first, getInt: read));
            Assert.AreEqual(target.Id, first.EntityId);

            store[RpgMapExplorationTracker.MarkerDiscoveryKey(target.Id)] = 1;
            Assert.IsTrue(RpgWorld500xWaypointNavigator.TryFindNearestTarget(
                layout, player, RpgWorld500xWaypointCategory.UndiscoveredLandmark,
                out RpgWorld500xWaypointGuidance next, getInt: read));
            Assert.AreNotEqual(target.Id, next.EntityId);
            var nearby = RpgWorld500xWaypointNavigator.FindTopNearbyPointsOfInterest(
                layout, player, 100, getInt: read);
            Assert.IsFalse(nearby.Exists(p =>
                p.Category == RpgWorld500xWaypointCategory.UndiscoveredLandmark &&
                p.EntityId == target.Id));

            // Restoring a save from before discovery makes the waypoint available again.
            store.Clear();
            Assert.IsTrue(RpgWorld500xWaypointNavigator.TryFindNearestTarget(
                layout, player, RpgWorld500xWaypointCategory.UndiscoveredLandmark,
                out RpgWorld500xWaypointGuidance restored, getInt: read));
            Assert.AreEqual(target.Id, restored.EntityId);
        }

        [Test]
        public void LandmarkGuidance_AllRegionsStopRecommendingCompletedLandmarksAndRestoreOldSave()
        {
            foreach (RpgExpandedMapSpec spec in RpgExpandedWorldScale.AllSpecs)
            {
                RpgWorld500xMapLayout layout = RpgWorld500xMapLayout.Build(spec.MapId);
                Assert.Greater(layout.Markers.Count, 0);
                var store = new Dictionary<string, int>(StringComparer.Ordinal);
                Func<string, int> read = k => store.TryGetValue(k, out int v) ? v : 0;
                foreach (RpgMapLandmarkMarker marker in layout.Markers)
                    store[RpgMapExplorationTracker.MarkerDiscoveryKey(marker.Id)] = 1;

                RpgMapLandmarkMarker target = layout.Markers[0];
                Vector2 player = new Vector2(target.Tile.x + 0.5f, target.Tile.y + 0.5f);
                Assert.IsFalse(RpgWorld500xWaypointNavigator.TryFindNearestTarget(
                    layout, player, RpgWorld500xWaypointCategory.UndiscoveredLandmark,
                    out _, getInt: read), spec.MapId.ToString());
                Assert.IsFalse(RpgWorld500xWaypointNavigator.FindTopNearbyPointsOfInterest(
                    layout, player, int.MaxValue, getInt: read).Exists(
                        p => p.Category == RpgWorld500xWaypointCategory.UndiscoveredLandmark));

                store.Remove(RpgMapExplorationTracker.MarkerDiscoveryKey(target.Id));
                Assert.IsTrue(RpgWorld500xWaypointNavigator.TryFindNearestTarget(
                    layout, player, RpgWorld500xWaypointCategory.UndiscoveredLandmark,
                    out RpgWorld500xWaypointGuidance restored, getInt: read));
                Assert.AreEqual(target.Id, restored.EntityId);
                Assert.IsTrue(restored.IsArrived);
                var nearby = RpgWorld500xWaypointNavigator.FindTopNearbyPointsOfInterest(
                    layout, player, int.MaxValue, getInt: read);
                Assert.AreEqual(1, nearby.FindAll(
                    p => p.Category == RpgWorld500xWaypointCategory.UndiscoveredLandmark).Count);
            }
        }

        [Test]
        public void TryFindNearestTarget_SkipsLootedCachesAndRanksTopNearbyPois()
        {
            var store = new Dictionary<string, int>(StringComparer.Ordinal);
            Func<string, int> read = k => store.TryGetValue(k, out int v) ? v : 0;
            Action<string, int> write = (k, v) => store[k] = v;

            RpgWorld500xMapLayout soundwell = RpgWorld500xMapLayout.Build(RpgWorldMapId.Soundwell);
            var tracker = new RpgMapExplorationTracker(RpgWorldMapId.Soundwell);
            var cachesCtrl = new RpgWorld500xCachesAndShortcutsController(soundwell, read, write);

            Vector2 player = new Vector2(290.5f, 780.5f);

            // Nearest cache is sw_c03 right at (290, 780)
            Assert.IsTrue(RpgWorld500xWaypointNavigator.TryFindNearestTarget(
                soundwell, player, RpgWorld500xWaypointCategory.UnlootedCache,
                out RpgWorld500xWaypointGuidance first, tracker, cachesCtrl, read));
            Assert.AreEqual("sw_c03", first.EntityId);
            Assert.IsTrue(first.IsArrived);
            Assert.IsTrue(first.HudReadoutZh.Contains("已抵达"));
            Assert.AreEqual("北廊听音带", first.DistrictNameZh);

            // After looting sw_c03, navigator advances to next nearest unlooted cache
            Assert.IsTrue(cachesCtrl.TryLootNearbyCache(player, 6f, out _));
            Assert.IsTrue(RpgWorld500xWaypointNavigator.TryFindNearestTarget(
                soundwell, player, RpgWorld500xWaypointCategory.UnlootedCache,
                out RpgWorld500xWaypointGuidance next, tracker, cachesCtrl, read));
            Assert.AreNotEqual("sw_c03", next.EntityId);
            Assert.IsFalse(next.IsArrived);
            Assert.IsTrue(next.HudReadoutZh.Contains("格"));

            // Top nearby POIs returns sorted ascending distances
            List<RpgWorld500xWaypointGuidance> top5 = RpgWorld500xWaypointNavigator.FindTopNearbyPointsOfInterest(
                soundwell, player, 5, tracker, cachesCtrl, read);
            Assert.AreEqual(5, top5.Count);
            for (int i = 1; i < top5.Count; i++)
            {
                Assert.LessOrEqual(top5[i - 1].DistanceTiles, top5[i].DistanceTiles);
            }

            // Invalid player coordinates return empty cleanly
            Assert.IsFalse(RpgWorld500xWaypointNavigator.TryFindNearestTarget(
                soundwell, new Vector2(float.NaN, 0f), RpgWorld500xWaypointCategory.FieldNpc, out _));
        }
    }
}
