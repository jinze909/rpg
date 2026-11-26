using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rpg.Soundwell
{
    public enum RpgSupplyCacheKind : byte
    {
        SupplyCrate = 1,
        BrineBarrel = 2,
        SalvagePile = 3,
        FieldRelic = 4
    }

    public enum RpgWorldShortcutKind : byte
    {
        OneWayLadder = 1,
        RustedDoor = 2,
        CanalDrawbridge = 3,
        FerryFootbridge = 4,
        MinecartSwitch = 5
    }

    public readonly struct RpgSupplyCacheDef
    {
        public RpgSupplyCacheDef(
            string id,
            RpgWorldMapId mapId,
            Vector2Int tile,
            RpgSupplyCacheKind kind,
            string titleZh,
            string rewardSummaryZh,
            int saltSilverReward,
            string loreNoteZh = null)
        {
            Id = id;
            MapId = mapId;
            Tile = tile;
            Kind = kind;
            TitleZh = titleZh;
            RewardSummaryZh = rewardSummaryZh;
            SaltSilverReward = saltSilverReward;
            LoreNoteZh = loreNoteZh ?? string.Empty;
        }

        public string Id { get; }
        public RpgWorldMapId MapId { get; }
        public Vector2Int Tile { get; }
        public RpgSupplyCacheKind Kind { get; }
        public string TitleZh { get; }
        public string RewardSummaryZh { get; }
        public int SaltSilverReward { get; }
        public string LoreNoteZh { get; }
        public string MakinomKey => "rpg.return.world.cache." + Id;
    }

    public readonly struct RpgWorldShortcutDef
    {
        public RpgWorldShortcutDef(
            string id,
            RpgWorldMapId mapId,
            RpgWorldShortcutKind kind,
            string titleZh,
            Vector2Int triggerTile,
            RectInt unlockSpan,
            byte unlockedTileCode,
            string unlockedToastZh)
        {
            Id = id;
            MapId = mapId;
            Kind = kind;
            TitleZh = titleZh;
            TriggerTile = triggerTile;
            UnlockSpan = unlockSpan;
            UnlockedTileCode = unlockedTileCode;
            UnlockedToastZh = unlockedToastZh;
        }

        public string Id { get; }
        public RpgWorldMapId MapId { get; }
        public RpgWorldShortcutKind Kind { get; }
        public string TitleZh { get; }
        public Vector2Int TriggerTile { get; }
        public RectInt UnlockSpan { get; }
        public byte UnlockedTileCode { get; }
        public string UnlockedToastZh { get; }
        public string MakinomKey => "rpg.return.world.shortcut." + Id;
    }

    public readonly struct RpgWorld500xCacheShortcutProgressSummary
    {
        public RpgWorld500xCacheShortcutProgressSummary(
            RpgWorldMapId mapId,
            int lootedCacheCount,
            int totalCacheCount,
            int remainingCacheCount,
            int remainingCacheSaltSilver,
            int unlockedShortcutCount,
            int totalShortcutCount,
            int lockedShortcutCount,
            string summaryZh)
        {
            MapId = mapId;
            LootedCacheCount = lootedCacheCount;
            TotalCacheCount = totalCacheCount;
            RemainingCacheCount = remainingCacheCount;
            RemainingCacheSaltSilver = remainingCacheSaltSilver;
            UnlockedShortcutCount = unlockedShortcutCount;
            TotalShortcutCount = totalShortcutCount;
            LockedShortcutCount = lockedShortcutCount;
            SummaryZh = summaryZh ?? string.Empty;
        }

        public RpgWorldMapId MapId { get; }
        public int LootedCacheCount { get; }
        public int TotalCacheCount { get; }
        public int RemainingCacheCount { get; }
        public int RemainingCacheSaltSilver { get; }
        public int UnlockedShortcutCount { get; }
        public int TotalShortcutCount { get; }
        public int LockedShortcutCount { get; }
        public string SummaryZh { get; }
    }

    /// <summary>
    /// Curated Wave-1 500x world enrichment catalog: 44 hidden supply caches &
    /// 《归来》 field relics plus 16 unlockable world shortcuts distributed across
    /// all four 500x-area maps.
    /// </summary>
    public static class RpgWorld500xContentCatalog
    {
        private static readonly RpgSupplyCacheDef[] SoundwellCaches =
        {
            new RpgSupplyCacheDef("sw_c01", RpgWorldMapId.Soundwell, new Vector2Int(180, 620),
                RpgSupplyCacheKind.SupplyCrate, "下井备用绳包", "绷带×2、盐银+18", 18),
            new RpgSupplyCacheDef("sw_c02", RpgWorldMapId.Soundwell, new Vector2Int(380, 610),
                RpgSupplyCacheKind.SalvagePile, "旧井壁铁钩堆", "修理铁件×2、盐银+15", 15),
            new RpgSupplyCacheDef("sw_c03", RpgWorldMapId.Soundwell, new Vector2Int(290, 780),
                RpgSupplyCacheKind.FieldRelic, "湿布裹锤遗物", "声井旧工笔记、盐银+30", 30,
                "锤头包着多层旧帆布，是当年测音矿工留下的降噪工具。"),
            new RpgSupplyCacheDef("sw_c04", RpgWorldMapId.Soundwell, new Vector2Int(520, 1040),
                RpgSupplyCacheKind.SupplyCrate, "北廊应急药箱", "止血纱布×2、盐银+22", 22),
            new RpgSupplyCacheDef("sw_c05", RpgWorldMapId.Soundwell, new Vector2Int(720, 1140),
                RpgSupplyCacheKind.SalvagePile, "北廊风灯灯芯盒", "灯油×2、盐银+16", 16),
            new RpgSupplyCacheDef("sw_c06", RpgWorldMapId.Soundwell, new Vector2Int(1040, 980),
                RpgSupplyCacheKind.FieldRelic, "钟岭测音铜片", "共鸣刻度记录、盐银+35", 35,
                "铜片背面刻着三道警示线，标注了借声兽最敏感的频段。"),
            new RpgSupplyCacheDef("sw_c07", RpgWorldMapId.Soundwell, new Vector2Int(620, 760),
                RpgSupplyCacheKind.SupplyCrate, "主厅石阶暗箱", "干粮×2、盐银+20", 20),
            new RpgSupplyCacheDef("sw_c08", RpgWorldMapId.Soundwell, new Vector2Int(920, 540),
                RpgSupplyCacheKind.BrineBarrel, "主厅密封清水桶", "清水补给、盐银+14", 14),
            new RpgSupplyCacheDef("sw_c09", RpgWorldMapId.Soundwell, new Vector2Int(1320, 780),
                RpgSupplyCacheKind.SalvagePile, "废弃矿灯零件箱", "黄铜零件×2、盐银+24", 24),
            new RpgSupplyCacheDef("sw_c10", RpgWorldMapId.Soundwell, new Vector2Int(1700, 860),
                RpgSupplyCacheKind.SupplyCrate, "东室备用干布捆", "厚帆布×2、盐银+20", 20),
            new RpgSupplyCacheDef("sw_c11", RpgWorldMapId.Soundwell, new Vector2Int(1940, 620),
                RpgSupplyCacheKind.FieldRelic, "旧铃匠刻名牌", "匠人铭文档案、盐银+40", 40,
                "铭牌记述了钟岭大铃在矿难前最后一次校音的日期。"),
            new RpgSupplyCacheDef("sw_c12", RpgWorldMapId.Soundwell, new Vector2Int(640, 300),
                RpgSupplyCacheKind.SupplyCrate, "暗廊扳手工具箱", "重型扳手、盐银+25", 25)
        };

        private static readonly RpgSupplyCacheDef[] RedSaltHarborCaches =
        {
            new RpgSupplyCacheDef("rsh_c01", RpgWorldMapId.RedSaltHarbor, new Vector2Int(200, 520),
                RpgSupplyCacheKind.BrineBarrel, "泊位封蜡盐桶", "粗盐补给、盐银+18", 18),
            new RpgSupplyCacheDef("rsh_c02", RpgWorldMapId.RedSaltHarbor, new Vector2Int(600, 660),
                RpgSupplyCacheKind.SupplyCrate, "码头搬运工木箱", "干粮×2、盐银+20", 20),
            new RpgSupplyCacheDef("rsh_c03", RpgWorldMapId.RedSaltHarbor, new Vector2Int(1040, 860),
                RpgSupplyCacheKind.FieldRelic, "韩乔旧核册残页", "税关旧账线索、盐银+38", 38,
                "残页记着三批失踪矿工的船票编号与扣留批注。"),
            new RpgSupplyCacheDef("rsh_c04", RpgWorldMapId.RedSaltHarbor, new Vector2Int(1440, 560),
                RpgSupplyCacheKind.SalvagePile, "船坞废铁铆钉筐", "修船铁件×3、盐银+22", 22),
            new RpgSupplyCacheDef("rsh_c05", RpgWorldMapId.RedSaltHarbor, new Vector2Int(1740, 860),
                RpgSupplyCacheKind.SupplyCrate, "修船摊松脂罐", "防渗松脂×2、盐银+24", 24),
            new RpgSupplyCacheDef("rsh_c06", RpgWorldMapId.RedSaltHarbor, new Vector2Int(2240, 620),
                RpgSupplyCacheKind.BrineBarrel, "防波堤备用灯油桶", "灯油×3、盐银+20", 20),
            new RpgSupplyCacheDef("rsh_c07", RpgWorldMapId.RedSaltHarbor, new Vector2Int(2860, 660),
                RpgSupplyCacheKind.FieldRelic, "老渔户暗礁浮标", "东南航道证物、盐银+42", 42,
                "浮标上涂着赤盐暗礁的夜航反光白漆记号。"),
            new RpgSupplyCacheDef("rsh_c08", RpgWorldMapId.RedSaltHarbor, new Vector2Int(1520, 1180),
                RpgSupplyCacheKind.SupplyCrate, "盐市秤房暗格", "盐银+32、伤药×1", 32),
            new RpgSupplyCacheDef("rsh_c09", RpgWorldMapId.RedSaltHarbor, new Vector2Int(2180, 1380),
                RpgSupplyCacheKind.SalvagePile, "盐灶耐火砖垛", "修缮材料、盐银+20", 20),
            new RpgSupplyCacheDef("rsh_c10", RpgWorldMapId.RedSaltHarbor, new Vector2Int(880, 1420),
                RpgSupplyCacheKind.SupplyCrate, "西货场遗落补给箱", "绷带×3、盐银+26", 26),
            new RpgSupplyCacheDef("rsh_c11", RpgWorldMapId.RedSaltHarbor, new Vector2Int(1360, 1720),
                RpgSupplyCacheKind.FieldRelic, "矿道旧路牌拓片", "矿营捷径线索、盐银+35", 35,
                "拓片标出了矿营北口巡逻亭的旧换班路线。"),
            new RpgSupplyCacheDef("rsh_c12", RpgWorldMapId.RedSaltHarbor, new Vector2Int(2500, 1460),
                RpgSupplyCacheKind.BrineBarrel, "东库腌鱼木桶", "行军口粮×3、盐银+22", 22)
        };

        private static readonly RpgSupplyCacheDef[] WhitewaterCaches =
        {
            new RpgSupplyCacheDef("ww_c01", RpgWorldMapId.WhitewaterCrossing, new Vector2Int(120, 420),
                RpgSupplyCacheKind.SupplyCrate, "西岸工兵弹药箱", "信号弹×2、盐银+20", 20),
            new RpgSupplyCacheDef("ww_c02", RpgWorldMapId.WhitewaterCrossing, new Vector2Int(280, 740),
                RpgSupplyCacheKind.SalvagePile, "北滩备用铁链捆", "锚链扣×2、盐银+18", 18),
            new RpgSupplyCacheDef("ww_c03", RpgWorldMapId.WhitewaterCrossing, new Vector2Int(200, 200),
                RpgSupplyCacheKind.FieldRelic, "焦黑旧桥钉", "白水渡旧战遗物、盐银+36", 36,
                "桥钉弯曲焦黑，见证了当年断桥阻击时的烈火。"),
            new RpgSupplyCacheDef("ww_c04", RpgWorldMapId.WhitewaterCrossing, new Vector2Int(720, 496),
                RpgSupplyCacheKind.SupplyCrate, "浮桥中段急救包", "止血绷带×3、盐银+24", 24),
            new RpgSupplyCacheDef("ww_c05", RpgWorldMapId.WhitewaterCrossing, new Vector2Int(1040, 496),
                RpgSupplyCacheKind.SalvagePile, "浮筏备用松木板", "桥板修补件、盐银+22", 22),
            new RpgSupplyCacheDef("ww_c06", RpgWorldMapId.WhitewaterCrossing, new Vector2Int(1480, 620),
                RpgSupplyCacheKind.SupplyCrate, "东岸掩体干粮袋", "干粮×3、盐银+24", 24),
            new RpgSupplyCacheDef("ww_c07", RpgWorldMapId.WhitewaterCrossing, new Vector2Int(1680, 780),
                RpgSupplyCacheKind.FieldRelic, "哨塔望远镜筒", "东岸侦察档案、盐银+40", 40,
                "镜筒刻着白水渡上下游三处渡口的水位刻线。"),
            new RpgSupplyCacheDef("ww_c08", RpgWorldMapId.WhitewaterCrossing, new Vector2Int(1500, 220),
                RpgSupplyCacheKind.BrineBarrel, "东岸火盆松脂桶", "松脂×3、盐银+20", 20)
        };

        private static readonly RpgSupplyCacheDef[] MineCampCaches =
        {
            new RpgSupplyCacheDef("mc_c01", RpgWorldMapId.MineCamp, new Vector2Int(240, 200),
                RpgSupplyCacheKind.SupplyCrate, "南门岗亭备用灯", "矿灯油×2、盐银+18", 18),
            new RpgSupplyCacheDef("mc_c02", RpgWorldMapId.MineCamp, new Vector2Int(520, 340),
                RpgSupplyCacheKind.SalvagePile, "矿车旧轴瓦箱", "轴承件×2、盐银+20", 20),
            new RpgSupplyCacheDef("mc_c03", RpgWorldMapId.MineCamp, new Vector2Int(940, 220),
                RpgSupplyCacheKind.SupplyCrate, "哨楼值班药匣", "伤药×2、盐银+24", 24),
            new RpgSupplyCacheDef("mc_c04", RpgWorldMapId.MineCamp, new Vector2Int(1720, 240),
                RpgSupplyCacheKind.BrineBarrel, "账房外盐渍木桶", "口粮×2、盐银+22", 22),
            new RpgSupplyCacheDef("mc_c05", RpgWorldMapId.MineCamp, new Vector2Int(1680, 520),
                RpgSupplyCacheKind.FieldRelic, "老张黄铜烟盒", "下层矿工纪念物、盐银+38", 38,
                "盒盖内侧刻着下层避难洞与主泵房的简易方位图。"),
            new RpgSupplyCacheDef("mc_c06", RpgWorldMapId.MineCamp, new Vector2Int(680, 820),
                RpgSupplyCacheKind.SupplyCrate, "宿舍床下旧铁盒", "绷带×2、盐银+25", 25),
            new RpgSupplyCacheDef("mc_c07", RpgWorldMapId.MineCamp, new Vector2Int(1220, 620),
                RpgSupplyCacheKind.SalvagePile, "转盘道岔润滑油壶", "机油×2、盐银+20", 20),
            new RpgSupplyCacheDef("mc_c08", RpgWorldMapId.MineCamp, new Vector2Int(760, 1260),
                RpgSupplyCacheKind.SupplyCrate, "上层通道应急包", "止血药×2、盐银+28", 28),
            new RpgSupplyCacheDef("mc_c09", RpgWorldMapId.MineCamp, new Vector2Int(1200, 1160),
                RpgSupplyCacheKind.FieldRelic, "沈梨水泵草图副本", "泵房结构档案、盐银+42", 42,
                "草图红笔圈出了主泵三小时停机时的临界回水位。"),
            new RpgSupplyCacheDef("mc_c10", RpgWorldMapId.MineCamp, new Vector2Int(1640, 940),
                RpgSupplyCacheKind.SalvagePile, "泵房黄铜压力阀", "精密阀件、盐银+30", 30)
        };

        private static readonly RpgWorldShortcutDef[] SoundwellShortcuts =
        {
            new RpgWorldShortcutDef("sw_sc_entry_north", RpgWorldMapId.Soundwell,
                RpgWorldShortcutKind.OneWayLadder, "北廊直降绳梯",
                new Vector2Int(380, 920), new RectInt(360, 820, 16, 80),
                (byte)SoundwellTileKind.FloorPlain, "放下绳梯：下井入口直通北侧回声长廊"),
            new RpgWorldShortcutDef("sw_sc_west_central", RpgWorldMapId.Soundwell,
                RpgWorldShortcutKind.RustedDoor, "暗廊锈蚀捷径门",
                new Vector2Int(980, 340), new RectInt(960, 360, 16, 120),
                (byte)SoundwellTileKind.FloorPlain, "打开锈门：西侧铁闸暗廊直通主井厅"),
            new RpgWorldShortcutDef("sw_sc_north_east", RpgWorldMapId.Soundwell,
                RpgWorldShortcutKind.OneWayLadder, "东室北壁密道",
                new Vector2Int(1680, 900), new RectInt(1300, 900, 340, 16),
                (byte)SoundwellTileKind.FloorPlain, "打通北壁密道：北廊直达东侧铜铃室"),
            new RpgWorldShortcutDef("sw_sc_central_east_south", RpgWorldMapId.Soundwell,
                RpgWorldShortcutKind.RustedDoor, "主厅南翼检修门",
                new Vector2Int(1660, 540), new RectInt(1460, 540, 180, 16),
                (byte)SoundwellTileKind.FloorPlain, "开启检修门：主井厅南侧直通湿布室")
        };

        private static readonly RpgWorldShortcutDef[] RedSaltHarborShortcuts =
        {
            new RpgWorldShortcutDef("rsh_sc_customs_gate", RpgWorldMapId.RedSaltHarbor,
                RpgWorldShortcutKind.RustedDoor, "税关侧门近路",
                new Vector2Int(1220, 720), new RectInt(1220, 700, 16, 16),
                (byte)HarborTileKind.QuayStone, "拉开税关侧门：码头直通盐市西侧"),
            new RpgWorldShortcutDef("rsh_sc_drydock_bridge", RpgWorldMapId.RedSaltHarbor,
                RpgWorldShortcutKind.CanalDrawbridge, "干船坞木栈吊桥",
                new Vector2Int(2040, 780), new RectInt(2040, 760, 16, 16),
                (byte)HarborTileKind.BridgePlank, "放下木栈吊桥：修船摊直连防波堤"),
            new RpgWorldShortcutDef("rsh_sc_west_cargo_door", RpgWorldMapId.RedSaltHarbor,
                RpgWorldShortcutKind.RustedDoor, "西货场北墙便门",
                new Vector2Int(700, 1360), new RectInt(700, 1380, 16, 16),
                (byte)HarborTileKind.QuayStone, "打开北墙便门：西货场直通北口石门坊"),
            new RpgWorldShortcutDef("rsh_sc_market_north_bridge", RpgWorldMapId.RedSaltHarbor,
                RpgWorldShortcutKind.CanalDrawbridge, "盐市北渠石桥",
                new Vector2Int(1640, 1480), new RectInt(1640, 1490, 16, 16),
                (byte)HarborTileKind.BridgePlank, "架通北渠石桥：盐市广场直达矿营入山口")
        };

        private static readonly RpgWorldShortcutDef[] WhitewaterShortcuts =
        {
            new RpgWorldShortcutDef("ww_sc_south_footbridge", RpgWorldMapId.WhitewaterCrossing,
                RpgWorldShortcutKind.FerryFootbridge, "南渡浮索便桥",
                new Vector2Int(1340, 314), new RectInt(488, 310, 832, 10),
                (byte)HarborTileKind.BridgePlank, "架设南渡便桥：西岸旧渡口直连东岸旧桥墩"),
            new RpgWorldShortcutDef("ww_sc_north_sandbar_bridge", RpgWorldMapId.WhitewaterCrossing,
                RpgWorldShortcutKind.FerryFootbridge, "北沙洲浅滩木桥",
                new Vector2Int(1340, 684), new RectInt(488, 680, 832, 10),
                (byte)HarborTileKind.BridgePlank, "铺通浅滩木桥：北沙洲直抵东岸哨塔"),
            new RpgWorldShortcutDef("ww_sc_west_wagon_path", RpgWorldMapId.WhitewaterCrossing,
                RpgWorldShortcutKind.RustedDoor, "西岸车阵捷径口",
                new Vector2Int(200, 640), new RectInt(200, 620, 12, 12),
                (byte)HarborTileKind.QuayStone, "移开拒马车辕：西营地直达辎重车阵"),
            new RpgWorldShortcutDef("ww_sc_east_ridge_gate", RpgWorldMapId.WhitewaterCrossing,
                RpgWorldShortcutKind.RustedDoor, "东岸山口木栅门",
                new Vector2Int(1600, 340), new RectInt(1600, 320, 12, 12),
                (byte)HarborTileKind.QuayStone, "打开山口木栅：补给棚直通撤离山口")
        };

        private static readonly RpgWorldShortcutDef[] MineCampShortcuts =
        {
            new RpgWorldShortcutDef("mc_sc_south_patrol_rail", RpgWorldMapId.MineCamp,
                RpgWorldShortcutKind.MinecartSwitch, "南门矿车直通岔道",
                new Vector2Int(660, 200), new RectInt(600, 196, 40, 16),
                (byte)SoundwellTileKind.FloorPlain, "扳动矿车道岔：南门直通护矿队南侧"),
            new RpgWorldShortcutDef("mc_sc_patrol_supply_gate", RpgWorldMapId.MineCamp,
                RpgWorldShortcutKind.RustedDoor, "账房铁栏边门",
                new Vector2Int(1440, 400), new RectInt(1300, 396, 120, 16),
                (byte)SoundwellTileKind.FloorPlain, "开启铁栏边门：巡逻岗直达物资账房"),
            new RpgWorldShortcutDef("mc_sc_shift_upper_ladder", RpgWorldMapId.MineCamp,
                RpgWorldShortcutKind.OneWayLadder, "宿舍直上疏散梯",
                new Vector2Int(520, 1120), new RectInt(512, 940, 16, 160),
                (byte)SoundwellTileKind.FloorPlain, "放下直上铁梯：矿工宿舍直通北侧通风竖井"),
            new RpgWorldShortcutDef("mc_sc_supply_pump_hoist", RpgWorldMapId.MineCamp,
                RpgWorldShortcutKind.MinecartSwitch, "堆场至泵房货梯口",
                new Vector2Int(1840, 900), new RectInt(1832, 640, 16, 240),
                (byte)SoundwellTileKind.FloorPlain, "接通货梯通道：盐煤堆场直下主泵房大厅")
        };

        public static IReadOnlyList<RpgSupplyCacheDef> GetCaches(RpgWorldMapId mapId)
        {
            switch (mapId)
            {
                case RpgWorldMapId.Soundwell: return SoundwellCaches;
                case RpgWorldMapId.RedSaltHarbor: return RedSaltHarborCaches;
                case RpgWorldMapId.WhitewaterCrossing: return WhitewaterCaches;
                case RpgWorldMapId.MineCamp: return MineCampCaches;
                default: throw new ArgumentOutOfRangeException(nameof(mapId), mapId, "Unknown map id.");
            }
        }

        public static IReadOnlyList<RpgWorldShortcutDef> GetShortcuts(RpgWorldMapId mapId)
        {
            switch (mapId)
            {
                case RpgWorldMapId.Soundwell: return SoundwellShortcuts;
                case RpgWorldMapId.RedSaltHarbor: return RedSaltHarborShortcuts;
                case RpgWorldMapId.WhitewaterCrossing: return WhitewaterShortcuts;
                case RpgWorldMapId.MineCamp: return MineCampShortcuts;
                default: throw new ArgumentOutOfRangeException(nameof(mapId), mapId, "Unknown map id.");
            }
        }
    }

    /// <summary>
    /// Chunk-indexed runtime controller managing hidden supply caches, relics,
    /// and unlockable world shortcuts on a 500x map with idempotent Makinom
    /// persistence and live byte[,] walkability mutation.
    /// </summary>
    public sealed class RpgWorld500xCachesAndShortcutsController
    {
        private readonly RpgWorld500xMapLayout layout;
        private readonly Func<string, int> getInt;
        private readonly Action<string, int> setInt;
        private readonly Action<RpgSupplyCacheDef> onCacheLooted;
        private readonly Dictionary<string, RpgSupplyCacheDef> cachesById = new Dictionary<string, RpgSupplyCacheDef>(StringComparer.Ordinal);
        private readonly Dictionary<string, RpgWorldShortcutDef> shortcutsById = new Dictionary<string, RpgWorldShortcutDef>(StringComparer.Ordinal);
        private readonly HashSet<string> lootedCaches = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> unlockedShortcuts = new HashSet<string>(StringComparer.Ordinal);

        public int TotalCacheCount => cachesById.Count;
        public int LootedCacheCount => lootedCaches.Count;
        public int TotalShortcutCount => shortcutsById.Count;
        public int UnlockedShortcutCount => unlockedShortcuts.Count;

        public RpgWorld500xCachesAndShortcutsController(
            RpgWorld500xMapLayout layout,
            Func<string, int> getInt,
            Action<string, int> setInt,
            Action<RpgSupplyCacheDef> onCacheLooted = null)
        {
            this.layout = layout ?? throw new ArgumentNullException(nameof(layout));
            this.getInt = getInt ?? (_ => 0);
            this.setInt = setInt ?? ((_, __) => { });
            this.onCacheLooted = onCacheLooted;

            IReadOnlyList<RpgSupplyCacheDef> caches = RpgWorld500xContentCatalog.GetCaches(layout.MapId);
            for (int i = 0; i < caches.Count; i++)
            {
                RpgSupplyCacheDef c = caches[i];
                cachesById[c.Id] = c;
                if (this.getInt(c.MakinomKey) == 1)
                    lootedCaches.Add(c.Id);
            }

            IReadOnlyList<RpgWorldShortcutDef> shortcuts = RpgWorld500xContentCatalog.GetShortcuts(layout.MapId);
            for (int i = 0; i < shortcuts.Count; i++)
            {
                RpgWorldShortcutDef sc = shortcuts[i];
                shortcutsById[sc.Id] = sc;
                if (this.getInt(sc.MakinomKey) == 1)
                {
                    unlockedShortcuts.Add(sc.Id);
                    ApplyShortcutTiles(sc);
                }
            }
        }

        public bool IsCacheLooted(string cacheId) =>
            !string.IsNullOrEmpty(cacheId) && lootedCaches.Contains(cacheId);

        public bool IsShortcutUnlocked(string shortcutId) =>
            !string.IsNullOrEmpty(shortcutId) && unlockedShortcuts.Contains(shortcutId);

        public RpgWorld500xCacheShortcutProgressSummary BuildProgressSummary()
        {
            int remainingSilver = 0;
            foreach (KeyValuePair<string, RpgSupplyCacheDef> kvp in cachesById)
            {
                if (!lootedCaches.Contains(kvp.Key))
                {
                    remainingSilver += Math.Max(0, kvp.Value.SaltSilverReward);
                }
            }

            int remainingCaches = Math.Max(0, TotalCacheCount - LootedCacheCount);
            int lockedShortcuts = Math.Max(0, TotalShortcutCount - UnlockedShortcutCount);
            string summaryZh = "【秘藏与捷径】补给箱 " + LootedCacheCount + "/" + TotalCacheCount +
                               "（余盐银 +" + remainingSilver + "）｜捷径 " +
                               UnlockedShortcutCount + "/" + TotalShortcutCount;

            return new RpgWorld500xCacheShortcutProgressSummary(
                layout.MapId,
                LootedCacheCount,
                TotalCacheCount,
                remainingCaches,
                remainingSilver,
                UnlockedShortcutCount,
                TotalShortcutCount,
                lockedShortcuts,
                summaryZh);
        }

        public bool TryFindNearestUnlootedCache(
            Vector2 playerTile,
            out RpgSupplyCacheDef nearest,
            out float distanceTiles)
        {
            nearest = default;
            distanceTiles = 0f;
            if (float.IsNaN(playerTile.x) || float.IsInfinity(playerTile.x) ||
                float.IsNaN(playerTile.y) || float.IsInfinity(playerTile.y))
            {
                return false;
            }

            bool found = false;
            float bestDistSq = float.MaxValue;
            foreach (KeyValuePair<string, RpgSupplyCacheDef> kvp in cachesById)
            {
                if (lootedCaches.Contains(kvp.Key))
                {
                    continue;
                }

                RpgSupplyCacheDef c = kvp.Value;
                float dx = playerTile.x - (c.Tile.x + 0.5f);
                float dy = playerTile.y - (c.Tile.y + 0.5f);
                float distSq = (dx * dx) + (dy * dy);
                if (!found || distSq < bestDistSq ||
                    (Mathf.Approximately(distSq, bestDistSq) && StringComparer.Ordinal.Compare(c.Id, nearest.Id) < 0))
                {
                    found = true;
                    bestDistSq = distSq;
                    nearest = c;
                }
            }

            if (found)
            {
                distanceTiles = Mathf.Sqrt(bestDistSq);
            }

            return found;
        }

        public bool TryFindNearestLockedShortcut(
            Vector2 playerTile,
            out RpgWorldShortcutDef nearest,
            out float distanceTiles)
        {
            nearest = default;
            distanceTiles = 0f;
            if (float.IsNaN(playerTile.x) || float.IsInfinity(playerTile.x) ||
                float.IsNaN(playerTile.y) || float.IsInfinity(playerTile.y))
            {
                return false;
            }

            bool found = false;
            float bestDistSq = float.MaxValue;
            foreach (KeyValuePair<string, RpgWorldShortcutDef> kvp in shortcutsById)
            {
                if (unlockedShortcuts.Contains(kvp.Key))
                {
                    continue;
                }

                RpgWorldShortcutDef sc = kvp.Value;
                float dx = playerTile.x - (sc.TriggerTile.x + 0.5f);
                float dy = playerTile.y - (sc.TriggerTile.y + 0.5f);
                float distSq = (dx * dx) + (dy * dy);
                if (!found || distSq < bestDistSq ||
                    (Mathf.Approximately(distSq, bestDistSq) && StringComparer.Ordinal.Compare(sc.Id, nearest.Id) < 0))
                {
                    found = true;
                    bestDistSq = distSq;
                    nearest = sc;
                }
            }

            if (found)
            {
                distanceTiles = Mathf.Sqrt(bestDistSq);
            }

            return found;
        }

        public bool TryLootNearbyCache(Vector2 playerTile, float interactRadius, out RpgSupplyCacheDef looted)
        {
            looted = default;
            if (float.IsNaN(interactRadius) || float.IsInfinity(interactRadius) ||
                interactRadius <= 0f ||
                !TryFindNearestUnlootedCache(playerTile, out RpgSupplyCacheDef nearest, out float distance) ||
                distance > interactRadius)
            {
                return false;
            }

            // Use the same nearest-target authority as the read-only exploration HUD.
            // The small regional catalog is bounded; no fixed chunk window can hide
            // a target that is within the caller's supplied interaction radius.
            lootedCaches.Add(nearest.Id);
            setInt(nearest.MakinomKey, 1);
            onCacheLooted?.Invoke(nearest);
            looted = nearest;
            return true;
        }

        public bool TryUnlockNearbyShortcut(Vector2 playerTile, float interactRadius,
            out RpgWorldShortcutDef unlocked)
        {
            unlocked = default;
            if (float.IsNaN(interactRadius) || float.IsInfinity(interactRadius) ||
                interactRadius <= 0f ||
                !TryFindNearestLockedShortcut(playerTile, out RpgWorldShortcutDef nearest, out float distance) ||
                distance > interactRadius)
            {
                return false;
            }

            unlockedShortcuts.Add(nearest.Id);
            setInt(nearest.MakinomKey, 1);
            ApplyShortcutTiles(nearest);
            unlocked = nearest;
            return true;
        }

        private void ApplyShortcutTiles(RpgWorldShortcutDef sc)
        {
            int xMin = Mathf.Max(1, sc.UnlockSpan.xMin);
            int xMax = Mathf.Min(layout.Width - 1, sc.UnlockSpan.xMax);
            int yMin = Mathf.Max(1, sc.UnlockSpan.yMin);
            int yMax = Mathf.Min(layout.Height - 1, sc.UnlockSpan.yMax);
            for (int x = xMin; x < xMax; x++)
            {
                for (int y = yMin; y < yMax; y++)
                    layout.Tiles[x, y] = sc.UnlockedTileCode;
            }
        }

    }
}
