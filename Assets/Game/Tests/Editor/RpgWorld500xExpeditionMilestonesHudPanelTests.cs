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
