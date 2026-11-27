using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    /// <summary>
    /// Four-region E2E regression tests for RpgWorld500xBountyBoardHudPanel covering
    /// ActiveMap/AllRegions scope switching, claimable Salt-Silver summary readouts,
    /// ancestor CanvasGroup input lockout/recovery, idempotent bounty settlement, and
    /// zero native Texture2D/Sprite leaks across Soundwell, RedSaltHarbor,
    /// WhitewaterCrossing, and MineCamp.
    /// </summary>
    public sealed class RpgWorld500xFourRegionBountyBoardHudE2ETests
    {
        [Test]
        public void AllFourRegions_BountyBoardHud_SwitchesScopesClaimsEligibleUnderLockGuardAndDisposesWithZeroLeak()
        {
            var root = new GameObject("FourRegionBountyBoardHudRoot");
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

                IReadOnlyList<RpgExpandedMapSpec> specs = RpgExpandedWorldScale.AllSpecs;
                Assert.AreEqual(4, specs.Count);

                for (int r = 0; r < specs.Count; r++)
                {
                    RpgExpandedMapSpec spec = specs[r];
                    var regionRoot = new GameObject("RegionBountyBoard_" + spec.MapId);
                    regionRoot.transform.SetParent(root.transform, false);

                    try
                    {
                        using (var boot = new RpgWorld500xSceneBootstrapper(
                            initialMapId: spec.MapId,
                            overrideTileSlices: new Sprite[] { null }))
                        using (var panel = new RpgWorld500xBountyBoardHudPanel(regionRoot.transform, boot))
                        {
                            Assert.IsTrue(panel.Show(), $"BountyBoardHudPanel failed to show on {spec.MapId}.");
                            Assert.AreEqual(RpgWorld500xBountyBoardScope.ActiveMap, panel.ActiveScope);
                            Assert.AreEqual(3, panel.DisplayedRowCount);
                            Assert.AreEqual(12, panel.TotalBountyCount);
                            Assert.AreEqual(0, panel.ClaimedCount);
                            Assert.AreEqual(0, panel.ClaimableCount);
                            Assert.IsFalse(panel.TryClickClaimEligible());
                            StringAssert.Contains(spec.DisplayNameZh, panel.HeaderText);

                            int initialRemainingSilver = panel.RemainingSaltSilver;
                            Assert.Greater(initialRemainingSilver, 0);

                            // Switch to AllRegions scope -> 12 regional bounties across all 4 maps.
                            Assert.IsTrue(panel.TrySelectScope(RpgWorld500xBountyBoardScope.AllRegions));
                            Assert.AreEqual(RpgWorld500xBountyBoardScope.AllRegions, panel.ActiveScope);
                            Assert.AreEqual(12, panel.DisplayedRowCount);

                            // Unlock all shortcuts in this region to satisfy its shortcut bounty.
                            IReadOnlyList<RpgWorldShortcutDef> shortcuts =
                                RpgWorld500xContentCatalog.GetShortcuts(spec.MapId);
                            Assert.GreaterOrEqual(shortcuts.Count, 2,
                                $"Expected shortcuts on {spec.MapId}.");
                            for (int i = 0; i < shortcuts.Count; i++)
                            {
                                var pos = new Vector2(
                                    shortcuts[i].TriggerTile.x + 0.5f,
                                    shortcuts[i].TriggerTile.y + 0.5f);
                                Assert.IsTrue(boot.Driver.Facade.ActivityDirector.CachesAndShortcuts
                                    .TryUnlockNearbyShortcut(pos, 6f, out _));
                            }

                            panel.Refresh();
                            int claimableCount = panel.ClaimableCount;
                            int claimableSilver = panel.ClaimableSaltSilver;
                            Assert.GreaterOrEqual(claimableCount, 1,
                                $"Expected at least 1 claimable bounty on {spec.MapId}.");
                            Assert.Greater(claimableSilver, 0);
                            StringAssert.Contains("+" + claimableSilver + " 盐银", panel.ClaimButtonLabelText);

                            // Ancestor CanvasGroup lock blocks scope switch, claim, and close cleanly.
                            CanvasGroup group = regionRoot.AddComponent<CanvasGroup>();
                            group.interactable = false;
                            Assert.IsFalse(panel.TrySelectScope(RpgWorld500xBountyBoardScope.ActiveMap));
                            Assert.IsFalse(panel.TryClickClaimEligible());
                            Assert.IsFalse(panel.TryClickClose());
                            Assert.AreEqual(0, panel.ClaimedCount);
                            group.interactable = true;

                            // Claim eligible bounties idempotently.
                            Assert.IsTrue(panel.TryClickClaimEligible());
                            Assert.AreEqual(claimableCount, panel.ClaimedCount);
                            Assert.AreEqual(0, panel.ClaimableCount);
                            Assert.AreEqual(0, panel.ClaimableSaltSilver);
                            Assert.AreEqual(initialRemainingSilver - claimableSilver, panel.RemainingSaltSilver);
                            Assert.AreEqual("领取本境已达标悬赏", panel.ClaimButtonLabelText);
                            Assert.GreaterOrEqual(boot.Driver.Facade.BountyAndSupply.SaltSilverBalance, claimableSilver);
                            StringAssert.Contains("盐银 +" + claimableSilver, panel.FeedbackText);
                            Assert.IsFalse(panel.TryClickClaimEligible());

                            Assert.IsTrue(panel.TryClickClose());
                            Assert.IsFalse(panel.IsVisible);
                        }
                    }
                    finally
                    {
                        UnityEngine.Object.DestroyImmediate(regionRoot);
                    }
                }

                Assert.AreEqual(0, root.transform.childCount);
                Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Texture2D>().Length, texBefore,
                    "Texture2D leak detected after four-region BountyBoardHudPanel sweep.");
                Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Sprite>().Length, spriteBefore,
                    "Sprite leak detected after four-region BountyBoardHudPanel sweep.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
