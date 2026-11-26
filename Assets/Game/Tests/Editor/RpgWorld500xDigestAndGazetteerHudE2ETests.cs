using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xDigestAndGazetteerHudE2ETests
    {
        [Test]
        public void ExpeditionDigestAndWorldGazetteerHudPanels_SyncAcrossWeatherChangesRouteToggleAndSearchWithZeroLeak()
        {
            var root = new GameObject("World500xDigestGazetteerE2ERoot");
            try
            {
                // Warm up one-time UGUI/font caches before steady-state leak baseline.
                using (var warmupBoot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.Soundwell,
                    overrideTileSlices: new Sprite[] { null }))
                using (var warmupDigest = new RpgWorld500xExpeditionDigestHudPanel(root.transform, warmupBoot))
                using (var warmupGazetteer = new RpgWorld500xWorldGazetteerHudPanel(root.transform, warmupBoot))
                {
                    warmupDigest.Show();
                    warmupDigest.Hide();
                    warmupGazetteer.Show();
                    warmupGazetteer.Hide();
                }

                int texBefore = Resources.FindObjectsOfTypeAll<Texture2D>().Length;
                int spriteBefore = Resources.FindObjectsOfTypeAll<Sprite>().Length;

                using (var boot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.Soundwell,
                    overrideTileSlices: new Sprite[] { null }))
                using (var digest = new RpgWorld500xExpeditionDigestHudPanel(root.transform, boot))
                using (var gazetteer = new RpgWorld500xWorldGazetteerHudPanel(root.transform, boot))
                {
                    var atmosphere = boot.Driver.Facade.Atmosphere;
                    atmosphere.SetWeather(RpgWorldMapId.Soundwell, RpgWorld500xWeatherKind.Clear);
                    atmosphere.SetWeather(RpgWorldMapId.RedSaltHarbor, RpgWorld500xWeatherKind.Clear);
                    atmosphere.SetWeather(RpgWorldMapId.WhitewaterCrossing, RpgWorld500xWeatherKind.Clear);
                    atmosphere.SetWeather(RpgWorldMapId.MineCamp, RpgWorld500xWeatherKind.Clear);

                    // Verify safe baseline on ExpeditionDigest.
                    Assert.IsTrue(digest.Show());
                    Assert.IsTrue(digest.IsVisible);
                    Assert.AreEqual(RpgWorld500xTravelRiskLevel.Safe, digest.LastDigest.TravelRiskLevel);
                    Assert.AreEqual(0, digest.HazardousRegionCount);

                    // Escalate weather in two regions -> digest reflects Hazardous risk and hazard count.
                    atmosphere.SetWeather(RpgWorldMapId.Soundwell, RpgWorld500xWeatherKind.GaleStorm);
                    atmosphere.SetWeather(RpgWorldMapId.RedSaltHarbor, RpgWorld500xWeatherKind.GaleStorm);
                    digest.Refresh();
                    Assert.AreEqual(RpgWorld500xTravelRiskLevel.Hazardous, digest.LastDigest.TravelRiskLevel);
                    Assert.GreaterOrEqual(digest.HazardousRegionCount, 2);
                    Assert.IsTrue(digest.TryClickClose());
                    Assert.IsFalse(digest.IsVisible);

                    // Open WorldGazetteer on the same bootstrapper: verify 4 regions, safest-route toggle, and search.
                    Assert.IsTrue(gazetteer.Show());
                    Assert.IsTrue(gazetteer.IsVisible);
                    Assert.AreEqual(RpgWorld500xGazetteerHudTab.RegionOverview, gazetteer.ActiveTab);
                    Assert.AreEqual(4, gazetteer.DisplayedRowCount);
                    Assert.AreEqual(4, gazetteer.LastCards.Count);
                    Assert.IsFalse(gazetteer.PreferSafestRoute);
                    StringAssert.Contains("路径：最短耗时", gazetteer.StatusText);
                    StringAssert.Contains("补给箱 42", gazetteer.HeaderText);
                    StringAssert.Contains("捷径 16", gazetteer.HeaderText);
                    StringAssert.Contains("悬赏 12", gazetteer.HeaderText);

                    Assert.IsTrue(gazetteer.TryToggleSafestRoute());
                    Assert.IsTrue(gazetteer.PreferSafestRoute);
                    StringAssert.Contains("路径：避险优先", gazetteer.StatusText);

                    Assert.IsTrue(gazetteer.TrySearch("声井测绘初录"));
                    Assert.Greater(gazetteer.LastSearchHitCount, 0);
                    Assert.IsTrue(gazetteer.TryClickClearSearch());
                    Assert.AreEqual(0, gazetteer.LastSearchHitCount);
                    Assert.IsTrue(gazetteer.TryClickClose());
                    Assert.IsFalse(gazetteer.IsVisible);
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
