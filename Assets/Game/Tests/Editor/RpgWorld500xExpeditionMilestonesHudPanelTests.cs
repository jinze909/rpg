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
                    Assert.IsFalse(panel.TryClickClaimAllEligible());

                    // Satisfy four_regions_sworn (+300 Salt-Silver).
                    IReadOnlyList<RpgExpandedMapSpec> specs = RpgExpandedWorldScale.AllSpecs;
                    for (int i = 0; i < specs.Count; i++)
                    {
                        externalStore[RpgWorld500xRegionalReputationService.ReputationKey(specs[i].MapId)] =
                            RpgWorld500xRegionalReputationService.SwornThreshold;
                    }

                    panel.Refresh();
                    Assert.AreEqual(1, panel.ClaimableCount);
                    Assert.IsTrue(panel.TryClickClaimAllEligible());
                    Assert.AreEqual(0, panel.ClaimableCount);
                    Assert.AreEqual(1, panel.ClaimedCount);
                    Assert.AreEqual(300, boot.Driver.Facade.BountyAndSupply.SaltSilverBalance);
                    Assert.IsTrue(panel.StatusText.Contains("300"));

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
