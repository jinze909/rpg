using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    /// <summary>
    /// Four-region E2E regression tests for RpgWorld500xFastTravelBeaconHudPanel
    /// covering beacon network coverage summaries, nearest discovered/undiscovered
    /// beacon readouts, ancestor CanvasGroup warp lock/unlock recovery, and zero
    /// native Texture2D/Sprite leaks across Soundwell, RedSaltHarbor,
    /// WhitewaterCrossing, and MineCamp.
    /// </summary>
    public sealed class RpgWorld500xFourRegionFastTravelBeaconHudE2ETests
    {
        [Test]
        public void AllFourRegions_FastTravelBeaconHud_TracksNetworkSummaryLocksWarpUnderCanvasGroupAndDisposesCleanly()
        {
            var root = new GameObject("FourRegionBeaconHudRoot");
            try
            {
                // Warm up one-time UGUI/font caches before steady-state leak baseline.
                using (var warmupBoot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.Soundwell,
                    overrideTileSlices: new Sprite[] { null }))
                using (var warmupPanel = new RpgWorld500xFastTravelBeaconHudPanel(root.transform, warmupBoot))
                {
                    warmupPanel.Show();
                    warmupPanel.Hide();
                }

                int texBefore = Resources.FindObjectsOfTypeAll<Texture2D>().Length;
                int spriteBefore = Resources.FindObjectsOfTypeAll<Sprite>().Length;

                IReadOnlyList<RpgExpandedMapSpec> specs = RpgExpandedWorldScale.AllSpecs;
                Assert.AreEqual(4, specs.Count);

                for (int r = 0; r < specs.Count; r++)
                {
                    RpgExpandedMapSpec spec = specs[r];
                    var regionRoot = new GameObject("RegionBeaconHud_" + spec.MapId);
                    regionRoot.transform.SetParent(root.transform, false);

                    try
                    {
                        using (var boot = new RpgWorld500xSceneBootstrapper(
                            initialMapId: spec.MapId,
                            overrideTileSlices: new Sprite[] { null }))
                        using (var panel = new RpgWorld500xFastTravelBeaconHudPanel(regionRoot.transform, boot))
                        {
                            Assert.IsTrue(panel.Show(), $"FastTravelBeaconHudPanel failed to show on {spec.MapId}.");

                            int initialUnlocked = panel.UnlockedBeaconCount;
                            int total = panel.TotalBeaconCount;
                            Assert.Greater(total, 1, $"Expected multiple fast-travel beacons on {spec.MapId}.");
                            Assert.AreEqual(total, panel.DisplayedBeaconRowCount);
                            Assert.AreEqual(total, panel.LastNetworkSummary.TotalBeaconCount);
                            Assert.AreEqual(initialUnlocked, panel.LastNetworkSummary.DiscoveredBeaconCount);
                            StringAssert.Contains(spec.DisplayNameZh, panel.HeaderText);
                            StringAssert.Contains("【传送烽火】", panel.NetworkSummaryText);

                            string targetUndiscoveredId = panel.NearestUndiscoveredBeaconId;
                            Assert.IsFalse(string.IsNullOrEmpty(targetUndiscoveredId),
                                $"Expected an undiscovered beacon on {spec.MapId}.");
                            Assert.GreaterOrEqual(panel.NearestUndiscoveredBeaconDistanceTiles, 0f);

                            // Discover nearest undiscovered beacon and refresh network summary.
                            Assert.IsTrue(boot.Driver.Facade.Session.Host.Tracker.TryDiscoverMarker(targetUndiscoveredId));
                            panel.Refresh();
                            Assert.AreEqual(initialUnlocked + 1, panel.UnlockedBeaconCount);
                            Assert.AreEqual(initialUnlocked + 1, panel.LastNetworkSummary.DiscoveredBeaconCount);
                            Assert.AreNotEqual(targetUndiscoveredId, panel.NearestUndiscoveredBeaconId);

                            // Ancestor CanvasGroup lock blocks beacon warp cleanly.
                            CanvasGroup group = regionRoot.AddComponent<CanvasGroup>();
                            group.interactable = false;
                            Assert.IsFalse(panel.TryClickWarp(targetUndiscoveredId),
                                $"Beacon warp succeeded under ancestor CanvasGroup lock on {spec.MapId}.");

                            // Unlocking restores warp execution and updates nearest discovered beacon.
                            group.interactable = true;
                            Assert.IsTrue(panel.TryClickWarp(targetUndiscoveredId),
                                $"Beacon warp failed after CanvasGroup unlock on {spec.MapId}.");
                            Assert.AreEqual(targetUndiscoveredId, panel.NearestDiscoveredBeaconId);
                            Assert.GreaterOrEqual(panel.NearestDiscoveredBeaconDistanceTiles, 0f);
                            StringAssert.Contains("已传送至", panel.StatusText);

                            Assert.IsTrue(panel.Hide());
                            Assert.IsFalse(panel.IsVisible);
                        }
                    }
                    finally
                    {
                        UnityEngine.Object.DestroyImmediate(regionRoot);
                    }
                }

                Assert.AreEqual(0, root.transform.childCount);
                Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Texture2D>().Length, texBefore,
                    "Texture2D leak detected after four-region FastTravelBeaconHudPanel sweep.");
                Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Sprite>().Length, spriteBefore,
                    "Sprite leak detected after four-region FastTravelBeaconHudPanel sweep.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
