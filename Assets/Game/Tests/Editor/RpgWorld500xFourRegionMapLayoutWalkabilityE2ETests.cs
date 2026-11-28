using System;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    [TestFixture]
    public sealed class RpgWorld500xFourRegionMapLayoutWalkabilityE2ETests
    {
        [Test]
        public void AllFourRegions_OutOfBoundsGuardsAndDistrictLookup_HoldAcrossEveryMap()
        {
            var specs = RpgExpandedWorldScale.AllSpecs;
            Assert.AreEqual(4, specs.Count, "Expected exactly 4 regions in 500x world scale.");

            foreach (RpgExpandedMapSpec spec in specs)
            {
                RpgWorld500xMapLayout layout = RpgWorld500xMapLayout.Build(spec.MapId);
                Assert.AreEqual(spec.MapId, layout.MapId);
                Assert.AreEqual(spec.ExpandedWidth, layout.Width);
                Assert.AreEqual(spec.ExpandedHeight, layout.Height);
                Assert.Greater(layout.Districts.Count, 0, $"Region {spec.MapId} should define districts.");
                Assert.Greater(layout.Markers.Count, 0, $"Region {spec.MapId} should define landmark markers.");
                Assert.AreEqual(layout.Markers.Count, layout.PointLights.Count, $"PointLights count should match Markers count in {spec.MapId}.");

                Assert.IsFalse(layout.IsWalkable(-1, 0), $"Negative X must not be walkable in {spec.MapId}.");
                Assert.IsFalse(layout.IsWalkable(0, -1), $"Negative Y must not be walkable in {spec.MapId}.");
                Assert.IsFalse(layout.IsWalkable(layout.Width, 0), $"Width X must not be walkable in {spec.MapId}.");
                Assert.IsFalse(layout.IsWalkable(0, layout.Height), $"Height Y must not be walkable in {spec.MapId}.");

                int expectedOobTile = layout.UsesHarborPalette
                    ? (int)HarborTileKind.WaterDeep
                    : (int)SoundwellTileKind.WallTop;
                Assert.AreEqual(expectedOobTile, layout.GetTileId(-1, -1), $"Negative OOB tile sentinel mismatch in {spec.MapId}.");
                Assert.AreEqual(expectedOobTile, layout.GetTileId(layout.Width, layout.Height), $"Upper OOB tile sentinel mismatch in {spec.MapId}.");

                Assert.IsTrue(layout.IsWalkable(layout.PlayerSpawn.x, layout.PlayerSpawn.y, gateOpen: true), $"PlayerSpawn should be walkable (gate open) in {spec.MapId}.");
                Assert.IsTrue(layout.IsWalkable(layout.PlayerSpawn.x, layout.PlayerSpawn.y, gateOpen: false), $"PlayerSpawn should be walkable (gate closed) in {spec.MapId}.");
                Assert.IsTrue(layout.FindDistrictAtTile(layout.PlayerSpawn).HasValue, $"PlayerSpawn should fall within a defined district in {spec.MapId}.");
                Assert.IsFalse(layout.FindDistrictAtTile(new Vector2Int(-10, -10)).HasValue, $"OOB tile should not resolve to a district in {spec.MapId}.");
            }
        }

        [Test]
        public void AllFourRegions_DownsampledGridSampling_ValidatesDimensionsAndMatchesSampledTiles()
        {
            var specs = RpgExpandedWorldScale.AllSpecs;
            Assert.AreEqual(4, specs.Count, "Expected exactly 4 regions in 500x world scale.");

            const int sampleWidth = 64;
            const int sampleHeight = 48;

            foreach (RpgExpandedMapSpec spec in specs)
            {
                RpgWorld500xMapLayout layout = RpgWorld500xMapLayout.Build(spec.MapId);
                Assert.Throws<ArgumentOutOfRangeException>(() => layout.SampleDownsampledGrid(0, sampleHeight));
                Assert.Throws<ArgumentOutOfRangeException>(() => layout.SampleDownsampledGrid(sampleWidth, 0));

                byte[,] grid = layout.SampleDownsampledGrid(sampleWidth, sampleHeight);
                Assert.AreEqual(sampleWidth, grid.GetLength(0));
                Assert.AreEqual(sampleHeight, grid.GetLength(1));

                int[] sampleXs = { 0, sampleWidth / 2, sampleWidth - 1 };
                int[] sampleYs = { 0, sampleHeight / 2, sampleHeight - 1 };
                foreach (int sx in sampleXs)
                {
                    int tx = Mathf.Clamp((int)((long)sx * layout.Width / sampleWidth), 0, layout.Width - 1);
                    foreach (int sy in sampleYs)
                    {
                        int ty = Mathf.Clamp((int)((long)sy * layout.Height / sampleHeight), 0, layout.Height - 1);
                        Assert.AreEqual(layout.Tiles[tx, ty], grid[sx, sy], $"Downsampled tile mismatch at ({sx},{sy}) in {spec.MapId}.");
                    }
                }
            }
        }
    }
}
