using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionPlayModeCrossRegionAutoSaveE2ETests
    {
        [Test]
        public void AllFourRegions_PlayModeControllerCrossRegionTravelAndAutoSaveRollbackPreserveWalletAndMap()
        {
            IReadOnlyList<RpgExpandedMapSpec> specs = RpgExpandedWorldScale.AllSpecs;
            Assert.AreEqual(4, specs.Count);

            for (int i = 0; i < specs.Count; i++)
            {
                RpgWorldMapId startMap = specs[i].MapId;
                RpgWorldMapId nextMap = specs[(i + 1) % specs.Count].MapId;

                var host = new GameObject("FourRegionCrossRegionAutoSave_" + startMap);
                var ctrl = host.AddComponent<RpgWorld500xPlayModeController>();
                try
                {
                    ctrl.Initialize(
                        overrideMap: startMap,
                        saveStorage: new RpgWorld500xInMemorySaveSlotStorage(),
                        overrideTileSlices: new Sprite[] { null });

                    var boot = ctrl.Bootstrapper;
                    var startShop = boot.Driver.Facade.BountyAndSupply;
                    startShop.AddSaltSilver(210);
                    Assert.AreEqual(
                        RpgCampSupplyPurchaseStatus.Succeeded,
                        startShop.TryPurchaseSupply("flare", 2),
                        startMap.ToString());
                    int baselineSilver = startShop.SaltSilverBalance;

                    ctrl.ProcessInputFrame(new RpgWorld500xInputFrame(), 0.05f);
                    Assert.Greater(boot.Driver.Facade.DistrictTracker.VisitedChunkCount, 0, startMap.ToString());

                    // 1. Travel to the next region and verify shared wallet/inventory survive the transition.
                    Assert.IsTrue(boot.SwitchMap(nextMap), startMap + "->" + nextMap);
                    ctrl.ProcessInputFrame(new RpgWorld500xInputFrame(), 0.05f);
                    Assert.AreEqual(nextMap, boot.Driver.Facade.CurrentMapId, startMap + "->" + nextMap);
                    Assert.AreEqual(baselineSilver, boot.Driver.Facade.BountyAndSupply.SaltSilverBalance, nextMap.ToString());
                    Assert.AreEqual(2, boot.Driver.Facade.BountyAndSupply.GetOwnedSupplyCount("flare"), nextMap.ToString());

                    // 2. Capture QuickAutoSave in nextMap.
                    ctrl.ProcessInputFrame(new RpgWorld500xInputFrame { QuickAutoSavePressed = true }, 0.05f);
                    StringAssert.Contains("自动存档成功", ctrl.LastAutoSaveStatusZh, nextMap.ToString());
                    Assert.IsTrue(
                        ctrl.SaveSlotManager.InspectSlot(RpgWorld500xSaveSlotId.AutoSave).IsOccupied,
                        nextMap.ToString());

                    // 3. Return to startMap, mutate wallet, then load AutoSave to verify cross-region rollback.
                    Assert.IsTrue(boot.SwitchMap(startMap), nextMap + "->" + startMap);
                    boot.Driver.Facade.BountyAndSupply.AddSaltSilver(300);
                    Assert.AreEqual(startMap, boot.Driver.Facade.CurrentMapId, startMap.ToString());

                    Assert.IsTrue(
                        ctrl.SaveSlotManager.TryLoadSlot(
                            boot,
                            RpgWorld500xSaveSlotId.AutoSave,
                            out _,
                            out string loadErr),
                        loadErr);
                    ctrl.ProcessInputFrame(new RpgWorld500xInputFrame(), 0.05f);

                    Assert.AreEqual(nextMap, boot.Driver.Facade.CurrentMapId, startMap + "->" + nextMap);
                    Assert.AreEqual(baselineSilver, boot.Driver.Facade.BountyAndSupply.SaltSilverBalance, nextMap.ToString());
                    Assert.AreEqual(2, boot.Driver.Facade.BountyAndSupply.GetOwnedSupplyCount("flare"), nextMap.ToString());

                    ctrl.Shutdown();
                    Assert.AreEqual(0, host.transform.childCount, startMap.ToString());
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
