using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xMapLayoutsTests
    {
        private static readonly Vector2Int[] Cardinal =
        {
            new Vector2Int(1, 0),
            new Vector2Int(-1, 0),
            new Vector2Int(0, 1),
            new Vector2Int(0, -1)
        };

        /// <summary>
        /// Fast coarse-stride BFS (stride 8 tiles, aligned with 24-tile avenues
        /// and 28x28 landmark plazas) verifying physical connectivity across
        /// 500x-area maps in milliseconds without allocating multi-million HashSets.
        /// </summary>
        private static HashSet<Vector2Int> CoarseReachable(RpgWorld500xMapLayout map,
            Vector2Int startTile, bool gateOpen, int stride = 8)
        {
            int cw = map.Width / stride;
            int ch = map.Height / stride;
            var startCell = new Vector2Int(startTile.x / stride, startTile.y / stride);
            var visited = new HashSet<Vector2Int>();
            if (!CellWalkable(map, startCell, stride, gateOpen))
                return visited;

            var queue = new Queue<Vector2Int>();
            visited.Add(startCell);
            queue.Enqueue(startCell);
            while (queue.Count > 0)
            {
                Vector2Int cur = queue.Dequeue();
                for (int i = 0; i < Cardinal.Length; i++)
                {
                    int nx = cur.x + Cardinal[i].x;
                    int ny = cur.y + Cardinal[i].y;
                    if (nx < 0 || nx >= cw || ny < 0 || ny >= ch)
                        continue;
                    var next = new Vector2Int(nx, ny);
                    if (!visited.Contains(next) &&
                        CellWalkable(map, next, stride, gateOpen) &&
                        BridgeWalkable(map, cur, next, stride, gateOpen))
                    {
                        visited.Add(next);
                        queue.Enqueue(next);
                    }
                }
            }
            return visited;
        }

        private static bool CellWalkable(RpgWorld500xMapLayout map, Vector2Int cell,
            int stride, bool gateOpen)
        {
            int tx = cell.x * stride + stride / 2;
            int ty = cell.y * stride + stride / 2;
            return map.IsWalkable(tx, ty, gateOpen);
        }

        private static bool BridgeWalkable(RpgWorld500xMapLayout map, Vector2Int a,
            Vector2Int b, int stride, bool gateOpen)
        {
            int ax = a.x * stride + stride / 2;
            int ay = a.y * stride + stride / 2;
            int bx = b.x * stride + stride / 2;
            int by = b.y * stride + stride / 2;
            int dx = Math.Sign(bx - ax);
            int dy = Math.Sign(by - ay);
            int x = ax;
            int y = ay;
            while (x != bx || y != by)
            {
                x += dx;
                y += dy;
                if (!map.IsWalkable(x, y, gateOpen))
                    return false;
            }
            return true;
        }

        [Test]
        public void AllFour500xMapsReachEveryLandmarkMarkerAndRespectSoundwellIronGate()
        {
            foreach (RpgExpandedMapSpec spec in RpgExpandedWorldScale.AllSpecs)
            {
                RpgWorld500xMapLayout map = RpgWorld500xMapLayout.Build(spec.MapId);
                Assert.That(map.Width, Is.EqualTo(spec.ExpandedWidth));
                Assert.That(map.Height, Is.EqualTo(spec.ExpandedHeight));
                Assert.That(map.Spec.AreaMultiplier, Is.GreaterThanOrEqualTo(495f));
                Assert.That(map.IsWalkable(map.PlayerSpawn.x, map.PlayerSpawn.y, gateOpen: false), Is.True,
                    spec.DisplayNameEn + " spawn must be walkable.");

                if (spec.MapId == RpgWorldMapId.Soundwell)
                {
                    HashSet<Vector2Int> closed = CoarseReachable(map, map.PlayerSpawn, gateOpen: false);
                    HashSet<Vector2Int> open = CoarseReachable(map, map.PlayerSpawn, gateOpen: true);

                    foreach (RpgMapLandmarkMarker marker in map.Markers)
                    {
                        Assert.That(map.IsWalkable(marker.Tile.x, marker.Tile.y, gateOpen: true), Is.True, marker.Id);
                        var cell = new Vector2Int(marker.Tile.x / 8, marker.Tile.y / 8);
                        Assert.That(open.Contains(cell), Is.True,
                            "Soundwell marker must be reachable after opening gate: " + marker.Id);

                        bool postGate = string.Equals(marker.DistrictId, "soundwell.extraction", StringComparison.Ordinal);
                        Assert.That(closed.Contains(cell), Is.EqualTo(!postGate),
                            "Soundwell iron gate must block only south extraction markers: " + marker.Id);
                    }
                }
                else
                {
                    HashSet<Vector2Int> reachable = CoarseReachable(map, map.PlayerSpawn, gateOpen: true);
                    foreach (RpgMapLandmarkMarker marker in map.Markers)
                    {
                        Assert.That(map.IsWalkable(marker.Tile.x, marker.Tile.y, gateOpen: true), Is.True, marker.Id);
                        var cell = new Vector2Int(marker.Tile.x / 8, marker.Tile.y / 8);
                        Assert.That(reachable.Contains(cell), Is.True,
                            spec.DisplayNameEn + " marker must be reachable from spawn: " + marker.Id);
                    }
                }

                byte[,] mini = map.SampleDownsampledGrid(160, 96);
                Assert.That(mini.GetLength(0), Is.EqualTo(160));
                Assert.That(mini.GetLength(1), Is.EqualTo(96));
            }
        }

        [Test]
        public void Expanded500xMapStreamsCleanlyThroughChunkRendererAndDisposesZeroLeak()
        {
            var root = new GameObject("500xStreamingLayoutTest");
            RpgLargeMapChunkRenderer renderer = null;
            try
            {
                RpgWorld500xMapLayout harbor = RpgWorld500xMapLayout.Build(RpgWorldMapId.RedSaltHarbor);
                renderer = harbor.CreateStreamingRenderer(root.transform, streamingRadiusChunks: 1,
                    tileSlices: new Sprite[] { null });

                Assert.That(renderer.IsStreaming, Is.True);
                Assert.That(renderer.TotalPossibleChunks, Is.EqualTo(98 * 59));
                Assert.That(renderer.ChunkCount, Is.LessThanOrEqualTo(9));
                Assert.That(renderer.LightHaloCount, Is.EqualTo(harbor.Markers.Count));

                Assert.That(renderer.UpdateStreamingFocus(new Vector2(1568f, 1660f)), Is.True);
                Assert.That(renderer.ChunkCount, Is.LessThanOrEqualTo(9));

                renderer.Dispose();
                Assert.That(renderer.IsDisposed, Is.True);
                Assert.That(root.transform.childCount, Is.Zero);
            }
            finally
            {
                renderer?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
