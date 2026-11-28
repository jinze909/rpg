using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionPlayModeSupplyModalGuardE2ETests
    {
        [Test]
        public void AllFourRegions_PlayModeControllerSupplyHotbarRespectsModalAndClosingFrame()
        {
            IReadOnlyList<RpgExpandedMapSpec> specs = RpgExpandedWorldScale.AllSpecs;
            Assert.AreEqual(4, specs.Count);

            foreach (RpgExpandedMapSpec spec in specs)
            {
                var host = new GameObject("FourRegionSupplyModalGuard_" + spec.MapId);
                var ctrl = host.AddComponent<RpgWorld500xPlayModeController>();
                try
                {
                    ctrl.Initialize(
                        overrideMap: spec.MapId,
                        saveStorage: new RpgWorld500xInMemorySaveSlotStorage(),
                        overrideTileSlices: new Sprite[] { null });

                    var shop = ctrl.Bootstrapper.Driver.Facade.BountyAndSupply;
                    shop.AddSaltSilver(200);
                    Assert.AreEqual(
                        RpgCampSupplyPurchaseStatus.Succeeded,
                        shop.TryPurchaseSupply("lamp_oil", 2),
                        spec.MapId.ToString());

                    var hotbar = ctrl.SupplyHotbarHud;
                    Assert.IsNotNull(hotbar, spec.MapId.ToString());
                    ctrl.ProcessInputFrame(new RpgWorld500xInputFrame(), 0.1f);

                    var button = host.transform
                        .Find("World500xPlayModeUiRoot/World500xFieldSupplyHotbarHud/SupplyHotbarPanel/SupplySlot_lamp_oil")
                        .GetComponent<Button>();
                    Assert.IsNotNull(button, spec.MapId.ToString());
                    Assert.IsTrue(button.IsInteractable(), spec.MapId.ToString());

                    // 1. Open modal locks supply hotbar mouse & public consumption on processed frame.
                    Assert.IsTrue(ctrl.MenuRouter.OpenModal(RpgWorld500xModalKind.WorldAtlas), spec.MapId.ToString());
                    ctrl.ProcessInputFrame(new RpgWorld500xInputFrame(), 0.1f);
                    Assert.IsFalse(button.IsInteractable(), spec.MapId.ToString());
                    button.onClick.Invoke();
                    Assert.IsFalse(hotbar.TryClickUseSupply("lamp_oil", 0.2f), spec.MapId.ToString());
                    Assert.AreEqual(2, shop.GetOwnedSupplyCount("lamp_oil"), spec.MapId.ToString());
                    Assert.AreEqual(0, hotbar.ActiveBuffCount, spec.MapId.ToString());

                    // 2. Closing frame keeps supply hotbar locked so Escape cannot leak a click on the same frame.
                    ctrl.ProcessInputFrame(new RpgWorld500xInputFrame { CloseModalPressed = true }, 0.1f);
                    Assert.AreEqual(RpgWorld500xModalKind.None, ctrl.MenuRouter.ActiveModal, spec.MapId.ToString());
                    Assert.IsFalse(button.IsInteractable(), spec.MapId.ToString());
                    button.onClick.Invoke();
                    Assert.IsFalse(hotbar.TryClickUseSupply("lamp_oil", 0.3f), spec.MapId.ToString());
                    Assert.AreEqual(2, shop.GetOwnedSupplyCount("lamp_oil"), spec.MapId.ToString());
                    Assert.AreEqual(0, hotbar.ActiveBuffCount, spec.MapId.ToString());

                    // 3. Next open-world frame restores hotbar interactivity and allows normal consumption.
                    ctrl.ProcessInputFrame(new RpgWorld500xInputFrame(), 0.1f);
                    Assert.IsTrue(button.IsInteractable(), spec.MapId.ToString());
                    button.onClick.Invoke();
                    Assert.AreEqual(1, shop.GetOwnedSupplyCount("lamp_oil"), spec.MapId.ToString());
                    Assert.AreEqual(1, hotbar.ActiveBuffCount, spec.MapId.ToString());

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
