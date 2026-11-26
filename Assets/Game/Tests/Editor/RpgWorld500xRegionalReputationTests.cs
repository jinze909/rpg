using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xRegionalReputationTests
    {
        [Test]
        public void AllFourMaps_HaveSafeKeysFactionNamesAndSwornHonorTitles()
        {
            foreach (RpgExpandedMapSpec spec in RpgExpandedWorldScale.AllSpecs)
            {
                string key = RpgWorld500xRegionalReputationService.ReputationKey(spec.MapId);
                Assert.IsTrue(RpgWorld500xEnrichedContentAuditor.IsSafeWorldKey(key), key);
                Assert.IsFalse(string.IsNullOrWhiteSpace(
                    RpgWorld500xRegionalReputationService.GetFactionNameZh(spec.MapId)));
                Assert.IsFalse(string.IsNullOrWhiteSpace(
                    RpgWorld500xRegionalReputationService.GetSwornHonorTitleZh(spec.MapId)));
            }
        }

        [Test]
        public void LargePositiveReputationGrant_SaturatesWithoutIntegerOverflow()
        {
            var store = new Dictionary<string, int>(StringComparer.Ordinal);
            var service = new RpgWorld500xRegionalReputationService(
                k => store.TryGetValue(k, out int value) ? value : 0,
                (k, v) => store[k] = v);

            const RpgWorldMapId mapId = RpgWorldMapId.WhitewaterCrossing;
            Assert.AreEqual(400, service.AddPoints(mapId, 400).Points);

            // Previously 400 + int.MaxValue overflowed a signed int, so
            // Mathf.Clamp would reset the player's earned rank to zero.
            RpgRegionalReputationSnapshot capped = service.AddPoints(mapId, int.MaxValue);
            Assert.AreEqual(RpgWorld500xRegionalReputationService.MaxReputationPoints, capped.Points);
            Assert.AreEqual(RpgRegionalReputationRank.Sworn, capped.Rank);
            Assert.AreEqual(30, capped.CampSupplyDiscountPercent);
            Assert.AreEqual(1000, store[RpgWorld500xRegionalReputationService.ReputationKey(mapId)]);

            Assert.AreEqual(1000, service.AddPoints(mapId, int.MaxValue).Points);
            Assert.AreEqual(1000, service.AddPoints(mapId, -10).Points,
                "Invalid negative gains must not lower earned reputation.");
        }

        [Test]
        public void ReputationRanksDiscountsAndCompletionSync_WorkMonotonicallyWithinBounds()
        {
            var store = new Dictionary<string, int>(StringComparer.Ordinal);
            var service = new RpgWorld500xRegionalReputationService(
                k => store.TryGetValue(k, out int v) ? v : 0,
                (k, v) => store[k] = v);

            RpgRegionalReputationSnapshot initial = service.GetSnapshot(RpgWorldMapId.RedSaltHarbor);
            Assert.AreEqual(RpgRegionalReputationRank.Stranger, initial.Rank);
            Assert.AreEqual(0, initial.CampSupplyDiscountPercent);
            Assert.AreEqual(150, initial.PointsToNextRank);
            Assert.AreEqual(30, RpgWorld500xRegionalReputationService.ComputeDiscountedCost(30, initial.Rank));

            // Sync from active exploration progress: 10*15 + 6*20 + 2*35 + 2*25 + 2*60 = 510 -> Ally (20% discount)
            var summary = new RpgWorld500xMapCompletionSummary(
                RpgWorldMapId.RedSaltHarbor,
                visitedChunks: 200, totalChunks: 5782,
                discoveredMarkers: 10, totalMarkers: 20,
                lootedCaches: 6, totalCaches: 12,
                unlockedShortcuts: 2, totalShortcuts: 4,
                inspectedStorySpots: 2, totalStorySpots: 4);

            RpgRegionalReputationSnapshot ally = service.SyncMinimumFromCompletion(summary, claimedBounties: 2);
            Assert.AreEqual(510, ally.Points);
            Assert.AreEqual(RpgRegionalReputationRank.Ally, ally.Rank);
            Assert.AreEqual(20, ally.CampSupplyDiscountPercent);
            Assert.AreEqual(24, RpgWorld500xRegionalReputationService.ComputeDiscountedCost(30, ally.Rank));

            // Add points up to cap -> Sworn (30% discount + honor title)
            RpgRegionalReputationSnapshot sworn = service.AddPoints(RpgWorldMapId.RedSaltHarbor, 1000);
            Assert.AreEqual(RpgWorld500xRegionalReputationService.MaxReputationPoints, sworn.Points);
            Assert.AreEqual(RpgRegionalReputationRank.Sworn, sworn.Rank);
            Assert.AreEqual(30, sworn.CampSupplyDiscountPercent);
            Assert.AreEqual("赤盐港义士", sworn.HonorTitleZh);
            Assert.AreEqual(0, sworn.PointsToNextRank);
            Assert.AreEqual(21, RpgWorld500xRegionalReputationService.ComputeDiscountedCost(30, sworn.Rank));
        }

        [Test]
        public void BuildWorldOverview_AggregatesFourRegionsBestDiscountAndPerMapCostHelper()
        {
            var store = new Dictionary<string, int>(StringComparer.Ordinal);
            var service = new RpgWorld500xRegionalReputationService(
                k => store.TryGetValue(k, out int v) ? v : 0,
                (k, v) => store[k] = v);

            service.AddPoints(RpgWorldMapId.Soundwell, 160);          // Trusted (10%)
            service.AddPoints(RpgWorldMapId.RedSaltHarbor, 760);      // Sworn (30%)

            RpgWorld500xWorldReputationOverview overview = service.BuildWorldOverview();
            Assert.AreEqual(4, overview.Snapshots.Count);
            Assert.AreEqual(920, overview.TotalPoints);
            Assert.AreEqual(1, overview.SwornRegionCount);
            Assert.AreEqual(RpgWorldMapId.RedSaltHarbor, overview.HighestStandingMapId);
            Assert.AreEqual(30, overview.BestCampDiscountPercent);
            Assert.IsTrue(overview.SummaryLineZh.Contains("【四境声望总览】"));
            Assert.IsTrue(overview.SummaryLineZh.Contains("累计 920 点"));
            Assert.IsTrue(overview.SummaryLineZh.Contains("生死托付 1/4 境"));
            Assert.IsTrue(overview.SummaryLineZh.Contains("-30%"));

            Assert.AreEqual(27, service.ComputeDiscountedCostForMap(RpgWorldMapId.Soundwell, 30));
            Assert.AreEqual(21, service.ComputeDiscountedCostForMap(RpgWorldMapId.RedSaltHarbor, 30));
            Assert.AreEqual(30, service.ComputeDiscountedCostForMap(RpgWorldMapId.MineCamp, 30));
        }
    }
}
