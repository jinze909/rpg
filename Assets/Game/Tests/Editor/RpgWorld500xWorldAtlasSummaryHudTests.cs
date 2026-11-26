using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xWorldAtlasSummaryHudTests
    {
        [Test]
        public void WorldAtlasHudPanel_ExposesAtlasSummaryAndShortestRoutePreviewWithZeroLeak()
        {
            var root = new GameObject("World500xAtlasSummaryHudTestRoot");
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

                using (var boot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.Soundwell,
                    overrideTileSlices: new Sprite[] { null }))
                using (var panel = new RpgWorld500xWorldAtlasHudPanel(root.transform, boot))
                {
                    Assert.IsTrue(panel.Show());
                    Assert.AreEqual(4, panel.DisplayedRegionRowCount);
                    Assert.AreEqual(4, panel.LastAtlasSummary.TotalRegions);
                    Assert.AreEqual(0, panel.TotalClaimedBounties);
                    Assert.AreEqual(12, panel.TotalBounties);
                    Assert.AreEqual(0, panel.TotalReputationPoints);
                    StringAssert.Contains("【四境总览】", panel.AtlasSummaryText);

                    // Unlock 2 Soundwell shortcuts and claim the completed bounty -> LastAtlasSummary reflects it live.
                    IReadOnlyList<RpgWorldShortcutDef> shortcuts =
                        RpgWorld500xContentCatalog.GetShortcuts(RpgWorldMapId.Soundwell);
                    Assert.GreaterOrEqual(shortcuts.Count, 2);
                    for (int i = 0; i < 2; i++)
                    {
                        Vector2 pos = new Vector2(shortcuts[i].TriggerTile.x + 0.5f, shortcuts[i].TriggerTile.y + 0.5f);
                        Assert.IsTrue(boot.Driver.Facade.ActivityDirector.CachesAndShortcuts
                            .TryUnlockNearbyShortcut(pos, 6f, out _));
                    }

                    List<RpgWorld500xFieldBountyDef> claimed =
                        boot.Driver.Facade.BountyAndSupply.ClaimCompletedBounties(
                            boot.Driver.Facade.GetCompletionSummary());
                    Assert.GreaterOrEqual(claimed.Count, 1);

                    panel.Refresh();
                    Assert.AreEqual(claimed.Count, panel.TotalClaimedBounties);
                    Assert.AreEqual(panel.TotalBounties - claimed.Count, panel.LastAtlasSummary.RemainingBounties);

                    // Multi-hop preview from Soundwell -> MineCamp (2 hops, 270 min).
                    Assert.IsFalse(panel.TryPreviewShortestRouteTo((RpgWorldMapId)255, out _));
                    Assert.IsTrue(panel.TryPreviewShortestRouteTo(
                        RpgWorldMapId.MineCamp, out RpgWorld500xRegionPathPlan multiHop));
                    Assert.IsFalse(multiHop.IsDirect);
                    Assert.AreEqual(2, multiHop.Hops.Count);
                    Assert.AreEqual(270, multiHop.TotalTravelMinutesInGame);

                    // Travel to RedSaltHarbor -> preview to MineCamp becomes direct (1 hop, 150 min).
                    Assert.IsTrue(panel.TryClickTravelTo(RpgWorldMapId.RedSaltHarbor));
                    Assert.IsTrue(panel.TryPreviewShortestRouteTo(
                        RpgWorldMapId.MineCamp, out RpgWorld500xRegionPathPlan directHop));
                    Assert.IsTrue(directHop.IsDirect);
                    Assert.AreEqual(1, directHop.Hops.Count);
                    Assert.AreEqual(150, directHop.TotalTravelMinutesInGame);

                    Assert.IsTrue(panel.Hide());
                    panel.Dispose();
                    Assert.IsFalse(panel.TryPreviewShortestRouteTo(RpgWorldMapId.MineCamp, out _));
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
