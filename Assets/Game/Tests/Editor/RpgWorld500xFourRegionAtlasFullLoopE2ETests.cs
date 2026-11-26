using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionAtlasFullLoopE2ETests
    {
        [Test]
        public void WorldAtlasHudPanel_PreviewsAll16RegionPairsAndCompletesFourRegionGrandCircuitWithZeroLeak()
        {
            var root = new GameObject("World500xFourRegionAtlasGrandCircuitRoot");
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

                RpgWorldMapId[] allRegions =
                {
                    RpgWorldMapId.Soundwell,
                    RpgWorldMapId.RedSaltHarbor,
                    RpgWorldMapId.MineCamp,
                    RpgWorldMapId.WhitewaterCrossing
                };

                using (var boot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: allRegions[0],
                    overrideTileSlices: new Sprite[] { null }))
                using (var panel = new RpgWorld500xWorldAtlasHudPanel(root.transform, boot))
                {
                    Assert.IsTrue(panel.Show());
                    Assert.AreEqual(4, panel.DisplayedRegionRowCount);
                    Assert.AreEqual(4, panel.LastAtlasSummary.TotalRegions);
                    Assert.AreEqual(12, panel.TotalBounties);
                    StringAssert.Contains("【四境总览】", panel.AtlasSummaryText);

                    for (int originIdx = 0; originIdx < allRegions.Length; originIdx++)
                    {
                        RpgWorldMapId origin = allRegions[originIdx];
                        Assert.AreEqual(origin, boot.Driver.Facade.CurrentMapId);

                        // Preview shortest route to all 4 destinations from current origin.
                        for (int targetIdx = 0; targetIdx < allRegions.Length; targetIdx++)
                        {
                            RpgWorldMapId target = allRegions[targetIdx];
                            Assert.IsTrue(panel.TryPreviewShortestRouteTo(target, out RpgWorld500xRegionPathPlan plan));
                            if (origin == target)
                            {
                                Assert.AreEqual(0, plan.Hops.Count);
                                Assert.AreEqual(0, plan.TotalTravelMinutesInGame);
                            }
                            else
                            {
                                Assert.GreaterOrEqual(plan.Hops.Count, 1);
                                Assert.Greater(plan.TotalTravelMinutesInGame, 0);
                            }
                        }

                        // Travel directly along the first hop of the shortest path to the next region in the circuit.
                        RpgWorldMapId nextStop = allRegions[(originIdx + 1) % allRegions.Length];
                        Assert.IsTrue(panel.TryPreviewShortestRouteTo(nextStop, out RpgWorld500xRegionPathPlan nextPlan));
                        RpgWorldMapId firstHopMap = nextPlan.Hops[0].ToMap;
                        Assert.IsTrue(panel.TryClickTravelTo(firstHopMap));
                        if (firstHopMap != nextStop)
                        {
                            Assert.IsTrue(panel.TryClickTravelTo(nextStop));
                        }
                    }

                    // Completed full 4-region grand circuit back to Soundwell.
                    Assert.AreEqual(RpgWorldMapId.Soundwell, boot.Driver.Facade.CurrentMapId);
                    Assert.IsTrue(panel.Hide());
                    Assert.IsFalse(panel.IsVisible);
                }

                Assert.AreEqual(0, root.transform.childCount);
                Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Texture2D>().Length, texBefore);
                Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Sprite>().Length, spriteBefore);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
