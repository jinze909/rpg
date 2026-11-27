using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    /// <summary>
    /// Four-region E2E regression tests for RpgWorld500xReconPulseHudBar verifying
    /// that changing filter category or switching regions invalidates stale recon
    /// pings without resetting pulse cooldown, while preserving valid pings on
    /// same-filter selection and releasing all native assets cleanly across Soundwell,
    /// RedSaltHarbor, WhitewaterCrossing, and MineCamp.
    /// </summary>
    public sealed class RpgWorld500xFourRegionReconPulseHudInvalidationE2ETests
    {
        [Test]
        public void AllFourRegions_ReconPulseHudInvalidatesOnFilterAndRegionSwitchWithoutResettingCooldown()
        {
            var root = new GameObject("FourRegionReconHudInvalidationRoot");
            try
            {
                // Warm up one-time UGUI/font caches before steady-state leak baseline.
                using (var warmupBoot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.Soundwell,
                    overrideTileSlices: new Sprite[] { null }))
                using (var warmupBar = new RpgWorld500xReconPulseHudBar(root.transform, warmupBoot, cooldownSeconds: 4f))
                {
                    warmupBar.UpdateCooldownDisplay(0f);
                }

                int texBefore = Resources.FindObjectsOfTypeAll<Texture2D>().Length;
                int spriteBefore = Resources.FindObjectsOfTypeAll<Sprite>().Length;

                IReadOnlyList<RpgExpandedMapSpec> specs = RpgExpandedWorldScale.AllSpecs;
                Assert.AreEqual(4, specs.Count);

                using (var boot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: specs[0].MapId,
                    overrideTileSlices: new Sprite[] { null }))
                using (var bar = new RpgWorld500xReconPulseHudBar(root.transform, boot, cooldownSeconds: 4f))
                {
                    float clock = 10f;

                    for (int r = 0; r < specs.Count; r++)
                    {
                        RpgExpandedMapSpec spec = specs[r];
                        RpgWorldMapId nextMap = specs[(r + 1) % specs.Count].MapId;

                        Assert.AreEqual(spec.MapId, boot.Driver.Facade.CurrentMapId);

                        // Reset filter to all categories before baseline scan on this region.
                        Assert.IsTrue(bar.TrySetFilterKind(null));
                        Assert.IsTrue(bar.TryClickPulseScan(clock), $"Baseline scan failed on {spec.MapId}.");
                        Assert.Greater(bar.LastPingCount, 0, $"Expected recon pings on {spec.MapId}.");

                        RpgWorld500xReconPingKind pickedKind = bar.LastPings[0].Kind;

                        // Switching filter clears stale pings and preserves active cooldown.
                        Assert.IsTrue(bar.TrySetFilterKind(pickedKind));
                        Assert.AreEqual(0, bar.LastPingCount, $"Filter switch did not clear pings on {spec.MapId}.");
                        Assert.IsEmpty(bar.PingsText);
                        StringAssert.Contains("重新勘测", bar.BannerText);
                        Assert.IsFalse(bar.TryClickPulseScan(clock + 1f),
                            $"Cooldown was reset after filter switch on {spec.MapId}.");

                        // After cooldown expires, filtered scan returns only matching pings.
                        clock += 4f;
                        Assert.IsTrue(bar.TryClickPulseScan(clock), $"Filtered scan failed on {spec.MapId}.");
                        Assert.Greater(bar.LastPingCount, 0);
                        for (int i = 0; i < bar.LastPings.Count; i++)
                        {
                            Assert.AreEqual(pickedKind, bar.LastPings[i].Kind,
                                $"Filtered ping kind mismatch on {spec.MapId}.");
                        }

                        // Selecting the same filter preserves valid pings.
                        int countBeforeSameFilter = bar.LastPingCount;
                        Assert.IsTrue(bar.TrySetFilterKind(pickedKind));
                        Assert.AreEqual(countBeforeSameFilter, bar.LastPingCount,
                            $"Same-filter selection unexpectedly cleared pings on {spec.MapId}.");

                        // Switching to next region invalidates old-region pings while keeping cooldown intact.
                        Assert.IsTrue(boot.Driver.SwitchMap(nextMap), $"Failed to switch to {nextMap}.");
                        bar.UpdateCooldownDisplay(clock + 1f);
                        Assert.AreEqual(0, bar.LastPingCount,
                            $"Region switch {spec.MapId}->{nextMap} did not clear stale pings.");
                        Assert.IsEmpty(bar.PingsText);
                        StringAssert.Contains("重新勘测", bar.BannerText);
                        Assert.AreEqual("冷却中 (3s)", bar.ButtonLabelText);
                        Assert.IsFalse(bar.TryClickPulseScan(clock + 1f));

                        clock += 4f;
                    }
                }

                Assert.AreEqual(0, root.transform.childCount);
                Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Texture2D>().Length, texBefore,
                    "Texture2D leak detected after four-region ReconPulseHudBar sweep.");
                Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Sprite>().Length, spriteBefore,
                    "Sprite leak detected after four-region ReconPulseHudBar sweep.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
