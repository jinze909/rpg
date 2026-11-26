using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionWeatherAndBadgeE2ETests
    {
        [Test]
        public void WeatherAdvisoryHudBadge_TracksFourRegionHazardsRejectsInvalidOverridesAndCalmsAfterCampfireWithZeroLeak()
        {
            var root = new GameObject("World500xFourRegionWeatherBadgeE2ERoot");
            try
            {
                // Warm up one-time UGUI/font caches before steady-state leak baseline.
                using (var warmupBoot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.Soundwell,
                    overrideTileSlices: new Sprite[] { null }))
                using (var warmupBadge = new RpgWorld500xWeatherAdvisoryHudBadge(root.transform, warmupBoot))
                {
                    warmupBadge.Refresh(0f);
                }

                int texBefore = Resources.FindObjectsOfTypeAll<Texture2D>().Length;
                int spriteBefore = Resources.FindObjectsOfTypeAll<Sprite>().Length;

                using (var boot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.Soundwell,
                    overrideTileSlices: new Sprite[] { null }))
                using (var badge = new RpgWorld500xWeatherAdvisoryHudBadge(root.transform, boot))
                {
                    var atmosphere = boot.Driver.Facade.Atmosphere;
                    atmosphere.SetWeather(RpgWorldMapId.Soundwell, RpgWorld500xWeatherKind.Clear);
                    atmosphere.SetWeather(RpgWorldMapId.RedSaltHarbor, RpgWorld500xWeatherKind.Clear);
                    atmosphere.SetWeather(RpgWorldMapId.WhitewaterCrossing, RpgWorld500xWeatherKind.Clear);
                    atmosphere.SetWeather(RpgWorldMapId.MineCamp, RpgWorld500xWeatherKind.Clear);

                    badge.Refresh(5f);
                    Assert.AreEqual(RpgWorld500xTravelRiskLevel.Safe, badge.LastRiskLevel);
                    Assert.IsFalse(badge.LastRecommendCampfireRest);
                    Assert.AreEqual(0, badge.HazardousRegionCount);
                    Assert.AreEqual(4, badge.LastWorldDigest.RegionReports.Count);
                    StringAssert.Contains("【四境气象速报】", badge.WorldBulletinText);
                    StringAssert.Contains("行路平稳", badge.RiskLabelText);

                    // Escalate RedSaltHarbor to GaleStorm and verify invalid overrides are ignored.
                    atmosphere.SetWeather(RpgWorldMapId.RedSaltHarbor, RpgWorld500xWeatherKind.GaleStorm);
                    atmosphere.SetWeather(RpgWorldMapId.RedSaltHarbor, (RpgWorld500xWeatherKind)999);
                    atmosphere.SetWeather((RpgWorldMapId)255, RpgWorld500xWeatherKind.AshHaze);
                    Assert.AreEqual(RpgWorld500xWeatherKind.GaleStorm, atmosphere.GetWeather(RpgWorldMapId.RedSaltHarbor));

                    badge.Refresh(10f);
                    Assert.AreEqual(1, badge.HazardousRegionCount);
                    Assert.AreNotEqual(RpgWorldMapId.RedSaltHarbor, badge.SafestRegionMapId);

                    // Switch to RedSaltHarbor -> badge shows Hazardous + campfire recommendation.
                    Assert.IsTrue(boot.SwitchMap(RpgWorldMapId.RedSaltHarbor));
                    badge.Refresh(25f);
                    Assert.AreEqual(RpgWorld500xTravelRiskLevel.Hazardous, badge.LastRiskLevel);
                    Assert.IsTrue(badge.LastRecommendCampfireRest);
                    StringAssert.Contains("高危预警", badge.RiskLabelText);
                    StringAssert.Contains("营火休整", badge.AdvisoryText);

                    // Switch to WhitewaterCrossing -> badge reflects Safe weather in that region.
                    Assert.IsTrue(boot.SwitchMap(RpgWorldMapId.WhitewaterCrossing));
                    badge.Refresh(25f);
                    Assert.AreEqual(RpgWorld500xTravelRiskLevel.Safe, badge.LastRiskLevel);
                    StringAssert.Contains("行路平稳", badge.RiskLabelText);

                    // Return to RedSaltHarbor and rest at campfire -> storm calms cleanly.
                    Assert.IsTrue(boot.SwitchMap(RpgWorldMapId.RedSaltHarbor));
                    atmosphere.RestAtCampfire(RpgWorldMapId.RedSaltHarbor);
                    badge.Refresh(30f);
                    Assert.AreNotEqual(RpgWorld500xTravelRiskLevel.Hazardous, badge.LastRiskLevel);
                    Assert.AreEqual(0, badge.HazardousRegionCount);
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
