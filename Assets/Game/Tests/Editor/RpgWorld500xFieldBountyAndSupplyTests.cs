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

        [Test]
        public void BuildBountyBoardSummary_PreviewsClaimableAndRemainingRewardsWithoutMutatingState()
        {
            var store = new Dictionary<string, int>(StringComparer.Ordinal);
            var service = new RpgWorld500xFieldBountyAndSupplyService(
                k => store.TryGetValue(k, out int v) ? v : 0,
                (k, v) => store[k] = v);

            var initialProgress = new RpgWorld500xMapCompletionSummary(
                RpgWorldMapId.Soundwell,
                visitedChunks: 10, totalChunks: 2814,
                discoveredMarkers: 2, totalMarkers: 18,
                lootedCaches: 1, totalCaches: 12,
                unlockedShortcuts: 0, totalShortcuts: 4,
                inspectedStorySpots: 0, totalStorySpots: 4);

            RpgWorld500xBountyBoardSummary before = service.BuildBountyBoardSummary(initialProgress);
            Assert.AreEqual(RpgWorldMapId.Soundwell, before.MapId);
            Assert.AreEqual(3, before.TotalBounties);
            Assert.AreEqual(0, before.ClaimedCount);
            Assert.AreEqual(0, before.ClaimableCount);
            Assert.AreEqual(0, before.ClaimableSaltSilver);
            Assert.AreEqual(150, before.RemainingSaltSilver);
            StringAssert.Contains("待完成 3 项", before.SummaryZh);

            var readyProgress = new RpgWorld500xMapCompletionSummary(
                RpgWorldMapId.Soundwell,
                visitedChunks: 120, totalChunks: 2814,
                discoveredMarkers: 6, totalMarkers: 18,
                lootedCaches: 4, totalCaches: 12,
                unlockedShortcuts: 1, totalShortcuts: 4,
                inspectedStorySpots: 2, totalStorySpots: 4);

            RpgWorld500xBountyBoardSummary claimable = service.BuildBountyBoardSummary(readyProgress);
            Assert.AreEqual(0, claimable.ClaimedCount);
            Assert.AreEqual(2, claimable.ClaimableCount);
            Assert.AreEqual(90, claimable.ClaimableSaltSilver);
            Assert.AreEqual(150, claimable.RemainingSaltSilver);
            StringAssert.Contains("可领 2 项（+90 盐银）", claimable.SummaryZh);
            // Strictly read-only: wallet and flags remain untouched.
            Assert.AreEqual(0, service.SaltSilverBalance);
            Assert.AreEqual(0, store.Count);

            service.ClaimCompletedBounties(readyProgress);
            RpgWorld500xBountyBoardSummary afterPartialClaim = service.BuildBountyBoardSummary(readyProgress);
            Assert.AreEqual(2, afterPartialClaim.ClaimedCount);
            Assert.AreEqual(0, afterPartialClaim.ClaimableCount);
            Assert.AreEqual(0, afterPartialClaim.ClaimableSaltSilver);
            Assert.AreEqual(60, afterPartialClaim.RemainingSaltSilver);
            StringAssert.Contains("已领 2/3", afterPartialClaim.SummaryZh);
            StringAssert.Contains("待完成 1 项", afterPartialClaim.SummaryZh);

            var completedProgress = new RpgWorld500xMapCompletionSummary(
                RpgWorldMapId.Soundwell,
                visitedChunks: 200, totalChunks: 2814,
                discoveredMarkers: 6, totalMarkers: 18,
                lootedCaches: 4, totalCaches: 12,
                unlockedShortcuts: 2, totalShortcuts: 4,
                inspectedStorySpots: 2, totalStorySpots: 4);

            service.ClaimCompletedBounties(completedProgress);
            RpgWorld500xBountyBoardSummary cleared = service.BuildBountyBoardSummary(completedProgress);
            Assert.AreEqual(3, cleared.ClaimedCount);
            Assert.AreEqual(0, cleared.ClaimableCount);
            Assert.AreEqual(0, cleared.RemainingSaltSilver);
            StringAssert.Contains("本境悬赏已全部结清", cleared.SummaryZh);
        }

        [Test]
        public void GetMaxAffordableQuantity_CapsByWalletBalanceAndRemainingStock()
        {
            var store = new Dictionary<string, int>(StringComparer.Ordinal);
            var service = new RpgWorld500xFieldBountyAndSupplyService(
                k => store.TryGetValue(k, out int v) ? v : 0,
                (k, v) => store[k] = v);

            Assert.AreEqual(0, service.GetMaxAffordableQuantity(null));
            Assert.AreEqual(0, service.GetMaxAffordableQuantity(""));
            Assert.AreEqual(0, service.GetMaxAffordableQuantity("unknown_item"));
            Assert.AreEqual(0, service.GetMaxAffordableQuantity("gauze"));

            service.AddSaltSilver(40);
            Assert.AreEqual(2, service.GetMaxAffordableQuantity("gauze"));       // 40 / 18 = 2 (< 10 stock)
            Assert.AreEqual(2, service.GetMaxAffordableQuantity("rations"));     // 40 / 15 = 2 (< 12 stock)
            Assert.AreEqual(1, service.GetMaxAffordableQuantity("iron_clamps")); // 40 / 35 = 1 (< 4 stock)

            service.AddSaltSilver(int.MaxValue);
            Assert.AreEqual(4, service.GetMaxAffordableQuantity("iron_clamps")); // capped at maxStock 4
            Assert.AreEqual(RpgCampSupplyPurchaseStatus.Succeeded, service.TryPurchaseSupply("iron_clamps", 3));
            Assert.AreEqual(1, service.GetMaxAffordableQuantity("iron_clamps"));
            Assert.AreEqual(RpgCampSupplyPurchaseStatus.Succeeded, service.TryPurchaseSupply("iron_clamps", 1));
            Assert.AreEqual(0, service.GetMaxAffordableQuantity("iron_clamps"));
        }
    }
}