using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFastTravelBeaconHudPanelTests
    {
        [Test]
        public void FastTravelBeaconHudPanel_DisabledCanvasRejectsWarpUntilRestored()
        {
            var root = new GameObject("BeaconDisabledCanvasRoot");
            try
            {
                using (var boot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.RedSaltHarbor,
                    overrideTileSlices: new Sprite[] { null }))
                using (var panel = new RpgWorld500xFastTravelBeaconHudPanel(root.transform, boot))
                {
                    var host = boot.Driver.Facade.Session.Host;
                    const string marker = "rsh.salt-market";
                    Assert.IsTrue(host.Tracker.TryDiscoverMarker(marker));
                    Assert.IsTrue(panel.Show());
                    var canvas = root.transform.Find("World500xFastTravelBeaconHudPanel").GetComponent<Canvas>();
                    var button = canvas.transform.Find("BeaconWarpModalBox/BeaconRows/BeaconRow_rsh.salt-market/WarpButton")
                        .GetComponent<UnityEngine.UI.Button>();
                    Vector2 start = host.PlayerPosition;
                    string status = panel.StatusText;
                    Assert.IsTrue(button.IsInteractable());

                    canvas.enabled = false;
                    Assert.IsTrue(button.IsInteractable());
                    button.onClick.Invoke();
                    Assert.IsFalse(panel.TryClickWarp(marker));
                    Assert.AreEqual(start, host.PlayerPosition);
                    Assert.AreEqual(status, panel.StatusText);

                    canvas.enabled = true;
                    Assert.IsTrue(panel.TryClickWarp(marker));
                    Assert.AreEqual(new Vector2Int(1700, 1260), host.CurrentTile);
                    Assert.That(panel.StatusText, Does.Contain("已传送至"));
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

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
        public void FastTravelBeaconHudPanel_RejectsHiddenDisabledRetiredAndDisposedActions()
        {
            var root = new GameObject("BeaconStaleActionTestRoot");
            try
            {
                using (var boot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.RedSaltHarbor,
                    overrideTileSlices: new Sprite[] { null }))
                using (var panel = new RpgWorld500xFastTravelBeaconHudPanel(root.transform, boot))
                {
                    var host = boot.Driver.Facade.Session.Host;
                    Vector2 start = host.PlayerPosition;
                    const string marker = "rsh.salt-market";
                    const string path = "World500xFastTravelBeaconHudPanel/BeaconWarpModalBox/BeaconRows/BeaconRow_rsh.salt-market/WarpButton";
                    Assert.IsTrue(host.Tracker.TryDiscoverMarker(marker));
                    Assert.IsTrue(panel.Show());
                    var button = root.transform.Find(path).GetComponent<UnityEngine.UI.Button>();
                    var retiredClick = button.onClick;

                    Assert.IsTrue(panel.Hide());
                    Assert.IsFalse(panel.TryClickWarp(marker));
                    retiredClick.Invoke();
                    Assert.AreEqual(start, host.PlayerPosition);

                    Assert.IsTrue(panel.Show());
                    retiredClick.Invoke();
                    Assert.AreEqual(start, host.PlayerPosition);

                    button = root.transform.Find(path).GetComponent<UnityEngine.UI.Button>();
                    button.interactable = false;
                    button.onClick.Invoke();
                    Assert.IsFalse(panel.TryClickWarp(marker));
                    Assert.AreEqual(start, host.PlayerPosition);

                    panel.Refresh();
                    button = root.transform.Find(path).GetComponent<UnityEngine.UI.Button>();
                    var disposedClick = button.onClick;
                    Assert.IsTrue(panel.TryClickWarp(marker));
                    Assert.AreEqual(new Vector2Int(1700, 1260), host.CurrentTile);
                    Vector2 arrived = host.PlayerPosition;
                    panel.Dispose();
                    disposedClick.Invoke();
                    Assert.IsFalse(panel.TryClickWarp(marker));
                    Assert.AreEqual(arrived, host.PlayerPosition);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void FastTravelBeaconHudPanel_RespectsParentInteractionLockAndInactiveHierarchy()
        {
            var root = new GameObject("BeaconParentInteractionLockRoot");
            try
            {
                using (var boot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.RedSaltHarbor,
                    overrideTileSlices: new Sprite[] { null }))
                using (var panel = new RpgWorld500xFastTravelBeaconHudPanel(root.transform, boot))
                {
                    var host = boot.Driver.Facade.Session.Host;
                    const string marker = "rsh.salt-market";
                    const string path = "World500xFastTravelBeaconHudPanel/BeaconWarpModalBox/BeaconRows/BeaconRow_rsh.salt-market/WarpButton";
                    Assert.IsTrue(host.Tracker.TryDiscoverMarker(marker));
                    Assert.IsTrue(panel.Show());
                    var button = root.transform.Find(path).GetComponent<UnityEngine.UI.Button>();
                    Vector2 start = host.PlayerPosition;
                    string status = panel.StatusText;
                    var group = button.transform.parent.gameObject.AddComponent<CanvasGroup>();
                    group.interactable = false;
                    Assert.IsTrue(button.interactable);
                    Assert.IsFalse(button.IsInteractable());
                    button.onClick.Invoke();
                    Assert.IsFalse(panel.TryClickWarp(marker));
                    Assert.AreEqual(start, host.PlayerPosition);
                    Assert.AreEqual(status, panel.StatusText);

                    group.interactable = true;
                    Assert.IsTrue(button.IsInteractable());
                    root.SetActive(false);
                    button.onClick.Invoke();
                    Assert.IsFalse(panel.TryClickWarp(marker));
                    Assert.AreEqual(start, host.PlayerPosition);
                    Assert.AreEqual(status, panel.StatusText);

                    root.SetActive(true);
                    Assert.IsTrue(button.IsInteractable());
                    Assert.IsTrue(panel.TryClickWarp(marker));
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
        public void FastTravelBeaconHudPanel_ReportsFailureForOutdatedRegionRowAndRecovers()
        {
            var root = new GameObject("BeaconOutdatedRegionRoot");
            try
            {
                using (var boot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.RedSaltHarbor,
                    overrideTileSlices: new Sprite[] { null }))
                using (var panel = new RpgWorld500xFastTravelBeaconHudPanel(root.transform, boot))
                {
                    const string marker = "rsh.salt-market";
                    var initialHost = boot.Driver.Facade.Session.Host;
                    // Discover through the bridge so the normal persisted discovery survives remounting.
                    initialHost.Bridge.UpdatePlayerPosition(new Vector2(1700.5f, 1260.5f));
                    Assert.IsTrue(initialHost.Tracker.IsMarkerDiscovered(marker));
                    Assert.IsTrue(panel.Show());
                    Assert.IsTrue(boot.SwitchMap(RpgWorldMapId.WhitewaterCrossing));
                    var facade = boot.Driver.Facade;
                    Vector2 start = facade.Session.Host.PlayerPosition;
                    float minutes = facade.Atmosphere.TimeOfDayMinutes;
                    int day = facade.Atmosphere.DayCount;
                    Assert.IsFalse(panel.TryClickWarp(marker));
                    Assert.AreEqual(RpgWorldMapId.WhitewaterCrossing, facade.CurrentMapId);
                    Assert.AreEqual(start, facade.Session.Host.PlayerPosition);
                    Assert.AreEqual(minutes, facade.Atmosphere.TimeOfDayMinutes);
                    Assert.AreEqual(day, facade.Atmosphere.DayCount);
                    Assert.That(panel.StatusText, Does.Contain("当前不可用"));
                    Assert.IsFalse(panel.TryClickWarp(marker));

                    Assert.IsTrue(boot.SwitchMap(RpgWorldMapId.RedSaltHarbor));
                    Assert.IsTrue(boot.Driver.Facade.Session.Host.Tracker.IsMarkerDiscovered(marker));
                    panel.Refresh();
                    Assert.IsTrue(panel.TryClickWarp(marker));
                    Assert.AreEqual(new Vector2Int(1700, 1260), facade.Session.Host.CurrentTile);
                    Assert.That(panel.StatusText, Does.Contain("已传送至"));
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void FastTravelBeaconHudPanel_RespectsParentCanvasGroupInputLock()
        {
            var root = new GameObject("BeaconCanvasGroupLockRoot");
            try
            {
                using (var boot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.RedSaltHarbor,
                    overrideTileSlices: new Sprite[] { null }))
                using (var panel = new RpgWorld500xFastTravelBeaconHudPanel(root.transform, boot))
                {
                    var host = boot.Driver.Facade.Session.Host;
                    const string marker = "rsh.salt-market";
                    Assert.IsTrue(host.Tracker.TryDiscoverMarker(marker));
                    Assert.IsTrue(panel.Show());
                    Transform modal = root.transform.Find("World500xFastTravelBeaconHudPanel/BeaconWarpModalBox");
                    var group = modal.gameObject.AddComponent<CanvasGroup>();
                    var button = modal.Find("BeaconRows/BeaconRow_rsh.salt-market/WarpButton")
                        .GetComponent<UnityEngine.UI.Button>();
                    Vector2 start = host.PlayerPosition;
                    string status = panel.StatusText;

                    group.interactable = false;
                    Assert.IsTrue(button.interactable, "The row itself remains enabled.");
                    Assert.IsFalse(button.IsInteractable(), "The ancestor must block selection.");
                    button.onClick.Invoke();
                    Assert.IsFalse(panel.TryClickWarp(marker));
                    Assert.AreEqual(start, host.PlayerPosition);
                    Assert.AreEqual(status, panel.StatusText);

                    group.interactable = true;
                    Assert.IsTrue(button.IsInteractable());
                    Assert.IsTrue(panel.TryClickWarp(marker));
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
