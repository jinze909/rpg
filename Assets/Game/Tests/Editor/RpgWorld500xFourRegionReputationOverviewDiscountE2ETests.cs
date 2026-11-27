using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionReputationOverviewDiscountE2ETests
    {
        [Test]
        public void RegionalReputationService_RotatesSwornLeaderAcrossAllFourRegionsAndComputesPerMapDiscounts()
        {
            var specs = RpgExpandedWorldScale.AllSpecs;
            Assert.That(specs.Count, Is.EqualTo(4));

            for (int i = 0; i < specs.Count; i++)
            {
                var leaderMap = specs[i].MapId;
                var trustedMap = specs[(i + 1) % specs.Count].MapId;
                var store = new Dictionary<string, int>(StringComparer.Ordinal);
                var service = new RpgWorld500xRegionalReputationService(
                    k => store.TryGetValue(k, out int v) ? v : 0,
                    (k, v) => store[k] = v);

                service.AddPoints(leaderMap, 760);
                service.AddPoints(trustedMap, 160);

                var overview = service.BuildWorldOverview();
                Assert.That(overview.Snapshots.Count, Is.EqualTo(4));
                Assert.That(overview.TotalPoints, Is.EqualTo(920));
                Assert.That(overview.SwornRegionCount, Is.EqualTo(1));
                Assert.That(overview.HighestStandingMapId, Is.EqualTo(leaderMap));
                Assert.That(overview.BestCampDiscountPercent, Is.EqualTo(30));
                StringAssert.Contains("【四境声望总览】", overview.SummaryLineZh);
                StringAssert.Contains("累计 920 点", overview.SummaryLineZh);
                StringAssert.Contains("生死托付 1/4 境", overview.SummaryLineZh);
                StringAssert.Contains("-30%", overview.SummaryLineZh);
                StringAssert.Contains(specs[i].DisplayNameZh, overview.SummaryLineZh);

                Assert.That(service.ComputeDiscountedCostForMap(leaderMap, 30), Is.EqualTo(21));
                Assert.That(service.ComputeDiscountedCostForMap(trustedMap, 30), Is.EqualTo(27));

                for (int j = 0; j < specs.Count; j++)
                {
                    var mapId = specs[j].MapId;
                    if (mapId == leaderMap || mapId == trustedMap)
                    {
                        continue;
                    }

                    Assert.That(service.ComputeDiscountedCostForMap(mapId, 30), Is.EqualTo(30));
                }
            }
        }

        [Test]
        public void RegionalReputationService_AllFourRegionsSwornAndExactIntegerRoundingAcrossCatalogPrices()
        {
            var specs = RpgExpandedWorldScale.AllSpecs;
            var supplies = RpgWorld500xBountyAndSupplyCatalog.GetCampSupplies();
            var store = new Dictionary<string, int>(StringComparer.Ordinal);
            var service = new RpgWorld500xRegionalReputationService(
                k => store.TryGetValue(k, out int v) ? v : 0,
                (k, v) => store[k] = v);

            for (int i = 0; i < specs.Count; i++)
            {
                service.AddPoints(specs[i].MapId, 1000);
            }

            var overview = service.BuildWorldOverview();
            Assert.That(overview.Snapshots.Count, Is.EqualTo(4));
            Assert.That(overview.SwornRegionCount, Is.EqualTo(4));
            Assert.That(overview.BestCampDiscountPercent, Is.EqualTo(30));
            StringAssert.Contains("生死托付 4/4 境", overview.SummaryLineZh);

            for (int i = 0; i < specs.Count; i++)
            {
                var mapId = specs[i].MapId;
                var snap = service.GetSnapshot(mapId);
                Assert.That(snap.Rank, Is.EqualTo(RpgRegionalReputationRank.Sworn));
                Assert.That(snap.PointsToNextRank, Is.EqualTo(0));

                for (int j = 0; j < supplies.Count; j++)
                {
                    int baseCost = supplies[j].SaltSilverCost;
                    int expected = (int)(((long)baseCost * (100 - snap.CampSupplyDiscountPercent) + 99L) / 100L);
                    Assert.That(service.ComputeDiscountedCostForMap(mapId, baseCost), Is.EqualTo(expected));
                }

                Assert.That(service.ComputeDiscountedCostForMap(mapId, 0), Is.EqualTo(0));
                Assert.That(service.ComputeDiscountedCostForMap(mapId, -5), Is.EqualTo(0));
            }
        }
    }
}
