using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    /// <summary>
    /// Four-region E2E regression tests verifying streaming chunk window budgets,
    /// minimap texture dimensions, zero native Texture2D/Sprite leaks, spatial chunk
    /// alignment, grid memory budget, and global Makinom key hygiene across Soundwell,
    /// RedSaltHarbor, WhitewaterCrossing, and MineCamp.
    /// </summary>
    public sealed class RpgWorld500xFourRegionStreamingBudgetAndKeyHygieneE2ETests
    {
        [Test]
        public void AllFourRegions_StreamingChunkWindowAndMinimapTextureRespectBudgetsAndReleaseAllNativeAssets()
        {
            IReadOnlyList<RpgExpandedMapSpec> specs = RpgExpandedWorldScale.AllSpecs;
            Assert.AreEqual(4, specs.Count);

            int texBefore = Resources.FindObjectsOfTypeAll<Texture2D>().Length;
            int spriteBefore = Resources.FindObjectsOfTypeAll<Sprite>().Length;

            for (int r = 0; r < specs.Count; r++)
            {
                RpgExpandedMapSpec spec = specs[r];
                var root = new GameObject("FourRegionBudgetSweep_" + spec.MapId);
                RpgLargeMapChunkRenderer renderer = null;
                RpgWorld500xMinimapHud hud = null;

                try
                {
                    RpgWorld500xMapLayout map = RpgWorld500xMapLayout.Build(spec.MapId);
                    renderer = map.CreateStreamingRenderer(
                        root.transform,
                        streamingRadiusChunks: 1,
                        tileSlices: new Sprite[] { null });
                    var tracker = new RpgMapExplorationTracker(spec.MapId);
                    hud = new RpgWorld500xMinimapHud(
                        root.transform,
                        map.Spec,
                        map.GetTileId,
                        map.UsesHarborPalette,
                        tracker);

                    Assert.LessOrEqual(hud.TextureWidth, RpgWorld500xMapIntegrityValidator.MaxMinimapWidth,
                        $"Minimap width exceeded budget on {spec.MapId}.");
                    Assert.LessOrEqual(hud.TextureHeight, RpgWorld500xMapIntegrityValidator.MaxMinimapHeight,
                        $"Minimap height exceeded budget on {spec.MapId}.");

                    Vector2[] sweepPoints =
                    {
                        new Vector2(map.PlayerSpawn.x + 0.5f, map.PlayerSpawn.y + 0.5f),
                        new Vector2(map.Width * 0.25f, map.Height * 0.25f),
                        new Vector2(map.Width * 0.75f, map.Height * 0.25f),
                        new Vector2(map.Width * 0.75f, map.Height * 0.75f),
                        new Vector2(map.Width * 0.25f, map.Height * 0.75f)
                    };

                    for (int i = 0; i < sweepPoints.Length; i++)
                    {
                        Assert.IsTrue(renderer.UpdateStreamingFocus(sweepPoints[i]),
                            $"Streaming focus update failed on {spec.MapId} at point {i}.");
                        Assert.LessOrEqual(renderer.ChunkCount, 9,
                            $"Active chunk window exceeded 3x3 budget on {spec.MapId} at point {i}.");
                        Assert.IsTrue(hud.UpdatePlayerAndCompass(sweepPoints[i]),
                            $"Minimap compass update failed on {spec.MapId} at point {i}.");
                    }
                }
                finally
                {
                    hud?.Dispose();
                    renderer?.Dispose();
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }

            Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Texture2D>().Length, texBefore,
                "Texture2D leak detected after four-region streaming/minimap sweep.");
            Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Sprite>().Length, spriteBefore,
                "Sprite leak detected after four-region streaming/minimap sweep.");
        }

        [Test]
        public void AllFourRegions_SpatialFootprintAndGlobalMakinomKeysPassValidatorWithZeroIssues()
        {
            List<RpgMapValidationIssue> allIssues = RpgWorld500xMapIntegrityValidator.ValidateAllFourMaps();
            Assert.IsEmpty(allIssues, string.Join("\n", allIssues));

            var seenKeys = new HashSet<string>(StringComparer.Ordinal);
            var keyIssues = new List<RpgMapValidationIssue>();
            long totalGridBytes = 0;

            IReadOnlyList<RpgExpandedMapSpec> specs = RpgExpandedWorldScale.AllSpecs;
            Assert.AreEqual(4, specs.Count);

            for (int r = 0; r < specs.Count; r++)
            {
                RpgExpandedMapSpec spec = specs[r];
                RpgWorld500xMapLayout map = RpgWorld500xMapLayout.Build(spec.MapId);
                string mapName = spec.MapId.ToString();

                int w = map.Tiles.GetLength(0);
                int h = map.Tiles.GetLength(1);
                totalGridBytes += (long)w * h;

                Assert.AreEqual(spec.ExpandedWidth, w, $"Width mismatch on {mapName}.");
                Assert.AreEqual(spec.ExpandedHeight, h, $"Height mismatch on {mapName}.");
                Assert.AreEqual(0, w % RpgLargeMapChunkRenderer.ChunkSizeTiles, $"Width not 32-tile aligned on {mapName}.");
                Assert.AreEqual(0, h % RpgLargeMapChunkRenderer.ChunkSizeTiles, $"Height not 32-tile aligned on {mapName}.");
                Assert.IsTrue(map.IsWalkable(map.PlayerSpawn.x, map.PlayerSpawn.y, gateOpen: false),
                    $"PlayerSpawn not walkable on {mapName}.");

                for (int i = 0; i < map.Markers.Count; i++)
                {
                    RpgMapLandmarkMarker marker = map.Markers[i];
                    RpgWorld500xMapIntegrityValidator.ValidateMakinomKeyHygiene(
                        mapName, RpgMapExplorationTracker.MarkerDiscoveryKey(marker.Id), seenKeys, keyIssues);
                    if (marker.Category == RpgMapMarkerCategory.DangerZone)
                    {
                        RpgWorld500xMapIntegrityValidator.ValidateMakinomKeyHygiene(
                            mapName, RpgWorldOrkExplorationBridge.DangerClearedKey(marker.Id), seenKeys, keyIssues);
                    }
                }

                for (int i = 0; i < map.Quests.Count; i++)
                {
                    RpgLandmarkQuestDefinition quest = map.Quests[i];
                    RpgWorld500xMapIntegrityValidator.ValidateMakinomKeyHygiene(
                        mapName, RpgMapExplorationTracker.QuestProgressKey(quest.QuestId), seenKeys, keyIssues);
                    RpgWorld500xMapIntegrityValidator.ValidateMakinomKeyHygiene(
                        mapName, RpgWorldOrkExplorationBridge.QuestRewardedKey(quest.QuestId), seenKeys, keyIssues);
                }

                for (int i = 0; i < map.PointLights.Count; i++)
                {
                    RpgLight2DAnchor light = map.PointLights[i];
                    Assert.Greater(light.OuterRadiusTiles, 0f, $"Invalid light radius on {mapName}/{light.Id}.");
                    Assert.IsFalse(float.IsNaN(light.OuterRadiusTiles) || float.IsInfinity(light.OuterRadiusTiles));
                }
            }

            Assert.IsEmpty(keyIssues, string.Join("\n", keyIssues));
            Assert.LessOrEqual(totalGridBytes, RpgWorld500xMapIntegrityValidator.MaxTotalGridBytes,
                $"Total grid footprint {totalGridBytes} exceeded budget.");
        }
    }
}
