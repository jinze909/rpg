using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Tests.Editor
{
    [TestFixture]
    public sealed class RpgWorld500xFourRegionReconPulseCanvasGuardE2ETests
    {
        [Test]
        public void AllFourRegions_ReconPulseHudBar_DisabledCanvasRejectsScanWithoutConsumingCooldown()
        {
            var specs = RpgExpandedWorldScale.AllSpecs;
            Assert.AreEqual(4, specs.Count, "Expected exactly 4 regions in 500x world scale.");

            foreach (RpgExpandedMapSpec spec in specs)
            {
                var root = new GameObject("FourRegionReconDisabledCanvas_" + spec.MapId);
                try
                {
                    using (var boot = new RpgWorld500xSceneBootstrapper(
                        initialMapId: spec.MapId,
                        overrideTileSlices: new Sprite[] { null }))
                    using (var bar = new RpgWorld500xReconPulseHudBar(root.transform, boot, cooldownSeconds: 4f))
                    {
                        Transform hud = root.transform.Find("World500xReconPulseHudBar");
                        var canvas = hud.GetComponent<Canvas>();
                        var button = hud.Find("ReconPulsePanel/PulseScanButton").GetComponent<Button>();

                        bar.UpdateCooldownDisplay(10f);
                        string originalBanner = bar.BannerText;

                        canvas.enabled = false;
                        Assert.IsFalse(bar.IsScanButtonInteractable, $"Disabled canvas should make scan non-interactable in {spec.MapId}.");
                        button.onClick.Invoke();
                        Assert.IsFalse(bar.TryClickPulseScan(10f));
                        Assert.AreEqual(0, bar.LastPingCount);
                        Assert.AreEqual(originalBanner, bar.BannerText);
                        Assert.IsEmpty(bar.PingsText);

                        canvas.enabled = true;
                        Assert.IsTrue(bar.IsScanButtonInteractable);
                        Assert.IsTrue(bar.TryClickPulseScan(10f));
                        Assert.Greater(bar.LastPingCount, 0);
                        Assert.IsFalse(bar.IsScanButtonInteractable);
                        bar.UpdateCooldownDisplay(14f);
                        Assert.IsTrue(bar.IsScanButtonInteractable);
                    }

                    Assert.AreEqual(0, root.transform.childCount);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }
        }

        [Test]
        public void AllFourRegions_ReconPulseHudBar_CanvasGroupLockAndNonFiniteClockRejectScansWithZeroLeak()
        {
            var specs = RpgExpandedWorldScale.AllSpecs;
            Assert.AreEqual(4, specs.Count, "Expected exactly 4 regions in 500x world scale.");

            foreach (RpgExpandedMapSpec spec in specs)
            {
                var root = new GameObject("FourRegionReconCanvasGroupLock_" + spec.MapId);
                try
                {
                    using (var boot = new RpgWorld500xSceneBootstrapper(
                        initialMapId: spec.MapId,
                        overrideTileSlices: new Sprite[] { null }))
                    using (var bar = new RpgWorld500xReconPulseHudBar(root.transform, boot, cooldownSeconds: 4f))
                    {
                        bar.UpdateCooldownDisplay(10f);
                        Assert.IsFalse(bar.TryClickPulseScan(float.NaN));
                        Assert.IsFalse(bar.TryClickPulseScan(float.PositiveInfinity));
                        Assert.AreEqual(0, bar.LastPingCount);

                        CanvasGroup group = root.AddComponent<CanvasGroup>();
                        group.interactable = false;
                        var button = root.transform
                            .Find("World500xReconPulseHudBar/ReconPulsePanel/PulseScanButton")
                            .GetComponent<Button>();

                        Assert.IsFalse(bar.IsScanButtonInteractable);
                        button.onClick.Invoke();
                        Assert.IsFalse(bar.TryClickPulseScan(10f));
                        Assert.AreEqual(0, bar.LastPingCount);
                    }

                    Assert.AreEqual(0, root.transform.childCount);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }
        }
    }
}
