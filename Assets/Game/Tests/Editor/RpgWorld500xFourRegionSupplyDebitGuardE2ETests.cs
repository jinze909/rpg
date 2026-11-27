using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionSupplyDebitGuardE2ETests
    {
        [Test]
        public void AllFourRegions_FieldSupplyUseRequiresConfirmedDebitAndRecoversCleanlyWithZeroLeak()
        {
            // Warm up one-time runtime caches before steady-state leak baseline.
            using (var warmupBoot = new RpgWorld500xSceneBootstrapper(
                initialMapId: RpgWorldMapId.Soundwell,
                overrideTileSlices: new Sprite[] { null }))
            {
                Assert.AreEqual(RpgWorldMapId.Soundwell, warmupBoot.Driver.Facade.CurrentMapId);
            }

            int texBefore = Resources.FindObjectsOfTypeAll<Texture2D>().Length;
            int spriteBefore = Resources.FindObjectsOfTypeAll<Sprite>().Length;

            int verifiedRegions = 0;
            foreach (RpgExpandedMapSpec spec in RpgExpandedWorldScale.AllSpecs)
            {
                var store = new Dictionary<string, int>(StringComparer.Ordinal);
                Func<string, int> read = k => store.TryGetValue(k, out int v) ? v : 0;
                bool allowWrites = true;
                Action<string, int> gatedWrite = (k, v) =>
                {
                    if (allowWrites)
                    {
                        store[k] = v;
                    }
                };

                using (var boot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: spec.MapId,
                    overrideTileSlices: new Sprite[] { null }))
                {
                    boot.Driver.Initialize(
                        overrideMap: spec.MapId,
                        getInt: read,
                        setInt: gatedWrite,
                        overrideTileSlices: new Sprite[] { null });

                    var shop = boot.Driver.Facade.BountyAndSupply;
                    shop.AddSaltSilver(300);
                    Assert.AreEqual(RpgCampSupplyPurchaseStatus.Succeeded, shop.TryPurchaseSupply("gauze", 1));
                    Assert.AreEqual(RpgCampSupplyPurchaseStatus.Succeeded, shop.TryPurchaseSupply("lamp_oil", 2));

                    // Read-only adapter cannot award healing without confirming inventory debit.
                    int healed = 0;
                    var readOnly = new RpgWorld500xFieldSupplyUseService(
                        getInt: read, onPartyHealPercent: (_, pct) => healed += pct);
                    var rejectedHeal = readOnly.TryConsumeSupply("gauze", 10f);
                    Assert.AreEqual(RpgFieldSupplyUseStatus.InventoryWriteFailed, rejectedHeal.Status);
                    Assert.IsFalse(rejectedHeal.Succeeded);
                    Assert.AreEqual(1, rejectedHeal.RemainingOwnedCount);
                    Assert.AreEqual(0, rejectedHeal.PartyHealPercent);
                    Assert.AreEqual(0, healed);
                    Assert.AreEqual(1, shop.GetOwnedSupplyCount("gauze"));

                    // Gated write rejection prevents buff stacking until writes succeed again.
                    var service = new RpgWorld500xFieldSupplyUseService(read, gatedWrite);
                    Assert.IsTrue(service.TryConsumeSupply("lamp_oil", 10f).Succeeded);
                    Assert.AreEqual(1, shop.GetOwnedSupplyCount("lamp_oil"));

                    allowWrites = false;
                    var rejectedBuff = service.TryConsumeSupply("lamp_oil", 20f);
                    Assert.AreEqual(RpgFieldSupplyUseStatus.InventoryWriteFailed, rejectedBuff.Status);
                    Assert.AreEqual(1, rejectedBuff.RemainingOwnedCount);
                    Assert.AreEqual(0f, rejectedBuff.BuffDurationSeconds);
                    Assert.AreEqual(80f, service.GetRemainingBuffSeconds("lamp_oil", 20f), 0.01f);
                    Assert.AreEqual(1, shop.GetOwnedSupplyCount("lamp_oil"));

                    allowWrites = true;
                    Assert.IsTrue(service.TryConsumeSupply("lamp_oil", 20f).Succeeded);
                    Assert.AreEqual(0, shop.GetOwnedSupplyCount("lamp_oil"));
                    Assert.AreEqual(170f, service.GetRemainingBuffSeconds("lamp_oil", 20f), 0.01f);
                }

                verifiedRegions++;
            }

            Assert.AreEqual(4, verifiedRegions);
            Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Texture2D>().Length, texBefore);
            Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Sprite>().Length, spriteBefore);
        }
    }
}
