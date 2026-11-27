using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionFieldSupplyHotbarHudE2ETests
    {
        [Test]
        public void FourRegion_FieldSupplyHotbarHud_ConsumesLiveInventoryPersistsAcrossSaveAndExpiresBuffWithZeroSpriteLeak()
        {
            var root = new GameObject("World500xFourRegionSupplyHotbarRoot");
            try
            {
                IReadOnlyList<RpgExpandedMapSpec> specs = RpgExpandedWorldScale.AllSpecs;
                Assert.AreEqual(4, specs.Count);

                // Warm up one-time UGUI/font and per-region caches before steady-state leak baseline.
                for (int w = 0; w < specs.Count; w++)
                {
                    using (var warmupBoot = new RpgWorld500xSceneBootstrapper(
                        initialMapId: specs[w].MapId,
                        overrideTileSlices: new Sprite[] { null }))
                    using (var warmupHud = new RpgWorld500xFieldSupplyHotbarHud(root.transform, warmupBoot))
                    {
                        warmupHud.Refresh(0f);
                    }
                }

                int spriteBefore = Resources.FindObjectsOfTypeAll<Sprite>().Length;

                for (int i = 0; i < specs.Count; i++)
                {
                    RpgExpandedMapSpec spec = specs[i];
                    var regionRoot = new GameObject("RegionSupplyHotbar_" + spec.MapId);
                    regionRoot.transform.SetParent(root.transform, false);
                    try
                    {
                        using (var boot = new RpgWorld500xSceneBootstrapper(
                            initialMapId: spec.MapId,
                            overrideTileSlices: new Sprite[] { null }))
                        using (var hud = new RpgWorld500xFieldSupplyHotbarHud(regionRoot.transform, boot))
                        {
                            var shop = boot.Driver.Facade.BountyAndSupply;
                            shop.AddSaltSilver(200);
                            Assert.AreEqual(RpgCampSupplyPurchaseStatus.Succeeded,
                                shop.TryPurchaseSupply("lamp_oil", 2));

                            hud.Refresh(10f);
                            Assert.AreEqual(6, hud.DisplayedSlotCount);
                            Assert.AreEqual(6, hud.LastInventorySnapshot.Slots.Count);
                            Assert.AreEqual(2, hud.TotalOwnedSupplyCount);
                            Assert.AreEqual(1, hud.DistinctOwnedSupplyKinds);
                            Assert.AreEqual(0, hud.ActiveBuffCount);
                            Assert.IsTrue(hud.HasUsableSupply("lamp_oil"));
                            Assert.IsFalse(hud.HasUsableSupply("flare"));
                            Assert.IsFalse(hud.TryClickUseSupply("flare", 10f));
                            StringAssert.Contains("【随身补给】", hud.InventorySummaryText);

                            // Consume 1 lamp_oil and verify live inventory, active buff, and save persistence.
                            Assert.IsTrue(hud.TryClickUseSupply("lamp_oil", 10f));
                            Assert.AreEqual(1, shop.GetOwnedSupplyCount("lamp_oil"));
                            Assert.AreEqual(1, hud.TotalOwnedSupplyCount);
                            Assert.AreEqual(1, hud.ActiveBuffCount);
                            StringAssert.Contains("矿灯灯油", hud.FeedbackText);
                            StringAssert.Contains("灯油照明", hud.ActiveBuffsText);
                            Assert.AreEqual(90f, hud.SupplyUseService.GetRemainingBuffSeconds("lamp_oil", 10f), 0.01f);

                            string saved = boot.ExportSaveJson();
                            using (var restored = new RpgWorld500xSceneBootstrapper(
                                initialMapId: spec.MapId,
                                overrideTileSlices: new Sprite[] { null }))
                            {
                                Assert.IsTrue(restored.TryImportSaveJson(saved, out string error), error);
                                Assert.AreEqual(spec.MapId, restored.Driver.Facade.CurrentMapId);
                                Assert.AreEqual(1, restored.Driver.Facade.BountyAndSupply.GetOwnedSupplyCount("lamp_oil"));
                            }

                            // Buff expires cleanly after duration elapses.
                            hud.Refresh(120f);
                            Assert.AreEqual(0, hud.ActiveBuffCount);
                            StringAssert.Contains("无", hud.ActiveBuffsText);
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
        public void FourRegion_FieldSupplyHotbarHud_RespectsAncestorCanvasGroupLockAndPostDisposeGuard()
        {
            var root = new GameObject("World500xFourRegionSupplyHotbarGuardRoot");
            RpgWorld500xFieldSupplyHotbarHud disposedHud = null;
            try
            {
                using (var boot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.MineCamp,
                    overrideTileSlices: new Sprite[] { null }))
                using (var hud = new RpgWorld500xFieldSupplyHotbarHud(root.transform, boot))
                {
                    disposedHud = hud;
                    var shop = boot.Driver.Facade.BountyAndSupply;
                    shop.AddSaltSilver(120);
                    Assert.AreEqual(RpgCampSupplyPurchaseStatus.Succeeded,
                        shop.TryPurchaseSupply("lamp_oil", 1));
                    hud.Refresh(10f);

                    CanvasGroup group = root.AddComponent<CanvasGroup>();
                    group.interactable = false;
                    Assert.IsFalse(hud.TryClickUseSupply("lamp_oil", 10f));
                    Assert.AreEqual(1, shop.GetOwnedSupplyCount("lamp_oil"));
                    Assert.AreEqual(0, hud.ActiveBuffCount);

                    group.interactable = true;
                    Assert.IsTrue(hud.TryClickUseSupply("lamp_oil", 10f));
                    Assert.AreEqual(0, shop.GetOwnedSupplyCount("lamp_oil"));
                    Assert.IsFalse(hud.HasUsableSupply("lamp_oil"));
                    Assert.AreEqual(1, hud.ActiveBuffCount);
                }

                Assert.IsNotNull(disposedHud);
                Assert.IsTrue(disposedHud.IsDisposed);
                Assert.IsFalse(disposedHud.HasUsableSupply("lamp_oil"));
                Assert.IsFalse(disposedHud.TryClickUseSupply("lamp_oil", 20f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
