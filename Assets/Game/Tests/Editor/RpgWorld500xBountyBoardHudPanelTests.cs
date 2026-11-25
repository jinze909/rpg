using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xBountyBoardHudPanelTests
    {
        [Test]
        public void BountyBoardHudPanel_SwitchesScopesClaimsCompletedBountyIdempotentlyRespectsLockAndZeroLeak()
        {
            var root = new GameObject("World500xBountyBoardHudPanelTestRoot");
            try
            {
                // Warm up one-time UGUI/font caches before steady-state leak baseline.
                using (var warmupBoot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.Soundwell,
                    overrideTileSlices: new Sprite[] { null }))
                using (var warmupPanel = new RpgWorld500xBountyBoardHudPanel(root.transform, warmupBoot))
                {
                    warmupPanel.Show();
                    warmupPanel.TrySelectScope(RpgWorld500xBountyBoardScope.AllRegions);
                    warmupPanel.TryClickClose();
                }

                int texBefore = Resources.FindObjectsOfTypeAll<Texture2D>().Length;
                int spriteBefore = Resources.FindObjectsOfTypeAll<Sprite>().Length;

                using (var boot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.Soundwell,
                    overrideTileSlices: new Sprite[] { null }))
                using (var panel = new RpgWorld500xBountyBoardHudPanel(root.transform, boot))
                {
                    Assert.IsTrue(panel.Show());
                    Assert.AreEqual(RpgWorld500xBountyBoardScope.ActiveMap, panel.ActiveScope);
                    Assert.AreEqual(3, panel.DisplayedRowCount);
                    Assert.AreEqual(12, panel.TotalBountyCount);
                    Assert.AreEqual(0, panel.ClaimedCount);
                    Assert.AreEqual(0, panel.ClaimableCount);
                    Assert.IsFalse(panel.TryClickClaimEligible());

                    // Switch to AllRegions scope -> 12 regional bounties across all 4 maps.
                    Assert.IsTrue(panel.TrySelectScope(RpgWorld500xBountyBoardScope.AllRegions));
                    Assert.AreEqual(RpgWorld500xBountyBoardScope.AllRegions, panel.ActiveScope);
                    Assert.AreEqual(12, panel.DisplayedRowCount);

                    // Unlock 2 Soundwell shortcuts to satisfy sw_b03 (60 Salt-Silver).
                    IReadOnlyList<RpgWorldShortcutDef> shortcuts =
                        RpgWorld500xContentCatalog.GetShortcuts(RpgWorldMapId.Soundwell);
                    Assert.GreaterOrEqual(shortcuts.Count, 2);
                    for (int i = 0; i < 2; i++)
                    {
                        Vector2 pos = new Vector2(shortcuts[i].TriggerTile.x + 0.5f, shortcuts[i].TriggerTile.y + 0.5f);
                        Assert.IsTrue(boot.Driver.Facade.ActivityDirector.CachesAndShortcuts
                            .TryUnlockNearbyShortcut(pos, 6f, out _));
                    }

                    panel.Refresh();
                    Assert.AreEqual(1, panel.ClaimableCount);

                    // Parent CanvasGroup lock blocks scope switch, claim, and close cleanly.
                    CanvasGroup group = root.AddComponent<CanvasGroup>();
                    group.interactable = false;
                    Assert.IsFalse(panel.TrySelectScope(RpgWorld500xBountyBoardScope.ActiveMap));
                    Assert.IsFalse(panel.TryClickClaimEligible());
                    Assert.IsFalse(panel.TryClickClose());
                    Assert.AreEqual(0, panel.ClaimedCount);
                    group.interactable = true;

                    // Claim eligible bounty -> credits 60 Salt-Silver idempotently.
                    Assert.IsTrue(panel.TryClickClaimEligible());
                    Assert.AreEqual(1, panel.ClaimedCount);
                    Assert.AreEqual(0, panel.ClaimableCount);
                    Assert.GreaterOrEqual(boot.Driver.Facade.BountyAndSupply.SaltSilverBalance, 60);
                    Assert.IsTrue(panel.FeedbackText.Contains("盐银 +60"));
                    Assert.IsFalse(panel.TryClickClaimEligible());

                    Assert.IsTrue(panel.TryClickClose());
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
