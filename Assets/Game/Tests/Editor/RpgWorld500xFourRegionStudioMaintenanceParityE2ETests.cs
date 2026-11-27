using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    /// <summary>
    /// Four-region E2E regression tests cross-checking RpgWorld500xMaintenanceReport.Generate()
    /// determinism, per-region catalog totals parity, strong portal-graph reachability from
    /// every region, and global Makinom key disjointness across reputation, save slots, and
    /// cross-map expedition milestones.
    /// </summary>
    public sealed class RpgWorld500xFourRegionStudioMaintenanceParityE2ETests
    {
        [Test]
        public void StudioMaintenanceReport_IsDeterministicAndMatchesDirectFourRegionCatalogTotals()
        {
            RpgWorld500xStudioHealthSnapshot reportA = RpgWorld500xMaintenanceReport.Generate();
            RpgWorld500xStudioHealthSnapshot reportB = RpgWorld500xMaintenanceReport.Generate();

            Assert.IsTrue(reportA.IsHealthy, reportA.FormattedReport);
            Assert.IsTrue(reportB.IsHealthy, reportB.FormattedReport);
            Assert.AreEqual(0, reportA.SpatialIssueCount);
            Assert.AreEqual(0, reportA.ContentAuditErrorCount);
            Assert.AreEqual(0, reportA.MetaSystemAuditErrorCount);
            Assert.AreEqual(reportA.FormattedReport, reportB.FormattedReport,
                "Repeated studio maintenance report generation must be deterministic.");
            StringAssert.Contains("Status: HEALTHY", reportA.FormattedReport);
            StringAssert.Contains("spatial=0, content=0, meta=0", reportA.FormattedReport);

            IReadOnlyList<RpgExpandedMapSpec> specs = RpgExpandedWorldScale.AllSpecs;
            Assert.AreEqual(4, specs.Count);

            long expectedTiles = 0;
            int expectedChunks = 0;
            int expectedDistricts = 0;
            int expectedBounties = 0;
            int expectedThreatProfiles = 0;
            int expectedRegionRoutes = 0;

            for (int i = 0; i < specs.Count; i++)
            {
                RpgExpandedMapSpec spec = specs[i];
                expectedTiles += (long)spec.ExpandedWidth * spec.ExpandedHeight;
                expectedChunks += (spec.ExpandedWidth / RpgLargeMapChunkRenderer.ChunkSizeTiles) *
                                  (spec.ExpandedHeight / RpgLargeMapChunkRenderer.ChunkSizeTiles);
                expectedDistricts += RpgWorld500xDistrictCatalog.GetDistricts(spec.MapId).Count;
                expectedBounties += RpgWorld500xBountyAndSupplyCatalog.GetBounties(spec.MapId).Count;
                expectedThreatProfiles += RpgWorld500xBestiaryCatalog.GetProfiles(spec.MapId).Count;
                expectedRegionRoutes += RpgWorld500xWorldAtlasPortalService.GetConnectedRegions(spec.MapId).Count;
            }

            Assert.AreEqual(expectedTiles, reportA.TotalTiles);
            Assert.AreEqual(expectedChunks, reportA.TotalChunks);
            Assert.AreEqual(expectedDistricts, reportA.TotalDistricts);
            Assert.AreEqual(expectedBounties, reportA.TotalBounties);
            Assert.AreEqual(RpgWorld500xBountyAndSupplyCatalog.GetCampSupplies().Count, reportA.TotalCampSupplies);
            Assert.AreEqual(expectedThreatProfiles, reportA.TotalThreatProfiles);
            Assert.AreEqual(expectedRegionRoutes, reportA.TotalRegionRoutes);
            Assert.AreEqual(RpgWorld500xSaveSlotManager.AllSlots.Count, reportA.TotalSaveSlots);
            Assert.AreEqual(RpgWorld500xExpeditionMilestoneService.AllMilestones.Count, reportA.TotalExpeditionMilestones);
        }

        [Test]
        public void FourRegionMetaSystems_HaveGloballyDisjointSafeKeysAndFullDirectedReachability()
        {
            IReadOnlyList<RpgExpandedMapSpec> specs = RpgExpandedWorldScale.AllSpecs;
            Assert.AreEqual(4, specs.Count);

            var metaKeys = new HashSet<string>(StringComparer.Ordinal);

            // Every region's reputation key and localized faction/title strings must be valid and unique.
            for (int i = 0; i < specs.Count; i++)
            {
                RpgWorldMapId mapId = specs[i].MapId;
                string repKey = RpgWorld500xRegionalReputationService.ReputationKey(mapId);
                Assert.IsTrue(RpgWorld500xEnrichedContentAuditor.IsSafeWorldKey(repKey), $"Unsafe rep key: {repKey}");
                Assert.IsTrue(metaKeys.Add(repKey), $"Duplicate meta key: {repKey}");
                Assert.IsFalse(string.IsNullOrWhiteSpace(RpgWorld500xRegionalReputationService.GetFactionNameZh(mapId)));
                Assert.IsFalse(string.IsNullOrWhiteSpace(RpgWorld500xRegionalReputationService.GetSwornHonorTitleZh(mapId)));

                // Directed BFS from every region must reach all 4 regions.
                var visited = new HashSet<RpgWorldMapId> { mapId };
                var queue = new Queue<RpgWorldMapId>();
                queue.Enqueue(mapId);
                while (queue.Count > 0)
                {
                    RpgWorldMapId cur = queue.Dequeue();
                    List<RpgWorld500xRegionRouteDef> routes = RpgWorld500xWorldAtlasPortalService.GetConnectedRegions(cur);
                    for (int r = 0; r < routes.Count; r++)
                    {
                        Assert.Greater(routes[r].TravelMinutesInGame, 0);
                        Assert.IsFalse(string.IsNullOrWhiteSpace(routes[r].RouteNameZh));
                        if (visited.Add(routes[r].ToMap))
                        {
                            queue.Enqueue(routes[r].ToMap);
                        }
                    }
                }

                Assert.AreEqual(specs.Count, visited.Count,
                    $"Portal graph from {mapId} did not reach all four regions.");
            }

            // Save slot keys must be safe world keys and disjoint from reputation keys.
            IReadOnlyList<RpgWorld500xSaveSlotId> slots = RpgWorld500xSaveSlotManager.AllSlots;
            Assert.Greater(slots.Count, 0);
            for (int i = 0; i < slots.Count; i++)
            {
                string slotKey = RpgWorld500xSaveSlotManager.GetStorageKey(slots[i]);
                Assert.IsTrue(RpgWorld500xEnrichedContentAuditor.IsSafeWorldKey(slotKey), $"Unsafe slot key: {slotKey}");
                Assert.IsTrue(metaKeys.Add(slotKey), $"Colliding save slot key: {slotKey}");
            }

            // Cross-map expedition milestone keys must be safe world keys and disjoint from all above.
            IReadOnlyList<RpgWorld500xExpeditionMilestoneDef> milestones =
                RpgWorld500xExpeditionMilestoneService.AllMilestones;
            Assert.Greater(milestones.Count, 0);
            for (int i = 0; i < milestones.Count; i++)
            {
                RpgWorld500xExpeditionMilestoneDef m = milestones[i];
                Assert.IsTrue(RpgWorld500xEnrichedContentAuditor.IsSafeWorldKey(m.MakinomKey),
                    $"Unsafe milestone key: {m.MakinomKey}");
                Assert.IsTrue(metaKeys.Add(m.MakinomKey), $"Colliding milestone key: {m.MakinomKey}");
                Assert.Greater(m.TargetCount, 0);
                Assert.Greater(m.SaltSilverReward, 0);
                Assert.IsFalse(string.IsNullOrWhiteSpace(m.TitleZh));
            }
        }
    }
}
