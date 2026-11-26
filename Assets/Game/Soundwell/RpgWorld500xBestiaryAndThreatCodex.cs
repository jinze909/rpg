using System;
using System.Collections.Generic;

namespace Rpg.Soundwell
{
    public enum RpgWorld500xThreatTier : byte
    {
        Skirmisher = 1,
        ElitePatrol = 2,
        HazardBoss = 3
    }

    public readonly struct RpgWorld500xThreatProfileDef
    {
        public RpgWorld500xThreatProfileDef(
            string profileId,
            RpgWorldMapId mapId,
            string enemyNameZh,
            RpgWorld500xThreatTier tier,
            string weaknessHintZh,
            string recommendedSupplyId,
            string counterplayNoteZh,
            int codexStudySilverBonus)
        {
            ProfileId = profileId;
            MapId = mapId;
            EnemyNameZh = enemyNameZh;
            Tier = tier;
            WeaknessHintZh = weaknessHintZh;
            RecommendedSupplyId = recommendedSupplyId;
            CounterplayNoteZh = counterplayNoteZh;
            CodexStudySilverBonus = codexStudySilverBonus;
        }

        public string ProfileId { get; }
        public RpgWorldMapId MapId { get; }
        public string EnemyNameZh { get; }
        public RpgWorld500xThreatTier Tier { get; }
        public string WeaknessHintZh { get; }
        public string RecommendedSupplyId { get; }
        public string CounterplayNoteZh { get; }
        public int CodexStudySilverBonus { get; }
    }

    public readonly struct RpgWorld500xDangerZoneDossier
    {
        public RpgWorld500xDangerZoneDossier(
            RpgWorldMapId mapId,
            string markerId,
            RpgWorld500xThreatProfileDef profile,
            bool isStudied)
        {
            MapId = mapId;
            MarkerId = markerId;
            Profile = profile;
            IsStudied = isStudied;
        }

        public RpgWorldMapId MapId { get; }
        public string MarkerId { get; }
        public RpgWorld500xThreatProfileDef Profile { get; }
        public bool IsStudied { get; }
        public string CodexMakinomKey => "rpg.return.world.codex.threat." + MarkerId;
    }

    public readonly struct RpgWorld500xMapCodexSummary
    {
        public RpgWorld500xMapCodexSummary(
            RpgWorldMapId mapId,
            int totalDossiers,
            int studiedCount,
            int unstudiedCount,
            int remainingStudySilverBonus,
            IReadOnlyList<string> recommendedSupplyIds,
            string summaryZh)
        {
            MapId = mapId;
            TotalDossiers = totalDossiers;
            StudiedCount = studiedCount;
            UnstudiedCount = unstudiedCount;
            RemainingStudySilverBonus = remainingStudySilverBonus;
            RecommendedSupplyIds = recommendedSupplyIds ?? Array.Empty<string>();
            SummaryZh = summaryZh ?? string.Empty;
        }

        public RpgWorldMapId MapId { get; }
        public int TotalDossiers { get; }
        public int StudiedCount { get; }
        public int UnstudiedCount { get; }
        public int RemainingStudySilverBonus { get; }
        public IReadOnlyList<string> RecommendedSupplyIds { get; }
        public string SummaryZh { get; }
    }

    /// <summary>
    /// Curated regional bestiary and DangerZone tactical prep catalog across all
    /// four 500x-area maps (12 distinct regional threat archetypes).
    /// </summary>
    public static class RpgWorld500xBestiaryCatalog
    {
        private static readonly RpgWorld500xThreatProfileDef[] SoundwellProfiles =
        {
            new RpgWorld500xThreatProfileDef("sw_th_echo_stalker", RpgWorldMapId.Soundwell, "借声兽幼体",
                RpgWorld500xThreatTier.Skirmisher, "畏惧低频钝响，奔跑声会立刻暴露方位", "damp_cloth",
                "贴墙慢行并用湿布包裹敲击工具，趁回声停歇绕后破防。", 20),
            new RpgWorld500xThreatProfileDef("sw_th_bell_lurker", RpgWorldMapId.Soundwell, "钟壁伏影",
                RpgWorld500xThreatTier.ElitePatrol, "受强光照射时行动迟滞", "lamp_oil",
                "先在避险石室添满矿灯灯油，诱入开阔石阶后再集火。", 30),
            new RpgWorld500xThreatProfileDef("sw_th_deep_resonator", RpgWorldMapId.Soundwell, "深井共鸣体",
                RpgWorld500xThreatTier.HazardBoss, "蓄力共鸣期间核心暴露，打断可造成高额破衡", "gauze",
                "保留打断技能应对长鸣前摇，随身备足止血纱布防震荡余波。", 45)
        };

        private static readonly RpgWorld500xThreatProfileDef[] RedSaltHarborProfiles =
        {
            new RpgWorld500xThreatProfileDef("rsh_th_salt_smuggler", RpgWorldMapId.RedSaltHarbor, "私盐打手",
                RpgWorld500xThreatTier.Skirmisher, "雾天视野受限，单人脱队时易被突袭", "flare",
                "利用防波堤与货箱拐角逐个拉出，避免在盐市空地被包夹。", 22),
            new RpgWorld500xThreatProfileDef("rsh_th_brine_hound", RpgWorldMapId.RedSaltHarbor, "盐滩猎犬",
                RpgWorld500xThreatTier.ElitePatrol, "涉水涨潮时扑击速度明显下降", "gauze",
                "借外港涨潮漫堤拖慢其突进节奏，优先清掉吹哨驯犬人。", 32),
            new RpgWorld500xThreatProfileDef("rsh_th_quay_captain", RpgWorldMapId.RedSaltHarbor, "税关稽查长",
                RpgWorld500xThreatTier.HazardBoss, "重盾正面减伤高，侧翼破甲收益显著", "rations",
                "在泊位灯塔盲区抢占先手，破盾后连续压制其反击窗口。", 48)
        };

        private static readonly RpgWorld500xThreatProfileDef[] WhitewaterProfiles =
        {
            new RpgWorld500xThreatProfileDef("ww_th_river_raider", RpgWorldMapId.WhitewaterCrossing, "白水劫掠兵",
                RpgWorld500xThreatTier.Skirmisher, "江雾起时弓弩命中率骤降", "flare",
                "趁白水江雾横扫浮桥时快速压近，近身瓦解其远程火力。", 24),
            new RpgWorld500xThreatProfileDef("ww_th_pontoon_shield", RpgWorldMapId.WhitewaterCrossing, "浮桥重盾手",
                RpgWorld500xThreatTier.ElitePatrol, "在狭窄桥面转身缓慢", "iron_clamps",
                "先抢通南北便桥形成包抄，或以破防技击溃盾阵。", 34),
            new RpgWorld500xThreatProfileDef("ww_th_watch_commander", RpgWorldMapId.WhitewaterCrossing, "东岸哨塔督战队",
                RpgWorld500xThreatTier.HazardBoss, "依赖火盆与信号鼓协同增援", "gauze",
                "优先切断信号手，稳住东岸掩体再逐层清理残敌。", 50)
        };

        private static readonly RpgWorld500xThreatProfileDef[] MineCampProfiles =
        {
            new RpgWorld500xThreatProfileDef("mc_th_mine_enforcer", RpgWorldMapId.MineCamp, "护矿队巡逻班",
                RpgWorld500xThreatTier.Skirmisher, "哨楼风灯转向瞬间存在照明盲区", "lamp_oil",
                "贴着盐煤堆场阴影推进，趁蒸汽烟柱遮蔽脚步声突袭。", 25),
            new RpgWorld500xThreatProfileDef("mc_th_tunnel_sapper", RpgWorldMapId.MineCamp, "矿道爆破工兵",
                RpgWorld500xThreatTier.ElitePatrol, "装填引信期间防御大幅下降", "gauze",
                "第一时间打断投掷动作，切忌站桩吃满范围震荡。", 36),
            new RpgWorld500xThreatProfileDef("mc_th_pump_warden", RpgWorldMapId.MineCamp, "泵房铁甲监工",
                RpgWorld500xThreatTier.HazardBoss, "重锤挥空后硬直极长", "rations",
                "围绕主泵房立柱周旋，抓准重锤落地后的破绽反打。", 55)
        };

        public static IReadOnlyList<RpgWorld500xThreatProfileDef> GetProfiles(RpgWorldMapId mapId)
        {
            switch (mapId)
            {
                case RpgWorldMapId.Soundwell: return SoundwellProfiles;
                case RpgWorldMapId.RedSaltHarbor: return RedSaltHarborProfiles;
                case RpgWorldMapId.WhitewaterCrossing: return WhitewaterProfiles;
                case RpgWorldMapId.MineCamp: return MineCampProfiles;
                default: throw new ArgumentOutOfRangeException(nameof(mapId), mapId, "Unknown map id.");
            }
        }

        public static RpgWorld500xThreatProfileDef ResolveProfileForMarker(RpgWorldMapId mapId, string markerId)
        {
            IReadOnlyList<RpgWorld500xThreatProfileDef> profiles = GetProfiles(mapId);
            if (string.IsNullOrEmpty(markerId))
            {
                return profiles[0];
            }

            unchecked
            {
                int hash = 17;
                for (int i = 0; i < markerId.Length; i++)
                {
                    hash = (hash * 31) + markerId[i];
                }

                int index = Math.Abs(hash) % profiles.Count;
                return profiles[index];
            }
        }
    }

    /// <summary>
    /// Tracks DangerZone threat codex unlocks across all four 500x maps, granting
    /// idempotent study bonuses when a DangerZone is cleared or researched.
    /// </summary>
    public sealed class RpgWorld500xThreatCodexService
    {
        private readonly Func<string, int> _getInt;
        private readonly Action<string, int> _setInt;
        private readonly Action<RpgWorld500xDangerZoneDossier> _onDossierStudied;

        public RpgWorld500xThreatCodexService(
            Func<string, int> getInt = null,
            Action<string, int> setInt = null,
            Action<RpgWorld500xDangerZoneDossier> onDossierStudied = null)
        {
            _getInt = getInt ?? (_ => 0);
            _setInt = setInt ?? ((_, __) => { });
            _onDossierStudied = onDossierStudied;
        }

        public static string CodexKey(string markerId) => "rpg.return.world.codex.threat." + markerId;

        public bool IsStudied(string markerId) =>
            !string.IsNullOrEmpty(markerId) && _getInt(CodexKey(markerId)) == 1;

        public RpgWorld500xDangerZoneDossier GetDossier(RpgWorldMapId mapId, string markerId)
        {
            RpgWorld500xThreatProfileDef profile = RpgWorld500xBestiaryCatalog.ResolveProfileForMarker(mapId, markerId);
            return new RpgWorld500xDangerZoneDossier(mapId, markerId, profile, IsStudied(markerId));
        }

        public bool TryRecordDangerZoneStudied(
            RpgWorldMapId mapId,
            string markerId,
            out RpgWorld500xDangerZoneDossier dossier)
        {
            dossier = GetDossier(mapId, markerId);
            if (string.IsNullOrWhiteSpace(markerId) || dossier.IsStudied)
            {
                return false;
            }

            _setInt(CodexKey(markerId), 1);
            dossier = new RpgWorld500xDangerZoneDossier(mapId, markerId, dossier.Profile, true);
            _onDossierStudied?.Invoke(dossier);
            return true;
        }

        public List<RpgWorld500xDangerZoneDossier> BuildMapDossiers(RpgWorld500xMapLayout layout)
        {
            var result = new List<RpgWorld500xDangerZoneDossier>();
            if (layout == null)
            {
                return result;
            }

            for (int i = 0; i < layout.Markers.Count; i++)
            {
                RpgMapLandmarkMarker marker = layout.Markers[i];
                if (marker.Category != RpgMapMarkerCategory.DangerZone)
                {
                    continue;
                }

                result.Add(GetDossier(layout.MapId, marker.Id));
            }

            return result;
        }

        public List<RpgWorld500xDangerZoneDossier> FilterDossiers(
            RpgWorld500xMapLayout layout,
            bool unstudiedOnly = false,
            RpgWorld500xThreatTier? tierFilter = null)
        {
            List<RpgWorld500xDangerZoneDossier> all = BuildMapDossiers(layout);
            var filtered = new List<RpgWorld500xDangerZoneDossier>(all.Count);
            for (int i = 0; i < all.Count; i++)
            {
                RpgWorld500xDangerZoneDossier d = all[i];
                if (unstudiedOnly && d.IsStudied)
                {
                    continue;
                }

                if (tierFilter.HasValue && d.Profile.Tier != tierFilter.Value)
                {
                    continue;
                }

                filtered.Add(d);
            }

            return filtered;
        }

        public RpgWorld500xMapCodexSummary BuildMapCodexSummary(RpgWorld500xMapLayout layout)
        {
            if (layout == null)
            {
                return new RpgWorld500xMapCodexSummary(
                    RpgWorldMapId.Soundwell, 0, 0, 0, 0,
                    Array.Empty<string>(),
                    "【图鉴考据】暂无危险区图鉴");
            }

            List<RpgWorld500xDangerZoneDossier> dossiers = BuildMapDossiers(layout);
            int studied = 0;
            int remainingSilver = 0;
            var seenSupplies = new HashSet<string>(StringComparer.Ordinal);
            var recommendedSupplies = new List<string>();

            for (int i = 0; i < dossiers.Count; i++)
            {
                RpgWorld500xDangerZoneDossier d = dossiers[i];
                if (d.IsStudied)
                {
                    studied++;
                    continue;
                }

                remainingSilver += Math.Max(0, d.Profile.CodexStudySilverBonus);
                if (!string.IsNullOrEmpty(d.Profile.RecommendedSupplyId) &&
                    seenSupplies.Add(d.Profile.RecommendedSupplyId))
                {
                    recommendedSupplies.Add(d.Profile.RecommendedSupplyId);
                }
            }

            int total = dossiers.Count;
            int unstudied = Math.Max(0, total - studied);
            string summaryZh = unstudied > 0
                ? "【图鉴考据】已考据 " + studied + "/" + total + "｜待考据 " + unstudied + " 项｜剩余盐银 +" + remainingSilver
                : "【图鉴考据】已考据 " + studied + "/" + total + " · 本境异兽图鉴已全部收录";

            return new RpgWorld500xMapCodexSummary(
                layout.MapId,
                total,
                studied,
                unstudied,
                remainingSilver,
                recommendedSupplies,
                summaryZh);
        }
    }
}
