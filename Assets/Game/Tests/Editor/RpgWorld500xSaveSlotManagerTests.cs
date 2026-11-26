using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xSaveSlotManagerTests
    {

        private sealed class RejectableStorage : IRpgWorld500xSaveSlotStorage
        {
            private readonly RpgWorld500xInMemorySaveSlotStorage _inner =
                new RpgWorld500xInMemorySaveSlotStorage();
            public bool RejectMutations;
            public bool ThrowOnMutation;

            public bool TryRead(string storageKey, out string payload) =>
                _inner.TryRead(storageKey, out payload);

            public void Write(string storageKey, string payload)
            {
                if (ThrowOnMutation) throw new InvalidOperationException("Storage unavailable");
                if (!RejectMutations) _inner.Write(storageKey, payload);
            }

            public void Delete(string storageKey)
            {
                if (ThrowOnMutation) throw new InvalidOperationException("Storage unavailable");
                if (!RejectMutations) _inner.Delete(storageKey);
            }
        }

        [Test]
        public void SaveCopyAndDelete_RequireStorageConfirmationAndPreserveOldCheckpointOnRejectedWrites()
        {
            var storage = new RejectableStorage();
            var manager = new RpgWorld500xSaveSlotManager(storage);
            const RpgWorld500xSaveSlotId oldSlot = RpgWorld500xSaveSlotId.Slot1;
            const RpgWorld500xSaveSlotId newSlot = RpgWorld500xSaveSlotId.Slot2;
            string oldKey = RpgWorld500xSaveSlotManager.GetStorageKey(oldSlot);

            using (var boot = new RpgWorld500xSceneBootstrapper(
                initialMapId: RpgWorldMapId.Soundwell,
                overrideTileSlices: new Sprite[] { null }))
            {
                boot.Driver.Facade.BountyAndSupply.AddSaltSilver(40);
                Assert.IsTrue(manager.TrySaveSlot(boot, oldSlot, out _, out string error), error);
                Assert.IsTrue(storage.TryRead(oldKey, out string original));
                boot.Driver.Facade.BountyAndSupply.AddSaltSilver(20);

                storage.RejectMutations = true;
                Assert.IsFalse(manager.TrySaveSlot(boot, oldSlot, out _, out error));
                StringAssert.Contains("写入未确认", error);
                Assert.IsFalse(manager.TryAutoSave(boot, out _, out error));
                Assert.IsFalse(manager.TryCopySlot(oldSlot, newSlot, out error));
                Assert.IsFalse(manager.DeleteSlot(oldSlot));
                Assert.IsFalse(manager.InspectSlot(newSlot).IsOccupied);
                Assert.IsTrue(storage.TryRead(oldKey, out string retained));
                Assert.AreEqual(original, retained);
                Assert.AreEqual(40, manager.InspectSlot(oldSlot).SaltSilverBalance);

                storage.ThrowOnMutation = true;
                Assert.IsFalse(manager.TrySaveSlot(boot, oldSlot, out _, out error));
                StringAssert.Contains("失败", error);
                Assert.IsFalse(manager.TryCopySlot(oldSlot, newSlot, out error));
                Assert.IsFalse(manager.DeleteSlot(oldSlot));
                Assert.AreEqual(40, manager.InspectSlot(oldSlot).SaltSilverBalance);

                storage.ThrowOnMutation = false;
                storage.RejectMutations = false;
                Assert.IsTrue(manager.TrySaveSlot(boot, oldSlot, out var saved, out error), error);
                Assert.AreEqual(60, saved.SaltSilverBalance);
                Assert.IsTrue(manager.TryCopySlot(oldSlot, newSlot, out error), error);
                Assert.AreEqual(60, manager.InspectSlot(newSlot).SaltSilverBalance);
                Assert.IsTrue(manager.DeleteSlot(oldSlot));
                Assert.IsFalse(manager.InspectSlot(oldSlot).IsOccupied);
                Assert.AreEqual(60, manager.InspectSlot(newSlot).SaltSilverBalance);
            }
        }

        [Test]
        public void SaveSlots_RejectEncounterSnapshotsWithoutOverwritingCheckpoints()
        {
            var storage = new RpgWorld500xInMemorySaveSlotStorage();
            var manager = new RpgWorld500xSaveSlotManager(storage);
            using (var boot = new RpgWorld500xSceneBootstrapper(
                initialMapId: RpgWorldMapId.Soundwell,
                overrideTileSlices: new Sprite[] { null }))
            {
                const RpgWorld500xSaveSlotId manual = RpgWorld500xSaveSlotId.Slot1;
                const RpgWorld500xSaveSlotId empty = RpgWorld500xSaveSlotId.Slot2;
                string autoKey = RpgWorld500xSaveSlotManager.GetStorageKey(RpgWorld500xSaveSlotId.AutoSave);
                string manualKey = RpgWorld500xSaveSlotManager.GetStorageKey(manual);
                var facade = boot.Driver.Facade;
                var host = facade.Session.Host;
                facade.BountyAndSupply.AddSaltSilver(30);
                Assert.IsTrue(manager.TryAutoSave(boot, out _, out string error), error);
                Assert.IsTrue(manager.TrySaveSlot(boot, manual, out _, out error), error);
                Assert.IsTrue(storage.TryRead(autoKey, out string originalAuto));
                Assert.IsTrue(storage.TryRead(manualKey, out string originalManual));

                RpgMapLandmarkMarker danger = default;
                bool found = false;
                foreach (RpgMapLandmarkMarker marker in host.Layout.Markers)
                {
                    if (marker.Category == RpgMapMarkerCategory.DangerZone)
                    {
                        danger = marker;
                        found = true;
                        break;
                    }
                }
                Assert.IsTrue(found);
                Assert.IsTrue(host.Bridge.TryTriggerDangerZoneEncounter(
                    danger.Id, new Vector2(danger.Tile.x + 0.5f, danger.Tile.y + 0.5f)));
                facade.BountyAndSupply.AddSaltSilver(10);
                Assert.IsFalse(manager.TryAutoSave(boot, out _, out error));
                Assert.That(error, Does.Contain("遭遇战进行中"));
                Assert.IsFalse(manager.TrySaveSlot(boot, manual, out _, out error));
                Assert.IsFalse(manager.TrySaveSlot(boot, empty, out _, out error));
                Assert.IsTrue(storage.TryRead(autoKey, out string unchangedAuto));
                Assert.IsTrue(storage.TryRead(manualKey, out string unchangedManual));
                Assert.AreEqual(originalAuto, unchangedAuto);
                Assert.AreEqual(originalManual, unchangedManual);
                Assert.IsFalse(manager.InspectSlot(empty).IsOccupied);
                Assert.IsTrue(host.Bridge.IsInDangerEncounter);
                Assert.AreEqual(40, facade.BountyAndSupply.SaltSilverBalance);

                Assert.IsTrue(host.Bridge.TryResolveDangerZoneVictory(danger.Id, true, out _));
                Assert.IsTrue(manager.TryAutoSave(boot, out var saved, out error), error);
                Assert.AreEqual(facade.BountyAndSupply.SaltSilverBalance, saved.SaltSilverBalance);
                Assert.IsTrue(manager.TrySaveSlot(boot, manual, out _, out error), error);
                Assert.IsTrue(storage.TryRead(autoKey, out string updatedAuto));
                Assert.AreNotEqual(originalAuto, updatedAuto);
            }
        }

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
        public void MostRecentSlot_OrdersSubsecondAndLegacyTimestampsAndRestoresLatestProgress()
        {
            DateTime clock = new DateTime(2026, 11, 24, 8, 0, 0, DateTimeKind.Utc);
            var storage = new RpgWorld500xInMemorySaveSlotStorage();
            var manager = new RpgWorld500xSaveSlotManager(storage, () => clock);

            using (var boot = new RpgWorld500xSceneBootstrapper(
                initialMapId: RpgWorldMapId.Soundwell,
                overrideTileSlices: new Sprite[] { null }))
            {
                boot.Driver.Facade.BountyAndSupply.AddSaltSilver(10);
                Assert.IsTrue(manager.TryAutoSave(boot, out var auto, out string error), error);
                Assert.AreEqual("2026-11-24T08:00:00Z", auto.SavedAtUtcIso);

                clock = clock.AddMilliseconds(100);
                boot.Driver.Facade.BountyAndSupply.AddSaltSilver(20);
                Assert.IsTrue(manager.TrySaveSlot(boot, RpgWorld500xSaveSlotId.Slot1, out var manual, out error), error);
                Assert.AreEqual("2026-11-24T08:00:00.1000000Z", manual.SavedAtUtcIso);

                // A fresh manager must find the most recent persisted checkpoint,
                // not merely remember which save method was last invoked.
                var reopened = new RpgWorld500xSaveSlotManager(storage, () => clock);
                Assert.IsTrue(reopened.TryFindMostRecentValidSlot(out var recent));
                Assert.AreEqual(RpgWorld500xSaveSlotId.Slot1, recent.SlotId);
                boot.Driver.Facade.BountyAndSupply.AddSaltSilver(90);
                Assert.AreEqual(120, boot.Driver.Facade.BountyAndSupply.SaltSilverBalance);
                Assert.IsTrue(reopened.TryLoadSlot(boot, recent.SlotId, out _, out error), error);
                Assert.AreEqual(30, boot.Driver.Facade.BountyAndSupply.SaltSilverBalance);

                clock = clock.AddMilliseconds(900);
                boot.Driver.Facade.BountyAndSupply.AddSaltSilver(40);
                Assert.IsTrue(manager.TrySaveSlot(boot, RpgWorld500xSaveSlotId.Slot2, out _, out error), error);
                Assert.IsTrue(reopened.TryFindMostRecentValidSlot(out recent));
                Assert.AreEqual(RpgWorld500xSaveSlotId.Slot2, recent.SlotId);
                Assert.AreEqual(70, recent.SaltSilverBalance);
            }
        }

        [Test]
        public void InvalidSlotId_CannotReadOverwriteCopyOrDeleteAutoSave()
        {
            var storage = new RpgWorld500xInMemorySaveSlotStorage();
            var manager = new RpgWorld500xSaveSlotManager(storage);
            var invalid = (RpgWorld500xSaveSlotId)255;
            string autoKey = RpgWorld500xSaveSlotManager.GetStorageKey(RpgWorld500xSaveSlotId.AutoSave);

            using (var boot = new RpgWorld500xSceneBootstrapper(
                initialMapId: RpgWorldMapId.Soundwell,
                overrideTileSlices: new Sprite[] { null }))
            {
                boot.Driver.Facade.BountyAndSupply.AddSaltSilver(95);
                Assert.IsTrue(manager.TryAutoSave(boot, out _, out string err), err);
                Assert.IsTrue(storage.TryRead(autoKey, out string original));
                boot.Driver.Facade.BountyAndSupply.AddSaltSilver(50);
                Assert.IsTrue(manager.TrySaveSlot(boot, RpgWorld500xSaveSlotId.Slot1, out _, out err), err);

                Assert.Throws<ArgumentOutOfRangeException>(() => RpgWorld500xSaveSlotManager.GetStorageKey(invalid));
                Assert.IsFalse(manager.TrySaveSlot(boot, invalid, out _, out _));
                Assert.IsFalse(manager.TryLoadSlot(boot, invalid, out _, out _));
                Assert.AreEqual(145, boot.Driver.Facade.BountyAndSupply.SaltSilverBalance);
                Assert.IsFalse(manager.TryCopySlot(invalid, RpgWorld500xSaveSlotId.Slot2, out _));
                Assert.IsFalse(manager.TryCopySlot(RpgWorld500xSaveSlotId.Slot1, invalid, out _));
                Assert.IsFalse(manager.DeleteSlot(invalid));
                Assert.IsFalse(manager.InspectSlot(invalid).IsOccupied);
                Assert.IsFalse(manager.InspectSlot(RpgWorld500xSaveSlotId.Slot2).IsOccupied);
                Assert.IsTrue(storage.TryRead(autoKey, out string after));
                Assert.AreEqual(original, after);
                Assert.IsTrue(manager.TryLoadSlot(boot, RpgWorld500xSaveSlotId.AutoSave, out _, out err), err);
                Assert.AreEqual(95, boot.Driver.Facade.BountyAndSupply.SaltSilverBalance);
            }
        }

        [Test]
        public void BlankStoredPayload_IsOccupiedCorruptAndCanBeExplicitlyDeleted()
        {
            var storage = new RpgWorld500xInMemorySaveSlotStorage();
            var manager = new RpgWorld500xSaveSlotManager(storage);
            var damaged = RpgWorld500xSaveSlotId.Slot1;
            var untouched = RpgWorld500xSaveSlotId.Slot2;
            string key = RpgWorld500xSaveSlotManager.GetStorageKey(damaged);

            using (var boot = new RpgWorld500xSceneBootstrapper(
                initialMapId: RpgWorldMapId.Soundwell,
                overrideTileSlices: new Sprite[] { null }))
            {
                boot.Driver.Facade.BountyAndSupply.AddSaltSilver(42);
                Assert.IsTrue(manager.TrySaveSlot(boot, untouched, out _, out string error), error);
                foreach (string payload in new[] { "", "   ", "\t\n" })
                {
                    storage.Write(key, payload);
                    var summary = manager.InspectSlot(damaged);
                    Assert.IsTrue(summary.IsOccupied);
                    Assert.IsTrue(summary.IsCorrupted);
                    Assert.IsFalse(manager.TryLoadSlot(boot, damaged, out var loaded, out _));
                    Assert.IsTrue(loaded.IsOccupied && loaded.IsCorrupted);
                    Assert.IsFalse(manager.TryCopySlot(damaged, untouched, out _));
                    Assert.AreEqual(42, manager.InspectSlot(untouched).SaltSilverBalance);
                    Assert.IsTrue(manager.TryFindMostRecentValidSlot(out var recent));
                    Assert.AreEqual(untouched, recent.SlotId);
                    Assert.IsTrue(manager.DeleteSlot(damaged));
                    Assert.IsFalse(manager.InspectSlot(damaged).IsOccupied);
                }
            }
        }

        [Test]
        public void PlayerPrefsStorage_PresentEmptyPayloadRemainsReadableAndDeletable()
        {
            var storage = new RpgWorld500xPlayerPrefsSaveSlotStorage();
            string key = RpgWorld500xSaveSlotManager.StorageKeyPrefix + "test_" + Guid.NewGuid().ToString("N");
            try
            {
                Assert.IsFalse(storage.TryRead(key, out _));
                storage.Write(key, string.Empty);
                Assert.IsTrue(storage.TryRead(key, out string payload));
                Assert.AreEqual(string.Empty, payload);
                storage.Delete(key);
                Assert.IsFalse(storage.TryRead(key, out _));
            }
            finally
            {
                storage.Delete(key);
            }
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
