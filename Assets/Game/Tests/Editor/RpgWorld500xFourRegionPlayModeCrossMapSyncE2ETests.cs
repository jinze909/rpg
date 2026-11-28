using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionPlayModeCrossMapSyncE2ETests
    {
        [Test]
        public void AllFourRegions_PlayModeControllerCrossMapTraversalSyncsHudWalletAndSaveRoundTrip()
        {
            IReadOnlyList<RpgExpandedMapSpec> specs = RpgExpandedWorldScale.AllSpecs;
            Assert.AreEqual(4, specs.Count);

            var host = new GameObject("FourRegionPlayModeCrossMapSync");
            var ctrl = host.AddComponent<RpgWorld500xPlayModeController>();
            try
            {
                ctrl.Initialize(
                    overrideMap: specs[0].MapId,
                    saveStorage: new RpgWorld500xInMemorySaveSlotStorage(),
                    overrideTileSlices: new Sprite[] { null });

                int expectedSilver = 0;
                foreach (RpgExpandedMapSpec spec in specs)
                {
                    if (ctrl.Bootstrapper.Driver.Facade.CurrentMapId != spec.MapId)
                    {
                        Assert.IsTrue(ctrl.Bootstrapper.Driver.SwitchMap(spec.MapId), spec.MapId.ToString());
                    }

                    ctrl.Bootstrapper.Driver.Facade.BountyAndSupply.AddSaltSilver(25);
                    expectedSilver += 25;

                    // Open and close a modal on each region to verify router + recon HUD stay synchronized.
                    Assert.IsTrue(ctrl.MenuRouter.OpenModal(RpgWorld500xModalKind.WorldAtlas), spec.MapId.ToString());
                    Assert.AreEqual(RpgWorld500xModalKind.WorldAtlas, ctrl.MenuRouter.ActiveModal, spec.MapId.ToString());
                    Assert.IsFalse(ctrl.ReconPulseHud.IsScanButtonInteractable, spec.MapId.ToString());

                    ctrl.ProcessInputFrame(new RpgWorld500xInputFrame { CloseModalPressed = true }, 0.1f);
                    Assert.AreEqual(RpgWorld500xModalKind.None, ctrl.MenuRouter.ActiveModal, spec.MapId.ToString());
                    Assert.IsFalse(ctrl.ReconPulseHud.IsScanButtonInteractable, spec.MapId.ToString());

                    ctrl.ProcessInputFrame(new RpgWorld500xInputFrame(), 0.1f);
                    Assert.IsTrue(ctrl.ReconPulseHud.IsScanButtonInteractable, spec.MapId.ToString());
                    Assert.AreEqual(spec.MapId, ctrl.Bootstrapper.Driver.Facade.CurrentMapId, spec.MapId.ToString());
                    Assert.AreEqual(
                        expectedSilver,
                        ctrl.Bootstrapper.Driver.Facade.BountyAndSupply.SaltSilverBalance,
                        spec.MapId.ToString());
                    Assert.IsFalse(string.IsNullOrEmpty(ctrl.ReconPulseHud.BannerText), spec.MapId.ToString());
                }

                // Export save snapshot after full 4-region sweep and verify restored state matches.
                RpgWorldMapId finalMap = ctrl.Bootstrapper.Driver.Facade.CurrentMapId;
                string saved = ctrl.Bootstrapper.ExportSaveJson();
                Assert.IsFalse(string.IsNullOrEmpty(saved));

                using (var restored = new RpgWorld500xSceneBootstrapper(
                    initialMapId: specs[0].MapId,
                    overrideTileSlices: new Sprite[] { null }))
                {
                    Assert.IsTrue(restored.TryImportSaveJson(saved, out string error), error);
                    Assert.AreEqual(finalMap, restored.Driver.Facade.CurrentMapId);
                    Assert.AreEqual(expectedSilver, restored.Driver.Facade.BountyAndSupply.SaltSilverBalance);
                }

                ctrl.Shutdown();
                Assert.AreEqual(0, host.transform.childCount);
            }
            finally
            {
                ctrl.Shutdown();
                Object.DestroyImmediate(host);
            }
        }
    }
}
