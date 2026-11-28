using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionPlayModeModalInputGuardE2ETests
    {
        [Test]
        public void AllFourRegions_PlayModeControllerReconMouseAndPublicClickRespectModalAndClosingFrame()
        {
            IReadOnlyList<RpgExpandedMapSpec> specs = RpgExpandedWorldScale.AllSpecs;
            Assert.AreEqual(4, specs.Count);

            foreach (RpgExpandedMapSpec spec in specs)
            {
                var host = new GameObject("FourRegionReconModalGuard_" + spec.MapId);
                var ctrl = host.AddComponent<RpgWorld500xPlayModeController>();
                try
                {
                    ctrl.Initialize(
                        overrideMap: spec.MapId,
                        saveStorage: new RpgWorld500xInMemorySaveSlotStorage(),
                        overrideTileSlices: new Sprite[] { null });

                    var hud = ctrl.ReconPulseHud;
                    Assert.IsNotNull(hud, spec.MapId.ToString());
                    var button = host.transform
                        .Find("World500xPlayModeUiRoot/World500xReconPulseHudBar/ReconPulsePanel/PulseScanButton")
                        .GetComponent<Button>();
                    Assert.IsNotNull(button, spec.MapId.ToString());

                    string banner = hud.BannerText;
                    Assert.IsTrue(hud.IsScanButtonInteractable, spec.MapId.ToString());

                    // 1. Opening via menu router immediately blocks recon mouse & public API before next frame.
                    Assert.IsTrue(ctrl.MenuRouter.OpenModal(RpgWorld500xModalKind.WorldAtlas), spec.MapId.ToString());
                    Assert.IsFalse(hud.IsScanButtonInteractable, spec.MapId.ToString());
                    button.onClick.Invoke();
                    Assert.IsFalse(hud.TryClickPulseScan(0f), spec.MapId.ToString());
                    Assert.AreEqual(banner, hud.BannerText, spec.MapId.ToString());
                    Assert.AreEqual(0, hud.LastPingCount, spec.MapId.ToString());

                    // 2. Hotkey press while modal is open keeps scan button non-interactable and blocked.
                    ctrl.ProcessInputFrame(new RpgWorld500xInputFrame { ReconPulsePressed = true }, 0.1f);
                    Assert.IsFalse(button.interactable, spec.MapId.ToString());
                    button.onClick.Invoke();
                    Assert.AreEqual(banner, hud.BannerText, spec.MapId.ToString());
                    Assert.AreEqual(0, hud.LastPingCount, spec.MapId.ToString());

                    // 3. Closing-frame guard: Escape + R closes modal without leaking scan on the same frame.
                    ctrl.ProcessInputFrame(new RpgWorld500xInputFrame
                    {
                        CloseModalPressed = true,
                        ReconPulsePressed = true
                    }, 0.1f);
                    Assert.AreEqual(RpgWorld500xModalKind.None, ctrl.MenuRouter.ActiveModal, spec.MapId.ToString());
                    Assert.IsFalse(hud.IsScanButtonInteractable, spec.MapId.ToString());
                    button.onClick.Invoke();
                    Assert.IsFalse(hud.TryClickPulseScan(0.2f), spec.MapId.ToString());
                    Assert.AreEqual(0, hud.LastPingCount, spec.MapId.ToString());
                    Assert.AreEqual(banner, hud.BannerText, spec.MapId.ToString());

                    // 4. Next open-world frame restores scan interactivity and fires cooldown cleanly.
                    ctrl.ProcessInputFrame(new RpgWorld500xInputFrame(), 0.1f);
                    Assert.IsTrue(hud.IsScanButtonInteractable, spec.MapId.ToString());
                    Assert.IsTrue(button.interactable, spec.MapId.ToString());
                    button.onClick.Invoke();
                    Assert.Greater(hud.LastPingCount, 0, spec.MapId.ToString());
                    Assert.IsFalse(hud.IsScanButtonInteractable, spec.MapId.ToString());
                    Assert.AreNotEqual(banner, hud.BannerText, spec.MapId.ToString());

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
