using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xReconPulseAndBeaconHudE2ETests
    {
        [Test]
        public void ReconPulseFilterInvalidationAndBeaconNetworkWarp_IntegrateEndToEndWithZeroLeak()
        {
            var root = new GameObject("World500xReconBeaconHudE2ERoot");
            try
            {
                // Warm up one-time UGUI/font caches before steady-state leak baseline.
                using (var warmupBoot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.RedSaltHarbor,
                    overrideTileSlices: new Sprite[] { null }))
                using (var warmupRecon = new RpgWorld500xReconPulseHudBar(root.transform, warmupBoot, cooldownSeconds: 4f))
                using (var warmupBeacon = new RpgWorld500xFastTravelBeaconHudPanel(root.transform, warmupBoot))
                {
                    warmupRecon.UpdateCooldownDisplay(1f);
                    warmupBeacon.Show();
                    warmupBeacon.Hide();
                }

                int texBefore = Resources.FindObjectsOfTypeAll<Texture2D>().Length;
                int spriteBefore = Resources.FindObjectsOfTypeAll<Sprite>().Length;

                using (var boot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.RedSaltHarbor,
                    overrideTileSlices: new Sprite[] { null }))
                using (var reconBar = new RpgWorld500xReconPulseHudBar(root.transform, boot, cooldownSeconds: 4f))
                using (var beaconPanel = new RpgWorld500xFastTravelBeaconHudPanel(root.transform, boot))
                {
                    // 1. ReconPulseHudBar scans, filters, previews nearest target without consuming cooldown, and rescans.
                    Assert.IsTrue(reconBar.TryClickPulseScan(10f));
                    Assert.Greater(reconBar.LastPingCount, 0);
                    RpgWorld500xReconPingKind sampleKind = reconBar.LastPings[0].Kind;

                    Assert.IsTrue(reconBar.TrySetFilterKind(sampleKind));
                    Assert.AreEqual(0, reconBar.LastPingCount);
                    StringAssert.Contains("重新勘测", reconBar.BannerText);
                    Assert.IsTrue(reconBar.TryPreviewNearestForActiveFilter(out RpgWorld500xReconPing preview));
                    Assert.AreEqual(sampleKind, preview.Kind);
                    Assert.IsFalse(reconBar.TryClickPulseScan(11f));

                    reconBar.UpdateCooldownDisplay(15f);
                    Assert.IsTrue(reconBar.TryClickPulseScan(15f));
                    Assert.Greater(reconBar.LastPingCount, 0);
                    for (int i = 0; i < reconBar.LastPings.Count; i++)
                    {
                        Assert.AreEqual(sampleKind, reconBar.LastPings[i].Kind);
                    }

                    // 2. FastTravelBeaconHudPanel tracks beacon coverage, discovers nearest beacon, and warps.
                    Assert.IsTrue(beaconPanel.Show());
                    int initialUnlocked = beaconPanel.UnlockedBeaconCount;
                    int totalBeacons = beaconPanel.TotalBeaconCount;
                    Assert.Greater(totalBeacons, 1);
                    StringAssert.Contains("【传送烽火】", beaconPanel.NetworkSummaryText);

                    string targetBeaconId = beaconPanel.NearestUndiscoveredBeaconId;
                    Assert.IsNotEmpty(targetBeaconId);
                    Assert.IsTrue(boot.Driver.Facade.Session.Host.Tracker.TryDiscoverMarker(targetBeaconId));
                    beaconPanel.Refresh();
                    Assert.AreEqual(initialUnlocked + 1, beaconPanel.UnlockedBeaconCount);
                    Assert.AreEqual(initialUnlocked + 1, beaconPanel.LastNetworkSummary.DiscoveredBeaconCount);
                    Assert.AreNotEqual(targetBeaconId, beaconPanel.NearestUndiscoveredBeaconId);

                    Assert.IsTrue(beaconPanel.TryClickWarp(targetBeaconId));
                    Assert.AreEqual(targetBeaconId, beaconPanel.NearestDiscoveredBeaconId);
                    StringAssert.Contains("已传送至", beaconPanel.StatusText);
                    Assert.IsTrue(beaconPanel.Hide());

                    // 3. Cross-map switch invalidates stale recon pings and refreshes beacon network for Soundwell.
                    Assert.IsTrue(boot.SwitchMap(RpgWorldMapId.Soundwell));
                    reconBar.UpdateCooldownDisplay(16f);
                    Assert.AreEqual(0, reconBar.LastPingCount);
                    StringAssert.Contains("重新勘测", reconBar.BannerText);

                    Assert.IsTrue(beaconPanel.Show());
                    Assert.AreEqual(RpgWorldMapId.Soundwell, beaconPanel.LastNetworkSummary.MapId);
                    Assert.Greater(beaconPanel.TotalBeaconCount, 0);
                    Assert.IsTrue(beaconPanel.Hide());
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
