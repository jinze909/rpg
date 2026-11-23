using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xCampHavenServiceTests
    {
        [Test]
        public void TryPurchaseDiscountedSupply_AppliesReputationDiscountAndPreventsOverflow()
        {
            var store = new Dictionary<string, int>(StringComparer.Ordinal);
            var haven = new RpgWorld500xCampHavenService(
                k => store.TryGetValue(k, out int v) ? v : 0,
                (k, v) => store[k] = v);

            // Elevate RedSaltHarbor to Sworn (30% discount): flare base cost 30 -> 21
            haven.Reputation.AddPoints(RpgWorldMapId.RedSaltHarbor, 800);
            store[RpgWorld500xFieldBountyAndSupplyService.WalletSaltSilverKey] = 50;

            Assert.AreEqual(21, haven.GetDiscountedUnitCost(RpgWorldMapId.RedSaltHarbor, "flare"));
            Assert.AreEqual(RpgCampSupplyPurchaseStatus.Succeeded,
                haven.TryPurchaseDiscountedSupply(RpgWorldMapId.RedSaltHarbor, "flare", 2, out int charged));
            Assert.AreEqual(42, charged);
            Assert.AreEqual(8, store[RpgWorld500xFieldBountyAndSupplyService.WalletSaltSilverKey]);

            // Overflow quantity safely rejected as OutOfStock
            Assert.AreEqual(RpgCampSupplyPurchaseStatus.OutOfStock,
                haven.TryPurchaseDiscountedSupply(RpgWorldMapId.RedSaltHarbor, "flare", int.MaxValue, out _));
        }

        [Test]
        public void RestAndSyncCampHaven_AdvancesToDawnSavesSnapshotAndPlansRoute()
        {
            using (var boot = new RpgWorld500xSceneBootstrapper(
                initialMapId: RpgWorldMapId.RedSaltHarbor,
                overrideTileSlices: new Sprite[] { null }))
            {
                boot.Driver.Facade.Atmosphere.SetWeather(RpgWorldMapId.RedSaltHarbor, RpgWorld500xWeatherKind.GaleStorm);
                boot.Driver.Facade.Session.Host.Tracker.TryDiscoverMarker("rsh.salt-market");

                var haven = new RpgWorld500xCampHavenService(
                    k => boot.Store.TryGetValue(k, out int v) ? v : 0,
                    (k, v) =>
                    {
                        // Delegate into bootstrapper's wallet/reputation via facade store if needed.
                    });

                RpgWorld500xCampHavenSummary summary = haven.RestAndSyncCampHaven(boot);
                Assert.AreEqual(RpgWorldMapId.RedSaltHarbor, summary.MapId);
                Assert.AreEqual("06:00", summary.Atmosphere.FormattedClock);
                Assert.AreEqual(RpgWorld500xWeatherKind.Clear, summary.Atmosphere.Weather);
                Assert.IsTrue(RpgWorld500xSaveSnapshotSerializer.TryDeserialize(summary.SaveSnapshotJson, out _, out _));
                Assert.IsTrue(summary.SummaryZh.Contains("营地休整完成"));

                RpgWorld500xRoutePlan route = RpgWorld500xCampHavenService.PlanRouteFromCurrentPosition(
                    boot, new Vector2Int(1740, 1280));
                Assert.IsTrue(route.RecommendFastTravel);
                Assert.AreEqual("rsh.salt-market", route.BestBeaconId);
            }
        }
    }
}
