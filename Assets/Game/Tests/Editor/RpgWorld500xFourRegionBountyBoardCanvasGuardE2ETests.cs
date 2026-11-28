using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionBountyBoardCanvasGuardE2ETests
    {
        [Test]
        public void AllFourRegions_BountyBoardRejectsDisabledCanvasLockedAndHiddenInputUntilRestored()
        {
            IReadOnlyList<RpgExpandedMapSpec> specs = RpgExpandedWorldScale.AllSpecs;
            Assert.AreEqual(4, specs.Count);

            foreach (RpgExpandedMapSpec spec in specs)
            {
                var root = new GameObject("BountyBoardCanvasGuard_" + spec.MapId);
                try
                {
                    using (var boot = new RpgWorld500xSceneBootstrapper(
                        initialMapId: spec.MapId,
                        overrideTileSlices: new Sprite[] { null }))
                    using (var panel = new RpgWorld500xBountyBoardHudPanel(root.transform, boot))
                    {
                        MakeRegionalBountyEligible(boot, spec.MapId);
                        Assert.IsTrue(panel.Show(), spec.MapId.ToString());

                        var supply = boot.Driver.Facade.BountyAndSupply;
                        int beforeSilver = supply.SaltSilverBalance;
                        int eligible = panel.ClaimableCount;
                        int reward = panel.ClaimableSaltSilver;
                        Assert.Greater(eligible, 0, spec.MapId.ToString());
                        Assert.Greater(reward, 0, spec.MapId.ToString());
                        string feedback = panel.FeedbackText;
                        string claimLabel = panel.ClaimButtonLabelText;

                        var canvas = root.transform.Find("World500xBountyBoardHudPanel").GetComponent<Canvas>();
                        Assert.IsNotNull(canvas, spec.MapId.ToString());
                        var modal = canvas.transform.Find("BountyBoardModalBox");
                        Assert.IsNotNull(modal, spec.MapId.ToString());

                        var claimBtn = modal.Find("ClaimEligibleBountiesButton").GetComponent<Button>();
                        var allBtn = modal.Find("Tab_AllRegions").GetComponent<Button>();
                        var closeBtn = modal.Find("CloseBountyBoardButton").GetComponent<Button>();
                        Assert.IsNotNull(claimBtn, spec.MapId.ToString());
                        Assert.IsNotNull(allBtn, spec.MapId.ToString());
                        Assert.IsNotNull(closeBtn, spec.MapId.ToString());

                        // 1. Disabled owning Canvas must reject both raw button callbacks and public APIs,
                        //    even after an explicit programmatic Refresh().
                        canvas.enabled = false;
                        Assert.IsTrue(claimBtn.IsInteractable(), spec.MapId.ToString());
                        AssertBlocked(panel, supply, claimBtn, allBtn, closeBtn,
                            true, eligible, beforeSilver, feedback, claimLabel);
                        panel.Refresh();
                        AssertBlocked(panel, supply, claimBtn, allBtn, closeBtn,
                            true, eligible, beforeSilver, feedback, claimLabel);
                        canvas.enabled = true;

                        // 2. Ancestor CanvasGroup lock must reject both raw button callbacks and public APIs.
                        var group = root.AddComponent<CanvasGroup>();
                        group.interactable = false;
                        AssertBlocked(panel, supply, claimBtn, allBtn, closeBtn,
                            true, eligible, beforeSilver, feedback, claimLabel);
                        group.interactable = true;

                        // 3. Hidden panel must reject both raw button callbacks and public APIs.
                        Assert.IsTrue(panel.Hide(), spec.MapId.ToString());
                        AssertBlocked(panel, supply, claimBtn, allBtn, closeBtn,
                            false, eligible, beforeSilver, feedback, claimLabel);
                        Assert.IsTrue(panel.Show(), spec.MapId.ToString());

                        // 4. Restored panel accepts scope switch, idempotent claim, and close.
                        Assert.IsTrue(panel.TrySelectScope(RpgWorld500xBountyBoardScope.AllRegions), spec.MapId.ToString());
                        Assert.AreEqual(12, panel.DisplayedRowCount, spec.MapId.ToString());
                        claimBtn.onClick.Invoke();
                        Assert.AreEqual(eligible, panel.ClaimedCount, spec.MapId.ToString());
                        Assert.AreEqual(0, panel.ClaimableCount, spec.MapId.ToString());
                        Assert.AreEqual(beforeSilver + reward, supply.SaltSilverBalance, spec.MapId.ToString());
                        Assert.IsFalse(panel.TryClickClaimEligible(), spec.MapId.ToString());
                        Assert.IsTrue(panel.TryClickClose(), spec.MapId.ToString());
                        Assert.IsFalse(panel.IsVisible, spec.MapId.ToString());
                    }

                    Assert.AreEqual(0, root.transform.childCount, spec.MapId.ToString());
                }
                finally
                {
                    Object.DestroyImmediate(root);
                }
            }
        }

        private static void MakeRegionalBountyEligible(RpgWorld500xSceneBootstrapper boot, RpgWorldMapId mapId)
        {
            var facade = boot.Driver.Facade;
            if (mapId == RpgWorldMapId.RedSaltHarbor)
            {
                var host = facade.Session.Host;
                var markers = host.Layout.Markers;
                Assert.GreaterOrEqual(markers.Count, 8);
                for (int i = 0; i < 8; i++)
                {
                    if (!host.Tracker.IsMarkerDiscovered(markers[i].Id))
                        Assert.IsTrue(host.Tracker.TryDiscoverMarker(markers[i].Id));
                }
            }
            else
            {
                IReadOnlyList<RpgWorldShortcutDef> shortcuts = RpgWorld500xContentCatalog.GetShortcuts(mapId);
                Assert.GreaterOrEqual(shortcuts.Count, 2, mapId.ToString());
                for (int i = 0; i < 2; i++)
                {
                    var pos = new Vector2(
                        shortcuts[i].TriggerTile.x + 0.5f,
                        shortcuts[i].TriggerTile.y + 0.5f);
                    Assert.IsTrue(facade.ActivityDirector.CachesAndShortcuts
                        .TryUnlockNearbyShortcut(pos, 6f, out _));
                }
            }
        }

        private static void AssertBlocked(
            RpgWorld500xBountyBoardHudPanel panel,
            RpgWorld500xFieldBountyAndSupplyService supply,
            Button claimBtn,
            Button allBtn,
            Button closeBtn,
            bool expectedVisible,
            int expectedEligible,
            int expectedSilver,
            string expectedFeedback,
            string expectedClaimLabel)
        {
            allBtn.onClick.Invoke();
            claimBtn.onClick.Invoke();
            closeBtn.onClick.Invoke();
            Assert.IsFalse(panel.TrySelectScope(RpgWorld500xBountyBoardScope.AllRegions));
            Assert.IsFalse(panel.TryClickClaimEligible());
            Assert.IsFalse(panel.TryClickClose());

            Assert.AreEqual(expectedVisible, panel.IsVisible);
            Assert.AreEqual(RpgWorld500xBountyBoardScope.ActiveMap, panel.ActiveScope);
            Assert.AreEqual(3, panel.DisplayedRowCount);
            Assert.AreEqual(0, panel.ClaimedCount);
            Assert.AreEqual(expectedEligible, panel.ClaimableCount);
            Assert.AreEqual(expectedSilver, supply.SaltSilverBalance);
            Assert.AreEqual(expectedFeedback, panel.FeedbackText);
            Assert.AreEqual(expectedClaimLabel, panel.ClaimButtonLabelText);
        }
    }
}
