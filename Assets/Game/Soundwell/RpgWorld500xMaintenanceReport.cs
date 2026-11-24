using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Rpg.Soundwell
{
    public sealed class RpgWorld500xStudioHealthSnapshot
    {
        public long TotalTiles { get; set; }
        public int TotalChunks { get; set; }
        public int TotalLandmarks { get; set; }
        public int TotalCaches { get; set; }
        public int TotalShortcuts { get; set; }
        public int TotalFieldNpcs { get; set; }
        public int TotalStorySpots { get; set; }
        public int TotalAmbientZones { get; set; }
        public int TotalDistricts { get; set; }
        public int TotalBounties { get; set; }
        public int TotalCampSupplies { get; set; }
        public int TotalThreatProfiles { get; set; }
        public int TotalRegionRoutes { get; set; }
        public int TotalSaveSlots { get; set; }
        public int TotalExpeditionMilestones { get; set; }
        public int SpatialIssueCount { get; set; }
        public int ContentAuditErrorCount { get; set; }
        public int MetaSystemAuditErrorCount { get; set; }
        public string FormattedReport { get; set; } = string.Empty;

        public bool IsHealthy =>
            SpatialIssueCount == 0 &&
            ContentAuditErrorCount == 0 &&
            MetaSystemAuditErrorCount == 0;
    }

    /// <summary>
    /// Studio-grade one-click maintenance and health report generator for the entire
    /// 500x-area RPG world. Unifies spatial/memory integrity checks, cross-system
    /// enriched content auditing, meta-system checks (4-region portal graph, regional
    /// reputation, multi-slot save keys, and cross-map expedition milestones), and a
    /// Unity Editor Tools menu entry.
    /// </summary>
    public static class RpgWorld500xMaintenanceReport
    {
        public static RpgWorld500xStudioHealthSnapshot Generate()
        {
            List<RpgMapValidationIssue> spatialIssues = RpgWorld500xMapIntegrityValidator.ValidateAllFourMaps();
            RpgWorld500xContentAuditReport contentAudit = RpgWorld500xEnrichedContentAuditor.AuditAllMaps();
            var metaErrors = new List<string>();

            long totalTiles = 0;
            int totalChunks = 0;
            int totalDistricts = 0;
            int totalBounties = 0;
            int totalThreatProfiles = 0;
            int totalRegionRoutes = 0;

            IReadOnlyList<RpgExpandedMapSpec> specs = RpgExpandedWorldScale.AllSpecs;
            for (int i = 0; i < specs.Count; i++)
            {
                RpgExpandedMapSpec spec = specs[i];
                totalTiles += (long)spec.ExpandedWidth * spec.ExpandedHeight;
                totalChunks += (spec.ExpandedWidth / RpgLargeMapChunkRenderer.ChunkSizeTiles) *
                               (spec.ExpandedHeight / RpgLargeMapChunkRenderer.ChunkSizeTiles);
                totalDistricts += RpgWorld500xDistrictCatalog.GetDistricts(spec.MapId).Count;
                totalBounties += RpgWorld500xBountyAndSupplyCatalog.GetBounties(spec.MapId).Count;
                totalThreatProfiles += RpgWorld500xBestiaryCatalog.GetProfiles(spec.MapId).Count;

                List<RpgWorld500xRegionRouteDef> routes = RpgWorld500xWorldAtlasPortalService.GetConnectedRegions(spec.MapId);
                totalRegionRoutes += routes.Count;
                for (int r = 0; r < routes.Count; r++)
                {
                    if (routes[r].TravelMinutesInGame <= 0 || string.IsNullOrWhiteSpace(routes[r].RouteNameZh))
                    {
                        metaErrors.Add("Invalid WorldAtlas route: " + routes[r].RouteId);
                    }
                }

                string repKey = RpgWorld500xRegionalReputationService.ReputationKey(spec.MapId);
                if (!RpgWorld500xEnrichedContentAuditor.IsSafeWorldKey(repKey))
                {
                    metaErrors.Add("Unsafe reputation key: " + repKey);
                }

                if (string.IsNullOrWhiteSpace(RpgWorld500xRegionalReputationService.GetFactionNameZh(spec.MapId)) ||
                    string.IsNullOrWhiteSpace(RpgWorld500xRegionalReputationService.GetSwornHonorTitleZh(spec.MapId)))
                {
                    metaErrors.Add("Missing faction name or honor title for map: " + spec.MapId);
                }
            }

            // Verify 4-region WorldAtlas portal graph strong connectivity via BFS.
            var visitedMaps = new HashSet<RpgWorldMapId> { RpgWorldMapId.Soundwell };
            var queue = new Queue<RpgWorldMapId>();
            queue.Enqueue(RpgWorldMapId.Soundwell);
            while (queue.Count > 0)
            {
                RpgWorldMapId cur = queue.Dequeue();
                List<RpgWorld500xRegionRouteDef> edges = RpgWorld500xWorldAtlasPortalService.GetConnectedRegions(cur);
                for (int i = 0; i < edges.Count; i++)
                {
                    if (visitedMaps.Add(edges[i].ToMap))
                    {
                        queue.Enqueue(edges[i].ToMap);
                    }
                }
            }

            if (visitedMaps.Count != specs.Count)
            {
                metaErrors.Add("WorldAtlas portal graph is not strongly connected (" +
                               visitedMaps.Count + "/" + specs.Count + " reachable).");
            }

            // Verify multi-slot save keys.
            IReadOnlyList<RpgWorld500xSaveSlotId> saveSlots = RpgWorld500xSaveSlotManager.AllSlots;
            var seenSaveKeys = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < saveSlots.Count; i++)
            {
                string key = RpgWorld500xSaveSlotManager.GetStorageKey(saveSlots[i]);
                if (!RpgWorld500xEnrichedContentAuditor.IsSafeWorldKey(key) || !seenSaveKeys.Add(key))
                {
                    metaErrors.Add("Invalid or duplicate save slot key: " + key);
                }
            }

            // Verify cross-map expedition milestones.
            IReadOnlyList<RpgWorld500xExpeditionMilestoneDef> milestones =
                RpgWorld500xExpeditionMilestoneService.AllMilestones;
            var seenMilestoneIds = new HashSet<string>(StringComparer.Ordinal);
            var seenMilestoneKeys = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < milestones.Count; i++)
            {
                RpgWorld500xExpeditionMilestoneDef m = milestones[i];
                if (string.IsNullOrWhiteSpace(m.Id) || !seenMilestoneIds.Add(m.Id))
                {
                    metaErrors.Add("Invalid or duplicate milestone id: " + m.Id);
                }

                if (!RpgWorld500xEnrichedContentAuditor.IsSafeWorldKey(m.MakinomKey) ||
                    !seenMilestoneKeys.Add(m.MakinomKey))
                {
                    metaErrors.Add("Invalid or duplicate milestone key: " + m.MakinomKey);
                }

                if (m.TargetCount <= 0 || m.SaltSilverReward <= 0 || string.IsNullOrWhiteSpace(m.TitleZh))
                {
                    metaErrors.Add("Invalid milestone target/reward/title: " + m.Id);
                }
            }

            int totalCampSupplies = RpgWorld500xBountyAndSupplyCatalog.GetCampSupplies().Count;

            var snapshot = new RpgWorld500xStudioHealthSnapshot
            {
                TotalTiles = totalTiles,
                TotalChunks = totalChunks,
                TotalLandmarks = contentAudit.TotalLandmarks,
                TotalCaches = contentAudit.TotalCaches,
                TotalShortcuts = contentAudit.TotalShortcuts,
                TotalFieldNpcs = contentAudit.TotalFieldNpcs,
                TotalStorySpots = contentAudit.TotalStorySpots,
                TotalAmbientZones = contentAudit.TotalAmbientZones,
                TotalDistricts = totalDistricts,
                TotalBounties = totalBounties,
                TotalCampSupplies = totalCampSupplies,
                TotalThreatProfiles = totalThreatProfiles,
                TotalRegionRoutes = totalRegionRoutes,
                TotalSaveSlots = saveSlots.Count,
                TotalExpeditionMilestones = milestones.Count,
                SpatialIssueCount = spatialIssues.Count,
                ContentAuditErrorCount = contentAudit.Errors.Count,
                MetaSystemAuditErrorCount = metaErrors.Count
            };

            var sb = new StringBuilder(640);
            sb.AppendLine("=== RPG 500x World Studio Maintenance Report ===");
            sb.AppendLine("Status: " + (snapshot.IsHealthy ? "HEALTHY" : "ISSUES_FOUND"));
            sb.AppendLine("Scale: " + snapshot.TotalTiles + " tiles / " + snapshot.TotalChunks + " chunks (4 maps)");
            sb.AppendLine("World POIs: " + snapshot.TotalLandmarks + " landmarks, " +
                          snapshot.TotalCaches + " caches, " +
                          snapshot.TotalShortcuts + " shortcuts, " +
                          snapshot.TotalFieldNpcs + " field NPCs, " +
                          snapshot.TotalStorySpots + " story spots, " +
                          snapshot.TotalAmbientZones + " ambient zones");
            sb.AppendLine("Systems: " + snapshot.TotalDistricts + " districts, " +
                          snapshot.TotalBounties + " bounties, " +
                          snapshot.TotalCampSupplies + " camp supplies, " +
                          snapshot.TotalThreatProfiles + " threat profiles");
            sb.AppendLine("Meta Systems: " + snapshot.TotalRegionRoutes + " region routes, " +
                          snapshot.TotalSaveSlots + " save slots, " +
                          snapshot.TotalExpeditionMilestones + " expedition milestones");
            sb.AppendLine("Audit Errors: spatial=" + snapshot.SpatialIssueCount +
                          ", content=" + snapshot.ContentAuditErrorCount +
                          ", meta=" + snapshot.MetaSystemAuditErrorCount);

            for (int i = 0; i < spatialIssues.Count; i++)
            {
                sb.AppendLine("  [SPATIAL] " + spatialIssues[i]);
            }

            for (int i = 0; i < contentAudit.Errors.Count; i++)
            {
                sb.AppendLine("  [CONTENT] " + contentAudit.Errors[i]);
            }

            for (int i = 0; i < metaErrors.Count; i++)
            {
                sb.AppendLine("  [META] " + metaErrors[i]);
            }

            snapshot.FormattedReport = sb.ToString();
            return snapshot;
        }

#if UNITY_EDITOR
        [UnityEditor.MenuItem("Tools/RPG 500x World/Run Full Map Maintenance Audit")]
        public static void RunMenuAudit()
        {
            RpgWorld500xStudioHealthSnapshot report = Generate();
            if (report.IsHealthy)
            {
                Debug.Log(report.FormattedReport);
            }
            else
            {
                Debug.LogError(report.FormattedReport);
            }
        }
#endif
    }
}
