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
                    Assert.IsTrue(hud.FeedbackText.Contains("矿灯灯油"));
                    Assert.IsTrue(hud.ActiveBuffsText.Contains("灯油照明"));

                    // After buff expires, active buffs readout clears.
                    hud.Refresh(120f);
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
