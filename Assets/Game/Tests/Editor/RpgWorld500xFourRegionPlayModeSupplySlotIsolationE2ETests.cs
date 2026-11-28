using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionPlayModeSupplySlotIsolationE2ETests
    {
        [Test]
        public void AllFourRegions_PlayModeControllerSupplyZeroStockGuardAndMultiSlotIsolationWork()
        {
            IReadOnlyList<RpgExpandedMapSpec> specs = RpgExpandedWorldScale.AllSpecs;
            Assert.AreEqual(4, specs.Count);

            foreach (RpgExpandedMapSpec spec in specs)
            {
                var host = new GameObject("FourRegionSupplySlotIsolation_" + spec.MapId);
                var ctrl = host.AddComponent<RpgWorld500xPlayModeController>();
                try
                {
                    ctrl.Initialize(
                        overrideMap: spec.MapId,
                        saveStorage: new RpgWorld500xInMemorySaveSlotStorage(),
                        overrideTileSlices: new Sprite[] { null });

                    var boot = ctrl.Bootstrapper;
                    var shop = boot.Driver.Facade.BountyAndSupply;
                    var manager = ctrl.SaveSlotManager;

                    // 1. Zero-stock hotbar use must fail cleanly without going negative.
                    Assert.AreEqual(0, shop.GetOwnedSupplyCount("flare"), spec.MapId.ToString());
                    Assert.IsFalse(ctrl.SupplyHotbarHud.TryClickUseSupply("flare", 10f), spec.MapId.ToString());
                    Assert.AreEqual(0, shop.GetOwnedSupplyCount("flare"), spec.MapId.ToString());

                    // 2. Save two independent checkpoints into Slot1 (1 flare) and Slot2 (3 flares).
                    shop.AddSaltSilver(150);
                    Assert.AreEqual(
                        RpgCampSupplyPurchaseStatus.Succeeded,
                        shop.TryPurchaseSupply("flare", 1),
                        spec.MapId.ToString());
                    int slot1Silver = shop.SaltSilverBalance;
                    Assert.IsTrue(
                        manager.TrySaveSlot(boot, RpgWorld500xSaveSlotId.Slot1, out _, out string slot1Err),
                        slot1Err);

                    shop.AddSaltSilver(250);
                    Assert.AreEqual(
                        RpgCampSupplyPurchaseStatus.Succeeded,
                        shop.TryPurchaseSupply("flare", 2),
                        spec.MapId.ToString());
                    int slot2Silver = shop.SaltSilverBalance;
                    Assert.AreEqual(3, shop.GetOwnedSupplyCount("flare"), spec.MapId.ToString());
                    Assert.IsTrue(
                        manager.TrySaveSlot(boot, RpgWorld500xSaveSlotId.Slot2, out _, out string slot2Err),
                        slot2Err);

                    // 3. Consume a flare, bounce Slot1 <-> Slot2, and verify exact isolated restoration.
                    Assert.IsTrue(ctrl.SupplyHotbarHud.TryClickUseSupply("flare", 20f), spec.MapId.ToString());
                    Assert.AreEqual(2, shop.GetOwnedSupplyCount("flare"), spec.MapId.ToString());

                    Assert.IsTrue(
                        manager.TryLoadSlot(boot, RpgWorld500xSaveSlotId.Slot1, out _, out string load1Err),
                        load1Err);
                    ctrl.ProcessInputFrame(new RpgWorld500xInputFrame(), 0.05f);
                    Assert.AreEqual(slot1Silver, boot.Driver.Facade.BountyAndSupply.SaltSilverBalance, spec.MapId.ToString());
                    Assert.AreEqual(1, boot.Driver.Facade.BountyAndSupply.GetOwnedSupplyCount("flare"), spec.MapId.ToString());

                    Assert.IsTrue(
                        manager.TryLoadSlot(boot, RpgWorld500xSaveSlotId.Slot2, out _, out string load2Err),
                        load2Err);
                    ctrl.ProcessInputFrame(new RpgWorld500xInputFrame(), 0.05f);
                    Assert.AreEqual(slot2Silver, boot.Driver.Facade.BountyAndSupply.SaltSilverBalance, spec.MapId.ToString());
                    Assert.AreEqual(3, boot.Driver.Facade.BountyAndSupply.GetOwnedSupplyCount("flare"), spec.MapId.ToString());

                    Assert.IsTrue(ctrl.SupplyHotbarHud.TryClickUseSupply("flare", 120f), spec.MapId.ToString());
                    Assert.AreEqual(2, boot.Driver.Facade.BountyAndSupply.GetOwnedSupplyCount("flare"), spec.MapId.ToString());

                    // 4. Deleting Slot2 must not affect Slot1 occupancy.
                    Assert.IsTrue(manager.DeleteSlot(RpgWorld500xSaveSlotId.Slot2), spec.MapId.ToString());
                    Assert.IsFalse(manager.InspectSlot(RpgWorld500xSaveSlotId.Slot2).IsOccupied, spec.MapId.ToString());
                    Assert.IsTrue(manager.InspectSlot(RpgWorld500xSaveSlotId.Slot1).IsOccupied, spec.MapId.ToString());

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
