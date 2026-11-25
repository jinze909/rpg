using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xReconPulseHudBarTests
    {
        [Test]
        public void ReconPulseHudBar_ScansNearbyTargetsRespectsCooldownAndDisposesWithZeroLeak()
        {
            var root = new GameObject("World500xReconPulseHudBarTestRoot");
            try
            {
                // Warm up one-time UGUI/font caches before steady-state leak baseline.
                using (var warmupBoot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.Soundwell,
                    overrideTileSlices: new Sprite[] { null }))
                using (var warmupBar = new RpgWorld500xReconPulseHudBar(root.transform, warmupBoot))
                {
                    warmupBar.TryClickPulseScan(1f);
                }

                int texBefore = Resources.FindObjectsOfTypeAll<Texture2D>().Length;
                int spriteBefore = Resources.FindObjectsOfTypeAll<Sprite>().Length;

                using (var boot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.Soundwell,
                    overrideTileSlices: new Sprite[] { null }))
                using (var bar = new RpgWorld500xReconPulseHudBar(root.transform, boot, cooldownSeconds: 4f))
                {
                    Assert.IsTrue(bar.IsScanButtonInteractable);
                    Assert.IsTrue(bar.TryClickPulseScan(10f));
                    Assert.Greater(bar.LastPingCount, 0);
                    Assert.LessOrEqual(bar.LastPingCount, 3);
                    Assert.IsTrue(bar.BannerText.Contains("勘测发现"));
                    Assert.IsFalse(string.IsNullOrEmpty(bar.PingsText));

                    // During cooldown, button is disabled and click is rejected.
                    Assert.IsFalse(bar.IsScanButtonInteractable);
                    Assert.IsTrue(bar.ButtonLabelText.Contains("冷却中"));
                    Assert.IsFalse(bar.TryClickPulseScan(12f));

                    // After cooldown expires, button becomes ready again.
                    bar.UpdateCooldownDisplay(15f);
                    Assert.IsTrue(bar.IsScanButtonInteractable);
                    Assert.IsTrue(bar.ButtonLabelText.Contains("就绪"));
                    Assert.IsTrue(bar.TryClickPulseScan(15f));
                }

                Assert.AreEqual(0, root.transform.childCount);
                Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Texture2D>().Length, texBefore);
                Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Sprite>().Length, spriteBefore);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
