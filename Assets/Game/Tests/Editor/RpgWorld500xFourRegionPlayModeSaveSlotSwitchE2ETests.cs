using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionPlayModeSaveSlotSwitchE2ETests
    {
        [Test]
        public void AllFourRegions_PlayModeControllerMultiSlotSaveLoadAndCorruptionIsolation()
        {
            var host = new GameObject("FourRegionPlayModeSaveSlotSwitch");
            var ctrl = host.AddComponent<RpgWorld500xPlayModeController>();
            var storage = new RpgWorld500xInMemorySaveSlotStorage();
            try
            {
                ctrl.Initialize(
                    overrideMap: RpgWorldMapId.Soundwell,
                    saveStorage: storage,
                    overrideTileSlices: new Sprite[] { null });

                var boot = ctrl.Bootstrapper;
                var manager = ctrl.SaveSlotManager;
                Assert.IsNotNull(manager);

                // AutoSave: Soundwell (100 silver)
                boot.Driver.Facade.BountyAndSupply.AddSaltSilver(100);
                Assert.IsTrue(manager.TrySaveSlot(boot, RpgWorld500xSaveSlotId.AutoSave, out _, out string err0), err0);

                // Slot1: RedSaltHarbor (200 silver)
                Assert.IsTrue(boot.SwitchMap(RpgWorldMapId.RedSaltHarbor));
                boot.Driver.Facade.BountyAndSupply.AddSaltSilver(100);
                Assert.IsTrue(manager.TrySaveSlot(boot, RpgWorld500xSaveSlotId.Slot1, out _, out string err1), err1);

                // Slot2: WhitewaterCrossing (300 silver)
                Assert.IsTrue(boot.SwitchMap(RpgWorldMapId.WhitewaterCrossing));
                boot.Driver.Facade.BountyAndSupply.AddSaltSilver(100);
                Assert.IsTrue(manager.TrySaveSlot(boot, RpgWorld500xSaveSlotId.Slot2, out _, out string err2), err2);

                // Slot3: MineCamp (400 silver)
                Assert.IsTrue(boot.SwitchMap(RpgWorldMapId.MineCamp));
                boot.Driver.Facade.BountyAndSupply.AddSaltSilver(100);
                Assert.IsTrue(manager.TrySaveSlot(boot, RpgWorld500xSaveSlotId.Slot3, out _, out string err3), err3);

                // Corrupt Slot2 externally -> loading fails without disturbing live MineCamp state.
                storage.Write(RpgWorld500xSaveSlotManager.GetStorageKey(RpgWorld500xSaveSlotId.Slot2), "   ");
                var corrupted = manager.InspectSlot(RpgWorld500xSaveSlotId.Slot2);
                Assert.IsTrue(corrupted.IsOccupied);
                Assert.IsTrue(corrupted.IsCorrupted);
                Assert.IsFalse(manager.TryLoadSlot(boot, RpgWorld500xSaveSlotId.Slot2, out _, out _));
                Assert.AreEqual(RpgWorldMapId.MineCamp, boot.Driver.Facade.CurrentMapId);
                Assert.AreEqual(400, boot.Driver.Facade.BountyAndSupply.SaltSilverBalance);

                // Load Slot1 -> RedSaltHarbor (200 silver) and verify live PlayModeController tick stays healthy.
                Assert.IsTrue(manager.TryLoadSlot(boot, RpgWorld500xSaveSlotId.Slot1, out _, out string load1Err), load1Err);
                ctrl.ProcessInputFrame(new RpgWorld500xInputFrame(), 0.1f);
                Assert.AreEqual(RpgWorldMapId.RedSaltHarbor, boot.Driver.Facade.CurrentMapId);
                Assert.AreEqual(200, boot.Driver.Facade.BountyAndSupply.SaltSilverBalance);
                Assert.IsTrue(ctrl.ReconPulseHud.IsScanButtonInteractable);

                // Load AutoSave -> Soundwell (100 silver)
                Assert.IsTrue(manager.TryLoadSlot(boot, RpgWorld500xSaveSlotId.AutoSave, out _, out string load0Err), load0Err);
                ctrl.ProcessInputFrame(new RpgWorld500xInputFrame(), 0.1f);
                Assert.AreEqual(RpgWorldMapId.Soundwell, boot.Driver.Facade.CurrentMapId);
                Assert.AreEqual(100, boot.Driver.Facade.BountyAndSupply.SaltSilverBalance);

                // Load Slot3 -> MineCamp (400 silver)
                Assert.IsTrue(manager.TryLoadSlot(boot, RpgWorld500xSaveSlotId.Slot3, out _, out string load3Err), load3Err);
                ctrl.ProcessInputFrame(new RpgWorld500xInputFrame(), 0.1f);
                Assert.AreEqual(RpgWorldMapId.MineCamp, boot.Driver.Facade.CurrentMapId);
                Assert.AreEqual(400, boot.Driver.Facade.BountyAndSupply.SaltSilverBalance);

                // Delete corrupted Slot2 cleanly.
                Assert.IsTrue(manager.DeleteSlot(RpgWorld500xSaveSlotId.Slot2));
                Assert.IsFalse(manager.InspectSlot(RpgWorld500xSaveSlotId.Slot2).IsOccupied);

                ctrl.Shutdown();
                Assert.AreEqual(0, host.transform.childCount);
            }
            finally
            {
                ctrl.Shutdown();
                Object.DestroyImmediate(host);
            }
        }
    }
}
