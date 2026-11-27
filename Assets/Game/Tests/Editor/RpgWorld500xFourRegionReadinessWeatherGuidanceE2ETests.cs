using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionReadinessWeatherGuidanceE2ETests
    {
        [Test]
        public void AllFourRegions_FullyStockedReadinessPreservesHazardousRestGuidanceAndClearsInFairWeatherWithZeroLeak()
        {
            // Warm up one-time runtime caches before steady-state leak baseline.
            using (var warmupBoot = new RpgWorld500xSceneBootstrapper(
                initialMapId: RpgWorldMapId.Soundwell,
                overrideTileSlices: new Sprite[] { null }))
            {
                RpgWorld500xExpeditionReadinessService.EvaluateReadiness(warmupBoot);
            }

            int texBefore = Resources.FindObjectsOfTypeAll<Texture2D>().Length;
            int spriteBefore = Resources.FindObjectsOfTypeAll<Sprite>().Length;

            RpgWorldMapId[] regions =
            {
                RpgWorldMapId.Soundwell,
                RpgWorldMapId.RedSaltHarbor,
                RpgWorldMapId.WhitewaterCrossing,
                RpgWorldMapId.MineCamp
            };

            using (var boot = new RpgWorld500xSceneBootstrapper(
                initialMapId: regions[0],
                overrideTileSlices: new Sprite[] { null }))
            {
                var shop = boot.Driver.Facade.BountyAndSupply;
                shop.AddSaltSilver(300);
                foreach (string id in new[] { "gauze", "rations", "lamp_oil", "flare", "iron_clamps" })
                {
                    Assert.AreEqual(RpgCampSupplyPurchaseStatus.Succeeded, shop.TryPurchaseSupply(id, 1), id);
                }

                int balanceBefore = shop.SaltSilverBalance;

                for (int i = 0; i < regions.Length; i++)
                {
                    RpgWorldMapId mapId = regions[i];
                    if (i > 0)
                    {
                        Assert.IsTrue(boot.SwitchMap(mapId), "Expected map switch to " + mapId);
                    }

                    var facade = boot.Driver.Facade;

                    // Hazardous weather retains campfire rest guidance even when fully stocked.
                    facade.Atmosphere.SetWeather(mapId, RpgWorld500xWeatherKind.GaleStorm);
                    var storm = RpgWorld500xExpeditionReadinessService.EvaluateReadiness(boot);
                    Assert.AreEqual(RpgWorld500xTravelRiskLevel.Hazardous, storm.TravelRiskLevel, "Expected Hazardous in " + mapId);
                    Assert.AreEqual(0, storm.RecommendedSupplyIds.Count, "Expected zero missing supplies in " + mapId);
                    StringAssert.Contains("整备齐全", storm.ChecklistSummaryZh);
                    StringAssert.Contains("建议营火休整", storm.ChecklistSummaryZh);
                    StringAssert.DoesNotContain("可直接启程", storm.ChecklistSummaryZh);

                    // Clear weather transitions to direct departure guidance without mutating wallet balance.
                    facade.Atmosphere.SetWeather(mapId, RpgWorld500xWeatherKind.Clear);
                    var clear = RpgWorld500xExpeditionReadinessService.EvaluateReadiness(boot);
                    Assert.AreEqual(RpgWorld500xTravelRiskLevel.Safe, clear.TravelRiskLevel, "Expected Safe in " + mapId);
                    Assert.AreEqual(0, clear.RecommendedSupplyIds.Count, "Expected zero missing supplies in " + mapId);
                    StringAssert.Contains("可直接启程", clear.ChecklistSummaryZh);
                    StringAssert.DoesNotContain("建议营火休整", clear.ChecklistSummaryZh);
                    Assert.AreEqual(balanceBefore, facade.BountyAndSupply.SaltSilverBalance);
                }
            }

            Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Texture2D>().Length, texBefore);
            Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Sprite>().Length, spriteBefore);
        }
    }
}
