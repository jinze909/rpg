using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFieldSupplyHotbarHudTests
    {
        [Test]
        public void FieldSupplyHotbarHud_RejectsLockedEventsAndReportsActualConsumption()
        {
            var root = new GameObject("SupplyHotbarActionGuardRoot");
            try
            {
                var store = new Dictionary<string, int>(StringComparer.Ordinal);
                Func<string, int> read = k => store.TryGetValue(k, out int v) ? v : 0;
                Action<string, int> write = (k, v) => store[k] = v;
                var shop = new RpgWorld500xFieldBountyAndSupplyService(read, write);
                shop.AddSaltSilver(100);
                Assert.AreEqual(RpgCampSupplyPurchaseStatus.Succeeded, shop.TryPurchaseSupply("lamp_oil", 2));
                var use = new RpgWorld500xFieldSupplyUseService(read, write);
                using (var boot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.Soundwell,
                    overrideTileSlices: new Sprite[] { null }))
                using (var hud = new RpgWorld500xFieldSupplyHotbarHud(root.transform, boot, use, () => 20f))
                {
                    hud.Refresh(10f);
                    var button = Array.Find(root.GetComponentsInChildren<UnityEngine.UI.Button>(true),
                        b => b.name == "SupplySlot_lamp_oil");
                    Assert.IsNotNull(button);
                    var group = root.AddComponent<CanvasGroup>();
                    group.interactable = false;
                    button.onClick.Invoke();
                    Assert.IsFalse(hud.TryClickUseSupply("lamp_oil", 10f));
                    Assert.AreEqual(2, use.GetOwnedCount("lamp_oil"));
                    group.interactable = true;
                    root.SetActive(false);
                    button.onClick.Invoke();
                    Assert.IsFalse(hud.TryClickUseSupply("lamp_oil", 10f));
                    Assert.AreEqual(2, use.GetOwnedCount("lamp_oil"));
                    root.SetActive(true);

                    // An inactive component can still have a retained UnityEvent reference.
                    button.enabled = false;
                    button.onClick.Invoke();
                    Assert.IsFalse(hud.TryClickUseSupply("lamp_oil", 10f));
                    Assert.AreEqual(2, use.GetOwnedCount("lamp_oil"));
                    button.enabled = true;

                    Assert.IsFalse(hud.TryClickUseSupply("lamp_oil", float.NaN));
                    Assert.AreEqual(2, use.GetOwnedCount("lamp_oil"));
                    Assert.IsTrue(hud.TryClickUseSupply("lamp_oil", 10f));
                    Assert.AreEqual(1, use.GetOwnedCount("lamp_oil"));
                    Assert.AreEqual(90f, use.GetRemainingBuffSeconds("lamp_oil", 10f), 0.01f);
                    button.onClick.Invoke();
                    Assert.AreEqual(0, use.GetOwnedCount("lamp_oil"));
                    Assert.AreEqual(170f, use.GetRemainingBuffSeconds("lamp_oil", 20f), 0.01f);
                    // Once the stock is exhausted, a retained event cannot extend the buff.
                    button.onClick.Invoke();
                    Assert.IsFalse(hud.TryClickUseSupply("lamp_oil", 20f));
                    Assert.AreEqual(0, use.GetOwnedCount("lamp_oil"));
                    Assert.AreEqual(170f, use.GetRemainingBuffSeconds("lamp_oil", 20f), 0.01f);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void FieldSupplyHotbarHud_DisabledCanvasRejectsConsumptionUntilRestored()
        {
            var root = new GameObject("SupplyHotbarDisabledCanvasRoot");
            try
            {
                using (var boot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.Soundwell,
                    overrideTileSlices: new Sprite[] { null }))
                using (var hud = new RpgWorld500xFieldSupplyHotbarHud(root.transform, boot, clockSecondsProvider: () => 20f))
                {
                    var shop = boot.Driver.Facade.BountyAndSupply;
                    shop.AddSaltSilver(200);
                    Assert.AreEqual(RpgCampSupplyPurchaseStatus.Succeeded, shop.TryPurchaseSupply("lamp_oil", 2));
                    hud.Refresh(10f);
                    var canvas = root.transform.Find("World500xFieldSupplyHotbarHud").GetComponent<Canvas>();
                    var button = canvas.transform.Find("SupplyHotbarPanel/SupplySlot_lamp_oil")
                        .GetComponent<UnityEngine.UI.Button>();
                    Assert.IsTrue(button.IsInteractable());
                    string feedback = hud.FeedbackText;
                    string buffs = hud.ActiveBuffsText;

                    canvas.enabled = false;
                    Assert.IsTrue(button.IsInteractable());
                    button.onClick.Invoke();
                    Assert.IsFalse(hud.TryClickUseSupply("lamp_oil", 10f));
                    Assert.AreEqual(2, shop.GetOwnedSupplyCount("lamp_oil"));
                    Assert.AreEqual(2, hud.TotalOwnedSupplyCount);
                    Assert.AreEqual(0, hud.ActiveBuffCount);
                    Assert.AreEqual(0f, hud.SupplyUseService.GetRemainingBuffSeconds("lamp_oil", 20f));
                    Assert.AreEqual(feedback, hud.FeedbackText);
                    Assert.AreEqual(buffs, hud.ActiveBuffsText);

                    canvas.enabled = true;
                    Assert.IsTrue(hud.TryClickUseSupply("lamp_oil", 10f));
                    Assert.AreEqual(1, shop.GetOwnedSupplyCount("lamp_oil"));
                    Assert.AreEqual(90f, hud.SupplyUseService.GetRemainingBuffSeconds("lamp_oil", 10f), 0.01f);
                    button.onClick.Invoke();
                    Assert.AreEqual(0, shop.GetOwnedSupplyCount("lamp_oil"));
                    Assert.AreEqual(170f, hud.SupplyUseService.GetRemainingBuffSeconds("lamp_oil", 20f), 0.01f);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DefaultHotbarConsumesLiveInventoryAndPersistsAcrossSaveReload()
        {
            var root = new GameObject("SupplyHotbarLiveInventoryRoot");
            try
            {
                using (var boot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.Soundwell,
                    overrideTileSlices: new Sprite[] { null }))
                using (var hud = new RpgWorld500xFieldSupplyHotbarHud(root.transform, boot))
                {
                    var shop = boot.Driver.Facade.BountyAndSupply;
                    shop.AddSaltSilver(200);
                    Assert.AreEqual(RpgCampSupplyPurchaseStatus.Succeeded,
                        shop.TryPurchaseSupply("lamp_oil", 2));
                    hud.Refresh(10f);
                    Assert.AreEqual(2, hud.TotalOwnedSupplyCount);
                    Assert.IsTrue(hud.TryClickUseSupply("lamp_oil", 10f));
                    Assert.AreEqual(1, shop.GetOwnedSupplyCount("lamp_oil"));
                    Assert.AreEqual(1, hud.TotalOwnedSupplyCount);
                    Assert.AreEqual(90f, hud.SupplyUseService.GetRemainingBuffSeconds("lamp_oil", 10f), 0.01f);
                    string saved = boot.ExportSaveJson();
                    using (var restored = new RpgWorld500xSceneBootstrapper(
                        initialMapId: RpgWorldMapId.Soundwell,
                        overrideTileSlices: new Sprite[] { null }))
                    {
                        Assert.IsTrue(restored.TryImportSaveJson(saved, out string error), error);
                        Assert.AreEqual(1, restored.Driver.Facade.BountyAndSupply.GetOwnedSupplyCount("lamp_oil"));
                    }
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void FieldSupplyHotbarHud_ConsumesOwnedSuppliesShowsBuffsRespectsParentLockAndZeroLeak()
        {
            var root = new GameObject("World500xFieldSupplyHotbarHudTestRoot");
            try
            {
                // Warm up one-time UGUI/font caches before steady-state leak baseline.
                using (var warmupBoot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.Soundwell,
                    overrideTileSlices: new Sprite[] { null }))
                using (var warmupHud = new RpgWorld500xFieldSupplyHotbarHud(root.transform, warmupBoot))
                {
                    warmupHud.Refresh(1f);
                }

                int texBefore = Resources.FindObjectsOfTypeAll<Texture2D>().Length;
                int spriteBefore = Resources.FindObjectsOfTypeAll<Sprite>().Length;

                var store = new Dictionary<string, int>(StringComparer.Ordinal);
                Func<string, int> read = k => store.TryGetValue(k, out int v) ? v : 0;
                Action<string, int> write = (k, v) => store[k] = v;

                var shop = new RpgWorld500xFieldBountyAndSupplyService(read, write);
                shop.AddSaltSilver(200);
                Assert.AreEqual(RpgCampSupplyPurchaseStatus.Succeeded, shop.TryPurchaseSupply("lamp_oil", 2));

                var useSvc = new RpgWorld500xFieldSupplyUseService(read, write);

                using (var boot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.Soundwell,
                    overrideTileSlices: new Sprite[] { null }))
                using (var hud = new RpgWorld500xFieldSupplyHotbarHud(root.transform, boot, useSvc))
                {
                    Assert.AreEqual(6, hud.DisplayedSlotCount);
                    Assert.AreEqual(6, hud.LastInventorySnapshot.Slots.Count);
                    Assert.AreEqual(2, hud.TotalOwnedSupplyCount);
                    Assert.AreEqual(1, hud.DistinctOwnedSupplyKinds);
                    Assert.AreEqual(0, hud.ActiveBuffCount);
                    Assert.IsTrue(hud.HasUsableSupply("lamp_oil"));
                    Assert.IsFalse(hud.HasUsableSupply("flare"));
                    StringAssert.Contains("【随身补给】", hud.InventorySummaryText);
                    Assert.IsFalse(hud.TryClickUseSupply("flare", 10f));

                    // Parent CanvasGroup lock blocks clicks cleanly.
                    CanvasGroup group = root.AddComponent<CanvasGroup>();
                    group.interactable = false;
                    Assert.IsFalse(hud.TryClickUseSupply("lamp_oil", 10f));
                    Assert.AreEqual(2, useSvc.GetOwnedCount("lamp_oil"));
                    group.interactable = true;

                    // Clicking owned lamp_oil consumes 1 and displays active buff.
                    Assert.IsTrue(hud.TryClickUseSupply("lamp_oil", 10f));
                    Assert.AreEqual(1, useSvc.GetOwnedCount("lamp_oil"));
                    Assert.AreEqual(1, hud.TotalOwnedSupplyCount);
                    Assert.AreEqual(1, hud.ActiveBuffCount);
                    Assert.IsTrue(hud.FeedbackText.Contains("矿灯灯油"));
                    Assert.IsTrue(hud.ActiveBuffsText.Contains("灯油照明"));

                    // After buff expires, active buffs readout clears.
                    hud.Refresh(120f);
                    Assert.AreEqual(0, hud.ActiveBuffCount);
                    Assert.IsTrue(hud.ActiveBuffsText.Contains("无"));
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
