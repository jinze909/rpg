using System;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionSaveSlotMutationGuardE2ETests
    {
        private sealed class GuardedStorage : IRpgWorld500xSaveSlotStorage
        {
            private readonly RpgWorld500xInMemorySaveSlotStorage _inner =
                new RpgWorld500xInMemorySaveSlotStorage();

            public bool RejectMutations;
            public bool ThrowOnMutation;

            public bool TryRead(string storageKey, out string payload) =>
                _inner.TryRead(storageKey, out payload);

            public void Write(string storageKey, string payload)
            {
                if (ThrowOnMutation)
                {
                    throw new InvalidOperationException("Storage unavailable");
                }

                if (!RejectMutations)
                {
                    _inner.Write(storageKey, payload);
                }
            }

            public void Delete(string storageKey)
            {
                if (ThrowOnMutation)
                {
                    throw new InvalidOperationException("Storage unavailable");
                }

                if (!RejectMutations)
                {
                    _inner.Delete(storageKey);
                }
            }
        }

        [Test]
        public void SaveSlotManager_AcrossAllFourRegions_ConfirmsSaveCopyDeleteAndPreservesCheckpointOnRejectedOrThrowingStorage()
        {
            var specs = RpgExpandedWorldScale.AllSpecs;
            Assert.That(specs.Count, Is.EqualTo(4));

            for (int i = 0; i < specs.Count; i++)
            {
                var mapId = specs[i].MapId;
                var storage = new GuardedStorage();
                var manager = new RpgWorld500xSaveSlotManager(storage);
                int initialSilver = 40 + i * 10;
                string slot1Key = RpgWorld500xSaveSlotManager.GetStorageKey(RpgWorld500xSaveSlotId.Slot1);

                using (var boot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: mapId,
                    overrideTileSlices: new Sprite[] { null }))
                {
                    boot.Driver.Facade.BountyAndSupply.AddSaltSilver(initialSilver);
                    Assert.IsTrue(manager.TrySaveSlot(boot, RpgWorld500xSaveSlotId.Slot1, out _, out string err), err);
                    Assert.IsTrue(storage.TryRead(slot1Key, out string originalPayload));
                    boot.Driver.Facade.BountyAndSupply.AddSaltSilver(20);

                    storage.RejectMutations = true;
                    Assert.IsFalse(manager.TrySaveSlot(boot, RpgWorld500xSaveSlotId.Slot1, out _, out err));
                    StringAssert.Contains("写入未确认", err);
                    Assert.IsFalse(manager.TryAutoSave(boot, out _, out err));
                    Assert.IsFalse(manager.TryCopySlot(RpgWorld500xSaveSlotId.Slot1, RpgWorld500xSaveSlotId.Slot2, out err));
                    Assert.IsFalse(manager.DeleteSlot(RpgWorld500xSaveSlotId.Slot1));
                    Assert.IsFalse(manager.InspectSlot(RpgWorld500xSaveSlotId.Slot2).IsOccupied);
                    Assert.IsTrue(storage.TryRead(slot1Key, out string retainedPayload));
                    Assert.That(retainedPayload, Is.EqualTo(originalPayload));
                    Assert.That(manager.InspectSlot(RpgWorld500xSaveSlotId.Slot1).SaltSilverBalance, Is.EqualTo(initialSilver));

                    storage.ThrowOnMutation = true;
                    Assert.IsFalse(manager.TrySaveSlot(boot, RpgWorld500xSaveSlotId.Slot1, out _, out err));
                    StringAssert.Contains("失败", err);
                    Assert.IsFalse(manager.TryCopySlot(RpgWorld500xSaveSlotId.Slot1, RpgWorld500xSaveSlotId.Slot2, out err));
                    Assert.IsFalse(manager.DeleteSlot(RpgWorld500xSaveSlotId.Slot1));
                    Assert.That(manager.InspectSlot(RpgWorld500xSaveSlotId.Slot1).SaltSilverBalance, Is.EqualTo(initialSilver));

                    storage.ThrowOnMutation = false;
                    storage.RejectMutations = false;
                    Assert.IsTrue(manager.TrySaveSlot(boot, RpgWorld500xSaveSlotId.Slot1, out var saved, out err), err);
                    Assert.That(saved.SaltSilverBalance, Is.EqualTo(initialSilver + 20));
                    Assert.IsTrue(manager.TryCopySlot(RpgWorld500xSaveSlotId.Slot1, RpgWorld500xSaveSlotId.Slot2, out err), err);
                    Assert.That(manager.InspectSlot(RpgWorld500xSaveSlotId.Slot2).SaltSilverBalance, Is.EqualTo(initialSilver + 20));
                    Assert.IsTrue(manager.DeleteSlot(RpgWorld500xSaveSlotId.Slot1));
                    Assert.IsFalse(manager.InspectSlot(RpgWorld500xSaveSlotId.Slot1).IsOccupied);
                    Assert.That(manager.InspectSlot(RpgWorld500xSaveSlotId.Slot2).SaltSilverBalance, Is.EqualTo(initialSilver + 20));
                }
            }
        }

        [Test]
        public void SaveSlotManager_AllFourRegions_DistinctSlotsRemainIsolatedAcrossSaveCopyAndDelete()
        {
            var specs = RpgExpandedWorldScale.AllSpecs;
            Assert.That(specs.Count, Is.EqualTo(4));

            var storage = new RpgWorld500xInMemorySaveSlotStorage();
            var manager = new RpgWorld500xSaveSlotManager(storage);
            var manualSlots = new[]
            {
                RpgWorld500xSaveSlotId.Slot1,
                RpgWorld500xSaveSlotId.Slot2,
                RpgWorld500xSaveSlotId.Slot3
            };

            for (int i = 0; i < manualSlots.Length; i++)
            {
                using (var boot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: specs[i].MapId,
                    overrideTileSlices: new Sprite[] { null }))
                {
                    boot.Driver.Facade.BountyAndSupply.AddSaltSilver((i + 1) * 100);
                    Assert.IsTrue(manager.TrySaveSlot(boot, manualSlots[i], out _, out string err), err);
                }
            }

            using (var autoBoot = new RpgWorld500xSceneBootstrapper(
                initialMapId: specs[3].MapId,
                overrideTileSlices: new Sprite[] { null }))
            {
                autoBoot.Driver.Facade.BountyAndSupply.AddSaltSilver(400);
                Assert.IsTrue(manager.TryAutoSave(autoBoot, out var autoSummary, out string err), err);
                Assert.That(autoSummary.SaltSilverBalance, Is.EqualTo(400));
            }

            for (int i = 0; i < manualSlots.Length; i++)
            {
                var summary = manager.InspectSlot(manualSlots[i]);
                Assert.IsTrue(summary.IsOccupied);
                Assert.That(summary.SaltSilverBalance, Is.EqualTo((i + 1) * 100));
            }

            Assert.IsTrue(manager.DeleteSlot(RpgWorld500xSaveSlotId.Slot2));
            Assert.IsFalse(manager.InspectSlot(RpgWorld500xSaveSlotId.Slot2).IsOccupied);
            Assert.That(manager.InspectSlot(RpgWorld500xSaveSlotId.Slot1).SaltSilverBalance, Is.EqualTo(100));
            Assert.That(manager.InspectSlot(RpgWorld500xSaveSlotId.Slot3).SaltSilverBalance, Is.EqualTo(300));
        }
    }
}
