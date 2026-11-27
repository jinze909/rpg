using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionMapStreamingAndIntegrityE2ETests
    {
        [Test]
        public void FourRegion_MapLayoutAndStreamingRenderer_PassIntegritySweepAndReleaseOwnedTexturesAcrossAllFourRegions()
        {
            List<RpgMapValidationIssue> issues = RpgWorld500xMapIntegrityValidator.ValidateAllFourMaps();
            Assert.That(issues, Is.Empty, string.Join("\n", issues));

            var root = new GameObject("World500xFourRegionMapStreamingRoot");
            try
            {
                IReadOnlyList<RpgExpandedMapSpec> specs = RpgExpandedWorldScale.AllSpecs;
                Assert.AreEqual(4, specs.Count);

                // Warm up one-time UGUI/font caches before steady-state Sprite leak baseline.
                RpgWorld500xMapLayout warmupLayout = RpgWorld500xMapLayout.Build(specs[0].MapId);
                using (var warmupTrackerHud = new RpgWorld500xMinimapHud(
                    root.transform, warmupLayout.Spec, warmupLayout.GetTileId, warmupLayout.UsesHarborPalette,
                    new RpgMapExplorationTracker(specs[0].MapId)))
                {
                    warmupTrackerHud.UpdatePlayerAndCompass(new Vector2(warmupLayout.PlayerSpawn.x, warmupLayout.PlayerSpawn.y));
                }

                int spriteBefore = Resources.FindObjectsOfTypeAll<Sprite>().Length;

                for (int r = 0; r < specs.Count; r++)
                {
                    RpgExpandedMapSpec spec = specs[r];
                    var regionRoot = new GameObject("RegionStreaming_" + spec.MapId);
                    regionRoot.transform.SetParent(root.transform, false);

                    RpgLargeMapChunkRenderer renderer = null;
                    RpgWorld500xMinimapHud hud = null;
                    Texture2D ownedMinimapTexture = null;

                    try
                    {
                        RpgWorld500xMapLayout layout = RpgWorld500xMapLayout.Build(spec.MapId);
                        Assert.AreEqual(spec.ExpandedWidth, layout.Width);
                        Assert.AreEqual(spec.ExpandedHeight, layout.Height);
                        Assert.IsTrue(layout.IsWalkable(layout.PlayerSpawn.x, layout.PlayerSpawn.y, gateOpen: false));

                        renderer = layout.CreateStreamingRenderer(
                            regionRoot.transform,
                            streamingRadiusChunks: 1,
                            tileSlices: new Sprite[] { null });
                        var tracker = new RpgMapExplorationTracker(spec.MapId);
                        hud = new RpgWorld500xMinimapHud(
                            regionRoot.transform,
                            layout.Spec,
                            layout.GetTileId,
                            layout.UsesHarborPalette,
                            tracker);
                        ownedMinimapTexture = hud.MinimapTexture;

                        Assert.LessOrEqual(hud.TextureWidth, RpgWorld500xMapIntegrityValidator.MaxMinimapWidth);
                        Assert.LessOrEqual(hud.TextureHeight, RpgWorld500xMapIntegrityValidator.MaxMinimapHeight);

                        Vector2[] focusPoints =
                        {
                            new Vector2(layout.PlayerSpawn.x + 0.5f, layout.PlayerSpawn.y + 0.5f),
                            new Vector2(spec.ExpandedWidth * 0.25f, spec.ExpandedHeight * 0.25f),
                            new Vector2(spec.ExpandedWidth * 0.75f, spec.ExpandedHeight * 0.25f),
                            new Vector2(spec.ExpandedWidth * 0.75f, spec.ExpandedHeight * 0.75f),
                            new Vector2(spec.ExpandedWidth * 0.25f, spec.ExpandedHeight * 0.75f)
                        };

                        for (int i = 0; i < focusPoints.Length; i++)
                        {
                            Assert.IsTrue(renderer.UpdateStreamingFocus(focusPoints[i]),
                                $"Streaming focus failed on {spec.MapId} at {focusPoints[i]}.");
                            Assert.LessOrEqual(renderer.ChunkCount, 9,
                                $"Streaming chunk window exceeded 3x3 budget on {spec.MapId}.");
                            Assert.IsTrue(hud.UpdatePlayerAndCompass(focusPoints[i]));
                        }

                        hud.Dispose();
                        hud = null;
                        renderer.Dispose();
                        renderer = null;

                        Assert.IsTrue(ownedMinimapTexture == null,
                            $"Owned minimap Texture2D must be destroyed on Dispose for {spec.MapId}.");
                    }
                    finally
                    {
                        hud?.Dispose();
                        renderer?.Dispose();
                        UnityEngine.Object.DestroyImmediate(regionRoot);
                    }
                }

                Assert.AreEqual(0, root.transform.childCount);
                Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Sprite>().Length, spriteBefore);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void FourRegion_MapIntegrityValidator_RejectsEmptyDuplicateWrongPrefixAndNarrativeCollisionKeys()
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var issues = new List<RpgMapValidationIssue>();

            RpgWorld500xMapIntegrityValidator.ValidateMakinomKeyHygiene(
                "Soundwell", "rpg.return.world.e2e.sample01", seen, issues);
            Assert.IsEmpty(issues);

            // Duplicate key
            RpgWorld500xMapIntegrityValidator.ValidateMakinomKeyHygiene(
                "RedSaltHarbor", "rpg.return.world.e2e.sample01", seen, issues);
            Assert.AreEqual(1, issues.Count);
            Assert.AreEqual("KEY-DUP", issues[0].RuleId);

            // Empty key
            issues.Clear();
            RpgWorld500xMapIntegrityValidator.ValidateMakinomKeyHygiene(
                "WhitewaterCrossing", "   ", seen, issues);
            Assert.AreEqual(1, issues.Count);
            Assert.AreEqual("KEY-EMPTY", issues[0].RuleId);

            // Wrong prefix & reserved narrative prefix collisions
            string[] badKeys =
            {
                "rpg.other.world.key",
                "rpg.return.m00.rollcall",
                "rpg.return.m16.epilogue",
                "rpg.return.st01.theater",
                "rpg.return.rc05.squad"
            };
            for (int i = 0; i < badKeys.Length; i++)
            {
                issues.Clear();
                RpgWorld500xMapIntegrityValidator.ValidateMakinomKeyHygiene(
                    "MineCamp", badKeys[i], seen, issues);
                Assert.GreaterOrEqual(issues.Count, 1, $"Expected key hygiene violation for '{badKeys[i]}'.");
            }
        }
    }
}
