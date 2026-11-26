using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rpg.Soundwell
{
    public enum RpgRegionalReputationRank : byte
    {
        Stranger = 0,
        Trusted = 1,
        Ally = 2,
        Sworn = 3
    }

    public readonly struct RpgRegionalReputationSnapshot
    {
        public RpgRegionalReputationSnapshot(
            RpgWorldMapId mapId,
            string factionNameZh,
            int points,
            RpgRegionalReputationRank rank,
            string rankNameZh,
            string honorTitleZh,
            int campSupplyDiscountPercent,
            int pointsToNextRank)
        {
            MapId = mapId;
            FactionNameZh = factionNameZh;
            Points = points;
            Rank = rank;
            RankNameZh = rankNameZh;
            HonorTitleZh = honorTitleZh;
            CampSupplyDiscountPercent = campSupplyDiscountPercent;
            PointsToNextRank = pointsToNextRank;
        }

        public RpgWorldMapId MapId { get; }
        public string FactionNameZh { get; }
        public int Points { get; }
        public RpgRegionalReputationRank Rank { get; }
        public string RankNameZh { get; }
        public string HonorTitleZh { get; }
        public int CampSupplyDiscountPercent { get; }
        public int PointsToNextRank { get; }
    }

    public readonly struct RpgWorld500xWorldReputationOverview
    {
        public RpgWorld500xWorldReputationOverview(
            IReadOnlyList<RpgRegionalReputationSnapshot> snapshots,
            int totalPoints,
            int swornRegionCount,
            RpgWorldMapId highestStandingMapId,
            int bestCampDiscountPercent,
            string summaryLineZh)
        {
            Snapshots = snapshots ?? Array.Empty<RpgRegionalReputationSnapshot>();
            TotalPoints = totalPoints;
            SwornRegionCount = swornRegionCount;
            HighestStandingMapId = highestStandingMapId;
            BestCampDiscountPercent = bestCampDiscountPercent;
            SummaryLineZh = summaryLineZh ?? string.Empty;
        }

        public IReadOnlyList<RpgRegionalReputationSnapshot> Snapshots { get; }
        public int TotalPoints { get; }
        public int SwornRegionCount { get; }
        public RpgWorldMapId HighestStandingMapId { get; }
        public int BestCampDiscountPercent { get; }
        public string SummaryLineZh { get; }
    }

    /// <summary>
    /// Tracks regional faction reputation across all four 500x-area maps (Soundwell,
    /// Red Salt Harbor, Whitewater Crossing, Mine Camp), deriving rank perks such as
    /// camp supply Salt-Silver discounts and regional honor titles under the
    /// rpg.return.world.reputation.* Makinom namespace.
    /// </summary>
    public sealed class RpgWorld500xRegionalReputationService
    {
        public const int MaxReputationPoints = 1000;
        public const int TrustedThreshold = 150;
        public const int AllyThreshold = 400;
        public const int SwornThreshold = 750;

        public const int PointsPerLandmark = 15;
        public const int PointsPerCache = 20;
        public const int PointsPerShortcut = 35;
        public const int PointsPerStorySpot = 25;
        public const int PointsPerBounty = 60;
        public const int PointsPerThreatDossier = 30;

        private readonly Func<string, int> _getInt;
        private readonly Action<string, int> _setInt;

        public RpgWorld500xRegionalReputationService(
            Func<string, int> getInt = null,
            Action<string, int> setInt = null)
        {
            _getInt = getInt ?? (_ => 0);
            _setInt = setInt ?? ((_, __) => { });
        }

        public static string ReputationKey(RpgWorldMapId mapId) =>
            "rpg.return.world.reputation." + mapId.ToString().ToLowerInvariant();

        public static string GetFactionNameZh(RpgWorldMapId mapId)
        {
            switch (mapId)
            {
                case RpgWorldMapId.RedSaltHarbor: return "赤盐港渔户与货帮";
                case RpgWorldMapId.WhitewaterCrossing: return "白水渡工兵防线";
                case RpgWorldMapId.MineCamp: return "赤盐矿营互助会";
                case RpgWorldMapId.Soundwell:
                default: return "声井测绘班";
            }
        }

        public static string GetSwornHonorTitleZh(RpgWorldMapId mapId)
        {
            switch (mapId)
            {
                case RpgWorldMapId.RedSaltHarbor: return "赤盐港义士";
                case RpgWorldMapId.WhitewaterCrossing: return "白水渡先锋";
                case RpgWorldMapId.MineCamp: return "矿营引路人";
                case RpgWorldMapId.Soundwell:
                default: return "声井守望者";
            }
        }

        public static RpgRegionalReputationRank ResolveRank(int points)
        {
            if (points >= SwornThreshold) return RpgRegionalReputationRank.Sworn;
            if (points >= AllyThreshold) return RpgRegionalReputationRank.Ally;
            if (points >= TrustedThreshold) return RpgRegionalReputationRank.Trusted;
            return RpgRegionalReputationRank.Stranger;
        }

        public static string GetRankNameZh(RpgRegionalReputationRank rank)
        {
            switch (rank)
            {
                case RpgRegionalReputationRank.Trusted: return "互通有无";
                case RpgRegionalReputationRank.Ally: return "同舟共济";
                case RpgRegionalReputationRank.Sworn: return "生死托付";
                case RpgRegionalReputationRank.Stranger:
                default: return "初来乍到";
            }
        }

        public static int GetCampSupplyDiscountPercent(RpgRegionalReputationRank rank)
        {
            switch (rank)
            {
                case RpgRegionalReputationRank.Trusted: return 10;
                case RpgRegionalReputationRank.Ally: return 20;
                case RpgRegionalReputationRank.Sworn: return 30;
                case RpgRegionalReputationRank.Stranger:
                default: return 0;
            }
        }

        public static int ComputeDiscountedCost(int baseSaltSilverCost, RpgRegionalReputationRank rank)
        {
            if (baseSaltSilverCost <= 0)
            {
                return 0;
            }

            int discountPct = GetCampSupplyDiscountPercent(rank);
            // Currency is integral: round up exactly without float precision loss
            // or overflowing an int when the undiscounted cost is large.
            long scaled = (long)baseSaltSilverCost * (100 - discountPct);
            return (int)((scaled + 99L) / 100L);
        }

        public int ComputeDiscountedCostForMap(RpgWorldMapId mapId, int baseSaltSilverCost)
        {
            return ComputeDiscountedCost(baseSaltSilverCost, ResolveRank(GetPoints(mapId)));
        }

        public int GetPoints(RpgWorldMapId mapId)
        {
            return Mathf.Clamp(_getInt(ReputationKey(mapId)), 0, MaxReputationPoints);
        }

        public RpgRegionalReputationSnapshot AddPoints(RpgWorldMapId mapId, int delta)
        {
            if (delta > 0)
            {
                int current = GetPoints(mapId);
                // Sum before clamping in a wider type: a large reputation grant
                // must saturate at the cap, never overflow back to zero.
                int updated = (int)Math.Min(MaxReputationPoints, (long)current + delta);
                _setInt(ReputationKey(mapId), updated);
            }

            return GetSnapshot(mapId);
        }

        public RpgRegionalReputationSnapshot SyncMinimumFromCompletion(
            RpgWorld500xMapCompletionSummary summary,
            int claimedBounties = 0,
            int studiedDossiers = 0)
        {
            long computed =
                ((long)Math.Max(0, summary.DiscoveredMarkers) * PointsPerLandmark) +
                ((long)Math.Max(0, summary.LootedCaches) * PointsPerCache) +
                ((long)Math.Max(0, summary.UnlockedShortcuts) * PointsPerShortcut) +
                ((long)Math.Max(0, summary.InspectedStorySpots) * PointsPerStorySpot) +
                ((long)Math.Max(0, claimedBounties) * PointsPerBounty) +
                ((long)Math.Max(0, studiedDossiers) * PointsPerThreatDossier);

            int clamped = (int)Math.Min(MaxReputationPoints, Math.Max(0L, computed));
            int current = GetPoints(summary.MapId);
            if (clamped > current)
            {
                _setInt(ReputationKey(summary.MapId), clamped);
            }

            return GetSnapshot(summary.MapId);
        }

        public RpgRegionalReputationSnapshot GetSnapshot(RpgWorldMapId mapId)
        {
            int points = GetPoints(mapId);
            RpgRegionalReputationRank rank = ResolveRank(points);
            int nextThreshold = rank == RpgRegionalReputationRank.Stranger ? TrustedThreshold
                : rank == RpgRegionalReputationRank.Trusted ? AllyThreshold
                : rank == RpgRegionalReputationRank.Ally ? SwornThreshold
                : points;
            int toNext = Math.Max(0, nextThreshold - points);
            string honor = rank == RpgRegionalReputationRank.Sworn
                ? GetSwornHonorTitleZh(mapId)
                : string.Empty;

            return new RpgRegionalReputationSnapshot(
                mapId,
                GetFactionNameZh(mapId),
                points,
                rank,
                GetRankNameZh(rank),
                honor,
                GetCampSupplyDiscountPercent(rank),
                toNext);
        }

        public RpgWorld500xWorldReputationOverview BuildWorldOverview()
        {
            IReadOnlyList<RpgExpandedMapSpec> specs = RpgExpandedWorldScale.AllSpecs;
            var snapshots = new List<RpgRegionalReputationSnapshot>(specs.Count);
            int totalPoints = 0;
            int swornCount = 0;
            int bestPoints = -1;
            int bestDiscount = 0;
            RpgWorldMapId highestMapId = specs.Count > 0 ? specs[0].MapId : RpgWorldMapId.Soundwell;
            string highestMapNameZh = specs.Count > 0 ? specs[0].DisplayNameZh : string.Empty;

            for (int i = 0; i < specs.Count; i++)
            {
                RpgExpandedMapSpec spec = specs[i];
                RpgRegionalReputationSnapshot snap = GetSnapshot(spec.MapId);
                snapshots.Add(snap);
                totalPoints += snap.Points;
                if (snap.Rank == RpgRegionalReputationRank.Sworn)
                {
                    swornCount++;
                }

                if (snap.CampSupplyDiscountPercent > bestDiscount)
                {
                    bestDiscount = snap.CampSupplyDiscountPercent;
                }

                if (snap.Points > bestPoints)
                {
                    bestPoints = snap.Points;
                    highestMapId = spec.MapId;
                    highestMapNameZh = spec.DisplayNameZh;
                }
            }

            string summary = "【四境声望总览】累计 " + totalPoints + " 点｜生死托付 " + swornCount + "/" + specs.Count +
                             " 境｜最高折扣 -" + bestDiscount + "%（" + highestMapNameZh + "）";

            return new RpgWorld500xWorldReputationOverview(
                snapshots,
                totalPoints,
                swornCount,
                highestMapId,
                bestDiscount,
                summary);
        }
    }
}
