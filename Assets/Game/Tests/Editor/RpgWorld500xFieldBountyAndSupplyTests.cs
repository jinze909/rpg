using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFieldBountyAndSupplyTests
    {
        [Test]
        public void All12BountiesAnd6CampSupplies_HaveUniqueIdsValidKeysAndPositiveRewards()
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var keys = new HashSet<string>(StringComparer.Ordinal);
            int totalBounties = 0;

            foreach (RpgExpandedMapSpec spec in RpgExpandedWorldScale.AllSpecs)
            {
                IReadOnlyList<RpgWorld500xFieldBountyDef> bounties =
                    RpgWorld500xBountyAndSupplyCatalog.GetBounties(spec.MapId);
                Assert.AreEqual(3, bounties.Count, spec.DisplayNameEn);
                totalBounties += bounties.Count;

                for (int i = 0; i < bounties.Count; i++)
                {
                    RpgWorld500xFieldBountyDef b = bounties[i];
                    Assert.IsTrue(ids.Add(b.Id), b.Id);
                    Assert.IsTrue(keys.Add(b.MakinomKey), b.MakinomKey);
                    Assert.IsTrue(b.MakinomKey.StartsWith("rpg.return.world.bounty.", StringComparison.Ordinal));
                    Assert.IsFalse(string.IsNullOrWhiteSpace(b.TitleZh));
                    Assert.IsFalse(string.IsNullOrWhiteSpace(b.DescriptionZh));
                    Assert.Greater(b.TargetCount, 0);
                    Assert.Greater(b.SaltSilverReward, 0);
                }
            }

            Assert.AreEqual(12, totalBounties);

            IReadOnlyList<RpgWorld500xCampSupplyItemDef> supplies =
                RpgWorld500xBountyAndSupplyCatalog.GetCampSupplies();
            Assert.AreEqual(6, supplies.Count);
            for (int i = 0; i < supplies.Count; i++)
            {
                RpgWorld500xCampSupplyItemDef item = supplies[i];
                Assert.IsTrue(ids.Add(item.Id), item.Id);
                Assert.IsTrue(keys.Add(item.OwnedCountKey), item.OwnedCountKey);
                Assert.IsTrue(item.OwnedCountKey.StartsWith("rpg.return.world.supply.item.", StringComparison.Ordinal));
                Assert.Greater(item.SaltSilverCost, 0);
                Assert.Greater(item.MaxStock, 0);
            }
        }

        [Test]
        public void BountiesClaimIdempotentlyAndCampSupplyExchangeEnforcesBalanceAndStock()
        {
            var store = new Dictionary<string, int>(StringComparer.Ordinal);
            var service = new RpgWorld500xFieldBountyAndSupplyService(
                k => store.TryGetValue(k, out int v) ? v : 0,
                (k, v) => store[k] = v);

            var summary = new RpgWorld500xMapCompletionSummary(
                RpgWorldMapId.Soundwell,
                visitedChunks: 120, totalChunks: 2814,
                discoveredMarkers: 6, totalMarkers: 18,
                lootedCaches: 4, totalCaches: 12,
                unlockedShortcuts: 1, totalShortcuts: 4,
                inspectedStorySpots: 2, totalStorySpots: 4);

            // Claims sw_b01 (40) + sw_b02 (50) = 90 salt silver; sw_b03 needs 2 shortcuts so stays unclaimed
            List<RpgWorld500xFieldBountyDef> firstClaim = service.ClaimCompletedBounties(summary);
            Assert.AreEqual(2, firstClaim.Count);
            Assert.AreEqual(90, service.SaltSilverBalance);

            // Duplicate claim grants nothing
            Assert.AreEqual(0, service.ClaimCompletedBounties(summary).Count);
            Assert.AreEqual(90, service.SaltSilverBalance);

            // Purchase 2 damp_cloth (25 * 2 = 50 -> remaining 40)
            Assert.AreEqual(RpgCampSupplyPurchaseStatus.Succeeded, service.TryPurchaseSupply("damp_cloth", 2));
            Assert.AreEqual(40, service.SaltSilverBalance);
            Assert.AreEqual(2, service.GetOwnedSupplyCount("damp_cloth"));
            Assert.AreEqual(4, service.GetRemainingStock("damp_cloth"));

            // Purchase exceeding remaining balance fails cleanly
            Assert.AreEqual(RpgCampSupplyPurchaseStatus.InsufficientSaltSilver, service.TryPurchaseSupply("iron_clamps", 2));
            Assert.AreEqual(40, service.SaltSilverBalance);

            // Purchase exceeding max stock fails cleanly
            Assert.AreEqual(RpgCampSupplyPurchaseStatus.OutOfStock, service.TryPurchaseSupply("damp_cloth", 10));
            Assert.AreEqual(RpgCampSupplyPurchaseStatus.UnknownItem, service.TryPurchaseSupply("unknown_item", 1));
            Assert.AreEqual(RpgCampSupplyPurchaseStatus.InvalidQuantity, service.TryPurchaseSupply("gauze", 0));
        }
        [Test]
        public void ExtremeQuantityAndWalletCreditNeverOverflowSavedCurrencyOrInventory()
        {
            var store = new Dictionary<string, int>(StringComparer.Ordinal);
            var service = new RpgWorld500xFieldBountyAndSupplyService(
                k => store.TryGetValue(k, out int v) ? v : 0,
                (k, v) => store[k] = v);

            service.AddSaltSilver(int.MaxValue);
            Assert.AreEqual(int.MaxValue, service.SaltSilverBalance);
            Assert.AreEqual(RpgCampSupplyPurchaseStatus.Succeeded,
                service.TryPurchaseSupply("gauze", 2));
            int balanceBeforeInvalidPurchase = service.SaltSilverBalance;

            Assert.AreEqual(RpgCampSupplyPurchaseStatus.OutOfStock,
                service.TryPurchaseSupply("gauze", int.MaxValue),
                "An overflowing quantity must never credit currency or corrupt stock.");
            Assert.AreEqual(balanceBeforeInvalidPurchase, service.SaltSilverBalance);
            Assert.AreEqual(2, service.GetOwnedSupplyCount("gauze"));

            service.AddSaltSilver(100);
            Assert.AreEqual(int.MaxValue, service.SaltSilverBalance,
                "A reward near the int limit must saturate, not wrap negative.");
        }

    }
}
