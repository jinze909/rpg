using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xWorldAtlasHudPanelTests
    {
        [Test]
        public void WorldAtlasHudPanel_BlocksTravelAndCampRestDuringEncounterThenRecovers()
        {
            var root = new GameObject("WorldAtlasEncounterActionTestRoot");
            try
            {
                using (var boot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.Soundwell,
                    overrideTileSlices: new Sprite[] { null }))
                using (var panel = new RpgWorld500xWorldAtlasHudPanel(root.transform, boot))
                {
                    Assert.IsTrue(panel.Show());
                    var facade = boot.Driver.Facade;
                    var host = facade.Session.Host;
                    const string travelPath = "World500xAtlasHudPanel/AtlasModalBox/RegionRows/RegionRow_RedSaltHarbor/TravelButton";
                    const string restPath = "World500xAtlasHudPanel/AtlasModalBox/CampRestButton";
                    var staleTravel = root.transform.Find(travelPath).GetComponent<UnityEngine.UI.Button>();
                    var rest = root.transform.Find(restPath).GetComponent<UnityEngine.UI.Button>();
                    Assert.IsTrue(staleTravel.interactable);
                    Assert.IsTrue(rest.interactable);

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
                    float minutes = facade.Atmosphere.TimeOfDayMinutes;
                    int day = facade.Atmosphere.DayCount;
                    Vector2 pos = host.PlayerPosition;
                    string before = panel.LastCampSaveJson;

                    // Direct callbacks exercise stale rendered buttons as well.
                    staleTravel.onClick.Invoke();
                    rest.onClick.Invoke();
                    Assert.IsFalse(panel.TryClickTravelTo(RpgWorldMapId.RedSaltHarbor));
                    Assert.IsFalse(panel.TryClickCampRest());
                    Assert.IsFalse(root.transform.Find(travelPath).GetComponent<UnityEngine.UI.Button>().interactable);
                    Assert.IsFalse(rest.interactable);
                    Assert.IsTrue(panel.StatusBannerText.Contains("遭遇战进行中"));
                    Assert.AreEqual(RpgWorldMapId.Soundwell, facade.CurrentMapId);
                    Assert.AreEqual(pos, host.PlayerPosition);
                    Assert.AreEqual(danger.Id, host.Bridge.ActiveEncounterMarkerId);
                    Assert.AreEqual(minutes, facade.Atmosphere.TimeOfDayMinutes);
                    Assert.AreEqual(day, facade.Atmosphere.DayCount);
                    Assert.AreEqual(before, panel.LastCampSaveJson);

                    Assert.IsTrue(host.Bridge.TryResolveDangerZoneVictory(danger.Id, true, out _));
                    panel.Refresh();
                    Assert.IsFalse(panel.StatusBannerText.Contains("遭遇战进行中"));
                    Assert.IsTrue(rest.interactable);
                    Assert.IsTrue(panel.TryClickCampRest());
                    Assert.IsTrue(RpgWorld500xSaveSnapshotSerializer.TryDeserialize(panel.LastCampSaveJson, out _, out _));
                    Assert.IsTrue(panel.TryClickTravelTo(RpgWorldMapId.RedSaltHarbor));
                    Assert.AreEqual(RpgWorldMapId.RedSaltHarbor, boot.Driver.Facade.CurrentMapId);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void WorldAtlasHudPanel_TravelsConnectedRegionsRestsAtCampAndDisposesWithZeroLeak()
        {
            var root = new GameObject("World500xAtlasHudPanelTestRoot");
            try
            {
                // Warm up one-time UGUI/font caches before steady-state leak baseline.
                using (var warmupBoot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.Soundwell,
                    overrideTileSlices: new Sprite[] { null }))
                using (var warmupPanel = new RpgWorld500xWorldAtlasHudPanel(root.transform, warmupBoot))
                {
                    warmupPanel.Show();
                    warmupPanel.TryClickTravelTo(RpgWorldMapId.RedSaltHarbor);
                }

                int texBefore = Resources.FindObjectsOfTypeAll<Texture2D>().Length;
                int spriteBefore = Resources.FindObjectsOfTypeAll<Sprite>().Length;

                using (var boot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.Soundwell,
                    overrideTileSlices: new Sprite[] { null }))
                using (var panel = new RpgWorld500xWorldAtlasHudPanel(root.transform, boot))
                {
                    Assert.IsFalse(panel.IsVisible);
                    Assert.IsTrue(panel.Show());
                    Assert.IsTrue(panel.IsVisible);
                    Assert.AreEqual(4, panel.DisplayedRegionRowCount);

                    // Non-adjacent MineCamp travel button is disabled from Soundwell.
                    Assert.IsFalse(panel.TryClickTravelTo(RpgWorldMapId.MineCamp));

                    // Adjacent RedSaltHarbor travel button succeeds and switches map.
                    Assert.IsTrue(panel.TryClickTravelTo(RpgWorldMapId.RedSaltHarbor));
                    Assert.AreEqual(RpgWorldMapId.RedSaltHarbor, boot.Driver.Facade.CurrentMapId);
                    Assert.IsTrue(panel.StatusBannerText.Contains("已沿"));

                    // Camp rest button advances to 06:00 Dawn and produces valid checksum save JSON.
                    Assert.IsTrue(panel.TryClickCampRest());
                    Assert.IsTrue(panel.StatusBannerText.Contains("营地休整完成"));
                    Assert.IsTrue(RpgWorld500xSaveSnapshotSerializer.TryDeserialize(panel.LastCampSaveJson, out _, out _));

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
