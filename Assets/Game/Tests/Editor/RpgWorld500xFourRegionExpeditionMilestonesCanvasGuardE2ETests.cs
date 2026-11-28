using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionExpeditionMilestonesCanvasGuardE2ETests
    {
        [Test]
        public void AllFourRegions_ExpeditionMilestonesRejectsDisabledCanvasLockedAndHiddenInputUntilRestored()
        {
            IReadOnlyList<RpgExpandedMapSpec> specs = RpgExpandedWorldScale.AllSpecs;
            Assert.AreEqual(4, specs.Count);
            const string claimKey = "rpg.return.world.milestone.four_regions_sworn";

            foreach (RpgExpandedMapSpec spec in specs)
            {
                var root = new GameObject("ExpeditionMilestonesCanvasGuard_" + spec.MapId);
                var store = new Dictionary<string, int>(StringComparer.Ordinal);
                try
                {
                    foreach (RpgExpandedMapSpec repSpec in specs)
                    {
                        store[RpgWorld500xRegionalReputationService.ReputationKey(repSpec.MapId)] =
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
                        initialMapId: spec.MapId,
                        overrideTileSlices: new Sprite[] { null }))
                    using (var panel = new RpgWorld500xExpeditionMilestonesHudPanel(root.transform, boot, Read, Write))
                    {
                        Assert.IsTrue(panel.Show(), spec.MapId.ToString());
                        var supply = boot.Driver.Facade.BountyAndSupply;
                        int initialSilver = supply.SaltSilverBalance;

                        var canvas = root.transform
                            .Find("World500xExpeditionMilestonesHudPanel")
                            .GetComponent<Canvas>();
                        Assert.IsNotNull(canvas, spec.MapId.ToString());
                        var claimBtn = canvas.transform
                            .Find("MilestonesModalBox/ClaimAllButton")
                            .GetComponent<Button>();
                        Assert.IsNotNull(claimBtn, spec.MapId.ToString());

                        Assert.AreEqual(1, panel.ClaimableCount, spec.MapId.ToString());
                        Assert.AreEqual(300, panel.ClaimableSaltSilver, spec.MapId.ToString());
                        Assert.AreEqual(8, panel.DisplayedMilestoneRowCount, spec.MapId.ToString());
                        string status = panel.StatusText;
                        string summary = panel.SummaryText;

                        // 1. Disabled owning Canvas rejects raw button callback and public APIs.
                        canvas.enabled = false;
                        Assert.IsTrue(claimBtn.IsInteractable(), spec.MapId.ToString());
                        AssertBlocked(panel, supply, claimBtn, Read, claimKey, () => writes,
                            true, initialSilver, status, summary);
                        canvas.enabled = true;

                        // 2. Ancestor CanvasGroup lock rejects raw button callback and public APIs.
                        var group = root.AddComponent<CanvasGroup>();
                        group.interactable = false;
                        AssertBlocked(panel, supply, claimBtn, Read, claimKey, () => writes,
                            true, initialSilver, status, summary);
                        group.interactable = true;

                        // 3. Hidden panel rejects raw button callback and public APIs.
                        Assert.IsTrue(panel.Hide(), spec.MapId.ToString());
                        AssertBlocked(panel, supply, claimBtn, Read, claimKey, () => writes,
                            false, initialSilver, status, summary);
                        Assert.IsTrue(panel.Show(), spec.MapId.ToString());

                        // 4. Restored panel accepts filter and idempotent one-time claim.
                        Assert.IsTrue(panel.TrySetUnclaimedOnly(true), spec.MapId.ToString());
                        claimBtn.onClick.Invoke();
                        Assert.AreEqual(1, panel.ClaimedCount, spec.MapId.ToString());
                        Assert.AreEqual(0, panel.ClaimableCount, spec.MapId.ToString());
                        Assert.AreEqual(7, panel.DisplayedMilestoneRowCount, spec.MapId.ToString());
                        Assert.AreEqual(1, Read(claimKey), spec.MapId.ToString());
                        Assert.AreEqual(initialSilver + 300, supply.SaltSilverBalance, spec.MapId.ToString());

                        int writesAfterClaim = writes;
                        claimBtn.onClick.Invoke();
                        Assert.IsFalse(panel.TryClickClaimAllEligible(), spec.MapId.ToString());
                        Assert.AreEqual(writesAfterClaim, writes, spec.MapId.ToString());
                        Assert.AreEqual(initialSilver + 300, supply.SaltSilverBalance, spec.MapId.ToString());
                    }

                    Assert.AreEqual(0, root.transform.childCount, spec.MapId.ToString());
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }
        }

        private static void AssertBlocked(
            RpgWorld500xExpeditionMilestonesHudPanel panel,
            RpgWorld500xFieldBountyAndSupplyService supply,
            Button claimBtn,
            Func<string, int> read,
            string claimKey,
            Func<int> getWrites,
            bool expectedVisible,
            int expectedSilver,
            string expectedStatus,
            string expectedSummary)
        {
            claimBtn.onClick.Invoke();
            Assert.IsFalse(panel.TryClickClaimAllEligible());
            Assert.IsFalse(panel.TrySetUnclaimedOnly(true));

            Assert.AreEqual(expectedVisible, panel.IsVisible);
            Assert.IsFalse(panel.UnclaimedOnly);
            Assert.AreEqual(8, panel.DisplayedMilestoneRowCount);
            Assert.AreEqual(1, panel.ClaimableCount);
            Assert.AreEqual(0, panel.ClaimedCount);
            Assert.AreEqual(0, read(claimKey));
            Assert.AreEqual(0, getWrites());
            Assert.AreEqual(expectedSilver, supply.SaltSilverBalance);
            Assert.AreEqual(expectedStatus, panel.StatusText);
            Assert.AreEqual(expectedSummary, panel.SummaryText);
        }
    }
}
