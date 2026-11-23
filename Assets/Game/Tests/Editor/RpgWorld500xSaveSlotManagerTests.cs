using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xSaveSlotManagerTests
    {
        [Test]
        public void AllSaveSlots_UseSafeWorldKeysAndChineseLabels()
        {
            Assert.AreEqual(4, RpgWorld500xSaveSlotManager.AllSlots.Count);
            var seenKeys = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < RpgWorld500xSaveSlotManager.AllSlots.Count; i++)
            {
                RpgWorld500xSaveSlotId slotId = RpgWorld500xSaveSlotManager.AllSlots[i];
                string key = RpgWorld500xSaveSlotManager.GetStorageKey(slotId);
                Assert.IsTrue(RpgWorld500xEnrichedContentAuditor.IsSafeWorldKey(key), key);
                Assert.IsTrue(seenKeys.Add(key), "Duplicate slot storage key: " + key);
                Assert.IsFalse(string.IsNullOrWhiteSpace(RpgWorld500xSaveSlotManager.GetSlotDisplayNameZh(slotId)));
            }
        }

        [Test]
        public void SaveLoadCopyDeleteAndMostRecentSelection_WorkDeterministicallyAcrossSlots()
        {
            DateTime clock = new DateTime(2026, 11, 23, 8, 0, 0, DateTimeKind.Utc);
            var storage = new RpgWorld500xInMemorySaveSlotStorage();
            var manager = new RpgWorld500xSaveSlotManager(storage, () => clock);

            // All 4 slots start empty.
            List<RpgWorld500xSaveSlotSummary> initial = manager.ListAllSlots();
            Assert.AreEqual(4, initial.Count);
            for (int i = 0; i < initial.Count; i++)
            {
                Assert.IsFalse(initial[i].IsOccupied);
                Assert.IsFalse(initial[i].IsCorrupted);
            }
            Assert.IsFalse(manager.TryFindMostRecentValidSlot(out _));

            using (var boot = new RpgWorld500xSceneBootstrapper(
                initialMapId: RpgWorldMapId.Soundwell,
                overrideTileSlices: new Sprite[] { null }))
            {
                boot.Driver.Facade.BountyAndSupply.AddSaltSilver(95);
                Assert.IsTrue(manager.TryAutoSave(boot, out RpgWorld500xSaveSlotSummary autoSum, out string err), err);
                Assert.IsTrue(autoSum.IsOccupied);
                Assert.IsFalse(autoSum.IsCorrupted);
                Assert.AreEqual(RpgWorldMapId.Soundwell, autoSum.MapId);
                Assert.AreEqual(95, autoSum.SaltSilverBalance);
                Assert.AreEqual("2026-11-23T08:00:00Z", autoSum.SavedAtUtcIso);

                // Advance clock, travel to RedSaltHarbor, add Salt-Silver, save to Slot1.
                clock = clock.AddMinutes(45);
                Assert.IsTrue(boot.SwitchMap(RpgWorldMapId.RedSaltHarbor));
                boot.Driver.Facade.BountyAndSupply.AddSaltSilver(120);
                Assert.IsTrue(manager.TrySaveSlot(
                    boot, RpgWorld500xSaveSlotId.Slot1, out RpgWorld500xSaveSlotSummary slot1Sum, out err, "赤盐港补给前"), err);
                Assert.AreEqual(RpgWorldMapId.RedSaltHarbor, slot1Sum.MapId);
                Assert.AreEqual(215, slot1Sum.SaltSilverBalance);
                Assert.AreEqual("赤盐港补给前", slot1Sum.CustomLabelZh);

                // Most recent valid slot must be Slot1.
                Assert.IsTrue(manager.TryFindMostRecentValidSlot(out RpgWorld500xSaveSlotSummary recent));
                Assert.AreEqual(RpgWorld500xSaveSlotId.Slot1, recent.SlotId);

                // Copy Slot1 -> Slot2 and verify.
                Assert.IsTrue(manager.TryCopySlot(RpgWorld500xSaveSlotId.Slot1, RpgWorld500xSaveSlotId.Slot2, out err), err);
                RpgWorld500xSaveSlotSummary slot2Sum = manager.InspectSlot(RpgWorld500xSaveSlotId.Slot2);
                Assert.IsTrue(slot2Sum.IsOccupied);
                Assert.AreEqual(RpgWorldMapId.RedSaltHarbor, slot2Sum.MapId);
                Assert.AreEqual(215, slot2Sum.SaltSilverBalance);

                // Reject copying onto self.
                Assert.IsFalse(manager.TryCopySlot(RpgWorld500xSaveSlotId.Slot1, RpgWorld500xSaveSlotId.Slot1, out _));
            }

            // Restore AutoSave into a fresh bootstrapper -> returns to Soundwell with 95 Salt-Silver.
            using (var restored = new RpgWorld500xSceneBootstrapper(
                initialMapId: RpgWorldMapId.MineCamp,
                overrideTileSlices: new Sprite[] { null }))
            {
                Assert.IsTrue(manager.TryLoadSlot(
                    restored, RpgWorld500xSaveSlotId.AutoSave, out RpgWorld500xSaveSlotSummary loaded, out string err), err);
                Assert.AreEqual(RpgWorldMapId.Soundwell, loaded.MapId);
                Assert.AreEqual(RpgWorldMapId.Soundwell, restored.Driver.Facade.CurrentMapId);
                Assert.AreEqual(95, restored.Driver.Facade.BountyAndSupply.SaltSilverBalance);
            }

            // Delete Slot2 and verify empty.
            Assert.IsTrue(manager.DeleteSlot(RpgWorld500xSaveSlotId.Slot2));
            Assert.IsFalse(manager.DeleteSlot(RpgWorld500xSaveSlotId.Slot2));
            Assert.IsFalse(manager.InspectSlot(RpgWorld500xSaveSlotId.Slot2).IsOccupied);
        }

        [Test]
        public void CorruptedOrTamperedSlot_IsIsolatedWithoutBreakingOtherSlots()
        {
            var storage = new RpgWorld500xInMemorySaveSlotStorage();
            DateTime clock = new DateTime(2026, 11, 23, 12, 0, 0, DateTimeKind.Utc);
            var manager = new RpgWorld500xSaveSlotManager(storage, () => clock);

            using (var boot = new RpgWorld500xSceneBootstrapper(
                initialMapId: RpgWorldMapId.WhitewaterCrossing,
                overrideTileSlices: new Sprite[] { null }))
            {
                boot.Driver.Facade.BountyAndSupply.AddSaltSilver(60);
                Assert.IsTrue(manager.TrySaveSlot(
                    boot, RpgWorld500xSaveSlotId.Slot1, out _, out string err), err);

                // Corrupt Slot3 with malformed JSON and tamper Slot2 with mismatched checksum.
                storage.Write(RpgWorld500xSaveSlotManager.GetStorageKey(RpgWorld500xSaveSlotId.Slot3), "{broken");
                Assert.IsTrue(storage.TryRead(
                    RpgWorld500xSaveSlotManager.GetStorageKey(RpgWorld500xSaveSlotId.Slot1), out string validRaw));
                storage.Write(
                    RpgWorld500xSaveSlotManager.GetStorageKey(RpgWorld500xSaveSlotId.Slot2),
                    validRaw.Replace("60", "99999"));

                List<RpgWorld500xSaveSlotSummary> all = manager.ListAllSlots();
                Assert.AreEqual(4, all.Count);
                Assert.IsFalse(all[0].IsOccupied); // AutoSave
                Assert.IsTrue(all[1].IsOccupied && !all[1].IsCorrupted); // Slot1 valid
                Assert.IsTrue(all[2].IsOccupied && all[2].IsCorrupted); // Slot2 tampered
                Assert.IsTrue(all[3].IsOccupied && all[3].IsCorrupted); // Slot3 malformed

                // Loading or copying corrupted slots fails cleanly without throwing.
                Assert.IsFalse(manager.TryLoadSlot(boot, RpgWorld500xSaveSlotId.Slot2, out _, out _));
                Assert.IsFalse(manager.TryLoadSlot(boot, RpgWorld500xSaveSlotId.Slot3, out _, out _));
                Assert.IsFalse(manager.TryCopySlot(RpgWorld500xSaveSlotId.Slot2, RpgWorld500xSaveSlotId.AutoSave, out _));

                // Most recent valid slot still resolves cleanly to Slot1.
                Assert.IsTrue(manager.TryFindMostRecentValidSlot(out RpgWorld500xSaveSlotSummary recent));
                Assert.AreEqual(RpgWorld500xSaveSlotId.Slot1, recent.SlotId);
            }
        }
    }
}
