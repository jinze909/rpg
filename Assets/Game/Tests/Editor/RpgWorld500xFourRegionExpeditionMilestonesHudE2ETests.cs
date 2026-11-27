using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionExpeditionMilestonesHudE2ETests
    {
        private static readonly RpgWorldMapId[] FourRegions =
        {
            RpgWorldMapId.Soundwell,
            RpgWorldMapId.RedSaltHarbor,
            RpgWorldMapId.WhitewaterCrossing,
            RpgWorldMapId.MineCamp
        };

        [Test]
        public void FourRegion_ExpeditionMilestonesHudPanel_ClaimAndUnclaimedFilterSyncAcrossAllFourMapsWithZeroSpriteLeak()
        {
            // Warm up one-time UGUI/font and per-region caches before steady-state leak baseline.
            var warmupGo = new GameObject("World500xFourRegionMilestonesWarmupHost");
            var warmupCtrl = warmupGo.AddComponent<RpgWorld500xPlayModeController>();
            try
            {
                warmupCtrl.Initialize(
                    overrideMap: RpgWorldMapId.Soundwell,
                    saveStorage: new RpgWorld500xInMemorySaveSlotStorage(),
                    overrideTileSlices: new Sprite[] { null });
                for (int i = 1; i < FourRegions.Length; i++)
                {
                    Assert.IsTrue(warmupCtrl.Bootstrapper.SwitchMap(FourRegions[i]));
                }

                Assert.IsTrue(warmupCtrl.MenuRouter.OpenModal(RpgWorld500xModalKind.ExpeditionMilestones));
                warmupCtrl.MenuRouter.CloseAll();
            }
            finally
            {
                warmupCtrl.Shutdown();
                UnityEngine.Object.DestroyImmediate(warmupGo);
            }

            int spriteBefore = Resources.FindObjectsOfTypeAll<Sprite>().Length;

            var hostGo = new GameObject("World500xFourRegionMilestonesE2EHost");
            var ctrl = hostGo.AddComponent<RpgWorld500xPlayModeController>();
            try
            {
                ctrl.Initialize(
                    overrideMap: RpgWorldMapId.Soundwell,
                    saveStorage: new RpgWorld500xInMemorySaveSlotStorage(),
                    overrideTileSlices: new Sprite[] { null });

                var boot = ctrl.Bootstrapper;
                var panel = ctrl.MenuRouter.MilestonesPanel;
                int initialSilver = boot.Driver.Facade.BountyAndSupply.SaltSilverBalance;

                Assert.IsTrue(ctrl.MenuRouter.OpenModal(RpgWorld500xModalKind.ExpeditionMilestones));
                Assert.IsTrue(panel.IsVisible);
                Assert.AreEqual(8, panel.DisplayedMilestoneRowCount);
                Assert.AreEqual(0, panel.ClaimableCount);
                Assert.AreEqual(0, panel.ClaimedCount);
                Assert.AreEqual(0, panel.ClaimableSaltSilver);
                Assert.IsFalse(panel.UnclaimedOnly);
                Assert.IsFalse(panel.TryClickClaimAllEligible());
                StringAssert.Contains("【远征里程碑】", panel.MilestoneSummaryLineZh);

                // Promote all 4 regions to Sworn (+800 each) to unlock four_regions_sworn (+300 Salt-Silver).
                for (int i = 0; i < FourRegions.Length; i++)
                {
                    ctrl.CampHaven.Reputation.AddPoints(FourRegions[i], 800);
                }

                Assert.IsTrue(boot.SwitchMap(RpgWorldMapId.MineCamp));
                panel.Refresh();
                Assert.AreEqual(1, panel.ClaimableCount);
                Assert.AreEqual(300, panel.ClaimableSaltSilver);

                // Claim idempotently on MineCamp.
                Assert.IsTrue(panel.TryClickClaimAllEligible());
                Assert.AreEqual(1, panel.ClaimedCount);
                Assert.AreEqual(0, panel.ClaimableCount);
                Assert.AreEqual(0, panel.ClaimableSaltSilver);
                Assert.AreEqual(initialSilver + 300, boot.Driver.Facade.BountyAndSupply.SaltSilverBalance);
                Assert.IsFalse(panel.TryClickClaimAllEligible());

                // Enable UnclaimedOnly filter (8 -> 7 rows) and verify persistence across all 4 maps.
                Assert.IsTrue(panel.TrySetUnclaimedOnly(true));
                Assert.IsTrue(panel.UnclaimedOnly);
                Assert.AreEqual(7, panel.DisplayedMilestoneRowCount);

                for (int i = 0; i < FourRegions.Length; i++)
                {
                    if (boot.Driver.Facade.CurrentMapId != FourRegions[i])
                    {
                        Assert.IsTrue(boot.SwitchMap(FourRegions[i]));
                    }

                    panel.Refresh();
                    Assert.IsTrue(panel.UnclaimedOnly);
                    Assert.AreEqual(1, panel.ClaimedCount);
                    Assert.AreEqual(0, panel.ClaimableCount);
                    Assert.AreEqual(7, panel.DisplayedMilestoneRowCount);
                }

                Assert.IsTrue(panel.TrySetUnclaimedOnly(false));
                Assert.IsFalse(panel.UnclaimedOnly);
                Assert.AreEqual(8, panel.DisplayedMilestoneRowCount);

                ctrl.MenuRouter.CloseAll();
                Assert.IsFalse(panel.IsVisible);
                Assert.IsFalse(ctrl.MenuRouter.HasAnyModalOpen);
            }
            finally
            {
                ctrl.Shutdown();
                UnityEngine.Object.DestroyImmediate(hostGo);
            }

            Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Sprite>().Length, spriteBefore);
        }

        [Test]
        public void FourRegion_ExpeditionMilestonesHudPanel_PersistsClaimedStateAcrossSaveLoadAndRegionSwitch()
        {
            var storage = new RpgWorld500xInMemorySaveSlotStorage();
            int expectedSilverAfterClaim;

            var firstHostGo = new GameObject("World500xFourRegionMilestonesSaveHost");
            var firstCtrl = firstHostGo.AddComponent<RpgWorld500xPlayModeController>();
            try
            {
                firstCtrl.Initialize(
                    overrideMap: RpgWorldMapId.WhitewaterCrossing,
                    saveStorage: storage,
                    overrideTileSlices: new Sprite[] { null });

                for (int i = 0; i < FourRegions.Length; i++)
                {
                    firstCtrl.CampHaven.Reputation.AddPoints(FourRegions[i], 800);
                }

                Assert.IsTrue(firstCtrl.MenuRouter.OpenModal(RpgWorld500xModalKind.ExpeditionMilestones));
                var milestones = firstCtrl.MenuRouter.MilestonesPanel;
                Assert.AreEqual(1, milestones.ClaimableCount);
                Assert.AreEqual(300, milestones.ClaimableSaltSilver);
                Assert.IsTrue(milestones.TryClickClaimAllEligible());
                expectedSilverAfterClaim = firstCtrl.Bootstrapper.Driver.Facade.BountyAndSupply.SaltSilverBalance;

                Assert.IsTrue(firstCtrl.MenuRouter.OpenModal(RpgWorld500xModalKind.SaveSlots));
                Assert.IsTrue(firstCtrl.MenuRouter.SaveSlotPanel.TryClickSave(RpgWorld500xSaveSlotId.Slot1));
                firstCtrl.MenuRouter.CloseAll();
            }
            finally
            {
                firstCtrl.Shutdown();
                UnityEngine.Object.DestroyImmediate(firstHostGo);
            }

            var secondHostGo = new GameObject("World500xFourRegionMilestonesLoadHost");
            var secondCtrl = secondHostGo.AddComponent<RpgWorld500xPlayModeController>();
            try
            {
                secondCtrl.Initialize(
                    overrideMap: RpgWorldMapId.RedSaltHarbor,
                    saveStorage: storage,
                    overrideTileSlices: new Sprite[] { null });

                Assert.IsTrue(secondCtrl.MenuRouter.OpenModal(RpgWorld500xModalKind.SaveSlots));
                Assert.IsTrue(secondCtrl.MenuRouter.SaveSlotPanel.TryClickLoad(RpgWorld500xSaveSlotId.Slot1));

                Assert.AreEqual(RpgWorldMapId.WhitewaterCrossing, secondCtrl.Bootstrapper.Driver.Facade.CurrentMapId);
                Assert.AreEqual(expectedSilverAfterClaim, secondCtrl.Bootstrapper.Driver.Facade.BountyAndSupply.SaltSilverBalance);

                Assert.IsTrue(secondCtrl.MenuRouter.OpenModal(RpgWorld500xModalKind.ExpeditionMilestones));
                var restoredPanel = secondCtrl.MenuRouter.MilestonesPanel;
                Assert.AreEqual(1, restoredPanel.ClaimedCount);
                Assert.AreEqual(0, restoredPanel.ClaimableCount);
                Assert.AreEqual(0, restoredPanel.ClaimableSaltSilver);
                Assert.IsFalse(restoredPanel.TryClickClaimAllEligible());
                Assert.AreEqual(expectedSilverAfterClaim, secondCtrl.Bootstrapper.Driver.Facade.BountyAndSupply.SaltSilverBalance);
            }
            finally
            {
                secondCtrl.Shutdown();
                UnityEngine.Object.DestroyImmediate(secondHostGo);
            }
        }

        [Test]
        public void FourRegion_ExpeditionMilestonesHudPanel_RespectsAncestorCanvasGroupLockAndPostShutdownGuard()
        {
            var hostGo = new GameObject("World500xFourRegionMilestonesGuardHost");
            var ctrl = hostGo.AddComponent<RpgWorld500xPlayModeController>();
            RpgWorld500xExpeditionMilestonesHudPanel panel = null;
            try
            {
                ctrl.Initialize(
                    overrideMap: RpgWorldMapId.Soundwell,
                    saveStorage: new RpgWorld500xInMemorySaveSlotStorage(),
                    overrideTileSlices: new Sprite[] { null });

                for (int i = 0; i < FourRegions.Length; i++)
                {
                    ctrl.CampHaven.Reputation.AddPoints(FourRegions[i], 800);
                }

                panel = ctrl.MenuRouter.MilestonesPanel;
                int walletBefore = ctrl.Bootstrapper.Driver.Facade.BountyAndSupply.SaltSilverBalance;

                Assert.IsTrue(ctrl.MenuRouter.OpenModal(RpgWorld500xModalKind.ExpeditionMilestones));
                Assert.AreEqual(1, panel.ClaimableCount);

                CanvasGroup group = hostGo.AddComponent<CanvasGroup>();
                group.interactable = false;
                Assert.IsFalse(panel.TrySetUnclaimedOnly(true));
                Assert.IsFalse(panel.TryClickClaimAllEligible());
                Assert.AreEqual(0, panel.ClaimedCount);
                Assert.AreEqual(walletBefore, ctrl.Bootstrapper.Driver.Facade.BountyAndSupply.SaltSilverBalance);

                group.interactable = true;
                Assert.IsTrue(panel.TrySetUnclaimedOnly(true));
                Assert.IsTrue(panel.TryClickClaimAllEligible());
                Assert.AreEqual(1, panel.ClaimedCount);
                Assert.AreEqual(walletBefore + 300, ctrl.Bootstrapper.Driver.Facade.BountyAndSupply.SaltSilverBalance);

                ctrl.MenuRouter.CloseAll();
            }
            finally
            {
                ctrl.Shutdown();
                UnityEngine.Object.DestroyImmediate(hostGo);
            }

            Assert.IsNotNull(panel);
            Assert.IsTrue(panel.IsDisposed);
            Assert.IsFalse(panel.IsVisible);
            Assert.IsFalse(panel.TrySetUnclaimedOnly(false));
            Assert.IsFalse(panel.TryClickClaimAllEligible());
        }
    }
}
