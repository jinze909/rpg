using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionCampHavenAndReputationE2ETests
    {
        [Test]
        public void PlayModeController_FourRegionReputationDiscountsPurchasesAndSaveReloadStayConsistentWithZeroLeak()
        {
            // Warm up one-time UGUI/font caches before steady-state leak baseline.
            var warmupGo = new GameObject("World500xFourRegionCampRepWarmupHost");
            var warmupCtrl = warmupGo.AddComponent<RpgWorld500xPlayModeController>();
            try
            {
                warmupCtrl.Initialize(
                    overrideMap: RpgWorldMapId.Soundwell,
                    saveStorage: new RpgWorld500xInMemorySaveSlotStorage(),
                    overrideTileSlices: new Sprite[] { null });
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

            RpgWorldMapId[] regions =
            {
                RpgWorldMapId.Soundwell,
                RpgWorldMapId.RedSaltHarbor,
                RpgWorldMapId.WhitewaterCrossing,
                RpgWorldMapId.MineCamp
            };

            var hostGo = new GameObject("World500xFourRegionCampRepE2EHost");
            var ctrl = hostGo.AddComponent<RpgWorld500xPlayModeController>();
            try
            {
                var storage = new RpgWorld500xInMemorySaveSlotStorage();
                ctrl.Initialize(
                    overrideMap: regions[0],
                    saveStorage: storage,
                    overrideTileSlices: new Sprite[] { null });

                var boot = ctrl.Bootstrapper;
                var economy = boot.Driver.Facade.BountyAndSupply;
                economy.AddSaltSilver(200);

                // In each of the 4 regions: verify full price (30), grant Sworn standing (800 -> 21), and buy 1 flare.
                int expectedSilver = 200;
                for (int i = 0; i < regions.Length; i++)
                {
                    RpgWorldMapId mapId = regions[i];
                    if (i > 0)
                    {
                        Assert.IsTrue(boot.SwitchMap(mapId));
                    }

                    Assert.AreEqual(30, ctrl.CampHaven.GetDiscountedUnitCost(mapId, "flare"));
                    ctrl.CampHaven.Reputation.AddPoints(mapId, 800);
                    Assert.AreEqual(21, ctrl.CampHaven.GetDiscountedUnitCost(mapId, "flare"));

                    Assert.IsTrue(ctrl.MenuRouter.OpenModal(RpgWorld500xModalKind.CampMerchant));
                    StringAssert.Contains("折扣 30%", ctrl.MenuRouter.MerchantPanel.HeaderText);
                    Assert.IsTrue(ctrl.MenuRouter.MerchantPanel.TryClickBuySupply("flare"));
                    expectedSilver -= 21;
                    Assert.AreEqual(expectedSilver, economy.SaltSilverBalance);
                    Assert.AreEqual(i + 1, economy.GetOwnedSupplyCount("flare"));
                }

                // Save to Slot1, buy one more flare, then load Slot1 -> restores exact balance, count, and discounts.
                Assert.IsTrue(ctrl.MenuRouter.OpenModal(RpgWorld500xModalKind.SaveSlots));
                Assert.IsTrue(ctrl.MenuRouter.SaveSlotPanel.TryClickSave(RpgWorld500xSaveSlotId.Slot1));

                Assert.IsTrue(ctrl.MenuRouter.OpenModal(RpgWorld500xModalKind.CampMerchant));
                Assert.IsTrue(ctrl.MenuRouter.MerchantPanel.TryClickBuySupply("flare"));
                Assert.AreEqual(expectedSilver - 21, economy.SaltSilverBalance);
                Assert.AreEqual(5, economy.GetOwnedSupplyCount("flare"));

                Assert.IsTrue(ctrl.MenuRouter.OpenModal(RpgWorld500xModalKind.SaveSlots));
                Assert.IsTrue(ctrl.MenuRouter.SaveSlotPanel.TryClickLoad(RpgWorld500xSaveSlotId.Slot1));
                Assert.AreEqual(RpgWorldMapId.MineCamp, boot.Driver.Facade.CurrentMapId);
                Assert.AreEqual(expectedSilver, boot.Driver.Facade.BountyAndSupply.SaltSilverBalance);
                Assert.AreEqual(4, boot.Driver.Facade.BountyAndSupply.GetOwnedSupplyCount("flare"));
                for (int i = 0; i < regions.Length; i++)
                {
                    Assert.AreEqual(21, ctrl.CampHaven.GetDiscountedUnitCost(regions[i], "flare"));
                }

                ctrl.MenuRouter.CloseAll();
                Assert.IsFalse(ctrl.MenuRouter.HasAnyModalOpen);
            }
            finally
            {
                ctrl.Shutdown();
                UnityEngine.Object.DestroyImmediate(hostGo);
            }

            Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Texture2D>().Length, texBefore);
            Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Sprite>().Length, spriteBefore);
        }
    }
}
