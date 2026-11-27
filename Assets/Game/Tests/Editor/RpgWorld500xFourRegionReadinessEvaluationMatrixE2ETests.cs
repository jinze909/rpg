using System;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionReadinessEvaluationMatrixE2ETests
    {
        [Test]
        public void ExpeditionReadinessService_AcrossAllFourOrigins_PreEvaluatesRemoteStormyRegionsWithoutMovingPlayer()
        {
            var specs = RpgExpandedWorldScale.AllSpecs;
            Assert.That(specs.Count, Is.EqualTo(4));

            for (int i = 0; i < specs.Count; i++)
            {
                var origin = specs[i].MapId;
                var remote = specs[(i + 2) % specs.Count].MapId;

                using (var boot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: origin,
                    overrideTileSlices: new Sprite[] { null }))
                {
                    var facade = boot.Driver.Facade;
                    facade.Atmosphere.SetWeather(origin, RpgWorld500xWeatherKind.Clear);
                    facade.Atmosphere.SetWeather(remote, RpgWorld500xWeatherKind.GaleStorm);

                    var localReport = RpgWorld500xExpeditionReadinessService.EvaluateReadiness(boot, elapsedSeconds: 5f);
                    Assert.That(localReport.MapId, Is.EqualTo(origin));
                    Assert.That(localReport.TravelRiskLevel, Is.EqualTo(RpgWorld500xTravelRiskLevel.Safe));

                    var remoteReport = RpgWorld500xExpeditionReadinessService.EvaluateReadinessForMap(boot, remote, elapsedSeconds: 5f);
                    Assert.That(remoteReport.MapId, Is.EqualTo(remote));
                    Assert.That(remoteReport.TravelRiskLevel, Is.EqualTo(RpgWorld500xTravelRiskLevel.Hazardous));
                    Assert.That(remoteReport.Score, Is.LessThan(localReport.Score));
                    CollectionAssert.Contains(remoteReport.RecommendedSupplyIds, "lamp_oil");
                    CollectionAssert.Contains(remoteReport.RecommendedSupplyIds, "flare");
                    Assert.That(facade.CurrentMapId, Is.EqualTo(origin));
                }
            }
        }

        [Test]
        public void ExpeditionReadinessService_AcrossAllFourRegions_PreservesStormRestGuidanceWhenStockedAndRejectsNullOrDisposed()
        {
            Assert.Throws<ArgumentNullException>(() =>
                RpgWorld500xExpeditionReadinessService.EvaluateReadiness(null));
            Assert.Throws<ArgumentNullException>(() =>
                RpgWorld500xExpeditionReadinessService.EvaluateReadinessForMap(null, RpgWorldMapId.Soundwell));

            var specs = RpgExpandedWorldScale.AllSpecs;
            Assert.That(specs.Count, Is.EqualTo(4));

            for (int i = 0; i < specs.Count; i++)
            {
                var mapId = specs[i].MapId;
                var boot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: mapId,
                    overrideTileSlices: new Sprite[] { null });
                try
                {
                    var facade = boot.Driver.Facade;
                    var shop = facade.BountyAndSupply;
                    shop.AddSaltSilver(300);

                    foreach (string id in new[] { "gauze", "rations", "lamp_oil", "flare", "iron_clamps" })
                    {
                        Assert.That(shop.TryPurchaseSupply(id, 1), Is.EqualTo(RpgCampSupplyPurchaseStatus.Succeeded), id);
                    }

                    facade.Atmosphere.SetWeather(mapId, RpgWorld500xWeatherKind.GaleStorm);
                    var storm = RpgWorld500xExpeditionReadinessService.EvaluateReadiness(boot, elapsedSeconds: 5f);
                    Assert.That(storm.TravelRiskLevel, Is.EqualTo(RpgWorld500xTravelRiskLevel.Hazardous));
                    Assert.IsEmpty(storm.RecommendedSupplyIds);
                    StringAssert.Contains("整备齐全", storm.ChecklistSummaryZh);
                    StringAssert.Contains("建议营火休整", storm.ChecklistSummaryZh);
                    StringAssert.DoesNotContain("可直接启程", storm.ChecklistSummaryZh);

                    facade.Atmosphere.SetWeather(mapId, RpgWorld500xWeatherKind.Clear);
                    var clear = RpgWorld500xExpeditionReadinessService.EvaluateReadiness(boot, elapsedSeconds: 5f);
                    Assert.That(clear.TravelRiskLevel, Is.EqualTo(RpgWorld500xTravelRiskLevel.Safe));
                    Assert.IsEmpty(clear.RecommendedSupplyIds);
                    StringAssert.Contains("可直接启程", clear.ChecklistSummaryZh);
                    StringAssert.DoesNotContain("建议营火休整", clear.ChecklistSummaryZh);
                }
                finally
                {
                    boot.Dispose();
                }

                Assert.Throws<ArgumentNullException>(() =>
                    RpgWorld500xExpeditionReadinessService.EvaluateReadiness(boot));
            }
        }
    }
}
