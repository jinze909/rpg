using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xMerchantHotbarBuffProvisioningE2ETests
    {
        [Test]
        public void CampMerchantPurchaseHotbarBuffProvisioningDiscountAndSaveReload_StayConsistentWithZeroLeak()
        {
            var uiRoot = new GameObject("World500xMerchantHotbarBuffUiRoot");
            // Warm up one-time UGUI/font caches before steady-state leak baseline.
            var warmupGo = new GameObject("World500xMerchantHotbarWarmupHost");
            var warmupCtrl = warmupGo.AddComponent<RpgWorld500xPlayModeController>();
            try
            {
                warmupCtrl.Initialize(
                    overrideMap: RpgWorldMapId.Soundwell,
                    saveStorage: new RpgWorld500xInMemorySaveSlotStorage(),
                    overrideTileSlices: new Sprite[] { null });
                using (var warmupHud = new RpgWorld500xFieldSupplyHotbarHud(uiRoot.transform, warmupCtrl.Bootstrapper))
                {
                    warmupHud.Refresh(0f);
                }

                warmupCtrl.MenuRouter.OpenModal(RpgWorld500xModalKind.CampMerchant);
                warmupCtrl.MenuRouter.OpenModal(RpgWorld500xModalKind.SaveSlots);
                warmupCtrl.MenuRouter.CloseAll();
            }
            finally
            {
                warmupCtrl.Shutdown();
                UnityEngine.Object.DestroyImmediate(warmupGo);
            }

            int texBefore = Resources.FindObjectsOfTypeAll<Texture2D>().Length;
            int spriteBefore = Resources.FindObjectsOfTypeAll<Sprite>().Length;

            var hostGo = new GameObject("World500xMerchantHotbarE2EHost");
            var ctrl = hostGo.AddComponent<RpgWorld500xPlayModeController>();
            try
            {
                var storage = new RpgWorld500xInMemorySaveSlotStorage();
                ctrl.Initialize(
                    overrideMap: RpgWorldMapId.Soundwell,
                    saveStorage: storage,
                    overrideTileSlices: new Sprite[] { null });

                var boot = ctrl.Bootstrapper;
                var facade = boot.Driver.Facade;
                facade.Atmosphere.SetWeather(RpgWorldMapId.Soundwell, RpgWorld500xWeatherKind.Clear);
                facade.Atmosphere.SetWeather(RpgWorldMapId.RedSaltHarbor, RpgWorld500xWeatherKind.Clear);
                facade.Atmosphere.SetWeather(RpgWorldMapId.MineCamp, RpgWorld500xWeatherKind.GaleStorm);

                var economy = facade.BountyAndSupply;
                economy.AddSaltSilver(200);

                // Buy 1 lamp_oil from CampMerchant and consume it so owned count reaches 0 while buff is active.
                Assert.IsTrue(ctrl.MenuRouter.OpenModal(RpgWorld500xModalKind.CampMerchant));
                Assert.IsTrue(ctrl.MenuRouter.MerchantPanel.TryClickBuySupply("lamp_oil"));
                Assert.AreEqual(1, economy.GetOwnedSupplyCount("lamp_oil"));
                ctrl.MenuRouter.CloseAll();

                using (var hud = new RpgWorld500xFieldSupplyHotbarHud(uiRoot.transform, boot))
                {
                    hud.Refresh(10f);
                    Assert.AreEqual(1, hud.TotalOwnedSupplyCount);

                    // Consume the lamp_oil -> owned drops to 0 and 90s buff activates.
                    Assert.IsTrue(hud.TryClickUseSupply("lamp_oil", 10f));
                    Assert.AreEqual(0, economy.GetOwnedSupplyCount("lamp_oil"));
                    Assert.AreEqual(0, hud.TotalOwnedSupplyCount);
                    Assert.AreEqual(90f, hud.SupplyUseService.GetRemainingBuffSeconds("lamp_oil", 10f), 0.01f);

                    // With 0 owned lamp_oil, basePlan requires buying lamp_oil (+22) while buffPlan discounts it (-22).
                    RpgWorld500xProvisioningPlan basePlan = RpgWorld500xCampProvisioningService.BuildProvisioningPlan(
                        boot, RpgWorldMapId.MineCamp, elapsedSeconds: 10f, supplyUseService: null);
                    RpgWorld500xProvisioningPlan buffPlan = RpgWorld500xCampProvisioningService.BuildProvisioningPlan(
                        boot, RpgWorldMapId.MineCamp, elapsedSeconds: 10f, supplyUseService: hud.SupplyUseService);
                    Assert.AreEqual(basePlan.TotalRequiredSaltSilver - 22, buffPlan.TotalRequiredSaltSilver);

                    // Buy another lamp_oil, save to Slot1, consume it, then reload Slot1 -> restores 1 lamp_oil.
                    Assert.IsTrue(ctrl.MenuRouter.OpenModal(RpgWorld500xModalKind.CampMerchant));
                    Assert.IsTrue(ctrl.MenuRouter.MerchantPanel.TryClickBuySupply("lamp_oil"));
                    Assert.AreEqual(1, economy.GetOwnedSupplyCount("lamp_oil"));

                    Assert.IsTrue(ctrl.MenuRouter.OpenModal(RpgWorld500xModalKind.SaveSlots));
                    Assert.IsTrue(ctrl.MenuRouter.SaveSlotPanel.TryClickSave(RpgWorld500xSaveSlotId.Slot1));
                    ctrl.MenuRouter.CloseAll();

                    hud.Refresh(120f);
                    Assert.IsTrue(hud.TryClickUseSupply("lamp_oil", 120f));
                    Assert.AreEqual(0, economy.GetOwnedSupplyCount("lamp_oil"));

                    Assert.IsTrue(ctrl.MenuRouter.OpenModal(RpgWorld500xModalKind.SaveSlots));
                    Assert.IsTrue(ctrl.MenuRouter.SaveSlotPanel.TryClickLoad(RpgWorld500xSaveSlotId.Slot1));
                    ctrl.MenuRouter.CloseAll();

                    hud.Refresh(125f);
                    Assert.AreEqual(1, boot.Driver.Facade.BountyAndSupply.GetOwnedSupplyCount("lamp_oil"));
                    Assert.AreEqual(1, hud.TotalOwnedSupplyCount);
                }

                Assert.AreEqual(0, uiRoot.transform.childCount);
            }
            finally
            {
                ctrl.Shutdown();
                UnityEngine.Object.DestroyImmediate(hostGo);
                UnityEngine.Object.DestroyImmediate(uiRoot);
            }

            Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Texture2D>().Length, texBefore);
            Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Sprite>().Length, spriteBefore);
        }
    }
}
