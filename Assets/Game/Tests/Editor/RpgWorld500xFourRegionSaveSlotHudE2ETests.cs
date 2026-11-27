using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionSaveSlotHudE2ETests
    {
        private static readonly RpgWorldMapId[] FourRegions =
        {
            RpgWorldMapId.Soundwell,
            RpgWorldMapId.RedSaltHarbor,
            RpgWorldMapId.WhitewaterCrossing,
            RpgWorldMapId.MineCamp
        };

        [Test]
        public void FourRegion_SaveSlotHudPanel_SavesRestoresAndDeletesAcrossAllFourSlotsWithZeroSpriteLeak()
        {
            // Warm up one-time UGUI/font and per-region caches before steady-state leak baseline.
            var warmupGo = new GameObject("World500xFourRegionSaveSlotWarmupHost");
            var warmupCtrl = warmupGo.AddComponent<RpgWorld500xPlayModeController>();
            try
            {
                warmupCtrl.Initialize(
                    overrideMap: RpgWorldMapId.Soundwell,
                    saveStorage: new RpgWorld500xInMemorySaveSlotStorage(),
                    overrideTileSlices: new Sprite[] { null });
                for (int i = 1; i < FourRegions.Length; i++)
                {
                    Assert.IsTrue(warmupCtrl.Bootstrapper.SwitchMap(FourRegions[i]));
                }

                Assert.IsTrue(warmupCtrl.MenuRouter.OpenModal(RpgWorld500xModalKind.SaveSlots));
                warmupCtrl.MenuRouter.CloseAll();
            }
            finally
            {
                warmupCtrl.Shutdown();
                UnityEngine.Object.DestroyImmediate(warmupGo);
            }

            int spriteBefore = Resources.FindObjectsOfTypeAll<Sprite>().Length;

            var hostGo = new GameObject("World500xFourRegionSaveSlotE2EHost");
            var ctrl = hostGo.AddComponent<RpgWorld500xPlayModeController>();
            try
            {
                var storage = new RpgWorld500xInMemorySaveSlotStorage();
                var inspector = new RpgWorld500xSaveSlotManager(storage);
                List<RpgWorld500xSaveSlotSummary> initialSlots = inspector.ListAllSlots();
                Assert.AreEqual(FourRegions.Length, initialSlots.Count);

                ctrl.Initialize(
                    overrideMap: RpgWorldMapId.Soundwell,
                    saveStorage: storage,
                    overrideTileSlices: new Sprite[] { null });

                var boot = ctrl.Bootstrapper;
                var panel = ctrl.MenuRouter.SaveSlotPanel;

                Assert.IsTrue(ctrl.MenuRouter.OpenModal(RpgWorld500xModalKind.SaveSlots));
                Assert.IsTrue(panel.IsVisible);
                Assert.AreEqual(4, panel.DisplayedSlotRowCount);
                StringAssert.Contains("0/4", panel.HeaderText);

                int[] expectedBalances = new int[FourRegions.Length];
                for (int i = 0; i < FourRegions.Length; i++)
                {
                    if (boot.Driver.Facade.CurrentMapId != FourRegions[i])
                    {
                        Assert.IsTrue(boot.SwitchMap(FourRegions[i]));
                    }

                    boot.Driver.Facade.BountyAndSupply.AddSaltSilver(100);
                    expectedBalances[i] = boot.Driver.Facade.BountyAndSupply.SaltSilverBalance;

                    RpgWorld500xSaveSlotId slotId = initialSlots[i].SlotId;
                    Assert.IsTrue(panel.TryClickSave(slotId));
                    StringAssert.Contains("已保存至", panel.StatusText);
                    StringAssert.Contains((i + 1) + "/4", panel.HeaderText);
                    Assert.IsTrue(storage.TryRead(RpgWorld500xSaveSlotManager.GetStorageKey(slotId), out _));
                }

                // Restore all 4 regions in reverse order via SaveSlotHudPanel Load buttons.
                for (int i = FourRegions.Length - 1; i >= 0; i--)
                {
                    RpgWorld500xSaveSlotId slotId = initialSlots[i].SlotId;
                    Assert.IsTrue(panel.TryClickLoad(slotId));
                    StringAssert.Contains("已读取", panel.StatusText);
                    Assert.AreEqual(FourRegions[i], boot.Driver.Facade.CurrentMapId);
                    Assert.AreEqual(expectedBalances[i], boot.Driver.Facade.BountyAndSupply.SaltSilverBalance);
                }

                // Delete all 4 slots and confirm repeated delete returns false.
                for (int i = 0; i < FourRegions.Length; i++)
                {
                    RpgWorld500xSaveSlotId slotId = initialSlots[i].SlotId;
                    Assert.IsTrue(panel.TryClickDelete(slotId));
                    Assert.IsFalse(panel.TryClickDelete(slotId));
                    Assert.IsFalse(storage.TryRead(RpgWorld500xSaveSlotManager.GetStorageKey(slotId), out _));
                }

                StringAssert.Contains("0/4", panel.HeaderText);
                ctrl.MenuRouter.CloseAll();
                Assert.IsFalse(ctrl.MenuRouter.HasAnyModalOpen);
            }
            finally
            {
                ctrl.Shutdown();
                UnityEngine.Object.DestroyImmediate(hostGo);
            }

            Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Sprite>().Length, spriteBefore);
        }

        [Test]
        public void FourRegion_SaveSlotHudPanel_IsolatesCorruptSlotAndRecoversViaOverwriteAndDelete()
        {
            var hostGo = new GameObject("World500xFourRegionSaveSlotCorruptHost");
            var ctrl = hostGo.AddComponent<RpgWorld500xPlayModeController>();
            try
            {
                var storage = new RpgWorld500xInMemorySaveSlotStorage();
                string slot3Key = RpgWorld500xSaveSlotManager.GetStorageKey(RpgWorld500xSaveSlotId.Slot3);
                storage.Write(slot3Key, "{corrupt-payload");

                ctrl.Initialize(
                    overrideMap: RpgWorldMapId.Soundwell,
                    saveStorage: storage,
                    overrideTileSlices: new Sprite[] { null });

                var boot = ctrl.Bootstrapper;
                var panel = ctrl.MenuRouter.SaveSlotPanel;

                Assert.IsTrue(ctrl.MenuRouter.OpenModal(RpgWorld500xModalKind.SaveSlots));
                Assert.IsFalse(panel.TryClickLoad(RpgWorld500xSaveSlotId.Slot3));

                boot.Driver.Facade.BountyAndSupply.AddSaltSilver(125);
                int soundwellSilver = boot.Driver.Facade.BountyAndSupply.SaltSilverBalance;
                Assert.IsTrue(panel.TryClickSave(RpgWorld500xSaveSlotId.Slot1));

                Assert.IsTrue(boot.SwitchMap(RpgWorldMapId.WhitewaterCrossing));
                boot.Driver.Facade.BountyAndSupply.AddSaltSilver(125);
                int crossingSilver = boot.Driver.Facade.BountyAndSupply.SaltSilverBalance;
                Assert.IsTrue(panel.TryClickSave(RpgWorld500xSaveSlotId.Slot2));

                // Corrupt Slot3 is excluded from valid slot count and cannot be loaded.
                StringAssert.Contains("2/4", panel.HeaderText);
                Assert.IsFalse(panel.TryClickLoad(RpgWorld500xSaveSlotId.Slot3));

                // Valid slots still load cleanly across regions.
                Assert.IsTrue(panel.TryClickLoad(RpgWorld500xSaveSlotId.Slot1));
                Assert.AreEqual(RpgWorldMapId.Soundwell, boot.Driver.Facade.CurrentMapId);
                Assert.AreEqual(soundwellSilver, boot.Driver.Facade.BountyAndSupply.SaltSilverBalance);

                Assert.IsTrue(panel.TryClickLoad(RpgWorld500xSaveSlotId.Slot2));
                Assert.AreEqual(RpgWorldMapId.WhitewaterCrossing, boot.Driver.Facade.CurrentMapId);
                Assert.AreEqual(crossingSilver, boot.Driver.Facade.BountyAndSupply.SaltSilverBalance);

                // Overwriting corrupt Slot3 from MineCamp recovers the slot.
                Assert.IsTrue(boot.SwitchMap(RpgWorldMapId.MineCamp));
                boot.Driver.Facade.BountyAndSupply.AddSaltSilver(125);
                int mineCampSilver = boot.Driver.Facade.BountyAndSupply.SaltSilverBalance;
                Assert.IsTrue(panel.TryClickSave(RpgWorld500xSaveSlotId.Slot3));
                StringAssert.Contains("3/4", panel.HeaderText);

                Assert.IsTrue(boot.SwitchMap(RpgWorldMapId.RedSaltHarbor));
                Assert.IsTrue(panel.TryClickLoad(RpgWorld500xSaveSlotId.Slot3));
                Assert.AreEqual(RpgWorldMapId.MineCamp, boot.Driver.Facade.CurrentMapId);
                Assert.AreEqual(mineCampSilver, boot.Driver.Facade.BountyAndSupply.SaltSilverBalance);

                // Re-corrupt Slot3 and verify Refresh + Delete cleans it up.
                storage.Write(slot3Key, "{corrupt-again");
                panel.Refresh();
                Assert.IsFalse(panel.TryClickLoad(RpgWorld500xSaveSlotId.Slot3));
                Assert.IsTrue(panel.TryClickDelete(RpgWorld500xSaveSlotId.Slot3));
                Assert.IsFalse(storage.TryRead(slot3Key, out _));
                StringAssert.Contains("2/4", panel.HeaderText);
            }
            finally
            {
                ctrl.Shutdown();
                UnityEngine.Object.DestroyImmediate(hostGo);
            }
        }

        [Test]
        public void FourRegion_SaveSlotHudPanel_RejectsStaleSaveAndLoadAfterExternalRegionReplacement()
        {
            var hostGo = new GameObject("World500xFourRegionSaveSlotRevisionHost");
            var ctrl = hostGo.AddComponent<RpgWorld500xPlayModeController>();
            try
            {
                var storage = new RpgWorld500xInMemorySaveSlotStorage();
                DateTime fixedClock = new DateTime(2026, 11, 27, 12, 30, 0, DateTimeKind.Utc);
                var externalManager = new RpgWorld500xSaveSlotManager(storage, () => fixedClock);
                const RpgWorld500xSaveSlotId slot = RpgWorld500xSaveSlotId.Slot1;
                string slotKey = RpgWorld500xSaveSlotManager.GetStorageKey(slot);

                ctrl.Initialize(
                    overrideMap: RpgWorldMapId.Soundwell,
                    saveStorage: storage,
                    overrideTileSlices: new Sprite[] { null });

                var boot = ctrl.Bootstrapper;
                var panel = ctrl.MenuRouter.SaveSlotPanel;

                Assert.IsTrue(ctrl.MenuRouter.OpenModal(RpgWorld500xModalKind.SaveSlots));
                boot.Driver.Facade.BountyAndSupply.AddSaltSilver(90);
                int liveSilver = boot.Driver.Facade.BountyAndSupply.SaltSilverBalance;
                Assert.IsTrue(panel.TryClickSave(slot));

                // Externally replace Slot1 with RedSaltHarbor state while HUD still holds older revision.
                string harborPayload;
                using (var extBoot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.RedSaltHarbor,
                    overrideTileSlices: new Sprite[] { null }))
                {
                    extBoot.Driver.Facade.BountyAndSupply.AddSaltSilver(210);
                    Assert.IsTrue(externalManager.TrySaveSlot(extBoot, slot, out _, out string err), err);
                    Assert.IsTrue(storage.TryRead(slotKey, out harborPayload));
                }

                // Stale Save must be rejected without clobbering external RedSaltHarbor save.
                Assert.IsFalse(panel.TryClickSave(slot));
                StringAssert.Contains("档位已更新", panel.StatusText);
                Assert.IsTrue(storage.TryRead(slotKey, out string afterStaleSave));
                Assert.AreEqual(harborPayload, afterStaleSave);

                // Externally replace Slot1 again with WhitewaterCrossing state before Load.
                int crossingExternalSilver;
                using (var extBoot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.WhitewaterCrossing,
                    overrideTileSlices: new Sprite[] { null }))
                {
                    extBoot.Driver.Facade.BountyAndSupply.AddSaltSilver(340);
                    crossingExternalSilver = extBoot.Driver.Facade.BountyAndSupply.SaltSilverBalance;
                    Assert.IsTrue(externalManager.TrySaveSlot(extBoot, slot, out _, out string err), err);
                }

                // Stale Load must be rejected without altering live Soundwell state.
                Assert.IsFalse(panel.TryClickLoad(slot));
                StringAssert.Contains("档位已更新", panel.StatusText);
                Assert.AreEqual(RpgWorldMapId.Soundwell, boot.Driver.Facade.CurrentMapId);
                Assert.AreEqual(liveSilver, boot.Driver.Facade.BountyAndSupply.SaltSilverBalance);

                // After auto-refresh from rejected action, intentional Load succeeds and restores WhitewaterCrossing.
                Assert.IsTrue(panel.TryClickLoad(slot));
                Assert.AreEqual(RpgWorldMapId.WhitewaterCrossing, boot.Driver.Facade.CurrentMapId);
                Assert.AreEqual(crossingExternalSilver, boot.Driver.Facade.BountyAndSupply.SaltSilverBalance);
            }
            finally
            {
                ctrl.Shutdown();
                UnityEngine.Object.DestroyImmediate(hostGo);
            }
        }
    }
}
