using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xCampfireJournalDigestTests
    {
        [Test]
        public void BuildDigest_SynthesizesLiveWorldStatePrioritizesHazardAndIncludesActiveSupplyBuffs()
        {
            using (var boot = new RpgWorld500xSceneBootstrapper(
                initialMapId: RpgWorldMapId.Soundwell,
                overrideTileSlices: new Sprite[] { null }))
            {
                boot.Driver.Facade.BountyAndSupply.AddSaltSilver(120);
                Assert.AreEqual(RpgCampSupplyPurchaseStatus.Succeeded,
                    boot.Driver.Facade.BountyAndSupply.TryPurchaseSupply("lamp_oil", 1));

                var supplyStore = new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    ["rpg.return.world.supply.item.lamp_oil"] = 1
                };
                var useSvc = new RpgWorld500xFieldSupplyUseService(
                    k => supplyStore.TryGetValue(k, out int v) ? v : 0,
                    (k, v) => supplyStore[k] = v);
                Assert.IsTrue(useSvc.TryConsumeSupply("lamp_oil", 5f).Succeeded);

                // Clear day -> suggests nearest undiscovered POI via Recon Scanner and reports readiness & pending camp provisioning.
                boot.Driver.Facade.Atmosphere.SetWeather(RpgWorldMapId.Soundwell, RpgWorld500xWeatherKind.Clear);
                RpgWorld500xExpeditionDigest clearDigest =
                    RpgWorld500xCampfireJournalDigestService.BuildDigest(boot, elapsedSeconds: 10f, supplyUseService: useSvc);

                Assert.AreEqual(RpgWorldMapId.Soundwell, clearDigest.MapId);
                Assert.AreEqual(98, clearDigest.SaltSilverBalance); // 120 - 22 (lamp_oil)
                Assert.AreEqual(1, clearDigest.TotalOwnedSupplyItems);
                Assert.AreEqual(RpgWorld500xTravelRiskLevel.Safe, clearDigest.TravelRiskLevel);
                Assert.Greater(clearDigest.ReadinessScore, 0);
                Assert.IsFalse(string.IsNullOrWhiteSpace(clearDigest.ReadinessGradeLabelZh));
                Assert.IsTrue(clearDigest.WorldWeatherBulletinZh.Contains("【四境气象速报】"));
                Assert.IsTrue(clearDigest.WorldReputationSummaryZh.Contains("【四境声望总览】"));
                Assert.IsTrue(clearDigest.MilestoneSummaryZh.Contains("【远征里程碑】"));
                Assert.IsTrue(clearDigest.FormattedDigestZh.Contains("远征营火纪要"));
                Assert.IsTrue(clearDigest.FormattedDigestZh.Contains("整备状态："));
                Assert.IsTrue(clearDigest.FormattedDigestZh.Contains("本境待补"));
                Assert.IsTrue(clearDigest.FormattedDigestZh.Contains("【四境气象速报】"));
                Assert.IsTrue(clearDigest.FormattedDigestZh.Contains("【四境声望总览】"));
                Assert.IsTrue(clearDigest.FormattedDigestZh.Contains("【远征里程碑】"));
                Assert.IsTrue(clearDigest.FormattedDigestZh.Contains("灯油照明"));
                Assert.IsTrue(clearDigest.NextObjectiveHintZh.Contains("未探索目标"));

                // GaleStorm -> prioritizes campfire rest advisory over field POI hint and flags hazardous region count.
                boot.Driver.Facade.Atmosphere.SetWeather(RpgWorldMapId.Soundwell, RpgWorld500xWeatherKind.GaleStorm);
                RpgWorld500xExpeditionDigest stormDigest =
                    RpgWorld500xCampfireJournalDigestService.BuildDigest(boot, elapsedSeconds: 10f);

                Assert.AreEqual(RpgWorld500xTravelRiskLevel.Hazardous, stormDigest.TravelRiskLevel);
                Assert.GreaterOrEqual(stormDigest.HazardousRegionCount, 1);
                Assert.IsTrue(stormDigest.WorldWeatherBulletinZh.Contains("高危预警"));
                Assert.IsTrue(stormDigest.NextObjectiveHintZh.Contains("营火休整"));

                // Full auto-provisioning updates digest readiness summary to stocked.
                boot.Driver.Facade.BountyAndSupply.AddSaltSilver(200);
                RpgWorld500xProvisioningReceipt receipt = RpgWorld500xCampProvisioningService.ExecuteAutoProvisioning(
                    boot, RpgWorldMapId.Soundwell, allowPartialIfUnderBudget: false, elapsedSeconds: 10f);
                Assert.IsTrue(receipt.FullyProvisioned);

                RpgWorld500xExpeditionDigest stockedDigest =
                    RpgWorld500xCampfireJournalDigestService.BuildDigest(boot, elapsedSeconds: 10f);
                Assert.Greater(stockedDigest.ReadinessScore, stormDigest.ReadinessScore);
                Assert.IsTrue(stockedDigest.FormattedDigestZh.Contains("行囊已齐备"));
            }
        }
    }
}
