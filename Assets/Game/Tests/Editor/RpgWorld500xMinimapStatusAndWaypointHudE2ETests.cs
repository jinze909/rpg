using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xMinimapStatusAndWaypointHudE2ETests
    {
        [Test]
        public void MinimapStatusOverlayAndWaypointNavigator_StayInSyncAcrossDiscoveryAndMapSwitchWithZeroLeak()
        {
            var root = new GameObject("World500xMinimapStatusWaypointE2ERoot");
            RpgWorld500xRuntimeDriver driver = null;
            RpgWorld500xStatusOverlayHud statusHud = null;
            RpgWorld500xMinimapHud minimapHud = null;
            try
            {
                driver = root.AddComponent<RpgWorld500xRuntimeDriver>();

                // Warm up one-time UGUI/font caches before steady-state leak baseline.
                driver.Initialize(
                    overrideMap: RpgWorldMapId.RedSaltHarbor,
                    overrideTileSlices: new Sprite[] { null });
                statusHud = new RpgWorld500xStatusOverlayHud(root.transform);
                statusHud.SyncFromDriver(driver);
                minimapHud = new RpgWorld500xMinimapHud(
                    root.transform,
                    RpgExpandedWorldScale.GetSpec(RpgWorldMapId.RedSaltHarbor),
                    (x, y) => y < 420 ? (int)HarborTileKind.WaterDeep : (int)HarborTileKind.QuayStone,
                    useHarborPalette: true,
                    tracker: driver.Facade.Session.Host.Tracker);
                minimapHud.UpdatePlayerAndCompass(driver.Facade.Session.Host.PlayerPosition);
                minimapHud.Dispose();
                minimapHud = null;
                statusHud.Dispose();
                statusHud = null;
                driver.Shutdown();

                int texBefore = Resources.FindObjectsOfTypeAll<Texture2D>().Length;
                int spriteBefore = Resources.FindObjectsOfTypeAll<Sprite>().Length;

                driver.Initialize(
                    overrideMap: RpgWorldMapId.RedSaltHarbor,
                    overrideTileSlices: new Sprite[] { null });
                statusHud = new RpgWorld500xStatusOverlayHud(root.transform);
                var tracker = driver.Facade.Session.Host.Tracker;
                var layout = driver.Facade.Session.Host.Layout;
                minimapHud = new RpgWorld500xMinimapHud(
                    root.transform,
                    RpgExpandedWorldScale.GetSpec(RpgWorldMapId.RedSaltHarbor),
                    (x, y) => y < 420 ? (int)HarborTileKind.WaterDeep : (int)HarborTileKind.QuayStone,
                    useHarborPalette: true,
                    tracker: tracker);

                int initialVisiblePins = minimapHud.VisiblePinCount;
                Assert.AreEqual(tracker.TotalMarkerCount, minimapHud.PinCount);
                Assert.Less(initialVisiblePins, minimapHud.PinCount);

                // Discover a landmark, track a quest, add silver, and verify WaypointNavigator + Minimap + StatusOverlay align.
                Vector2 probePos = new Vector2(360f, 540f);
                Assert.GreaterOrEqual(tracker.DiscoverMarkersInRange(probePos), 1);
                Assert.IsTrue(tracker.TrySetTrackedQuest("q.rsh.compare-salt-prices"));
                Assert.IsTrue(minimapHud.UpdatePlayerAndCompass(probePos, "外港浅水泊位"));
                Assert.Greater(minimapHud.VisiblePinCount, initialVisiblePins);
                StringAssert.Contains("外港浅水泊位", minimapHud.LastCompassText);
                StringAssert.Contains("摸底赤盐行情", minimapHud.LastCompassText);

                Assert.IsTrue(RpgWorld500xWaypointNavigator.TryFindNearestTarget(
                    layout,
                    probePos,
                    RpgWorld500xWaypointCategory.UndiscoveredLandmark,
                    out RpgWorld500xWaypointGuidance nearest,
                    arrivalThresholdTiles: 8f));
                Assert.IsFalse(string.IsNullOrEmpty(nearest.HudReadoutZh));

                var nearby = RpgWorld500xWaypointNavigator.FindTopNearbyPointsOfInterest(
                    layout,
                    probePos,
                    5,
                    arrivalThresholdTiles: 8f);
                Assert.IsNotEmpty(nearby);

                driver.TickFrame(Vector2.right, 0.2f);
                driver.Facade.BountyAndSupply.AddSaltSilver(60);
                Assert.IsTrue(statusHud.SyncFromDriver(driver));
                Assert.IsFalse(string.IsNullOrEmpty(statusHud.EnvironmentReadout));
                StringAssert.Contains("盐银 60", statusHud.WaypointAndEconomyReadout);
                StringAssert.Contains("探索度", statusHud.WaypointAndEconomyReadout);

                // Overview mode reveals all pins; switching maps updates StatusOverlay cleanly.
                Assert.IsTrue(minimapHud.SetOverviewMode(true));
                Assert.AreEqual(minimapHud.PinCount, minimapHud.VisiblePinCount);

                Assert.IsTrue(driver.SwitchMap(RpgWorldMapId.Soundwell));
                driver.TickFrame(Vector2.zero, 0.1f);
                Assert.IsTrue(statusHud.SyncFromDriver(driver));
                Assert.IsFalse(string.IsNullOrEmpty(statusHud.EnvironmentReadout));

                minimapHud.Dispose();
                minimapHud = null;
                statusHud.Dispose();
                statusHud = null;
                driver.Shutdown();

                Assert.AreEqual(0, root.transform.childCount);
                Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Texture2D>().Length, texBefore);
                Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Sprite>().Length, spriteBefore);
            }
            finally
            {
                minimapHud?.Dispose();
                statusHud?.Dispose();
                if (driver != null)
                {
                    driver.Shutdown();
                }

                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
