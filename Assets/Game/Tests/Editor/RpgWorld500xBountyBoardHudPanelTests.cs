using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;
using UnityEngine.UI;

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
                    // Unlock 2 Soundwell shortcuts first while panel is still hidden.
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
                    Assert.AreEqual(60, panel.ClaimableSaltSilver);
                    Assert.AreEqual(150, panel.RemainingSaltSilver);
                    StringAssert.Contains("+60 盐银", panel.ClaimButtonLabelText);
                    StringAssert.Contains("可领 1 项（+60 盐银）", panel.ActiveMapSummaryZh);

                    // Hidden panel must reject Try* calls AND direct stale button callbacks without claiming rewards.
                    Assert.IsFalse(panel.TrySelectScope(RpgWorld500xBountyBoardScope.AllRegions));
                    Assert.IsFalse(panel.TryClickClaimEligible());
                    Assert.IsFalse(panel.TryClickClose());
                    Button claimBtn = root.transform
                        .Find("World500xBountyBoardHudPanel/BountyBoardModalBox/ClaimEligibleBountiesButton")
                        .GetComponent<Button>();
                    claimBtn.onClick.Invoke();
                    Assert.AreEqual(0, panel.ClaimedCount);
                    Assert.AreEqual(0, boot.Driver.Facade.BountyAndSupply.SaltSilverBalance);

                    Assert.IsTrue(panel.Show());
                    Assert.AreEqual(RpgWorld500xBountyBoardScope.ActiveMap, panel.ActiveScope);
                    Assert.AreEqual(3, panel.DisplayedRowCount);
                    Assert.AreEqual(12, panel.TotalBountyCount);
                    Assert.AreEqual(0, panel.ClaimedCount);
                    Assert.AreEqual(1, panel.ClaimableCount);

                    // Switch to AllRegions scope -> 12 regional bounties across all 4 maps.
                    Assert.IsTrue(panel.TrySelectScope(RpgWorld500xBountyBoardScope.AllRegions));
                    Assert.AreEqual(RpgWorld500xBountyBoardScope.AllRegions, panel.ActiveScope);
                    Assert.AreEqual(12, panel.DisplayedRowCount);

                    // Parent CanvasGroup lock blocks scope switch, claim, close, AND direct stale claim clicks cleanly.
                    CanvasGroup group = root.AddComponent<CanvasGroup>();
                    group.interactable = false;
                    Assert.IsFalse(panel.TrySelectScope(RpgWorld500xBountyBoardScope.ActiveMap));
                    Assert.IsFalse(panel.TryClickClaimEligible());
                    Assert.IsFalse(panel.TryClickClose());
                    claimBtn.onClick.Invoke();
                    Assert.AreEqual(0, panel.ClaimedCount);
                    Assert.AreEqual(0, boot.Driver.Facade.BountyAndSupply.SaltSilverBalance);
                    group.interactable = true;

                    // Claim eligible bounty -> credits 60 Salt-Silver idempotently.
                    Assert.IsTrue(panel.TryClickClaimEligible());
                    Assert.AreEqual(1, panel.ClaimedCount);
                    Assert.AreEqual(0, panel.ClaimableCount);
                    Assert.AreEqual(0, panel.ClaimableSaltSilver);
                    Assert.AreEqual(90, panel.RemainingSaltSilver);
                    Assert.AreEqual("领取本境已达标悬赏", panel.ClaimButtonLabelText);
                    StringAssert.Contains("剩余总赏 90", panel.HeaderText);
                    Assert.GreaterOrEqual(boot.Driver.Facade.BountyAndSupply.SaltSilverBalance, 60);
                    Assert.IsTrue(panel.FeedbackText.Contains("盐银 +60"));
                    Assert.IsFalse(panel.TryClickClaimEligible());

                    Assert.IsTrue(panel.TryClickClose());
                    Assert.IsFalse(panel.IsVisible);
                    Assert.IsFalse(panel.TryClickClose());
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