using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xSaveSlotAndCampMerchantHudE2ETests
    {
        [Test]
        public void PlayModeController_CampMerchantPurchaseSaveSwitchLoadAndDeleteRoundTripWithZeroLeak()
        {
            // Warm up one-time UGUI/font caches before steady-state leak baseline.
            var warmupGo = new GameObject("World500xSaveMerchantWarmupHost");
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

            var hostGo = new GameObject("World500xSaveMerchantE2EHost");
            var ctrl = hostGo.AddComponent<RpgWorld500xPlayModeController>();
            try
            {
                var storage = new RpgWorld500xInMemorySaveSlotStorage();
                ctrl.Initialize(
                    overrideMap: RpgWorldMapId.Soundwell,
                    saveStorage: storage,
                    overrideTileSlices: new Sprite[] { null });

                var boot = ctrl.Bootstrapper;
                var economy = boot.Driver.Facade.BountyAndSupply;
                economy.AddSaltSilver(100);
                ctrl.CampHaven.Reputation.AddPoints(RpgWorldMapId.Soundwell, 800);

                // Purchase discounted supply via CampMerchant modal.
                Assert.IsTrue(ctrl.MenuRouter.OpenModal(RpgWorld500xModalKind.CampMerchant));
                Assert.IsTrue(ctrl.MenuRouter.MerchantPanel.IsVisible);
                StringAssert.Contains("折扣 30%", ctrl.MenuRouter.MerchantPanel.HeaderText);
                Assert.IsTrue(ctrl.MenuRouter.MerchantPanel.TryClickBuySupply("flare"));
                Assert.AreEqual(79, economy.SaltSilverBalance);
                Assert.AreEqual(1, economy.GetOwnedSupplyCount("flare"));

                // Switch to SaveSlots modal (mutual exclusion) and persist Slot1.
                const RpgWorld500xSaveSlotId slot = RpgWorld500xSaveSlotId.Slot1;
                string slotKey = RpgWorld500xSaveSlotManager.GetStorageKey(slot);
                Assert.IsTrue(ctrl.MenuRouter.OpenModal(RpgWorld500xModalKind.SaveSlots));
                Assert.IsFalse(ctrl.MenuRouter.MerchantPanel.IsVisible);
                Assert.IsTrue(ctrl.MenuRouter.SaveSlotPanel.IsVisible);
                Assert.IsTrue(ctrl.MenuRouter.SaveSlotPanel.TryClickSave(slot));
                Assert.IsTrue(storage.TryRead(slotKey, out _));

                // Mutate map, wallet, and inventory after saving.
                Assert.IsTrue(boot.SwitchMap(RpgWorldMapId.RedSaltHarbor));
                Assert.IsTrue(ctrl.MenuRouter.OpenModal(RpgWorld500xModalKind.CampMerchant));
                Assert.IsTrue(ctrl.MenuRouter.MerchantPanel.TryClickBuySupply("flare"));
                Assert.AreEqual(RpgWorldMapId.RedSaltHarbor, boot.Driver.Facade.CurrentMapId);
                Assert.AreEqual(49, economy.SaltSilverBalance);
                Assert.AreEqual(2, economy.GetOwnedSupplyCount("flare"));

                // Load Slot1 and verify map, wallet, and owned supply restore cleanly.
                Assert.IsTrue(ctrl.MenuRouter.OpenModal(RpgWorld500xModalKind.SaveSlots));
                Assert.IsTrue(ctrl.MenuRouter.SaveSlotPanel.TryClickLoad(slot));
                Assert.AreEqual(RpgWorldMapId.Soundwell, boot.Driver.Facade.CurrentMapId);
                Assert.AreEqual(79, boot.Driver.Facade.BountyAndSupply.SaltSilverBalance);
                Assert.AreEqual(1, boot.Driver.Facade.BountyAndSupply.GetOwnedSupplyCount("flare"));

                // Delete Slot1 and confirm storage cleanup.
                Assert.IsTrue(ctrl.MenuRouter.SaveSlotPanel.TryClickDelete(slot));
                Assert.IsFalse(storage.TryRead(slotKey, out _));

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
