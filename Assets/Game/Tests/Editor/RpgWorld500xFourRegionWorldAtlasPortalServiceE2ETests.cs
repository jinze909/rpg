using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionWorldAtlasPortalServiceE2ETests
    {
        [Test]
        public void TryFindShortestRegionPath_AllSixteenPairs_IsSymmetricContiguousAndRejectsInvalidEndpoints()
        {
            var specs = RpgExpandedWorldScale.AllSpecs;
            Assert.That(specs.Count, Is.EqualTo(4));

            var invalid = (RpgWorldMapId)255;
            Assert.IsFalse(RpgWorld500xWorldAtlasPortalService.TryFindShortestRegionPath(invalid, specs[0].MapId, out _));
            Assert.IsFalse(RpgWorld500xWorldAtlasPortalService.TryFindShortestRegionPath(specs[0].MapId, invalid, out _));
            Assert.IsFalse(RpgWorld500xWorldAtlasPortalService.TryFindShortestRegionPath(invalid, invalid, out _));

            for (int i = 0; i < specs.Count; i++)
            {
                for (int j = 0; j < specs.Count; j++)
                {
                    var from = specs[i].MapId;
                    var to = specs[j].MapId;

                    Assert.IsTrue(RpgWorld500xWorldAtlasPortalService.TryFindShortestRegionPath(from, to, out var forward));
                    Assert.IsTrue(RpgWorld500xWorldAtlasPortalService.TryFindShortestRegionPath(to, from, out var reverse));
                    Assert.That(forward.FromMap, Is.EqualTo(from));
                    Assert.That(forward.ToMap, Is.EqualTo(to));
                    Assert.That(forward.TotalTravelMinutesInGame, Is.EqualTo(reverse.TotalTravelMinutesInGame));
                    StringAssert.Contains("【跨境路线】", forward.SummaryZh);

                    if (from == to)
                    {
                        Assert.IsTrue(forward.IsSameRegion);
                        Assert.IsFalse(forward.IsDirect);
                        Assert.That(forward.TotalTravelMinutesInGame, Is.EqualTo(0));
                        Assert.IsEmpty(forward.Hops);
                    }
                    else
                    {
                        Assert.IsFalse(forward.IsSameRegion);
                        Assert.That(forward.IsDirect, Is.EqualTo(forward.Hops.Count == 1));
                        Assert.That(forward.Hops.Count, Is.GreaterThanOrEqualTo(1));
                        Assert.That(forward.Hops[0].FromMap, Is.EqualTo(from));
                        Assert.That(forward.Hops[forward.Hops.Count - 1].ToMap, Is.EqualTo(to));

                        int summed = 0;
                        for (int k = 0; k < forward.Hops.Count; k++)
                        {
                            summed += forward.Hops[k].TravelMinutesInGame;
                            if (k + 1 < forward.Hops.Count)
                            {
                                Assert.That(forward.Hops[k].ToMap, Is.EqualTo(forward.Hops[k + 1].FromMap));
                            }
                        }

                        Assert.That(summed, Is.EqualTo(forward.TotalTravelMinutesInGame));
                    }
                }
            }
        }

        [Test]
        public void BuildWorldAtlasSummary_RotatesHighestReputationLeaderAcrossAllFourRegionsWithoutMutatingStore()
        {
            var specs = RpgExpandedWorldScale.AllSpecs;
            Assert.That(specs.Count, Is.EqualTo(4));

            for (int i = 0; i < specs.Count; i++)
            {
                var leaderMap = specs[i].MapId;
                var runnerUpMap = specs[(i + 1) % specs.Count].MapId;
                var bounties = RpgWorld500xBountyAndSupplyCatalog.GetBounties(leaderMap);

                var store = new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    [RpgWorld500xRegionalReputationService.ReputationKey(leaderMap)] = 500,
                    [RpgWorld500xRegionalReputationService.ReputationKey(runnerUpMap)] = 150,
                    [bounties[0].MakinomKey] = 1,
                    [bounties[1].MakinomKey] = 1
                };
                int initialCount = store.Count;

                var summary = RpgWorld500xWorldAtlasPortalService.BuildWorldAtlasSummary(store);
                Assert.That(summary.TotalRegions, Is.EqualTo(4));
                Assert.That(summary.Entries.Count, Is.EqualTo(4));
                Assert.That(summary.TotalClaimedBounties, Is.EqualTo(2));
                Assert.That(summary.TotalBounties, Is.EqualTo(12));
                Assert.That(summary.RemainingBounties, Is.EqualTo(10));
                Assert.That(summary.TotalReputationPoints, Is.EqualTo(650));
                Assert.That(summary.HighestReputationMap, Is.EqualTo(leaderMap));
                StringAssert.Contains("【四境总览】", summary.SummaryZh);
                StringAssert.Contains(specs[i].DisplayNameZh, summary.SummaryZh);
                Assert.That(store.Count, Is.EqualTo(initialCount));
            }
        }
    }
}
