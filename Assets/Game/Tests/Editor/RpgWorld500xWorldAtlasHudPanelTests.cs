using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xWorldAtlasHudPanelTests
    {
        [Test]
        public void WorldAtlasHudPanel_CapturesEncounterBlockedAndRecoveredFrames()
        {
            var root = new GameObject("WorldAtlasVisualEvidenceRoot");
            try
            {
                using (var boot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.Soundwell,
                    overrideTileSlices: new Sprite[] { null }))
                using (var panel = new RpgWorld500xWorldAtlasHudPanel(root.transform, boot))
                {
                    Assert.IsTrue(panel.Show());
                    var host = boot.Driver.Facade.Session.Host;
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
                    panel.Refresh();
                    var canvas = root.GetComponentInChildren<Canvas>();
                    Assert.IsNotNull(canvas);
                    Assert.IsTrue(panel.StatusBannerText.Contains("遭遇战进行中"));
                    CaptureAtlasFrame(canvas, "world500x-atlas-encounter-blocked");

                    Assert.IsTrue(host.Bridge.TryResolveDangerZoneVictory(danger.Id, true, out _));
                    panel.Refresh();
                    Assert.IsFalse(panel.StatusBannerText.Contains("遭遇战进行中"));
                    CaptureAtlasFrame(canvas, "world500x-atlas-encounter-recovered");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void CaptureAtlasFrame(Canvas canvas, string name)
        {
            // Nonblank panels alone are insufficient: the old Latin font drew
            // the backgrounds and digits but omitted all Chinese labels.
            Font expectedFont = Resources.Load<Font>("Soundwell/Fonts/SoundwellChinese");
            Assert.IsNotNull(expectedFont);
            foreach (UnityEngine.UI.Text label in canvas.GetComponentsInChildren<UnityEngine.UI.Text>())
            {
                Assert.AreSame(expectedFont, label.font, label.name);
                foreach (char glyph in label.text)
                {
                    if (glyph >= '\u4e00' && glyph <= '\u9fff')
                        Assert.IsTrue(label.font.HasCharacter(glyph), "Missing atlas glyph: " + glyph);
                }
            }

            // Use the same actual UGUI camera render approach as M04 readiness evidence.
            const int width = 960;
            const int height = 540;
            var cameraObject = new GameObject("Atlas UI evidence camera", typeof(Camera));
            Camera camera = cameraObject.GetComponent<Camera>();
            RenderTexture target = RenderTexture.GetTemporary(width, height, 24);
            RenderTexture previousActive = RenderTexture.active;
            RenderMode previousMode = canvas.renderMode;
            Camera previousCamera = canvas.worldCamera;
            float previousPlane = canvas.planeDistance;
            var frame = new Texture2D(width, height, TextureFormat.RGB24, false);
            try
            {
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.cullingMask = 1 << canvas.gameObject.layer;
                camera.orthographic = true;
                camera.orthographicSize = 5f;
                camera.transform.position = new Vector3(0f, 0f, -10f);
                camera.targetTexture = target;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1f;
                Canvas.ForceUpdateCanvases();
                camera.Render();

                RenderTexture.active = target;
                frame.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                frame.Apply();
                int visiblePixels = 0;
                foreach (Color32 pixel in frame.GetPixels32())
                    if (pixel.r > 24 || pixel.g > 24 || pixel.b > 24)
                        visiblePixels++;
                Assert.That(visiblePixels, Is.GreaterThan(16000), "Atlas capture cannot be blank.");

                const string folder = "artifacts/p0/story";
                System.IO.Directory.CreateDirectory(folder);
                byte[] png = frame.EncodeToPNG();
                Assert.That(png.Length, Is.GreaterThan(2000));
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(folder, name + ".png"), png);
                // The GitHub-only reviewer can reconstruct the exact archived PNG from
                // bounded, indexed log lines without requiring a local Unity checkout.
                string encoded = System.Convert.ToBase64String(png);
                const int chunkSize = 3000;
                for (int offset = 0; offset < encoded.Length; offset += chunkSize)
                {
                    int count = System.Math.Min(chunkSize, encoded.Length - offset);
                    TestContext.Out.WriteLine("ATLAS_PNG " + name + " " + offset + "/" + encoded.Length +
                        " " + encoded.Substring(offset, count));
                }
            }
            finally
            {
                canvas.renderMode = previousMode;
                canvas.worldCamera = previousCamera;
                canvas.planeDistance = previousPlane;
                camera.targetTexture = null;
                RenderTexture.active = previousActive;
                RenderTexture.ReleaseTemporary(target);
                UnityEngine.Object.DestroyImmediate(frame);
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }
        }

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
