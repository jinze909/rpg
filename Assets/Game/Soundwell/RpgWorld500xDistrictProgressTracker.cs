using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rpg.Soundwell
{
    public readonly struct RpgWorld500xDistrictDef
    {
        public RpgWorld500xDistrictDef(
            string id,
            RpgWorldMapId mapId,
            string displayNameZh,
            RectInt boundsTiles)
        {
            Id = id;
            MapId = mapId;
            DisplayNameZh = displayNameZh;
            BoundsTiles = boundsTiles;
        }

        public string Id { get; }
        public RpgWorldMapId MapId { get; }
        public string DisplayNameZh { get; }
        public RectInt BoundsTiles { get; }
        public bool ContainsTile(Vector2Int tile) => BoundsTiles.Contains(tile);
    }

    public readonly struct RpgWorld500xCartographyMilestone
    {
        public RpgWorld500xCartographyMilestone(
            RpgWorldMapId mapId,
            int thresholdPercent,
            int saltSilverReward,
            string titleZh)
        {
            MapId = mapId;
            ThresholdPercent = thresholdPercent;
            SaltSilverReward = saltSilverReward;
            TitleZh = titleZh;
        }

        public RpgWorldMapId MapId { get; }
        public int ThresholdPercent { get; }
        public int SaltSilverReward { get; }
        public string TitleZh { get; }
        public string MakinomKey =>
            "rpg.return.world.cartography." + MapId.ToString().ToLowerInvariant() + ".tier" + ThresholdPercent;
    }

    public readonly struct RpgWorld500xCartographyProgressPreview
    {
        public RpgWorld500xCartographyProgressPreview(
            RpgWorldMapId mapId,
            int completionPercent,
            int claimedCount,
            int totalMilestones,
            int claimableCount,
            int claimableSaltSilver,
            int remainingSaltSilver,
            bool hasNextThreshold,
            int nextThresholdPercent,
            string summaryZh)
        {
            MapId = mapId;
            CompletionPercent = completionPercent;
            ClaimedCount = claimedCount;
            TotalMilestones = totalMilestones;
            ClaimableCount = claimableCount;
            ClaimableSaltSilver = claimableSaltSilver;
            RemainingSaltSilver = remainingSaltSilver;
            HasNextThreshold = hasNextThreshold;
            NextThresholdPercent = nextThresholdPercent;
            SummaryZh = summaryZh ?? string.Empty;
        }

        public RpgWorldMapId MapId { get; }
        public int CompletionPercent { get; }
        public int ClaimedCount { get; }
        public int TotalMilestones { get; }
        public int ClaimableCount { get; }
        public int ClaimableSaltSilver { get; }
        public int RemainingSaltSilver { get; }
        public bool HasNextThreshold { get; }
        public int NextThresholdPercent { get; }
        public string SummaryZh { get; }
    }

    public readonly struct RpgWorld500xMapCompletionSummary
    {
        public RpgWorld500xMapCompletionSummary(
            RpgWorldMapId mapId,
            int visitedChunks,
            int totalChunks,
            int discoveredMarkers,
            int totalMarkers,
            int lootedCaches,
            int totalCaches,
            int unlockedShortcuts,
            int totalShortcuts,
            int inspectedStorySpots,
            int totalStorySpots)
        {
            MapId = mapId;
            VisitedChunks = visitedChunks;
            TotalChunks = totalChunks;
            DiscoveredMarkers = discoveredMarkers;
            TotalMarkers = totalMarkers;
            LootedCaches = lootedCaches;
            TotalCaches = totalCaches;
            UnlockedShortcuts = unlockedShortcuts;
            TotalShortcuts = totalShortcuts;
            InspectedStorySpots = inspectedStorySpots;
            TotalStorySpots = totalStorySpots;
        }

        public RpgWorldMapId MapId { get; }
        public int VisitedChunks { get; }
        public int TotalChunks { get; }
        public int DiscoveredMarkers { get; }
        public int TotalMarkers { get; }
        public int LootedCaches { get; }
        public int TotalCaches { get; }
        public int UnlockedShortcuts { get; }
        public int TotalShortcuts { get; }
        public int InspectedStorySpots { get; }
        public int TotalStorySpots { get; }

        public float ChunkCoverageRatio => TotalChunks > 0 ? Mathf.Clamp01((float)VisitedChunks / TotalChunks) : 0f;

        public float PoiCompletionRatio
        {
            get
            {
                int total = TotalMarkers + TotalCaches + TotalShortcuts + TotalStorySpots;
                if (total <= 0)
                {
                    return 0f;
                }

                int completed = DiscoveredMarkers + LootedCaches + UnlockedShortcuts + InspectedStorySpots;
                return Mathf.Clamp01((float)completed / total);
            }
        }

        public int OverallCompletionPercent => Mathf.Clamp(
            Mathf.RoundToInt(((PoiCompletionRatio * 0.75f) + (ChunkCoverageRatio * 0.25f)) * 100f),
            0,
            100);
    }

    /// <summary>
    /// Canonical 15-district partition and cartography milestone catalog across all
    /// four 500x-area maps (exact non-overlapping full-area tiling).
    /// </summary>
    public static class RpgWorld500xDistrictCatalog
    {
        private static readonly RpgWorld500xDistrictDef[] SoundwellDistricts =
        {
            new RpgWorld500xDistrictDef("sw_dist_nw", RpgWorldMapId.Soundwell, "北廊听音带", new RectInt(0, 672, 1120, 672)),
            new RpgWorld500xDistrictDef("sw_dist_ne", RpgWorldMapId.Soundwell, "东室铜铃区", new RectInt(1120, 672, 1024, 672)),
            new RpgWorld500xDistrictDef("sw_dist_sw", RpgWorldMapId.Soundwell, "西侧铁闸暗廊", new RectInt(0, 0, 1120, 672)),
            new RpgWorld500xDistrictDef("sw_dist_se", RpgWorldMapId.Soundwell, "主井厅与撤离井", new RectInt(1120, 0, 1024, 672))
        };

        private static readonly RpgWorld500xDistrictDef[] RedSaltHarborDistricts =
        {
            new RpgWorld500xDistrictDef("rsh_dist_sw", RpgWorldMapId.RedSaltHarbor, "外港泊位与税关", new RectInt(0, 0, 1568, 960)),
            new RpgWorld500xDistrictDef("rsh_dist_se", RpgWorldMapId.RedSaltHarbor, "干船坞与防波堤", new RectInt(1568, 0, 1568, 960)),
            new RpgWorld500xDistrictDef("rsh_dist_nw", RpgWorldMapId.RedSaltHarbor, "西货场与北口矿道", new RectInt(0, 960, 1568, 928)),
            new RpgWorld500xDistrictDef("rsh_dist_ne", RpgWorldMapId.RedSaltHarbor, "赤盐市集与伤病棚", new RectInt(1568, 960, 1568, 928))
        };

        private static readonly RpgWorld500xDistrictDef[] WhitewaterDistricts =
        {
            new RpgWorld500xDistrictDef("ww_dist_west", RpgWorldMapId.WhitewaterCrossing, "西岸营地与旧渡口", new RectInt(0, 0, 576, 992)),
            new RpgWorld500xDistrictDef("ww_dist_pontoon", RpgWorldMapId.WhitewaterCrossing, "江心浮桥战线", new RectInt(576, 0, 640, 992)),
            new RpgWorld500xDistrictDef("ww_dist_east", RpgWorldMapId.WhitewaterCrossing, "东岸哨塔与撤离山口", new RectInt(1216, 0, 576, 992))
        };

        private static readonly RpgWorld500xDistrictDef[] MineCampDistricts =
        {
            new RpgWorld500xDistrictDef("mc_dist_sw", RpgWorldMapId.MineCamp, "南门检疫与外围哨楼", new RectInt(0, 0, 1120, 704)),
            new RpgWorld500xDistrictDef("mc_dist_se", RpgWorldMapId.MineCamp, "盐煤堆场与物资账房", new RectInt(1120, 0, 1024, 704)),
            new RpgWorld500xDistrictDef("mc_dist_nw", RpgWorldMapId.MineCamp, "矿工宿舍与提升机区", new RectInt(0, 704, 1120, 736)),
            new RpgWorld500xDistrictDef("mc_dist_ne", RpgWorldMapId.MineCamp, "地下主泵房与水压廊", new RectInt(1120, 704, 1024, 736))
        };

        public static IReadOnlyList<RpgWorld500xDistrictDef> GetDistricts(RpgWorldMapId mapId)
        {
            switch (mapId)
            {
                case RpgWorldMapId.Soundwell: return SoundwellDistricts;
                case RpgWorldMapId.RedSaltHarbor: return RedSaltHarborDistricts;
                case RpgWorldMapId.WhitewaterCrossing: return WhitewaterDistricts;
                case RpgWorldMapId.MineCamp: return MineCampDistricts;
                default: throw new ArgumentOutOfRangeException(nameof(mapId), mapId, "Unknown map id.");
            }
        }

        public static bool TryResolveDistrictAtTile(RpgWorldMapId mapId, Vector2Int tile, out RpgWorld500xDistrictDef district)
        {
            IReadOnlyList<RpgWorld500xDistrictDef> list = GetDistricts(mapId);
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].ContainsTile(tile))
                {
                    district = list[i];
                    return true;
                }
            }

            district = default;
            return false;
        }

        public static RpgWorld500xCartographyMilestone[] GetMilestones(RpgWorldMapId mapId)
        {
            return new[]
            {
                new RpgWorld500xCartographyMilestone(mapId, 25, 45, "初绘堪舆（探索度 25%）"),
                new RpgWorld500xCartographyMilestone(mapId, 50, 90, "半境通明（探索度 50%）"),
                new RpgWorld500xCartographyMilestone(mapId, 100, 180, "全图踏遍（探索度 100%）")
            };
        }
    }

    /// <summary>
    /// Tracks chunk-level fog-of-war cartography, active district transitions,
    /// cross-system POI completion, per-district fog coverage, read-only cartography
    /// milestone previews, and idempotent regional cartography rewards.
    /// </summary>
    public sealed class RpgWorld500xDistrictProgressTracker
    {
        private readonly RpgWorldMapId _mapId;
        private readonly int _chunksWide;
        private readonly int _chunksHigh;
        private readonly Func<string, int> _getInt;
        private readonly Action<string, int> _setInt;
        private readonly Action<RpgWorld500xCartographyMilestone> _onMilestoneClaimed;
        private readonly HashSet<int> _visitedChunkIndices = new HashSet<int>();
        private readonly RpgWorld500xCartographyMilestone[] _milestones;
        private string _currentDistrictId = string.Empty;

        public RpgWorldMapId MapId => _mapId;
        public int TotalChunks => _chunksWide * _chunksHigh;
        public int VisitedChunkCount => _visitedChunkIndices.Count;
        public string CurrentDistrictId => _currentDistrictId;

        public RpgWorld500xDistrictProgressTracker(
            RpgExpandedMapSpec spec,
            Func<string, int> getInt = null,
            Action<string, int> setInt = null,
            Action<RpgWorld500xCartographyMilestone> onMilestoneClaimed = null)
        {
            _mapId = spec.MapId;
            _chunksWide = Math.Max(1, spec.ExpandedWidth / RpgLargeMapChunkRenderer.ChunkSizeTiles);
            _chunksHigh = Math.Max(1, spec.ExpandedHeight / RpgLargeMapChunkRenderer.ChunkSizeTiles);
            _getInt = getInt ?? (_ => 0);
            _setInt = setInt ?? ((_, __) => { });
            _onMilestoneClaimed = onMilestoneClaimed;
            _milestones = RpgWorld500xDistrictCatalog.GetMilestones(_mapId);
        }

        public bool RecordPlayerTile(Vector2 playerTile, int revealRadiusChunks, out RpgWorld500xDistrictDef enteredDistrict)
        {
            enteredDistrict = default;
            if (float.IsNaN(playerTile.x) || float.IsInfinity(playerTile.x) ||
                float.IsNaN(playerTile.y) || float.IsInfinity(playerTile.y))
            {
                return false;
            }

            var tile = new Vector2Int(Mathf.FloorToInt(playerTile.x), Mathf.FloorToInt(playerTile.y));
            int centerCx = Mathf.Clamp(tile.x / RpgLargeMapChunkRenderer.ChunkSizeTiles, 0, _chunksWide - 1);
            int centerCy = Mathf.Clamp(tile.y / RpgLargeMapChunkRenderer.ChunkSizeTiles, 0, _chunksHigh - 1);
            int radius = Math.Max(0, revealRadiusChunks);

            for (int dy = -radius; dy <= radius; dy++)
            {
                int cy = centerCy + dy;
                if (cy < 0 || cy >= _chunksHigh)
                {
                    continue;
                }

                for (int dx = -radius; dx <= radius; dx++)
                {
                    int cx = centerCx + dx;
                    if (cx < 0 || cx >= _chunksWide)
                    {
                        continue;
                    }

                    _visitedChunkIndices.Add((cy * _chunksWide) + cx);
                }
            }

            if (RpgWorld500xDistrictCatalog.TryResolveDistrictAtTile(_mapId, tile, out RpgWorld500xDistrictDef resolved))
            {
                bool changed = !string.Equals(_currentDistrictId, resolved.Id, StringComparison.Ordinal);
                _currentDistrictId = resolved.Id;
                enteredDistrict = resolved;
                return changed;
            }

            return false;
        }

        public bool IsChunkVisited(int chunkX, int chunkY)
        {
            if (chunkX < 0 || chunkX >= _chunksWide || chunkY < 0 || chunkY >= _chunksHigh)
            {
                return false;
            }

            return _visitedChunkIndices.Contains((chunkY * _chunksWide) + chunkX);
        }

        /// <summary>
        /// Computes the fraction [0, 1] of map chunks within the specified district
        /// that have been revealed in fog-of-war.
        /// </summary>
        public float ComputeDistrictChunkCoverageRatio(string districtId)
        {
            if (string.IsNullOrEmpty(districtId))
            {
                return 0f;
            }

            IReadOnlyList<RpgWorld500xDistrictDef> districts = RpgWorld500xDistrictCatalog.GetDistricts(_mapId);
            RpgWorld500xDistrictDef target = default;
            bool found = false;
            for (int i = 0; i < districts.Count; i++)
            {
                if (string.Equals(districts[i].Id, districtId, StringComparison.Ordinal))
                {
                    target = districts[i];
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                return 0f;
            }

            int chunkSize = Math.Max(1, RpgLargeMapChunkRenderer.ChunkSizeTiles);
            int minCx = Mathf.Clamp(target.BoundsTiles.xMin / chunkSize, 0, _chunksWide - 1);
            int maxCx = Mathf.Clamp((target.BoundsTiles.xMax - 1) / chunkSize, 0, _chunksWide - 1);
            int minCy = Mathf.Clamp(target.BoundsTiles.yMin / chunkSize, 0, _chunksHigh - 1);
            int maxCy = Mathf.Clamp((target.BoundsTiles.yMax - 1) / chunkSize, 0, _chunksHigh - 1);

            int total = 0;
            int visited = 0;
            for (int cy = minCy; cy <= maxCy; cy++)
            {
                for (int cx = minCx; cx <= maxCx; cx++)
                {
                    total++;
                    if (_visitedChunkIndices.Contains((cy * _chunksWide) + cx))
                    {
                        visited++;
                    }
                }
            }

            return total > 0 ? Mathf.Clamp01((float)visited / total) : 0f;
        }

        /// <summary>
        /// Builds a read-only preview of cartography milestone progress and claimable
        /// Salt-Silver for the given completion percent without mutating claim flags.
        /// </summary>
        public RpgWorld500xCartographyProgressPreview BuildCartographyPreview(int completionPercent)
        {
            int clamped = Mathf.Clamp(completionPercent, 0, 100);
            int claimedCount = 0;
            int claimableCount = 0;
            int claimableSilver = 0;
            int remainingSilver = 0;
            bool hasNext = false;
            int nextThreshold = 0;

            for (int i = 0; i < _milestones.Length; i++)
            {
                RpgWorld500xCartographyMilestone m = _milestones[i];
                if (_getInt(m.MakinomKey) == 1)
                {
                    claimedCount++;
                    continue;
                }

                remainingSilver += m.SaltSilverReward;
                if (clamped >= m.ThresholdPercent)
                {
                    claimableCount++;
                    claimableSilver += m.SaltSilverReward;
                }
                else if (!hasNext)
                {
                    hasNext = true;
                    nextThreshold = m.ThresholdPercent;
                }
            }

            string summaryZh;
            if (claimedCount >= _milestones.Length)
            {
                summaryZh = "【堪舆里程碑】已领 " + claimedCount + "/" + _milestones.Length + " · 本境堪舆里程碑已全部结清";
            }
            else if (claimableCount > 0)
            {
                summaryZh = "【堪舆里程碑】已领 " + claimedCount + "/" + _milestones.Length +
                            " · 可领 " + claimableCount + " 项（+" + claimableSilver + " 盐银）· 剩余总赏 " + remainingSilver + " 盐银";
            }
            else
            {
                summaryZh = "【堪舆里程碑】已领 " + claimedCount + "/" + _milestones.Length +
                            " · 下一阶 " + nextThreshold + "%（当前 " + clamped + "%）· 剩余总赏 " + remainingSilver + " 盐银";
            }

            return new RpgWorld500xCartographyProgressPreview(
                _mapId,
                clamped,
                claimedCount,
                _milestones.Length,
                claimableCount,
                claimableSilver,
                remainingSilver,
                hasNext,
                nextThreshold,
                summaryZh);
        }

        public RpgWorld500xMapCompletionSummary ComputeCompletionSummary(
            RpgWorld500xMapLayout layout,
            RpgMapExplorationTracker explorationTracker,
            RpgWorld500xCachesAndShortcutsController cachesAndShortcuts)
        {
            int totalMarkers = layout != null ? layout.Markers.Count : 0;
            int discoveredMarkers = 0;
            if (layout != null && explorationTracker != null)
            {
                for (int i = 0; i < layout.Markers.Count; i++)
                {
                    if (explorationTracker.IsMarkerDiscovered(layout.Markers[i].Id))
                    {
                        discoveredMarkers++;
                    }
                }
            }

            IReadOnlyList<RpgSupplyCacheDef> caches = RpgWorld500xContentCatalog.GetCaches(_mapId);
            IReadOnlyList<RpgWorldShortcutDef> shortcuts = RpgWorld500xContentCatalog.GetShortcuts(_mapId);
            int lootedCaches = cachesAndShortcuts != null ? cachesAndShortcuts.LootedCacheCount : 0;
            int unlockedShortcuts = cachesAndShortcuts != null ? cachesAndShortcuts.UnlockedShortcutCount : 0;

            IReadOnlyList<RpgStorySpotDef> spots = RpgWorld500xLivingWorldCatalog.GetStorySpots(_mapId);
            int inspectedSpots = 0;
            for (int i = 0; i < spots.Count; i++)
            {
                if (_getInt(spots[i].MakinomKey) == 1)
                {
                    inspectedSpots++;
                }
            }

            return new RpgWorld500xMapCompletionSummary(
                _mapId,
                _visitedChunkIndices.Count,
                TotalChunks,
                discoveredMarkers,
                totalMarkers,
                lootedCaches,
                caches.Count,
                unlockedShortcuts,
                shortcuts.Count,
                inspectedSpots,
                spots.Count);
        }

        public List<RpgWorld500xCartographyMilestone> ClaimEligibleMilestones(int completionPercent)
        {
            var claimed = new List<RpgWorld500xCartographyMilestone>();
            for (int i = 0; i < _milestones.Length; i++)
            {
                RpgWorld500xCartographyMilestone m = _milestones[i];
                if (completionPercent < m.ThresholdPercent)
                {
                    continue;
                }

                if (_getInt(m.MakinomKey) == 1)
                {
                    continue;
                }

                _setInt(m.MakinomKey, 1);
                _onMilestoneClaimed?.Invoke(m);
                claimed.Add(m);
            }

            return claimed;
        }
    }
}