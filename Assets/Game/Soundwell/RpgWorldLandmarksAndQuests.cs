using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rpg.Soundwell
{
    public enum RpgWorldMapId
    {
        Soundwell = 1,
        RedSaltHarbor = 2,
        WhitewaterCrossing = 3,
        MineCamp = 4
    }

    public enum RpgMapMarkerCategory
    {
        MainStory = 1,
        LandmarkQuest = 2,
        FastTravelBeacon = 3,
        CampAndSupply = 4,
        LoreArchive = 5,
        DangerZone = 6
    }

    public enum RpgLandmarkQuestKind
    {
        InspectClue = 1,
        ActivateBeacon = 2,
        SalvageSupply = 3,
        ClearHazard = 4,
        SurveyRoute = 5,
        WitnessEcho = 6
    }

    public enum RpgLandmarkQuestState
    {
        Available = 0,
        InProgress = 1,
        Completed = 2
    }

    public readonly struct RpgExpandedMapSpec
    {
        public RpgExpandedMapSpec(
            RpgWorldMapId mapId,
            string displayNameEn,
            string displayNameZh,
            int legacyWidth,
            int legacyHeight,
            int expandedWidth,
            int expandedHeight,
            int streamingRadiusChunks)
        {
            MapId = mapId;
            DisplayNameEn = displayNameEn;
            DisplayNameZh = displayNameZh;
            LegacyWidth = legacyWidth;
            LegacyHeight = legacyHeight;
            ExpandedWidth = expandedWidth;
            ExpandedHeight = expandedHeight;
            StreamingRadiusChunks = streamingRadiusChunks;
        }

        public RpgWorldMapId MapId { get; }
        public string DisplayNameEn { get; }
        public string DisplayNameZh { get; }
        public int LegacyWidth { get; }
        public int LegacyHeight { get; }
        public int ExpandedWidth { get; }
        public int ExpandedHeight { get; }
        public int StreamingRadiusChunks { get; }

        public long LegacyTileArea => (long)LegacyWidth * LegacyHeight;
        public long ExpandedTileArea => (long)ExpandedWidth * ExpandedHeight;
        public float AreaMultiplier => LegacyTileArea > 0
            ? (float)ExpandedTileArea / LegacyTileArea
            : 0f;

        public Bounds BuildExpandedCameraBounds()
        {
            return RpgWorldMapScale.BuildCameraBounds(ExpandedWidth, ExpandedHeight);
        }
    }

    /// <summary>
    /// 500x-area world-scale targets (~22.4x width x ~22.4x height) for all four
    /// playable RPG maps so exploration spans genuine multi-minute districts
    /// backed by camera-windowed chunk streaming and landmark wayfinding.
    /// </summary>
    public static class RpgExpandedWorldScale
    {
        public const int SoundwellWidth = 2144;
        public const int SoundwellHeight = 1344;

        public const int RedSaltHarborWidth = 3136;
        public const int RedSaltHarborHeight = 1888;

        public const int WhitewaterCrossingWidth = 1792;
        public const int WhitewaterCrossingHeight = 992;

        public const int MineCampWidth = 2144;
        public const int MineCampHeight = 1440;

        public const int DefaultStreamingRadiusChunks = 3;

        private static readonly RpgExpandedMapSpec[] Specs =
        {
            new RpgExpandedMapSpec(
                RpgWorldMapId.Soundwell, "Soundwell Caverns", "声井地下群",
                RpgWorldMapScale.SoundwellMinWidth, RpgWorldMapScale.SoundwellMinHeight,
                SoundwellWidth, SoundwellHeight, DefaultStreamingRadiusChunks),
            new RpgExpandedMapSpec(
                RpgWorldMapId.RedSaltHarbor, "Red Salt Outer Harbor", "赤盐外港大港区",
                RpgWorldMapScale.RedSaltHarborMinWidth, RpgWorldMapScale.RedSaltHarborMinHeight,
                RedSaltHarborWidth, RedSaltHarborHeight, DefaultStreamingRadiusChunks),
            new RpgExpandedMapSpec(
                RpgWorldMapId.WhitewaterCrossing, "Whitewater Crossing", "白水渡跨江战区",
                RpgWorldMapScale.WhitewaterCrossingMinWidth, RpgWorldMapScale.WhitewaterCrossingMinHeight,
                WhitewaterCrossingWidth, WhitewaterCrossingHeight, DefaultStreamingRadiusChunks),
            new RpgExpandedMapSpec(
                RpgWorldMapId.MineCamp, "Red Salt Mine Camp", "赤盐矿营与地下泵房",
                96, 64,
                MineCampWidth, MineCampHeight, DefaultStreamingRadiusChunks)
        };

        public static IReadOnlyList<RpgExpandedMapSpec> AllSpecs => Specs;

        public static RpgExpandedMapSpec GetSpec(RpgWorldMapId mapId)
        {
            for (int i = 0; i < Specs.Length; i++)
            {
                if (Specs[i].MapId == mapId)
                    return Specs[i];
            }
            throw new ArgumentOutOfRangeException(nameof(mapId), mapId, "Unknown map id.");
        }
    }

    public readonly struct RpgMapLandmarkMarker
    {
        public RpgMapLandmarkMarker(
            string id,
            RpgWorldMapId mapId,
            string districtId,
            string titleEn,
            string titleZh,
            RpgMapMarkerCategory category,
            Vector2Int tile,
            float discoveryRadiusTiles,
            bool supportsFastTravel,
            string linkedQuestId = null)
        {
            Id = id;
            MapId = mapId;
            DistrictId = districtId;
            TitleEn = titleEn;
            TitleZh = titleZh;
            Category = category;
            Tile = tile;
            DiscoveryRadiusTiles = discoveryRadiusTiles;
            SupportsFastTravel = supportsFastTravel;
            LinkedQuestId = linkedQuestId ?? string.Empty;
        }

        public string Id { get; }
        public RpgWorldMapId MapId { get; }
        public string DistrictId { get; }
        public string TitleEn { get; }
        public string TitleZh { get; }
        public RpgMapMarkerCategory Category { get; }
        public Vector2Int Tile { get; }
        public float DiscoveryRadiusTiles { get; }
        public bool SupportsFastTravel { get; }
        public string LinkedQuestId { get; }
    }

    public readonly struct RpgLandmarkQuestDefinition
    {
        public RpgLandmarkQuestDefinition(
            string questId,
            RpgWorldMapId mapId,
            string markerId,
            string titleEn,
            string titleZh,
            string objectiveSummaryZh,
            RpgLandmarkQuestKind kind,
            Vector2Int targetTile,
            int requiredSteps,
            string rewardSummaryZh)
        {
            QuestId = questId;
            MapId = mapId;
            MarkerId = markerId;
            TitleEn = titleEn;
            TitleZh = titleZh;
            ObjectiveSummaryZh = objectiveSummaryZh;
            Kind = kind;
            TargetTile = targetTile;
            RequiredSteps = requiredSteps;
            RewardSummaryZh = rewardSummaryZh;
        }

        public string QuestId { get; }
        public RpgWorldMapId MapId { get; }
        public string MarkerId { get; }
        public string TitleEn { get; }
        public string TitleZh { get; }
        public string ObjectiveSummaryZh { get; }
        public RpgLandmarkQuestKind Kind { get; }
        public Vector2Int TargetTile { get; }
        public int RequiredSteps { get; }
        public string RewardSummaryZh { get; }
    }

    public readonly struct RpgWayfindingReading
    {
        public RpgWayfindingReading(string targetId, string titleZh, Vector2Int targetTile,
            float distanceTiles, string compassDirectionZh)
        {
            TargetId = targetId;
            TitleZh = titleZh;
            TargetTile = targetTile;
            DistanceTiles = distanceTiles;
            CompassDirectionZh = compassDirectionZh;
        }

        public string TargetId { get; }
        public string TitleZh { get; }
        public Vector2Int TargetTile { get; }
        public float DistanceTiles { get; }
        public string CompassDirectionZh { get; }
    }

    /// <summary>
    /// Authoritative catalog of 500x-scale marked locations and landmark quests
    /// across Soundwell, Red Salt Harbor, Whitewater Crossing, and Mine Camp.
    /// </summary>
    public static class RpgWorldLandmarkCatalog
    {
        private static readonly RpgMapLandmarkMarker[] SoundwellMarkers =
        {
            new RpgMapLandmarkMarker("sw.entry-shaft", RpgWorldMapId.Soundwell, "soundwell.entry",
                "Entry Shaft", "下井主入口", RpgMapMarkerCategory.FastTravelBeacon,
                new Vector2Int(224, 672), 18f, true, "q.sw.check-rope-rig"),
            new RpgMapLandmarkMarker("sw.old-wellhead", RpgWorldMapId.Soundwell, "soundwell.entry",
                "Old Wellhead Capstan", "旧井口绞盘", RpgMapMarkerCategory.LandmarkQuest,
                new Vector2Int(340, 740), 16f, false, "q.sw.grease-capstan"),
            new RpgMapLandmarkMarker("sw.echo-alcove", RpgWorldMapId.Soundwell, "soundwell.north-echo",
                "West Echo Alcove", "西侧回声壁龛", RpgMapMarkerCategory.LoreArchive,
                new Vector2Int(410, 960), 16f, false, "q.sw.record-wall-echo"),
            new RpgMapLandmarkMarker("sw.north-echo", RpgWorldMapId.Soundwell, "soundwell.north-echo",
                "North Echo Gallery", "北侧回声长廊", RpgMapMarkerCategory.FastTravelBeacon,
                new Vector2Int(880, 1080), 20f, true, "q.sw.map-north-resonance"),
            new RpgMapLandmarkMarker("sw.moss-cistern", RpgWorldMapId.Soundwell, "soundwell.north-echo",
                "Moss Cistern", "苔藓蓄水池", RpgMapMarkerCategory.CampAndSupply,
                new Vector2Int(1180, 1120), 16f, false, "q.sw.collect-clean-water"),
            new RpgMapLandmarkMarker("sw.sunken-drain", RpgWorldMapId.Soundwell, "soundwell.central-hall",
                "Sunken Drain Basin", "沉降排水渠", RpgMapMarkerCategory.DangerZone,
                new Vector2Int(680, 640), 18f, false, "q.sw.clear-drain-grate"),
            new RpgMapLandmarkMarker("sw.central-chamber", RpgWorldMapId.Soundwell, "soundwell.central-hall",
                "Central Bell Chamber", "中央钟岭主井厅", RpgMapMarkerCategory.FastTravelBeacon,
                new Vector2Int(1040, 660), 22f, true, "q.sw.survey-central-hall"),
            new RpgMapLandmarkMarker("sw.resonance-pillar", RpgWorldMapId.Soundwell, "soundwell.central-hall",
                "Bronze Resonance Pillar", "青铜共鸣石柱", RpgMapMarkerCategory.LoreArchive,
                new Vector2Int(1220, 760), 16f, false, "q.sw.measure-pillar-pitch"),
            new RpgMapLandmarkMarker("sw.broken-lift", RpgWorldMapId.Soundwell, "soundwell.central-hall",
                "Broken Lift Shaft", "断绳矿梯井", RpgMapMarkerCategory.LandmarkQuest,
                new Vector2Int(1360, 560), 16f, false, "q.sw.salvage-lift-pulley"),
            new RpgMapLandmarkMarker("sw.east-vault", RpgWorldMapId.Soundwell, "soundwell.east-vault",
                "Bronze Bell Pedestal", "东侧铜铃祭台", RpgMapMarkerCategory.MainStory,
                new Vector2Int(1790, 760), 20f, true, "q.sw.inspect-bronze-bell"),
            new RpgMapLandmarkMarker("sw.wet-cloth-ledge", RpgWorldMapId.Soundwell, "soundwell.east-vault",
                "Wet Cloth Drying Ledge", "湿布阴干台", RpgMapMarkerCategory.MainStory,
                new Vector2Int(1790, 580), 18f, false, "q.sw.prepare-damp-cloth"),
            new RpgMapLandmarkMarker("sw.miners-shelter", RpgWorldMapId.Soundwell, "soundwell.east-vault",
                "Miners' Emergency Shelter", "矿工避险石室", RpgMapMarkerCategory.CampAndSupply,
                new Vector2Int(1920, 880), 18f, false, "q.sw.check-shelter-lanterns"),
            new RpgMapLandmarkMarker("sw.west-gate-corridor", RpgWorldMapId.Soundwell, "soundwell.west-gate",
                "Iron Gate Lever Post", "西侧铁闸拉杆位", RpgMapMarkerCategory.MainStory,
                new Vector2Int(1030, 240), 18f, false, "q.sw.test-gate-counterweight"),
            new RpgMapLandmarkMarker("sw.rust-valve", RpgWorldMapId.Soundwell, "soundwell.west-gate",
                "Rust Valve Gallery", "锈蚀水阀廊道", RpgMapMarkerCategory.LandmarkQuest,
                new Vector2Int(760, 260), 16f, false, "q.sw.release-valve-pressure"),
            new RpgMapLandmarkMarker("sw.flooded-sluice", RpgWorldMapId.Soundwell, "soundwell.west-gate",
                "Flooded Sluice Room", "积水闸门间", RpgMapMarkerCategory.DangerZone,
                new Vector2Int(560, 220), 18f, false),
            new RpgMapLandmarkMarker("sw.deep-acoustic", RpgWorldMapId.Soundwell, "soundwell.extraction",
                "Deep Acoustic Cavern", "深层回声溶洞", RpgMapMarkerCategory.DangerZone,
                new Vector2Int(1420, 320), 20f, false, "q.sw.mark-beast-den"),
            new RpgMapLandmarkMarker("sw.extraction-hall", RpgWorldMapId.Soundwell, "soundwell.extraction",
                "South Extraction Hatch", "南侧撤退井口", RpgMapMarkerCategory.MainStory,
                new Vector2Int(1740, 240), 20f, true),
            new RpgMapLandmarkMarker("sw.rope-hoist", RpgWorldMapId.Soundwell, "soundwell.extraction",
                "Emergency Rope Hoist", "应急拉索吊架", RpgMapMarkerCategory.CampAndSupply,
                new Vector2Int(1920, 240), 16f, false)
        };

        private static readonly RpgLandmarkQuestDefinition[] SoundwellQuests =
        {
            new RpgLandmarkQuestDefinition("q.sw.check-rope-rig", RpgWorldMapId.Soundwell, "sw.entry-shaft",
                "Check Descent Rigging", "核验下井绳索", "检查井口三处承重锚钉与下井信号绳。",
                RpgLandmarkQuestKind.InspectClue, new Vector2Int(224, 672), 2, "解锁下井快速返程路标"),
            new RpgLandmarkQuestDefinition("q.sw.grease-capstan", RpgWorldMapId.Soundwell, "sw.old-wellhead",
                "Free Old Capstan", "疏通旧绞盘", "清理旧绞盘齿轮上的盐锈并补充机油。",
                RpgLandmarkQuestKind.SalvageSupply, new Vector2Int(340, 740), 2, "获得备用滑轮件"),
            new RpgLandmarkQuestDefinition("q.sw.record-wall-echo", RpgWorldMapId.Soundwell, "sw.echo-alcove",
                "Record Wall Echoes", "记录壁龛回声", "对比壁龛低语与清脆敲击的反射延迟。",
                RpgLandmarkQuestKind.WitnessEcho, new Vector2Int(410, 960), 2, "补全声学笔记"),
            new RpgLandmarkQuestDefinition("q.sw.map-north-resonance", RpgWorldMapId.Soundwell, "sw.north-echo",
                "Survey North Gallery", "测绘北廊回声区", "沿北侧长廊插下三处静音路标避开盲区。",
                RpgLandmarkQuestKind.SurveyRoute, new Vector2Int(880, 1080), 3, "解锁北廊快速传送点"),
            new RpgLandmarkQuestDefinition("q.sw.collect-clean-water", RpgWorldMapId.Soundwell, "sw.moss-cistern",
                "Gather Cistern Water", "取用蓄水池清水", "装满随身水壶供后续湿布与应急包扎使用。",
                RpgLandmarkQuestKind.SalvageSupply, new Vector2Int(1180, 1120), 1, "补给清水与湿布耐久"),
            new RpgLandmarkQuestDefinition("q.sw.clear-drain-grate", RpgWorldMapId.Soundwell, "sw.sunken-drain",
                "Clear Sunken Drain", "清理沉降排水栅", "移开堵塞栅格的碎石，降低积水涉水噪音。",
                RpgLandmarkQuestKind.ClearHazard, new Vector2Int(680, 640), 2, "主厅涉水噪声下降"),
            new RpgLandmarkQuestDefinition("q.sw.survey-central-hall", RpgWorldMapId.Soundwell, "sw.central-chamber",
                "Light Central Beacon", "点亮主厅应急风灯", "在主井厅四角固定防风信号灯确认撤离路线。",
                RpgLandmarkQuestKind.ActivateBeacon, new Vector2Int(1040, 660), 3, "解锁中央井厅传送点"),
            new RpgLandmarkQuestDefinition("q.sw.measure-pillar-pitch", RpgWorldMapId.Soundwell, "sw.resonance-pillar",
                "Measure Bronze Pillar", "核对青铜石柱音高", "记录旧矿工刻在柱身的警示刻痕与频率。",
                RpgLandmarkQuestKind.InspectClue, new Vector2Int(1220, 760), 2, "获得矿井旧档线索"),
            new RpgLandmarkQuestDefinition("q.sw.salvage-lift-pulley", RpgWorldMapId.Soundwell, "sw.broken-lift",
                "Salvage Lift Pulley", "回收断梯滑轮", "从废弃矿梯井厢拆下完好的黄铜滑轮。",
                RpgLandmarkQuestKind.SalvageSupply, new Vector2Int(1360, 560), 2, "获得修缮物资"),
            new RpgLandmarkQuestDefinition("q.sw.inspect-bronze-bell", RpgWorldMapId.Soundwell, "sw.east-vault",
                "Inspect Bronze Bell", "勘验东室铜铃", "检查铜铃裂纹与吊环，确认敲击传声范围。",
                RpgLandmarkQuestKind.InspectClue, new Vector2Int(1790, 760), 1, "推进声井试炼线索"),
            new RpgLandmarkQuestDefinition("q.sw.prepare-damp-cloth", RpgWorldMapId.Soundwell, "sw.wet-cloth-ledge",
                "Soak Damp Cloth", "浸润隔音湿布", "取用阴干台厚帆布并在渗水槽充分浸湿。",
                RpgLandmarkQuestKind.SalvageSupply, new Vector2Int(1790, 580), 1, "备齐闷铃湿布"),
            new RpgLandmarkQuestDefinition("q.sw.check-shelter-lanterns", RpgWorldMapId.Soundwell, "sw.miners-shelter",
                "Restock Shelter Lamps", "补给避险室风灯", "点亮避险室油灯并清点残留干粮与绷带。",
                RpgLandmarkQuestKind.ActivateBeacon, new Vector2Int(1920, 880), 2, "解锁东室休整营地"),
            new RpgLandmarkQuestDefinition("q.sw.test-gate-counterweight", RpgWorldMapId.Soundwell, "sw.west-gate-corridor",
                "Test Gate Counterweight", "检修铁闸配重", "确认铁闸拉杆锁扣顺畅且无卡死风险。",
                RpgLandmarkQuestKind.InspectClue, new Vector2Int(1030, 240), 2, "铁闸开启前置就绪"),
            new RpgLandmarkQuestDefinition("q.sw.release-valve-pressure", RpgWorldMapId.Soundwell, "sw.rust-valve",
                "Release Valve Pressure", "泄放水阀余压", "缓慢旋开锈蚀水阀避免突发水锤惊动借声兽。",
                RpgLandmarkQuestKind.ClearHazard, new Vector2Int(760, 260), 2, "降低暗廊异响风险"),
            new RpgLandmarkQuestDefinition("q.sw.mark-beast-den", RpgWorldMapId.Soundwell, "sw.deep-acoustic",
                "Mark Beast Patrol Den", "标记借声兽巢穴", "在深层溶洞口留下警示记号供后续队员规避。",
                RpgLandmarkQuestKind.SurveyRoute, new Vector2Int(1420, 320), 2, "小地图标出高危巡逻区")
        };

        private static readonly RpgMapLandmarkMarker[] RedSaltHarborMarkers =
        {
            new RpgMapLandmarkMarker("rsh.outer-berth", RpgWorldMapId.RedSaltHarbor, "harbor.outer-berth",
                "Outer Shallow Berth", "外港浅水泊位", RpgMapMarkerCategory.FastTravelBeacon,
                new Vector2Int(360, 540), 22f, true, "q.rsh.secure-tender-mooring"),
            new RpgMapLandmarkMarker("rsh.landing-check", RpgWorldMapId.RedSaltHarbor, "harbor.outer-berth",
                "Landing Tally Post", "登岸查验岗", RpgMapMarkerCategory.MainStory,
                new Vector2Int(490, 540), 20f, false, "q.rsh.verify-landing-pass"),
            new RpgMapLandmarkMarker("rsh.tide-gauge", RpgWorldMapId.RedSaltHarbor, "harbor.outer-berth",
                "Tide Gauge Tower", "潮位标尺塔", RpgMapMarkerCategory.LandmarkQuest,
                new Vector2Int(240, 760), 18f, false, "q.rsh.log-tide-window"),
            new RpgMapLandmarkMarker("rsh.quay-promenade", RpgWorldMapId.RedSaltHarbor, "harbor.quay-promenade",
                "Quay Promenade", "外港石码头长堤", RpgMapMarkerCategory.FastTravelBeacon,
                new Vector2Int(960, 560), 22f, true),
            new RpgMapLandmarkMarker("rsh.seran-letters", RpgWorldMapId.RedSaltHarbor, "harbor.quay-promenade",
                "Seran's Letter Post", "瑟兰代书摊", RpgMapMarkerCategory.MainStory,
                new Vector2Int(1120, 580), 20f, false, "q.rsh.sort-harbor-letters"),
            new RpgMapLandmarkMarker("rsh.customs-desk", RpgWorldMapId.RedSaltHarbor, "harbor.quay-promenade",
                "Customs Tally Desk", "税关核册台", RpgMapMarkerCategory.LoreArchive,
                new Vector2Int(1260, 760), 18f, false, "q.rsh.inspect-cargo-manifest"),
            new RpgMapLandmarkMarker("rsh.rope-warehouse", RpgWorldMapId.RedSaltHarbor, "harbor.quay-promenade",
                "Rope & Tar Warehouse", "缆绳沥青库", RpgMapMarkerCategory.CampAndSupply,
                new Vector2Int(840, 920), 18f, false, "q.rsh.gather-tar-hemp"),
            new RpgMapLandmarkMarker("rsh.repair-stall", RpgWorldMapId.RedSaltHarbor, "harbor.repair-yard",
                "Repair Stall Yard", "修船摊院落", RpgMapMarkerCategory.MainStory,
                new Vector2Int(1880, 670), 22f, true, "q.rsh.check-hull-planks"),
            new RpgMapLandmarkMarker("rsh.drydock-slipway", RpgWorldMapId.RedSaltHarbor, "harbor.repair-yard",
                "Drydock Timber Slipway", "干船坞木滑道", RpgMapMarkerCategory.LandmarkQuest,
                new Vector2Int(2080, 820), 18f, false, "q.rsh.inspect-slipway-winch"),
            new RpgMapLandmarkMarker("rsh.se-breakwater", RpgWorldMapId.RedSaltHarbor, "harbor.se-breakwater",
                "Southeast Breakwater", "东南防波堤", RpgMapMarkerCategory.FastTravelBeacon,
                new Vector2Int(2640, 500), 22f, true, "q.rsh.hear-fisher-rumors"),
            new RpgMapLandmarkMarker("rsh.foghorn-post", RpgWorldMapId.RedSaltHarbor, "harbor.se-breakwater",
                "Foghorn Signal Post", "雾角信号台", RpgMapMarkerCategory.LandmarkQuest,
                new Vector2Int(2920, 520), 18f, false, "q.rsh.tune-foghorn-reed"),
            new RpgMapLandmarkMarker("rsh.fisher-wharf", RpgWorldMapId.RedSaltHarbor, "harbor.se-breakwater",
                "Fisherfolk Net Wharf", "渔民晒网栈桥", RpgMapMarkerCategory.LoreArchive,
                new Vector2Int(2780, 780), 18f, false, "q.rsh.chart-reef-currents"),
            new RpgMapLandmarkMarker("rsh.salt-market", RpgWorldMapId.RedSaltHarbor, "harbor.salt-market",
                "Red Salt Market Plaza", "赤盐盐市广场", RpgMapMarkerCategory.MainStory,
                new Vector2Int(1700, 1260), 24f, true, "q.rsh.compare-salt-prices"),
            new RpgMapLandmarkMarker("rsh.salt-kiln", RpgWorldMapId.RedSaltHarbor, "harbor.salt-market",
                "Salt Kiln Terrace", "晒盐灶台区", RpgMapMarkerCategory.LandmarkQuest,
                new Vector2Int(1360, 1280), 18f, false, "q.rsh.check-kiln-brine"),
            new RpgMapLandmarkMarker("rsh.old-weighhouse", RpgWorldMapId.RedSaltHarbor, "harbor.salt-market",
                "Old Weighhouse", "旧盐秤公房", RpgMapMarkerCategory.LoreArchive,
                new Vector2Int(2020, 1320), 18f, false, "q.rsh.audit-old-weights"),
            new RpgMapLandmarkMarker("rsh.harbor-clinic", RpgWorldMapId.RedSaltHarbor, "harbor.salt-market",
                "Harbor Clinic Shelter", "港区伤病棚", RpgMapMarkerCategory.CampAndSupply,
                new Vector2Int(2340, 1180), 18f, false, "q.rsh.deliver-bandages"),
            new RpgMapLandmarkMarker("rsh.west-cargo-yard", RpgWorldMapId.RedSaltHarbor, "harbor.salt-market",
                "West Cargo Yard", "西货场栈棚", RpgMapMarkerCategory.DangerZone,
                new Vector2Int(640, 1340), 20f, false, "q.rsh.watch-patrol-shift"),
            new RpgMapLandmarkMarker("rsh.north-stone-gate", RpgWorldMapId.RedSaltHarbor, "harbor.mine-approach",
                "North Stone Gate", "北口石门坊", RpgMapMarkerCategory.LandmarkQuest,
                new Vector2Int(1180, 1600), 18f, false),
            new RpgMapLandmarkMarker("rsh.mine-checkpoint", RpgWorldMapId.RedSaltHarbor, "harbor.mine-approach",
                "Mine Road Checkpoint", "矿道盘查亭", RpgMapMarkerCategory.DangerZone,
                new Vector2Int(1920, 1600), 18f, false),
            new RpgMapLandmarkMarker("rsh.mine-camp-gate", RpgWorldMapId.RedSaltHarbor, "harbor.mine-approach",
                "Mine Camp Approach Gate", "矿营入山口", RpgMapMarkerCategory.MainStory,
                new Vector2Int(1568, 1660), 22f, true)
        };

        private static readonly RpgLandmarkQuestDefinition[] RedSaltHarborQuests =
        {
            new RpgLandmarkQuestDefinition("q.rsh.secure-tender-mooring", RpgWorldMapId.RedSaltHarbor, "rsh.outer-berth",
                "Secure Shallow Tender", "加固浅水小艇系缆", "核对泊位系缆桩与退潮留绳长度。",
                RpgLandmarkQuestKind.InspectClue, new Vector2Int(360, 540), 2, "确保返航小艇安全"),
            new RpgLandmarkQuestDefinition("q.rsh.verify-landing-pass", RpgWorldMapId.RedSaltHarbor, "rsh.landing-check",
                "Check Landing Notices", "核对登岸告示", "在登岸岗抄录最新入港管制与矿区通行条文。",
                RpgLandmarkQuestKind.InspectClue, new Vector2Int(490, 540), 2, "补全外港情报"),
            new RpgLandmarkQuestDefinition("q.rsh.log-tide-window", RpgWorldMapId.RedSaltHarbor, "rsh.tide-gauge",
                "Log Tide Window", "记录平潮窗口", "读取潮位标尺塔刻度并换算夜间离港窗口。",
                RpgLandmarkQuestKind.SurveyRoute, new Vector2Int(240, 760), 2, "获得潮汐时刻记录"),
            new RpgLandmarkQuestDefinition("q.rsh.sort-harbor-letters", RpgWorldMapId.RedSaltHarbor, "rsh.seran-letters",
                "Verify Dockside Mail", "核对代书家信", "协助瑟兰核对发往临潮与矿营的待寄家书清单。",
                RpgLandmarkQuestKind.InspectClue, new Vector2Int(1120, 580), 2, "解锁更多家书线索"),
            new RpgLandmarkQuestDefinition("q.rsh.inspect-cargo-manifest", RpgWorldMapId.RedSaltHarbor, "rsh.customs-desk",
                "Cross-Check Cargo Tally", "比对税关货册", "查看近三日矿营发出的盐包与劳工转运批号。",
                RpgLandmarkQuestKind.InspectClue, new Vector2Int(1260, 760), 2, "获得矿营转运证据"),
            new RpgLandmarkQuestDefinition("q.rsh.gather-tar-hemp", RpgWorldMapId.RedSaltHarbor, "rsh.rope-warehouse",
                "Collect Caulk & Hemp", "采购麻丝与沥青", "在缆绳库挑选防渗麻丝以备小艇应急修补。",
                RpgLandmarkQuestKind.SalvageSupply, new Vector2Int(840, 920), 2, "获得修船补给"),
            new RpgLandmarkQuestDefinition("q.rsh.check-hull-planks", RpgWorldMapId.RedSaltHarbor, "rsh.repair-stall",
                "Inspect Hull Planks", "查验修船木料", "在修船摊询问船底补板工艺与暗礁擦痕。",
                RpgLandmarkQuestKind.InspectClue, new Vector2Int(1880, 670), 2, "解锁修船摊传送点"),
            new RpgLandmarkQuestDefinition("q.rsh.inspect-slipway-winch", RpgWorldMapId.RedSaltHarbor, "rsh.drydock-slipway",
                "Check Drydock Winch", "勘察滑道绞车", "确认干船坞木滑道承重与夜间拖船动静。",
                RpgLandmarkQuestKind.SurveyRoute, new Vector2Int(2080, 820), 2, "记录外港拖船路线"),
            new RpgLandmarkQuestDefinition("q.rsh.hear-fisher-rumors", RpgWorldMapId.RedSaltHarbor, "rsh.se-breakwater",
                "Listen on Breakwater", "防波堤听风声", "向东南防波堤老渔户打听夜间雾海异响与船影。",
                RpgLandmarkQuestKind.WitnessEcho, new Vector2Int(2640, 500), 2, "解锁防波堤传送点"),
            new RpgLandmarkQuestDefinition("q.rsh.tune-foghorn-reed", RpgWorldMapId.RedSaltHarbor, "rsh.foghorn-post",
                "Inspect Foghorn Reed", "校准雾角簧片", "清理雾角铜管盐渍，记录不同吹响节奏的含义。",
                RpgLandmarkQuestKind.ActivateBeacon, new Vector2Int(2920, 520), 2, "掌握外港雾号信号"),
            new RpgLandmarkQuestDefinition("q.rsh.chart-reef-currents", RpgWorldMapId.RedSaltHarbor, "rsh.fisher-wharf",
                "Chart Reef Currents", "测绘暗礁流向", "依据晒网栈桥渔民口述标出东南暗礁安全航道。",
                RpgLandmarkQuestKind.SurveyRoute, new Vector2Int(2780, 780), 3, "更新海图安全航线"),
            new RpgLandmarkQuestDefinition("q.rsh.compare-salt-prices", RpgWorldMapId.RedSaltHarbor, "rsh.salt-market",
                "Survey Salt Market", "摸底赤盐行情", "走访盐市广场三家盐铺核实近期盐价与欠薪传闻。",
                RpgLandmarkQuestKind.InspectClue, new Vector2Int(1700, 1260), 3, "解锁盐市广场传送点"),
            new RpgLandmarkQuestDefinition("q.rsh.check-kiln-brine", RpgWorldMapId.RedSaltHarbor, "rsh.salt-kiln",
                "Inspect Brine Kilns", "查看盐灶卤水", "核查灶台排水沟是否混入矿井下层矿泥。",
                RpgLandmarkQuestKind.InspectClue, new Vector2Int(1360, 1280), 2, "获得矿水渗透佐证"),
            new RpgLandmarkQuestDefinition("q.rsh.audit-old-weights", RpgWorldMapId.RedSaltHarbor, "rsh.old-weighhouse",
                "Audit Old Weighhouse", "核对旧盐秤底簿", "比对旧公房秤码与矿营交割斤两差额。",
                RpgLandmarkQuestKind.InspectClue, new Vector2Int(2020, 1320), 2, "补充账册交叉证据"),
            new RpgLandmarkQuestDefinition("q.rsh.deliver-bandages", RpgWorldMapId.RedSaltHarbor, "rsh.harbor-clinic",
                "Supply Harbor Clinic", "送药至港区伤病棚", "将随身干净纱布与清水送给受伤搬运工。",
                RpgLandmarkQuestKind.SalvageSupply, new Vector2Int(2340, 1180), 2, "解锁伤病棚休整点"),
            new RpgLandmarkQuestDefinition("q.rsh.watch-patrol-shift", RpgWorldMapId.RedSaltHarbor, "rsh.west-cargo-yard",
                "Time Cargo Patrols", "记录货场换岗间隔", "观察西货场巡逻换班节奏以规划北上潜行路线。",
                RpgLandmarkQuestKind.SurveyRoute, new Vector2Int(640, 1340), 2, "标记北门安全空档")
        };

        private static readonly RpgMapLandmarkMarker[] WhitewaterMarkers =
        {
            new RpgMapLandmarkMarker("ww.west-camp", RpgWorldMapId.WhitewaterCrossing, "whitewater.west-bank",
                "West Bank Forward Camp", "西岸前线营地", RpgMapMarkerCategory.FastTravelBeacon,
                new Vector2Int(240, 496), 20f, true, "q.ww.rally-west-camp"),
            new RpgMapLandmarkMarker("ww.supply-wagons", RpgWorldMapId.WhitewaterCrossing, "whitewater.west-bank",
                "Supply Wagon Park", "西岸辎重车阵", RpgMapMarkerCategory.CampAndSupply,
                new Vector2Int(160, 680), 18f, false, "q.ww.check-bridge-timber"),
            new RpgMapLandmarkMarker("ww.west-brazier-south", RpgWorldMapId.WhitewaterCrossing, "whitewater.west-bank",
                "West South Brazier", "西岸南侧火盆", RpgMapMarkerCategory.LandmarkQuest,
                new Vector2Int(420, 380), 16f, false, "q.ww.stoke-west-braziers"),
            new RpgMapLandmarkMarker("ww.west-brazier-north", RpgWorldMapId.WhitewaterCrossing, "whitewater.west-bank",
                "West North Brazier", "西岸北侧火盆", RpgMapMarkerCategory.LandmarkQuest,
                new Vector2Int(420, 610), 16f, false),
            new RpgMapLandmarkMarker("ww.cable-anchor", RpgWorldMapId.WhitewaterCrossing, "whitewater.west-bank",
                "Pontoon Cable Anchor", "浮桥铁索主锚", RpgMapMarkerCategory.MainStory,
                new Vector2Int(470, 496), 18f, true, "q.ww.brace-cable-anchor"),
            new RpgMapLandmarkMarker("ww.ferry-pier", RpgWorldMapId.WhitewaterCrossing, "whitewater.west-bank",
                "Old Ferryman Pier", "旧摆渡石埠", RpgMapMarkerCategory.LoreArchive,
                new Vector2Int(340, 220), 18f, false, "q.ww.recover-ferry-log"),
            new RpgMapLandmarkMarker("ww.sandbar-outpost", RpgWorldMapId.WhitewaterCrossing, "whitewater.west-bank",
                "North Sandbar Outpost", "北沙洲哨位", RpgMapMarkerCategory.DangerZone,
                new Vector2Int(360, 820), 18f, false, "q.ww.scout-north-shoal"),
            new RpgMapLandmarkMarker("ww.bridgehead-defense", RpgWorldMapId.WhitewaterCrossing, "whitewater.pontoon",
                "West Bridgehead Line", "西桥头防线", RpgMapMarkerCategory.MainStory,
                new Vector2Int(580, 496), 20f, false, "q.ww.hold-bridgehead-line"),
            new RpgMapLandmarkMarker("ww.central-sluice-raft", RpgWorldMapId.WhitewaterCrossing, "whitewater.pontoon",
                "Central Sluice Raft", "江心浮筏中段", RpgMapMarkerCategory.FastTravelBeacon,
                new Vector2Int(896, 496), 20f, true, "q.ww.clear-driftwood-jam"),
            new RpgMapLandmarkMarker("ww.east-span-barricade", RpgWorldMapId.WhitewaterCrossing, "whitewater.pontoon",
                "East Span Barricade", "东桥段拒马阵", RpgMapMarkerCategory.DangerZone,
                new Vector2Int(1180, 496), 20f, false, "q.ww.breach-east-barricade"),
            new RpgMapLandmarkMarker("ww.shattered-piles", RpgWorldMapId.WhitewaterCrossing, "whitewater.east-bank",
                "Shattered Pier Piles", "残断旧桥墩", RpgMapMarkerCategory.LoreArchive,
                new Vector2Int(1360, 320), 18f, false, "q.ww.mark-collapsed-span"),
            new RpgMapLandmarkMarker("ww.east-brazier-south", RpgWorldMapId.WhitewaterCrossing, "whitewater.east-bank",
                "East South Brazier", "东岸南侧火盆", RpgMapMarkerCategory.LandmarkQuest,
                new Vector2Int(1380, 380), 16f, false, "q.ww.light-east-signal"),
            new RpgMapLandmarkMarker("ww.far-bank-front", RpgWorldMapId.WhitewaterCrossing, "whitewater.east-bank",
                "Far Bank Bridgehead", "东岸桥头阵地", RpgMapMarkerCategory.MainStory,
                new Vector2Int(1440, 496), 20f, true),
            new RpgMapLandmarkMarker("ww.river-watchtower", RpgWorldMapId.WhitewaterCrossing, "whitewater.east-bank",
                "East River Watchtower", "东岸观江哨塔", RpgMapMarkerCategory.LandmarkQuest,
                new Vector2Int(1560, 740), 18f, false, "q.ww.survey-far-bank"),
            new RpgMapLandmarkMarker("ww.far-supply-depot", RpgWorldMapId.WhitewaterCrossing, "whitewater.east-bank",
                "East Supply Depot", "东岸弹药补给棚", RpgMapMarkerCategory.CampAndSupply,
                new Vector2Int(1620, 480), 18f, false, "q.ww.salvage-east-crates"),
            new RpgMapLandmarkMarker("ww.retreat-trailhead", RpgWorldMapId.WhitewaterCrossing, "whitewater.east-bank",
                "Retreat Trailhead", "东岸撤离山口", RpgMapMarkerCategory.FastTravelBeacon,
                new Vector2Int(1660, 260), 20f, true)
        };

        private static readonly RpgLandmarkQuestDefinition[] WhitewaterQuests =
        {
            new RpgLandmarkQuestDefinition("q.ww.rally-west-camp", RpgWorldMapId.WhitewaterCrossing, "ww.west-camp",
                "Rally West Camp", "整编西岸渡江队列", "核对西岸前线营地伤员与工兵编组。",
                RpgLandmarkQuestKind.InspectClue, new Vector2Int(240, 496), 2, "解锁西岸营地传送点"),
            new RpgLandmarkQuestDefinition("q.ww.check-bridge-timber", RpgWorldMapId.WhitewaterCrossing, "ww.supply-wagons",
                "Check Bridge Planks", "清点备用桥板", "确认车阵内的备换松木板与铁钉数量。",
                RpgLandmarkQuestKind.SalvageSupply, new Vector2Int(160, 680), 2, "获得修桥补给"),
            new RpgLandmarkQuestDefinition("q.ww.stoke-west-braziers", RpgWorldMapId.WhitewaterCrossing, "ww.west-brazier-south",
                "Stoke West Braziers", "添旺西岸引航火盆", "为南北两座火盆补充松脂照亮桥头雾面。",
                RpgLandmarkQuestKind.ActivateBeacon, new Vector2Int(420, 380), 2, "扩大桥头视野"),
            new RpgLandmarkQuestDefinition("q.ww.brace-cable-anchor", RpgWorldMapId.WhitewaterCrossing, "ww.cable-anchor",
                "Brace Cable Anchor", "加固主缆铁锚", "紧固浮桥主缆楔块，防止急流冲击脱锚。",
                RpgLandmarkQuestKind.InspectClue, new Vector2Int(470, 496), 2, "稳固浮桥主索"),
            new RpgLandmarkQuestDefinition("q.ww.recover-ferry-log", RpgWorldMapId.WhitewaterCrossing, "ww.ferry-pier",
                "Recover Ferry Log", "寻回旧渡口水文簿", "在旧石埠木箱里找回记录汛期流速的水文簿。",
                RpgLandmarkQuestKind.InspectClue, new Vector2Int(340, 220), 1, "补充白水渡背景档案"),
            new RpgLandmarkQuestDefinition("q.ww.scout-north-shoal", RpgWorldMapId.WhitewaterCrossing, "ww.sandbar-outpost",
                "Scout North Sandbar", "侦察北沙洲浅滩", "查明敌方是否可从北侧浅滩涉水包抄。",
                RpgLandmarkQuestKind.SurveyRoute, new Vector2Int(360, 820), 2, "标注北岸侧翼警戒"),
            new RpgLandmarkQuestDefinition("q.ww.hold-bridgehead-line", RpgWorldMapId.WhitewaterCrossing, "ww.bridgehead-defense",
                "Hold Bridgehead", "稳住西桥头掩体", "架设木盾掩体并标定桥面火力交叉点。",
                RpgLandmarkQuestKind.ClearHazard, new Vector2Int(580, 496), 2, "提升桥头防御稳固度"),
            new RpgLandmarkQuestDefinition("q.ww.clear-driftwood-jam", RpgWorldMapId.WhitewaterCrossing, "ww.central-sluice-raft",
                "Clear Driftwood Jam", "清理江心浮木卡阻", "撬开卡在江心浮筏底部的枯木以防断桥。",
                RpgLandmarkQuestKind.ClearHazard, new Vector2Int(896, 496), 2, "解锁江心浮筏传送点"),
            new RpgLandmarkQuestDefinition("q.ww.breach-east-barricade", RpgWorldMapId.WhitewaterCrossing, "ww.east-span-barricade",
                "Clear East Barricade", "拆除东段拒马", "拆开东桥段铁丝与木拒马打通渡江通道。",
                RpgLandmarkQuestKind.ClearHazard, new Vector2Int(1180, 496), 2, "打通东岸登陆口"),
            new RpgLandmarkQuestDefinition("q.ww.mark-collapsed-span", RpgWorldMapId.WhitewaterCrossing, "ww.shattered-piles",
                "Mark Collapsed Span", "标定旧断桥暗桩", "记录旧桥墩水下暗桩供撤退船只避让。",
                RpgLandmarkQuestKind.SurveyRoute, new Vector2Int(1360, 320), 2, "更新江面障碍标记"),
            new RpgLandmarkQuestDefinition("q.ww.light-east-signal", RpgWorldMapId.WhitewaterCrossing, "ww.east-brazier-south",
                "Light East Beacon", "点燃东岸信号火盆", "向西岸发出已控制东岸桥头的回信号。",
                RpgLandmarkQuestKind.ActivateBeacon, new Vector2Int(1380, 380), 1, "解锁东岸桥头传送点"),
            new RpgLandmarkQuestDefinition("q.ww.survey-far-bank", RpgWorldMapId.WhitewaterCrossing, "ww.river-watchtower",
                "Survey Far Bank", "登塔眺望东岸纵深", "从观江哨塔核对撤离山口与追兵动向。",
                RpgLandmarkQuestKind.SurveyRoute, new Vector2Int(1560, 740), 2, "显示东岸全境标点"),
            new RpgLandmarkQuestDefinition("q.ww.salvage-east-crates", RpgWorldMapId.WhitewaterCrossing, "ww.far-supply-depot",
                "Salvage East Supplies", "回收东岸补给箱", "清点并带走东岸补给棚里的伤药与信号弹。",
                RpgLandmarkQuestKind.SalvageSupply, new Vector2Int(1620, 480), 2, "获得渡江补给物资")
        };

        private static readonly RpgMapLandmarkMarker[] MineCampMarkers =
        {
            new RpgMapLandmarkMarker("mc.south-gate", RpgWorldMapId.MineCamp, "minecamp.south-gate",
                "Mine South Gate", "矿营南门入口", RpgMapMarkerCategory.FastTravelBeacon,
                new Vector2Int(420, 220), 20f, true, "q.mc.check-gate-pass"),
            new RpgMapLandmarkMarker("mc.quarantine-post", RpgWorldMapId.MineCamp, "minecamp.south-gate",
                "Quarantine Checkpost", "入营检疫岗", RpgMapMarkerCategory.LandmarkQuest,
                new Vector2Int(260, 340), 18f, false, "q.mc.read-quarantine-board"),
            new RpgMapLandmarkMarker("mc.escort-barracks", RpgWorldMapId.MineCamp, "minecamp.patrol-checkpost",
                "Escort Barracks", "护矿队营房", RpgMapMarkerCategory.DangerZone,
                new Vector2Int(760, 360), 20f, false, "q.mc.observe-escort-roster"),
            new RpgMapLandmarkMarker("mc.patrol-tower", RpgWorldMapId.MineCamp, "minecamp.patrol-checkpost",
                "Patrol Watchtower", "外围巡逻哨楼", RpgMapMarkerCategory.LandmarkQuest,
                new Vector2Int(1140, 260), 18f, false, "q.mc.map-tower-sightlines"),
            new RpgMapLandmarkMarker("mc.supply-ledger", RpgWorldMapId.MineCamp, "minecamp.supply-yard",
                "Supply Ledger Office", "矿营物资账房", RpgMapMarkerCategory.MainStory,
                new Vector2Int(1560, 340), 20f, true, "q.mc.check-supply-ledger"),
            new RpgMapLandmarkMarker("mc.salt-coal-yard", RpgWorldMapId.MineCamp, "minecamp.supply-yard",
                "Salt & Coal Yard", "盐煤露天堆场", RpgMapMarkerCategory.CampAndSupply,
                new Vector2Int(1860, 300), 18f, false, "q.mc.salvage-lamp-oil"),
            new RpgMapLandmarkMarker("mc.tool-forge", RpgWorldMapId.MineCamp, "minecamp.supply-yard",
                "Tool Forge Shed", "矿具铁匠棚", RpgMapMarkerCategory.LandmarkQuest,
                new Vector2Int(1920, 560), 18f, false, "q.mc.borrow-wrench-set"),
            new RpgMapLandmarkMarker("mc.shift-corridor", RpgWorldMapId.MineCamp, "minecamp.shift-corridor",
                "Shift Change Corridor", "换班通道闸口", RpgMapMarkerCategory.FastTravelBeacon,
                new Vector2Int(960, 680), 20f, true, "q.mc.time-shift-bell"),
            new RpgMapLandmarkMarker("mc.miner-dormitory", RpgWorldMapId.MineCamp, "minecamp.shift-corridor",
                "Miner Dormitory Row", "矿工宿舍排屋", RpgMapMarkerCategory.LoreArchive,
                new Vector2Int(480, 680), 18f, false, "q.mc.deliver-dorm-letters"),
            new RpgMapLandmarkMarker("mc.canteen-kitchen", RpgWorldMapId.MineCamp, "minecamp.shift-corridor",
                "Canteen Steam Kitchen", "大灶蒸汽伙房", RpgMapMarkerCategory.CampAndSupply,
                new Vector2Int(320, 880), 18f, false, "q.mc.check-water-ration"),
            new RpgMapLandmarkMarker("mc.ore-turntable", RpgWorldMapId.MineCamp, "minecamp.shift-corridor",
                "Ore Cart Turntable", "矿车转盘枢纽", RpgMapMarkerCategory.LandmarkQuest,
                new Vector2Int(1420, 740), 18f, false, "q.mc.unlock-rail-switch"),
            new RpgMapLandmarkMarker("mc.vent-shaft-north", RpgWorldMapId.MineCamp, "minecamp.upper-gallery",
                "North Ventilation Shaft", "北侧通风竖井", RpgMapMarkerCategory.LandmarkQuest,
                new Vector2Int(540, 1220), 18f, false, "q.mc.test-air-draft"),
            new RpgMapLandmarkMarker("mc.upper-evac-gallery", RpgWorldMapId.MineCamp, "minecamp.upper-gallery",
                "Upper Evacuation Gallery", "上层疏散通道", RpgMapMarkerCategory.FastTravelBeacon,
                new Vector2Int(980, 1240), 22f, true, "q.mc.clear-evac-barricade"),
            new RpgMapLandmarkMarker("mc.hoist-control", RpgWorldMapId.MineCamp, "minecamp.upper-gallery",
                "Hoist Control Room", "提升机控制室", RpgMapMarkerCategory.MainStory,
                new Vector2Int(1360, 1220), 18f, false, "q.mc.verify-hoist-brake"),
            new RpgMapLandmarkMarker("mc.main-pump-hall", RpgWorldMapId.MineCamp, "minecamp.pump-hall",
                "Underground Main Pump Hall", "地下主泵房大厅", RpgMapMarkerCategory.MainStory,
                new Vector2Int(1720, 1080), 22f, true, "q.mc.inspect-main-pump"),
            new RpgMapLandmarkMarker("mc.pressure-alcove", RpgWorldMapId.MineCamp, "minecamp.pump-hall",
                "Pressure Gauge Alcove", "水压表读数室", RpgMapMarkerCategory.LoreArchive,
                new Vector2Int(1920, 960), 16f, false, "q.mc.read-three-hour-window"),
            new RpgMapLandmarkMarker("mc.drainage-sluice", RpgWorldMapId.MineCamp, "minecamp.pump-hall",
                "Drainage Sluice Gate", "下层排水闸门", RpgMapMarkerCategory.DangerZone,
                new Vector2Int(1940, 1260), 18f, false),
            new RpgMapLandmarkMarker("mc.lower-seam-refuge", RpgWorldMapId.MineCamp, "minecamp.pump-hall",
                "Lower Seam Refuge", "下层矿脉避难洞", RpgMapMarkerCategory.CampAndSupply,
                new Vector2Int(1540, 1320), 18f, false)
        };

        private static readonly RpgLandmarkQuestDefinition[] MineCampQuests =
        {
            new RpgLandmarkQuestDefinition("q.mc.check-gate-pass", RpgWorldMapId.MineCamp, "mc.south-gate",
                "Verify South Gate Access", "查验南门通行规则", "核对矿营南门出入登记牌与夜禁时辰。",
                RpgLandmarkQuestKind.InspectClue, new Vector2Int(420, 220), 2, "解锁矿营南门传送点"),
            new RpgLandmarkQuestDefinition("q.mc.read-quarantine-board", RpgWorldMapId.MineCamp, "mc.quarantine-post",
                "Read Quarantine Notices", "抄录检疫与伤病名单", "核对检疫岗张贴的矿工留营与病休记录。",
                RpgLandmarkQuestKind.InspectClue, new Vector2Int(260, 340), 2, "补充矿工去向线索"),
            new RpgLandmarkQuestDefinition("q.mc.observe-escort-roster", RpgWorldMapId.MineCamp, "mc.escort-barracks",
                "Track Escort Patrols", "摸清护矿队巡逻表", "在不惊动守卫的情况下记录护矿队换班时刻。",
                RpgLandmarkQuestKind.SurveyRoute, new Vector2Int(760, 360), 2, "标记营房警戒窗口"),
            new RpgLandmarkQuestDefinition("q.mc.map-tower-sightlines", RpgWorldMapId.MineCamp, "mc.patrol-tower",
                "Map Tower Sightlines", "测绘哨楼照明盲区", "标出探照风灯覆盖死角供夜间撤离使用。",
                RpgLandmarkQuestKind.SurveyRoute, new Vector2Int(1140, 260), 2, "更新撤离潜行路线"),
            new RpgLandmarkQuestDefinition("q.mc.check-supply-ledger", RpgWorldMapId.MineCamp, "mc.supply-ledger",
                "Audit Camp Ledger", "核对矿营物资总账", "比对口粮、煤油与下层矿工实有人数差额。",
                RpgLandmarkQuestKind.InspectClue, new Vector2Int(1560, 340), 2, "解锁账房传送点"),
            new RpgLandmarkQuestDefinition("q.mc.salvage-lamp-oil", RpgWorldMapId.MineCamp, "mc.salt-coal-yard",
                "Collect Lamp Oil", "取用防爆矿灯油", "从堆场角棚补充备用矿灯油与火石。",
                RpgLandmarkQuestKind.SalvageSupply, new Vector2Int(1860, 300), 1, "矿灯照明补给充足"),
            new RpgLandmarkQuestDefinition("q.mc.borrow-wrench-set", RpgWorldMapId.MineCamp, "mc.tool-forge",
                "Acquire Heavy Wrenches", "借取大号闸阀扳手", "在铁匠棚找到适配主泵房泄压阀的扳手。",
                RpgLandmarkQuestKind.SalvageSupply, new Vector2Int(1920, 560), 2, "获得停泵检修工具"),
            new RpgLandmarkQuestDefinition("q.mc.time-shift-bell", RpgWorldMapId.MineCamp, "mc.shift-corridor",
                "Time Shift Bell", "核对换班钟声间隔", "记录换班通道闸口开放与关闭的准确时长。",
                RpgLandmarkQuestKind.WitnessEcho, new Vector2Int(960, 680), 2, "解锁换班通道传送点"),
            new RpgLandmarkQuestDefinition("q.mc.deliver-dorm-letters", RpgWorldMapId.MineCamp, "mc.miner-dormitory",
                "Check Miner Dorms", "走访宿舍排屋", "核对矿工床位牌并寻找熟悉下层矿道的工友。",
                RpgLandmarkQuestKind.InspectClue, new Vector2Int(480, 680), 2, "获得下层矿道口信"),
            new RpgLandmarkQuestDefinition("q.mc.check-water-ration", RpgWorldMapId.MineCamp, "mc.canteen-kitchen",
                "Check Kitchen Rations", "核查伙房口粮配给", "了解大灶每日送往下层矿道的饮水与干粮数。",
                RpgLandmarkQuestKind.SalvageSupply, new Vector2Int(320, 880), 2, "解锁伙房补给点"),
            new RpgLandmarkQuestDefinition("q.mc.unlock-rail-switch", RpgWorldMapId.MineCamp, "mc.ore-turntable",
                "Free Rail Turntable", "疏通矿车转盘道岔", "清理卡石使上层疏散矿车可平滑转向。",
                RpgLandmarkQuestKind.ClearHazard, new Vector2Int(1420, 740), 2, "打通矿车快速通道"),
            new RpgLandmarkQuestDefinition("q.mc.test-air-draft", RpgWorldMapId.MineCamp, "mc.vent-shaft-north",
                "Test Vent Draft", "测验通风竖井气流", "确认北侧竖井风向可安全导排下层潮气。",
                RpgLandmarkQuestKind.SurveyRoute, new Vector2Int(540, 1220), 2, "确认疏散通风安全"),
            new RpgLandmarkQuestDefinition("q.mc.clear-evac-barricade", RpgWorldMapId.MineCamp, "mc.upper-evac-gallery",
                "Clear Evac Gallery", "清理上层疏散道杂物", "移开堆放木箱确保 3 人及以上队伍顺畅通过。",
                RpgLandmarkQuestKind.ClearHazard, new Vector2Int(980, 1240), 2, "解锁上层疏散道传送点"),
            new RpgLandmarkQuestDefinition("q.mc.verify-hoist-brake", RpgWorldMapId.MineCamp, "mc.hoist-control",
                "Verify Hoist Brake", "校验提升机制动器", "检查控制室制动闸瓦，防止满载撤离溜车。",
                RpgLandmarkQuestKind.ActivateBeacon, new Vector2Int(1360, 1220), 2, "保障撤离提升机安全"),
            new RpgLandmarkQuestDefinition("q.mc.inspect-main-pump", RpgWorldMapId.MineCamp, "mc.main-pump-hall",
                "Inspect Main Pump", "勘验地下主泵机组", "核对主泵停机开关与沈梨约定的三小时排水窗口。",
                RpgLandmarkQuestKind.InspectClue, new Vector2Int(1720, 1080), 2, "解锁主泵房传送点"),
            new RpgLandmarkQuestDefinition("q.mc.read-three-hour-window", RpgWorldMapId.MineCamp, "mc.pressure-alcove",
                "Log 3-Hour Flood Curve", "记录三小时涨水曲线", "抄录水压表历史读数确认安全停泵上限。",
                RpgLandmarkQuestKind.InspectClue, new Vector2Int(1920, 960), 2, "完善停泵方案依据")
        };

        public static IReadOnlyList<RpgMapLandmarkMarker> GetMarkers(RpgWorldMapId mapId)
        {
            switch (mapId)
            {
                case RpgWorldMapId.Soundwell: return SoundwellMarkers;
                case RpgWorldMapId.RedSaltHarbor: return RedSaltHarborMarkers;
                case RpgWorldMapId.WhitewaterCrossing: return WhitewaterMarkers;
                case RpgWorldMapId.MineCamp: return MineCampMarkers;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mapId), mapId, "Unknown map id.");
            }
        }

        public static IReadOnlyList<RpgLandmarkQuestDefinition> GetQuests(RpgWorldMapId mapId)
        {
            switch (mapId)
            {
                case RpgWorldMapId.Soundwell: return SoundwellQuests;
                case RpgWorldMapId.RedSaltHarbor: return RedSaltHarborQuests;
                case RpgWorldMapId.WhitewaterCrossing: return WhitewaterQuests;
                case RpgWorldMapId.MineCamp: return MineCampQuests;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mapId), mapId, "Unknown map id.");
            }
        }

        public static bool TryGetMarker(RpgWorldMapId mapId, string markerId,
            out RpgMapLandmarkMarker marker)
        {
            if (!string.IsNullOrEmpty(markerId))
            {
                IReadOnlyList<RpgMapLandmarkMarker> list = GetMarkers(mapId);
                for (int i = 0; i < list.Count; i++)
                {
                    if (string.Equals(list[i].Id, markerId, StringComparison.Ordinal))
                    {
                        marker = list[i];
                        return true;
                    }
                }
            }
            marker = default;
            return false;
        }

        public static bool TryGetQuest(RpgWorldMapId mapId, string questId,
            out RpgLandmarkQuestDefinition quest)
        {
            if (!string.IsNullOrEmpty(questId))
            {
                IReadOnlyList<RpgLandmarkQuestDefinition> list = GetQuests(mapId);
                for (int i = 0; i < list.Count; i++)
                {
                    if (string.Equals(list[i].QuestId, questId, StringComparison.Ordinal))
                    {
                        quest = list[i];
                        return true;
                    }
                }
            }
            quest = default;
            return false;
        }
    }

    /// <summary>
    /// Tracks discovered landmarks, unlocked fast-travel beacons, landmark quest
    /// progress, and live wayfinding compass readings on a 500x-scale map.
    /// </summary>
    public sealed class RpgMapExplorationTracker
    {
        public RpgWorldMapId MapId { get; }
        private readonly IReadOnlyList<RpgMapLandmarkMarker> markers;
        private readonly IReadOnlyList<RpgLandmarkQuestDefinition> quests;
        private readonly HashSet<string> discoveredMarkers = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> questProgress = new Dictionary<string, int>(StringComparer.Ordinal);
        private string trackedQuestId = string.Empty;
        private string trackedMarkerId = string.Empty;

        public RpgMapExplorationTracker(RpgWorldMapId mapId)
        {
            MapId = mapId;
            markers = RpgWorldLandmarkCatalog.GetMarkers(mapId);
            quests = RpgWorldLandmarkCatalog.GetQuests(mapId);
        }

        public int TotalMarkerCount => markers.Count;
        public int DiscoveredMarkerCount => discoveredMarkers.Count;
        public int TotalQuestCount => quests.Count;
        public string TrackedQuestId => trackedQuestId;
        public string TrackedMarkerId => trackedMarkerId;

        public int CompletedQuestCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < quests.Count; i++)
                {
                    if (GetQuestState(quests[i].QuestId) == RpgLandmarkQuestState.Completed)
                        count++;
                }
                return count;
            }
        }

        public bool IsMarkerDiscovered(string markerId)
        {
            return !string.IsNullOrEmpty(markerId) && discoveredMarkers.Contains(markerId);
        }

        public bool TryDiscoverMarker(string markerId)
        {
            if (!RpgWorldLandmarkCatalog.TryGetMarker(MapId, markerId, out _))
                return false;
            return discoveredMarkers.Add(markerId);
        }

        public int DiscoverMarkersInRange(Vector2 playerTile)
        {
            if (float.IsNaN(playerTile.x) || float.IsInfinity(playerTile.x) ||
                float.IsNaN(playerTile.y) || float.IsInfinity(playerTile.y))
                return 0;

            int newlyDiscovered = 0;
            for (int i = 0; i < markers.Count; i++)
            {
                RpgMapLandmarkMarker marker = markers[i];
                if (discoveredMarkers.Contains(marker.Id))
                    continue;
                float dx = playerTile.x - marker.Tile.x;
                float dy = playerTile.y - marker.Tile.y;
                float radius = Mathf.Max(1f, marker.DiscoveryRadiusTiles);
                if (dx * dx + dy * dy <= radius * radius)
                {
                    discoveredMarkers.Add(marker.Id);
                    newlyDiscovered++;
                }
            }
            return newlyDiscovered;
        }

        public bool TryGetFastTravelDestination(string markerId, out Vector2Int destination)
        {
            destination = default;
            if (!IsMarkerDiscovered(markerId) ||
                !RpgWorldLandmarkCatalog.TryGetMarker(MapId, markerId, out RpgMapLandmarkMarker marker) ||
                !marker.SupportsFastTravel)
                return false;
            destination = marker.Tile;
            return true;
        }

        public int GetQuestProgress(string questId)
        {
            if (string.IsNullOrEmpty(questId))
                return 0;
            return questProgress.TryGetValue(questId, out int steps) ? steps : 0;
        }

        public RpgLandmarkQuestState GetQuestState(string questId)
        {
            if (!RpgWorldLandmarkCatalog.TryGetQuest(MapId, questId, out RpgLandmarkQuestDefinition quest))
                return RpgLandmarkQuestState.Available;
            int steps = GetQuestProgress(questId);
            if (steps <= 0) return RpgLandmarkQuestState.Available;
            return steps >= quest.RequiredSteps
                ? RpgLandmarkQuestState.Completed
                : RpgLandmarkQuestState.InProgress;
        }

        public bool TryAdvanceQuest(string questId, int deltaSteps = 1)
        {
            if (deltaSteps <= 0 ||
                !RpgWorldLandmarkCatalog.TryGetQuest(MapId, questId, out RpgLandmarkQuestDefinition quest))
                return false;
            int current = GetQuestProgress(questId);
            if (current >= quest.RequiredSteps)
                return false;
            int next = current + deltaSteps;
            if (next > quest.RequiredSteps)
                next = quest.RequiredSteps;
            questProgress[questId] = next;
            discoveredMarkers.Add(quest.MarkerId);
            return true;
        }

        public bool TrySetTrackedQuest(string questId)
        {
            if (!RpgWorldLandmarkCatalog.TryGetQuest(MapId, questId, out RpgLandmarkQuestDefinition quest))
                return false;
            trackedQuestId = quest.QuestId;
            trackedMarkerId = quest.MarkerId;
            return true;
        }

        public bool TrySetTrackedMarker(string markerId)
        {
            if (!RpgWorldLandmarkCatalog.TryGetMarker(MapId, markerId, out RpgMapLandmarkMarker marker))
                return false;
            trackedMarkerId = marker.Id;
            trackedQuestId = marker.LinkedQuestId;
            return true;
        }

        public bool TryGetWayfindingReading(Vector2 fromTile, out RpgWayfindingReading reading)
        {
            reading = default;
            if (float.IsNaN(fromTile.x) || float.IsInfinity(fromTile.x) ||
                float.IsNaN(fromTile.y) || float.IsInfinity(fromTile.y))
                return false;

            if (!string.IsNullOrEmpty(trackedQuestId) &&
                RpgWorldLandmarkCatalog.TryGetQuest(MapId, trackedQuestId, out RpgLandmarkQuestDefinition quest) &&
                GetQuestState(quest.QuestId) != RpgLandmarkQuestState.Completed)
            {
                reading = BuildReading(quest.QuestId, quest.TitleZh, fromTile, quest.TargetTile);
                return true;
            }

            if (!string.IsNullOrEmpty(trackedMarkerId) &&
                RpgWorldLandmarkCatalog.TryGetMarker(MapId, trackedMarkerId, out RpgMapLandmarkMarker marker))
            {
                reading = BuildReading(marker.Id, marker.TitleZh, fromTile, marker.Tile);
                return true;
            }

            if (TryFindNearestUndiscoveredMarker(fromTile, out RpgMapLandmarkMarker nearest, out _))
            {
                reading = BuildReading(nearest.Id, nearest.TitleZh, fromTile, nearest.Tile);
                return true;
            }

            return false;
        }

        public bool TryFindNearestUndiscoveredMarker(Vector2 fromTile,
            out RpgMapLandmarkMarker nearest, out float distanceTiles)
        {
            nearest = default;
            distanceTiles = 0f;
            if (float.IsNaN(fromTile.x) || float.IsInfinity(fromTile.x) ||
                float.IsNaN(fromTile.y) || float.IsInfinity(fromTile.y))
                return false;

            float bestSq = float.PositiveInfinity;
            bool found = false;
            for (int i = 0; i < markers.Count; i++)
            {
                RpgMapLandmarkMarker marker = markers[i];
                if (discoveredMarkers.Contains(marker.Id))
                    continue;
                float dx = marker.Tile.x - fromTile.x;
                float dy = marker.Tile.y - fromTile.y;
                float distSq = dx * dx + dy * dy;
                if (distSq < bestSq)
                {
                    bestSq = distSq;
                    nearest = marker;
                    found = true;
                }
            }

            if (!found)
                return false;
            distanceTiles = Mathf.Sqrt(bestSq);
            return true;
        }

        public bool TrySave(Action<string, int> setInt)
        {
            if (setInt == null)
                return false;
            for (int i = 0; i < markers.Count; i++)
            {
                string id = markers[i].Id;
                setInt(MarkerDiscoveryKey(id), discoveredMarkers.Contains(id) ? 1 : 0);
            }
            for (int i = 0; i < quests.Count; i++)
            {
                string qid = quests[i].QuestId;
                setInt(QuestProgressKey(qid), GetQuestProgress(qid));
            }
            return true;
        }

        public bool TryLoad(Func<string, int> getInt)
        {
            if (getInt == null)
                return false;
            discoveredMarkers.Clear();
            questProgress.Clear();
            for (int i = 0; i < markers.Count; i++)
            {
                string id = markers[i].Id;
                if (getInt(MarkerDiscoveryKey(id)) == 1)
                    discoveredMarkers.Add(id);
            }
            for (int i = 0; i < quests.Count; i++)
            {
                RpgLandmarkQuestDefinition quest = quests[i];
                int raw = getInt(QuestProgressKey(quest.QuestId));
                if (raw > 0)
                {
                    int clamped = raw > quest.RequiredSteps ? quest.RequiredSteps : raw;
                    questProgress[quest.QuestId] = clamped;
                    discoveredMarkers.Add(quest.MarkerId);
                }
            }
            return true;
        }

        public static string MarkerDiscoveryKey(string markerId)
        {
            return "rpg.return.world.marker." + markerId;
        }

        public static string QuestProgressKey(string questId)
        {
            return "rpg.return.world.quest." + questId;
        }

        private static RpgWayfindingReading BuildReading(string id, string titleZh,
            Vector2 fromTile, Vector2Int targetTile)
        {
            float dx = targetTile.x - fromTile.x;
            float dy = targetTile.y - fromTile.y;
            float dist = Mathf.Sqrt(dx * dx + dy * dy);
            return new RpgWayfindingReading(id, titleZh, targetTile, dist,
                FormatCompassDirectionZh(dx, dy));
        }

        public static string FormatCompassDirectionZh(float dx, float dy)
        {
            if (float.IsNaN(dx) || float.IsInfinity(dx) ||
                float.IsNaN(dy) || float.IsInfinity(dy))
                return "当前位置";
            if (dx * dx + dy * dy < 0.25f)
                return "已抵达";
            float angleDeg = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
            if (angleDeg < 0f) angleDeg += 360f;
            if (angleDeg >= 337.5f || angleDeg < 22.5f) return "正东";
            if (angleDeg < 67.5f) return "东北";
            if (angleDeg < 112.5f) return "正北";
            if (angleDeg < 157.5f) return "西北";
            if (angleDeg < 202.5f) return "正西";
            if (angleDeg < 247.5f) return "西南";
            if (angleDeg < 292.5f) return "正南";
            return "东南";
        }
    }
}
