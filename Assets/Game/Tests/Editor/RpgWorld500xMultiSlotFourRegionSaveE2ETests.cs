using System;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xMultiSlotFourRegionSaveE2ETests
    {
        [Test]
        public void MultiSlotSaveManager_PreservesDistinctRegionsHandlesCorruptionCopyAndRestoreWithZeroLeak()
        {
            // Warm up one-time runtime caches before steady-state leak baseline.
            using (var warmupBoot = new RpgWorld500xSceneBootstrapper(
                initialMapId: RpgWorldMapId.Soundwell,
                overrideTileSlices: new Sprite[] { null }))
            {
                warmupBoot.SwitchMap(RpgWorldMapId.RedSaltHarbor);
            }

            int texBefore = Resources.FindObjectsOfTypeAll<Texture2D>().Length;
            int spriteBefore = Resources.FindObjectsOfTypeAll<Sprite>().Length;

            var storage = new RpgWorld500xInMemorySaveSlotStorage();
            DateTime clock = new DateTime(2026, 11, 26, 10, 0, 0, DateTimeKind.Utc);
            var manager = new RpgWorld500xSaveSlotManager(storage, () => clock);

            using (var boot = new RpgWorld500xSceneBootstrapper(
                initialMapId: RpgWorldMapId.Soundwell,
                overrideTileSlices: new Sprite[] { null }))
            {
                // Slot1: Soundwell with 110 Salt Silver.
                boot.Driver.Facade.BountyAndSupply.AddSaltSilver(110);
                Assert.IsTrue(manager.TrySaveSlot(boot, RpgWorld500xSaveSlotId.Slot1, out _, out string err1), err1);

                // Slot2: RedSaltHarbor with 220 Salt Silver.
                clock = clock.AddMinutes(5);
                Assert.IsTrue(boot.SwitchMap(RpgWorldMapId.RedSaltHarbor));
                boot.Driver.Facade.BountyAndSupply.AddSaltSilver(110);
                Assert.AreEqual(220, boot.Driver.Facade.BountyAndSupply.SaltSilverBalance);
                Assert.IsTrue(manager.TrySaveSlot(boot, RpgWorld500xSaveSlotId.Slot2, out _, out string err2), err2);

                // Slot3: MineCamp with 330 Salt Silver.
                clock = clock.AddMinutes(5);
                Assert.IsTrue(boot.SwitchMap(RpgWorldMapId.MineCamp));
                boot.Driver.Facade.BountyAndSupply.AddSaltSilver(110);
                Assert.AreEqual(330, boot.Driver.Facade.BountyAndSupply.SaltSilverBalance);
                Assert.IsTrue(manager.TrySaveSlot(boot, RpgWorld500xSaveSlotId.Slot3, out _, out string err3), err3);

                Assert.IsTrue(manager.TryFindMostRecentValidSlot(out var recentBefore));
                Assert.AreEqual(RpgWorld500xSaveSlotId.Slot3, recentBefore.SlotId);

                // Corrupt Slot2 externally -> isolated without breaking Slot1 or Slot3.
                storage.Write(RpgWorld500xSaveSlotManager.GetStorageKey(RpgWorld500xSaveSlotId.Slot2), "   ");
                var slot2Summary = manager.InspectSlot(RpgWorld500xSaveSlotId.Slot2);
                Assert.IsTrue(slot2Summary.IsOccupied);
                Assert.IsTrue(slot2Summary.IsCorrupted);
                Assert.IsFalse(manager.TryLoadSlot(boot, RpgWorld500xSaveSlotId.Slot2, out _, out _));

                Assert.AreEqual(110, manager.InspectSlot(RpgWorld500xSaveSlotId.Slot1).SaltSilverBalance);
                Assert.AreEqual(330, manager.InspectSlot(RpgWorld500xSaveSlotId.Slot3).SaltSilverBalance);

                // Copy Slot1 -> AutoSave and restore Soundwell (110 silver).
                Assert.IsTrue(manager.TryCopySlot(RpgWorld500xSaveSlotId.Slot1, RpgWorld500xSaveSlotId.AutoSave, out string copyErr), copyErr);
                Assert.IsTrue(manager.TryLoadSlot(boot, RpgWorld500xSaveSlotId.AutoSave, out _, out string loadAutoErr), loadAutoErr);
                Assert.AreEqual(RpgWorldMapId.Soundwell, boot.Driver.Facade.CurrentMapId);
                Assert.AreEqual(110, boot.Driver.Facade.BountyAndSupply.SaltSilverBalance);

                // Restore Slot3 -> MineCamp (330 silver).
                Assert.IsTrue(manager.TryLoadSlot(boot, RpgWorld500xSaveSlotId.Slot3, out _, out string load3Err), load3Err);
                Assert.AreEqual(RpgWorldMapId.MineCamp, boot.Driver.Facade.CurrentMapId);
                Assert.AreEqual(330, boot.Driver.Facade.BountyAndSupply.SaltSilverBalance);

                // Explicitly delete damaged Slot2.
                Assert.IsTrue(manager.DeleteSlot(RpgWorld500xSaveSlotId.Slot2));
                Assert.IsFalse(manager.InspectSlot(RpgWorld500xSaveSlotId.Slot2).IsOccupied);
            }

            Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Texture2D>().Length, texBefore);
            Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Sprite>().Length, spriteBefore);
        }
    }
}
