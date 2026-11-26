using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Rpg.Soundwell
{
    public readonly struct RpgWorld500xDangerZoneTacticalEntry
    {
        public RpgWorld500xDangerZoneTacticalEntry(
            string markerId,
            string enemyNameZh,
            RpgWorld500xThreatTier tier,
            Vector2Int tile,
            int distanceTiles,
            string direction8Zh,
            bool isCleared,
            int effectiveThreatLevel,
            string recommendedSupplyId,
            int estimatedStandardSilver,
            int estimatedLockBreakSilver,
            string summaryLineZh)
        {
            MarkerId = markerId ?? string.Empty;
            EnemyNameZh = enemyNameZh ?? string.Empty;
            Tier = tier;
            Tile = tile;
            DistanceTiles = distanceTiles;
            Direction8Zh = direction8Zh ?? string.Empty;
            IsCleared = isCleared;
            EffectiveThreatLevel = effectiveThreatLevel;
            RecommendedSupplyId = recommendedSupplyId ?? string.Empty;
            EstimatedStandardSilver = estimatedStandardSilver;
            EstimatedLockBreakSilver = estimatedLockBreakSilver;
            SummaryLineZh = summaryLineZh ?? string.Empty;
        }

        public string MarkerId { get; }
        public string EnemyNameZh { get; }
        public RpgWorld500xThreatTier Tier { get; }
        public Vector2Int Tile { get; }
        public int DistanceTiles { get; }
        public string Direction8Zh { get; }
        public bool IsCleared { get; }
        public int EffectiveThreatLevel { get; }
        public string RecommendedSupplyId { get; }
        public int EstimatedStandardSilver { get; }
        public int EstimatedLockBreakSilver { get; }
        public string SummaryLineZh { get; }
    }

    public readonly struct RpgWorld500xRegionThreatBrief
    {
        public RpgWorld500xRegionThreatBrief(
            RpgWorldMapId mapId,
            string mapDisplayNameZh,
            RpgWorld500xTravelRiskLevel travelRiskLevel,
            int totalDangerZones,
            int clearedDangerZones,
            int remainingDangerZones,
            bool hasNearestUncleared,
            RpgWorld500xDangerZoneTacticalEntry nearestUnclearedZone,
            IReadOnlyList<RpgWorld500xDangerZoneTacticalEntry> entries,
            string formattedBriefZh)
        {
            MapId = mapId;
            MapDisplayNameZh = mapDisplayNameZh ?? string.Empty;
            TravelRiskLevel = travelRiskLevel;
            TotalDangerZones = totalDangerZones;
            ClearedDangerZones = clearedDangerZones;
            RemainingDangerZones = remainingDangerZones;
            HasNearestUncleared = hasNearestUncleared;
            NearestUnclearedZone = nearestUnclearedZone;
            Entries = entries ?? Array.Empty<RpgWorld500xDangerZoneTacticalEntry>();
            FormattedBriefZh = formattedBriefZh ?? string.Empty;
        }

        public RpgWorldMapId MapId { get; }
        public string MapDisplayNameZh { get; }
        public RpgWorld500xTravelRiskLevel TravelRiskLevel { get; }
        public int TotalDangerZones { get; }
        public int ClearedDangerZones { get; }
        public int RemainingDangerZones { get; }
        public bool HasNearestUncleared { get; }
        public RpgWorld500xDangerZoneTacticalEntry NearestUnclearedZone { get; }
        public IReadOnlyList<RpgWorld500xDangerZoneTacticalEntry> Entries { get; }
        public string FormattedBriefZh { get; }
    }

    public readonly struct RpgWorld500xDangerClearancePayoutSummary
    {
        public RpgWorld500xDangerClearancePayoutSummary(
            RpgWorldMapId mapId,
            int remainingZones,
            int remainingStandardSilver,
            int remainingLockBreakSilver,
            int lockBreakBonusSilver,
            int peakEffectiveThreatLevel,
            string topRecommendedSupplyId,
            string summaryZh)
        {
            MapId = mapId;
            RemainingZones = remainingZones;
            RemainingStandardSilver = remainingStandardSilver;
            RemainingLockBreakSilver = remainingLockBreakSilver;
            LockBreakBonusSilver = lockBreakBonusSilver;
            PeakEffectiveThreatLevel = peakEffectiveThreatLevel;
            TopRecommendedSupplyId = topRecommendedSupplyId ?? string.Empty;
            SummaryZh = summaryZh ?? string.Empty;
        }

        public RpgWorldMapId MapId { get; }
        public int RemainingZones { get; }
        public int RemainingStandardSilver { get; }
        public int RemainingLockBreakSilver { get; }
        public int LockBreakBonusSilver { get; }
        public int PeakEffectiveThreatLevel { get; }
        public string TopRecommendedSupplyId { get; }
        public string SummaryZh { get; }
    }

    /// <summary>
    /// Builds a deterministic DangerZone tactical briefing for the active 500x map,
    /// combining Bestiary threat profiles, live weather risk scaling, 8-direction
    /// bearing/distance from the player, persisted first-clear flags, and expected
    /// standard vs. Lock-Break Salt-Silver payouts.
    /// </summary>
    public static class RpgWorld500xDangerZoneTacticalBriefService
    {
        public static RpgWorld500xRegionThreatBrief BuildRegionBrief(
            RpgWorld500xSceneBootstrapper bootstrapper,
            float elapsedSeconds = 0f,
            Func<string, int> overrideGetInt = null)
        {
            if (bootstrapper == null || bootstrapper.IsDisposed)
            {
                throw new ArgumentNullException(nameof(bootstrapper));
            }

            RpgWorld500xGrandRuntimeFacade facade = bootstrapper.Driver.Facade;
            RpgWorldMapId mapId = facade.CurrentMapId;
            RpgWorld500xMapLayout layout = facade.Session.Host.Layout;
            string mapNameZh = layout.Spec.DisplayNameZh;
            Vector2 playerPos = facade.Session.Host.PlayerPosition;

            RpgWorld500xAtmosphereSnapshot atmo = facade.Atmosphere.GetSnapshot(mapId);
            RpgWorld500xTravelAdvisoryReport advisory =
                RpgWorld500xWeatherTravelAdvisoryService.BuildAdvisory(atmo, elapsedSeconds);

            IReadOnlyDictionary<string, int> store = bootstrapper.Store;
            Func<string, int> read = overrideGetInt ??
                (k => (store != null && store.TryGetValue(k, out int v)) ? v : 0);

            var entries = new List<RpgWorld500xDangerZoneTacticalEntry>();
            int clearedCount = 0;

            for (int i = 0; i < layout.Markers.Count; i++)
            {
                RpgMapLandmarkMarker marker = layout.Markers[i];
                if (marker.Category != RpgMapMarkerCategory.DangerZone)
                {
                    continue;
                }

                RpgWorld500xThreatProfileDef profile =
                    RpgWorld500xBestiaryCatalog.ResolveProfileForMarker(mapId, marker.Id);
                bool isCleared = read(RpgWorld500xDangerVictoryRewardService.FirstClearKey(marker.Id)) == 1;
                if (isCleared)
                {
                    clearedCount++;
                }

                int dist = Mathf.CeilToInt(Vector2.Distance(
                    playerPos,
                    new Vector2(marker.Tile.x + 0.5f, marker.Tile.y + 0.5f)));
                string dirZh = RpgWorld500xFieldReconScannerService.ResolveDirection8Zh(playerPos, marker.Tile);
                int effectiveLevel = ComputeEffectiveThreatLevel(profile.Tier, advisory.RiskLevel);

                RpgWorld500xDangerVictoryReward stdReward =
                    RpgWorld500xDangerVictoryRewardService.ComputeReward(mapId, marker.Id, lockBreakAchieved: false, isFirstClear: !isCleared);
                RpgWorld500xDangerVictoryReward lbReward =
                    RpgWorld500xDangerVictoryRewardService.ComputeReward(mapId, marker.Id, lockBreakAchieved: true, isFirstClear: !isCleared);

                string statusTag = isCleared ? "[已首破] " : "[未清剿] ";
                string line = statusTag + profile.EnemyNameZh +
                              "｜威胁 Lv." + effectiveLevel +
                              "｜方位：" + dirZh + " " + dist + " 格" +
                              "｜预计盐银 " + stdReward.SaltSilverReward + "（破锁 " + lbReward.SaltSilverReward + "）";

                entries.Add(new RpgWorld500xDangerZoneTacticalEntry(
                    marker.Id,
                    profile.EnemyNameZh,
                    profile.Tier,
                    marker.Tile,
                    dist,
                    dirZh,
                    isCleared,
                    effectiveLevel,
                    profile.RecommendedSupplyId,
                    stdReward.SaltSilverReward,
                    lbReward.SaltSilverReward,
                    line));
            }

            entries.Sort((a, b) =>
            {
                int clearedCmp = a.IsCleared.CompareTo(b.IsCleared);
                if (clearedCmp != 0) return clearedCmp;
                int distCmp = a.DistanceTiles.CompareTo(b.DistanceTiles);
                if (distCmp != 0) return distCmp;
                return StringComparer.Ordinal.Compare(a.MarkerId, b.MarkerId);
            });

            bool hasNearest = false;
            RpgWorld500xDangerZoneTacticalEntry nearest = default;
            for (int i = 0; i < entries.Count; i++)
            {
                if (!entries[i].IsCleared)
                {
                    hasNearest = true;
                    nearest = entries[i];
                    break;
                }
            }

            int total = entries.Count;
            int remaining = Math.Max(0, total - clearedCount);

            var sb = new StringBuilder(256);
            sb.AppendLine("【危险区战备简报】" + mapNameZh + "（已清剿 " + clearedCount + "/" + total + "｜" + advisory.RiskLabelZh + "）");
            if (hasNearest)
            {
                sb.Append("最近未清剿目标：").Append(nearest.SummaryLineZh);
            }
            else
            {
                sb.Append("本境所有危险区均已完成首破清剿。");
            }

            return new RpgWorld500xRegionThreatBrief(
                mapId,
                mapNameZh,
                advisory.RiskLevel,
                total,
                clearedCount,
                remaining,
                hasNearest,
                nearest,
                entries,
                sb.ToString());
        }

        /// <summary>
        /// Aggregates remaining standard vs. Lock-Break Salt-Silver payouts, peak
        /// effective threat level, and top recommended camp supply across all uncleared
        /// DangerZones in a region tactical brief.
        /// </summary>
        public static RpgWorld500xDangerClearancePayoutSummary SummarizeRemainingPayouts(
            RpgWorld500xRegionThreatBrief brief)
        {
            IReadOnlyList<RpgWorld500xDangerZoneTacticalEntry> entries =
                brief.Entries ?? Array.Empty<RpgWorld500xDangerZoneTacticalEntry>();

            int remainingZones = 0;
            int remainingStd = 0;
            int remainingLockBreak = 0;
            int peakThreat = 0;
            var supplyCounts = new Dictionary<string, int>(StringComparer.Ordinal);

            for (int i = 0; i < entries.Count; i++)
            {
                RpgWorld500xDangerZoneTacticalEntry entry = entries[i];
                if (entry.IsCleared)
                {
                    continue;
                }

                remainingZones++;
                remainingStd += Math.Max(0, entry.EstimatedStandardSilver);
                remainingLockBreak += Math.Max(0, entry.EstimatedLockBreakSilver);
                if (entry.EffectiveThreatLevel > peakThreat)
                {
                    peakThreat = entry.EffectiveThreatLevel;
                }

                if (!string.IsNullOrEmpty(entry.RecommendedSupplyId))
                {
                    supplyCounts.TryGetValue(entry.RecommendedSupplyId, out int c);
                    supplyCounts[entry.RecommendedSupplyId] = c + 1;
                }
            }

            string topSupply = string.Empty;
            int topSupplyCount = 0;
            foreach (KeyValuePair<string, int> kv in supplyCounts)
            {
                if (kv.Value > topSupplyCount ||
                    (kv.Value == topSupplyCount && StringComparer.Ordinal.Compare(kv.Key, topSupply) < 0))
                {
                    topSupply = kv.Key;
                    topSupplyCount = kv.Value;
                }
            }

            int lockBreakBonus = Math.Max(0, remainingLockBreak - remainingStd);
            string mapName = string.IsNullOrEmpty(brief.MapDisplayNameZh)
                ? brief.MapId.ToString()
                : brief.MapDisplayNameZh;
            string summaryZh = remainingZones == 0
                ? "【清剿收益预估】" + mapName + "：本境危险区已全部首破，暂无待清剿赏金"
                : "【清剿收益预估】" + mapName + "：待清剿 " + remainingZones + " 处 · 最高威胁 Lv." + peakThreat +
                  " · 预计盐银 " + remainingStd + "（破锁 " + remainingLockBreak + "，额外 +" + lockBreakBonus + "）";

            return new RpgWorld500xDangerClearancePayoutSummary(
                brief.MapId,
                remainingZones,
                remainingStd,
                remainingLockBreak,
                lockBreakBonus,
                peakThreat,
                topSupply,
                summaryZh);
        }

        /// <summary>
        /// Filters entries from a region threat brief by clearance status, minimum
        /// effective threat level, and optional threat tier while preserving distance order.
        /// </summary>
        public static List<RpgWorld500xDangerZoneTacticalEntry> FilterEntries(
            RpgWorld500xRegionThreatBrief brief,
            bool unclearedOnly = false,
            int minEffectiveThreatLevel = 1,
            RpgWorld500xThreatTier? tierFilter = null)
        {
            var filtered = new List<RpgWorld500xDangerZoneTacticalEntry>();
            IReadOnlyList<RpgWorld500xDangerZoneTacticalEntry> entries =
                brief.Entries ?? Array.Empty<RpgWorld500xDangerZoneTacticalEntry>();

            for (int i = 0; i < entries.Count; i++)
            {
                RpgWorld500xDangerZoneTacticalEntry entry = entries[i];
                if (unclearedOnly && entry.IsCleared)
                {
                    continue;
                }

                if (entry.EffectiveThreatLevel < minEffectiveThreatLevel)
                {
                    continue;
                }

                if (tierFilter.HasValue && entry.Tier != tierFilter.Value)
                {
                    continue;
                }

                filtered.Add(entry);
            }

            return filtered;
        }

        public static int ComputeEffectiveThreatLevel(
            RpgWorld500xThreatTier tier,
            RpgWorld500xTravelRiskLevel travelRisk)
        {
            int baseLevel;
            switch (tier)
            {
                case RpgWorld500xThreatTier.HazardBoss:
                    baseLevel = 4;
                    break;
                case RpgWorld500xThreatTier.ElitePatrol:
                    baseLevel = 3;
                    break;
                case RpgWorld500xThreatTier.Skirmisher:
                default:
                    baseLevel = 2;
                    break;
            }

            if (travelRisk == RpgWorld500xTravelRiskLevel.Hazardous)
            {
                baseLevel += 1;
            }
            else if (travelRisk == RpgWorld500xTravelRiskLevel.StealthAdvantage)
            {
                baseLevel -= 1;
            }

            return Mathf.Clamp(baseLevel, 1, 5);
        }
    }
}