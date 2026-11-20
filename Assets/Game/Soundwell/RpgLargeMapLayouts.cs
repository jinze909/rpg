using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Shared scale constants for normal-scale 2D pixel-art RPG maps in Return
    /// (16 PPU, 320x180 logical viewport = 20 x 11.25 tiles per screen).
    /// All primary explorable areas span 4x the baseline multi-screen target so
    /// Cinemachine 2D camera follow and district pacing feel like a full RPG.
    /// </summary>
    public static class RpgWorldMapScale
    {
        public const int PixelsPerUnit = 16;
        public const float LogicalScreenTilesX = 20f;
        public const float LogicalScreenTilesY = 11.25f;

        // 4x-area targets (2x width x 2x height over standard 2-3 screen areas).
        public const int SoundwellMinWidth = 96;
        public const int SoundwellMinHeight = 60;
        public const int RedSaltHarborMinWidth = 140;
        public const int RedSaltHarborMinHeight = 84;
        public const int WhitewaterCrossingMinWidth = 80;
        public const int WhitewaterCrossingMinHeight = 44;

        public static Bounds BuildCameraBounds(int width, int height)
        {
            return new Bounds(
                new Vector3(width * 0.5f, height * 0.5f, 0f),
                new Vector3(width, height, 1f));
        }
    }

    public enum SoundwellTileKind
    {
        FloorPlain = 0,
        FloorCracked = 1,
        FloorPuddle = 2,
        FloorMoss = 3,
        WallTop = 4,
        WallGrating = 5,
        WallCorner = 6,
        WallMoss = 7,
        GateClosed = 8,
        GateOpen = 9,
        BellPedestal = 10,
        WetClothSpot = 11,
        WallTorch = 12,
        ExtractionHatch = 13,
        Rubble = 14,
        FloorDrain = 15
    }

    public enum HarborTileKind
    {
        WaterDeep = 0,
        WaterCalm = 1,
        WaterFoam = 2,
        WaterShallow = 3,
        BridgePlank = 4,
        BridgeCable = 5,
        BridgeBrokenEnd = 6,
        QuayStone = 7,
        MooringPost = 8,
        Brazier = 9,
        RepairStall = 10,
        TenderBoat = 11,
        SaltStall = 12,
        SaltSacks = 13,
        Crates = 14,
        LanternPost = 15
    }

    public readonly struct RpgMapZone
    {
        public RpgMapZone(string id, string displayNameEn, string displayNameZh, RectInt bounds)
        {
            Id = id;
            DisplayNameEn = displayNameEn;
            DisplayNameZh = displayNameZh;
            Bounds = bounds;
        }

        public string Id { get; }
        public string DisplayNameEn { get; }
        public string DisplayNameZh { get; }
        public RectInt Bounds { get; }
    }

    public readonly struct RpgMapTriggerAnchor
    {
        public RpgMapTriggerAnchor(string id, Vector2Int tile, float radiusTiles)
        {
            Id = id;
            Tile = tile;
            RadiusTiles = radiusTiles;
        }

        public string Id { get; }
        public Vector2Int Tile { get; }
        public float RadiusTiles { get; }
    }

    public readonly struct RpgLight2DAnchor
    {
        public RpgLight2DAnchor(string id, Vector2Int tile, float outerRadiusTiles,
            float intensity, Color color)
        {
            Id = id;
            Tile = tile;
            OuterRadiusTiles = outerRadiusTiles;
            Intensity = intensity;
            Color = color;
        }

        public string Id { get; }
        public Vector2Int Tile { get; }
        public float OuterRadiusTiles { get; }
        public float Intensity { get; }
        public Color Color { get; }
    }

    /// <summary>
    /// 96x60 (5,760-tile) multi-zone Soundwell subterranean dungeon layout
    /// mapped directly to the 4x4 soundwell_tileset.png slices.
    /// </summary>
    public sealed class SoundwellLargeMapLayout
    {
        public int Width { get; }
        public int Height { get; }
        public SoundwellTileKind[,] Tiles { get; }
        public Bounds CameraBounds { get; }
        public Vector2Int PlayerSpawn { get; }
        public Vector2Int BronzeBellTile { get; }
        public Vector2Int WetClothTile { get; }
        public Vector2Int GateLeverTile { get; }
        public Vector2Int ExtractionTile { get; }
        public IReadOnlyList<RpgMapZone> Zones { get; }
        public IReadOnlyList<Vector2Int> BeastPatrolWaypoints { get; }
        public IReadOnlyList<RpgLight2DAnchor> PointLights { get; }
        public Color GlobalLightColor { get; }
        public float GlobalLightIntensity { get; }

        private SoundwellLargeMapLayout(int width, int height, SoundwellTileKind[,] tiles,
            Vector2Int playerSpawn, Vector2Int bronzeBellTile, Vector2Int wetClothTile,
            Vector2Int gateLeverTile, Vector2Int extractionTile,
            IReadOnlyList<RpgMapZone> zones, IReadOnlyList<Vector2Int> beastPatrolWaypoints,
            IReadOnlyList<RpgLight2DAnchor> pointLights)
        {
            Width = width;
            Height = height;
            Tiles = tiles;
            CameraBounds = RpgWorldMapScale.BuildCameraBounds(width, height);
            PlayerSpawn = playerSpawn;
            BronzeBellTile = bronzeBellTile;
            WetClothTile = wetClothTile;
            GateLeverTile = gateLeverTile;
            ExtractionTile = extractionTile;
            Zones = zones;
            BeastPatrolWaypoints = beastPatrolWaypoints;
            PointLights = pointLights;
            GlobalLightColor = new Color(0.56f, 0.66f, 0.78f, 1f);
            GlobalLightIntensity = 0.38f;
        }

        public static bool IsWalkable(SoundwellTileKind tile, bool gateOpen)
        {
            switch (tile)
            {
                case SoundwellTileKind.FloorPlain:
                case SoundwellTileKind.FloorCracked:
                case SoundwellTileKind.FloorPuddle:
                case SoundwellTileKind.FloorMoss:
                case SoundwellTileKind.GateOpen:
                case SoundwellTileKind.BellPedestal:
                case SoundwellTileKind.WetClothSpot:
                case SoundwellTileKind.ExtractionHatch:
                case SoundwellTileKind.FloorDrain:
                    return true;
                case SoundwellTileKind.GateClosed:
                    return gateOpen;
                default:
                    return false;
            }
        }

        public static SoundwellLargeMapLayout Build()
        {
            const int width = RpgWorldMapScale.SoundwellMinWidth;
            const int height = RpgWorldMapScale.SoundwellMinHeight;
            var tiles = new SoundwellTileKind[width, height];
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                    tiles[x, y] = SoundwellTileKind.WallTop;

            var entryShaft = new RectInt(4, 24, 16, 12);
            var northEcho = new RectInt(24, 42, 40, 12);
            var centralHall = new RectInt(26, 20, 40, 18);
            var eastVault = new RectInt(72, 22, 18, 22);
            var westGateCorridor = new RectInt(26, 6, 24, 10);
            var southExtraction = new RectInt(56, 6, 30, 10);

            CarveFloor(tiles, entryShaft);
            CarveFloor(tiles, northEcho);
            CarveFloor(tiles, centralHall);
            CarveFloor(tiles, eastVault);
            CarveFloor(tiles, westGateCorridor);
            CarveFloor(tiles, southExtraction);

            // 4-tile-wide corridors so a 3-member party never snags on corners.
            CarveFloor(tiles, new RectInt(19, 28, 8, 4));  // Entry -> Central
            CarveFloor(tiles, new RectInt(10, 35, 4, 13)); // Entry -> North bend
            CarveFloor(tiles, new RectInt(10, 44, 15, 4)); // North bend -> NorthEcho
            CarveFloor(tiles, new RectInt(44, 37, 4, 6));  // Central -> NorthEcho
            CarveFloor(tiles, new RectInt(65, 28, 8, 4));  // Central -> EastVault
            CarveFloor(tiles, new RectInt(34, 15, 4, 6));  // Central -> WestGate
            CarveFloor(tiles, new RectInt(49, 9, 8, 4));   // WestGate -> SouthExtraction

            // Apply grating wall face immediately north of carved floors.
            for (int x = 1; x < width - 1; x++)
            {
                for (int y = 1; y < height - 1; y++)
                {
                    if (tiles[x, y] == SoundwellTileKind.WallTop &&
                        IsWalkable(tiles[x, y - 1], gateOpen: true))
                        tiles[x, y] = (x + y) % 5 == 0
                            ? SoundwellTileKind.WallMoss
                            : SoundwellTileKind.WallGrating;
                }
            }

            // Closed iron gate separates WestGateCorridor from SouthExtractionHall.
            for (int y = 9; y < 13; y++)
                tiles[52, y] = SoundwellTileKind.GateClosed;

            var playerSpawn = new Vector2Int(10, 30);
            var bronzeBell = new Vector2Int(80, 34);
            var wetCloth = new Vector2Int(80, 26);
            var gateLever = new Vector2Int(46, 11);
            var extraction = new Vector2Int(78, 11);

            tiles[bronzeBell.x, bronzeBell.y] = SoundwellTileKind.BellPedestal;
            tiles[wetCloth.x, wetCloth.y] = SoundwellTileKind.WetClothSpot;
            tiles[extraction.x, extraction.y] = SoundwellTileKind.ExtractionHatch;
            tiles[46, 29] = SoundwellTileKind.FloorDrain;

            var zones = new[]
            {
                new RpgMapZone("soundwell.entry", "Entry Shaft", "下井入口", entryShaft),
                new RpgMapZone("soundwell.north-echo", "North Echo Gallery", "北侧回声长廊", northEcho),
                new RpgMapZone("soundwell.central-hall", "Central Bell Chamber", "中央钟岭主井厅", centralHall),
                new RpgMapZone("soundwell.east-vault", "Bell & Cloth Vault", "东侧铜铃与湿布室", eastVault),
                new RpgMapZone("soundwell.west-gate", "Iron Gate Corridor", "西侧铁闸暗廊", westGateCorridor),
                new RpgMapZone("soundwell.extraction", "South Extraction Hall", "南侧撤退井口", southExtraction)
            };

            var patrol = new[]
            {
                new Vector2Int(32, 29),
                new Vector2Int(58, 29),
                new Vector2Int(46, 46),
                new Vector2Int(28, 46)
            };

            Color warmAmber = new Color(1.0f, 0.78f, 0.45f, 1f);
            Color bronzeGlow = new Color(0.95f, 0.72f, 0.36f, 1f);
            var lights = new[]
            {
                new RpgLight2DAnchor("light.entry-torch", new Vector2Int(12, 35), 7f, 0.9f, warmAmber),
                new RpgLight2DAnchor("light.central-torch", new Vector2Int(46, 37), 8f, 0.85f, warmAmber),
                new RpgLight2DAnchor("light.bronze-bell", bronzeBell, 7.5f, 1.0f, bronzeGlow),
                new RpgLight2DAnchor("light.extraction-hatch", extraction, 8.5f, 1.05f, warmAmber)
            };

            return new SoundwellLargeMapLayout(width, height, tiles,
                playerSpawn, bronzeBell, wetCloth, gateLever, extraction,
                zones, patrol, lights);
        }

        private static void CarveFloor(SoundwellTileKind[,] tiles, RectInt rect)
        {
            for (int x = rect.xMin; x < rect.xMax; x++)
            {
                for (int y = rect.yMin; y < rect.yMax; y++)
                {
                    int hash = (x * 17 + y * 31) & 15;
                    tiles[x, y] = hash == 0 ? SoundwellTileKind.FloorPuddle
                        : hash == 3 ? SoundwellTileKind.FloorCracked
                        : hash == 7 ? SoundwellTileKind.FloorMoss
                        : SoundwellTileKind.FloorPlain;
                }
            }
        }
    }

    /// <summary>
    /// 140x84 (11,760-tile) explorable Red Salt Outer Harbor district map
    /// linking every delivered M05 scene to a physical walkable location.
    /// </summary>
    public sealed class RedSaltHarborMapLayout
    {
        public int Width { get; }
        public int Height { get; }
        public HarborTileKind[,] Tiles { get; }
        public Bounds CameraBounds { get; }
        public Vector2Int PlayerSpawn { get; }
        public IReadOnlyList<RpgMapZone> Districts { get; }
        public IReadOnlyList<RpgMapTriggerAnchor> Triggers { get; }
        public IReadOnlyList<RpgLight2DAnchor> PointLights { get; }
        public Color GlobalLightColor { get; }
        public float GlobalLightIntensity { get; }

        private RedSaltHarborMapLayout(int width, int height, HarborTileKind[,] tiles,
            Vector2Int playerSpawn, IReadOnlyList<RpgMapZone> districts,
            IReadOnlyList<RpgMapTriggerAnchor> triggers, IReadOnlyList<RpgLight2DAnchor> pointLights)
        {
            Width = width;
            Height = height;
            Tiles = tiles;
            CameraBounds = RpgWorldMapScale.BuildCameraBounds(width, height);
            PlayerSpawn = playerSpawn;
            Districts = districts;
            Triggers = triggers;
            PointLights = pointLights;
            GlobalLightColor = new Color(0.52f, 0.62f, 0.76f, 1f);
            GlobalLightIntensity = 0.44f;
        }

        public static bool IsWalkable(HarborTileKind tile)
        {
            switch (tile)
            {
                case HarborTileKind.QuayStone:
                case HarborTileKind.BridgePlank:
                case HarborTileKind.BridgeCable:
                    return true;
                default:
                    return false;
            }
        }

        public static RedSaltHarborMapLayout Build()
        {
            const int width = RpgWorldMapScale.RedSaltHarborMinWidth;
            const int height = RpgWorldMapScale.RedSaltHarborMinHeight;
            var tiles = new HarborTileKind[width, height];

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    if (y < 10)
                        tiles[x, y] = HarborTileKind.WaterDeep;
                    else if (y < 16)
                        tiles[x, y] = HarborTileKind.WaterCalm;
                    else if (y < 19)
                        tiles[x, y] = (x % 3 == 0) ? HarborTileKind.WaterFoam : HarborTileKind.WaterShallow;
                    else if (x < 4 || x >= width - 4 || y >= height - 4)
                        tiles[x, y] = HarborTileKind.Crates;
                    else
                        tiles[x, y] = HarborTileKind.QuayStone;
                }
            }

            // Moored shallow tender in water south of the landing berth.
            tiles[16, 17] = HarborTileKind.TenderBoat;
            tiles[18, 19] = HarborTileKind.MooringPost;
            tiles[28, 19] = HarborTileKind.MooringPost;
            // Decorative stall props placed adjacent to (not on top of) trigger tiles.
            tiles[84, 33] = HarborTileKind.RepairStall;
            tiles[76, 59] = HarborTileKind.SaltStall;
            tiles[79, 59] = HarborTileKind.SaltSacks;
            tiles[50, 29] = HarborTileKind.LanternPost;
            tiles[118, 25] = HarborTileKind.LanternPost;

            var districts = new[]
            {
                new RpgMapZone("harbor.outer-berth", "Outer Berth", "外港浅水泊位", new RectInt(8, 19, 26, 14)),
                new RpgMapZone("harbor.quay-promenade", "Quay Promenade", "外港石码头", new RectInt(36, 19, 32, 16)),
                new RpgMapZone("harbor.repair-yard", "Repair Stall Yard", "修船摊院落", new RectInt(70, 21, 30, 18)),
                new RpgMapZone("harbor.se-breakwater", "Southeast Breakwater", "东南防波堤", new RectInt(104, 19, 28, 14)),
                new RpgMapZone("harbor.salt-market", "Red Salt Market", "赤盐盐市广场", new RectInt(52, 42, 48, 24)),
                new RpgMapZone("harbor.mine-approach", "Mine Camp Approach", "矿营入山口", new RectInt(58, 68, 28, 11))
            };

            var triggers = new[]
            {
                new RpgMapTriggerAnchor("m05.landing-check", new Vector2Int(22, 24), 3f),
                new RpgMapTriggerAnchor("m05.seran-letters", new Vector2Int(50, 26), 3f),
                new RpgMapTriggerAnchor("m05.repair-stall", new Vector2Int(84, 30), 3.5f),
                new RpgMapTriggerAnchor("m05.fisher-rumor", new Vector2Int(118, 22), 3.5f),
                new RpgMapTriggerAnchor("m05.salt-market", new Vector2Int(76, 56), 4f),
                new RpgMapTriggerAnchor("m06.mine-camp-gate", new Vector2Int(70, 74), 3.5f)
            };

            Color harborLantern = new Color(1.0f, 0.80f, 0.48f, 1f);
            var lights = new[]
            {
                new RpgLight2DAnchor("light.harbor-berth", new Vector2Int(18, 20), 9f, 0.95f, harborLantern),
                new RpgLight2DAnchor("light.harbor-seran", new Vector2Int(50, 29), 8f, 0.90f, harborLantern),
                new RpgLight2DAnchor("light.harbor-repair", new Vector2Int(84, 33), 8.5f, 0.95f, harborLantern),
                new RpgLight2DAnchor("light.harbor-market", new Vector2Int(76, 59), 10f, 1.0f, harborLantern)
            };

            return new RedSaltHarborMapLayout(width, height, tiles,
                new Vector2Int(16, 24), districts, triggers, lights);
        }
    }

    /// <summary>
    /// 80x44 (3,520-tile) Whitewater Crossing pontoon bridge map for S00.
    /// </summary>
    public sealed class WhitewaterCrossingMapLayout
    {
        public int Width { get; }
        public int Height { get; }
        public HarborTileKind[,] Tiles { get; }
        public Bounds CameraBounds { get; }
        public Vector2Int PlayerSpawn { get; }
        public Vector2Int CableAnchorTile { get; }
        public Vector2Int BridgeheadDefenseTile { get; }
        public Vector2Int FarBankFrontTile { get; }
        public IReadOnlyList<RpgLight2DAnchor> PointLights { get; }

        private WhitewaterCrossingMapLayout(int width, int height, HarborTileKind[,] tiles,
            Vector2Int playerSpawn, Vector2Int cableAnchorTile,
            Vector2Int bridgeheadDefenseTile, Vector2Int farBankFrontTile,
            IReadOnlyList<RpgLight2DAnchor> pointLights)
        {
            Width = width;
            Height = height;
            Tiles = tiles;
            CameraBounds = RpgWorldMapScale.BuildCameraBounds(width, height);
            PlayerSpawn = playerSpawn;
            CableAnchorTile = cableAnchorTile;
            BridgeheadDefenseTile = bridgeheadDefenseTile;
            FarBankFrontTile = farBankFrontTile;
            PointLights = pointLights;
        }

        public static WhitewaterCrossingMapLayout Build()
        {
            const int width = RpgWorldMapScale.WhitewaterCrossingMinWidth;
            const int height = RpgWorldMapScale.WhitewaterCrossingMinHeight;
            var tiles = new HarborTileKind[width, height];

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    if (x <= 20 || x >= 60)
                    {
                        tiles[x, y] = (y < 4 || y >= height - 4 || x < 2 || x >= width - 2)
                            ? HarborTileKind.Crates
                            : HarborTileKind.QuayStone;
                    }
                    else
                    {
                        tiles[x, y] = (x == 21 || x == 59)
                            ? HarborTileKind.WaterFoam
                            : HarborTileKind.WaterDeep;
                    }
                }
            }

            // 40-tile-long, 6-tile-wide pontoon bridge spanning both banks.
            for (int x = 20; x <= 60; x++)
            {
                for (int y = 19; y <= 24; y++)
                {
                    tiles[x, y] = (y == 19 || y == 24)
                        ? HarborTileKind.BridgeCable
                        : HarborTileKind.BridgePlank;
                }
            }

            // Bridgehead braziers flanking the crossing.
            tiles[19, 17] = HarborTileKind.Brazier;
            tiles[19, 26] = HarborTileKind.Brazier;
            tiles[61, 17] = HarborTileKind.Brazier;
            tiles[61, 26] = HarborTileKind.Brazier;

            Color brazierFlame = new Color(1.0f, 0.66f, 0.30f, 1f);
            var lights = new[]
            {
                new RpgLight2DAnchor("light.west-brazier-south", new Vector2Int(19, 17), 9f, 1.1f, brazierFlame),
                new RpgLight2DAnchor("light.west-brazier-north", new Vector2Int(19, 26), 9f, 1.1f, brazierFlame),
                new RpgLight2DAnchor("light.east-brazier-south", new Vector2Int(61, 17), 8f, 0.85f, brazierFlame),
                new RpgLight2DAnchor("light.east-brazier-north", new Vector2Int(61, 26), 8f, 0.85f, brazierFlame)
            };

            return new WhitewaterCrossingMapLayout(width, height, tiles,
                playerSpawn: new Vector2Int(14, 22),
                cableAnchorTile: new Vector2Int(21, 22),
                bridgeheadDefenseTile: new Vector2Int(26, 22),
                farBankFrontTile: new Vector2Int(64, 22),
                pointLights: lights);
        }
    }
}
