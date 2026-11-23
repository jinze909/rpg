using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Compact 1-byte-per-tile 500x-area world map layout for Soundwell
    /// (2144x1344), Red Salt Harbor (3136x1888), Whitewater Crossing (1792x992),
    /// and Red Salt Mine Camp (2144x1440). Pairs directly with
    /// RpgLargeMapChunkRenderer.BuildStreaming and RpgWorldLandmarkCatalog.
    /// </summary>
    public sealed class RpgWorld500xMapLayout
    {
        public RpgWorldMapId MapId { get; }
        public RpgExpandedMapSpec Spec { get; }
        public int Width => Spec.ExpandedWidth;
        public int Height => Spec.ExpandedHeight;
        public bool UsesHarborPalette { get; }
        public byte[,] Tiles { get; }
        public Bounds CameraBounds { get; }
        public Vector2Int PlayerSpawn { get; }
        public IReadOnlyList<RpgMapZone> Districts { get; }
        public IReadOnlyList<RpgMapLandmarkMarker> Markers { get; }
        public IReadOnlyList<RpgLandmarkQuestDefinition> Quests { get; }
        public IReadOnlyList<RpgLight2DAnchor> PointLights { get; }

        private RpgWorld500xMapLayout(
            RpgWorldMapId mapId,
            RpgExpandedMapSpec spec,
            bool usesHarborPalette,
            byte[,] tiles,
            Vector2Int playerSpawn,
            IReadOnlyList<RpgMapZone> districts,
            IReadOnlyList<RpgMapLandmarkMarker> markers,
            IReadOnlyList<RpgLandmarkQuestDefinition> quests,
            IReadOnlyList<RpgLight2DAnchor> pointLights)
        {
            MapId = mapId;
            Spec = spec;
            UsesHarborPalette = usesHarborPalette;
            Tiles = tiles;
            CameraBounds = spec.BuildExpandedCameraBounds();
            PlayerSpawn = playerSpawn;
            Districts = districts;
            Markers = markers;
            Quests = quests;
            PointLights = pointLights;
        }

        public int GetTileId(int x, int y)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height)
                return UsesHarborPalette ? (int)HarborTileKind.WaterDeep : (int)SoundwellTileKind.WallTop;
            return Tiles[x, y];
        }

        public bool IsWalkable(int x, int y, bool gateOpen = true)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height)
                return false;
            int id = Tiles[x, y];
            return UsesHarborPalette
                ? RedSaltHarborMapLayout.IsWalkable((HarborTileKind)id)
                : SoundwellLargeMapLayout.IsWalkable((SoundwellTileKind)id, gateOpen);
        }

        public RpgMapZone? FindDistrictAtTile(Vector2Int tile)
        {
            for (int i = 0; i < Districts.Count; i++)
            {
                if (Districts[i].Bounds.Contains(tile))
                    return Districts[i];
            }
            return null;
        }

        public RpgLargeMapChunkRenderer CreateStreamingRenderer(
            Transform parent,
            int streamingRadiusChunks = RpgExpandedWorldScale.DefaultStreamingRadiusChunks,
            Sprite[] tileSlices = null)
        {
            return RpgLargeMapChunkRenderer.BuildStreaming(
                parent,
                Spec.DisplayNameEn + " 500x Streaming",
                Width,
                Height,
                GetTileId,
                UsesHarborPalette,
                PointLights,
                new Vector2(PlayerSpawn.x + 0.5f, PlayerSpawn.y + 0.5f),
                streamingRadiusChunks,
                tileSlices);
        }

        public byte[,] SampleDownsampledGrid(int sampleWidth, int sampleHeight)
        {
            if (sampleWidth <= 0) throw new ArgumentOutOfRangeException(nameof(sampleWidth));
            if (sampleHeight <= 0) throw new ArgumentOutOfRangeException(nameof(sampleHeight));

            var grid = new byte[sampleWidth, sampleHeight];
            for (int sy = 0; sy < sampleHeight; sy++)
            {
                int ty = Mathf.Clamp((int)((long)sy * Height / sampleHeight), 0, Height - 1);
                for (int sx = 0; sx < sampleWidth; sx++)
                {
                    int tx = Mathf.Clamp((int)((long)sx * Width / sampleWidth), 0, Width - 1);
                    grid[sx, sy] = Tiles[tx, ty];
                }
            }
            return grid;
        }

        public static RpgWorld500xMapLayout Build(RpgWorldMapId mapId)
        {
            switch (mapId)
            {
                case RpgWorldMapId.Soundwell: return BuildSoundwell();
                case RpgWorldMapId.RedSaltHarbor: return BuildRedSaltHarbor();
                case RpgWorldMapId.WhitewaterCrossing: return BuildWhitewaterCrossing();
                case RpgWorldMapId.MineCamp: return BuildMineCamp();
                default:
                    throw new ArgumentOutOfRangeException(nameof(mapId), mapId, "Unknown map id.");
            }
        }

        private static RpgWorld500xMapLayout BuildSoundwell()
        {
            RpgExpandedMapSpec spec = RpgExpandedWorldScale.GetSpec(RpgWorldMapId.Soundwell);
            int w = spec.ExpandedWidth;
            int h = spec.ExpandedHeight;
            var tiles = new byte[w, h];
            FillRect(tiles, w, h, new RectInt(0, 0, w, h), (byte)SoundwellTileKind.WallTop);

            // Six 500x-scale subterranean macro districts.
            var entry = new RectInt(120, 560, 320, 260);
            var northEcho = new RectInt(340, 900, 960, 300);
            var central = new RectInt(560, 480, 900, 360);
            var eastVault = new RectInt(1640, 480, 380, 480);
            var westGate = new RectInt(480, 140, 640, 220);
            var southExtraction = new RectInt(1320, 140, 680, 260);

            CarveSoundwellFloor(tiles, w, h, entry);
            CarveSoundwellFloor(tiles, w, h, northEcho);
            CarveSoundwellFloor(tiles, w, h, central);
            CarveSoundwellFloor(tiles, w, h, eastVault);
            CarveSoundwellFloor(tiles, w, h, westGate);
            CarveSoundwellFloor(tiles, w, h, southExtraction);

            // Wide arterial corridors (16 tiles wide for smooth 3-member party travel).
            CarveSoundwellFloor(tiles, w, h, new RectInt(420, 656, 160, 24));   // Entry -> Central
            CarveSoundwellFloor(tiles, w, h, new RectInt(240, 800, 24, 180));   // Entry -> North bend
            CarveSoundwellFloor(tiles, w, h, new RectInt(240, 956, 120, 24));   // North bend -> NorthEcho
            CarveSoundwellFloor(tiles, w, h, new RectInt(980, 820, 24, 100));   // Central -> NorthEcho
            CarveSoundwellFloor(tiles, w, h, new RectInt(1440, 648, 220, 24));  // Central -> EastVault
            CarveSoundwellFloor(tiles, w, h, new RectInt(748, 340, 24, 160));   // Central -> WestGate
            CarveSoundwellFloor(tiles, w, h, new RectInt(1100, 228, 240, 24));  // WestGate -> SouthExtraction

            IReadOnlyList<RpgMapLandmarkMarker> markers = RpgWorldLandmarkCatalog.GetMarkers(RpgWorldMapId.Soundwell);
            for (int i = 0; i < markers.Count; i++)
            {
                Vector2Int t = markers[i].Tile;
                CarveSoundwellFloor(tiles, w, h, new RectInt(t.x - 14, t.y - 14, 28, 28));
            }

            // Iron gate physically separates WestGate from SouthExtraction at x = 1200..1203.
            for (int x = 1200; x <= 1203; x++)
            {
                for (int y = 228; y < 252; y++)
                    tiles[x, y] = (byte)SoundwellTileKind.GateClosed;
            }

            // Key story tile landmarks.
            tiles[1790, 760] = (byte)SoundwellTileKind.BellPedestal;
            tiles[1790, 580] = (byte)SoundwellTileKind.WetClothSpot;
            tiles[1740, 240] = (byte)SoundwellTileKind.ExtractionHatch;
            tiles[680, 640] = (byte)SoundwellTileKind.FloorDrain;

            var districts = new[]
            {
                new RpgMapZone("soundwell.entry", "Entry Shaft", "下井入口", entry),
                new RpgMapZone("soundwell.north-echo", "North Echo Gallery", "北侧回声长廊", northEcho),
                new RpgMapZone("soundwell.central-hall", "Central Bell Chamber", "中央钟岭主井厅", central),
                new RpgMapZone("soundwell.east-vault", "Bell & Cloth Vault", "东侧铜铃与湿布室", eastVault),
                new RpgMapZone("soundwell.west-gate", "Iron Gate Corridor", "西侧铁闸暗廊", westGate),
                new RpgMapZone("soundwell.extraction", "South Extraction Hall", "南侧撤退井口", southExtraction)
            };

            return new RpgWorld500xMapLayout(
                RpgWorldMapId.Soundwell, spec, usesHarborPalette: false, tiles,
                playerSpawn: new Vector2Int(224, 672),
                districts, markers,
                RpgWorldLandmarkCatalog.GetQuests(RpgWorldMapId.Soundwell),
                BuildLandmarkLights(markers, new Color(1.0f, 0.78f, 0.45f, 1f)));
        }

        private static RpgWorld500xMapLayout BuildRedSaltHarbor()
        {
            RpgExpandedMapSpec spec = RpgExpandedWorldScale.GetSpec(RpgWorldMapId.RedSaltHarbor);
            int w = spec.ExpandedWidth;
            int h = spec.ExpandedHeight;
            var tiles = new byte[w, h];

            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++)
                {
                    if (y < 240)
                        tiles[x, y] = (byte)HarborTileKind.WaterDeep;
                    else if (y < 360)
                        tiles[x, y] = (byte)HarborTileKind.WaterCalm;
                    else if (y < 420)
                        tiles[x, y] = (x % 4 == 0) ? (byte)HarborTileKind.WaterFoam : (byte)HarborTileKind.WaterShallow;
                    else if (x < 24 || x >= w - 24 || y >= h - 24)
                        tiles[x, y] = (byte)HarborTileKind.Crates;
                    else
                        tiles[x, y] = (byte)HarborTileKind.QuayStone;
                }
            }

            // Decorative waterfront piers and warehouse props placed clear of all 20 marker plazas.
            tiles[360, 412] = (byte)HarborTileKind.TenderBoat;
            tiles[340, 422] = (byte)HarborTileKind.MooringPost;
            tiles[380, 422] = (byte)HarborTileKind.MooringPost;
            tiles[1880, 696] = (byte)HarborTileKind.RepairStall;
            tiles[1700, 1288] = (byte)HarborTileKind.SaltStall;
            tiles[1708, 1288] = (byte)HarborTileKind.SaltSacks;

            IReadOnlyList<RpgMapLandmarkMarker> markers = RpgWorldLandmarkCatalog.GetMarkers(RpgWorldMapId.RedSaltHarbor);
            for (int i = 0; i < markers.Count; i++)
            {
                Vector2Int t = markers[i].Tile;
                FillRect(tiles, w, h, new RectInt(t.x - 14, t.y - 14, 28, 28), (byte)HarborTileKind.QuayStone);
            }

            var districts = new[]
            {
                new RpgMapZone("harbor.outer-berth", "Outer Berth", "外港浅水泊位", new RectInt(120, 420, 560, 460)),
                new RpgMapZone("harbor.quay-promenade", "Quay Promenade", "外港石码头", new RectInt(700, 420, 760, 580)),
                new RpgMapZone("harbor.repair-yard", "Repair Stall Yard", "修船摊院落", new RectInt(1520, 440, 780, 540)),
                new RpgMapZone("harbor.se-breakwater", "Southeast Breakwater", "东南防波堤", new RectInt(2360, 420, 680, 520)),
                new RpgMapZone("harbor.salt-market", "Red Salt Market", "赤盐盐市广场", new RectInt(480, 1020, 2060, 480)),
                new RpgMapZone("harbor.mine-approach", "Mine Camp Approach", "矿营入山口", new RectInt(980, 1520, 1140, 280))
            };

            return new RpgWorld500xMapLayout(
                RpgWorldMapId.RedSaltHarbor, spec, usesHarborPalette: true, tiles,
                playerSpawn: new Vector2Int(360, 540),
                districts, markers,
                RpgWorldLandmarkCatalog.GetQuests(RpgWorldMapId.RedSaltHarbor),
                BuildLandmarkLights(markers, new Color(1.0f, 0.80f, 0.48f, 1f)));
        }

        private static RpgWorld500xMapLayout BuildWhitewaterCrossing()
        {
            RpgExpandedMapSpec spec = RpgExpandedWorldScale.GetSpec(RpgWorldMapId.WhitewaterCrossing);
            int w = spec.ExpandedWidth;
            int h = spec.ExpandedHeight;
            var tiles = new byte[w, h];

            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++)
                {
                    if (x <= 488 || x >= 1320)
                    {
                        bool border = x < 24 || x >= w - 24 || y < 24 || y >= h - 24;
                        tiles[x, y] = border ? (byte)HarborTileKind.Crates : (byte)HarborTileKind.QuayStone;
                    }
                    else
                    {
                        bool foam = x <= 496 || x >= 1312;
                        tiles[x, y] = foam ? (byte)HarborTileKind.WaterFoam : (byte)HarborTileKind.WaterDeep;
                    }
                }
            }

            // 872-tile-long, 28-tile-wide pontoon bridge spanning West Bank to East Bank.
            for (int x = 468; x <= 1340; x++)
            {
                for (int y = 482; y <= 510; y++)
                {
                    bool rail = y == 482 || y == 510;
                    tiles[x, y] = rail ? (byte)HarborTileKind.BridgeCable : (byte)HarborTileKind.BridgePlank;
                }
            }

            // Flanking bridgehead braziers adjacent to bridgehead markers.
            tiles[420, 360] = (byte)HarborTileKind.Brazier;
            tiles[420, 630] = (byte)HarborTileKind.Brazier;
            tiles[1380, 360] = (byte)HarborTileKind.Brazier;
            tiles[1380, 630] = (byte)HarborTileKind.Brazier;

            IReadOnlyList<RpgMapLandmarkMarker> markers = RpgWorldLandmarkCatalog.GetMarkers(RpgWorldMapId.WhitewaterCrossing);
            var districts = new[]
            {
                new RpgMapZone("whitewater.west-bank", "West Bank Staging", "西岸前线集结区", new RectInt(40, 60, 448, 872)),
                new RpgMapZone("whitewater.pontoon", "Whitewater Pontoon Span", "白水渡超长浮桥", new RectInt(488, 460, 832, 72)),
                new RpgMapZone("whitewater.east-bank", "East Bank Bridgehead", "东岸桥头与纵深", new RectInt(1320, 60, 432, 872))
            };

            return new RpgWorld500xMapLayout(
                RpgWorldMapId.WhitewaterCrossing, spec, usesHarborPalette: true, tiles,
                playerSpawn: new Vector2Int(240, 496),
                districts, markers,
                RpgWorldLandmarkCatalog.GetQuests(RpgWorldMapId.WhitewaterCrossing),
                BuildLandmarkLights(markers, new Color(1.0f, 0.66f, 0.30f, 1f)));
        }

        private static RpgWorld500xMapLayout BuildMineCamp()
        {
            RpgExpandedMapSpec spec = RpgExpandedWorldScale.GetSpec(RpgWorldMapId.MineCamp);
            int w = spec.ExpandedWidth;
            int h = spec.ExpandedHeight;
            var tiles = new byte[w, h];
            FillRect(tiles, w, h, new RectInt(0, 0, w, h), (byte)SoundwellTileKind.WallTop);

            var southGate = new RectInt(180, 140, 420, 280);
            var patrolPost = new RectInt(640, 160, 660, 300);
            var supplyYard = new RectInt(1420, 180, 580, 460);
            var shiftCorridor = new RectInt(240, 560, 1320, 380);
            var upperGallery = new RectInt(440, 1100, 1020, 240);
            var pumpHall = new RectInt(1460, 880, 560, 500);

            CarveSoundwellFloor(tiles, w, h, southGate);
            CarveSoundwellFloor(tiles, w, h, patrolPost);
            CarveSoundwellFloor(tiles, w, h, supplyYard);
            CarveSoundwellFloor(tiles, w, h, shiftCorridor);
            CarveSoundwellFloor(tiles, w, h, upperGallery);
            CarveSoundwellFloor(tiles, w, h, pumpHall);

            // Wide interconnected mine avenues (24 tiles wide).
            CarveSoundwellFloor(tiles, w, h, new RectInt(580, 260, 80, 24));    // SouthGate -> PatrolPost
            CarveSoundwellFloor(tiles, w, h, new RectInt(1280, 280, 160, 24));  // PatrolPost -> SupplyYard
            CarveSoundwellFloor(tiles, w, h, new RectInt(420, 400, 24, 180));   // SouthGate -> ShiftCorridor
            CarveSoundwellFloor(tiles, w, h, new RectInt(948, 440, 24, 140));   // PatrolPost -> ShiftCorridor
            CarveSoundwellFloor(tiles, w, h, new RectInt(1520, 620, 180, 280)); // SupplyYard/Shift -> PumpHall
            CarveSoundwellFloor(tiles, w, h, new RectInt(968, 920, 24, 200));   // ShiftCorridor -> UpperGallery
            CarveSoundwellFloor(tiles, w, h, new RectInt(1440, 1180, 60, 24));  // UpperGallery -> PumpHall

            IReadOnlyList<RpgMapLandmarkMarker> markers = RpgWorldLandmarkCatalog.GetMarkers(RpgWorldMapId.MineCamp);
            for (int i = 0; i < markers.Count; i++)
            {
                Vector2Int t = markers[i].Tile;
                CarveSoundwellFloor(tiles, w, h, new RectInt(t.x - 14, t.y - 14, 28, 28));
            }

            var districts = new[]
            {
                new RpgMapZone("minecamp.south-gate", "Mine South Gate", "矿营南门入口", southGate),
                new RpgMapZone("minecamp.patrol-checkpost", "Escort & Patrol Checkpost", "护矿队与巡逻岗", patrolPost),
                new RpgMapZone("minecamp.supply-yard", "Supply Ledger & Salt Yard", "物资账房与盐煤场", supplyYard),
                new RpgMapZone("minecamp.shift-corridor", "Shift Change Corridor", "换班通道与宿舍区", shiftCorridor),
                new RpgMapZone("minecamp.upper-gallery", "Upper Evacuation Gallery", "上层疏散通道", upperGallery),
                new RpgMapZone("minecamp.pump-hall", "Underground Main Pump Hall", "地下主泵房大厅", pumpHall)
            };

            return new RpgWorld500xMapLayout(
                RpgWorldMapId.MineCamp, spec, usesHarborPalette: false, tiles,
                playerSpawn: new Vector2Int(420, 220),
                districts, markers,
                RpgWorldLandmarkCatalog.GetQuests(RpgWorldMapId.MineCamp),
                BuildLandmarkLights(markers, new Color(0.95f, 0.74f, 0.42f, 1f)));
        }

        private static RpgLight2DAnchor[] BuildLandmarkLights(
            IReadOnlyList<RpgMapLandmarkMarker> markers, Color color)
        {
            var lights = new RpgLight2DAnchor[markers.Count];
            for (int i = 0; i < markers.Count; i++)
            {
                RpgMapLandmarkMarker m = markers[i];
                float radius = m.SupportsFastTravel ? 12f : 8f;
                float intensity = m.Category == RpgMapMarkerCategory.MainStory ? 1.05f : 0.88f;
                lights[i] = new RpgLight2DAnchor("light." + m.Id, m.Tile, radius, intensity, color);
            }
            return lights;
        }

        private static void FillRect(byte[,] tiles, int width, int height, RectInt rect, byte value)
        {
            int xMin = Mathf.Max(0, rect.xMin);
            int xMax = Mathf.Min(width, rect.xMax);
            int yMin = Mathf.Max(0, rect.yMin);
            int yMax = Mathf.Min(height, rect.yMax);
            for (int x = xMin; x < xMax; x++)
            {
                for (int y = yMin; y < yMax; y++)
                    tiles[x, y] = value;
            }
        }

        private static void CarveSoundwellFloor(byte[,] tiles, int width, int height, RectInt rect)
        {
            int xMin = Mathf.Max(1, rect.xMin);
            int xMax = Mathf.Min(width - 1, rect.xMax);
            int yMin = Mathf.Max(1, rect.yMin);
            int yMax = Mathf.Min(height - 1, rect.yMax);
            for (int x = xMin; x < xMax; x++)
            {
                for (int y = yMin; y < yMax; y++)
                {
                    int hash = (x * 17 + y * 31) & 15;
                    tiles[x, y] = hash == 0 ? (byte)SoundwellTileKind.FloorPuddle
                        : hash == 3 ? (byte)SoundwellTileKind.FloorCracked
                        : hash == 7 ? (byte)SoundwellTileKind.FloorMoss
                        : (byte)SoundwellTileKind.FloorPlain;
                }
            }
        }
    }
}
