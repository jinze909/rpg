using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rpg.Soundwell
{
    public enum RpgWorld500xBountyGoalKind : byte
    {
        DiscoverLandmarks = 1,
        LootCaches = 2,
        UnlockShortcuts = 3,
        InspectStorySpots = 4
    }

    public readonly struct RpgWorld500xFieldBountyDef
    {
        public RpgWorld500xFieldBountyDef(
            string id,
            RpgWorldMapId mapId,
            string titleZh,
            string descriptionZh,
            RpgWorld500xBountyGoalKind goalKind,
            int targetCount,
            int saltSilverReward)
        {
            Id = id;
            MapId = mapId;
            TitleZh = titleZh;
            DescriptionZh = descriptionZh;
            GoalKind = goalKind;
            TargetCount = targetCount;
            SaltSilverReward = saltSilverReward;
        }

        public string Id { get; }
        public RpgWorldMapId MapId { get; }
        public string TitleZh { get; }
        public string DescriptionZh { get; }
        public RpgWorld500xBountyGoalKind GoalKind { get; }
        public int TargetCount { get; }
        public int SaltSilverReward { get; }
        public string MakinomKey => "rpg.return.world.bounty." + Id;
    }

    public readonly struct RpgWorld500xCampSupplyItemDef
    {
        public RpgWorld500xCampSupplyItemDef(
            string id,
            string displayNameZh,
            string effectSummaryZh,
            int saltSilverCost,
            int maxStock)
        {
            Id = id;
            DisplayNameZh = displayNameZh;
            EffectSummaryZh = effectSummaryZh;
            SaltSilverCost = saltSilverCost;
            MaxStock = maxStock;
        }

        public string Id { get; }
        public string DisplayNameZh { get; }
        public string EffectSummaryZh { get; }
        public int SaltSilverCost { get; }
        public int MaxStock { get; }
        public string OwnedCountKey => "rpg.return.world.supply.item." + Id;
    }

    public enum RpgCampSupplyPurchaseStatus : byte
    {
        Succeeded = 0,
        UnknownItem = 1,
        InvalidQuantity = 2,
        OutOfStock = 3,
        InsufficientSaltSilver = 4
    }

    public readonly struct RpgWorld500xBountyBoardSummary
    {
        public RpgWorld500xBountyBoardSummary(
            RpgWorldMapId mapId,
            int totalBounties,
            int claimedCount,
            int claimableCount,
            int claimableSaltSilver,
            int remainingSaltSilver,
            string summaryZh)
        {
            MapId = mapId;
            TotalBounties = totalBounties;
            ClaimedCount = claimedCount;
            ClaimableCount = claimableCount;
            ClaimableSaltSilver = claimableSaltSilver;
            RemainingSaltSilver = remainingSaltSilver;
            SummaryZh = summaryZh ?? string.Empty;
        }

        public RpgWorldMapId MapId { get; }
        public int TotalBounties { get; }
        public int ClaimedCount { get; }
        public int ClaimableCount { get; }
        public int ClaimableSaltSilver { get; }
        public int RemainingSaltSilver { get; }
        public string SummaryZh { get; }
    }

    /// <summary>
    /// Curated catalog of 12 regional exploration bounties (3 per 500x map) and
    /// 6 camp merchant field supplies redeemable with Salt-Silver (盐银).
    /// </summary>
    public static class RpgWorld500xBountyAndSupplyCatalog
    {
        private static readonly RpgWorld500xFieldBountyDef[] SoundwellBounties =
        {
            new RpgWorld500xFieldBountyDef("sw_b01", RpgWorldMapId.Soundwell, "声井测绘初录",
                "在声井发现至少 6 处地标并回报测绘班。", RpgWorld500xBountyGoalKind.DiscoverLandmarks, 6, 40),
            new RpgWorld500xFieldBountyDef("sw_b02", RpgWorldMapId.Soundwell, "井下补给回收",
                "搜寻声井内至少 4 处藏匿补给箱或旧工遗物。", RpgWorld500xBountyGoalKind.LootCaches, 4, 50),
            new RpgWorld500xFieldBountyDef("sw_b03", RpgWorldMapId.Soundwell, "暗廊通路贯通",
                "打通声井内至少 2 条单向捷径或检修门。", RpgWorld500xBountyGoalKind.UnlockShortcuts, 2, 60)
        };

        private static readonly RpgWorld500xFieldBountyDef[] RedSaltHarborBounties =
        {
            new RpgWorld500xFieldBountyDef("rsh_b01", RpgWorldMapId.RedSaltHarbor, "港区航标巡查",
                "在赤盐港踏勘至少 8 处码头与防波堤地标。", RpgWorld500xBountyGoalKind.DiscoverLandmarks, 8, 45),
            new RpgWorld500xFieldBountyDef("rsh_b02", RpgWorldMapId.RedSaltHarbor, "伤病棚物资筹措",
                "在赤盐港寻获至少 4 处补给箱或封蜡盐桶。", RpgWorld500xBountyGoalKind.LootCaches, 4, 55),
            new RpgWorld500xFieldBountyDef("rsh_b03", RpgWorldMapId.RedSaltHarbor, "旧港证物抄录",
                "检视赤盐港至少 2 处环境叙事碑刻与旧木匣。", RpgWorld500xBountyGoalKind.InspectStorySpots, 2, 50)
        };

        private static readonly RpgWorld500xFieldBountyDef[] WhitewaterBounties =
        {
            new RpgWorld500xFieldBountyDef("ww_b01", RpgWorldMapId.WhitewaterCrossing, "白水渡两岸勘察",
                "在白水渡发现至少 6 处两岸及浮桥关键地标。", RpgWorld500xBountyGoalKind.DiscoverLandmarks, 6, 45),
            new RpgWorld500xFieldBountyDef("ww_b02", RpgWorldMapId.WhitewaterCrossing, "渡口便桥抢通",
                "架通白水渡至少 2 条浮索便桥或山口木栅。", RpgWorld500xBountyGoalKind.UnlockShortcuts, 2, 65),
            new RpgWorld500xFieldBountyDef("ww_b03", RpgWorldMapId.WhitewaterCrossing, "断桥旧迹考证",
                "检视白水渡至少 2 处旧渡口与断桥纪念点。", RpgWorld500xBountyGoalKind.InspectStorySpots, 2, 45)
        };

        private static readonly RpgWorld500xFieldBountyDef[] MineCampBounties =
        {
            new RpgWorld500xFieldBountyDef("mc_b01", RpgWorldMapId.MineCamp, "矿营外围侦察",
                "在赤盐矿营摸清至少 6 处哨楼与泵房地标。", RpgWorld500xBountyGoalKind.DiscoverLandmarks, 6, 50),
            new RpgWorld500xFieldBountyDef("mc_b02", RpgWorldMapId.MineCamp, "泵房备件征集",
                "在矿营回收至少 4 处矿具与泵房压力阀补给。", RpgWorld500xBountyGoalKind.LootCaches, 4, 60),
            new RpgWorld500xFieldBountyDef("mc_b03", RpgWorldMapId.MineCamp, "疏散通道疏通",
                "解锁矿营内至少 2 条矿车道岔或疏散直梯。", RpgWorld500xBountyGoalKind.UnlockShortcuts, 2, 70)
        };

        private static readonly RpgWorld500xCampSupplyItemDef[] CampSupplies =
        {
            new RpgWorld500xCampSupplyItemDef("gauze", "止血纱布", "恢复少量生命并止血", 18, 10),
            new RpgWorld500xCampSupplyItemDef("rations", "行军干粮", "野外休整回复体力", 15, 12),
            new RpgWorld500xCampSupplyItemDef("lamp_oil", "矿灯灯油", "延长夜间与浓雾提灯照明", 22, 8),
            new RpgWorld500xCampSupplyItemDef("damp_cloth", "防潮湿布", "降低声井回声奔跑噪音", 25, 6),
            new RpgWorld500xCampSupplyItemDef("flare", "应急信号弹", "照亮危险区巡逻视野盲区", 30, 5),
            new RpgWorld500xCampSupplyItemDef("iron_clamps", "便携修桥铁件", "加速捷径与吊桥检修", 35, 4)
        };

        public static IReadOnlyList<RpgWorld500xFieldBountyDef> GetBounties(RpgWorldMapId mapId)
        {
            switch (mapId)
            {
                case RpgWorldMapId.Soundwell: return SoundwellBounties;
                case RpgWorldMapId.RedSaltHarbor: return RedSaltHarborBounties;
                case RpgWorldMapId.WhitewaterCrossing: return WhitewaterBounties;
                case RpgWorldMapId.MineCamp: return MineCampBounties;
                default: throw new ArgumentOutOfRangeException(nameof(mapId), mapId, "Unknown map id.");
            }
        }

        public static IReadOnlyList<RpgWorld500xCampSupplyItemDef> GetCampSupplies() => CampSupplies;
    }

    /// <summary>
    /// Evaluates regional exploration bounties against live 500x completion state,
    /// grants idempotent Salt-Silver rewards, and manages camp merchant supply
    /// redemptions under the rpg.return.world.* Makinom namespace.
    /// </summary>
    public sealed class RpgWorld500xFieldBountyAndSupplyService
    {
        public const string WalletSaltSilverKey = "rpg.return.world.supply.salt_silver_balance";

        private readonly Func<string, int> _getInt;
        private readonly Action<string, int> _setInt;
        private readonly Dictionary<string, RpgWorld500xCampSupplyItemDef> _itemsById =
            new Dictionary<string, RpgWorld500xCampSupplyItemDef>(StringComparer.Ordinal);

        public RpgWorld500xFieldBountyAndSupplyService(
            Func<string, int> getInt = null,
            Action<string, int> setInt = null)
        {
            _getInt = getInt ?? (_ => 0);
            _setInt = setInt ?? ((_, __) => { });

            IReadOnlyList<RpgWorld500xCampSupplyItemDef> supplies = RpgWorld500xBountyAndSupplyCatalog.GetCampSupplies();
            for (int i = 0; i < supplies.Count; i++)
            {
                _itemsById[supplies[i].Id] = supplies[i];
            }
        }

        public int SaltSilverBalance => Math.Max(0, _getInt(WalletSaltSilverKey));

        public void AddSaltSilver(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            // Saturate currency credits rather than wrapping the persisted int wallet.
            long credited = (long)SaltSilverBalance + amount;
            _setInt(WalletSaltSilverKey, (int)Math.Min(int.MaxValue, credited));
        }

        public bool IsBountyClaimed(RpgWorld500xFieldBountyDef bounty) =>
            _getInt(bounty.MakinomKey) == 1;

        public static int EvaluateProgressCount(
            RpgWorld500xFieldBountyDef bounty,
            RpgWorld500xMapCompletionSummary summary)
        {
            switch (bounty.GoalKind)
            {
                case RpgWorld500xBountyGoalKind.DiscoverLandmarks:
                    return summary.DiscoveredMarkers;
                case RpgWorld500xBountyGoalKind.LootCaches:
                    return summary.LootedCaches;
                case RpgWorld500xBountyGoalKind.UnlockShortcuts:
                    return summary.UnlockedShortcuts;
                case RpgWorld500xBountyGoalKind.InspectStorySpots:
                    return summary.InspectedStorySpots;
                default:
                    return 0;
            }
        }

        /// <summary>
        /// Builds a read-only summary of regional bounty progress and claimable
        /// Salt-Silver without mutating wallet balance or claimed flags.
        /// </summary>
        public RpgWorld500xBountyBoardSummary BuildBountyBoardSummary(RpgWorld500xMapCompletionSummary summary)
        {
            IReadOnlyList<RpgWorld500xFieldBountyDef> bounties =
                RpgWorld500xBountyAndSupplyCatalog.GetBounties(summary.MapId);

            int claimedCount = 0;
            int claimableCount = 0;
            int claimableSaltSilver = 0;
            int remainingSaltSilver = 0;

            for (int i = 0; i < bounties.Count; i++)
            {
                RpgWorld500xFieldBountyDef bounty = bounties[i];
                if (IsBountyClaimed(bounty))
                {
                    claimedCount++;
                    continue;
                }

                remainingSaltSilver += bounty.SaltSilverReward;
                if (EvaluateProgressCount(bounty, summary) >= bounty.TargetCount)
                {
                    claimableCount++;
                    claimableSaltSilver += bounty.SaltSilverReward;
                }
            }

            string summaryZh;
            if (claimedCount >= bounties.Count)
            {
                summaryZh = $"【悬赏进度】已领 {claimedCount}/{bounties.Count} · 本境悬赏已全部结清";
            }
            else if (claimableCount > 0)
            {
                summaryZh = $"【悬赏进度】已领 {claimedCount}/{bounties.Count} · 可领 {claimableCount} 项（+{claimableSaltSilver} 盐银）· 剩余总赏 {remainingSaltSilver} 盐银";
            }
            else
            {
                int pendingCount = bounties.Count - claimedCount;
                summaryZh = $"【悬赏进度】已领 {claimedCount}/{bounties.Count} · 待完成 {pendingCount} 项 · 剩余总赏 {remainingSaltSilver} 盐银";
            }

            return new RpgWorld500xBountyBoardSummary(
                summary.MapId,
                bounties.Count,
                claimedCount,
                claimableCount,
                claimableSaltSilver,
                remainingSaltSilver,
                summaryZh);
        }

        public List<RpgWorld500xFieldBountyDef> ClaimCompletedBounties(RpgWorld500xMapCompletionSummary summary)
        {
            var claimed = new List<RpgWorld500xFieldBountyDef>();
            IReadOnlyList<RpgWorld500xFieldBountyDef> bounties =
                RpgWorld500xBountyAndSupplyCatalog.GetBounties(summary.MapId);

            for (int i = 0; i < bounties.Count; i++)
            {
                RpgWorld500xFieldBountyDef bounty = bounties[i];
                if (IsBountyClaimed(bounty))
                {
                    continue;
                }

                int current = EvaluateProgressCount(bounty, summary);
                if (current < bounty.TargetCount)
                {
                    continue;
                }

                _setInt(bounty.MakinomKey, 1);
                AddSaltSilver(bounty.SaltSilverReward);
                claimed.Add(bounty);
            }

            return claimed;
        }

        public int GetOwnedSupplyCount(string itemId)
        {
            if (string.IsNullOrEmpty(itemId) || !_itemsById.TryGetValue(itemId, out RpgWorld500xCampSupplyItemDef item))
            {
                return 0;
            }

            return Math.Max(0, _getInt(item.OwnedCountKey));
        }

        public int GetRemainingStock(string itemId)
        {
            if (string.IsNullOrEmpty(itemId) || !_itemsById.TryGetValue(itemId, out RpgWorld500xCampSupplyItemDef item))
            {
                return 0;
            }

            return Math.Max(0, item.MaxStock - GetOwnedSupplyCount(itemId));
        }

        /// <summary>
        /// Returns the maximum quantity of a camp supply item that the player can
        /// currently purchase given both remaining merchant stock and Salt-Silver balance.
        /// </summary>
        public int GetMaxAffordableQuantity(string itemId)
        {
            if (string.IsNullOrEmpty(itemId) || !_itemsById.TryGetValue(itemId, out RpgWorld500xCampSupplyItemDef item))
            {
                return 0;
            }

            int remainingStock = GetRemainingStock(itemId);
            if (remainingStock <= 0 || item.SaltSilverCost <= 0)
            {
                return 0;
            }

            int affordableByWallet = SaltSilverBalance / item.SaltSilverCost;
            return Math.Min(remainingStock, affordableByWallet);
        }

        public RpgCampSupplyPurchaseStatus TryPurchaseSupply(string itemId, int quantity)
        {
            if (quantity <= 0)
            {
                return RpgCampSupplyPurchaseStatus.InvalidQuantity;
            }

            if (string.IsNullOrEmpty(itemId) || !_itemsById.TryGetValue(itemId, out RpgWorld500xCampSupplyItemDef item))
            {
                return RpgCampSupplyPurchaseStatus.UnknownItem;
            }

            int owned = GetOwnedSupplyCount(itemId);
            // Subtract before comparing: owned + quantity could overflow int.MaxValue
            // and bypass the stock limit for an untrusted purchase quantity.
            if (quantity > item.MaxStock - owned)
            {
                return RpgCampSupplyPurchaseStatus.OutOfStock;
            }

            long totalCost = (long)item.SaltSilverCost * quantity;
            int balance = SaltSilverBalance;
            if (balance < totalCost)
            {
                return RpgCampSupplyPurchaseStatus.InsufficientSaltSilver;
            }

            _setInt(WalletSaltSilverKey, (int)(balance - totalCost));
            _setInt(item.OwnedCountKey, owned + quantity);
            return RpgCampSupplyPurchaseStatus.Succeeded;
        }
    }
}