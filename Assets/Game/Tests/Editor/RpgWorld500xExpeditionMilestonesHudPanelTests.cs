using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xExpeditionMilestonesHudPanelTests
    {
        [Test]
        public void ExpeditionMilestonesHudPanel_DisabledCanvasRejectsClaimAndFilterUntilRestored()
        {
            var root = new GameObject("ExpeditionDisabledCanvasRoot");
            var store = new Dictionary<string, int>(StringComparer.Ordinal);
            const string claimKey = "rpg.return.world.milestone.four_regions_sworn";
            try
            {
                foreach (RpgExpandedMapSpec spec in RpgExpandedWorldScale.AllSpecs)
                {
                    store[RpgWorld500xRegionalReputationService.ReputationKey(spec.MapId)] =
                        RpgWorld500xRegionalReputationService.SwornThreshold;
                }

                int writes = 0;
                int Read(string key) => store.TryGetValue(key, out int value) ? value : 0;
                void Write(string key, int value)
                {
                    writes++;
                    store[key] = value;
                }

                using (var boot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.Soundwell,
                    overrideTileSlices: new Sprite[] { null }))
                using (var panel = new RpgWorld500xExpeditionMilestonesHudPanel(root.transform, boot, Read, Write))
                {
                    Assert.IsTrue(panel.Show());
                    var supply = boot.Driver.Facade.BountyAndSupply;
                    int initialSilver = supply.SaltSilverBalance;
                    var canvas = root.transform.Find("World500xExpeditionMilestonesHudPanel").GetComponent<Canvas>();
                    var button = canvas.transform.Find("MilestonesModalBox/ClaimAllButton")
                        .GetComponent<UnityEngine.UI.Button>();
                    var action = button.onClick;
                    Assert.AreEqual(1, panel.ClaimableCount);
                    Assert.AreEqual(300, panel.ClaimableSaltSilver);
                    Assert.AreEqual(8, panel.DisplayedMilestoneRowCount);
                    string status = panel.StatusText;
                    string summary = panel.SummaryText;

                    canvas.enabled = false;
                    Assert.IsTrue(button.IsInteractable());
                    action.Invoke();
                    Assert.IsFalse(panel.TryClickClaimAllEligible());
                    Assert.IsFalse(panel.TrySetUnclaimedOnly(true));
                    Assert.IsFalse(panel.UnclaimedOnly);
                    Assert.AreEqual(8, panel.DisplayedMilestoneRowCount);
                    Assert.AreEqual(1, panel.ClaimableCount);
                    Assert.AreEqual(0, panel.ClaimedCount);
                    Assert.AreEqual(0, Read(claimKey));
                    Assert.AreEqual(0, writes);
                    Assert.AreEqual(initialSilver, supply.SaltSilverBalance);
                    Assert.AreEqual(status, panel.StatusText);
                    Assert.AreEqual(summary, panel.SummaryText);

                    canvas.enabled = true;
                    Assert.IsTrue(panel.TrySetUnclaimedOnly(true));
                    action.Invoke();
                    Assert.AreEqual(1, panel.ClaimedCount);
                    Assert.AreEqual(0, panel.ClaimableCount);
                    Assert.AreEqual(7, panel.DisplayedMilestoneRowCount);
                    Assert.AreEqual(1, Read(claimKey));
                    Assert.AreEqual(initialSilver + 300, supply.SaltSilverBalance);
                    int writesAfterClaim = writes;
                    action.Invoke();
                    Assert.IsFalse(panel.TryClickClaimAllEligible());
                    Assert.AreEqual(writesAfterClaim, writes);
                    Assert.AreEqual(initialSilver + 300, supply.SaltSilverBalance);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ExpeditionMilestonesHudPanel_ClaimFlagsAndWalletFollowRestoredSave()
        {
            var root = new GameObject("ExpeditionClaimSaveRoot");
            var ctrl = root.AddComponent<RpgWorld500xPlayModeController>();
            const string claimKey = "rpg.return.world.milestone.four_regions_sworn";
            try
            {
                ctrl.Initialize(
                    overrideMap: RpgWorldMapId.Soundwell,
                    saveStorage: new RpgWorld500xInMemorySaveSlotStorage(),
                    overrideTileSlices: new Sprite[] { null });
                var boot = ctrl.Bootstrapper;
                foreach (RpgExpandedMapSpec spec in RpgExpandedWorldScale.AllSpecs)
                {
                    ctrl.CampHaven.Reputation.AddPoints(spec.MapId, RpgWorld500xRegionalReputationService.SwornThreshold);
                }

                int initialSilver = boot.Driver.Facade.BountyAndSupply.SaltSilverBalance;
                string before = boot.ExportSaveJson();
                Assert.IsTrue(ctrl.MenuRouter.OpenModal(RpgWorld500xModalKind.ExpeditionMilestones));
                var panel = ctrl.MenuRouter.MilestonesPanel;
                Assert.AreEqual(1, panel.ClaimableCount);
                Assert.AreEqual(300, panel.ClaimableSaltSilver);
                StringAssert.Contains("【远征里程碑】", panel.MilestoneSummaryLineZh);
                Assert.IsTrue(panel.TryClickClaimAllEligible());
                Assert.AreEqual(1, boot.Store[claimKey]);
                Assert.AreEqual(initialSilver + 300, boot.Driver.Facade.BountyAndSupply.SaltSilverBalance);
                string after = boot.ExportSaveJson();

                Assert.IsTrue(boot.TryImportSaveJson(before, out string beforeError), beforeError);
                panel.Refresh();
                Assert.AreEqual(0, panel.ClaimedCount);
                Assert.AreEqual(1, panel.ClaimableCount);
                Assert.AreEqual(initialSilver, boot.Driver.Facade.BountyAndSupply.SaltSilverBalance);
                Assert.IsTrue(panel.TryClickClaimAllEligible());
                Assert.AreEqual(initialSilver + 300, boot.Driver.Facade.BountyAndSupply.SaltSilverBalance);

                // A new controller/panel reading the claimed save must not pay it again.
                ctrl.Shutdown();
                ctrl.Initialize(
                    overrideMap: RpgWorldMapId.Soundwell,
                    saveStorage: new RpgWorld500xInMemorySaveSlotStorage(),
                    overrideTileSlices: new Sprite[] { null });
                boot = ctrl.Bootstrapper;
                Assert.IsTrue(boot.TryImportSaveJson(after, out string afterError), afterError);
                Assert.IsTrue(ctrl.MenuRouter.OpenModal(RpgWorld500xModalKind.ExpeditionMilestones));
                panel = ctrl.MenuRouter.MilestonesPanel;
                Assert.AreEqual(1, panel.ClaimedCount);
                Assert.AreEqual(0, panel.ClaimableCount);
                Assert.AreEqual(0, panel.ClaimableSaltSilver);
                Assert.IsFalse(panel.TryClickClaimAllEligible());
                Assert.AreEqual(1, boot.Store[claimKey]);
                Assert.AreEqual(initialSilver + 300, boot.Driver.Facade.BountyAndSupply.SaltSilverBalance);
            }
            finally
            {
                ctrl.Shutdown();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ExpeditionMilestonesHudPanel_RejectsHiddenDisabledAndDisposedClaimActions()
        {
            var root = new GameObject("ExpeditionClaimLifetimeRoot");
            var externalStore = new Dictionary<string, int>(StringComparer.Ordinal);
            try
            {
                IReadOnlyList<RpgExpandedMapSpec> specs = RpgExpandedWorldScale.AllSpecs;
                for (int i = 0; i < specs.Count; i++)
                {
                    externalStore[RpgWorld500xRegionalReputationService.ReputationKey(specs[i].MapId)] =
                        RpgWorld500xRegionalReputationService.SwornThreshold;
                }

                int writes = 0;
                using (var boot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.Soundwell,
                    overrideTileSlices: new Sprite[] { null }))
                {
                    int initialSilver = boot.Driver.Facade.BountyAndSupply.SaltSilverBalance;
                    int Read(string key) => externalStore.TryGetValue(key, out int value) ? value : 0;
                    void Write(string key, int value)
                    {
                        writes++;
                        externalStore[key] = value;
                    }

                    UnityEngine.UI.Button ClaimButton() => root.transform
                        .Find("World500xExpeditionMilestonesHudPanel/MilestonesModalBox/ClaimAllButton")
                        .GetComponent<UnityEngine.UI.Button>();

                    void AssertUnclaimed()
                    {
                        Assert.AreEqual(0, writes);
                        Assert.AreEqual(initialSilver, boot.Driver.Facade.BountyAndSupply.SaltSilverBalance);
                    }

                    using (var panel = new RpgWorld500xExpeditionMilestonesHudPanel(root.transform, boot, Read, Write))
                    {
                        Assert.AreEqual(1, panel.ClaimableCount);
                        Assert.IsFalse(panel.TrySetUnclaimedOnly(true));
                        var button = ClaimButton();
                        var action = button.onClick;
                        action.Invoke();
                        Assert.IsFalse(panel.TryClickClaimAllEligible());
                        AssertUnclaimed();

                        Assert.IsTrue(panel.Show());
                        button.interactable = false;
                        action.Invoke();
                        Assert.IsFalse(panel.TryClickClaimAllEligible());
                        AssertUnclaimed();

                        panel.Refresh();
                        Assert.IsTrue(button.interactable);
                        root.SetActive(false);
                        action.Invoke();
                        Assert.IsFalse(panel.TryClickClaimAllEligible());
                        Assert.IsFalse(panel.TrySetUnclaimedOnly(true));
                        AssertUnclaimed();
                        root.SetActive(true);

                        Assert.IsTrue(panel.Hide());
                        action.Invoke();
                        Assert.IsFalse(panel.TryClickClaimAllEligible());
                        AssertUnclaimed();

                        Assert.IsTrue(panel.Show());
                        panel.Dispose();
                        action.Invoke();
                        Assert.IsFalse(panel.TryClickClaimAllEligible());
                        Assert.IsFalse(panel.TrySetUnclaimedOnly(true));
                        AssertUnclaimed();
                    }

                    using (var reopened = new RpgWorld500xExpeditionMilestonesHudPanel(root.transform, boot, Read, Write))
                    {
                        Assert.IsTrue(reopened.Show());
                        Assert.AreEqual(1, reopened.ClaimableCount);
                        var action = ClaimButton().onClick;
                        action.Invoke();
                        Assert.AreEqual(1, reopened.ClaimedCount);
                        Assert.AreEqual(0, reopened.ClaimableCount);
                        Assert.AreEqual(initialSilver + 300, boot.Driver.Facade.BountyAndSupply.SaltSilverBalance);
                        Assert.Greater(writes, 0);
                        int writesAfterClaim = writes;
                        action.Invoke();
                        Assert.IsFalse(reopened.TryClickClaimAllEligible());
                        Assert.AreEqual(writesAfterClaim, writes);
                        Assert.AreEqual(initialSilver + 300, boot.Driver.Facade.BountyAndSupply.SaltSilverBalance);
                    }
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ExpeditionMilestonesHudPanel_ClaimsEligibleRewardsCreditsSilverAndDisposesWithZeroLeak()
        {
            var root = new GameObject("World500xExpeditionMilestonesHudPanelTestRoot");
            try
            {
                // Warm up one-time UGUI/font caches before steady-state leak baseline.
                using (var warmupBoot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.Soundwell,
                    overrideTileSlices: new Sprite[] { null }))
                using (var warmupPanel = new RpgWorld500xExpeditionMilestonesHudPanel(root.transform, warmupBoot))
                {
                    warmupPanel.Show();
                    warmupPanel.Hide();
                }

                int texBefore = Resources.FindObjectsOfTypeAll<Texture2D>().Length;
                int spriteBefore = Resources.FindObjectsOfTypeAll<Sprite>().Length;

                var externalStore = new Dictionary<string, int>(StringComparer.Ordinal);

                using (var boot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.Soundwell,
                    overrideTileSlices: new Sprite[] { null }))
                using (var panel = new RpgWorld500xExpeditionMilestonesHudPanel(
                    root.transform,
                    boot,
                    getInt: k => externalStore.TryGetValue(k, out int v) ? v : 0,
                    setInt: (k, v) => externalStore[k] = v))
                {
                    Assert.IsTrue(panel.Show());
                    Assert.AreEqual(8, panel.DisplayedMilestoneRowCount);
                    Assert.AreEqual(0, panel.ClaimableCount);
                    Assert.AreEqual(0, panel.ClaimableSaltSilver);
                    Assert.IsFalse(panel.UnclaimedOnly);
                    Assert.IsFalse(panel.TryClickClaimAllEligible());
                    StringAssert.Contains("【远征里程碑】", panel.SummaryText);

                    // Satisfy four_regions_sworn (+300 Salt-Silver).
                    IReadOnlyList<RpgExpandedMapSpec> specs = RpgExpandedWorldScale.AllSpecs;
                    for (int i = 0; i < specs.Count; i++)
                    {
                        externalStore[RpgWorld500xRegionalReputationService.ReputationKey(specs[i].MapId)] =
                            RpgWorld500xRegionalReputationService.SwornThreshold;
                    }

                    panel.Refresh();
                    Assert.AreEqual(1, panel.ClaimableCount);
                    Assert.AreEqual(300, panel.ClaimableSaltSilver);
                    Assert.IsTrue(panel.TryClickClaimAllEligible());
                    Assert.AreEqual(0, panel.ClaimableCount);
                    Assert.AreEqual(0, panel.ClaimableSaltSilver);
                    Assert.AreEqual(1, panel.ClaimedCount);
                    Assert.AreEqual(300, boot.Driver.Facade.BountyAndSupply.SaltSilverBalance);
                    Assert.IsTrue(panel.StatusText.Contains("300"));

                    // UnclaimedOnly hides the claimed milestone (8 -> 7) and respects ancestor CanvasGroup lock.
                    Assert.IsTrue(panel.TrySetUnclaimedOnly(true));
                    Assert.IsTrue(panel.UnclaimedOnly);
                    Assert.AreEqual(7, panel.DisplayedMilestoneRowCount);

                    CanvasGroup group = root.AddComponent<CanvasGroup>();
                    group.interactable = false;
                    Assert.IsFalse(panel.TrySetUnclaimedOnly(false));
                    Assert.IsTrue(panel.UnclaimedOnly);
                    group.interactable = true;

                    Assert.IsTrue(panel.TrySetUnclaimedOnly(false));
                    Assert.AreEqual(8, panel.DisplayedMilestoneRowCount);

                    // Duplicate claim is disabled/idempotent.
                    Assert.IsFalse(panel.TryClickClaimAllEligible());

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