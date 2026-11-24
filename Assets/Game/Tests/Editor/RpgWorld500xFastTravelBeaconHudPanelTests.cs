using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFastTravelBeaconHudPanelTests
    {
        [Test]
        public void FastTravelBeaconHudPanel_SyncsCameraAndWaypointWithoutAdvancingWorldFrame()
        {
            var root = new GameObject("BeaconImmediatePositionSyncRoot");
            var cameraObject = new GameObject("BeaconFollowCamera", typeof(Camera));
            try
            {
                Camera camera = cameraObject.GetComponent<Camera>();
                camera.transform.position = new Vector3(0f, 0f, -10f);
                using (var boot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.RedSaltHarbor,
                    followCamera: camera,
                    overrideTileSlices: new Sprite[] { null }))
                using (var panel = new RpgWorld500xFastTravelBeaconHudPanel(root.transform, boot))
                {
                    var facade = boot.Driver.Facade;
                    var host = facade.Session.Host;
                    boot.Driver.AutomaticFrameUpdates = false;
                    Vector2 start = host.PlayerPosition;
                    Assert.IsTrue(host.Tracker.TryDiscoverMarker("rsh.salt-market"));
                    Assert.IsTrue(panel.Show());
                    float minutes = facade.Atmosphere.TimeOfDayMinutes;
                    int day = facade.Atmosphere.DayCount;
                    Assert.IsTrue(panel.TryClickWarp("rsh.salt-market"));
                    Vector2 arrived = host.PlayerPosition;
                    Assert.AreNotEqual(start, arrived);
                    Assert.AreEqual(new Vector2Int(1700, 1260), host.CurrentTile);
                    Assert.AreEqual(new Vector3(arrived.x, arrived.y, -10f), camera.transform.position);
                    var nearest = facade.GetTopNearbyWaypoints(1);
                    Assert.AreEqual(1, nearest.Count);
                    Assert.AreEqual(nearest[0].HudReadoutZh, boot.Driver.LastNearestWaypointReadoutZh);
                    Assert.IsTrue(boot.StatusHud.WaypointAndEconomyReadout.Contains(nearest[0].HudReadoutZh));
                    Assert.AreEqual(minutes, facade.Atmosphere.TimeOfDayMinutes);
                    Assert.AreEqual(day, facade.Atmosphere.DayCount);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(cameraObject);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void FastTravelBeaconHudPanel_ExplainsEncounterLockAndRestoresWarpAfterVictory()
        {
            var root = new GameObject("BeaconEncounterFeedbackTestRoot");
            try
            {
                using (var boot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.RedSaltHarbor,
                    overrideTileSlices: new Sprite[] { null }))
                using (var panel = new RpgWorld500xFastTravelBeaconHudPanel(root.transform, boot))
                {
                    var host = boot.Driver.Facade.Session.Host;
                    Assert.IsTrue(host.Tracker.TryDiscoverMarker("rsh.salt-market"));
                    Assert.IsTrue(panel.Show());
                    const string buttonPath = "World500xFastTravelBeaconHudPanel/BeaconWarpModalBox/BeaconRows/BeaconRow_rsh.salt-market/WarpButton";
                    var staleButton = root.transform.Find(buttonPath).GetComponent<UnityEngine.UI.Button>();
                    Assert.IsTrue(staleButton.interactable);

                    RpgMapLandmarkMarker danger = default;
                    bool found = false;
                    foreach (RpgMapLandmarkMarker marker in host.Layout.Markers)
                    {
                        if (marker.Category == RpgMapMarkerCategory.DangerZone)
                        {
                            danger = marker;
                            found = true;
                            break;
                        }
                    }

                    Assert.IsTrue(found);
                    Assert.IsTrue(host.Bridge.TryTriggerDangerZoneEncounter(
                        danger.Id, new Vector2(danger.Tile.x + 0.5f, danger.Tile.y + 0.5f)));
                    Vector2 position = host.PlayerPosition;

                    // Exercise the actual click callback before the panel has refreshed.
                    staleButton.onClick.Invoke();
                    Assert.AreEqual(position, host.PlayerPosition);
                    Assert.IsTrue(host.Bridge.IsInDangerEncounter);
                    Assert.IsTrue(panel.StatusText.Contains("遭遇战进行中"));
                    var blockedButton = root.transform.Find(buttonPath).GetComponent<UnityEngine.UI.Button>();
                    Assert.IsFalse(blockedButton.interactable);
                    Assert.AreEqual("战斗中", blockedButton.GetComponentInChildren<UnityEngine.UI.Text>().text);
                    Assert.IsFalse(panel.TryClickWarp("rsh.salt-market"));
                    Assert.GreaterOrEqual(panel.UnlockedBeaconCount, 1);

                    Assert.IsTrue(host.Bridge.TryResolveDangerZoneVictory(danger.Id, true, out _));
                    panel.Refresh();
                    Assert.IsFalse(panel.StatusText.Contains("遭遇战进行中"));
                    Assert.IsTrue(root.transform.Find(buttonPath).GetComponent<UnityEngine.UI.Button>().interactable);
                    Assert.IsTrue(panel.TryClickWarp("rsh.salt-market"));
                    Assert.AreEqual(new Vector2Int(1700, 1260), host.CurrentTile);
                    Assert.IsTrue(panel.StatusText.Contains("已传送至"));
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void FastTravelBeaconHudPanel_WarpsToDiscoveredBeaconRejectsLockedAndDisposesWithZeroLeak()
        {
            var root = new GameObject("World500xFastTravelBeaconHudPanelTestRoot");
            try
            {
                // Warm up one-time UGUI/font caches before steady-state leak baseline.
                using (var warmupBoot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.RedSaltHarbor,
                    overrideTileSlices: new Sprite[] { null }))
                using (var warmupPanel = new RpgWorld500xFastTravelBeaconHudPanel(root.transform, warmupBoot))
                {
                    warmupPanel.Show();
                    warmupPanel.Hide();
                }

                int texBefore = Resources.FindObjectsOfTypeAll<Texture2D>().Length;
                int spriteBefore = Resources.FindObjectsOfTypeAll<Sprite>().Length;

                using (var boot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.RedSaltHarbor,
                    overrideTileSlices: new Sprite[] { null }))
                using (var panel = new RpgWorld500xFastTravelBeaconHudPanel(root.transform, boot))
                {
                    Assert.IsTrue(panel.Show());
                    Assert.Greater(panel.DisplayedBeaconRowCount, 0);

                    // Locked beacon cannot be warped to.
                    Assert.IsFalse(panel.TryClickWarp("rsh.salt-market"));

                    // Discover beacon, refresh panel, and warp via UI click.
                    Assert.IsTrue(boot.Driver.Facade.Session.Host.Tracker.TryDiscoverMarker("rsh.salt-market"));
                    panel.Refresh();
                    Assert.GreaterOrEqual(panel.UnlockedBeaconCount, 1);

                    Assert.IsTrue(panel.TryClickWarp("rsh.salt-market"));
                    Assert.AreEqual(new Vector2Int(1700, 1260), boot.Driver.Facade.Session.Host.CurrentTile);
                    Assert.IsTrue(panel.StatusText.Contains("已传送至"));

                    Assert.IsTrue(panel.Hide());
                    Assert.IsFalse(panel.IsVisible);
                }

                Assert.AreEqual(0, root.transform.childCount);
                Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Texture2D>().Length, texBefore);
                Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Sprite>().Length, spriteBefore);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
