using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xWeatherAdvisoryHudBadgeTests
    {
        [Test]
        public void WeatherAdvisoryHudBadge_ReflectsWeatherRiskAmbientForecastAndDisposesWithZeroLeak()
        {
            var root = new GameObject("World500xWeatherAdvisoryHudBadgeTestRoot");
            try
            {
                // Warm up one-time UGUI/font caches before steady-state leak baseline.
                using (var warmupBoot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.Soundwell,
                    overrideTileSlices: new Sprite[] { null }))
                using (var warmupBadge = new RpgWorld500xWeatherAdvisoryHudBadge(root.transform, warmupBoot))
                {
                    warmupBadge.Refresh(5f);
                }

                int texBefore = Resources.FindObjectsOfTypeAll<Texture2D>().Length;
                int spriteBefore = Resources.FindObjectsOfTypeAll<Sprite>().Length;

                using (var boot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.RedSaltHarbor,
                    overrideTileSlices: new Sprite[] { null }))
                using (var badge = new RpgWorld500xWeatherAdvisoryHudBadge(root.transform, boot))
                {
                    boot.Driver.Facade.Atmosphere.SetWeather(RpgWorldMapId.Soundwell, RpgWorld500xWeatherKind.Clear);
                    boot.Driver.Facade.Atmosphere.SetWeather(RpgWorldMapId.RedSaltHarbor, RpgWorld500xWeatherKind.Clear);
                    boot.Driver.Facade.Atmosphere.SetWeather(RpgWorldMapId.WhitewaterCrossing, RpgWorld500xWeatherKind.Clear);
                    boot.Driver.Facade.Atmosphere.SetWeather(RpgWorldMapId.MineCamp, RpgWorld500xWeatherKind.Clear);
                    badge.Refresh(5f);
                    Assert.AreEqual(RpgWorld500xTravelRiskLevel.Safe, badge.LastRiskLevel);
                    Assert.IsFalse(badge.LastRecommendCampfireRest);
                    Assert.AreEqual(0, badge.HazardousRegionCount);
                    Assert.AreEqual(4, badge.LastWorldDigest.RegionReports.Count);
                    StringAssert.Contains("【四境气象速报】", badge.WorldBulletinText);
                    Assert.IsTrue(badge.RiskLabelText.Contains("行路平稳"));
                    Assert.IsTrue(badge.AmbientForecastText.Contains("异象激活中"));

                    boot.Driver.Facade.Atmosphere.SetWeather(RpgWorldMapId.RedSaltHarbor, RpgWorld500xWeatherKind.GaleStorm);
                    badge.Refresh(25f);
                    Assert.AreEqual(RpgWorld500xTravelRiskLevel.Hazardous, badge.LastRiskLevel);
                    Assert.IsTrue(badge.LastRecommendCampfireRest);
                    Assert.AreEqual(1, badge.HazardousRegionCount);
                    Assert.AreNotEqual(RpgWorldMapId.RedSaltHarbor, badge.SafestRegionMapId);
                    StringAssert.Contains("高危预警", badge.WorldBulletinText);
                    Assert.IsTrue(badge.RiskLabelText.Contains("高危预警"));
                    Assert.IsTrue(badge.AdvisoryText.Contains("营火休整"));
                    Assert.IsTrue(badge.AmbientForecastText.Contains("下次异象"));
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