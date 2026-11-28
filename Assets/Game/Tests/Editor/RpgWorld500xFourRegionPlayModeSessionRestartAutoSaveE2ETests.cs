using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionPlayModeSessionRestartAutoSaveE2ETests
    {
        [Test]
        public void AllFourRegions_PlayModeControllerPersistsQuickAutoSaveAcrossSessionRestartAndIdempotentShutdown()
        {
            IReadOnlyList<RpgExpandedMapSpec> specs = RpgExpandedWorldScale.AllSpecs;
            Assert.AreEqual(4, specs.Count);

            foreach (RpgExpandedMapSpec spec in specs)
            {
                var sharedStorage = new RpgWorld500xInMemorySaveSlotStorage();
                int savedSilver;

                // 1. Run first Play Mode session: earn silver, buy supplies, trigger QuickAutoSave, shut down twice.
                var firstHost = new GameObject("FourRegionSessionRestartFirst_" + spec.MapId);
                var firstCtrl = firstHost.AddComponent<RpgWorld500xPlayModeController>();
                try
                {
                    firstCtrl.Initialize(
                        overrideMap: spec.MapId,
                        saveStorage: sharedStorage,
                        overrideTileSlices: new Sprite[] { null });

                    var firstShop = firstCtrl.Bootstrapper.Driver.Facade.BountyAndSupply;
                    firstShop.AddSaltSilver(180);
                    Assert.AreEqual(
                        RpgCampSupplyPurchaseStatus.Succeeded,
                        firstShop.TryPurchaseSupply("flare", 2),
                        spec.MapId.ToString());
                    savedSilver = firstShop.SaltSilverBalance;
                    Assert.AreEqual(2, firstShop.GetOwnedSupplyCount("flare"), spec.MapId.ToString());

                    firstCtrl.ProcessInputFrame(new RpgWorld500xInputFrame { QuickAutoSavePressed = true }, 0.05f);
                    StringAssert.Contains("自动存档成功", firstCtrl.LastAutoSaveStatusZh, spec.MapId.ToString());
                    Assert.IsTrue(
                        firstCtrl.SaveSlotManager.InspectSlot(RpgWorld500xSaveSlotId.AutoSave).IsOccupied,
                        spec.MapId.ToString());

                    firstCtrl.Shutdown();
                    Assert.AreEqual(0, firstHost.transform.childCount, spec.MapId.ToString());
                    // Second Shutdown call must remain idempotent.
                    firstCtrl.Shutdown();
                    Assert.AreEqual(0, firstHost.transform.childCount, spec.MapId.ToString());
                }
                finally
                {
                    firstCtrl.Shutdown();
                    Object.DestroyImmediate(firstHost);
                }

                // 2. Boot a fresh Play Mode session with the same storage and restore AutoSave.
                var secondHost = new GameObject("FourRegionSessionRestartSecond_" + spec.MapId);
                var secondCtrl = secondHost.AddComponent<RpgWorld500xPlayModeController>();
                try
                {
                    secondCtrl.Initialize(
                        overrideMap: spec.MapId,
                        saveStorage: sharedStorage,
                        overrideTileSlices: new Sprite[] { null });

                    Assert.IsTrue(
                        secondCtrl.SaveSlotManager.InspectSlot(RpgWorld500xSaveSlotId.AutoSave).IsOccupied,
                        spec.MapId.ToString());
                    Assert.IsTrue(
                        secondCtrl.SaveSlotManager.TryLoadSlot(
                            secondCtrl.Bootstrapper,
                            RpgWorld500xSaveSlotId.AutoSave,
                            out _,
                            out string loadErr),
                        loadErr);

                    secondCtrl.ProcessInputFrame(new RpgWorld500xInputFrame(), 0.05f);

                    var secondFacade = secondCtrl.Bootstrapper.Driver.Facade;
                    Assert.AreEqual(spec.MapId, secondFacade.CurrentMapId, spec.MapId.ToString());
                    Assert.AreEqual(savedSilver, secondFacade.BountyAndSupply.SaltSilverBalance, spec.MapId.ToString());
                    Assert.AreEqual(2, secondFacade.BountyAndSupply.GetOwnedSupplyCount("flare"), spec.MapId.ToString());

                    // 3. Verify HUD modal routing and recon lock remain functional after AutoSave reload.
                    secondCtrl.ProcessInputFrame(new RpgWorld500xInputFrame
                    {
                        RequestedModalToggle = RpgWorld500xModalKind.WorldAtlas
                    }, 0.05f);
                    Assert.AreEqual(RpgWorld500xModalKind.WorldAtlas, secondCtrl.MenuRouter.ActiveModal, spec.MapId.ToString());
                    Assert.AreEqual(RpgWorld500xModalKind.WorldAtlas, secondCtrl.QuickMenuDockHud.ActiveHighlightedModal, spec.MapId.ToString());
                    Assert.IsFalse(secondCtrl.ReconPulseHud.IsScanButtonInteractable, spec.MapId.ToString());

                    secondCtrl.ProcessInputFrame(new RpgWorld500xInputFrame { CloseModalPressed = true }, 0.05f);
                    Assert.AreEqual(RpgWorld500xModalKind.None, secondCtrl.MenuRouter.ActiveModal, spec.MapId.ToString());
                    Assert.IsFalse(secondCtrl.ReconPulseHud.IsScanButtonInteractable, spec.MapId.ToString());

                    secondCtrl.ProcessInputFrame(new RpgWorld500xInputFrame(), 0.05f);
                    Assert.IsTrue(secondCtrl.ReconPulseHud.IsScanButtonInteractable, spec.MapId.ToString());

                    secondCtrl.Shutdown();
                    Assert.AreEqual(0, secondHost.transform.childCount, spec.MapId.ToString());
                }
                finally
                {
                    secondCtrl.Shutdown();
                    Object.DestroyImmediate(secondHost);
                }
            }
        }
    }
}
