using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xCampProvisioningTests
    {
        [Test]
        public void ExecuteAutoProvisioning_StocksMultiHopHazardousTripAndRaisesReadinessScore()
        {
            using (var boot = new RpgWorld500xSceneBootstrapper(
                initialMapId: RpgWorldMapId.Soundwell,
                overrideTileSlices: new Sprite[] { null }))
            {
                RpgWorld500xGrandRuntimeFacade facade = boot.Driver.Facade;
                facade.Atmosphere.SetWeather(RpgWorldMapId.Soundwell, RpgWorld500xWeatherKind.Clear);
                facade.Atmosphere.SetWeather(RpgWorldMapId.RedSaltHarbor, RpgWorld500xWeatherKind.Clear);
                facade.Atmosphere.SetWeather(RpgWorldMapId.MineCamp, RpgWorld500xWeatherKind.GaleStorm);

                // Soundwell -> MineCamp is a 2-hop hazardous route: requires 2 rations plus lamp_oil, flare, gauze, iron_clamps.
                RpgWorld500xProvisioningPlan emptyPlan = RpgWorld500xCampProvisioningService.BuildProvisioningPlan(
                    boot, RpgWorldMapId.MineCamp, elapsedSeconds: 5f);
                Assert.AreEqual(2, emptyPlan.Itinerary.HopCount);
                Assert.AreEqual(RpgWorld500xTravelRiskLevel.Hazardous, emptyPlan.Itinerary.HighestRiskLevel);
                Assert.IsFalse(emptyPlan.CanAffordFullPlan);
                Assert.Greater(emptyPlan.TotalRequiredSaltSilver, 0);

                // Strict full-plan mode must not spend anything when wallet is empty.
                RpgWorld500xProvisioningReceipt denied = RpgWorld500xCampProvisioningService.ExecuteAutoProvisioning(
                    boot, RpgWorldMapId.MineCamp, allowPartialIfUnderBudget: false, elapsedSeconds: 5f);
                Assert.IsFalse(denied.AlreadyPrepared);
                Assert.IsFalse(denied.FullyProvisioned);
                Assert.AreEqual(0, denied.TotalSpentSaltSilver);
                Assert.AreEqual(0, denied.PurchasedLines.Count);
                Assert.Greater(denied.SkippedItemIds.Count, 0);

                // Fund full plan + extra buffer -> executes full provisioning and jumps readiness to WellPrepared.
                facade.BountyAndSupply.AddSaltSilver(300);
                RpgWorld500xProvisioningReceipt full = RpgWorld500xCampProvisioningService.ExecuteAutoProvisioning(
                    boot, RpgWorldMapId.MineCamp, allowPartialIfUnderBudget: false, elapsedSeconds: 5f);
                Assert.IsFalse(full.AlreadyPrepared);
                Assert.IsTrue(full.FullyProvisioned);
                Assert.AreEqual(emptyPlan.TotalRequiredSaltSilver, full.TotalSpentSaltSilver);
                Assert.AreEqual(300 - full.TotalSpentSaltSilver, full.RemainingSaltSilver);
                Assert.Greater(full.AfterScore, full.BeforeScore);
                Assert.AreEqual(RpgWorld500xReadinessGrade.WellPrepared, full.AfterGrade);
                Assert.AreEqual(2, facade.BountyAndSupply.GetOwnedSupplyCount("rations"));
                Assert.AreEqual(1, facade.BountyAndSupply.GetOwnedSupplyCount("lamp_oil"));
                Assert.AreEqual(1, facade.BountyAndSupply.GetOwnedSupplyCount("flare"));
                Assert.IsTrue(full.SummaryZh.Contains("全额备齐"));

                // Running again immediately is idempotent and spends zero Salt-Silver.
                RpgWorld500xProvisioningReceipt repeat = RpgWorld500xCampProvisioningService.ExecuteAutoProvisioning(
                    boot, RpgWorldMapId.MineCamp, allowPartialIfUnderBudget: true, elapsedSeconds: 5f);
                Assert.IsTrue(repeat.AlreadyPrepared);
                Assert.IsTrue(repeat.FullyProvisioned);
                Assert.AreEqual(0, repeat.TotalSpentSaltSilver);
            }
        }

        [Test]
        public void ExecuteAutoProvisioning_PartialBudgetBuysPrioritizedItemsWithoutOverspending()
        {
            using (var boot = new RpgWorld500xSceneBootstrapper(
                initialMapId: RpgWorldMapId.Soundwell,
                overrideTileSlices: new Sprite[] { null }))
            {
                boot.Driver.Facade.Atmosphere.SetWeather(RpgWorldMapId.Soundwell, RpgWorld500xWeatherKind.GaleStorm);
                // 35 SaltSilver covers gauze (18) + rations (15) = 33, leaving 2 SaltSilver and skipping remaining items.
                boot.Driver.Facade.BountyAndSupply.AddSaltSilver(35);

                RpgWorld500xProvisioningReceipt partial = RpgWorld500xCampProvisioningService.ExecuteAutoProvisioning(
                    boot, RpgWorldMapId.Soundwell, allowPartialIfUnderBudget: true, elapsedSeconds: 5f);
                Assert.IsFalse(partial.AlreadyPrepared);
                Assert.IsFalse(partial.FullyProvisioned);
                Assert.AreEqual(33, partial.TotalSpentSaltSilver);
                Assert.AreEqual(2, partial.RemainingSaltSilver);
                Assert.AreEqual(2, partial.PurchasedLines.Count);
                Assert.Greater(partial.SkippedItemIds.Count, 0);
                Assert.Greater(partial.AfterScore, partial.BeforeScore);
                Assert.IsTrue(partial.SummaryZh.Contains("部分备齐"));
            }
        }

        [Test]
        public void ExecuteAutoProvisioning_RespectsMaxBudgetCapAndFormatsItemizedBreakdown()
        {
            using (var boot = new RpgWorld500xSceneBootstrapper(
                initialMapId: RpgWorldMapId.Soundwell,
                overrideTileSlices: new Sprite[] { null }))
            {
                boot.Driver.Facade.Atmosphere.SetWeather(RpgWorldMapId.Soundwell, RpgWorld500xWeatherKind.GaleStorm);
                boot.Driver.Facade.BountyAndSupply.AddSaltSilver(300);

                RpgWorld500xProvisioningPlan plan = RpgWorld500xCampProvisioningService.BuildProvisioningPlan(
                    boot, RpgWorldMapId.Soundwell, elapsedSeconds: 5f);
                string breakdown = RpgWorld500xCampProvisioningService.FormatPlanBreakdownZh(plan);
                Assert.IsTrue(breakdown.Contains("×"));
                Assert.IsTrue(breakdown.Contains("合计 " + plan.TotalRequiredSaltSilver + " 盐银"));

                // Strict mode rejects spending when cap is below full plan cost even though wallet can afford it.
                RpgWorld500xProvisioningReceipt cappedDenied = RpgWorld500xCampProvisioningService.ExecuteAutoProvisioning(
                    boot, RpgWorldMapId.Soundwell, allowPartialIfUnderBudget: false, elapsedSeconds: 5f, maxBudgetSaltSilver: 35);
                Assert.IsFalse(cappedDenied.FullyProvisioned);
                Assert.AreEqual(0, cappedDenied.TotalSpentSaltSilver);
                Assert.AreEqual(300, cappedDenied.RemainingSaltSilver);
                Assert.IsTrue(cappedDenied.SummaryZh.Contains("超出预算上限"));

                // Partial mode spends at most the 35 Salt-Silver cap (gauze 18 + rations 15 = 33), preserving 267 Salt-Silver.
                RpgWorld500xProvisioningReceipt cappedPartial = RpgWorld500xCampProvisioningService.ExecuteAutoProvisioning(
                    boot, RpgWorldMapId.Soundwell, allowPartialIfUnderBudget: true, elapsedSeconds: 5f, maxBudgetSaltSilver: 35);
                Assert.IsFalse(cappedPartial.FullyProvisioned);
                Assert.AreEqual(33, cappedPartial.TotalSpentSaltSilver);
                Assert.AreEqual(267, cappedPartial.RemainingSaltSilver);
                Assert.AreEqual(2, cappedPartial.PurchasedLines.Count);
                Assert.Greater(cappedPartial.SkippedItemIds.Count, 0);

                // Completing the remaining restock makes the formatted breakdown report fully stocked.
                RpgWorld500xProvisioningReceipt finish = RpgWorld500xCampProvisioningService.ExecuteAutoProvisioning(
                    boot, RpgWorldMapId.Soundwell, allowPartialIfUnderBudget: false, elapsedSeconds: 5f);
                Assert.IsTrue(finish.FullyProvisioned);
                RpgWorld500xProvisioningPlan stockedPlan = RpgWorld500xCampProvisioningService.BuildProvisioningPlan(
                    boot, RpgWorldMapId.Soundwell, elapsedSeconds: 5f);
                Assert.AreEqual("行囊已齐备（无需补购）", RpgWorld500xCampProvisioningService.FormatPlanBreakdownZh(stockedPlan));
            }
        }
    }
}
