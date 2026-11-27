using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionCartographyReputationHudE2ETests
    {
        private static readonly RpgWorldMapId[] FourRegions =
        {
            RpgWorldMapId.Soundwell,
            RpgWorldMapId.RedSaltHarbor,
            RpgWorldMapId.WhitewaterCrossing,
            RpgWorldMapId.MineCamp
        };

        [Test]
        public void FourRegion_CartographyReputationHudPanel_LivePreviewAndRemoteTabsAcrossAllFourRegionsWithZeroSpriteLeak()
        {
            // Warm up one-time UGUI/font and per-region caches before steady-state leak baseline.
            var warmupGo = new GameObject("World500xFourRegionCartographyWarmupHost");
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

                Assert.IsTrue(warmupCtrl.MenuRouter.OpenModal(RpgWorld500xModalKind.CartographyReputation));
                warmupCtrl.MenuRouter.CloseAll();
            }
            finally
            {
                warmupCtrl.Shutdown();
                UnityEngine.Object.DestroyImmediate(warmupGo);
            }

            int spriteBefore = Resources.FindObjectsOfTypeAll<Sprite>().Length;

            var hostGo = new GameObject("World500xFourRegionCartographyE2EHost");
            var ctrl = hostGo.AddComponent<RpgWorld500xPlayModeController>();
            try
            {
                ctrl.Initialize(
                    overrideMap: RpgWorldMapId.Soundwell,
                    saveStorage: new RpgWorld500xInMemorySaveSlotStorage(),
                    overrideTileSlices: new Sprite[] { null });

                var boot = ctrl.Bootstrapper;
                var panel = ctrl.MenuRouter.CartographyPanel;

                for (int i = 0; i < FourRegions.Length; i++)
                {
                    RpgWorldMapId currentRegion = FourRegions[i];
                    if (boot.Driver.Facade.CurrentMapId != currentRegion)
                    {
                        Assert.IsTrue(boot.SwitchMap(currentRegion));
                    }

                    Assert.IsTrue(ctrl.MenuRouter.OpenModal(RpgWorld500xModalKind.CartographyReputation));
                    Assert.IsTrue(panel.IsVisible);
                    Assert.IsTrue(panel.TrySelectRegion(currentRegion));
                    Assert.AreEqual(currentRegion, panel.SelectedMapId);
                    Assert.IsTrue(panel.HasCurrentMapCartographyPreview);
                    Assert.AreEqual(currentRegion, panel.LastCartographyPreview.MapId);
                    StringAssert.Contains("当前所在", panel.HeaderText);
                    StringAssert.Contains("综合堪舆度", panel.SummaryText);
                    StringAssert.Contains("【堪舆里程碑】", panel.CartographyPreviewSummaryZh);
                    StringAssert.Contains("【四境声望总览】", panel.WorldReputationSummaryZh);
                    Assert.AreEqual(panel.RemainingMilestoneSaltSilver, panel.LastCartographyPreview.RemainingSaltSilver);
                    Assert.GreaterOrEqual(panel.ActiveDistrictCoverageRatio, 0f);
                    Assert.LessOrEqual(panel.ActiveDistrictCoverageRatio, 1f);
                    Assert.AreEqual(3, panel.TotalMilestoneCount);
                    Assert.Greater(panel.DisplayedRowCount, 3);

                    for (int j = 0; j < FourRegions.Length; j++)
                    {
                        RpgWorldMapId otherRegion = FourRegions[j];
                        if (otherRegion == currentRegion)
                        {
                            continue;
                        }

                        Assert.IsTrue(panel.TrySelectRegion(otherRegion));
                        Assert.AreEqual(otherRegion, panel.SelectedMapId);
                        Assert.IsFalse(panel.HasCurrentMapCartographyPreview);
                        Assert.AreEqual(string.Empty, panel.CartographyPreviewSummaryZh);
                        Assert.AreEqual(0f, panel.ActiveDistrictCoverageRatio, 0.0001f);
                        StringAssert.Contains("非当前所在区域", panel.SummaryText);
                        Assert.Greater(panel.DisplayedRowCount, 3);
                    }
                }

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
        public void FourRegion_CartographyReputationHudPanel_TrySelectHighestStandingRegionTracksDynamicLeader()
        {
            var hostGo = new GameObject("World500xFourRegionCartographyLeaderHost");
            var ctrl = hostGo.AddComponent<RpgWorld500xPlayModeController>();
            try
            {
                ctrl.Initialize(
                    overrideMap: RpgWorldMapId.Soundwell,
                    saveStorage: new RpgWorld500xInMemorySaveSlotStorage(),
                    overrideTileSlices: new Sprite[] { null });

                var panel = ctrl.MenuRouter.CartographyPanel;
                Assert.IsTrue(ctrl.MenuRouter.OpenModal(RpgWorld500xModalKind.CartographyReputation));

                int[] cumulativePoints = { 100, 250, 450, 700 };
                for (int i = 0; i < FourRegions.Length; i++)
                {
                    RpgWorldMapId leader = FourRegions[i];
                    RpgWorldMapId nonLeader = FourRegions[(i + 1) % FourRegions.Length];

                    ctrl.CampHaven.Reputation.AddPoints(leader, cumulativePoints[i]);
                    Assert.IsTrue(panel.TrySelectRegion(nonLeader));
                    Assert.AreEqual(nonLeader, panel.SelectedMapId);

                    panel.Refresh();
                    Assert.AreEqual(leader, panel.LastWorldReputationOverview.HighestStandingMapId);
                    Assert.IsTrue(panel.TrySelectHighestStandingRegion());
                    Assert.AreEqual(leader, panel.SelectedMapId);
                    StringAssert.Contains("【四境声望总览】", panel.WorldReputationSummaryZh);
                }

                ctrl.MenuRouter.CloseAll();
            }
            finally
            {
                ctrl.Shutdown();
                UnityEngine.Object.DestroyImmediate(hostGo);
            }
        }

        [Test]
        public void FourRegion_CartographyReputationHudPanel_EnforcesModalExclusionAndPostShutdownGuard()
        {
            var hostGo = new GameObject("World500xFourRegionCartographyLifecycleHost");
            var ctrl = hostGo.AddComponent<RpgWorld500xPlayModeController>();
            RpgWorld500xCartographyReputationHudPanel panel = null;
            try
            {
                ctrl.Initialize(
                    overrideMap: RpgWorldMapId.Soundwell,
                    saveStorage: new RpgWorld500xInMemorySaveSlotStorage(),
                    overrideTileSlices: new Sprite[] { null });

                panel = ctrl.MenuRouter.CartographyPanel;
                var milestones = ctrl.MenuRouter.MilestonesPanel;

                Assert.IsTrue(ctrl.MenuRouter.OpenModal(RpgWorld500xModalKind.CartographyReputation));
                Assert.IsTrue(panel.IsVisible);
                Assert.IsFalse(milestones.IsVisible);

                Assert.IsTrue(ctrl.MenuRouter.OpenModal(RpgWorld500xModalKind.ExpeditionMilestones));
                Assert.IsFalse(panel.IsVisible);
                Assert.IsTrue(milestones.IsVisible);

                ctrl.MenuRouter.CloseAll();
                Assert.IsFalse(panel.IsVisible);
                Assert.IsFalse(milestones.IsVisible);
                Assert.IsFalse(ctrl.MenuRouter.HasAnyModalOpen);
            }
            finally
            {
                ctrl.Shutdown();
                UnityEngine.Object.DestroyImmediate(hostGo);
            }

            Assert.IsNotNull(panel);
            Assert.IsTrue(panel.IsDisposed);
            Assert.IsFalse(panel.IsVisible);
            Assert.IsFalse(panel.TrySelectRegion(RpgWorldMapId.MineCamp));
            Assert.IsFalse(panel.TrySelectHighestStandingRegion());
        }
    }
}
