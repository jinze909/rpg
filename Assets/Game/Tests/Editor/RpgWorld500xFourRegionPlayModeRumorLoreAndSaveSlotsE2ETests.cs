using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionPlayModeRumorLoreAndSaveSlotsE2ETests
    {
        [Test]
        public void AllFourRegions_PlayModeControllerDrivesRumorLoreSaveSlotsAndQuickSaveCleanly()
        {
            IReadOnlyList<RpgExpandedMapSpec> specs = RpgExpandedWorldScale.AllSpecs;
            Assert.AreEqual(4, specs.Count);

            foreach (RpgExpandedMapSpec spec in specs)
            {
                var host = new GameObject("FourRegionRumorLoreSaveSlots_" + spec.MapId);
                var ctrl = host.AddComponent<RpgWorld500xPlayModeController>();
                try
                {
                    ctrl.Initialize(
                        overrideMap: spec.MapId,
                        saveStorage: new RpgWorld500xInMemorySaveSlotStorage(),
                        overrideTileSlices: new Sprite[] { null });

                    // 1. Open FieldRumorAndLore via hotkey and verify dock sync + recon modal lock.
                    ctrl.ProcessInputFrame(new RpgWorld500xInputFrame
                    {
                        RequestedModalToggle = RpgWorld500xModalKind.FieldRumorAndLore
                    }, 0.05f);
                    Assert.AreEqual(RpgWorld500xModalKind.FieldRumorAndLore, ctrl.MenuRouter.ActiveModal, spec.MapId.ToString());
                    Assert.AreEqual(RpgWorld500xModalKind.FieldRumorAndLore, ctrl.QuickMenuDockHud.ActiveHighlightedModal, spec.MapId.ToString());
                    Assert.IsFalse(ctrl.ReconPulseHud.IsScanButtonInteractable, spec.MapId.ToString());

                    // 2. Switch directly to SaveSlots via hotkey and verify dock sync + modal lock.
                    ctrl.ProcessInputFrame(new RpgWorld500xInputFrame
                    {
                        RequestedModalToggle = RpgWorld500xModalKind.SaveSlots
                    }, 0.05f);
                    Assert.AreEqual(RpgWorld500xModalKind.SaveSlots, ctrl.MenuRouter.ActiveModal, spec.MapId.ToString());
                    Assert.AreEqual(RpgWorld500xModalKind.SaveSlots, ctrl.QuickMenuDockHud.ActiveHighlightedModal, spec.MapId.ToString());
                    Assert.IsFalse(ctrl.ReconPulseHud.IsScanButtonInteractable, spec.MapId.ToString());

                    // 3. Close SaveSlots via Escape (closing frame keeps open-world recon locked), then restore.
                    ctrl.ProcessInputFrame(new RpgWorld500xInputFrame { CloseModalPressed = true }, 0.05f);
                    Assert.AreEqual(RpgWorld500xModalKind.None, ctrl.MenuRouter.ActiveModal, spec.MapId.ToString());
                    Assert.AreEqual(RpgWorld500xModalKind.None, ctrl.QuickMenuDockHud.ActiveHighlightedModal, spec.MapId.ToString());
                    Assert.IsFalse(ctrl.ReconPulseHud.IsScanButtonInteractable, spec.MapId.ToString());

                    ctrl.ProcessInputFrame(new RpgWorld500xInputFrame(), 0.05f);
                    Assert.IsTrue(ctrl.ReconPulseHud.IsScanButtonInteractable, spec.MapId.ToString());

                    // 4. Trigger QuickAutoSavePressed and verify AutoSave slot is populated and reloadable.
                    ctrl.Bootstrapper.Driver.Facade.BountyAndSupply.AddSaltSilver(75);
                    ctrl.ProcessInputFrame(new RpgWorld500xInputFrame { QuickAutoSavePressed = true }, 0.05f);
                    StringAssert.Contains("自动存档成功", ctrl.LastAutoSaveStatusZh, spec.MapId.ToString());
                    Assert.IsTrue(
                        ctrl.SaveSlotManager.InspectSlot(RpgWorld500xSaveSlotId.AutoSave).IsOccupied,
                        spec.MapId.ToString());

                    ctrl.Bootstrapper.Driver.Facade.BountyAndSupply.AddSaltSilver(50);
                    Assert.IsTrue(
                        ctrl.SaveSlotManager.TryLoadSlot(
                            ctrl.Bootstrapper,
                            RpgWorld500xSaveSlotId.AutoSave,
                            out _,
                            out string loadErr),
                        loadErr);
                    Assert.AreEqual(75, ctrl.Bootstrapper.Driver.Facade.BountyAndSupply.SaltSilverBalance, spec.MapId.ToString());

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
