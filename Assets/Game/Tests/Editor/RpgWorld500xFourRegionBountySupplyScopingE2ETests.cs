using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionBountySupplyScopingE2ETests
    {
        [Test]
        public void FieldBountyAndSupplyService_AcrossAllFourRegions_PreviewsClaimsIdempotentlyAndKeepsOtherMapsIsolated()
        {
            var specs = RpgExpandedWorldScale.AllSpecs;
            Assert.That(specs.Count, Is.EqualTo(4));

            for (int i = 0; i < specs.Count; i++)
            {
                var mapId = specs[i].MapId;
                var store = new Dictionary<string, int>(StringComparer.Ordinal);
                var service = new RpgWorld500xFieldBountyAndSupplyService(
                    k => store.TryGetValue(k, out int v) ? v : 0,
                    (k, v) => store[k] = v);

                var completed = new RpgWorld500xMapCompletionSummary(
                    mapId,
                    visitedChunks: 200, totalChunks: 2814,
                    discoveredMarkers: 12, totalMarkers: 18,
                    lootedCaches: 8, totalCaches: 12,
                    unlockedShortcuts: 4, totalShortcuts: 4,
                    inspectedStorySpots: 4, totalStorySpots: 4);

                var before = service.BuildBountyBoardSummary(completed);
                Assert.That(before.MapId, Is.EqualTo(mapId));
                Assert.That(before.TotalBounties, Is.EqualTo(3));
                Assert.That(before.ClaimedCount, Is.EqualTo(0));
                Assert.That(before.ClaimableCount, Is.EqualTo(3));
                Assert.That(before.ClaimableSaltSilver, Is.GreaterThan(0));
                Assert.That(before.RemainingSaltSilver, Is.EqualTo(before.ClaimableSaltSilver));
                Assert.That(service.SaltSilverBalance, Is.EqualTo(0));
                Assert.That(store.Count, Is.EqualTo(0));

                var claimed = service.ClaimCompletedBounties(completed);
                Assert.That(claimed.Count, Is.EqualTo(3));
                Assert.That(service.SaltSilverBalance, Is.EqualTo(before.ClaimableSaltSilver));

                var after = service.BuildBountyBoardSummary(completed);
                Assert.That(after.ClaimedCount, Is.EqualTo(3));
                Assert.That(after.ClaimableCount, Is.EqualTo(0));
                Assert.That(after.RemainingSaltSilver, Is.EqualTo(0));
                StringAssert.Contains("本境悬赏已全部结清", after.SummaryZh);

                var secondClaim = service.ClaimCompletedBounties(completed);
                Assert.IsEmpty(secondClaim);
                Assert.That(service.SaltSilverBalance, Is.EqualTo(before.ClaimableSaltSilver));

                for (int j = 0; j < specs.Count; j++)
                {
                    if (specs[j].MapId == mapId)
                    {
                        continue;
                    }

                    var otherEmpty = new RpgWorld500xMapCompletionSummary(
                        specs[j].MapId,
                        visitedChunks: 0, totalChunks: 2814,
                        discoveredMarkers: 0, totalMarkers: 18,
                        lootedCaches: 0, totalCaches: 12,
                        unlockedShortcuts: 0, totalShortcuts: 4,
                        inspectedStorySpots: 0, totalStorySpots: 4);
                    var otherSummary = service.BuildBountyBoardSummary(otherEmpty);
                    Assert.That(otherSummary.ClaimedCount, Is.EqualTo(0));
                    Assert.That(otherSummary.RemainingSaltSilver, Is.GreaterThan(0));
                }
            }
        }

        [Test]
        public void FieldBountyAndSupplyService_CatalogAffordabilityAndPurchaseGuards_EnforceStockAndWalletCaps()
        {
            var store = new Dictionary<string, int>(StringComparer.Ordinal);
            var service = new RpgWorld500xFieldBountyAndSupplyService(
                k => store.TryGetValue(k, out int v) ? v : 0,
                (k, v) => store[k] = v);

            var supplies = RpgWorld500xBountyAndSupplyCatalog.GetCampSupplies();
            Assert.That(supplies.Count, Is.EqualTo(6));

            Assert.That(service.TryPurchaseSupply("unknown_item", 1), Is.EqualTo(RpgCampSupplyPurchaseStatus.UnknownItem));
            Assert.That(service.TryPurchaseSupply("gauze", 0), Is.EqualTo(RpgCampSupplyPurchaseStatus.InvalidQuantity));

            for (int i = 0; i < supplies.Count; i++)
            {
                Assert.That(service.GetMaxAffordableQuantity(supplies[i].Id), Is.EqualTo(0));
            }

            service.AddSaltSilver(50);
            for (int i = 0; i < supplies.Count; i++)
            {
                var item = supplies[i];
                int expected = Math.Min(item.MaxStock, 50 / item.SaltSilverCost);
                Assert.That(service.GetMaxAffordableQuantity(item.Id), Is.EqualTo(expected), item.Id);
            }

            service.AddSaltSilver(10000);
            for (int i = 0; i < supplies.Count; i++)
            {
                var item = supplies[i];
                Assert.That(service.GetMaxAffordableQuantity(item.Id), Is.EqualTo(item.MaxStock), item.Id);
                Assert.That(
                    service.TryPurchaseSupply(item.Id, item.MaxStock),
                    Is.EqualTo(RpgCampSupplyPurchaseStatus.Succeeded),
                    item.Id);
                Assert.That(service.GetRemainingStock(item.Id), Is.EqualTo(0), item.Id);
                Assert.That(service.GetMaxAffordableQuantity(item.Id), Is.EqualTo(0), item.Id);
            }
        }
    }
}
