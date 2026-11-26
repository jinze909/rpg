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

        [Test]
        public void UnreachableDestination_DoesNotQuoteOrSpendSupplies()
        {
            using (var boot = new RpgWorld500xSceneBootstrapper(
                initialMapId: RpgWorldMapId.Soundwell,
                overrideTileSlices: new Sprite[] { null }))
            {
                var facade = boot.Driver.Facade;
                facade.Atmosphere.SetWeather(RpgWorldMapId.Soundwell, RpgWorld500xWeatherKind.GaleStorm);
                var shop = facade.BountyAndSupply;
                shop.AddSaltSilver(300);
                int stock = shop.GetRemainingStock("gauze");
                int owned = shop.GetOwnedSupplyCount("gauze");
                var invalidMap = (RpgWorldMapId)255;

                foreach (bool safest in new[] { false, true })
                {
                    var plan = RpgWorld500xCampProvisioningService.BuildProvisioningPlan(
                        boot, invalidMap, elapsedSeconds: 5f, preferSafestRoute: safest);
                    Assert.IsFalse(plan.Itinerary.IsReachable);
                    Assert.IsFalse(plan.CanAffordFullPlan);
                    Assert.IsEmpty(plan.Lines);
                    Assert.AreEqual(0, plan.TotalRequiredSaltSilver);
                    StringAssert.Contains("不可达", plan.SummaryZh);
                    StringAssert.Contains("不可达", RpgWorld500xCampProvisioningService.FormatPlanBreakdownZh(plan));

                    foreach (bool partial in new[] { false, true })
                    {
                        var receipt = RpgWorld500xCampProvisioningService.ExecuteAutoProvisioning(
                            boot, invalidMap, allowPartialIfUnderBudget: partial,
                            elapsedSeconds: 5f, preferSafestRoute: safest);
                        Assert.IsFalse(receipt.AlreadyPrepared);
                        Assert.IsFalse(receipt.FullyProvisioned);
                        Assert.AreEqual(0, receipt.TotalSpentSaltSilver);
                        Assert.AreEqual(300, receipt.RemainingSaltSilver);
                        Assert.AreEqual(receipt.BeforeScore, receipt.AfterScore);
                        Assert.IsEmpty(receipt.PurchasedLines);
                        StringAssert.Contains("不可达", receipt.SummaryZh);
                    }
                }

                Assert.AreEqual(300, shop.SaltSilverBalance);
                Assert.AreEqual(stock, shop.GetRemainingStock("gauze"));
                Assert.AreEqual(owned, shop.GetOwnedSupplyCount("gauze"));
                var valid = RpgWorld500xCampProvisioningService.BuildProvisioningPlan(
                    boot, RpgWorldMapId.Soundwell, elapsedSeconds: 5f);
                Assert.IsTrue(valid.Itinerary.IsReachable);
                Assert.Greater(valid.TotalRequiredSaltSilver, 0);
            }
        }

        [Test]
        public void BuildProvisioningPlan_WithPreferSafestRouteUsesSafestItineraryPlanner()
        {
            using (var boot = new RpgWorld500xSceneBootstrapper(
                initialMapId: RpgWorldMapId.WhitewaterCrossing,
                overrideTileSlices: new Sprite[] { null }))
            {
                RpgWorld500xGrandRuntimeFacade facade = boot.Driver.Facade;
                facade.Atmosphere.SetWeather(RpgWorldMapId.WhitewaterCrossing, RpgWorld500xWeatherKind.Clear);
                facade.Atmosphere.SetWeather(RpgWorldMapId.MineCamp, RpgWorld500xWeatherKind.Clear);
                facade.Atmosphere.SetWeather(RpgWorldMapId.RedSaltHarbor, RpgWorld500xWeatherKind.GaleStorm);

                RpgWorld500xProvisioningPlan safestPlan = RpgWorld500xCampProvisioningService.BuildProvisioningPlan(
                    boot, RpgWorldMapId.MineCamp, elapsedSeconds: 5f, preferSafestRoute: true);
                Assert.AreEqual(1, safestPlan.Itinerary.HopCount);
                Assert.AreEqual(RpgWorld500xTravelRiskLevel.Safe, safestPlan.Itinerary.HighestRiskLevel);
                Assert.IsTrue(safestPlan.Itinerary.SummaryZh.Contains("安全绕行行程"));

                facade.BountyAndSupply.AddSaltSilver(300);
                RpgWorld500xProvisioningReceipt receipt = RpgWorld500xCampProvisioningService.ExecuteAutoProvisioning(
                    boot, RpgWorldMapId.MineCamp, allowPartialIfUnderBudget: false, elapsedSeconds: 5f, preferSafestRoute: true);
                Assert.IsTrue(receipt.FullyProvisioned);
                Assert.AreEqual(safestPlan.TotalRequiredSaltSilver, receipt.TotalSpentSaltSilver);
            }
        }
    }
}
