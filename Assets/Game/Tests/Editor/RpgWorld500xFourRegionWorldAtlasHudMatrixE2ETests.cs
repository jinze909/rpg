using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    /// <summary>
    /// Four-region E2E regression tests for RpgWorld500xWorldAtlasHudPanel covering
    /// 4x4 shortest-route preview parity, direct neighbor travel execution, invalid
    /// destination rejection, post-dispose lockout, and zero native Texture2D/Sprite
    /// leaks across Soundwell, RedSaltHarbor, WhitewaterCrossing, and MineCamp.
    /// </summary>
    public sealed class RpgWorld500xFourRegionWorldAtlasHudMatrixE2ETests
    {
        [Test]
        public void AllFourRegions_WorldAtlasHudPanel_PreviewsAllRoutesExecutesDirectTravelAndDisposesWithZeroLeak()
        {
            var root = new GameObject("FourRegionWorldAtlasHudMatrixRoot");
            try
            {
                // Warm up one-time UGUI/font caches before steady-state leak baseline.
                using (var warmupBoot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.Soundwell,
                    overrideTileSlices: new Sprite[] { null }))
                using (var warmupPanel = new RpgWorld500xWorldAtlasHudPanel(root.transform, warmupBoot))
                {
                    warmupPanel.Show();
                    warmupPanel.Hide();
                }

                int texBefore = Resources.FindObjectsOfTypeAll<Texture2D>().Length;
                int spriteBefore = Resources.FindObjectsOfTypeAll<Sprite>().Length;

                IReadOnlyList<RpgExpandedMapSpec> specs = RpgExpandedWorldScale.AllSpecs;
                Assert.AreEqual(4, specs.Count);

                for (int r = 0; r < specs.Count; r++)
                {
                    RpgWorldMapId origin = specs[r].MapId;
                    using (var boot = new RpgWorld500xSceneBootstrapper(
                        initialMapId: origin,
                        overrideTileSlices: new Sprite[] { null }))
                    using (var panel = new RpgWorld500xWorldAtlasHudPanel(root.transform, boot))
                    {
                        Assert.IsTrue(panel.Show(), $"WorldAtlasHudPanel failed to show on {origin}.");
                        Assert.AreEqual(4, panel.DisplayedRegionRowCount);
                        Assert.AreEqual(4, panel.LastAtlasSummary.TotalRegions);
                        Assert.AreEqual(12, panel.TotalBounties);
                        StringAssert.Contains("【四境总览】", panel.AtlasSummaryText);
                        StringAssert.Contains("四境世界图册", panel.HeaderText);

                        // Invalid destination is rejected cleanly.
                        Assert.IsFalse(panel.TryPreviewShortestRouteTo((RpgWorldMapId)255, out _));

                        // Shortest-route preview matches portal service across all 3 other regions.
                        RpgWorldMapId directNeighbor = origin;
                        bool foundDirect = false;

                        for (int t = 0; t < specs.Count; t++)
                        {
                            RpgWorldMapId target = specs[t].MapId;
                            if (target == origin)
                            {
                                continue;
                            }

                            Assert.IsTrue(panel.TryPreviewShortestRouteTo(target, out RpgWorld500xRegionPathPlan hudPlan),
                                $"HUD shortest-route preview failed for {origin}->{target}.");
                            Assert.IsTrue(
                                RpgWorld500xWorldAtlasPortalService.TryFindShortestRegionPath(
                                    origin, target, out RpgWorld500xRegionPathPlan servicePlan));

                            Assert.AreEqual(servicePlan.IsDirect, hudPlan.IsDirect);
                            Assert.AreEqual(servicePlan.Hops.Count, hudPlan.Hops.Count);
                            Assert.AreEqual(servicePlan.TotalTravelMinutesInGame, hudPlan.TotalTravelMinutesInGame);
                            Assert.Greater(hudPlan.TotalTravelMinutesInGame, 0);

                            if (!foundDirect && hudPlan.IsDirect)
                            {
                                directNeighbor = target;
                                foundDirect = true;
                            }
                        }

                        Assert.IsTrue(foundDirect, $"Expected at least one direct neighbor from {origin}.");

                        // Clicking direct neighbor travel switches active region cleanly.
                        Assert.IsTrue(panel.TryClickTravelTo(directNeighbor),
                            $"Direct travel {origin}->{directNeighbor} failed.");
                        Assert.AreEqual(directNeighbor, boot.Driver.Facade.CurrentMapId);

                        Assert.IsTrue(panel.Hide());
                        Assert.IsFalse(panel.IsVisible);
                        panel.Dispose();
                        Assert.IsFalse(panel.TryPreviewShortestRouteTo(origin, out _));
                    }

                    Assert.AreEqual(0, root.transform.childCount);
                }

                Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Texture2D>().Length, texBefore,
                    "Texture2D leak detected after four-region WorldAtlasHudPanel matrix sweep.");
                Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Sprite>().Length, spriteBefore,
                    "Sprite leak detected after four-region WorldAtlasHudPanel matrix sweep.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
