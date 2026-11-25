using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Rpg.Soundwell
{
    public readonly struct RpgWorld500xPlayabilityGateReport
    {
        public RpgWorld500xPlayabilityGateReport(
            bool passed,
            int totalChecks,
            int passedChecks,
            IReadOnlyList<string> issues,
            string formattedReportZh)
        {
            Passed = passed;
            TotalChecks = totalChecks;
            PassedChecks = passedChecks;
            Issues = issues ?? Array.Empty<string>();
            FormattedReportZh = formattedReportZh ?? string.Empty;
        }

        public bool Passed { get; }
        public int TotalChecks { get; }
        public int PassedChecks { get; }
        public IReadOnlyList<string> Issues { get; }
        public string FormattedReportZh { get; }
    }

    /// <summary>
    /// Automated studio maintenance &amp; playability gate auditing the full 500x
    /// four-region expansion stack (World Gazetteer totals, DangerZone tactical
    /// profiles &amp; Lock-Break reward keys, Camp Supply safety &amp; timed buff
    /// evaluation, Weather Travel Advisory risk matrix, Expedition Readiness
    /// scoring, and Campfire Journal Digest synthesis across all 4 playable maps).
    /// </summary>
    public static class RpgWorld500xPlayabilityMaintenanceGate
    {
        public static RpgWorld500xPlayabilityGateReport RunFullAudit(Sprite[] overrideTileSlices = null)
        {
            var issues = new List<string>();
            int totalChecks = 0;
            int passedChecks = 0;

            void Check(bool condition, string failureMessage)
            {
                totalChecks++;
                if (condition)
                {
                    passedChecks++;
                }
                else
                {
                    issues.Add(failureMessage);
                }
            }

            // 1. Cross-Map Gazetteer & World Catalog Integrity
            List<RpgWorld500xRegionGazetteerCard> cards = RpgWorld500xWorldGazetteerService.BuildWorldCards();
            Check(cards.Count == 4, "Expected 4 region gazetteer cards, found " + cards.Count);

            int totalCaches = 0;
            int totalShortcuts = 0;
            int totalBounties = 0;
            int totalAmbients = 0;
            for (int i = 0; i < cards.Count; i++)
            {
                RpgWorld500xRegionGazetteerCard c = cards[i];
                totalCaches += c.TotalCaches;
                totalShortcuts += c.TotalShortcuts;
                totalBounties += c.TotalBounties;
                totalAmbients += c.AmbientZoneCount;
                Check(!string.IsNullOrWhiteSpace(c.FactionNameZh), "Missing FactionNameZh for " + c.MapId);
                Check(c.ConnectedRouteCount > 0, "Region has zero connected atlas routes: " + c.MapId);
            }

            Check(totalCaches == 42, "Expected 42 total hidden caches, found " + totalCaches);
            Check(totalShortcuts == 16, "Expected 16 total unlockable shortcuts, found " + totalShortcuts);
            Check(totalBounties == 12, "Expected 12 total regional bounties, found " + totalBounties);
            Check(totalAmbients == 12, "Expected 12 total ambient zones, found " + totalAmbients);

            // 2. Camp Supply Catalog & Field Supply Use Safety
            IReadOnlyList<RpgWorld500xCampSupplyItemDef> supplies = RpgWorld500xBountyAndSupplyCatalog.GetCampSupplies();
            Check(supplies.Count == 6, "Expected 6 camp supplies, found " + supplies.Count);
            var supplyStore = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < supplies.Count; i++)
            {
                RpgWorld500xCampSupplyItemDef item = supplies[i];
                Check(RpgWorld500xEnrichedContentAuditor.IsSafeWorldKey(item.OwnedCountKey),
                    "Unsafe supply OwnedCountKey: " + item.OwnedCountKey);
                Check(item.SaltSilverCost > 0 && item.MaxStock > 0,
                    "Invalid supply cost/stock for " + item.Id);
                supplyStore[item.OwnedCountKey] = 1;
            }

            var supplyUse = new RpgWorld500xFieldSupplyUseService(
                k => supplyStore.TryGetValue(k, out int v) ? v : 0,
                (k, v) => supplyStore[k] = v);
            RpgWorld500xFieldSupplyUseResult lampRes = supplyUse.TryConsumeSupply("lamp_oil", 5f);
            Check(lampRes.Succeeded && supplyUse.EvaluateActiveModifiers(10f).ActiveBuffCount == 1,
                "FieldSupplyUseService failed to activate lamp_oil timed buff.");

            // 3. Per-Map Live Bootstrapper Audit (DangerZone Brief, Readiness, Weather Advisory, Campfire Digest)
            Sprite[] slices = overrideTileSlices ?? new Sprite[] { null };
            IReadOnlyList<RpgExpandedMapSpec> specs = RpgExpandedWorldScale.AllSpecs;
            for (int s = 0; s < specs.Count; s++)
            {
                RpgWorldMapId mapId = specs[s].MapId;
                using (var boot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: mapId,
                    overrideTileSlices: slices))
                {
                    RpgWorld500xRegionThreatBrief brief =
                        RpgWorld500xDangerZoneTacticalBriefService.BuildRegionBrief(boot, elapsedSeconds: 5f);
                    Check(brief.TotalDangerZones > 0, "Map has zero DangerZones: " + mapId);
                    for (int e = 0; e < brief.Entries.Count; e++)
                    {
                        RpgWorld500xDangerZoneTacticalEntry entry = brief.Entries[e];
                        string clearKey = RpgWorld500xDangerVictoryRewardService.FirstClearKey(entry.MarkerId);
                        Check(RpgWorld500xEnrichedContentAuditor.IsSafeWorldKey(clearKey),
                            "Unsafe DangerZone clear key: " + clearKey);
                        Check(entry.EstimatedLockBreakSilver > entry.EstimatedStandardSilver,
                            "Lock-Break reward did not exceed standard reward for " + entry.MarkerId);
                    }

                    boot.Driver.Facade.Atmosphere.SetWeather(mapId, RpgWorld500xWeatherKind.Clear);
                    RpgWorld500xExpeditionDigest digest =
                        RpgWorld500xCampfireJournalDigestService.BuildDigest(boot, elapsedSeconds: 10f, supplyUseService: supplyUse);
                    Check(digest.MapId == mapId && !string.IsNullOrWhiteSpace(digest.FormattedDigestZh),
                        "Campfire digest failed on " + mapId);

                    boot.Driver.Facade.Atmosphere.SetWeather(mapId, RpgWorld500xWeatherKind.GaleStorm);
                    RpgWorld500xExpeditionReadinessReport stormReadiness =
                        RpgWorld500xExpeditionReadinessService.EvaluateReadiness(boot, elapsedSeconds: 10f, supplyUseService: supplyUse);
                    Check(stormReadiness.TravelRiskLevel == RpgWorld500xTravelRiskLevel.Hazardous &&
                          stormReadiness.Score >= 0 && stormReadiness.Score <= 100 &&
                          !string.IsNullOrWhiteSpace(stormReadiness.ChecklistSummaryZh),
                        "Expedition readiness failed under GaleStorm on " + mapId);
                }
            }

            bool passed = issues.Count == 0;
            var sb = new StringBuilder(192);
            sb.Append("【500x 可玩性巡检门禁】")
              .Append(passed ? "通过" : "未通过")
              .Append("（").Append(passedChecks).Append('/').Append(totalChecks).Append(" 项通过）");
            if (!passed)
            {
                sb.Append("｜问题数：").Append(issues.Count);
            }

            return new RpgWorld500xPlayabilityGateReport(
                passed,
                totalChecks,
                passedChecks,
                issues,
                sb.ToString());
        }
    }
}
