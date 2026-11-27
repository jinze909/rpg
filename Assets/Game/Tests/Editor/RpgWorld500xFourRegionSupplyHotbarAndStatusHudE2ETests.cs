using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionSupplyHotbarAndStatusHudE2ETests
    {
        [Test]
        public void AllFourRegions_LiveSupplyHotbarStatusOverlayAndSaveJsonRestoreStayInSyncWithZeroLeak()
        {
            // Warm up one-time UGUI/font caches before steady-state leak baseline.
            var warmupRoot = new GameObject("World500xFourRegionHotbarStatusWarmupRoot");
            try
            {
                using (var warmupBoot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.Soundwell,
                    overrideTileSlices: new Sprite[] { null }))
                using (var warmupHotbar = new RpgWorld500xFieldSupplyHotbarHud(warmupRoot.transform, warmupBoot))
                using (var warmupStatus = new RpgWorld500xStatusOverlayHud(warmupRoot.transform))
                {
                    warmupHotbar.Refresh(1f);
                    warmupStatus.SyncFromDriver(warmupBoot.Driver);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(warmupRoot);
            }

            int texBefore = Resources.FindObjectsOfTypeAll<Texture2D>().Length;
            int spriteBefore = Resources.FindObjectsOfTypeAll<Sprite>().Length;

            RpgWorldMapId[] regions =
            {
                RpgWorldMapId.Soundwell,
                RpgWorldMapId.RedSaltHarbor,
                RpgWorldMapId.WhitewaterCrossing,
                RpgWorldMapId.MineCamp
            };

            var root = new GameObject("World500xFourRegionHotbarStatusE2ERoot");
            try
            {
                string savedJson;
                int expectedSavedSilver;

                using (var boot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: regions[0],
                    overrideTileSlices: new Sprite[] { null }))
                using (var hotbar = new RpgWorld500xFieldSupplyHotbarHud(root.transform, boot))
                using (var statusHud = new RpgWorld500xStatusOverlayHud(root.transform))
                {
                    var shop = boot.Driver.Facade.BountyAndSupply;
                    shop.AddSaltSilver(300);

                    for (int i = 0; i < regions.Length; i++)
                    {
                        if (i > 0)
                        {
                            Assert.IsTrue(boot.SwitchMap(regions[i]));
                        }

                        float now = 10f + (i * 120f);
                        Assert.AreEqual(RpgCampSupplyPurchaseStatus.Succeeded,
                            shop.TryPurchaseSupply("lamp_oil", 1));
                        hotbar.Refresh(now);
                        Assert.AreEqual(1, hotbar.TotalOwnedSupplyCount);

                        Assert.IsTrue(hotbar.TryClickUseSupply("lamp_oil", now));
                        Assert.AreEqual(0, shop.GetOwnedSupplyCount("lamp_oil"));
                        Assert.AreEqual(0, hotbar.TotalOwnedSupplyCount);
                        Assert.AreEqual(90f, hotbar.SupplyUseService.GetRemainingBuffSeconds("lamp_oil", now), 0.01f);

                        boot.Driver.TickFrame(Vector2.right, 0.2f);
                        Assert.IsTrue(statusHud.SyncFromDriver(boot.Driver));
                        Assert.IsFalse(string.IsNullOrEmpty(statusHud.EnvironmentReadout));
                        StringAssert.Contains("盐银 " + shop.SaltSilverBalance, statusHud.WaypointAndEconomyReadout);
                        StringAssert.Contains("探索度", statusHud.WaypointAndEconomyReadout);
                    }

                    // Buy 1 lamp_oil, snapshot save JSON, buy 1 more, then restore into a fresh bootstrapper.
                    Assert.AreEqual(RpgCampSupplyPurchaseStatus.Succeeded,
                        shop.TryPurchaseSupply("lamp_oil", 1));
                    expectedSavedSilver = shop.SaltSilverBalance;
                    savedJson = boot.ExportSaveJson();
                    Assert.IsFalse(string.IsNullOrEmpty(savedJson));

                    Assert.AreEqual(RpgCampSupplyPurchaseStatus.Succeeded,
                        shop.TryPurchaseSupply("lamp_oil", 1));
                    Assert.AreEqual(2, shop.GetOwnedSupplyCount("lamp_oil"));
                }

                using (var restored = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.Soundwell,
                    overrideTileSlices: new Sprite[] { null }))
                using (var restoredHotbar = new RpgWorld500xFieldSupplyHotbarHud(root.transform, restored))
                using (var restoredStatus = new RpgWorld500xStatusOverlayHud(root.transform))
                {
                    Assert.IsTrue(restored.TryImportSaveJson(savedJson, out string error), error);
                    Assert.AreEqual(RpgWorldMapId.MineCamp, restored.Driver.Facade.CurrentMapId);
                    Assert.AreEqual(1, restored.Driver.Facade.BountyAndSupply.GetOwnedSupplyCount("lamp_oil"));
                    Assert.AreEqual(expectedSavedSilver, restored.Driver.Facade.BountyAndSupply.SaltSilverBalance);

                    restoredHotbar.Refresh(500f);
                    Assert.AreEqual(1, restoredHotbar.TotalOwnedSupplyCount);
                    Assert.IsTrue(restoredStatus.SyncFromDriver(restored.Driver));
                    StringAssert.Contains("盐银 " + expectedSavedSilver, restoredStatus.WaypointAndEconomyReadout);
                }

                Assert.AreEqual(0, root.transform.childCount);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }

            Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Texture2D>().Length, texBefore);
            Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Sprite>().Length, spriteBefore);
        }
    }
}
