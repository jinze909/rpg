using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionPlayModeShortcutOverwriteE2ETests
    {
        [Test]
        public void AllFourRegions_PlayModeControllerShortcutIdempotencySlotOverwriteAndModalSyncWork()
        {
            IReadOnlyList<RpgExpandedMapSpec> specs = RpgExpandedWorldScale.AllSpecs;
            Assert.AreEqual(4, specs.Count);

            foreach (RpgExpandedMapSpec spec in specs)
            {
                var host = new GameObject("FourRegionShortcutOverwrite_" + spec.MapId);
                var ctrl = host.AddComponent<RpgWorld500xPlayModeController>();
                try
                {
                    ctrl.Initialize(
                        overrideMap: spec.MapId,
                        saveStorage: new RpgWorld500xInMemorySaveSlotStorage(),
                        overrideTileSlices: new Sprite[] { null });

                    var boot = ctrl.Bootstrapper;
                    var facade = boot.Driver.Facade;
                    var cachesSvc = facade.ActivityDirector.CachesAndShortcuts;
                    var shop = facade.BountyAndSupply;
                    var manager = ctrl.SaveSlotManager;

                    // 1. Unlock first regional shortcut once and verify idempotent duplicate guard.
                    IReadOnlyList<RpgWorldShortcutDef> shortcuts = RpgWorld500xContentCatalog.GetShortcuts(spec.MapId);
                    Assert.Greater(shortcuts.Count, 0, spec.MapId.ToString());
                    var shortcutPos = new Vector2(
                        shortcuts[0].TriggerTile.x + 0.5f,
                        shortcuts[0].TriggerTile.y + 0.5f);

                    Assert.IsFalse(cachesSvc.IsShortcutUnlocked(shortcuts[0].Id), spec.MapId.ToString());
                    Assert.IsTrue(cachesSvc.TryUnlockNearbyShortcut(shortcutPos, 1.5f, out _), spec.MapId.ToString());
                    Assert.IsTrue(cachesSvc.IsShortcutUnlocked(shortcuts[0].Id), spec.MapId.ToString());
                    Assert.IsFalse(cachesSvc.TryUnlockNearbyShortcut(shortcutPos, 1.5f, out _), spec.MapId.ToString());

                    // 2. Save initial Slot1 checkpoint, advance wallet/supply, then overwrite Slot1.
                    shop.AddSaltSilver(120);
                    Assert.AreEqual(
                        RpgCampSupplyPurchaseStatus.Succeeded,
                        shop.TryPurchaseSupply("flare", 1),
                        spec.MapId.ToString());
                    Assert.IsTrue(
                        manager.TrySaveSlot(boot, RpgWorld500xSaveSlotId.Slot1, out _, out string firstSaveErr),
                        firstSaveErr);

                    shop.AddSaltSilver(240);
                    Assert.AreEqual(
                        RpgCampSupplyPurchaseStatus.Succeeded,
                        shop.TryPurchaseSupply("flare", 2),
                        spec.MapId.ToString());
                    int overwrittenSilver = shop.SaltSilverBalance;
                    Assert.AreEqual(3, shop.GetOwnedSupplyCount("flare"), spec.MapId.ToString());
                    Assert.IsTrue(
                        manager.TrySaveSlot(boot, RpgWorld500xSaveSlotId.Slot1, out _, out string overwriteSaveErr),
                        overwriteSaveErr);
                    Assert.IsTrue(manager.InspectSlot(RpgWorld500xSaveSlotId.Slot1).IsOccupied, spec.MapId.ToString());

                    // 3. Mutate live state and reload Slot1; verify overwritten checkpoint restores cleanly.
                    shop.AddSaltSilver(500);
                    Assert.IsTrue(ctrl.SupplyHotbarHud.TryClickUseSupply("flare", 10f), spec.MapId.ToString());
                    Assert.AreEqual(2, shop.GetOwnedSupplyCount("flare"), spec.MapId.ToString());

                    Assert.IsTrue(
                        manager.TryLoadSlot(boot, RpgWorld500xSaveSlotId.Slot1, out _, out string loadErr),
                        loadErr);
                    ctrl.ProcessInputFrame(new RpgWorld500xInputFrame(), 0.05f);

                    Assert.AreEqual(spec.MapId, facade.CurrentMapId, spec.MapId.ToString());
                    Assert.AreEqual(overwrittenSilver, shop.SaltSilverBalance, spec.MapId.ToString());
                    Assert.AreEqual(3, shop.GetOwnedSupplyCount("flare"), spec.MapId.ToString());
                    Assert.IsTrue(cachesSvc.IsShortcutUnlocked(shortcuts[0].Id), spec.MapId.ToString());
                    Assert.IsFalse(cachesSvc.TryUnlockNearbyShortcut(shortcutPos, 1.5f, out _), spec.MapId.ToString());

                    // 4. Open CacheAndShortcut modal, verify dock sync + recon lock, and close cleanly.
                    ctrl.ProcessInputFrame(new RpgWorld500xInputFrame
                    {
                        RequestedModalToggle = RpgWorld500xModalKind.CacheAndShortcut
                    }, 0.05f);
                    Assert.AreEqual(RpgWorld500xModalKind.CacheAndShortcut, ctrl.MenuRouter.ActiveModal, spec.MapId.ToString());
                    Assert.AreEqual(RpgWorld500xModalKind.CacheAndShortcut, ctrl.QuickMenuDockHud.ActiveHighlightedModal, spec.MapId.ToString());
                    Assert.IsFalse(ctrl.ReconPulseHud.IsScanButtonInteractable, spec.MapId.ToString());

                    ctrl.ProcessInputFrame(new RpgWorld500xInputFrame { CloseModalPressed = true }, 0.05f);
                    Assert.AreEqual(RpgWorld500xModalKind.None, ctrl.MenuRouter.ActiveModal, spec.MapId.ToString());
                    Assert.AreEqual(RpgWorld500xModalKind.None, ctrl.QuickMenuDockHud.ActiveHighlightedModal, spec.MapId.ToString());
                    Assert.IsFalse(ctrl.ReconPulseHud.IsScanButtonInteractable, spec.MapId.ToString());

                    ctrl.ProcessInputFrame(new RpgWorld500xInputFrame(), 0.05f);
                    Assert.IsTrue(ctrl.ReconPulseHud.IsScanButtonInteractable, spec.MapId.ToString());

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
