using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionSaveSlotBlankPayloadAndCopyGuardE2ETests
    {
        [Test]
        public void FourRegion_SaveSlotManager_IsolatesBlankPayloadsPreservesSiblingCheckpointsAndAllowsExplicitDeletion()
        {
            IReadOnlyList<RpgExpandedMapSpec> specs = RpgExpandedWorldScale.AllSpecs;
            Assert.AreEqual(4, specs.Count);

            string[] blankPayloads = { "", "   ", "\t\n" };
            const RpgWorld500xSaveSlotId damaged = RpgWorld500xSaveSlotId.Slot1;
            const RpgWorld500xSaveSlotId untouched = RpgWorld500xSaveSlotId.Slot2;
            string damagedKey = RpgWorld500xSaveSlotManager.GetStorageKey(damaged);

            for (int r = 0; r < specs.Count; r++)
            {
                RpgWorldMapId region = specs[r].MapId;
                var storage = new RpgWorld500xInMemorySaveSlotStorage();
                DateTime clock = new DateTime(2026, 11, 27, 17, 20, 0, DateTimeKind.Utc);
                var manager = new RpgWorld500xSaveSlotManager(storage, () => clock);

                using (var boot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: region,
                    overrideTileSlices: new Sprite[] { null }))
                {
                    boot.Driver.Facade.BountyAndSupply.AddSaltSilver(60 + (r * 30));
                    int expectedSilver = boot.Driver.Facade.BountyAndSupply.SaltSilverBalance;
                    Assert.IsTrue(manager.TrySaveSlot(boot, untouched, out _, out string saveErr), saveErr);

                    for (int p = 0; p < blankPayloads.Length; p++)
                    {
                        storage.Write(damagedKey, blankPayloads[p]);

                        RpgWorld500xSaveSlotSummary inspected = manager.InspectSlot(damaged);
                        Assert.IsTrue(inspected.IsOccupied, $"Blank payload must remain occupied on {region}.");
                        Assert.IsTrue(inspected.IsCorrupted, $"Blank payload must be flagged corrupt on {region}.");

                        Assert.IsFalse(manager.TryLoadSlot(boot, damaged, out RpgWorld500xSaveSlotSummary failedLoad, out _));
                        Assert.IsTrue(failedLoad.IsOccupied && failedLoad.IsCorrupted);

                        Assert.IsFalse(manager.TryCopySlot(damaged, untouched, out _));
                        RpgWorld500xSaveSlotSummary sibling = manager.InspectSlot(untouched);
                        Assert.AreEqual(region, sibling.MapId);
                        Assert.AreEqual(expectedSilver, sibling.SaltSilverBalance);

                        Assert.IsTrue(manager.TryFindMostRecentValidSlot(out RpgWorld500xSaveSlotSummary recent));
                        Assert.AreEqual(untouched, recent.SlotId);
                        Assert.AreEqual(region, recent.MapId);
                        Assert.AreEqual(expectedSilver, recent.SaltSilverBalance);

                        Assert.IsTrue(manager.DeleteSlot(damaged));
                        Assert.IsFalse(manager.InspectSlot(damaged).IsOccupied);
                    }
                }
            }
        }

        [Test]
        public void FourRegion_SaveSlotManager_OrdersRapidCheckpointsByUtcClockAndCopiesAcrossRegionsCleanly()
        {
            var storage = new RpgWorld500xInMemorySaveSlotStorage();
            DateTime clock = new DateTime(2026, 11, 27, 17, 25, 0, DateTimeKind.Utc);
            var manager = new RpgWorld500xSaveSlotManager(storage, () => clock);

            RpgWorld500xSaveSlotId[] manualSlots =
            {
                RpgWorld500xSaveSlotId.Slot1,
                RpgWorld500xSaveSlotId.Slot2,
                RpgWorld500xSaveSlotId.Slot3
            };
            RpgWorldMapId[] sequence =
            {
                RpgWorldMapId.Soundwell,
                RpgWorldMapId.RedSaltHarbor,
                RpgWorldMapId.WhitewaterCrossing,
                RpgWorldMapId.MineCamp
            };

            using (var boot = new RpgWorld500xSceneBootstrapper(
                initialMapId: sequence[0],
                overrideTileSlices: new Sprite[] { null }))
            {
                int latestMineCampSilver = 0;
                for (int i = 0; i < sequence.Length; i++)
                {
                    if (boot.Driver.Facade.CurrentMapId != sequence[i])
                    {
                        Assert.IsTrue(boot.SwitchMap(sequence[i]));
                    }

                    boot.Driver.Facade.BountyAndSupply.AddSaltSilver(50);
                    clock = clock.AddMilliseconds(150);
                    RpgWorld500xSaveSlotId targetSlot = manualSlots[i % manualSlots.Length];
                    Assert.IsTrue(manager.TrySaveSlot(boot, targetSlot, out _, out string err), err);

                    Assert.IsTrue(manager.TryFindMostRecentValidSlot(out RpgWorld500xSaveSlotSummary recent));
                    Assert.AreEqual(targetSlot, recent.SlotId);
                    Assert.AreEqual(sequence[i], recent.MapId);
                    if (sequence[i] == RpgWorldMapId.MineCamp)
                    {
                        latestMineCampSilver = boot.Driver.Facade.BountyAndSupply.SaltSilverBalance;
                    }
                }

                // Clone latest MineCamp save (Slot1) into Slot3, switch back to Soundwell, and restore from Slot3.
                Assert.IsTrue(manager.TryCopySlot(RpgWorld500xSaveSlotId.Slot1, RpgWorld500xSaveSlotId.Slot3, out string copyErr), copyErr);
                Assert.IsTrue(boot.SwitchMap(RpgWorldMapId.Soundwell));
                Assert.IsTrue(manager.TryLoadSlot(boot, RpgWorld500xSaveSlotId.Slot3, out _, out string loadErr), loadErr);
                Assert.AreEqual(RpgWorldMapId.MineCamp, boot.Driver.Facade.CurrentMapId);
                Assert.AreEqual(latestMineCampSilver, boot.Driver.Facade.BountyAndSupply.SaltSilverBalance);
            }
        }
    }
}
