using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgLargeMapLayoutTests
    {
        private static readonly Vector2Int[] CardinalSteps =
        {
            new Vector2Int(1, 0),
            new Vector2Int(-1, 0),
            new Vector2Int(0, 1),
            new Vector2Int(0, -1)
        };

        private static HashSet<Vector2Int> FloodFill(int width, int height,
            Vector2Int start, System.Func<int, int, bool> isWalkable)
        {
            var visited = new HashSet<Vector2Int>();
            if (!isWalkable(start.x, start.y))
                return visited;

            var queue = new Queue<Vector2Int>();
            visited.Add(start);
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                Vector2Int cur = queue.Dequeue();
                for (int i = 0; i < CardinalSteps.Length; i++)
                {
                    int nx = cur.x + CardinalSteps[i].x;
                    int ny = cur.y + CardinalSteps[i].y;
                    if (nx < 0 || nx >= width || ny < 0 || ny >= height)
                        continue;
                    var next = new Vector2Int(nx, ny);
                    if (!visited.Contains(next) && isWalkable(nx, ny))
                    {
                        visited.Add(next);
                        queue.Enqueue(next);
                    }
                }
            }
            return visited;
        }

        [Test]
        public void SoundwellLargeMapMeets4xRpgScaleAndEnforcesIronGateConnectivity()
        {
            SoundwellLargeMapLayout map = SoundwellLargeMapLayout.Build();

            Assert.That(map.Width, Is.GreaterThanOrEqualTo(RpgWorldMapScale.SoundwellMinWidth));
            Assert.That(map.Height, Is.GreaterThanOrEqualTo(RpgWorldMapScale.SoundwellMinHeight));
            Assert.That(map.Width / RpgWorldMapScale.LogicalScreenTilesX, Is.GreaterThanOrEqualTo(4.5f));
            Assert.That(map.Height / RpgWorldMapScale.LogicalScreenTilesY, Is.GreaterThanOrEqualTo(5.0f));
            Assert.That(map.Zones.Count, Is.EqualTo(6));
            Assert.That(map.PointLights.Count, Is.GreaterThanOrEqualTo(4));

            var beforeGate = FloodFill(map.Width, map.Height, map.PlayerSpawn,
                (x, y) => SoundwellLargeMapLayout.IsWalkable(map.Tiles[x, y], gateOpen: false));
            Assert.That(beforeGate.Contains(map.BronzeBellTile), Is.True);
            Assert.That(beforeGate.Contains(map.WetClothTile), Is.True);
            Assert.That(beforeGate.Contains(map.GateLeverTile), Is.True);
            Assert.That(beforeGate.Contains(map.ExtractionTile), Is.False,
                "Closed iron gate must physically block the extraction hall.");
            foreach (Vector2Int wp in map.BeastPatrolWaypoints)
                Assert.That(beforeGate.Contains(wp), Is.True, "Patrol waypoint must be walkable.");

            var afterGate = FloodFill(map.Width, map.Height, map.PlayerSpawn,
                (x, y) => SoundwellLargeMapLayout.IsWalkable(map.Tiles[x, y], gateOpen: true));
            Assert.That(afterGate.Contains(map.ExtractionTile), Is.True,
                "Opening the iron gate must connect the extraction hall.");
        }

        [Test]
        public void RedSaltHarborLargeMapMeets4xScaleAndReachesEveryM05Trigger()
        {
            RedSaltHarborMapLayout map = RedSaltHarborMapLayout.Build();

            Assert.That(map.Width, Is.GreaterThanOrEqualTo(RpgWorldMapScale.RedSaltHarborMinWidth));
            Assert.That(map.Height, Is.GreaterThanOrEqualTo(RpgWorldMapScale.RedSaltHarborMinHeight));
            Assert.That(map.Width / RpgWorldMapScale.LogicalScreenTilesX, Is.GreaterThanOrEqualTo(7.0f));
            Assert.That(map.Height / RpgWorldMapScale.LogicalScreenTilesY, Is.GreaterThanOrEqualTo(7.0f));
            Assert.That(map.Districts.Count, Is.EqualTo(6));
            Assert.That(map.Triggers.Count, Is.EqualTo(6));

            var reachable = FloodFill(map.Width, map.Height, map.PlayerSpawn,
                (x, y) => RedSaltHarborMapLayout.IsWalkable(map.Tiles[x, y]));
            foreach (RpgMapTriggerAnchor trigger in map.Triggers)
            {
                Assert.That(reachable.Contains(trigger.Tile), Is.True,
                    "Harbor trigger must be walkable from arrival spawn: " + trigger.Id);
            }
            foreach (RpgLight2DAnchor light in map.PointLights)
            {
                Assert.That(map.CameraBounds.Contains(
                    new Vector3(light.Tile.x, light.Tile.y, 0f)), Is.True, light.Id);
            }
        }

        [Test]
        public void WhitewaterCrossingLargeMapConnectsBothBanksAcrossLongPontoonBridge()
        {
            WhitewaterCrossingMapLayout map = WhitewaterCrossingMapLayout.Build();

            Assert.That(map.Width, Is.GreaterThanOrEqualTo(RpgWorldMapScale.WhitewaterCrossingMinWidth));
            Assert.That(map.Height, Is.GreaterThanOrEqualTo(RpgWorldMapScale.WhitewaterCrossingMinHeight));
            Assert.That(map.PointLights.Count, Is.EqualTo(4));

            var reachable = FloodFill(map.Width, map.Height, map.PlayerSpawn,
                (x, y) => RedSaltHarborMapLayout.IsWalkable(map.Tiles[x, y]));
            Assert.That(reachable.Contains(map.CableAnchorTile), Is.True);
            Assert.That(reachable.Contains(map.BridgeheadDefenseTile), Is.True);
            Assert.That(reachable.Contains(map.FarBankFrontTile), Is.True);
        }
    }
}
