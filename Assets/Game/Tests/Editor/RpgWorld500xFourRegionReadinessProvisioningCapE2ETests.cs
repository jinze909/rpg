using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionReadinessProvisioningCapE2ETests
    {
        private static readonly RpgWorldMapId[] FourRegions =
        {
            RpgWorldMapId.Soundwell,
            RpgWorldMapId.RedSaltHarbor,
            RpgWorldMapId.WhitewaterCrossing,
            RpgWorldMapId.MineCamp
        };

        [Test]
        public void FourRegion_ExpeditionReadinessHudPanel_EnforcesBudgetCapAndCompletesSafestRouteProvisioningWithZeroSpriteLeak()
        {
            var root = new GameObject("World500xFourRegionReadinessCapRoot");
            try
            {
                // Warm up one-time UGUI/font and per-region caches before steady-state Sprite leak baseline.
                for (int w = 0; w < FourRegions.Length; w++)
                {
                    using (var warmupBoot = new RpgWorld500xSceneBootstrapper(
                        initialMapId: FourRegions[w],
                        overrideTileSlices: new Sprite[] { null }))
                    using (var warmupPanel = new RpgWorld500xExpeditionReadinessHudPanel(root.transform, warmupBoot))
                    {
                        warmupPanel.Show(0f);
                    }
                }

                int spriteBefore = Resources.FindObjectsOfTypeAll<Sprite>().Length;

                for (int r = 0; r < FourRegions.Length; r++)
                {
                    RpgWorldMapId origin = FourRegions[r];
                    RpgWorldMapId destination = FourRegions[(r + 2) % FourRegions.Length];

                    var regionRoot = new GameObject("RegionReadiness_" + origin);
                    regionRoot.transform.SetParent(root.transform, false);
                    try
                    {
                        using (var boot = new RpgWorld500xSceneBootstrapper(
                            initialMapId: origin,
                            overrideTileSlices: new Sprite[] { null }))
                        using (var panel = new RpgWorld500xExpeditionReadinessHudPanel(regionRoot.transform, boot))
                        {
                            var facade = boot.Driver.Facade;
                            facade.Atmosphere.SetWeather(destination, RpgWorld500xWeatherKind.GaleStorm);
                            facade.BountyAndSupply.AddSaltSilver(400);
                            int walletBefore = facade.BountyAndSupply.SaltSilverBalance;

                            Assert.IsTrue(panel.Show(5f), $"ReadinessHudPanel failed to show on {origin}.");
                            Assert.IsTrue(panel.IsVisible);

                            string breakdownBefore = panel.FormatProvisioningBreakdownForDestination(
                                destination, preferSafestRoute: true);
                            StringAssert.Contains("合计", breakdownBefore);

                            // Strict budget cap of 1 Salt-Silver must refuse partial purchase and spend 0.
                            Assert.IsTrue(panel.TryAutoProvisionForDestination(
                                destination,
                                allowPartialIfUnderBudget: false,
                                maxBudgetSaltSilver: 1,
                                preferSafestRoute: true));
                            Assert.IsFalse(panel.LastProvisioningReceipt.FullyProvisioned);
                            Assert.AreEqual(0, panel.LastProvisioningReceipt.TotalSpentSaltSilver);
                            Assert.AreEqual(walletBefore, facade.BountyAndSupply.SaltSilverBalance);

                            // Full budget allows safest-route auto-provisioning to stock everything cleanly.
                            Assert.IsTrue(panel.TryAutoProvisionForDestination(
                                destination,
                                allowPartialIfUnderBudget: false,
                                maxBudgetSaltSilver: 400,
                                preferSafestRoute: true));
                            Assert.IsTrue(panel.LastProvisioningReceipt.FullyProvisioned);
                            int spent = panel.LastProvisioningReceipt.TotalSpentSaltSilver;
                            Assert.Greater(spent, 0);
                            Assert.LessOrEqual(spent, 400);
                            Assert.AreEqual(walletBefore - spent, facade.BountyAndSupply.SaltSilverBalance);

                            Assert.AreEqual(
                                "行囊已齐备（无需补购）",
                                panel.FormatProvisioningBreakdownForDestination(destination, preferSafestRoute: true));
                        }
                    }
                    finally
                    {
                        UnityEngine.Object.DestroyImmediate(regionRoot);
                    }
                }

                Assert.AreEqual(0, root.transform.childCount);
                Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Sprite>().Length, spriteBefore);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void FourRegion_ExpeditionReadinessHudPanel_RespectsHiddenAncestorCanvasGroupLockAndPostDisposeGuard()
        {
            var root = new GameObject("World500xFourRegionReadinessGuardRoot");
            RpgWorld500xExpeditionReadinessHudPanel disposedPanel = null;
            try
            {
                using (var boot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.Soundwell,
                    overrideTileSlices: new Sprite[] { null }))
                using (var panel = new RpgWorld500xExpeditionReadinessHudPanel(root.transform, boot))
                {
                    disposedPanel = panel;
                    var facade = boot.Driver.Facade;
                    facade.Atmosphere.SetWeather(RpgWorldMapId.MineCamp, RpgWorld500xWeatherKind.GaleStorm);
                    facade.BountyAndSupply.AddSaltSilver(400);
                    int walletBefore = facade.BountyAndSupply.SaltSilverBalance;

                    // Hidden panel must reject auto-provisioning without spending Salt-Silver.
                    Assert.IsFalse(panel.IsVisible);
                    Assert.IsFalse(panel.TryAutoProvisionForDestination(
                        RpgWorldMapId.MineCamp, allowPartialIfUnderBudget: false, preferSafestRoute: true));
                    Assert.AreEqual(walletBefore, facade.BountyAndSupply.SaltSilverBalance);

                    // Ancestor CanvasGroup lock must block auto-provisioning without spending Salt-Silver.
                    Assert.IsTrue(panel.Show(5f));
                    CanvasGroup group = root.AddComponent<CanvasGroup>();
                    group.interactable = false;
                    Assert.IsFalse(panel.TryAutoProvisionForDestination(
                        RpgWorldMapId.MineCamp, allowPartialIfUnderBudget: false, preferSafestRoute: true));
                    Assert.AreEqual(walletBefore, facade.BountyAndSupply.SaltSilverBalance);

                    // Unlocking restores normal provisioning.
                    group.interactable = true;
                    Assert.IsTrue(panel.TryAutoProvisionForDestination(
                        RpgWorldMapId.MineCamp, allowPartialIfUnderBudget: false, preferSafestRoute: true));
                    Assert.IsTrue(panel.LastProvisioningReceipt.FullyProvisioned);
                    Assert.Less(facade.BountyAndSupply.SaltSilverBalance, walletBefore);
                }

                Assert.IsNotNull(disposedPanel);
                Assert.IsTrue(disposedPanel.IsDisposed);
                Assert.IsFalse(disposedPanel.IsVisible);
                Assert.IsFalse(disposedPanel.TryAutoProvisionForDestination(RpgWorldMapId.MineCamp));
                Assert.AreEqual(string.Empty, disposedPanel.FormatProvisioningBreakdownForDestination(RpgWorldMapId.MineCamp));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
