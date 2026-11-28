using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionCampMerchantCanvasGuardE2ETests
    {
        [Test]
        public void AllFourRegions_CampMerchantRejectsDisabledCanvasAndLockedCanvasGroupInput()
        {
            var specs = RpgExpandedWorldScale.AllSpecs;
            Assert.AreEqual(4, specs.Count);

            foreach (var spec in specs)
            {
                var root = new GameObject("CampMerchantCanvasGuard_" + spec.MapId);
                RpgWorld500xPlayModeController ctrl = null;
                try
                {
                    ctrl = root.AddComponent<RpgWorld500xPlayModeController>();
                    ctrl.Initialize(
                        overrideMap: spec.MapId,
                        saveStorage: new RpgWorld500xInMemorySaveSlotStorage(),
                        overrideTileSlices: new Sprite[] { null });

                    var supply = ctrl.Bootstrapper.Driver.Facade.BountyAndSupply;
                    supply.AddSaltSilver(100);
                    int initialSilver = supply.SaltSilverBalance;
                    int initialFlares = supply.GetOwnedSupplyCount("flare");

                    ctrl.MenuRouter.OpenModal(RpgWorld500xModalKind.CampMerchant);
                    var panel = ctrl.MenuRouter.MerchantPanel;
                    Assert.IsNotNull(panel, spec.MapId.ToString());
                    Assert.IsTrue(panel.IsVisible, spec.MapId.ToString());
                    Assert.AreEqual(RpgCampMerchantTab.Supplies, panel.CurrentTab, spec.MapId.ToString());

                    var modalBox = root.transform.Find(
                        "World500xPlayModeUiRoot/World500xCampMerchantHudPanel/MerchantModalBox");
                    Assert.IsNotNull(modalBox, spec.MapId.ToString());

                    var canvas = modalBox.GetComponentInParent<Canvas>();
                    Assert.IsNotNull(canvas, spec.MapId.ToString());

                    var buyFlareBtn = modalBox.Find("MerchantRows/SupplyRow_flare/ActionButton")
                        ?.GetComponent<Button>();
                    var bountiesTabBtn = modalBox.Find("BountiesTabButton")
                        ?.GetComponent<Button>();
                    Assert.IsNotNull(buyFlareBtn, spec.MapId.ToString());
                    Assert.IsNotNull(bountiesTabBtn, spec.MapId.ToString());

                    canvas.enabled = false;
                    AssertBlockedOnSuppliesTab(panel, supply, buyFlareBtn, bountiesTabBtn, initialSilver, initialFlares);
                    canvas.enabled = true;

                    var group = root.AddComponent<CanvasGroup>();
                    group.interactable = false;
                    AssertBlockedOnSuppliesTab(panel, supply, buyFlareBtn, bountiesTabBtn, initialSilver, initialFlares);
                    group.interactable = true;

                    Assert.IsTrue(panel.TryClickBuySupply("flare"), spec.MapId.ToString());
                    Assert.AreEqual(initialFlares + 1, supply.GetOwnedSupplyCount("flare"), spec.MapId.ToString());
                    Assert.Less(supply.SaltSilverBalance, initialSilver, spec.MapId.ToString());
                    int postPurchaseSilver = supply.SaltSilverBalance;

                    bountiesTabBtn.onClick.Invoke();
                    Assert.AreEqual(RpgCampMerchantTab.Bounties, panel.CurrentTab, spec.MapId.ToString());

                    var claimBtn = modalBox.Find("MerchantRows/ClaimAllBountiesRow/ActionButton")
                        ?.GetComponent<Button>();
                    canvas.enabled = false;
                    Assert.IsFalse(panel.TryClickClaimBounties(), spec.MapId.ToString());
                    if (claimBtn != null)
                        claimBtn.onClick.Invoke();
                    Assert.AreEqual(postPurchaseSilver, supply.SaltSilverBalance, spec.MapId.ToString());
                    canvas.enabled = true;
                }
                finally
                {
                    if (ctrl != null)
                        ctrl.Shutdown();
                    Object.DestroyImmediate(root);
                }
            }
        }

        private static void AssertBlockedOnSuppliesTab(
            RpgWorld500xCampMerchantHudPanel panel,
            RpgWorld500xBountyAndSupplyService supply,
            Button buyFlareBtn,
            Button bountiesTabBtn,
            int expectedSilver,
            int expectedFlares)
        {
            Assert.IsFalse(panel.TryClickBuySupply("flare"));
            Assert.IsFalse(panel.TryClickClaimBounties());
            buyFlareBtn.onClick.Invoke();
            bountiesTabBtn.onClick.Invoke();

            Assert.IsTrue(panel.IsVisible);
            Assert.AreEqual(RpgCampMerchantTab.Supplies, panel.CurrentTab);
            Assert.AreEqual(expectedSilver, supply.SaltSilverBalance);
            Assert.AreEqual(expectedFlares, supply.GetOwnedSupplyCount("flare"));
        }
    }
}
