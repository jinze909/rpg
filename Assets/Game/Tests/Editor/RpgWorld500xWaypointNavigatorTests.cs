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
