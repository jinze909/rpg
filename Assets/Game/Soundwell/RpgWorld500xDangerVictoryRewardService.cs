using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rpg.Soundwell
{
    public readonly struct RpgWorld500xMapFirstClearSummary
    {
        public RpgWorld500xMapFirstClearSummary(
            RpgWorldMapId mapId,
            int totalDangerZones,
            int clearedDangerZones,
            int remainingDangerZones,
            int remainingStandardSaltSilver,
            int remainingLockBreakSaltSilver,
            int remainingStandardReputation,
            int remainingLockBreakReputation,
            string summaryZh)
        {
            MapId = mapId;
            TotalDangerZones = totalDangerZones;
            ClearedDangerZones = clearedDangerZones;
            RemainingDangerZones = remainingDangerZones;
            RemainingStandardSaltSilver = remainingStandardSaltSilver;
            RemainingLockBreakSaltSilver = remainingLockBreakSaltSilver;
            RemainingStandardReputation = remainingStandardReputation;
            RemainingLockBreakReputation = remainingLockBreakReputation;
            SummaryZh = summaryZh ?? string.Empty;
        }

        public RpgWorldMapId MapId { get; }
        public int TotalDangerZones { get; }
        public int ClearedDangerZones { get; }
        public int RemainingDangerZones { get; }
        public int RemainingStandardSaltSilver { get; }
        public int RemainingLockBreakSaltSilver { get; }
        public int RemainingStandardReputation { get; }
        public int RemainingLockBreakReputation { get; }
        public string SummaryZh { get; }
    }

    public readonly struct RpgWorld500xDangerVictoryReward
    {
        public RpgWorld500xDangerVictoryReward(
            RpgWorldMapId mapId,
            string markerId,
            string enemyNameZh,
            RpgWorld500xThreatTier tier,
            bool isFirstClear,
            bool lockBreakAchieved,
            int saltSilverReward,
            int reputationGain,
            string bonusSupplyId,
            string bonusSupplyNameZh,
            int bonusSupplyQuantity,
            string summaryBannerZh)
        {
            MapId = mapId;
            MarkerId = markerId ?? string.Empty;
            EnemyNameZh = enemyNameZh ?? string.Empty;
            Tier = tier;
            IsFirstClear = isFirstClear;
            LockBreakAchieved = lockBreakAchieved;
            SaltSilverReward = saltSilverReward;
            ReputationGain = reputationGain;
            BonusSupplyId = bonusSupplyId ?? string.Empty;
            BonusSupplyNameZh = bonusSupplyNameZh ?? string.Empty;
            BonusSupplyQuantity = bonusSupplyQuantity;
            SummaryBannerZh = summaryBannerZh ?? string.Empty;
        }

        public RpgWorldMapId MapId { get; }
        public string MarkerId { get; }
        public string EnemyNameZh { get; }
        public RpgWorld500xThreatTier Tier { get; }
        public bool IsFirstClear { get; }
        public bool LockBreakAchieved { get; }
        public int SaltSilverReward { get; }
        public int ReputationGain { get; }
        public string BonusSupplyId { get; }
        public string BonusSupplyNameZh { get; }
        public int BonusSupplyQuantity { get; }
        public string SummaryBannerZh { get; }
    }

    /// <summary>
    /// Deterministic DangerZone combat victory reward resolver for all four 500x maps.
    /// Scales Salt-Silver, regional faction reputation, and recommended camp supply
    /// drops by threat tier, first-clear status, and Lock-Break mastery under the
    /// rpg.return.world.danger_clear.* namespace.
    /// </summary>
    public static class RpgWorld500xDangerVictoryRewardService
    {
        public const string FirstClearKeyPrefix = "rpg.return.world.danger_clear.";

        public static string FirstClearKey(string markerId) =>
            FirstClearKeyPrefix + (markerId ?? string.Empty);

        public static bool IsFirstClearCompleted(string markerId, Func<string, int> getInt)
        {
            return !string.IsNullOrWhiteSpace(markerId) &&
                   getInt != null &&
                   getInt(FirstClearKey(markerId)) != 0;
        }

        public static RpgWorld500xDangerVictoryReward PreviewVictoryReward(
            RpgWorldMapId mapId,
            string markerId,
            bool lockBreakAchieved,
            Func<string, int> getInt = null)
        {
            bool isFirstClear = !string.IsNullOrWhiteSpace(markerId) && !IsFirstClearCompleted(markerId, getInt);
            return ComputeReward(mapId, markerId, lockBreakAchieved, isFirstClear);
        }

        public static RpgWorld500xMapFirstClearSummary SummarizeRemainingFirstClearRewards(
            RpgWorld500xMapLayout layout,
            Func<string, int> getInt = null)
        {
            if (layout == null)
            {
                throw new ArgumentNullException(nameof(layout));
            }

            RpgWorldMapId mapId = layout.Spec.MapId;
            int total = 0;
            int cleared = 0;
            int remStdSilver = 0;
            int remLbSilver = 0;
            int remStdRep = 0;
            int remLbRep = 0;

            for (int i = 0; i < layout.Markers.Count; i++)
            {
                RpgMapLandmarkMarker marker = layout.Markers[i];
                if (marker.Category != RpgMapMarkerCategory.DangerZone)
                {
                    continue;
                }

                total++;
                if (IsFirstClearCompleted(marker.Id, getInt))
                {
                    cleared++;
                    continue;
                }

                RpgWorld500xDangerVictoryReward std = ComputeReward(mapId, marker.Id, lockBreakAchieved: false, isFirstClear: true);
                RpgWorld500xDangerVictoryReward lb = ComputeReward(mapId, marker.Id, lockBreakAchieved: true, isFirstClear: true);
                remStdSilver += std.SaltSilverReward;
                remLbSilver += lb.SaltSilverReward;
                remStdRep += std.ReputationGain;
                remLbRep += lb.ReputationGain;
            }

            int remaining = Math.Max(0, total - cleared);
            string summaryZh = remaining > 0
                ? "【首破悬赏余量】" + layout.Spec.DisplayNameZh + "｜已首破 " + cleared + "/" + total +
                  "｜剩余盐银 " + remStdSilver + "（破锁 " + remLbSilver + "）｜声望 " + remStdRep + "（破锁 " + remLbRep + "）"
                : "【首破悬赏余量】" + layout.Spec.DisplayNameZh + "｜已全部完成首破（" + cleared + "/" + total + "）";

            return new RpgWorld500xMapFirstClearSummary(
                mapId,
                total,
                cleared,
                remaining,
                remStdSilver,
                remLbSilver,
                remStdRep,
                remLbRep,
                summaryZh);
        }

        public static RpgWorld500xDangerVictoryReward ComputeReward(
            RpgWorldMapId mapId,
            string markerId,
            bool lockBreakAchieved,
            bool isFirstClear)
        {
            RpgWorld500xThreatProfileDef profile =
                RpgWorld500xBestiaryCatalog.ResolveProfileForMarker(mapId, markerId);

            int baseSilver;
            int baseRep;
            switch (profile.Tier)
            {
                case RpgWorld500xThreatTier.HazardBoss:
                    baseSilver = 85;
                    baseRep = 60;
                    break;
                case RpgWorld500xThreatTier.ElitePatrol:
                    baseSilver = 50;
                    baseRep = 35;
                    break;
                case RpgWorld500xThreatTier.Skirmisher:
                default:
                    baseSilver = 30;
                    baseRep = 20;
                    break;
            }

            float clearScale = isFirstClear ? 1.0f : 0.60f;
            float repScale = isFirstClear ? 1.0f : 0.50f;
            float lockBreakScale = lockBreakAchieved ? 1.35f : 1.0f;

            int finalSilver = Math.Max(1, Mathf.CeilToInt(baseSilver * clearScale * lockBreakScale));
            int finalRep = Math.Max(1, Mathf.CeilToInt(baseRep * repScale)) + (lockBreakAchieved ? 15 : 0);

            bool grantSupply = isFirstClear || lockBreakAchieved;
            string supplyId = grantSupply ? profile.RecommendedSupplyId : string.Empty;
            string supplyNameZh = grantSupply ? ResolveSupplyNameZh(supplyId) : string.Empty;
            int supplyQty = grantSupply && !string.IsNullOrEmpty(supplyId) ? 1 : 0;

            string clearTag = isFirstClear ? "首破" : "清剿";
            string lbTag = lockBreakAchieved ? " · 破锁精通" : string.Empty;
            string supplySegment = supplyQty > 0 ? "｜缴获" + supplyNameZh + " ×" + supplyQty : string.Empty;
            string banner = "[" + clearTag + lbTag + "] 击败" + profile.EnemyNameZh +
                            "：盐银 +" + finalSilver + "｜声望 +" + finalRep + supplySegment;

            return new RpgWorld500xDangerVictoryReward(
                mapId,
                markerId,
                profile.EnemyNameZh,
                profile.Tier,
                isFirstClear,
                lockBreakAchieved,
                finalSilver,
                finalRep,
                supplyId,
                supplyNameZh,
                supplyQty,
                banner);
        }

        public static RpgWorld500xDangerVictoryReward GrantVictoryReward(
            RpgWorld500xSceneBootstrapper bootstrapper,
            RpgWorld500xCampHavenService campHaven,
            RpgWorldMapId mapId,
            string markerId,
            bool lockBreakAchieved,
            Func<string, int> getInt = null,
            Action<string, int> setInt = null)
        {
            if (bootstrapper == null || bootstrapper.IsDisposed)
            {
                throw new ArgumentNullException(nameof(bootstrapper));
            }

            Func<string, int> reader = getInt ??
                (k => (bootstrapper.Store != null && bootstrapper.Store.TryGetValue(k, out int v)) ? v : 0);

            string clearKey = FirstClearKey(markerId);
            bool isFirstClear = !string.IsNullOrWhiteSpace(markerId) && reader(clearKey) == 0;

            RpgWorld500xDangerVictoryReward reward = ComputeReward(mapId, markerId, lockBreakAchieved, isFirstClear);

            if (isFirstClear && RpgWorld500xEnrichedContentAuditor.IsSafeWorldKey(clearKey))
            {
                setInt?.Invoke(clearKey, 1);
            }

            RpgWorld500xFieldBountyAndSupplyService supplySvc = bootstrapper.Driver.Facade.BountyAndSupply;
            supplySvc.AddSaltSilver(reward.SaltSilverReward);

            if (reward.BonusSupplyQuantity > 0 && TryFindSupply(reward.BonusSupplyId, out RpgWorld500xCampSupplyItemDef item))
            {
                int owned = supplySvc.GetOwnedSupplyCount(item.Id);
                if (owned < item.MaxStock)
                {
                    int capped = Math.Min(item.MaxStock, owned + reward.BonusSupplyQuantity);
                    setInt?.Invoke(item.OwnedCountKey, capped);
                }
            }

            if (campHaven != null)
            {
                campHaven.Reputation.AddPoints(mapId, reward.ReputationGain);
            }

            bootstrapper.StatusHud.SyncFromDriver(bootstrapper.Driver);
            return reward;
        }

        private static bool TryFindSupply(string itemId, out RpgWorld500xCampSupplyItemDef found)
        {
            found = default;
            if (string.IsNullOrEmpty(itemId))
            {
                return false;
            }

            IReadOnlyList<RpgWorld500xCampSupplyItemDef> supplies =
                RpgWorld500xBountyAndSupplyCatalog.GetCampSupplies();
            for (int i = 0; i < supplies.Count; i++)
            {
                if (string.Equals(supplies[i].Id, itemId, StringComparison.Ordinal))
                {
                    found = supplies[i];
                    return true;
                }
            }

            return false;
        }

        private static string ResolveSupplyNameZh(string itemId)
        {
            return TryFindSupply(itemId, out RpgWorld500xCampSupplyItemDef item)
                ? item.DisplayNameZh
                : itemId;
        }
    }
}
