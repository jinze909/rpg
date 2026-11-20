using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class M06MineCampMapLayoutTests
    {
        private static readonly Vector2Int[] CardinalSteps =
        {
            new Vector2Int(1, 0),
            new Vector2Int(-1, 0),
            new Vector2Int(0, 1),
            new Vector2Int(0, -1)
        };

        private static HashSet<Vector2Int> FloodFill(M06MineCampMapLayout map)
        {
            var visited = new HashSet<Vector2Int>();
            if (!M06MineCampMapLayout.IsWalkable(map.Tiles[map.PlayerSpawn.x, map.PlayerSpawn.y]))
                return visited;

            var queue = new Queue<Vector2Int>();
            visited.Add(map.PlayerSpawn);
            queue.Enqueue(map.PlayerSpawn);
            while (queue.Count > 0)
            {
                Vector2Int cur = queue.Dequeue();
                for (int i = 0; i < CardinalSteps.Length; i++)
                {
                    int nx = cur.x + CardinalSteps[i].x;
                    int ny = cur.y + CardinalSteps[i].y;
                    if (nx < 0 || nx >= map.Width || ny < 0 || ny >= map.Height)
                        continue;
                    var next = new Vector2Int(nx, ny);
                    if (!visited.Contains(next) &&
                        M06MineCampMapLayout.IsWalkable(map.Tiles[nx, ny]))
                    {
                        visited.Add(next);
                        queue.Enqueue(next);
                    }
                }
            }
            return visited;
        }

        [Test]
        public void MineCampLargeMapMeets4xScaleAndConnectsEveryM06Trigger()
        {
            M06MineCampMapLayout map = M06MineCampMapLayout.Build();

            Assert.That(map.Width, Is.GreaterThanOrEqualTo(M06MineCampMapLayout.MinWidth));
            Assert.That(map.Height, Is.GreaterThanOrEqualTo(M06MineCampMapLayout.MinHeight));
            Assert.That(map.Width / RpgWorldMapScale.LogicalScreenTilesX, Is.GreaterThanOrEqualTo(4.5f));
            Assert.That(map.Height / RpgWorldMapScale.LogicalScreenTilesY, Is.GreaterThanOrEqualTo(5.0f));
            Assert.That(map.Districts.Count, Is.EqualTo(6));
            Assert.That(map.Triggers.Count, Is.EqualTo(6));
            Assert.That(map.PointLights.Count, Is.GreaterThanOrEqualTo(5));

            HashSet<Vector2Int> reachable = FloodFill(map);
            foreach (RpgMapTriggerAnchor trigger in map.Triggers)
            {
                Assert.That(reachable.Contains(trigger.Tile), Is.True,
                    "M06 trigger must be walkable from south gate spawn: " + trigger.Id);
            }

            foreach (RpgMapZone district in map.Districts)
            {
                var center = new Vector2Int(
                    district.Bounds.xMin + district.Bounds.width / 2,
                    district.Bounds.yMin + district.Bounds.height / 2);
                Assert.That(reachable.Contains(center), Is.True,
                    "District center must be reachable from spawn: " + district.Id);
            }

            foreach (RpgLight2DAnchor light in map.PointLights)
            {
                Assert.That(map.CameraBounds.Contains(new Vector3(light.Tile.x, light.Tile.y, 0f)),
                    Is.True, light.Id);
            }
        }

        [Test]
        public void MineCampDistrictsAreDistinctAndCoverAllTriggers()
        {
            M06MineCampMapLayout map = M06MineCampMapLayout.Build();
            var ids = new HashSet<string>();

            foreach (RpgMapZone district in map.Districts)
            {
                Assert.That(ids.Add(district.Id), Is.True, "Duplicate district ID: " + district.Id);
                Assert.That(district.DisplayNameEn, Is.Not.Empty);
                Assert.That(district.DisplayNameZh, Is.Not.Empty);
                Assert.That(district.Bounds.width, Is.GreaterThan(0));
                Assert.That(district.Bounds.height, Is.GreaterThan(0));
            }

            foreach (RpgMapTriggerAnchor trigger in map.Triggers)
            {
                Assert.That(map.FindDistrictAtTile(trigger.Tile).HasValue, Is.True,
                    "Trigger must belong to a named M06 district: " + trigger.Id);
            }
        }

        [Test]
        public void FindDistrictAtTileReturnsExactRoomAndClearsInConnectorCorridors()
        {
            M06MineCampMapLayout map = M06MineCampMapLayout.Build();

            RpgMapZone? spawnZone = map.FindDistrictAtTile(map.PlayerSpawn);
            Assert.That(spawnZone.HasValue, Is.True);
            Assert.That(spawnZone.Value.Id, Is.EqualTo("minecamp.south-gate"));

            RpgMapZone? pumpZone = map.FindDistrictAtTile(new Vector2Int(54, 36));
            Assert.That(pumpZone.HasValue, Is.True);
            Assert.That(pumpZone.Value.Id, Is.EqualTo("minecamp.main-pump-hall"));

            var corridorTile = new Vector2Int(30, 12);
            Assert.That(M06MineCampMapLayout.IsWalkable(map.Tiles[corridorTile.x, corridorTile.y]),
                Is.True);
            Assert.That(map.FindDistrictAtTile(corridorTile).HasValue, Is.False,
                "Connector corridor between districts must not report a stale room name.");
        }
    }
}
