using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionPlayModeSupplyLoopE2ETests
    {
        [Test]
        public void AllFourRegions_PlayModeControllerMerchantBuyHotbarUseAndSlotReloadStayConsistent()
        {
            IReadOnlyList<RpgExpandedMapSpec> specs = RpgExpandedWorldScale.AllSpecs;
            Assert.AreEqual(4, specs.Count);

            foreach (RpgExpandedMapSpec spec in specs)
            {
                var host = new GameObject("FourRegionPlayModeSupplyLoop_" + spec.MapId);
                var ctrl = host.AddComponent<RpgWorld500xPlayModeController>();
                try
                {
                    ctrl.Initialize(
                        overrideMap: spec.MapId,
                        saveStorage: new RpgWorld500xInMemorySaveSlotStorage(),
                        overrideTileSlices: new Sprite[] { null });

                    var shop = ctrl.Bootstrapper.Driver.Facade.BountyAndSupply;
                    shop.AddSaltSilver(200);
                    int initialSilver = shop.SaltSilverBalance;

                    // 1. Purchase 2 lamp_oil through live CampMerchant panel.
                    Assert.IsTrue(ctrl.MenuRouter.OpenModal(RpgWorld500xModalKind.CampMerchant), spec.MapId.ToString());
                    var merchant = ctrl.MenuRouter.MerchantPanel;
                    Assert.IsNotNull(merchant, spec.MapId.ToString());
                    Assert.IsTrue(merchant.TryClickBuySupply("lamp_oil"), spec.MapId.ToString());
                    Assert.IsTrue(merchant.TryClickBuySupply("lamp_oil"), spec.MapId.ToString());
                    Assert.AreEqual(2, shop.GetOwnedSupplyCount("lamp_oil"), spec.MapId.ToString());
                    Assert.Less(shop.SaltSilverBalance, initialSilver, spec.MapId.ToString());
                    int silverAfterBuy = shop.SaltSilverBalance;

                    // 2. Close modal, advance past closing frame, and consume 1 lamp_oil via hotbar.
                    ctrl.ProcessInputFrame(new RpgWorld500xInputFrame { CloseModalPressed = true }, 0.1f);
                    Assert.AreEqual(RpgWorld500xModalKind.None, ctrl.MenuRouter.ActiveModal, spec.MapId.ToString());
                    ctrl.ProcessInputFrame(new RpgWorld500xInputFrame(), 0.1f);

                    Assert.IsTrue(ctrl.SupplyHotbarHud.TryClickUseSupply("lamp_oil", 10f), spec.MapId.ToString());
                    Assert.AreEqual(1, shop.GetOwnedSupplyCount("lamp_oil"), spec.MapId.ToString());
                    Assert.AreEqual(1, ctrl.SupplyHotbarHud.ActiveBuffCount, spec.MapId.ToString());

                    // 3. Save to Slot1, mutate live wallet/stock, then reload Slot1 to verify exact restore.
                    Assert.IsTrue(
                        ctrl.SaveSlotManager.TrySaveSlot(ctrl.Bootstrapper, RpgWorld500xSaveSlotId.Slot1, out _, out string saveErr),
                        saveErr);

                    shop.AddSaltSilver(500);
                    Assert.IsTrue(ctrl.SupplyHotbarHud.TryClickUseSupply("lamp_oil", 15f), spec.MapId.ToString());
                    Assert.AreEqual(0, shop.GetOwnedSupplyCount("lamp_oil"), spec.MapId.ToString());

                    Assert.IsTrue(
                        ctrl.SaveSlotManager.TryLoadSlot(ctrl.Bootstrapper, RpgWorld500xSaveSlotId.Slot1, out _, out string loadErr),
                        loadErr);
                    ctrl.ProcessInputFrame(new RpgWorld500xInputFrame(), 0.1f);

                    Assert.AreEqual(spec.MapId, ctrl.Bootstrapper.Driver.Facade.CurrentMapId, spec.MapId.ToString());
                    Assert.AreEqual(silverAfterBuy, shop.SaltSilverBalance, spec.MapId.ToString());
                    Assert.AreEqual(1, shop.GetOwnedSupplyCount("lamp_oil"), spec.MapId.ToString());

                    ctrl.Shutdown();
                    Assert.AreEqual(0, host.transform.childCount, spec.MapId.ToString());
                }
                finally
                {
                    ctrl.Shutdown();
                    Object.DestroyImmediate(host);
                }
            }
        }
    }
}
