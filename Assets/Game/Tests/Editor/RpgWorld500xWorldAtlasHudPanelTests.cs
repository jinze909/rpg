using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xWorldAtlasHudPanelTests
    {
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
