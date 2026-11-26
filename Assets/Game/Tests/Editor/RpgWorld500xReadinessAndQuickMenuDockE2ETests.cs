using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xReadinessAndQuickMenuDockE2ETests
    {
        [Test]
        public void QuickMenuDockModalSwitchingAndReadinessSafestProvisioning_IntegrateEndToEndWithZeroLeak()
        {
            var root = new GameObject("World500xReadinessQuickMenuE2ERoot");
            try
            {
                // Warm up one-time UGUI/font caches before steady-state leak baseline.
                using (var warmupBoot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.WhitewaterCrossing,
                    overrideTileSlices: new Sprite[] { null }))
                using (var warmupRouter = new RpgWorld500xMenuRouter(
                    root.transform, warmupBoot, new RpgWorld500xCampHavenService()))
                using (var warmupDock = new RpgWorld500xQuickMenuDockHud(root.transform, warmupRouter))
                using (var warmupReadiness = new RpgWorld500xExpeditionReadinessHudPanel(root.transform, warmupBoot))
                {
                    warmupDock.TryClickModalButton(RpgWorld500xModalKind.ExpeditionReadiness);
                    warmupDock.TryClickModalButton(RpgWorld500xModalKind.ExpeditionReadiness);
                    warmupReadiness.Show(1f);
                    warmupReadiness.TryClickClose();
                }

                int texBefore = Resources.FindObjectsOfTypeAll<Texture2D>().Length;
                int spriteBefore = Resources.FindObjectsOfTypeAll<Sprite>().Length;

                using (var boot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.WhitewaterCrossing,
                    overrideTileSlices: new Sprite[] { null }))
                using (var router = new RpgWorld500xMenuRouter(
                    root.transform, boot, new RpgWorld500xCampHavenService()))
                using (var dock = new RpgWorld500xQuickMenuDockHud(root.transform, router))
                using (var readiness = new RpgWorld500xExpeditionReadinessHudPanel(root.transform, boot))
                {
                    // 1. QuickMenuDockHud exposes all 16 modals and keeps active highlight synced with MenuRouter.
                    Assert.AreEqual(16, dock.DisplayedButtonCount);
                    Assert.AreEqual(RpgWorld500xModalKind.None, dock.ActiveHighlightedModal);

                    Assert.IsTrue(dock.TryClickModalButton(RpgWorld500xModalKind.ExpeditionReadiness));
                    Assert.AreEqual(RpgWorld500xModalKind.ExpeditionReadiness, router.ActiveModal);
                    Assert.AreEqual(RpgWorld500xModalKind.ExpeditionReadiness, dock.ActiveHighlightedModal);

                    Assert.IsTrue(dock.TryClickModalButton(RpgWorld500xModalKind.FastTravelBeacons));
                    Assert.AreEqual(RpgWorld500xModalKind.FastTravelBeacons, router.ActiveModal);
                    Assert.AreEqual(RpgWorld500xModalKind.FastTravelBeacons, dock.ActiveHighlightedModal);

                    Assert.IsTrue(dock.TryClickModalButton(RpgWorld500xModalKind.FastTravelBeacons));
                    Assert.AreEqual(RpgWorld500xModalKind.None, router.ActiveModal);
                    Assert.AreEqual(RpgWorld500xModalKind.None, dock.ActiveHighlightedModal);

                    // 2. ExpeditionReadinessHudPanel evaluates safest-route provisioning and stocks required supplies.
                    RpgWorld500xGrandRuntimeFacade facade = boot.Driver.Facade;
                    facade.Atmosphere.SetWeather(RpgWorldMapId.WhitewaterCrossing, RpgWorld500xWeatherKind.Clear);
                    facade.Atmosphere.SetWeather(RpgWorldMapId.MineCamp, RpgWorld500xWeatherKind.Clear);
                    facade.Atmosphere.SetWeather(RpgWorldMapId.RedSaltHarbor, RpgWorld500xWeatherKind.GaleStorm);
                    facade.BountyAndSupply.AddSaltSilver(300);

                    Assert.IsTrue(readiness.Show(5f));
                    string breakdownBefore = readiness.FormatProvisioningBreakdownForDestination(
                        RpgWorldMapId.MineCamp, preferSafestRoute: true);
                    StringAssert.Contains("合计", breakdownBefore);

                    Assert.IsTrue(readiness.TryAutoProvisionForDestination(
                        RpgWorldMapId.MineCamp, allowPartialIfUnderBudget: false, preferSafestRoute: true));
                    Assert.IsTrue(readiness.LastProvisioningReceipt.FullyProvisioned);
                    Assert.Greater(readiness.LastProvisioningReceipt.TotalSpentSaltSilver, 0);
                    Assert.AreEqual(
                        "行囊已齐备（无需补购）",
                        readiness.FormatProvisioningBreakdownForDestination(
                            RpgWorldMapId.MineCamp, preferSafestRoute: true));

                    Assert.IsTrue(readiness.TryClickClose());
                    Assert.IsFalse(readiness.IsVisible);
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
