using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionExpeditionDigestHudE2ETests
    {
        private static readonly RpgWorldMapId[] FourRegions =
        {
            RpgWorldMapId.Soundwell,
            RpgWorldMapId.RedSaltHarbor,
            RpgWorldMapId.WhitewaterCrossing,
            RpgWorldMapId.MineCamp
        };

        [Test]
        public void FourRegion_ExpeditionDigestHudPanel_TracksSafeToHazardousWeatherAndWalletAcrossAllFourMapsWithZeroSpriteLeak()
        {
            var root = new GameObject("World500xFourRegionDigestHudRoot");
            try
            {
                // Warm up one-time UGUI/font and per-region caches before steady-state leak baseline.
                for (int w = 0; w < FourRegions.Length; w++)
                {
                    using (var warmupBoot = new RpgWorld500xSceneBootstrapper(
                        initialMapId: FourRegions[w],
                        overrideTileSlices: new Sprite[] { null }))
                    using (var warmupPanel = new RpgWorld500xExpeditionDigestHudPanel(root.transform, warmupBoot))
                    {
                        warmupPanel.Show(0f);
                        warmupPanel.TryClickClose();
                    }
                }

                int spriteBefore = Resources.FindObjectsOfTypeAll<Sprite>().Length;

                for (int i = 0; i < FourRegions.Length; i++)
                {
                    RpgWorldMapId region = FourRegions[i];
                    var regionRoot = new GameObject("RegionDigest_" + region);
                    regionRoot.transform.SetParent(root.transform, false);
                    try
                    {
                        using (var boot = new RpgWorld500xSceneBootstrapper(
                            initialMapId: region,
                            overrideTileSlices: new Sprite[] { null }))
                        using (var panel = new RpgWorld500xExpeditionDigestHudPanel(regionRoot.transform, boot))
                        {
                            var facade = boot.Driver.Facade;
                            for (int m = 0; m < FourRegions.Length; m++)
                            {
                                facade.Atmosphere.SetWeather(FourRegions[m], RpgWorld500xWeatherKind.Clear);
                            }

                            facade.BountyAndSupply.AddSaltSilver(70 + (i * 25));
                            int balance = facade.BountyAndSupply.SaltSilverBalance;

                            float showTime = 10f + i;
                            Assert.IsTrue(panel.Show(showTime), $"ExpeditionDigestHudPanel failed to show on {region}.");
                            Assert.IsTrue(panel.IsVisible);
                            StringAssert.Contains("远征营火手记", panel.HeaderText);
                            StringAssert.Contains("盐银 " + balance, panel.StatusRibbonText);
                            StringAssert.Contains("【四境气象速报】", panel.WorldWeatherBulletinText);
                            StringAssert.Contains("【四境声望总览】", panel.WorldReputationSummaryText);
                            StringAssert.Contains("【远征里程碑】", panel.MilestoneSummaryText);
                            StringAssert.Contains("远征营火纪要", panel.DigestBodyText);
                            Assert.AreEqual(RpgWorld500xTravelRiskLevel.Safe, panel.LastDigest.TravelRiskLevel);
                            Assert.AreEqual(0, panel.HazardousRegionCount);
                            StringAssert.Contains("高危区域 0 境", panel.RiskBannerText);

                            // Escalate active region weather to GaleStorm and verify live refresh readout.
                            int refreshesBefore = panel.RefreshCount;
                            facade.Atmosphere.SetWeather(region, RpgWorld500xWeatherKind.GaleStorm);
                            float refreshTime = 20f + i;
                            Assert.IsTrue(panel.TryClickRefresh(refreshTime));
                            Assert.AreEqual(refreshesBefore + 1, panel.RefreshCount);
                            Assert.AreEqual(refreshTime, panel.LastElapsedSeconds, 0.001f);
                            Assert.AreEqual(RpgWorld500xTravelRiskLevel.Hazardous, panel.LastDigest.TravelRiskLevel);
                            Assert.AreEqual(1, panel.HazardousRegionCount);
                            StringAssert.Contains("高危", panel.RiskBannerText);
                            StringAssert.Contains("高危区域 1 境", panel.RiskBannerText);

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
                Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Sprite>().Length, spriteBefore);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void FourRegion_ExpeditionDigestHudPanel_RespectsAncestorCanvasGroupLockAndPostDisposeGuard()
        {
            var root = new GameObject("World500xFourRegionDigestGuardRoot");
            RpgWorld500xExpeditionDigestHudPanel disposedPanel = null;
            try
            {
                using (var boot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.MineCamp,
                    overrideTileSlices: new Sprite[] { null }))
                using (var panel = new RpgWorld500xExpeditionDigestHudPanel(root.transform, boot))
                {
                    disposedPanel = panel;
                    Assert.IsTrue(panel.Show(5f));
                    int refreshesBeforeLock = panel.RefreshCount;

                    CanvasGroup group = root.AddComponent<CanvasGroup>();
                    group.interactable = false;
                    Assert.IsFalse(panel.TryClickRefresh(9f));
                    Assert.AreEqual(refreshesBeforeLock, panel.RefreshCount);
                    Assert.IsFalse(panel.TryClickClose());
                    Assert.IsTrue(panel.IsVisible);

                    group.interactable = true;
                    Assert.IsTrue(panel.TryClickRefresh(12f));
                    Assert.AreEqual(refreshesBeforeLock + 1, panel.RefreshCount);
                    Assert.IsTrue(panel.TryClickClose());
                    Assert.IsFalse(panel.IsVisible);
                }

                Assert.IsNotNull(disposedPanel);
                Assert.IsTrue(disposedPanel.IsDisposed);
                Assert.IsFalse(disposedPanel.IsVisible);
                Assert.IsFalse(disposedPanel.TryClickRefresh(15f));
                Assert.IsFalse(disposedPanel.TryClickClose());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
