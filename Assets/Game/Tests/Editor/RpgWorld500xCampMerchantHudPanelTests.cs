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
                    Assert.AreEqual(100, store[wallet]);
                    current.interactable = true;
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
