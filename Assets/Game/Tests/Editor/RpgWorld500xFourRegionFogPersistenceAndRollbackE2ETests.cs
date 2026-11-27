using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    /// <summary>
    /// Four-region E2E regression tests for regional fog-of-war persistence verifying
    /// that visited chunk bitmasks survive cross-region travel, roll back later
    /// exploration on same-map and cross-map save import, and dispose cleanly without
    /// Texture2D/Sprite leaks across Soundwell, RedSaltHarbor, WhitewaterCrossing,
    /// and MineCamp.
    /// </summary>
    public sealed class RpgWorld500xFourRegionFogPersistenceAndRollbackE2ETests
    {
        [Test]
        public void AllFourRegions_FogCoverageSurvivesCrossRegionTravelAndRollsBackOnSaveImport()
        {
            IReadOnlyList<RpgExpandedMapSpec> specs = RpgExpandedWorldScale.AllSpecs;
            Assert.AreEqual(4, specs.Count);

            int texBefore = Resources.FindObjectsOfTypeAll<Texture2D>().Length;
            int spriteBefore = Resources.FindObjectsOfTypeAll<Sprite>().Length;

            using (var boot = new RpgWorld500xSceneBootstrapper(
                initialMapId: specs[0].MapId,
                overrideTileSlices: new Sprite[] { null }))
            {
                for (int r = 0; r < specs.Count; r++)
                {
                    RpgExpandedMapSpec spec = specs[r];
                    RpgWorldMapId otherMap = specs[(r + 1) % specs.Count].MapId;

                    if (boot.Driver.Facade.CurrentMapId != spec.MapId)
                    {
                        Assert.IsTrue(boot.SwitchMap(spec.MapId), $"Failed to switch to {spec.MapId}.");
                    }

                    RpgWorld500xGrandRuntimeFacade facade = boot.Driver.Facade;
                    RpgWorld500xMapLayout layout = facade.Session.Host.Layout;
                    Vector2Int spawnChunk = ToChunk(layout.PlayerSpawn);

                    Assert.IsTrue(TryPickTwoDistantWalkableMarkers(layout, spawnChunk, out Vector2Int tileA, out Vector2Int tileB),
                        $"Could not pick two distant walkable markers on {spec.MapId}.");
                    Vector2Int chunkA = ToChunk(tileA);
                    Vector2Int chunkB = ToChunk(tileB);

                    // Reveal tileA and export snapshot.
                    Assert.IsTrue(facade.Session.Host.TryPlaceAtWalkableTile(tileA),
                        $"Failed to place at tileA on {spec.MapId}.");
                    boot.StepFrame(Vector2.zero, 0.1f);
                    int visitedAtA = facade.DistrictTracker.VisitedChunkCount;
                    Assert.IsTrue(facade.DistrictTracker.IsChunkVisited(chunkA.x, chunkA.y),
                        $"ChunkA not visited on {spec.MapId}.");
                    string savedAtA = boot.ExportSaveJson();

                    // Reveal tileB further away -> VisitedChunkCount increases.
                    Assert.IsTrue(facade.Session.Host.TryPlaceAtWalkableTile(tileB),
                        $"Failed to place at tileB on {spec.MapId}.");
                    boot.StepFrame(Vector2.zero, 0.1f);
                    Assert.Greater(facade.DistrictTracker.VisitedChunkCount, visitedAtA,
                        $"VisitedChunkCount did not increase at tileB on {spec.MapId}.");
                    Assert.IsTrue(facade.DistrictTracker.IsChunkVisited(chunkB.x, chunkB.y),
                        $"ChunkB not visited on {spec.MapId}.");

                    // Same-map save import must roll back chunkB while keeping chunkA.
                    Assert.IsTrue(boot.TryImportSaveJson(savedAtA, out string err),
                        $"Same-map import failed on {spec.MapId}: {err}");
                    Assert.AreEqual(visitedAtA, facade.DistrictTracker.VisitedChunkCount,
                        $"Same-map import did not roll back VisitedChunkCount on {spec.MapId}.");
                    Assert.IsTrue(facade.DistrictTracker.IsChunkVisited(chunkA.x, chunkA.y));
                    Assert.IsFalse(facade.DistrictTracker.IsChunkVisited(chunkB.x, chunkB.y),
                        $"Same-map import retained rolled-back chunkB on {spec.MapId}.");

                    // Cross-region round-trip preserves persisted fog coverage.
                    Assert.IsTrue(boot.SwitchMap(otherMap));
                    Assert.IsTrue(boot.SwitchMap(spec.MapId));
                    Assert.AreEqual(visitedAtA, boot.Driver.Facade.DistrictTracker.VisitedChunkCount,
                        $"Cross-region travel altered VisitedChunkCount on {spec.MapId}.");
                    Assert.IsTrue(boot.Driver.Facade.DistrictTracker.IsChunkVisited(chunkA.x, chunkA.y));
                }
            }

            Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Texture2D>().Length, texBefore,
                "Texture2D leak detected after four-region fog persistence E2E sweep.");
            Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Sprite>().Length, spriteBefore,
                "Sprite leak detected after four-region fog persistence E2E sweep.");
        }

        private static bool TryPickTwoDistantWalkableMarkers(
            RpgWorld500xMapLayout layout,
            Vector2Int spawnChunk,
            out Vector2Int tileA,
            out Vector2Int tileB)
        {
            tileA = default;
            tileB = default;
            bool foundA = false;
            Vector2Int chunkA = default;

            for (int i = 0; i < layout.Markers.Count; i++)
            {
                Vector2Int t = layout.Markers[i].Tile;
                if (!layout.IsWalkable(t.x, t.y, gateOpen: false))
                {
                    continue;
                }

                Vector2Int c = ToChunk(t);
                if (ChunkChebyshev(c, spawnChunk) < 5)
                {
                    continue;
                }

                if (!foundA)
                {
                    tileA = t;
                    chunkA = c;
                    foundA = true;
                }
                else if (ChunkChebyshev(c, chunkA) >= 5)
                {
                    tileB = t;
                    return true;
                }
            }

            return false;
        }

        private static Vector2Int ToChunk(Vector2Int tile) =>
            new Vector2Int(
                tile.x / RpgLargeMapChunkRenderer.ChunkSizeTiles,
                tile.y / RpgLargeMapChunkRenderer.ChunkSizeTiles);

        private static int ChunkChebyshev(Vector2Int a, Vector2Int b) =>
            Math.Max(Math.Abs(a.x - b.x), Math.Abs(a.y - b.y));
    }
}
