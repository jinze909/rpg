using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionPortalShortestPathAndSummaryE2ETests
    {
        [Test]
        public void AllFourRegions_AllPairsShortestPathsAreSymmetricAndAtlasSummaryTracksEveryLeader()
        {
            RpgWorldMapId[] regions =
            {
                RpgWorldMapId.Soundwell,
                RpgWorldMapId.RedSaltHarbor,
                RpgWorldMapId.WhitewaterCrossing,
                RpgWorldMapId.MineCamp
            };

            // Invalid enum values are rejected safely.
            Assert.IsFalse(RpgWorld500xWorldAtlasPortalService.TryFindShortestRegionPath(
                (RpgWorldMapId)255, RpgWorldMapId.Soundwell, out _));
            Assert.IsFalse(RpgWorld500xWorldAtlasPortalService.TryFindShortestRegionPath(
                RpgWorldMapId.Soundwell, (RpgWorldMapId)255, out _));

            // Verify all 16 ordered pairs across the 4-region portal graph.
            for (int i = 0; i < regions.Length; i++)
            {
                for (int j = 0; j < regions.Length; j++)
                {
                    RpgWorldMapId from = regions[i];
                    RpgWorldMapId to = regions[j];

                    Assert.IsTrue(
                        RpgWorld500xWorldAtlasPortalService.TryFindShortestRegionPath(from, to, out var plan),
                        "Expected reachable path from " + from + " to " + to);
                    Assert.AreEqual(from, plan.FromMap);
                    Assert.AreEqual(to, plan.ToMap);
                    StringAssert.Contains("【跨境路线】", plan.SummaryZh);

                    if (from == to)
                    {
                        Assert.IsTrue(plan.IsSameRegion);
                        Assert.IsFalse(plan.IsDirect);
                        Assert.AreEqual(0, plan.TotalTravelMinutesInGame);
                        Assert.AreEqual(0, plan.Hops.Count);
                    }
                    else
                    {
                        Assert.IsFalse(plan.IsSameRegion);
                        Assert.GreaterOrEqual(plan.Hops.Count, 1);
                        Assert.AreEqual(plan.Hops.Count == 1, plan.IsDirect);
                        Assert.Greater(plan.TotalTravelMinutesInGame, 0);

                        Assert.AreEqual(from, plan.Hops[0].FromMap);
                        int summedMinutes = 0;
                        for (int h = 0; h < plan.Hops.Count; h++)
                        {
                            summedMinutes += plan.Hops[h].TravelMinutesInGame;
                            if (h + 1 < plan.Hops.Count)
                            {
                                Assert.AreEqual(plan.Hops[h].ToMap, plan.Hops[h + 1].FromMap);
                            }
                        }

                        Assert.AreEqual(to, plan.Hops[plan.Hops.Count - 1].ToMap);
                        Assert.AreEqual(summedMinutes, plan.TotalTravelMinutesInGame);

                        // Reverse shortest path has identical travel minutes on the bidirectional graph.
                        Assert.IsTrue(
                            RpgWorld500xWorldAtlasPortalService.TryFindShortestRegionPath(to, from, out var reversePlan));
                        Assert.AreEqual(plan.TotalTravelMinutesInGame, reversePlan.TotalTravelMinutesInGame);
                    }
                }
            }

            // Verify BuildWorldAtlasSummary selects each of the 4 regions as leader cleanly without mutating store.
            for (int i = 0; i < regions.Length; i++)
            {
                RpgWorldMapId leader = regions[i];
                var store = new Dictionary<string, int>(StringComparer.Ordinal);
                for (int r = 0; r < regions.Length; r++)
                {
                    store[RpgWorld500xRegionalReputationService.ReputationKey(regions[r])] =
                        regions[r] == leader ? 600 : 100;
                }

                var leaderBounties = RpgWorld500xBountyAndSupplyCatalog.GetBounties(leader);
                Assert.IsNotEmpty(leaderBounties);
                store[leaderBounties[0].MakinomKey] = 1;
                int storeCountBefore = store.Count;

                RpgWorld500xWorldAtlasSummary summary =
                    RpgWorld500xWorldAtlasPortalService.BuildWorldAtlasSummary(store);

                Assert.AreEqual(4, summary.TotalRegions);
                Assert.AreEqual(1, summary.TotalClaimedBounties);
                Assert.Greater(summary.TotalBounties, 1);
                Assert.AreEqual(summary.TotalBounties - 1, summary.RemainingBounties);
                Assert.AreEqual(900, summary.TotalReputationPoints);
                Assert.AreEqual(leader, summary.HighestReputationMap);
                StringAssert.Contains("【四境总览】", summary.SummaryZh);
                StringAssert.Contains(RpgExpandedWorldScale.GetSpec(leader).DisplayNameZh, summary.SummaryZh);
                Assert.AreEqual(storeCountBefore, store.Count);
            }
        }
    }
}
