using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rpg.Soundwell
{
    public enum RpgWorld500xMilestoneMetric : byte
    {
        LootedCaches = 0,
        UnlockedShortcuts = 1,
        HeardRumors = 2,
        InspectedStorySpots = 3,
        ClaimedBounties = 4,
        ClaimedCartographyMilestones = 5,
        SwornFactions = 6
    }

    public readonly struct RpgWorld500xExpeditionMilestoneDef
    {
        public RpgWorld500xExpeditionMilestoneDef(
            string id,
            string titleZh,
            string descriptionZh,
            RpgWorld500xMilestoneMetric metric,
            int targetCount,
            int saltSilverReward)
        {
            Id = id;
            TitleZh = titleZh;
            DescriptionZh = descriptionZh;
            Metric = metric;
            TargetCount = targetCount;
            SaltSilverReward = saltSilverReward;
        }

        public string Id { get; }
        public string TitleZh { get; }
        public string DescriptionZh { get; }
        public RpgWorld500xMilestoneMetric Metric { get; }
        public int TargetCount { get; }
        public int SaltSilverReward { get; }
        public string MakinomKey => "rpg.return.world.milestone." + Id;
    }

    public readonly struct RpgWorld500xCrossMapProgressSnapshot
    {
        public RpgWorld500xCrossMapProgressSnapshot(
            int lootedCaches,
            int totalCaches,
            int unlockedShortcuts,
            int totalShortcuts,
            int heardRumors,
            int totalFieldNpcs,
            int inspectedStorySpots,
            int totalStorySpots,
            int claimedBounties,
            int totalBounties,
            int claimedCartographyMilestones,
            int totalCartographyMilestones,
            int swornFactions,
            int totalFactions)
        {
            LootedCaches = lootedCaches;
            TotalCaches = totalCaches;
            UnlockedShortcuts = unlockedShortcuts;
            TotalShortcuts = totalShortcuts;
            HeardRumors = heardRumors;
            TotalFieldNpcs = totalFieldNpcs;
            InspectedStorySpots = inspectedStorySpots;
            TotalStorySpots = totalStorySpots;
            ClaimedBounties = claimedBounties;
            TotalBounties = totalBounties;
            ClaimedCartographyMilestones = claimedCartographyMilestones;
            TotalCartographyMilestones = totalCartographyMilestones;
            SwornFactions = swornFactions;
            TotalFactions = totalFactions;
        }

        public int LootedCaches { get; }
        public int TotalCaches { get; }
        public int UnlockedShortcuts { get; }
        public int TotalShortcuts { get; }
        public int HeardRumors { get; }
        public int TotalFieldNpcs { get; }
        public int InspectedStorySpots { get; }
        public int TotalStorySpots { get; }
        public int ClaimedBounties { get; }
        public int TotalBounties { get; }
        public int ClaimedCartographyMilestones { get; }
        public int TotalCartographyMilestones { get; }
        public int SwornFactions { get; }
        public int TotalFactions { get; }

        public int TotalCompletedItems =>
            LootedCaches + UnlockedShortcuts + HeardRumors + InspectedStorySpots +
            ClaimedBounties + ClaimedCartographyMilestones + SwornFactions;

        public int TotalWorldItems =>
            TotalCaches + TotalShortcuts + TotalFieldNpcs + TotalStorySpots +
            TotalBounties + TotalCartographyMilestones + TotalFactions;

        public int OverallWorldCompletionPercent => TotalWorldItems > 0
            ? Mathf.Clamp(Mathf.RoundToInt(((float)TotalCompletedItems / TotalWorldItems) * 100f), 0, 100)
            : 0;

        public int GetMetricValue(RpgWorld500xMilestoneMetric metric)
        {
            switch (metric)
            {
                case RpgWorld500xMilestoneMetric.LootedCaches: return LootedCaches;
                case RpgWorld500xMilestoneMetric.UnlockedShortcuts: return UnlockedShortcuts;
                case RpgWorld500xMilestoneMetric.HeardRumors: return HeardRumors;
                case RpgWorld500xMilestoneMetric.InspectedStorySpots: return InspectedStorySpots;
                case RpgWorld500xMilestoneMetric.ClaimedBounties: return ClaimedBounties;
                case RpgWorld500xMilestoneMetric.ClaimedCartographyMilestones: return ClaimedCartographyMilestones;
                case RpgWorld500xMilestoneMetric.SwornFactions: return SwornFactions;
                default: return 0;
            }
        }
    }

    public readonly struct RpgWorld500xMilestoneStatus
    {
        public RpgWorld500xMilestoneStatus(
            RpgWorld500xExpeditionMilestoneDef definition,
            int currentCount,
            bool isClaimed)
        {
            Definition = definition;
            CurrentCount = currentCount;
            IsClaimed = isClaimed;
        }

        public RpgWorld500xExpeditionMilestoneDef Definition { get; }
        public int CurrentCount { get; }
        public bool IsClaimed { get; }
        public bool IsSatisfied => CurrentCount >= Definition.TargetCount;
    }

    public readonly struct RpgWorld500xMilestoneSummary
    {
        public RpgWorld500xMilestoneSummary(
            RpgWorld500xCrossMapProgressSnapshot progress,
            int totalMilestones,
            int claimedCount,
            int claimableCount,
            int unclaimedSaltSilverReward,
            bool hasNextMilestone,
            RpgWorld500xMilestoneStatus nextMilestone,
            string summaryLineZh)
        {
            Progress = progress;
            TotalMilestones = totalMilestones;
            ClaimedCount = claimedCount;
            ClaimableCount = claimableCount;
            UnclaimedSaltSilverReward = unclaimedSaltSilverReward;
            HasNextMilestone = hasNextMilestone;
            NextMilestone = nextMilestone;
            SummaryLineZh = summaryLineZh ?? string.Empty;
        }

        public RpgWorld500xCrossMapProgressSnapshot Progress { get; }
        public int TotalMilestones { get; }
        public int ClaimedCount { get; }
        public int ClaimableCount { get; }
        public int UnclaimedSaltSilverReward { get; }
        public bool HasNextMilestone { get; }
        public RpgWorld500xMilestoneStatus NextMilestone { get; }
        public string SummaryLineZh { get; }
    }

    /// <summary>
    /// Aggregates cross-map exploration completion across all four 500x-area maps
    /// (42 caches, 16 shortcuts, 24 field NPC rumors, 16 story spots, 12 bounties,
    /// 12 cartography milestones, and 4 Sworn regional factions) and idempotently
    /// awards global expedition milestone bonuses under rpg.return.world.milestone.*.
    /// </summary>
    public sealed class RpgWorld500xExpeditionMilestoneService
    {
        private static readonly RpgWorld500xExpeditionMilestoneDef[] MilestoneDefs =
        {
            new RpgWorld500xExpeditionMilestoneDef(
                "first_cache_sweep", "寻踪觅迹", "累计开启 10 处野外暗藏补给箱。",
                RpgWorld500xMilestoneMetric.LootedCaches, 10, 60),
            new RpgWorld500xExpeditionMilestoneDef(
                "master_scavenger", "踏遍秘藏", "开启四境全部 42 处暗藏补给与遗物箱。",
                RpgWorld500xMilestoneMetric.LootedCaches, 42, 220),
            new RpgWorld500xExpeditionMilestoneDef(
                "pathfinder_shortcuts", "逢山开路", "打通 8 条大世界可解锁捷径通道。",
                RpgWorld500xMilestoneMetric.UnlockedShortcuts, 8, 90),
            new RpgWorld500xExpeditionMilestoneDef(
                "rumor_network", "四境耳目", "向 12 位野外行商或哨探打听见闻传闻。",
                RpgWorld500xMilestoneMetric.HeardRumors, 12, 80),
            new RpgWorld500xExpeditionMilestoneDef(
                "chronicle_keeper", "盐道行纪", "勘察 8 处四境风物轶事点。",
                RpgWorld500xMilestoneMetric.InspectedStorySpots, 8, 90),
            new RpgWorld500xExpeditionMilestoneDef(
                "bounty_veteran", "赏格老手", "累计领取 6 项区域野外悬赏赏金。",
                RpgWorld500xMilestoneMetric.ClaimedBounties, 6, 120),
            new RpgWorld500xExpeditionMilestoneDef(
                "cartography_pioneer", "山河初绘", "在四境累计领取 4 项堪舆探索里程碑。",
                RpgWorld500xMilestoneMetric.ClaimedCartographyMilestones, 4, 100),
            new RpgWorld500xExpeditionMilestoneDef(
                "four_regions_sworn", "四境生死托付", "声井、赤盐港、白水渡与矿营声望皆达生死托付。",
                RpgWorld500xMilestoneMetric.SwornFactions, 4, 300)
        };

        private readonly Func<string, int> _getInt;
        private readonly Action<string, int> _setInt;
        private readonly Action<RpgWorld500xExpeditionMilestoneDef> _onMilestoneUnlocked;

        public RpgWorld500xExpeditionMilestoneService(
            Func<string, int> getInt = null,
            Action<string, int> setInt = null,
            Action<RpgWorld500xExpeditionMilestoneDef> onMilestoneUnlocked = null)
        {
            _getInt = getInt ?? (_ => 0);
            _setInt = setInt;
            _onMilestoneUnlocked = onMilestoneUnlocked;
        }

        public static IReadOnlyList<RpgWorld500xExpeditionMilestoneDef> AllMilestones => MilestoneDefs;

        public RpgWorld500xCrossMapProgressSnapshot ComputeCrossMapProgress()
        {
            int lootedCaches = 0;
            int totalCaches = 0;
            int unlockedShortcuts = 0;
            int totalShortcuts = 0;
            int heardRumors = 0;
            int totalFieldNpcs = 0;
            int inspectedStorySpots = 0;
            int totalStorySpots = 0;
            int claimedBounties = 0;
            int totalBounties = 0;
            int claimedCartography = 0;
            int totalCartography = 0;
            int swornFactions = 0;

            var repService = new RpgWorld500xRegionalReputationService(_getInt);
            IReadOnlyList<RpgExpandedMapSpec> specs = RpgExpandedWorldScale.AllSpecs;

            for (int s = 0; s < specs.Count; s++)
            {
                RpgWorldMapId mapId = specs[s].MapId;

                IReadOnlyList<RpgSupplyCacheDef> caches = RpgWorld500xContentCatalog.GetCaches(mapId);
                totalCaches += caches.Count;
                for (int i = 0; i < caches.Count; i++)
                {
                    if (_getInt(caches[i].MakinomKey) == 1)
                    {
                        lootedCaches++;
                    }
                }

                IReadOnlyList<RpgWorldShortcutDef> shortcuts = RpgWorld500xContentCatalog.GetShortcuts(mapId);
                totalShortcuts += shortcuts.Count;
                for (int i = 0; i < shortcuts.Count; i++)
                {
                    if (_getInt(shortcuts[i].MakinomKey) == 1)
                    {
                        unlockedShortcuts++;
                    }
                }

                IReadOnlyList<RpgFieldNpcDef> npcs = RpgWorld500xLivingWorldCatalog.GetFieldNpcs(mapId);
                totalFieldNpcs += npcs.Count;
                for (int i = 0; i < npcs.Count; i++)
                {
                    if (_getInt(npcs[i].RumorMakinomKey) == 1)
                    {
                        heardRumors++;
                    }
                }

                IReadOnlyList<RpgStorySpotDef> spots = RpgWorld500xLivingWorldCatalog.GetStorySpots(mapId);
                totalStorySpots += spots.Count;
                for (int i = 0; i < spots.Count; i++)
                {
                    if (_getInt(spots[i].MakinomKey) == 1)
                    {
                        inspectedStorySpots++;
                    }
                }

                IReadOnlyList<RpgWorld500xFieldBountyDef> bounties = RpgWorld500xBountyAndSupplyCatalog.GetBounties(mapId);
                totalBounties += bounties.Count;
                for (int i = 0; i < bounties.Count; i++)
                {
                    if (_getInt(bounties[i].MakinomKey) == 1)
                    {
                        claimedBounties++;
                    }
                }

                RpgWorld500xCartographyMilestone[] carto = RpgWorld500xDistrictCatalog.GetMilestones(mapId);
                totalCartography += carto.Length;
                for (int i = 0; i < carto.Length; i++)
                {
                    if (_getInt(carto[i].MakinomKey) == 1)
                    {
                        claimedCartography++;
                    }
                }

                if (repService.GetSnapshot(mapId).Rank == RpgRegionalReputationRank.Sworn)
                {
                    swornFactions++;
                }
            }

            return new RpgWorld500xCrossMapProgressSnapshot(
                lootedCaches, totalCaches,
                unlockedShortcuts, totalShortcuts,
                heardRumors, totalFieldNpcs,
                inspectedStorySpots, totalStorySpots,
                claimedBounties, totalBounties,
                claimedCartography, totalCartography,
                swornFactions, specs.Count);
        }

        public List<RpgWorld500xExpeditionMilestoneDef> EvaluateAndClaimAll(
            out RpgWorld500xCrossMapProgressSnapshot progress)
        {
            progress = ComputeCrossMapProgress();
            var newlyClaimed = new List<RpgWorld500xExpeditionMilestoneDef>();

            // A read-only progress view cannot persist a claim. Never emit a
            // reward callback for a claim that would remain claimable next time.
            if (_setInt == null)
            {
                return newlyClaimed;
            }

            for (int i = 0; i < MilestoneDefs.Length; i++)
            {
                RpgWorld500xExpeditionMilestoneDef def = MilestoneDefs[i];
                if (_getInt(def.MakinomKey) == 1)
                {
                    continue;
                }

                if (progress.GetMetricValue(def.Metric) >= def.TargetCount)
                {
                    _setInt(def.MakinomKey, 1);
                    _onMilestoneUnlocked?.Invoke(def);
                    newlyClaimed.Add(def);
                }
            }

            return newlyClaimed;
        }

        public List<RpgWorld500xMilestoneStatus> BuildStatusList(
            out RpgWorld500xCrossMapProgressSnapshot progress)
        {
            progress = ComputeCrossMapProgress();
            var list = new List<RpgWorld500xMilestoneStatus>(MilestoneDefs.Length);

            for (int i = 0; i < MilestoneDefs.Length; i++)
            {
                RpgWorld500xExpeditionMilestoneDef def = MilestoneDefs[i];
                int current = progress.GetMetricValue(def.Metric);
                bool claimed = _getInt(def.MakinomKey) == 1;
                list.Add(new RpgWorld500xMilestoneStatus(def, current, claimed));
            }

            return list;
        }

        public RpgWorld500xMilestoneSummary BuildMilestoneSummary()
        {
            List<RpgWorld500xMilestoneStatus> statuses = BuildStatusList(out RpgWorld500xCrossMapProgressSnapshot progress);
            int claimedCount = 0;
            int claimableCount = 0;
            int unclaimedSilver = 0;
            bool hasNext = false;
            RpgWorld500xMilestoneStatus bestNext = default;
            float bestRatio = -1f;

            for (int i = 0; i < statuses.Count; i++)
            {
                RpgWorld500xMilestoneStatus st = statuses[i];
                if (st.IsClaimed)
                {
                    claimedCount++;
                    continue;
                }

                if (st.IsSatisfied)
                {
                    claimableCount++;
                    unclaimedSilver += st.Definition.SaltSilverReward;
                    if (!hasNext || bestRatio < 1f)
                    {
                        hasNext = true;
                        bestNext = st;
                        bestRatio = 1f;
                    }

                    continue;
                }

                float ratio = st.Definition.TargetCount > 0
                    ? (float)st.CurrentCount / st.Definition.TargetCount
                    : 0f;
                if (!hasNext || ratio > bestRatio)
                {
                    hasNext = true;
                    bestNext = st;
                    bestRatio = ratio;
                }
            }

            string summary;
            if (claimableCount > 0)
            {
                summary = "【远征里程碑】已领 " + claimedCount + "/" + statuses.Count +
                          "｜待领 " + claimableCount + " 项（+" + unclaimedSilver + " 盐银）";
            }
            else if (hasNext)
            {
                summary = "【远征里程碑】已领 " + claimedCount + "/" + statuses.Count +
                          "｜最近目标「" + bestNext.Definition.TitleZh + "」" +
                          bestNext.CurrentCount + "/" + bestNext.Definition.TargetCount;
            }
            else
            {
                summary = "【远征里程碑】已全数达成（" + claimedCount + "/" + statuses.Count + "）";
            }

            return new RpgWorld500xMilestoneSummary(
                progress,
                statuses.Count,
                claimedCount,
                claimableCount,
                unclaimedSilver,
                hasNext,
                bestNext,
                summary);
        }
    }
}
