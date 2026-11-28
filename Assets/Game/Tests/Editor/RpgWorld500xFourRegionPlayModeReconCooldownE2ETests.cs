using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionPlayModeReconCooldownE2ETests
    {
        [Test]
        public void AllFourRegions_ReconHotkeyInvalidatesOldRegionPingsWithoutResettingCooldown()
        {
            IReadOnlyList<RpgExpandedMapSpec> specs = RpgExpandedWorldScale.AllSpecs;
            Assert.AreEqual(4, specs.Count);
            var host = new GameObject("FourRegionPlayModeReconCooldown");
            var ctrl = host.AddComponent<RpgWorld500xPlayModeController>();
            try
            {
                ctrl.Initialize(
                    overrideMap: specs[0].MapId,
                    saveStorage: new RpgWorld500xInMemorySaveSlotStorage(),
                    overrideTileSlices: new Sprite[] { null });

                var hud = ctrl.ReconPulseHud;
                Assert.IsNotNull(hud);
                Assert.IsTrue(hud.IsScanButtonInteractable);

                // Start the cooldown through the controller's actual R input path.
                ctrl.ProcessInputFrame(new RpgWorld500xInputFrame { ReconPulsePressed = true }, 0.25f);
                Assert.Greater(hud.LastPingCount, 0);
                Assert.IsFalse(hud.IsScanButtonInteractable);

                // Visit every other region, then return to the original region.
                // An early R press must neither retain old pings nor reset the timer.
                for (int i = 1; i <= specs.Count; i++)
                {
                    RpgWorldMapId nextMap = specs[i % specs.Count].MapId;
                    Assert.IsTrue(ctrl.Bootstrapper.Driver.SwitchMap(nextMap), nextMap.ToString());
                    ctrl.ProcessInputFrame(new RpgWorld500xInputFrame { ReconPulsePressed = true }, 1f);

                    Assert.AreEqual(nextMap, ctrl.Bootstrapper.Driver.Facade.CurrentMapId);
                    Assert.AreEqual(0, hud.LastPingCount, nextMap.ToString());
                    Assert.IsEmpty(hud.PingsText, nextMap.ToString());
                    StringAssert.Contains("重新勘测", hud.BannerText, nextMap.ToString());
                    Assert.IsFalse(hud.IsScanButtonInteractable, nextMap.ToString());
                    Assert.AreEqual("冷却中 (3s)", hud.ButtonLabelText, nextMap.ToString());

                    // Let the original cooldown expire, without a second clock or
                    // direct HUD mutation. The next R press scans the new region.
                    ctrl.ProcessInputFrame(new RpgWorld500xInputFrame(),
                        RpgWorld500xFieldReconScannerService.DefaultCooldownSeconds - 1f + 0.125f);
                    Assert.IsTrue(hud.IsScanButtonInteractable, nextMap.ToString());
                    ctrl.ProcessInputFrame(new RpgWorld500xInputFrame { ReconPulsePressed = true }, 0.125f);
                    Assert.Greater(hud.LastPingCount, 0, nextMap.ToString());
                    Assert.IsFalse(hud.IsScanButtonInteractable, nextMap.ToString());
                    Assert.IsFalse(string.IsNullOrEmpty(hud.PingsText), nextMap.ToString());
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
