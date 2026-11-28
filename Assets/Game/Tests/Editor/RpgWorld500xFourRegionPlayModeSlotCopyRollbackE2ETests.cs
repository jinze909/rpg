using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionPlayModeSlotCopyRollbackE2ETests
    {
        [Test]
        public void AllFourRegions_PlayModeControllerSlotCopyEmptyLoadGuardAndShortcutPersistenceWork()
        {
            IReadOnlyList<RpgExpandedMapSpec> specs = RpgExpandedWorldScale.AllSpecs;
            Assert.AreEqual(4, specs.Count);

            foreach (RpgExpandedMapSpec spec in specs)
            {
                var host = new GameObject("FourRegionSlotCopyRollback_" + spec.MapId);
                var ctrl = host.AddComponent<RpgWorld500xPlayModeController>();
                try
                {
                    ctrl.Initialize(
                        overrideMap: spec.MapId,
                        saveStorage: new RpgWorld500xInMemorySaveSlotStorage(),
                        overrideTileSlices: new Sprite[] { null });

                    var boot = ctrl.Bootstrapper;
                    var facade = boot.Driver.Facade;
                    var shop = facade.BountyAndSupply;
                    var manager = ctrl.SaveSlotManager;

                    // 1. Loading an empty slot must fail cleanly without disturbing live state.
                    shop.AddSaltSilver(150);
                    Assert.AreEqual(
                        RpgCampSupplyPurchaseStatus.Succeeded,
                        shop.TryPurchaseSupply("flare", 2),
                        spec.MapId.ToString());
                    int baselineSilver = shop.SaltSilverBalance;
                    Assert.IsFalse(manager.InspectSlot(RpgWorld500xSaveSlotId.Slot2).IsOccupied, spec.MapId.ToString());
                    Assert.IsFalse(
                        manager.TryLoadSlot(boot, RpgWorld500xSaveSlotId.Slot2, out _, out _),
                        spec.MapId.ToString());
                    Assert.AreEqual(baselineSilver, shop.SaltSilverBalance, spec.MapId.ToString());
                    Assert.AreEqual(2, shop.GetOwnedSupplyCount("flare"), spec.MapId.ToString());

                    // 2. Unlock first regional shortcut and save checkpoint into Slot1.
                    IReadOnlyList<RpgWorldShortcutDef> shortcuts = RpgWorld500xContentCatalog.GetShortcuts(spec.MapId);
                    Assert.Greater(shortcuts.Count, 0, spec.MapId.ToString());
                    var shortcutPos = new Vector2(
                        shortcuts[0].TriggerTile.x + 0.5f,
                        shortcuts[0].TriggerTile.y + 0.5f);
                    Assert.IsTrue(
                        facade.ActivityDirector.CachesAndShortcuts.TryUnlockNearbyShortcut(shortcutPos, 6f, out _),
                        spec.MapId.ToString());
                    Assert.IsTrue(
                        facade.ActivityDirector.CachesAndShortcuts.IsShortcutUnlocked(shortcuts[0].Id),
                        spec.MapId.ToString());

                    Assert.IsTrue(
                        manager.TrySaveSlot(boot, RpgWorld500xSaveSlotId.Slot1, out _, out string saveErr),
                        saveErr);

                    // 3. Copy Slot1 -> Slot3, delete Slot1, mutate live wallet/stock, then restore from Slot3.
                    Assert.IsTrue(
                        manager.TryCopySlot(RpgWorld500xSaveSlotId.Slot1, RpgWorld500xSaveSlotId.Slot3, out string copyErr),
                        copyErr);
                    Assert.IsTrue(manager.DeleteSlot(RpgWorld500xSaveSlotId.Slot1), spec.MapId.ToString());
                    Assert.IsFalse(manager.InspectSlot(RpgWorld500xSaveSlotId.Slot1).IsOccupied, spec.MapId.ToString());
                    Assert.IsTrue(manager.InspectSlot(RpgWorld500xSaveSlotId.Slot3).IsOccupied, spec.MapId.ToString());

                    shop.AddSaltSilver(400);
                    Assert.IsTrue(ctrl.SupplyHotbarHud.TryClickUseSupply("flare", 10f), spec.MapId.ToString());
                    Assert.AreEqual(1, shop.GetOwnedSupplyCount("flare"), spec.MapId.ToString());

                    Assert.IsTrue(
                        manager.TryLoadSlot(boot, RpgWorld500xSaveSlotId.Slot3, out _, out string loadErr),
                        loadErr);
                    ctrl.ProcessInputFrame(new RpgWorld500xInputFrame(), 0.1f);

                    Assert.AreEqual(spec.MapId, facade.CurrentMapId, spec.MapId.ToString());
                    Assert.AreEqual(baselineSilver, shop.SaltSilverBalance, spec.MapId.ToString());
                    Assert.AreEqual(2, shop.GetOwnedSupplyCount("flare"), spec.MapId.ToString());
                    Assert.IsTrue(
                        facade.ActivityDirector.CachesAndShortcuts.IsShortcutUnlocked(shortcuts[0].Id),
                        spec.MapId.ToString());

                    ctrl.Shutdown();
                    Assert.AreEqual(0, host.transform.childCount, spec.MapId.ToString());
                }
                finally
                {
                    ctrl.Shutdown();
                    Object.DestroyImmediate(host);
                }
            }
        }
    }
}
