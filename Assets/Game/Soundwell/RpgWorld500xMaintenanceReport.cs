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
        public int SpatialIssueCount { get; set; }
        public int ContentAuditErrorCount { get; set; }
        public string FormattedReport { get; set; } = string.Empty;

        public bool IsHealthy => SpatialIssueCount == 0 && ContentAuditErrorCount == 0;
    }

    /// <summary>
    /// Studio-grade one-click maintenance and health report generator for the entire
    /// 500x-area RPG world. Unifies spatial/memory integrity checks, cross-system
    /// enriched content auditing, district/bounty/supply/bestiary inventory totals,
    /// and a Unity Editor Tools menu entry.
    /// </summary>
    public static class RpgWorld500xMaintenanceReport
    {
        public static RpgWorld500xStudioHealthSnapshot Generate()
        {
            List<RpgMapValidationIssue> spatialIssues = RpgWorld500xMapIntegrityValidator.ValidateAllFourMaps();
            RpgWorld500xContentAuditReport contentAudit = RpgWorld500xEnrichedContentAuditor.AuditAllMaps();

            long totalTiles = 0;
            int totalChunks = 0;
            int totalDistricts = 0;
            int totalBounties = 0;
            int totalThreatProfiles = 0;

            foreach (RpgExpandedMapSpec spec in RpgExpandedWorldScale.AllSpecs)
            {
                totalTiles += (long)spec.ExpandedWidth * spec.ExpandedHeight;
                totalChunks += (spec.ExpandedWidth / RpgLargeMapChunkRenderer.ChunkSizeTiles) *
                               (spec.ExpandedHeight / RpgLargeMapChunkRenderer.ChunkSizeTiles);
                totalDistricts += RpgWorld500xDistrictCatalog.GetDistricts(spec.MapId).Count;
                totalBounties += RpgWorld500xBountyAndSupplyCatalog.GetBounties(spec.MapId).Count;
                totalThreatProfiles += RpgWorld500xBestiaryCatalog.GetProfiles(spec.MapId).Count;
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
                SpatialIssueCount = spatialIssues.Count,
                ContentAuditErrorCount = contentAudit.Errors.Count
            };

            var sb = new StringBuilder(512);
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
            sb.AppendLine("Audit Errors: spatial=" + snapshot.SpatialIssueCount +
                          ", content=" + snapshot.ContentAuditErrorCount);

            for (int i = 0; i < spatialIssues.Count; i++)
            {
                sb.AppendLine("  [SPATIAL] " + spatialIssues[i]);
            }

            for (int i = 0; i < contentAudit.Errors.Count; i++)
            {
                sb.AppendLine("  [CONTENT] " + contentAudit.Errors[i]);
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
