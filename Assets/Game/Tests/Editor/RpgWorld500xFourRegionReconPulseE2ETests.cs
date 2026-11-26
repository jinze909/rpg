using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionReconPulseE2ETests
    {
        [Test]
        public void ReconPulseHudBar_ScansFiltersInvalidatesAcrossAllFourRegionsAndDisposesWithZeroLeak()
        {
            var root = new GameObject("World500xFourRegionReconPulseE2ERoot");
            try
            {
                // Warm up one-time UGUI/font caches before steady-state leak baseline.
                using (var warmupBoot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.Soundwell,
                    overrideTileSlices: new Sprite[] { null }))
                using (var warmupBar = new RpgWorld500xReconPulseHudBar(root.transform, warmupBoot, cooldownSeconds: 4f))
                {
                    warmupBar.TryClickPulseScan(10f);
                }

                int texBefore = Resources.FindObjectsOfTypeAll<Texture2D>().Length;
                int spriteBefore = Resources.FindObjectsOfTypeAll<Sprite>().Length;

                RpgWorldMapId[] journey =
                {
                    RpgWorldMapId.Soundwell,
                    RpgWorldMapId.RedSaltHarbor,
                    RpgWorldMapId.WhitewaterCrossing,
                    RpgWorldMapId.MineCamp
                };

                using (var boot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: journey[0],
                    overrideTileSlices: new Sprite[] { null }))
                using (var bar = new RpgWorld500xReconPulseHudBar(root.transform, boot, cooldownSeconds: 4f))
                {
                    float clock = 10f;
                    for (int i = 0; i < journey.Length; i++)
                    {
                        if (i > 0)
                        {
                            Assert.IsTrue(boot.SwitchMap(journey[i]));
                            bar.UpdateCooldownDisplay(clock + 1f);
                            Assert.AreEqual(0, bar.LastPingCount);
                            Assert.IsEmpty(bar.PingsText);
                            StringAssert.Contains("重新勘测", bar.BannerText);
                            Assert.IsFalse(bar.TryClickPulseScan(clock + 1f));
                            clock += 4f;
                        }

                        Assert.IsTrue(bar.TrySetFilterKind(null));
                        Assert.IsTrue(bar.TryClickPulseScan(clock));
                        Assert.Greater(bar.LastPingCount, 0, "Expected at least one recon ping in " + journey[i]);

                        RpgWorld500xReconPingKind firstKind = bar.LastPings[0].Kind;
                        Assert.IsTrue(bar.TrySetFilterKind(firstKind));
                        Assert.AreEqual(0, bar.LastPingCount);
                        Assert.IsFalse(bar.TryClickPulseScan(clock + 1f));

                        clock += 4f;
                        Assert.IsTrue(bar.TryClickPulseScan(clock));
                        Assert.Greater(bar.LastPingCount, 0);
                        for (int p = 0; p < bar.LastPings.Count; p++)
                        {
                            Assert.AreEqual(firstKind, bar.LastPings[p].Kind);
                        }
                    }
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
