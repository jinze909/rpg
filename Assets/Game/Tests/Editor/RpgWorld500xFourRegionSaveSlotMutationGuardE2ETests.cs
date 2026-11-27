using System;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionSaveSlotMutationGuardE2ETests
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
        public void AllFourRegions_SaveCopyAndDeleteGuardPreservesCheckpointsAndRecoversCleanlyWithZeroLeak()
        {
            // Warm up one-time runtime caches before steady-state leak baseline.
            using (var warmupBoot = new RpgWorld500xSceneBootstrapper(
                initialMapId: RpgWorldMapId.Soundwell,
                overrideTileSlices: new Sprite[] { null }))
            {
                var warmupMgr = new RpgWorld500xSaveSlotManager(new RpgWorld500xInMemorySaveSlotStorage());
                warmupMgr.TrySaveSlot(warmupBoot, RpgWorld500xSaveSlotId.Slot1, out _, out _);
            }

            int texBefore = Resources.FindObjectsOfTypeAll<Texture2D>().Length;
            int spriteBefore = Resources.FindObjectsOfTypeAll<Sprite>().Length;

            int verifiedRegions = 0;
            foreach (RpgExpandedMapSpec spec in RpgExpandedWorldScale.AllSpecs)
            {
                var storage = new RejectableStorage();
                var manager = new RpgWorld500xSaveSlotManager(storage);
                const RpgWorld500xSaveSlotId oldSlot = RpgWorld500xSaveSlotId.Slot1;
                const RpgWorld500xSaveSlotId newSlot = RpgWorld500xSaveSlotId.Slot2;
                string oldKey = RpgWorld500xSaveSlotManager.GetStorageKey(oldSlot);

                using (var boot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: spec.MapId,
                    overrideTileSlices: new Sprite[] { null }))
                {
                    boot.Driver.Facade.BountyAndSupply.AddSaltSilver(40);
                    Assert.IsTrue(manager.TrySaveSlot(boot, oldSlot, out _, out string error), error);
                    Assert.IsTrue(storage.TryRead(oldKey, out string original));
                    boot.Driver.Facade.BountyAndSupply.AddSaltSilver(20);

                    // Silent write/delete rejection preserves the prior checkpoint.
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

                    // Storage exceptions fail closed without corrupting the existing checkpoint.
                    storage.ThrowOnMutation = true;
                    Assert.IsFalse(manager.TrySaveSlot(boot, oldSlot, out _, out error));
                    StringAssert.Contains("失败", error);
                    Assert.IsFalse(manager.TryCopySlot(oldSlot, newSlot, out error));
                    Assert.IsFalse(manager.DeleteSlot(oldSlot));
                    Assert.AreEqual(40, manager.InspectSlot(oldSlot).SaltSilverBalance);

                    // Recovery restores normal save, copy, and delete semantics.
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

                verifiedRegions++;
            }

            Assert.AreEqual(4, verifiedRegions);
            Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Texture2D>().Length, texBefore);
            Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Sprite>().Length, spriteBefore);
        }
    }
}
