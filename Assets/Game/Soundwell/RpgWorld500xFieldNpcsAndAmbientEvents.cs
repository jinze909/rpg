using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rpg.Soundwell
{
    public enum RpgFieldNpcRole : byte
    {
        DistrictLocal = 1,
        RumorBroker = 2
    }

    public enum RpgAmbientMicroEventKind : byte
    {
        None = 0,
        EchoTremor = 1,
        TideSurge = 2,
        FogGust = 3,
        LanternShift = 4,
        KilnPlume = 5
    }

    public readonly struct RpgFieldNpcDef
    {
        public RpgFieldNpcDef(
            string id,
            RpgWorldMapId mapId,
            Vector2Int tile,
            string displayNameZh,
            RpgFieldNpcRole role,
            string ambientLineZh,
            string rumorTargetMarkerId,
            string rumorHintZh)
        {
            Id = id;
            MapId = mapId;
            Tile = tile;
            DisplayNameZh = displayNameZh;
            Role = role;
            AmbientLineZh = ambientLineZh;
            RumorTargetMarkerId = rumorTargetMarkerId ?? string.Empty;
            RumorHintZh = rumorHintZh ?? string.Empty;
        }

        public string Id { get; }
        public RpgWorldMapId MapId { get; }
        public Vector2Int Tile { get; }
        public string DisplayNameZh { get; }
        public RpgFieldNpcRole Role { get; }
        public string AmbientLineZh { get; }
        public string RumorTargetMarkerId { get; }
        public string RumorHintZh { get; }
        public string RumorMakinomKey => "rpg.return.world.rumor." + Id;
    }

    public readonly struct RpgStorySpotDef
    {
        public RpgStorySpotDef(
            string id,
            RpgWorldMapId mapId,
            Vector2Int tile,
            string titleZh,
            string descriptionZh)
        {
            Id = id;
            MapId = mapId;
            Tile = tile;
            TitleZh = titleZh;
            DescriptionZh = descriptionZh;
        }

        public string Id { get; }
        public RpgWorldMapId MapId { get; }
        public Vector2Int Tile { get; }
        public string TitleZh { get; }
        public string DescriptionZh { get; }
        public string MakinomKey => "rpg.return.world.lore." + Id;
    }

    public readonly struct RpgAmbientMicroEventZoneDef
    {
        public RpgAmbientMicroEventZoneDef(
            string id,
            RpgWorldMapId mapId,
            Vector2Int centerTile,
            float radiusTiles,
            RpgAmbientMicroEventKind kind,
            float cycleSeconds,
            float activeSeconds,
            float patrolSightMultiplier,
            float patrolNoiseMultiplier,
            float moveSpeedMultiplier,
            string captionZh)
        {
            Id = id;
            MapId = mapId;
            CenterTile = centerTile;
            RadiusTiles = radiusTiles;
            Kind = kind;
            CycleSeconds = cycleSeconds;
            ActiveSeconds = activeSeconds;
            PatrolSightMultiplier = patrolSightMultiplier;
            PatrolNoiseMultiplier = patrolNoiseMultiplier;
            MoveSpeedMultiplier = moveSpeedMultiplier;
            CaptionZh = captionZh;
        }

        public string Id { get; }
        public RpgWorldMapId MapId { get; }
        public Vector2Int CenterTile { get; }
        public float RadiusTiles { get; }
        public RpgAmbientMicroEventKind Kind { get; }
        public float CycleSeconds { get; }
        public float ActiveSeconds { get; }
        public float PatrolSightMultiplier { get; }
        public float PatrolNoiseMultiplier { get; }
        public float MoveSpeedMultiplier { get; }
        public string CaptionZh { get; }
    }

    public readonly struct RpgActiveAmbientState
    {
        public RpgActiveAmbientState(
            RpgAmbientMicroEventKind kind,
            string zoneId,
            float patrolSightMultiplier,
            float patrolNoiseMultiplier,
            float moveSpeedMultiplier,
            string captionZh)
        {
            Kind = kind;
            ZoneId = zoneId ?? string.Empty;
            PatrolSightMultiplier = patrolSightMultiplier;
            PatrolNoiseMultiplier = patrolNoiseMultiplier;
            MoveSpeedMultiplier = moveSpeedMultiplier;
            CaptionZh = captionZh ?? string.Empty;
        }

        public RpgAmbientMicroEventKind Kind { get; }
        public string ZoneId { get; }
        public float PatrolSightMultiplier { get; }
        public float PatrolNoiseMultiplier { get; }
        public float MoveSpeedMultiplier { get; }
        public string CaptionZh { get; }
        public bool IsActive => Kind != RpgAmbientMicroEventKind.None;

        public static RpgActiveAmbientState Neutral =>
            new RpgActiveAmbientState(RpgAmbientMicroEventKind.None, string.Empty, 1f, 1f, 1f, string.Empty);
    }

    public readonly struct RpgWorld500xLivingWorldProgressSummary
    {
        public RpgWorld500xLivingWorldProgressSummary(
            RpgWorldMapId mapId,
            int heardRumorCount,
            int totalRumorCount,
            int remainingRumorCount,
            int inspectedStorySpotCount,
            int totalStorySpotCount,
            int remainingStorySpotCount,
            int ambientZoneCount,
            string summaryZh)
        {
            MapId = mapId;
            HeardRumorCount = heardRumorCount;
            TotalRumorCount = totalRumorCount;
            RemainingRumorCount = remainingRumorCount;
            InspectedStorySpotCount = inspectedStorySpotCount;
            TotalStorySpotCount = totalStorySpotCount;
            RemainingStorySpotCount = remainingStorySpotCount;
            AmbientZoneCount = ambientZoneCount;
            SummaryZh = summaryZh ?? string.Empty;
        }

        public RpgWorldMapId MapId { get; }
        public int HeardRumorCount { get; }
        public int TotalRumorCount { get; }
        public int RemainingRumorCount { get; }
        public int InspectedStorySpotCount { get; }
        public int TotalStorySpotCount { get; }
        public int RemainingStorySpotCount { get; }
        public int AmbientZoneCount { get; }
        public string SummaryZh { get; }
    }

    /// <summary>
    /// Wave-2 500x world enrichment catalog: 24 interactive field NPCs & rumor
    /// brokers, 16 inspectable environmental storytelling spots, and 12 regional
    /// ambient micro-event zones across all four 500x-area maps.
    /// </summary>
    public static class RpgWorld500xLivingWorldCatalog
    {
        private static readonly RpgFieldNpcDef[] SoundwellNpcs =
        {
            new RpgFieldNpcDef("sw_npc_01", RpgWorldMapId.Soundwell, new Vector2Int(250, 690),
                "守井老索工", RpgFieldNpcRole.RumorBroker,
                "下井前先摸一摸缆绳干湿，潮气重的时候回声传得远。",
                "sw.old-wellhead", "旧井口绞盘那边还卡着备用滑轮，顺路可以去松一松。"),
            new RpgFieldNpcDef("sw_npc_02", RpgWorldMapId.Soundwell, new Vector2Int(620, 1060),
                "北廊听音学徒", RpgFieldNpcRole.DistrictLocal,
                "北侧长廊每走三十步就有一次低频反响，别在这儿猛跑。",
                "sw.moss-cistern", "往东走到苔藓蓄水池能打到干净清水，湿布干了就去蘸。"),
            new RpgFieldNpcDef("sw_npc_03", RpgWorldMapId.Soundwell, new Vector2Int(1080, 680),
                "测绘班抄录员", RpgFieldNpcRole.RumorBroker,
                "主井厅青铜石柱上的刻痕不是装饰，那是前人标出的安全频率。",
                "sw.resonance-pillar", "东北角的青铜共鸣石柱还没人核对完，去看看能补全笔记。"),
            new RpgFieldNpcDef("sw_npc_04", RpgWorldMapId.Soundwell, new Vector2Int(1840, 780),
                "东室值守工匠", RpgFieldNpcRole.DistrictLocal,
                "铜铃千万别干敲，先去南边阴干台把帆布浸透再裹上。",
                "sw.miners-shelter", "东室东北角有个矿工避险石室，点亮风灯就能休整。"),
            new RpgFieldNpcDef("sw_npc_05", RpgWorldMapId.Soundwell, new Vector2Int(820, 280),
                "闸门检修工", RpgFieldNpcRole.RumorBroker,
                "西侧铁闸配重沉得很，没卸掉水阀余压之前动静会惊动整片溶洞。",
                "sw.rust-valve", "先去西边锈蚀水阀廊道泄压，再去拉铁闸才稳妥。"),
            new RpgFieldNpcDef("sw_npc_06", RpgWorldMapId.Soundwell, new Vector2Int(1660, 260),
                "撤离井口接应员", RpgFieldNpcRole.DistrictLocal,
                "铁闸一开就赶紧往南侧撤退井口聚拢，吊架绳索已经挂好了。",
                "sw.rope-hoist", "东侧应急拉索吊架还备着最后一批干粮。")
        };

        private static readonly RpgFieldNpcDef[] RedSaltHarborNpcs =
        {
            new RpgFieldNpcDef("rsh_npc_01", RpgWorldMapId.RedSaltHarbor, new Vector2Int(420, 580),
                "泊位引水人", RpgFieldNpcRole.RumorBroker,
                "浅水小艇只能趁平潮靠岸，退潮后龙骨会擦着盐礁。",
                "rsh.tide-gauge", "西边潮位标尺塔写着今夜的准确平潮时刻。"),
            new RpgFieldNpcDef("rsh_npc_02", RpgWorldMapId.RedSaltHarbor, new Vector2Int(1020, 620),
                "码头代书小伙", RpgFieldNpcRole.DistrictLocal,
                "瑟兰每天都要把发往临潮和矿营的家信分拣两遍。",
                "rsh.customs-desk", "税关核册台那儿贴着最近三天的盐包与劳工转运号。"),
            new RpgFieldNpcDef("rsh_npc_03", RpgWorldMapId.RedSaltHarbor, new Vector2Int(1940, 700),
                "修船摊老木匠", RpgFieldNpcRole.RumorBroker,
                "这几艘驳船底板都有同样的暗礁擦痕，像是半夜摸黑靠港的。",
                "rsh.drydock-slipway", "去东边干船坞木滑道看看，昨晚有人拖过一条无牌船。"),
            new RpgFieldNpcDef("rsh_npc_04", RpgWorldMapId.RedSaltHarbor, new Vector2Int(2560, 540),
                "防波堤老渔户", RpgFieldNpcRole.RumorBroker,
                "东南雾角一响两短一长，就说明外海有官船靠过来了。",
                "rsh.foghorn-post", "防波堤最东头的雾角信号台铜管积了盐渍，去校准一下吧。"),
            new RpgFieldNpcDef("rsh_npc_05", RpgWorldMapId.RedSaltHarbor, new Vector2Int(1760, 1280),
                "赤盐行账房", RpgFieldNpcRole.DistrictLocal,
                "这月盐价压得狠，矿营那边欠了工钱，搬运工都在伤病棚歇着。",
                "rsh.harbor-clinic", "东边港区伤病棚正缺干净纱布，带点过去能帮大忙。"),
            new RpgFieldNpcDef("rsh_npc_06", RpgWorldMapId.RedSaltHarbor, new Vector2Int(1620, 1620),
                "矿道挑夫", RpgFieldNpcRole.DistrictLocal,
                "过了矿营入山口就归护矿队管了，进山前把灯油和干粮备齐。",
                "rsh.north-stone-gate", "西边北口石门坊能绕开盘查亭的正门视线。")
        };

        private static readonly RpgFieldNpcDef[] WhitewaterNpcs =
        {
            new RpgFieldNpcDef("ww_npc_01", RpgWorldMapId.WhitewaterCrossing, new Vector2Int(280, 520),
                "西岸工兵班长", RpgFieldNpcRole.RumorBroker,
                "浮桥主缆刚紧过一轮，可上游漂来的枯木随时会顶弯桥面。",
                "ww.supply-wagons", "西北辎重车阵还留着最后几块松木备板。"),
            new RpgFieldNpcDef("ww_npc_02", RpgWorldMapId.WhitewaterCrossing, new Vector2Int(220, 660),
                "旧渡口老船夫", RpgFieldNpcRole.DistrictLocal,
                "白水渡起江雾的时候，连对岸火盆都只剩一团橘红。",
                "ww.ferry-pier", "南边旧摆渡石埠的木箱里还压着历年水文簿。"),
            new RpgFieldNpcDef("ww_npc_03", RpgWorldMapId.WhitewaterCrossing, new Vector2Int(620, 496),
                "桥头盾手", RpgFieldNpcRole.DistrictLocal,
                "趁江雾吹过来的时候往前推进，对岸哨位看不清桥面动静。",
                "ww.central-sluice-raft", "江心浮筏中段卡了浮木，得有人过去撬开。"),
            new RpgFieldNpcDef("ww_npc_04", RpgWorldMapId.WhitewaterCrossing, new Vector2Int(940, 496),
                "浮桥检修兵", RpgFieldNpcRole.RumorBroker,
                "东桥段拒马后面就是东岸桥头，拆开拒马就能站稳脚跟。",
                "ww.east-span-barricade", "东桥段拒马阵守备严，先清理再过桥。"),
            new RpgFieldNpcDef("ww_npc_05", RpgWorldMapId.WhitewaterCrossing, new Vector2Int(1480, 520),
                "东岸先遣斥候", RpgFieldNpcRole.RumorBroker,
                "站上观江哨塔能把整条东岸撤离山口看得一清二楚。",
                "ww.river-watchtower", "东北方东岸观江哨塔视野最好，去登塔核对路线。"),
            new RpgFieldNpcDef("ww_npc_06", RpgWorldMapId.WhitewaterCrossing, new Vector2Int(1580, 440),
                "东岸伤员看护", RpgFieldNpcRole.DistrictLocal,
                "补给棚就在旁边，拿完伤药和信号弹就能向撤离山口汇合。",
                "ww.far-supply-depot", "东岸弹药补给棚里还有未开封的伤药箱。")
        };

        private static readonly RpgFieldNpcDef[] MineCampNpcs =
        {
            new RpgFieldNpcDef("mc_npc_01", RpgWorldMapId.MineCamp, new Vector2Int(460, 240),
                "南门检疫杂役", RpgFieldNpcRole.RumorBroker,
                "检疫岗布告栏上贴着病休矿工的去向，其实好些人被调去了下层。",
                "mc.quarantine-post", "西边入营检疫岗墙上能抄到真实留营名单。"),
            new RpgFieldNpcDef("mc_npc_02", RpgWorldMapId.MineCamp, new Vector2Int(880, 320),
                "夜班巡道工", RpgFieldNpcRole.RumorBroker,
                "外围哨楼的探照风灯每刻钟转一圈，摸清盲区就能贴墙过。",
                "mc.patrol-tower", "东边外围巡逻哨楼底下能看清全部照明死角。"),
            new RpgFieldNpcDef("mc_npc_03", RpgWorldMapId.MineCamp, new Vector2Int(1600, 380),
                "铁匠棚学徒", RpgFieldNpcRole.DistrictLocal,
                "主泵房泄压阀口径大，寻常小扳手拧不动，得拿铁匠棚的大号扳手。",
                "mc.tool-forge", "东北边矿具铁匠棚架子上挂着全套大号闸阀扳手。"),
            new RpgFieldNpcDef("mc_npc_04", RpgWorldMapId.MineCamp, new Vector2Int(1020, 720),
                "换班记录员", RpgFieldNpcRole.RumorBroker,
                "换班钟声一响，宿舍排屋和伙房之间的闸口会敞开五分钟。",
                "mc.miner-dormitory", "西边矿工宿舍排屋里有熟悉下层矿道的老工友。"),
            new RpgFieldNpcDef("mc_npc_05", RpgWorldMapId.MineCamp, new Vector2Int(1040, 1220),
                "提升机老技师", RpgFieldNpcRole.DistrictLocal,
                "疏散矿车满载上行前必须校验制动闸瓦，不然半坡溜车全完蛋。",
                "mc.hoist-control", "东边提升机控制室能检查制动器与配重状态。"),
            new RpgFieldNpcDef("mc_npc_06", RpgWorldMapId.MineCamp, new Vector2Int(1760, 1120),
                "泵房值班学徒", RpgFieldNpcRole.DistrictLocal,
                "沈梨算过，停泵最多只有三小时安全窗口，水压表读数室有历年曲线。",
                "mc.pressure-alcove", "东边水压表读数室墙上记着完整涨水曲线。")
        };

        private static readonly RpgStorySpotDef[] SoundwellStorySpots =
        {
            new RpgStorySpotDef("sw_lore_01", RpgWorldMapId.Soundwell, new Vector2Int(160, 720),
                "旧井壁粉笔字", "岩壁上留着矿工手写的粉笔字：『敲铃先浸布，莫在暗廊奔跑』。"),
            new RpgStorySpotDef("sw_lore_02", RpgWorldMapId.Soundwell, new Vector2Int(960, 1140),
                "风干湿布挂钩", "一排生锈铁钩仍挂着褪色帆布条，是旧班组留下的隔音标记。"),
            new RpgStorySpotDef("sw_lore_03", RpgWorldMapId.Soundwell, new Vector2Int(1160, 540),
                "主井厅青铜铭文", "铜柱底座刻着历年水位线与钟岭最初凿井的年份。"),
            new RpgStorySpotDef("sw_lore_04", RpgWorldMapId.Soundwell, new Vector2Int(1880, 520),
                "矿难避险刻痕", "石室角落刻着十七道短竖线，记录着当年困守石室的十七名矿工。")
        };

        private static readonly RpgStorySpotDef[] RedSaltHarborStorySpots =
        {
            new RpgStorySpotDef("rsh_lore_01", RpgWorldMapId.RedSaltHarbor, new Vector2Int(300, 680),
                "潮痕石碑", "泊位石碑刻着十年大潮最高水位，盐霜已爬满碑身。"),
            new RpgStorySpotDef("rsh_lore_02", RpgWorldMapId.RedSaltHarbor, new Vector2Int(1340, 680),
                "废旧船票木匣", "税关廊下积灰木匣里塞着数十张未盖返程章的矿工船票。"),
            new RpgStorySpotDef("rsh_lore_03", RpgWorldMapId.RedSaltHarbor, new Vector2Int(2700, 640),
                "防波堤祭海石堆", "渔户堆起的小石堆下压着写有失踪船员姓名的旧油布条。"),
            new RpgStorySpotDef("rsh_lore_04", RpgWorldMapId.RedSaltHarbor, new Vector2Int(1460, 1360),
                "盐市旧告示柱", "层层叠叠的旧盐价榜下，露出一张被撕去半角的欠薪讨要联名信。")
        };

        private static readonly RpgStorySpotDef[] WhitewaterStorySpots =
        {
            new RpgStorySpotDef("ww_lore_01", RpgWorldMapId.WhitewaterCrossing, new Vector2Int(380, 260),
                "旧渡口系船石", "被铁链磨出深沟的青石柱，见证了无数次逆流夜渡。"),
            new RpgStorySpotDef("ww_lore_02", RpgWorldMapId.WhitewaterCrossing, new Vector2Int(400, 780),
                "北滩浅水标杆", "倾斜的竹标杆上绑着褪色红布，用于雨季警示流沙暗槽。"),
            new RpgStorySpotDef("ww_lore_03", RpgWorldMapId.WhitewaterCrossing, new Vector2Int(1400, 280),
                "断桥纪念石冢", "东岸残墩旁垒着一小堆江石，纪念当年断桥阻击未归的工兵。"),
            new RpgStorySpotDef("ww_lore_04", RpgWorldMapId.WhitewaterCrossing, new Vector2Int(1640, 680),
                "哨塔焦木栏杆", "栏杆上残留着信号火把灼烧的炭痕与急促刻下的日期。")
        };

        private static readonly RpgStorySpotDef[] MineCampStorySpots =
        {
            new RpgStorySpotDef("mc_lore_01", RpgWorldMapId.MineCamp, new Vector2Int(300, 260),
                "矿营班次铁牌架", "半排铜牌已经蒙尘，背面刻着下层采掘队的班组编号。"),
            new RpgStorySpotDef("mc_lore_02", RpgWorldMapId.MineCamp, new Vector2Int(1780, 420),
                "盐煤堆场磅秤台", "锈蚀地磅旁丢着一本被盐水浸皱的运矿过磅副联。"),
            new RpgStorySpotDef("mc_lore_03", RpgWorldMapId.MineCamp, new Vector2Int(560, 760),
                "宿舍床梁归期刻痕", "床梁上密密麻麻刻着正字，每个正字旁都写着家人的小名。"),
            new RpgStorySpotDef("mc_lore_04", RpgWorldMapId.MineCamp, new Vector2Int(1820, 1040),
                "主泵房检修铜牌", "铜牌注明：『停泵超三时，下层巷道必淹，慎之又慎』。")
        };

        private static readonly RpgAmbientMicroEventZoneDef[] SoundwellAmbientZones =
        {
            new RpgAmbientMicroEventZoneDef("sw_amb_echo_north", RpgWorldMapId.Soundwell,
                new Vector2Int(880, 1080), 280f, RpgAmbientMicroEventKind.EchoTremor,
                40f, 10f, 1.0f, 1.30f, 1.0f, "声井回声震颤：奔跑声响放大"),
            new RpgAmbientMicroEventZoneDef("sw_amb_echo_central", RpgWorldMapId.Soundwell,
                new Vector2Int(1040, 660), 320f, RpgAmbientMicroEventKind.EchoTremor,
                45f, 10f, 1.0f, 1.25f, 1.0f, "主井厅钟壁共鸣：注意脚步声"),
            new RpgAmbientMicroEventZoneDef("sw_amb_lantern_west", RpgWorldMapId.Soundwell,
                new Vector2Int(760, 260), 240f, RpgAmbientMicroEventKind.LanternShift,
                36f, 12f, 0.85f, 1.0f, 1.0f, "暗廊风灯明暗交替：便于潜行")
        };

        private static readonly RpgAmbientMicroEventZoneDef[] RedSaltHarborAmbientZones =
        {
            new RpgAmbientMicroEventZoneDef("rsh_amb_tide_berth", RpgWorldMapId.RedSaltHarbor,
                new Vector2Int(700, 520), 420f, RpgAmbientMicroEventKind.TideSurge,
                48f, 12f, 1.0f, 1.0f, 0.88f, "外港涨潮漫堤：涉水行进略微减速"),
            new RpgAmbientMicroEventZoneDef("rsh_amb_fog_breakwater", RpgWorldMapId.RedSaltHarbor,
                new Vector2Int(2640, 560), 440f, RpgAmbientMicroEventKind.FogGust,
                42f, 14f, 0.75f, 1.0f, 1.0f, "东南海雾袭来：巡逻视野缩小"),
            new RpgAmbientMicroEventZoneDef("rsh_amb_lantern_market", RpgWorldMapId.RedSaltHarbor,
                new Vector2Int(1700, 1260), 500f, RpgAmbientMicroEventKind.LanternShift,
                50f, 15f, 0.90f, 1.0f, 1.0f, "盐市夜灯换油：街巷阴影加深")
        };

        private static readonly RpgAmbientMicroEventZoneDef[] WhitewaterAmbientZones =
        {
            new RpgAmbientMicroEventZoneDef("ww_amb_fog_pontoon", RpgWorldMapId.WhitewaterCrossing,
                new Vector2Int(896, 496), 440f, RpgAmbientMicroEventKind.FogGust,
                38f, 12f, 0.75f, 1.0f, 1.0f, "白水江雾横扫浮桥：借雾掩护前进"),
            new RpgAmbientMicroEventZoneDef("ww_amb_lantern_west", RpgWorldMapId.WhitewaterCrossing,
                new Vector2Int(340, 496), 260f, RpgAmbientMicroEventKind.LanternShift,
                44f, 12f, 0.90f, 1.0f, 1.0f, "西岸火盆风曳：桥头光影晃动"),
            new RpgAmbientMicroEventZoneDef("ww_amb_tide_east", RpgWorldMapId.WhitewaterCrossing,
                new Vector2Int(1440, 496), 280f, RpgAmbientMicroEventKind.TideSurge,
                46f, 10f, 1.0f, 1.0f, 0.90f, "急流拍岸溅湿桥板：注意脚下")
        };

        private static readonly RpgAmbientMicroEventZoneDef[] MineCampAmbientZones =
        {
            new RpgAmbientMicroEventZoneDef("mc_amb_kiln_yard", RpgWorldMapId.MineCamp,
                new Vector2Int(1680, 400), 340f, RpgAmbientMicroEventKind.KilnPlume,
                42f, 12f, 0.85f, 0.80f, 1.0f, "盐灶蒸汽烟柱飘散：掩盖脚步声"),
            new RpgAmbientMicroEventZoneDef("mc_amb_lantern_patrol", RpgWorldMapId.MineCamp,
                new Vector2Int(960, 320), 320f, RpgAmbientMicroEventKind.LanternShift,
                36f, 10f, 0.80f, 1.0f, 1.0f, "哨楼风灯转向：外围出现照明盲区"),
            new RpgAmbientMicroEventZoneDef("mc_amb_echo_pump", RpgWorldMapId.MineCamp,
                new Vector2Int(1720, 1080), 320f, RpgAmbientMicroEventKind.EchoTremor,
                48f, 12f, 1.0f, 0.85f, 1.0f, "地下主泵低鸣震动：掩盖近处动静")
        };

        public static IReadOnlyList<RpgFieldNpcDef> GetFieldNpcs(RpgWorldMapId mapId)
        {
            switch (mapId)
            {
                case RpgWorldMapId.Soundwell: return SoundwellNpcs;
                case RpgWorldMapId.RedSaltHarbor: return RedSaltHarborNpcs;
                case RpgWorldMapId.WhitewaterCrossing: return WhitewaterNpcs;
                case RpgWorldMapId.MineCamp: return MineCampNpcs;
                default: throw new ArgumentOutOfRangeException(nameof(mapId), mapId, "Unknown map id.");
            }
        }

        public static IReadOnlyList<RpgStorySpotDef> GetStorySpots(RpgWorldMapId mapId)
        {
            switch (mapId)
            {
                case RpgWorldMapId.Soundwell: return SoundwellStorySpots;
                case RpgWorldMapId.RedSaltHarbor: return RedSaltHarborStorySpots;
                case RpgWorldMapId.WhitewaterCrossing: return WhitewaterStorySpots;
                case RpgWorldMapId.MineCamp: return MineCampStorySpots;
                default: throw new ArgumentOutOfRangeException(nameof(mapId), mapId, "Unknown map id.");
            }
        }

        public static IReadOnlyList<RpgAmbientMicroEventZoneDef> GetAmbientZones(RpgWorldMapId mapId)
        {
            switch (mapId)
            {
                case RpgWorldMapId.Soundwell: return SoundwellAmbientZones;
                case RpgWorldMapId.RedSaltHarbor: return RedSaltHarborAmbientZones;
                case RpgWorldMapId.WhitewaterCrossing: return WhitewaterAmbientZones;
                case RpgWorldMapId.MineCamp: return MineCampAmbientZones;
                default: throw new ArgumentOutOfRangeException(nameof(mapId), mapId, "Unknown map id.");
            }
        }
    }

    /// <summary>
    /// Runtime service for interacting with field NPCs/rumor brokers, inspecting
    /// environmental storytelling spots, and evaluating deterministic regional
    /// ambient micro-events across all four 500x maps.
    /// </summary>
    public sealed class RpgWorld500xLivingWorldService
    {
        private readonly RpgWorldMapId mapId;
        private readonly RpgMapExplorationTracker tracker;
        private readonly Func<string, int> getInt;
        private readonly Action<string, int> setInt;
        private readonly IReadOnlyList<RpgFieldNpcDef> npcs;
        private readonly IReadOnlyList<RpgStorySpotDef> storySpots;
        private readonly IReadOnlyList<RpgAmbientMicroEventZoneDef> ambientZones;

        public RpgWorld500xLivingWorldService(
            RpgWorldMapId mapId,
            RpgMapExplorationTracker tracker,
            Func<string, int> getInt = null,
            Action<string, int> setInt = null)
        {
            this.mapId = mapId;
            this.tracker = tracker ?? throw new ArgumentNullException(nameof(tracker));
            this.getInt = getInt ?? (_ => 0);
            this.setInt = setInt ?? ((_, __) => { });
            npcs = RpgWorld500xLivingWorldCatalog.GetFieldNpcs(mapId);
            storySpots = RpgWorld500xLivingWorldCatalog.GetStorySpots(mapId);
            ambientZones = RpgWorld500xLivingWorldCatalog.GetAmbientZones(mapId);
        }

        public RpgWorld500xLivingWorldProgressSummary BuildLivingWorldProgressSummary()
        {
            int heard = 0;
            for (int i = 0; i < npcs.Count; i++)
            {
                if (getInt(npcs[i].RumorMakinomKey) == 1)
                {
                    heard++;
                }
            }

            int inspected = 0;
            for (int i = 0; i < storySpots.Count; i++)
            {
                if (getInt(storySpots[i].MakinomKey) == 1)
                {
                    inspected++;
                }
            }

            int remRumors = Math.Max(0, npcs.Count - heard);
            int remSpots = Math.Max(0, storySpots.Count - inspected);
            string summaryZh = "【风土与传闻】传闻 " + heard + "/" + npcs.Count +
                               "｜轶事点 " + inspected + "/" + storySpots.Count +
                               "｜微事件区 " + ambientZones.Count + " 处";

            return new RpgWorld500xLivingWorldProgressSummary(
                mapId,
                heard,
                npcs.Count,
                remRumors,
                inspected,
                storySpots.Count,
                remSpots,
                ambientZones.Count,
                summaryZh);
        }

        public bool TryFindNearestUnheardRumorNpc(
            Vector2 playerTile,
            out RpgFieldNpcDef nearest,
            out float distanceTiles)
        {
            nearest = default;
            distanceTiles = 0f;
            if (!IsFinite(playerTile))
            {
                return false;
            }

            bool found = false;
            float bestDistSq = float.MaxValue;
            for (int i = 0; i < npcs.Count; i++)
            {
                RpgFieldNpcDef n = npcs[i];
                if (getInt(n.RumorMakinomKey) == 1)
                {
                    continue;
                }

                float dx = playerTile.x - (n.Tile.x + 0.5f);
                float dy = playerTile.y - (n.Tile.y + 0.5f);
                float distSq = (dx * dx) + (dy * dy);
                if (!found || distSq < bestDistSq ||
                    (Mathf.Approximately(distSq, bestDistSq) && StringComparer.Ordinal.Compare(n.Id, nearest.Id) < 0))
                {
                    found = true;
                    bestDistSq = distSq;
                    nearest = n;
                }
            }

            if (found)
            {
                distanceTiles = Mathf.Sqrt(bestDistSq);
            }

            return found;
        }

        public bool TryFindNearestUninspectedStorySpot(
            Vector2 playerTile,
            out RpgStorySpotDef nearest,
            out float distanceTiles)
        {
            nearest = default;
            distanceTiles = 0f;
            if (!IsFinite(playerTile))
            {
                return false;
            }

            bool found = false;
            float bestDistSq = float.MaxValue;
            for (int i = 0; i < storySpots.Count; i++)
            {
                RpgStorySpotDef s = storySpots[i];
                if (getInt(s.MakinomKey) == 1)
                {
                    continue;
                }

                float dx = playerTile.x - (s.Tile.x + 0.5f);
                float dy = playerTile.y - (s.Tile.y + 0.5f);
                float distSq = (dx * dx) + (dy * dy);
                if (!found || distSq < bestDistSq ||
                    (Mathf.Approximately(distSq, bestDistSq) && StringComparer.Ordinal.Compare(s.Id, nearest.Id) < 0))
                {
                    found = true;
                    bestDistSq = distSq;
                    nearest = s;
                }
            }

            if (found)
            {
                distanceTiles = Mathf.Sqrt(bestDistSq);
            }

            return found;
        }

        public bool TryTalkToNearbyNpc(Vector2 playerTile, float interactRadius,
            out RpgFieldNpcDef npc, out string dialogueLineZh, out string revealedMarkerId)
        {
            return TryTalkToNearbyNpc(playerTile, interactRadius, out npc, out dialogueLineZh,
                out revealedMarkerId, out _);
        }

        public bool TryTalkToNearbyNpc(Vector2 playerTile, float interactRadius,
            out RpgFieldNpcDef npc, out string dialogueLineZh, out string revealedMarkerId,
            out bool firstHearing)
        {
            npc = default;
            dialogueLineZh = string.Empty;
            revealedMarkerId = string.Empty;
            firstHearing = false;
            if (!IsFinite(playerTile) || !IsFinite(interactRadius) || interactRadius <= 0f)
                return false;

            float radiusSq = interactRadius * interactRadius;
            for (int i = 0; i < npcs.Count; i++)
            {
                RpgFieldNpcDef n = npcs[i];
                float dx = playerTile.x - (n.Tile.x + 0.5f);
                float dy = playerTile.y - (n.Tile.y + 0.5f);
                if (dx * dx + dy * dy > radiusSq)
                    continue;

                npc = n;
                bool hasTarget = !string.IsNullOrEmpty(n.RumorTargetMarkerId);
                bool alreadyKnown = hasTarget && tracker.IsMarkerDiscovered(n.RumorTargetMarkerId);
                bool heard = getInt(n.RumorMakinomKey) == 1;
                // Hearing a rumor and visiting its landmark are independent
                // completion goals. First conversation must count in either order.
                if (hasTarget && (!heard || !alreadyKnown))
                {
                    dialogueLineZh = n.RumorHintZh;
                    if (!heard)
                    {
                        setInt(n.RumorMakinomKey, 1);
                        firstHearing = true;
                    }
                    // Do not replace the player's waypoint with an already visited landmark.
                    if (!alreadyKnown)
                    {
                        revealedMarkerId = n.RumorTargetMarkerId;
                        tracker.TrySetTrackedMarker(n.RumorTargetMarkerId);
                    }
                }
                else
                {
                    dialogueLineZh = n.AmbientLineZh;
                }
                return true;
            }
            return false;
        }

        public bool TryInspectNearbyStorySpot(Vector2 playerTile, float interactRadius,
            out RpgStorySpotDef spot, out bool firstInspection)
        {
            spot = default;
            firstInspection = false;
            if (!IsFinite(playerTile) || !IsFinite(interactRadius) || interactRadius <= 0f)
                return false;

            float radiusSq = interactRadius * interactRadius;
            for (int i = 0; i < storySpots.Count; i++)
            {
                RpgStorySpotDef s = storySpots[i];
                float dx = playerTile.x - (s.Tile.x + 0.5f);
                float dy = playerTile.y - (s.Tile.y + 0.5f);
                if (dx * dx + dy * dy > radiusSq)
                    continue;

                spot = s;
                firstInspection = getInt(s.MakinomKey) == 0;
                if (firstInspection)
                    setInt(s.MakinomKey, 1);
                return true;
            }
            return false;
        }

        public RpgActiveAmbientState EvaluateAmbientEvent(Vector2 playerTile, float elapsedSeconds)
        {
            if (!IsFinite(playerTile) || !IsFinite(elapsedSeconds) || elapsedSeconds < 0f)
                return RpgActiveAmbientState.Neutral;

            for (int i = 0; i < ambientZones.Count; i++)
            {
                RpgAmbientMicroEventZoneDef z = ambientZones[i];
                float dx = playerTile.x - (z.CenterTile.x + 0.5f);
                float dy = playerTile.y - (z.CenterTile.y + 0.5f);
                if (dx * dx + dy * dy > z.RadiusTiles * z.RadiusTiles)
                    continue;

                float phase = z.CycleSeconds > 0f ? (elapsedSeconds % z.CycleSeconds) : 0f;
                if (phase < z.ActiveSeconds)
                {
                    return new RpgActiveAmbientState(
                        z.Kind, z.Id,
                        z.PatrolSightMultiplier,
                        z.PatrolNoiseMultiplier,
                        z.MoveSpeedMultiplier,
                        z.CaptionZh);
                }
            }
            return RpgActiveAmbientState.Neutral;
        }

        private static bool IsFinite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
        private static bool IsFinite(Vector2 v) => IsFinite(v.x) && IsFinite(v.y);
    }
}
