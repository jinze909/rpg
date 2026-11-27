using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xCampMerchantHudPanelTests
    {
        [Test]
        public void CampMerchantPanel_RejectsStaleRegionalAndReputationQuotesBeforeCharging()
        {
            var root = new GameObject("World500xMerchantQuoteRoot");
            var ctrl = root.AddComponent<RpgWorld500xPlayModeController>();
            try
            {
                ctrl.Initialize(
                    overrideMap: RpgWorldMapId.Soundwell,
                    saveStorage: new RpgWorld500xInMemorySaveSlotStorage(),
                    overrideTileSlices: new Sprite[] { null });
                var boot = ctrl.Bootstrapper;
                var haven = ctrl.CampHaven;
                boot.Driver.Facade.BountyAndSupply.AddSaltSilver(100);
                Assert.IsTrue(ctrl.MenuRouter.OpenModal(RpgWorld500xModalKind.CampMerchant));
                var panel = ctrl.MenuRouter.MerchantPanel;
                const string path = "World500xPlayModeUiRoot/World500xCampMerchantHudPanel/MerchantModalBox/MerchantRows/SupplyRow_flare/ActionButton";
                Assert.AreEqual(30, haven.GetDiscountedUnitCost(RpgWorldMapId.Soundwell, "flare"));
                var staleReputationEvent = root.transform.Find(path).GetComponent<UnityEngine.UI.Button>().onClick;
                haven.Reputation.AddPoints(RpgWorldMapId.Soundwell, 800);
                staleReputationEvent.Invoke();
                Assert.AreEqual(100, boot.Driver.Facade.BountyAndSupply.SaltSilverBalance);
                Assert.AreEqual(0, boot.Driver.Facade.BountyAndSupply.GetOwnedSupplyCount("flare"));
                Assert.That(panel.FeedbackText, Does.Contain("报价已更新"));
                Assert.That(panel.HeaderText, Does.Contain("折扣 30%"));
                Assert.IsTrue(panel.TryClickBuySupply("flare"));
                Assert.AreEqual(79, boot.Driver.Facade.BountyAndSupply.SaltSilverBalance);
                Assert.AreEqual(1, boot.Driver.Facade.BountyAndSupply.GetOwnedSupplyCount("flare"));

                var staleRegionEvent = root.transform.Find(path).GetComponent<UnityEngine.UI.Button>().onClick;
                Assert.IsTrue(boot.SwitchMap(RpgWorldMapId.RedSaltHarbor));
                Assert.AreEqual(30, haven.GetDiscountedUnitCost(RpgWorldMapId.RedSaltHarbor, "flare"));
                staleRegionEvent.Invoke();
                Assert.AreEqual(79, boot.Driver.Facade.BountyAndSupply.SaltSilverBalance);
                Assert.AreEqual(1, boot.Driver.Facade.BountyAndSupply.GetOwnedSupplyCount("flare"));
                Assert.That(panel.FeedbackText, Does.Contain("报价已更新"));
                Assert.IsTrue(panel.TryClickBuySupply("flare"));
                Assert.AreEqual(49, boot.Driver.Facade.BountyAndSupply.SaltSilverBalance);
                Assert.AreEqual(2, boot.Driver.Facade.BountyAndSupply.GetOwnedSupplyCount("flare"));
            }
            finally
            {
                ctrl.Shutdown();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void CampMerchantPanel_RejectsHiddenRetiredAndDisposedButtonEvents()
        {
            var root = new GameObject("World500xMerchantEventLifetimeRoot");
            try
            {
                var store = new Dictionary<string, int>(StringComparer.Ordinal);
                var haven = new RpgWorld500xCampHavenService(
                    k => store.TryGetValue(k, out int v) ? v : 0,
                    (k, v) => store[k] = v);
                string wallet = RpgWorld500xFieldBountyAndSupplyService.WalletSaltSilverKey;
                const string owned = "rpg.return.world.supply.item.flare";
                store[wallet] = 100;
                using (var boot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.Soundwell,
                    overrideTileSlices: new Sprite[] { null }))
                using (var panel = new RpgWorld500xCampMerchantHudPanel(root.transform, boot, haven))
                {
                    boot.Driver.Facade.BountyAndSupply.AddSaltSilver(100);
                    const string path = "World500xCampMerchantHudPanel/MerchantModalBox/MerchantRows/SupplyRow_flare/ActionButton";
                    Assert.IsFalse(panel.TryClickBuySupply("flare"));
                    Assert.IsTrue(panel.Show(RpgCampMerchantTab.Supplies));
                    var first = root.transform.Find(path).GetComponent<UnityEngine.UI.Button>();
                    var firstEvent = first.onClick;

                    panel.Hide();
                    firstEvent.Invoke();
                    Assert.IsFalse(panel.TryClickBuySupply("flare"));
                    Assert.AreEqual(100, store[wallet]);
                    Assert.IsFalse(store.ContainsKey(owned));

                    panel.Show(RpgCampMerchantTab.Supplies);
                    var current = root.transform.Find(path).GetComponent<UnityEngine.UI.Button>();
                    var currentEvent = current.onClick;
                    firstEvent.Invoke();
                    Assert.AreEqual(100, store[wallet]);
                    current.interactable = false;
                    currentEvent.Invoke();
                    Assert.IsFalse(panel.TryClickBuySupply("flare"));
                    Assert.AreEqual(100, store[wallet]);
                    current.interactable = true;

                    current.enabled = false;
                    currentEvent.Invoke();
                    Assert.IsFalse(panel.TryClickBuySupply("flare"));
                    Assert.AreEqual(100, store[wallet]);
                    current.enabled = true;

                    root.SetActive(false);
                    currentEvent.Invoke();
                    Assert.IsFalse(panel.TryClickBuySupply("flare"));
                    Assert.AreEqual(100, store[wallet]);
                    Assert.IsFalse(store.ContainsKey(owned));
                    root.SetActive(true);

                    Assert.IsTrue(panel.TryClickBuySupply("flare"));
                    Assert.AreEqual(70, store[wallet]);
                    Assert.AreEqual(1, store[owned]);

                    // Successful purchase rebuilds rows; old click cannot charge twice.
                    currentEvent.Invoke();
                    Assert.AreEqual(70, store[wallet]);
                    var lastEvent = root.transform.Find(path).GetComponent<UnityEngine.UI.Button>().onClick;
                    panel.SetTab(RpgCampMerchantTab.Bounties);
                    lastEvent.Invoke();
                    Assert.AreEqual(70, store[wallet]);
                    Assert.AreEqual(1, store[owned]);

                    panel.SetTab(RpgCampMerchantTab.Supplies);
                    var disposedEvent = root.transform.Find(path).GetComponent<UnityEngine.UI.Button>().onClick;
                    panel.Dispose();
                    disposedEvent.Invoke();
                    Assert.AreEqual(70, store[wallet]);
                    Assert.AreEqual(1, store[owned]);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void CampMerchantPanel_DisabledCanvasRejectsInvisiblePurchaseAndTabEvents()
        {
            var root = new GameObject("World500xMerchantDisabledCanvas");
            var ctrl = root.AddComponent<RpgWorld500xPlayModeController>();
            try
            {
                ctrl.Initialize(
                    overrideMap: RpgWorldMapId.Soundwell,
                    saveStorage: new RpgWorld500xInMemorySaveSlotStorage(),
                    overrideTileSlices: new Sprite[] { null });
                var supply = ctrl.Bootstrapper.Driver.Facade.BountyAndSupply;
                supply.AddSaltSilver(100);
                Assert.IsTrue(ctrl.MenuRouter.OpenModal(RpgWorld500xModalKind.CampMerchant));
                var panel = ctrl.MenuRouter.MerchantPanel;
                var modal = root.transform.Find(
                    "World500xPlayModeUiRoot/World500xCampMerchantHudPanel/MerchantModalBox");
                var canvas = modal.GetComponentInParent<Canvas>();
                var buy = modal.Find("MerchantRows/SupplyRow_flare/ActionButton")
                    .GetComponent<UnityEngine.UI.Button>();
                var bounties = modal.Find("BountiesTabButton")
                    .GetComponent<UnityEngine.UI.Button>();
                Assert.IsTrue(buy.IsInteractable());

                canvas.enabled = false;
                buy.onClick.Invoke();
                bounties.onClick.Invoke();
                Assert.IsFalse(panel.TryClickBuySupply("flare"));
                Assert.AreEqual(RpgCampMerchantTab.Supplies, panel.CurrentTab);
                Assert.AreEqual(100, supply.SaltSilverBalance);
                Assert.AreEqual(0, supply.GetOwnedSupplyCount("flare"));

                canvas.enabled = true;
                Assert.IsTrue(panel.TryClickBuySupply("flare"));
                Assert.AreEqual(70, supply.SaltSilverBalance);
                Assert.AreEqual(1, supply.GetOwnedSupplyCount("flare"));
                bounties.onClick.Invoke();
                Assert.AreEqual(RpgCampMerchantTab.Bounties, panel.CurrentTab);
                var claim = modal.Find("MerchantRows/ClaimAllBountiesRow/ActionButton")
                    .GetComponent<UnityEngine.UI.Button>();
                claim.interactable = true;
                canvas.enabled = false;
                Assert.IsFalse(panel.TryClickClaimBounties());
                claim.onClick.Invoke();
                Assert.AreEqual(70, supply.SaltSilverBalance);
            }
            finally
            {
                ctrl.Shutdown();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void CampMerchantPanel_BuysDiscountedSuppliesSwitchesTabsAndDisposesWithZeroLeak()
        {
            var root = new GameObject("World500xCampMerchantHudPanelTestRoot");
            try
            {
                // Warm up one-time UGUI/font caches before steady-state leak baseline.
                using (var warmupBoot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.Soundwell,
                    overrideTileSlices: new Sprite[] { null }))
                {
                    var warmupStore = new Dictionary<string, int>(StringComparer.Ordinal);
                    var warmupHaven = new RpgWorld500xCampHavenService(
                        k => warmupStore.TryGetValue(k, out int v) ? v : 0,
                        (k, v) => warmupStore[k] = v);
                    using (var warmupPanel = new RpgWorld500xCampMerchantHudPanel(root.transform, warmupBoot, warmupHaven))
                    {
                        warmupPanel.Show(RpgCampMerchantTab.Supplies);
                        warmupPanel.SetTab(RpgCampMerchantTab.Bounties);
                    }
                }

                int texBefore = Resources.FindObjectsOfTypeAll<Texture2D>().Length;
                int spriteBefore = Resources.FindObjectsOfTypeAll<Sprite>().Length;

                var sharedStore = new Dictionary<string, int>(StringComparer.Ordinal);
                var haven = new RpgWorld500xCampHavenService(
                    k => sharedStore.TryGetValue(k, out int v) ? v : 0,
                    (k, v) => sharedStore[k] = v);
                haven.Reputation.AddPoints(RpgWorldMapId.Soundwell, 800); // Sworn: 30% discount
                sharedStore[RpgWorld500xFieldBountyAndSupplyService.WalletSaltSilverKey] = 40;

                using (var boot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.Soundwell,
                    overrideTileSlices: new Sprite[] { null }))
                using (var panel = new RpgWorld500xCampMerchantHudPanel(root.transform, boot, haven))
                {
                    // Fund bootstrapper wallet as well so header & button interactability reflect 40 salt silver.
                    boot.Driver.Facade.BountyAndSupply.AddSaltSilver(40);
                    Assert.IsTrue(panel.Show(RpgCampMerchantTab.Supplies));
                    Assert.AreEqual(6, panel.DisplayedRowCount);
                    Assert.IsTrue(panel.HeaderText.Contains("折扣 30%"));

                    // Buy flare (base 30 -> discounted 21)
                    Assert.IsTrue(panel.TryClickBuySupply("flare"));
                    Assert.IsTrue(panel.FeedbackText.Contains("消耗盐银 21"));
                    Assert.AreEqual(19, sharedStore[RpgWorld500xFieldBountyAndSupplyService.WalletSaltSilverKey]);

                    // Switch to Bounties tab (3 bounties + 1 claim row = 4 rows)
                    Assert.IsTrue(panel.SetTab(RpgCampMerchantTab.Bounties));
                    Assert.AreEqual(4, panel.DisplayedRowCount);
                    Assert.IsFalse(panel.TryClickClaimBounties());
                    var claimButton = root.transform.Find(
                        "World500xCampMerchantHudPanel/MerchantModalBox/MerchantRows/ClaimAllBountiesRow/ActionButton")
                        .GetComponent<UnityEngine.UI.Button>();
                    // A disabled or inactive control is not an accepted action,
                    // even if its interactable flag is stale.
                    claimButton.interactable = true;
                    claimButton.enabled = false;
                    Assert.IsFalse(panel.TryClickClaimBounties());
                    claimButton.enabled = true;
                    root.SetActive(false);
                    Assert.IsFalse(panel.TryClickClaimBounties());
                    root.SetActive(true);

                    Assert.IsTrue(panel.Hide());
                    Assert.IsFalse(panel.IsVisible);
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
