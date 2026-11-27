using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionCampProvisioningMatrixE2ETests
    {
        [Test]
        public void CampProvisioningService_AcrossAllFourOrigins_FullProvisioningStocksSuppliesAndSecondCallReportsAlreadyPrepared()
        {
            var specs = RpgExpandedWorldScale.AllSpecs;
            Assert.That(specs.Count, Is.EqualTo(4));

            for (int i = 0; i < specs.Count; i++)
            {
                var origin = specs[i].MapId;
                var destination = specs[(i + 2) % specs.Count].MapId;

                using (var boot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: origin,
                    overrideTileSlices: new Sprite[] { null }))
                {
                    var facade = boot.Driver.Facade;
                    for (int j = 0; j < specs.Count; j++)
                    {
                        var mapId = specs[j].MapId;
                        facade.Atmosphere.SetWeather(
                            mapId,
                            mapId == destination ? RpgWorld500xWeatherKind.GaleStorm : RpgWorld500xWeatherKind.Clear);
                    }

                    facade.BountyAndSupply.AddSaltSilver(300);

                    var plan = RpgWorld500xCampProvisioningService.BuildProvisioningPlan(
                        boot, destination, elapsedSeconds: 5f, preferSafestRoute: true);
                    Assert.IsTrue(plan.Itinerary.IsReachable);
                    Assert.That(plan.Lines.Count, Is.GreaterThan(0));
                    Assert.That(plan.TotalRequiredSaltSilver, Is.GreaterThan(0));
                    Assert.IsTrue(plan.CanAffordFullPlan);

                    var receipt = RpgWorld500xCampProvisioningService.ExecuteAutoProvisioning(
                        boot, destination, allowPartialIfUnderBudget: false, elapsedSeconds: 5f, preferSafestRoute: true);
                    Assert.IsFalse(receipt.AlreadyPrepared);
                    Assert.IsTrue(receipt.FullyProvisioned);
                    Assert.That(receipt.TotalSpentSaltSilver, Is.EqualTo(plan.TotalRequiredSaltSilver));
                    Assert.That(receipt.RemainingSaltSilver, Is.EqualTo(300 - receipt.TotalSpentSaltSilver));
                    Assert.That(receipt.AfterScore, Is.GreaterThanOrEqualTo(receipt.BeforeScore));

                    var stockedPlan = RpgWorld500xCampProvisioningService.BuildProvisioningPlan(
                        boot, destination, elapsedSeconds: 5f, preferSafestRoute: true);
                    Assert.IsEmpty(stockedPlan.Lines);
                    Assert.That(stockedPlan.TotalRequiredSaltSilver, Is.EqualTo(0));
                    Assert.That(
                        RpgWorld500xCampProvisioningService.FormatPlanBreakdownZh(stockedPlan),
                        Is.EqualTo("行囊已齐备（无需补购）"));

                    var secondReceipt = RpgWorld500xCampProvisioningService.ExecuteAutoProvisioning(
                        boot, destination, allowPartialIfUnderBudget: false, elapsedSeconds: 5f, preferSafestRoute: true);
                    Assert.IsTrue(secondReceipt.AlreadyPrepared);
                    Assert.That(secondReceipt.TotalSpentSaltSilver, Is.EqualTo(0));
                }
            }
        }

        [Test]
        public void CampProvisioningService_AcrossAllFourOrigins_EnforcesMaxBudgetCapAndRejectsInvalidDestination()
        {
            var specs = RpgExpandedWorldScale.AllSpecs;
            Assert.That(specs.Count, Is.EqualTo(4));
            var invalidMap = (RpgWorldMapId)255;

            for (int i = 0; i < specs.Count; i++)
            {
                var origin = specs[i].MapId;
                var destination = specs[(i + 1) % specs.Count].MapId;

                using (var boot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: origin,
                    overrideTileSlices: new Sprite[] { null }))
                {
                    var facade = boot.Driver.Facade;
                    facade.Atmosphere.SetWeather(origin, RpgWorld500xWeatherKind.Clear);
                    facade.Atmosphere.SetWeather(destination, RpgWorld500xWeatherKind.GaleStorm);
                    facade.BountyAndSupply.AddSaltSilver(300);

                    var cappedReceipt = RpgWorld500xCampProvisioningService.ExecuteAutoProvisioning(
                        boot,
                        destination,
                        allowPartialIfUnderBudget: true,
                        elapsedSeconds: 5f,
                        maxBudgetSaltSilver: 20);
                    Assert.That(cappedReceipt.TotalSpentSaltSilver, Is.LessThanOrEqualTo(20));
                    Assert.That(cappedReceipt.RemainingSaltSilver, Is.EqualTo(300 - cappedReceipt.TotalSpentSaltSilver));

                    int balanceBeforeInvalid = facade.BountyAndSupply.SaltSilverBalance;
                    var invalidPlan = RpgWorld500xCampProvisioningService.BuildProvisioningPlan(
                        boot, invalidMap, elapsedSeconds: 5f);
                    Assert.IsFalse(invalidPlan.Itinerary.IsReachable);
                    Assert.IsEmpty(invalidPlan.Lines);
                    Assert.That(invalidPlan.TotalRequiredSaltSilver, Is.EqualTo(0));
                    StringAssert.Contains("不可达", RpgWorld500xCampProvisioningService.FormatPlanBreakdownZh(invalidPlan));

                    var invalidReceipt = RpgWorld500xCampProvisioningService.ExecuteAutoProvisioning(
                        boot, invalidMap, allowPartialIfUnderBudget: true, elapsedSeconds: 5f);
                    Assert.IsFalse(invalidReceipt.AlreadyPrepared);
                    Assert.IsFalse(invalidReceipt.FullyProvisioned);
                    Assert.That(invalidReceipt.TotalSpentSaltSilver, Is.EqualTo(0));
                    Assert.That(facade.BountyAndSupply.SaltSilverBalance, Is.EqualTo(balanceBeforeInvalid));
                }
            }
        }
    }
}
