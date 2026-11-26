using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xMilestonesAndCartographyHudE2ETests
    {
        [Test]
        public void PlayModeController_FourRegionSwornReputationClaimsMilestoneAndSyncsCartographyHudWithZeroLeak()
        {
            // Warm up one-time UGUI/font caches before steady-state leak baseline.
            var warmupGo = new GameObject("World500xMilestonesCartographyWarmupHost");
            var warmupCtrl = warmupGo.AddComponent<RpgWorld500xPlayModeController>();
            try
            {
                warmupCtrl.Initialize(
                    overrideMap: RpgWorldMapId.Soundwell,
                    saveStorage: new RpgWorld500xInMemorySaveSlotStorage(),
                    overrideTileSlices: new Sprite[] { null });
                warmupCtrl.MenuRouter.OpenModal(RpgWorld500xModalKind.ExpeditionMilestones);
                warmupCtrl.MenuRouter.OpenModal(RpgWorld500xModalKind.CartographyReputation);
                warmupCtrl.MenuRouter.CloseAll();
            }
            finally
            {
                warmupCtrl.Shutdown();
                UnityEngine.Object.DestroyImmediate(warmupGo);
            }

            int texBefore = Resources.FindObjectsOfTypeAll<Texture2D>().Length;
            int spriteBefore = Resources.FindObjectsOfTypeAll<Sprite>().Length;

            var hostGo = new GameObject("World500xMilestonesCartographyE2EHost");
            var ctrl = hostGo.AddComponent<RpgWorld500xPlayModeController>();
            try
            {
                ctrl.Initialize(
                    overrideMap: RpgWorldMapId.Soundwell,
                    saveStorage: new RpgWorld500xInMemorySaveSlotStorage(),
                    overrideTileSlices: new Sprite[] { null });

                var boot = ctrl.Bootstrapper;
                int initialSilver = boot.Driver.Facade.BountyAndSupply.SaltSilverBalance;

                // Reach Sworn standing across all four regions to unlock four_regions_sworn (+300 Salt-Silver).
                IReadOnlyList<RpgExpandedMapSpec> specs = RpgExpandedWorldScale.AllSpecs;
                for (int i = 0; i < specs.Count; i++)
                {
                    ctrl.CampHaven.Reputation.AddPoints(specs[i].MapId, 800);
                }

                // Open ExpeditionMilestones modal, verify summary, claim reward, and toggle UnclaimedOnly filter.
                Assert.IsTrue(ctrl.MenuRouter.OpenModal(RpgWorld500xModalKind.ExpeditionMilestones));
                var milestones = ctrl.MenuRouter.MilestonesPanel;
                Assert.IsTrue(milestones.IsVisible);
                Assert.AreEqual(8, milestones.DisplayedMilestoneRowCount);
                Assert.AreEqual(1, milestones.ClaimableCount);
                Assert.AreEqual(300, milestones.ClaimableSaltSilver);
                StringAssert.Contains("【远征里程碑】", milestones.MilestoneSummaryLineZh);

                Assert.IsTrue(milestones.TryClickClaimAllEligible());
                Assert.AreEqual(0, milestones.ClaimableCount);
                Assert.AreEqual(0, milestones.ClaimableSaltSilver);
                Assert.AreEqual(1, milestones.ClaimedCount);
                Assert.AreEqual(initialSilver + 300, boot.Driver.Facade.BountyAndSupply.SaltSilverBalance);

                Assert.IsTrue(milestones.TrySetUnclaimedOnly(true));
                Assert.IsTrue(milestones.UnclaimedOnly);
                Assert.AreEqual(7, milestones.DisplayedMilestoneRowCount);

                // Switch to CartographyReputation modal (mutual exclusion) and verify live + remote previews.
                Assert.IsTrue(ctrl.MenuRouter.OpenModal(RpgWorld500xModalKind.CartographyReputation));
                var cartography = ctrl.MenuRouter.CartographyPanel;
                Assert.IsFalse(milestones.IsVisible);
                Assert.IsTrue(cartography.IsVisible);
                Assert.AreEqual(RpgWorldMapId.Soundwell, cartography.SelectedMapId);
                Assert.IsTrue(cartography.HasCurrentMapCartographyPreview);
                StringAssert.Contains("【堪舆里程碑】", cartography.CartographyPreviewSummaryZh);
                StringAssert.Contains("【四境声望总览】", cartography.WorldReputationSummaryZh);

                Assert.IsTrue(cartography.TrySelectRegion(RpgWorldMapId.WhitewaterCrossing));
                Assert.AreEqual(RpgWorldMapId.WhitewaterCrossing, cartography.SelectedMapId);
                Assert.IsFalse(cartography.HasCurrentMapCartographyPreview);

                Assert.IsTrue(cartography.TrySelectHighestStandingRegion());
                Assert.AreEqual(cartography.LastWorldReputationOverview.HighestStandingMapId, cartography.SelectedMapId);

                ctrl.MenuRouter.CloseAll();
                Assert.IsFalse(ctrl.MenuRouter.HasAnyModalOpen);
            }
            finally
            {
                ctrl.Shutdown();
                UnityEngine.Object.DestroyImmediate(hostGo);
            }

            Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Texture2D>().Length, texBefore);
            Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Sprite>().Length, spriteBefore);
        }
    }
}
