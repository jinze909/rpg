using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xExpeditionReadinessTests
    {
        [Test]
        public void EvaluateReadiness_ScoresWeatherRiskSupplyPrepAndActiveBuffsDeterministically()
        {
            using (var boot = new RpgWorld500xSceneBootstrapper(
                initialMapId: RpgWorldMapId.Soundwell,
                overrideTileSlices: new Sprite[] { null }))
            {
                // GaleStorm with zero supplies -> Critical readiness and recommends lamp_oil, flare, gauze, rations, iron_clamps.
                boot.Driver.Facade.Atmosphere.SetWeather(RpgWorldMapId.Soundwell, RpgWorld500xWeatherKind.GaleStorm);
                RpgWorld500xExpeditionReadinessReport stormEmpty =
                    RpgWorld500xExpeditionReadinessService.EvaluateReadiness(boot, elapsedSeconds: 5f);

                Assert.AreEqual(RpgWorld500xReadinessGrade.Critical, stormEmpty.Grade);
                Assert.AreEqual(5, stormEmpty.Score);
                Assert.AreEqual(RpgWorld500xTravelRiskLevel.Hazardous, stormEmpty.TravelRiskLevel);
                Assert.IsFalse(stormEmpty.HasHealingStock);
                Assert.IsFalse(stormEmpty.HasVisibilityPrep);
                Assert.IsFalse(stormEmpty.HasShortcutTools);
                Assert.Greater(stormEmpty.LockedShortcutCount, 0);
                CollectionAssert.Contains(stormEmpty.RecommendedSupplyIds, "gauze");
                CollectionAssert.Contains(stormEmpty.RecommendedSupplyIds, "lamp_oil");
                CollectionAssert.Contains(stormEmpty.RecommendedSupplyIds, "flare");
                CollectionAssert.Contains(stormEmpty.RecommendedSupplyIds, "iron_clamps");
                Assert.IsTrue(stormEmpty.ChecklistSummaryZh.Contains("高危缺备"));

                // Stock all core supplies + switch to Clear weather -> WellPrepared with zero missing recommendations.
                RpgWorld500xFieldBountyAndSupplyService shop = boot.Driver.Facade.BountyAndSupply;
                shop.AddSaltSilver(300);
                Assert.AreEqual(RpgCampSupplyPurchaseStatus.Succeeded, shop.TryPurchaseSupply("gauze", 1));
                Assert.AreEqual(RpgCampSupplyPurchaseStatus.Succeeded, shop.TryPurchaseSupply("rations", 1));
                Assert.AreEqual(RpgCampSupplyPurchaseStatus.Succeeded, shop.TryPurchaseSupply("lamp_oil", 1));
                Assert.AreEqual(RpgCampSupplyPurchaseStatus.Succeeded, shop.TryPurchaseSupply("flare", 1));
                Assert.AreEqual(RpgCampSupplyPurchaseStatus.Succeeded, shop.TryPurchaseSupply("iron_clamps", 1));

                boot.Driver.Facade.Atmosphere.SetWeather(RpgWorldMapId.Soundwell, RpgWorld500xWeatherKind.Clear);
                RpgWorld500xExpeditionReadinessReport wellPrepped =
                    RpgWorld500xExpeditionReadinessService.EvaluateReadiness(boot, elapsedSeconds: 5f);

                Assert.AreEqual(RpgWorld500xReadinessGrade.WellPrepared, wellPrepped.Grade);
                Assert.GreaterOrEqual(wellPrepped.Score, 95);
                Assert.IsTrue(wellPrepped.HasHealingStock);
                Assert.IsTrue(wellPrepped.HasVisibilityPrep);
                Assert.IsTrue(wellPrepped.HasStealthOrFlarePrep);
                Assert.IsTrue(wellPrepped.HasShortcutTools);
                Assert.AreEqual(0, wellPrepped.RecommendedSupplyIds.Count);
                Assert.IsTrue(wellPrepped.ChecklistSummaryZh.Contains("整备齐全"));

                // Active lamp_oil buff satisfies visibility prep even when owned stock drops to 0.
                var buffStore = new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    ["rpg.return.world.supply.item.lamp_oil"] = 1
                };
                var useSvc = new RpgWorld500xFieldSupplyUseService(
                    k => buffStore.TryGetValue(k, out int v) ? v : 0,
                    (k, v) => buffStore[k] = v);
                Assert.IsTrue(useSvc.TryConsumeSupply("lamp_oil", 2f).Succeeded);
                Assert.AreEqual(0, useSvc.GetOwnedCount("lamp_oil"));

                using (var freshBoot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.RedSaltHarbor,
                    overrideTileSlices: new Sprite[] { null }))
                {
                    freshBoot.Driver.Facade.Atmosphere.SetWeather(RpgWorldMapId.RedSaltHarbor, RpgWorld500xWeatherKind.MistFog);
                    RpgWorld500xExpeditionReadinessReport withActiveBuff =
                        RpgWorld500xExpeditionReadinessService.EvaluateReadiness(freshBoot, elapsedSeconds: 10f, supplyUseService: useSvc);
                    Assert.IsTrue(withActiveBuff.HasVisibilityPrep);
                    CollectionAssert.DoesNotContain(withActiveBuff.RecommendedSupplyIds, "lamp_oil");
                }
            }
        }

        [Test]
        public void FullyStockedExpedition_StillRecommendsRestInHazardousWeather()
        {
            using (var boot = new RpgWorld500xSceneBootstrapper(
                initialMapId: RpgWorldMapId.Soundwell,
                overrideTileSlices: new Sprite[] { null }))
            {
                var facade = boot.Driver.Facade;
                var shop = facade.BountyAndSupply;
                shop.AddSaltSilver(300);
                foreach (string id in new[] { "gauze", "rations", "lamp_oil", "flare", "iron_clamps" })
                {
                    Assert.AreEqual(RpgCampSupplyPurchaseStatus.Succeeded, shop.TryPurchaseSupply(id, 1), id);
                }

                int balance = shop.SaltSilverBalance;
                facade.Atmosphere.SetWeather(RpgWorldMapId.Soundwell, RpgWorld500xWeatherKind.GaleStorm);
                var storm = RpgWorld500xExpeditionReadinessService.EvaluateReadiness(boot);
                Assert.AreEqual(RpgWorld500xTravelRiskLevel.Hazardous, storm.TravelRiskLevel);
                Assert.AreEqual(0, storm.RecommendedSupplyIds.Count);
                StringAssert.Contains("整备齐全", storm.ChecklistSummaryZh);
                StringAssert.Contains("建议营火休整", storm.ChecklistSummaryZh);
                StringAssert.DoesNotContain("可直接启程", storm.ChecklistSummaryZh);

                facade.Atmosphere.SetWeather(RpgWorldMapId.Soundwell, RpgWorld500xWeatherKind.Clear);
                var clear = RpgWorld500xExpeditionReadinessService.EvaluateReadiness(boot);
                Assert.AreEqual(RpgWorld500xTravelRiskLevel.Safe, clear.TravelRiskLevel);
                Assert.AreEqual(0, clear.RecommendedSupplyIds.Count);
                StringAssert.Contains("可直接启程", clear.ChecklistSummaryZh);
                StringAssert.DoesNotContain("建议营火休整", clear.ChecklistSummaryZh);
                Assert.AreEqual(balance, shop.SaltSilverBalance);
            }
        }

        [Test]
        public void EvaluateReadinessForMap_PreEvaluatesRemoteHazardousDestinationBeforeTravel()
        {
            using (var boot = new RpgWorld500xSceneBootstrapper(
                initialMapId: RpgWorldMapId.Soundwell,
                overrideTileSlices: new Sprite[] { null }))
            {
                boot.Driver.Facade.Atmosphere.SetWeather(RpgWorldMapId.Soundwell, RpgWorld500xWeatherKind.Clear);
                boot.Driver.Facade.Atmosphere.SetWeather(RpgWorldMapId.MineCamp, RpgWorld500xWeatherKind.GaleStorm);

                RpgWorld500xExpeditionReadinessReport localClear =
                    RpgWorld500xExpeditionReadinessService.EvaluateReadiness(boot, elapsedSeconds: 5f);
                Assert.AreEqual(RpgWorldMapId.Soundwell, localClear.MapId);
                Assert.AreEqual(RpgWorld500xTravelRiskLevel.Safe, localClear.TravelRiskLevel);

                RpgWorld500xExpeditionReadinessReport remoteStorm =
                    RpgWorld500xExpeditionReadinessService.EvaluateReadinessForMap(boot, RpgWorldMapId.MineCamp, elapsedSeconds: 5f);
                Assert.AreEqual(RpgWorldMapId.MineCamp, remoteStorm.MapId);
                Assert.AreEqual(RpgWorld500xTravelRiskLevel.Hazardous, remoteStorm.TravelRiskLevel);
                Assert.AreEqual(RpgWorld500xReadinessGrade.Critical, remoteStorm.Grade);
                Assert.AreEqual(5, remoteStorm.Score);
                CollectionAssert.Contains(remoteStorm.RecommendedSupplyIds, "lamp_oil");
                CollectionAssert.Contains(remoteStorm.RecommendedSupplyIds, "flare");
                Assert.AreEqual(RpgWorldMapId.Soundwell, boot.Driver.Facade.CurrentMapId);
            }
        }
    }
}
