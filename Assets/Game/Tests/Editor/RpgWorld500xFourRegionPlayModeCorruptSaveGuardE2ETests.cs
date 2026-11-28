using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionPlayModeCorruptSaveGuardE2ETests
    {
        [Test]
        public void AllFourRegions_PlayModeControllerRejectsCorruptSaveImportsWithoutMutatingLiveState()
        {
            IReadOnlyList<RpgExpandedMapSpec> specs = RpgExpandedWorldScale.AllSpecs;
            Assert.AreEqual(4, specs.Count);

            foreach (RpgExpandedMapSpec spec in specs)
            {
                var host = new GameObject("FourRegionCorruptSaveGuard_" + spec.MapId);
                var ctrl = host.AddComponent<RpgWorld500xPlayModeController>();
                try
                {
                    ctrl.Initialize(
                        overrideMap: spec.MapId,
                        saveStorage: new RpgWorld500xInMemorySaveSlotStorage(),
                        overrideTileSlices: new Sprite[] { null });

                    var boot = ctrl.Bootstrapper;
                    var shop = boot.Driver.Facade.BountyAndSupply;
                    shop.AddSaltSilver(160);
                    Assert.AreEqual(
                        RpgCampSupplyPurchaseStatus.Succeeded,
                        shop.TryPurchaseSupply("flare", 1),
                        spec.MapId.ToString());
                    int baselineSilver = shop.SaltSilverBalance;

                    string validJson = boot.ExportSaveJson();
                    Assert.IsNotEmpty(validJson, spec.MapId.ToString());

                    // 1. Empty, malformed, and tampered JSON snapshots must be rejected cleanly.
                    Assert.IsFalse(boot.TryImportSaveJson(string.Empty, out _), spec.MapId.ToString());
                    Assert.IsFalse(boot.TryImportSaveJson("{not-valid-json", out _), spec.MapId.ToString());
                    Assert.IsFalse(boot.TryImportSaveJson(validJson + "tampered", out _), spec.MapId.ToString());

                    Assert.AreEqual(spec.MapId, boot.Driver.Facade.CurrentMapId, spec.MapId.ToString());
                    Assert.AreEqual(baselineSilver, boot.Driver.Facade.BountyAndSupply.SaltSilverBalance, spec.MapId.ToString());
                    Assert.AreEqual(1, boot.Driver.Facade.BountyAndSupply.GetOwnedSupplyCount("flare"), spec.MapId.ToString());

                    // 2. Mutate live state and confirm intact snapshot still imports cleanly.
                    shop.AddSaltSilver(320);
                    Assert.Greater(boot.Driver.Facade.BountyAndSupply.SaltSilverBalance, baselineSilver, spec.MapId.ToString());

                    Assert.IsTrue(boot.TryImportSaveJson(validJson, out string importErr), importErr);
                    ctrl.ProcessInputFrame(new RpgWorld500xInputFrame(), 0.05f);

                    Assert.AreEqual(spec.MapId, boot.Driver.Facade.CurrentMapId, spec.MapId.ToString());
                    Assert.AreEqual(baselineSilver, boot.Driver.Facade.BountyAndSupply.SaltSilverBalance, spec.MapId.ToString());
                    Assert.AreEqual(1, boot.Driver.Facade.BountyAndSupply.GetOwnedSupplyCount("flare"), spec.MapId.ToString());

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
