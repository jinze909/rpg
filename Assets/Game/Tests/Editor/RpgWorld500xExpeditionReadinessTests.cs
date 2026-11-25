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
    }
}
