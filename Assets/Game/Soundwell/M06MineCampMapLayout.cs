using System.Collections.Generic;
using UnityEngine;

namespace Rpg.Soundwell
{
    /// <summary>
    /// 96x64 (6,144-tile) 4x-scale Red Salt Mine Camp and Underground Pump Room
    /// map layout for M06 ("她没有在原地等你"), mapped directly to the existing
    /// 16-PPU SoundwellTileKind tileset for chunk rendering and 2D lighting.
    /// </summary>
    public sealed class M06MineCampMapLayout
    {
        public const int MinWidth = 96;
        public const int MinHeight = 64;

        public int Width { get; }
        public int Height { get; }
        public SoundwellTileKind[,] Tiles { get; }
        public Bounds CameraBounds { get; }
        public Vector2Int PlayerSpawn { get; }
        public IReadOnlyList<RpgMapZone> Districts { get; }
        public IReadOnlyList<RpgMapTriggerAnchor> Triggers { get; }
        public IReadOnlyList<RpgLight2DAnchor> PointLights { get; }
        public Color GlobalLightColor { get; }
        public float GlobalLightIntensity { get; }

        private M06MineCampMapLayout(
            int width,
            int height,
            SoundwellTileKind[,] tiles,
            Vector2Int playerSpawn,
            IReadOnlyList<RpgMapZone> districts,
            IReadOnlyList<RpgMapTriggerAnchor> triggers,
            IReadOnlyList<RpgLight2DAnchor> pointLights)
        {
            Width = width;
            Height = height;
            Tiles = tiles;
            CameraBounds = RpgWorldMapScale.BuildCameraBounds(width, height);
            PlayerSpawn = playerSpawn;
            Districts = districts;
            Triggers = triggers;
            PointLights = pointLights;
            GlobalLightColor = new Color(0.58f, 0.62f, 0.72f, 1f);
            GlobalLightIntensity = 0.40f;
        }

        public static bool IsWalkable(SoundwellTileKind tile)
        {
            return SoundwellLargeMapLayout.IsWalkable(tile, gateOpen: true);
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

        public static M06MineCampMapLayout Build()
        {
            const int width = MinWidth;
            const int height = MinHeight;
            var tiles = new SoundwellTileKind[width, height];
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                    tiles[x, y] = SoundwellTileKind.WallTop;

            var southGate = new RectInt(6, 6, 22, 12);
            var escortCheckpost = new RectInt(34, 6, 28, 12);
            var supplyYard = new RectInt(68, 8, 22, 18);
            var shiftBarracks = new RectInt(6, 26, 26, 16);
            var mainPumpHall = new RectInt(38, 26, 32, 20);
            var upperAssembly = new RectInt(36, 50, 44, 10);

            CarveFloor(tiles, southGate);
            CarveFloor(tiles, escortCheckpost);
            CarveFloor(tiles, supplyYard);
            CarveFloor(tiles, shiftBarracks);
            CarveFloor(tiles, mainPumpHall);
            CarveFloor(tiles, upperAssembly);

            // 4-tile-wide connector corridors so a 3-member party moves cleanly.
            CarveFloor(tiles, new RectInt(27, 10, 8, 4));  // SouthGate -> EscortCheckpost
            CarveFloor(tiles, new RectInt(61, 10, 8, 4));  // EscortCheckpost -> SupplyYard
            CarveFloor(tiles, new RectInt(14, 17, 4, 10)); // SouthGate -> ShiftBarracks
            CarveFloor(tiles, new RectInt(31, 32, 8, 4));  // ShiftBarracks -> MainPumpHall
            CarveFloor(tiles, new RectInt(48, 17, 4, 10)); // EscortCheckpost -> MainPumpHall
            CarveFloor(tiles, new RectInt(68, 25, 4, 4));  // SupplyYard -> MainPumpHall
            CarveFloor(tiles, new RectInt(52, 45, 4, 6));  // MainPumpHall -> UpperAssembly

            // Apply grating/moss wall face immediately north of walkable floor.
            for (int x = 1; x < width - 1; x++)
            {
                for (int y = 1; y < height - 1; y++)
                {
                    if (tiles[x, y] == SoundwellTileKind.WallTop &&
                        IsWalkable(tiles[x, y - 1]))
                    {
                        tiles[x, y] = (x + y) % 5 == 0
                            ? SoundwellTileKind.WallMoss
                            : SoundwellTileKind.WallGrating;
                    }
                }
            }

            // Drainage floor grate inside the underground main pump hall.
            tiles[54, 38] = SoundwellTileKind.FloorDrain;
            tiles[62, 36] = SoundwellTileKind.FloorDrain;

            var districts = new[]
            {
                new RpgMapZone("minecamp.south-gate", "Mine Camp South Gate", "矿营入山口", southGate),
                new RpgMapZone("minecamp.escort-checkpost", "Escort & Patrol Checkpost", "押运与看守哨口", escortCheckpost),
                new RpgMapZone("minecamp.supply-yard", "Supply Ledger & Salt Yard", "物资账房与盐箱坪", supplyYard),
                new RpgMapZone("minecamp.shift-barracks", "Shift Change Corridor", "班组换班廊道", shiftBarracks),
                new RpgMapZone("minecamp.main-pump-hall", "Underground Main Pump Hall", "地下主排水泵房", mainPumpHall),
                new RpgMapZone("minecamp.upper-assembly", "Upper Level Evacuation Gallery", "上层安全集结区", upperAssembly)
            };

            var triggers = new[]
            {
                new RpgMapTriggerAnchor("m06.pump-reunion", new Vector2Int(54, 36), 3.5f),
                new RpgMapTriggerAnchor("m06.recon.escort-schedule", new Vector2Int(42, 12), 3.0f),
                new RpgMapTriggerAnchor("m06.recon.guard-patrol", new Vector2Int(56, 12), 3.0f),
                new RpgMapTriggerAnchor("m06.recon.supply-ledger", new Vector2Int(78, 16), 3.0f),
                new RpgMapTriggerAnchor("m06.recon.mine-tunnel-pump", new Vector2Int(62, 34), 3.5f),
                new RpgMapTriggerAnchor("m06.oulin-truth", new Vector2Int(18, 34), 3.0f)
            };

            Color warmLantern = new Color(1.0f, 0.78f, 0.44f, 1f);
            Color pumpAmber = new Color(0.98f, 0.68f, 0.34f, 1f);
            var lights = new[]
            {
                new RpgLight2DAnchor("light.m06-south-gate", new Vector2Int(14, 17), 8f, 0.90f, warmLantern),
                new RpgLight2DAnchor("light.m06-checkpost", new Vector2Int(48, 17), 8.5f, 0.90f, warmLantern),
                new RpgLight2DAnchor("light.m06-supply-yard", new Vector2Int(78, 24), 8.5f, 0.95f, warmLantern),
                new RpgLight2DAnchor("light.m06-main-pump", new Vector2Int(54, 44), 10f, 1.05f, pumpAmber),
                new RpgLight2DAnchor("light.m06-upper-assembly", new Vector2Int(58, 58), 9.5f, 0.95f, warmLantern)
            };

            return new M06MineCampMapLayout(
                width,
                height,
                tiles,
                playerSpawn: new Vector2Int(14, 12),
                districts,
                triggers,
                lights);
        }

        private static void CarveFloor(SoundwellTileKind[,] tiles, RectInt rect)
        {
            for (int x = rect.xMin; x < rect.xMax; x++)
            {
                for (int y = rect.yMin; y < rect.yMax; y++)
                {
                    int hash = (x * 19 + y * 29) & 15;
                    tiles[x, y] = hash == 0 ? SoundwellTileKind.FloorPuddle
                        : hash == 4 ? SoundwellTileKind.FloorCracked
                        : hash == 9 ? SoundwellTileKind.FloorMoss
                        : SoundwellTileKind.FloorPlain;
                }
            }
        }
    }
}
