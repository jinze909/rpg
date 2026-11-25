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
        public void PlanRouteFromCurrentPosition_RespectsEncounterLockAndRestoresBeaconAfterVictory()
        {
            using (var boot = new RpgWorld500xSceneBootstrapper(
                initialMapId: RpgWorldMapId.RedSaltHarbor,
                overrideTileSlices: new Sprite[] { null }))
            {
                var host = boot.Driver.Facade.Session.Host;
                Assert.IsTrue(host.Tracker.TryDiscoverMarker("rsh.salt-market"));
                var destination = new Vector2Int(1740, 1280);
                var before = RpgWorld500xCampHavenService.PlanRouteFromCurrentPosition(boot, destination);
                Assert.IsTrue(before.RecommendFastTravel);
                Assert.AreEqual("rsh.salt-market", before.BestBeaconId);

                RpgMapLandmarkMarker danger = default;
                bool found = false;
                foreach (RpgMapLandmarkMarker marker in host.Layout.Markers)
                {
                    if (marker.Category == RpgMapMarkerCategory.DangerZone)
                    {
                        danger = marker;
                        found = true;
                        break;
                    }
                }

                Assert.IsTrue(found);
                Assert.IsTrue(host.Bridge.TryTriggerDangerZoneEncounter(
                    danger.Id, new Vector2(danger.Tile.x + 0.5f, danger.Tile.y + 0.5f)));
                Vector2 position = host.PlayerPosition;
                var locked = RpgWorld500xCampHavenService.PlanRouteFromCurrentPosition(boot, destination);
                Assert.IsFalse(locked.RecommendFastTravel);
                Assert.IsEmpty(locked.BestBeaconId);
                Assert.AreEqual(0f, locked.TilesSavedByFastTravel);
                Assert.AreEqual(locked.DirectWalkTiles, locked.WalkAfterBeaconTiles);
                Assert.Greater(locked.EstimatedWalkSeconds, before.EstimatedWalkSeconds);
                Assert.That(locked.SummaryZh, Does.Contain("传送暂不可用"));
                Assert.AreEqual(position, host.PlayerPosition);
                Assert.IsTrue(host.Bridge.IsInDangerEncounter);

                // The locked route must use the actual walking segment, not the
                // previously selected beacon's shorter segment or danger estimate.
                var atmosphere = boot.Driver.Facade.Atmosphere.GetSnapshot(RpgWorldMapId.RedSaltHarbor);
                var direct = RpgWorld500xFastTravelRoutePlanner.PlanRoute(
                    RpgWorldMapId.RedSaltHarbor, position, destination, null,
                    atmosphere.MovementSpeedMultiplier);
                Assert.AreEqual(direct.EstimatedWalkSeconds, locked.EstimatedWalkSeconds, 0.001f);
                Assert.AreEqual(direct.EstimatedSprintSeconds, locked.EstimatedSprintSeconds, 0.001f);
                Assert.AreEqual(direct.CrossesDangerZone, locked.CrossesDangerZone);
                Assert.IsEmpty(locked.BestBeaconTitleZh);
                Assert.AreEqual(default(Vector2Int), locked.BestBeaconTile);
                var repeated = RpgWorld500xCampHavenService.PlanRouteFromCurrentPosition(boot, destination);
                Assert.AreEqual(locked.SummaryZh, repeated.SummaryZh);
                Assert.AreEqual(position, host.PlayerPosition);
                Assert.IsTrue(host.Bridge.IsInDangerEncounter);

                Assert.IsTrue(host.Bridge.TryResolveDangerZoneVictory(danger.Id, true, out _));
                var after = RpgWorld500xCampHavenService.PlanRouteFromCurrentPosition(boot, destination);
                Assert.IsTrue(after.RecommendFastTravel);
                Assert.AreEqual("rsh.salt-market", after.BestBeaconId);
                Assert.That(after.SummaryZh, Does.Not.Contain("传送暂不可用"));
            }
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
