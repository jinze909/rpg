using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionPlayModeSupplyPurchaseGuardE2ETests
    {
        [Test]
        public void AllFourRegions_PlayModeControllerRejectsInsufficientOrInvalidSupplyPurchasesAndPreservesWallet()
        {
            IReadOnlyList<RpgExpandedMapSpec> specs = RpgExpandedWorldScale.AllSpecs;
            Assert.AreEqual(4, specs.Count);

            foreach (RpgExpandedMapSpec spec in specs)
            {
                var host = new GameObject("FourRegionSupplyPurchaseGuard_" + spec.MapId);
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

                    // 1. Insufficient balance purchase must fail cleanly without charging or granting stock.
                    Assert.AreEqual(0, shop.SaltSilverBalance, spec.MapId.ToString());
                    Assert.AreNotEqual(
                        RpgCampSupplyPurchaseStatus.Succeeded,
                        shop.TryPurchaseSupply("flare", 1),
                        spec.MapId.ToString());
                    Assert.AreEqual(0, shop.SaltSilverBalance, spec.MapId.ToString());
                    Assert.AreEqual(0, shop.GetOwnedSupplyCount("flare"), spec.MapId.ToString());

                    // 2. Invalid supply id and non-positive quantities must fail cleanly even when funded.
                    shop.AddSaltSilver(500);
                    Assert.AreEqual(500, shop.SaltSilverBalance, spec.MapId.ToString());
                    Assert.AreNotEqual(
                        RpgCampSupplyPurchaseStatus.Succeeded,
                        shop.TryPurchaseSupply("nonexistent_supply_id", 1),
                        spec.MapId.ToString());
                    Assert.AreNotEqual(
                        RpgCampSupplyPurchaseStatus.Succeeded,
                        shop.TryPurchaseSupply("flare", 0),
                        spec.MapId.ToString());
                    Assert.AreNotEqual(
                        RpgCampSupplyPurchaseStatus.Succeeded,
                        shop.TryPurchaseSupply("flare", -2),
                        spec.MapId.ToString());
                    Assert.AreEqual(500, shop.SaltSilverBalance, spec.MapId.ToString());
                    Assert.AreEqual(0, shop.GetOwnedSupplyCount("flare"), spec.MapId.ToString());

                    // 3. Valid purchase deducts wallet, saves to Slot1, and rolls back later spending on load.
                    Assert.AreEqual(
                        RpgCampSupplyPurchaseStatus.Succeeded,
                        shop.TryPurchaseSupply("flare", 2),
                        spec.MapId.ToString());
                    int afterFirstBuySilver = shop.SaltSilverBalance;
                    Assert.Less(afterFirstBuySilver, 500, spec.MapId.ToString());
                    Assert.AreEqual(2, shop.GetOwnedSupplyCount("flare"), spec.MapId.ToString());

                    Assert.IsTrue(
                        manager.TrySaveSlot(boot, RpgWorld500xSaveSlotId.Slot1, out _, out string saveErr),
                        saveErr);

                    Assert.AreEqual(
                        RpgCampSupplyPurchaseStatus.Succeeded,
                        shop.TryPurchaseSupply("flare", 1),
                        spec.MapId.ToString());
                    Assert.Less(shop.SaltSilverBalance, afterFirstBuySilver, spec.MapId.ToString());
                    Assert.AreEqual(3, shop.GetOwnedSupplyCount("flare"), spec.MapId.ToString());

                    Assert.IsTrue(
                        manager.TryLoadSlot(boot, RpgWorld500xSaveSlotId.Slot1, out _, out string loadErr),
                        loadErr);
                    ctrl.ProcessInputFrame(new RpgWorld500xInputFrame(), 0.05f);

                    Assert.AreEqual(afterFirstBuySilver, boot.Driver.Facade.BountyAndSupply.SaltSilverBalance, spec.MapId.ToString());
                    Assert.AreEqual(2, boot.Driver.Facade.BountyAndSupply.GetOwnedSupplyCount("flare"), spec.MapId.ToString());

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
