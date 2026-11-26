using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xAtlasAndBeaconHudE2ETests
    {
        [Test]
        public void WorldAtlasMultiHopPreviewAndBeaconNetworkWarp_IntegrateAcrossThreeRegionsWithZeroLeak()
        {
            var root = new GameObject("World500xAtlasBeaconHudE2ERoot");
            try
            {
                // Warm up one-time UGUI/font caches before steady-state leak baseline.
                using (var warmupBoot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.Soundwell,
                    overrideTileSlices: new Sprite[] { null }))
                using (var warmupAtlas = new RpgWorld500xWorldAtlasHudPanel(root.transform, warmupBoot))
                using (var warmupBeacon = new RpgWorld500xFastTravelBeaconHudPanel(root.transform, warmupBoot))
                {
                    warmupAtlas.Show();
                    warmupAtlas.Hide();
                    warmupBeacon.Show();
                    warmupBeacon.Hide();
                }

                int texBefore = Resources.FindObjectsOfTypeAll<Texture2D>().Length;
                int spriteBefore = Resources.FindObjectsOfTypeAll<Sprite>().Length;

                using (var boot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.Soundwell,
                    overrideTileSlices: new Sprite[] { null }))
                using (var atlasPanel = new RpgWorld500xWorldAtlasHudPanel(root.transform, boot))
                using (var beaconPanel = new RpgWorld500xFastTravelBeaconHudPanel(root.transform, boot))
                {
                    // 1. Soundwell: inspect 4-region atlas summary & 2-hop preview to MineCamp.
                    Assert.IsTrue(atlasPanel.Show());
                    Assert.AreEqual(4, atlasPanel.LastAtlasSummary.TotalRegions);
                    StringAssert.Contains("【四境总览】", atlasPanel.AtlasSummaryText);
                    Assert.IsTrue(atlasPanel.TryPreviewShortestRouteTo(
                        RpgWorldMapId.MineCamp, out RpgWorld500xRegionPathPlan multiHop));
                    Assert.IsFalse(multiHop.IsDirect);
                    Assert.AreEqual(2, multiHop.Hops.Count);
                    Assert.AreEqual(270, multiHop.TotalTravelMinutesInGame);

                    // Discover and warp to the nearest undiscovered Soundwell beacon.
                    Assert.IsTrue(beaconPanel.Show());
                    Assert.AreEqual(RpgWorldMapId.Soundwell, beaconPanel.LastNetworkSummary.MapId);
                    StringAssert.Contains("【传送烽火】", beaconPanel.NetworkSummaryText);
                    int swUnlockedBefore = beaconPanel.UnlockedBeaconCount;
                    string swTargetBeacon = beaconPanel.NearestUndiscoveredBeaconId;
                    Assert.IsNotEmpty(swTargetBeacon);
                    Assert.IsTrue(boot.Driver.Facade.Session.Host.Tracker.TryDiscoverMarker(swTargetBeacon));
                    beaconPanel.Refresh();
                    Assert.AreEqual(swUnlockedBefore + 1, beaconPanel.UnlockedBeaconCount);
                    Assert.IsTrue(beaconPanel.TryClickWarp(swTargetBeacon));
                    Assert.AreEqual(swTargetBeacon, beaconPanel.NearestDiscoveredBeaconId);
                    Assert.IsTrue(beaconPanel.Hide());

                    // 2. Travel Soundwell -> RedSaltHarbor; route preview to MineCamp becomes direct (1 hop, 150 min).
                    Assert.IsTrue(atlasPanel.TryClickTravelTo(RpgWorldMapId.RedSaltHarbor));
                    Assert.AreEqual(RpgWorldMapId.RedSaltHarbor, boot.Driver.Facade.CurrentMapId);
                    Assert.IsTrue(atlasPanel.TryPreviewShortestRouteTo(
                        RpgWorldMapId.MineCamp, out RpgWorld500xRegionPathPlan directHop));
                    Assert.IsTrue(directHop.IsDirect);
                    Assert.AreEqual(1, directHop.Hops.Count);
                    Assert.AreEqual(150, directHop.TotalTravelMinutesInGame);

                    // Beacon panel reflects RedSaltHarbor network and warps to newly discovered beacon.
                    Assert.IsTrue(beaconPanel.Show());
                    Assert.AreEqual(RpgWorldMapId.RedSaltHarbor, beaconPanel.LastNetworkSummary.MapId);
                    string rshTargetBeacon = beaconPanel.NearestUndiscoveredBeaconId;
                    Assert.IsNotEmpty(rshTargetBeacon);
                    Assert.IsTrue(boot.Driver.Facade.Session.Host.Tracker.TryDiscoverMarker(rshTargetBeacon));
                    beaconPanel.Refresh();
                    Assert.IsTrue(beaconPanel.TryClickWarp(rshTargetBeacon));
                    Assert.AreEqual(rshTargetBeacon, beaconPanel.NearestDiscoveredBeaconId);
                    Assert.IsTrue(beaconPanel.Hide());

                    // 3. Travel RedSaltHarbor -> MineCamp; route preview to MineCamp reports SameRegion (0 min).
                    Assert.IsTrue(atlasPanel.TryClickTravelTo(RpgWorldMapId.MineCamp));
                    Assert.AreEqual(RpgWorldMapId.MineCamp, boot.Driver.Facade.CurrentMapId);
                    Assert.IsTrue(atlasPanel.TryPreviewShortestRouteTo(
                        RpgWorldMapId.MineCamp, out RpgWorld500xRegionPathPlan sameRegion));
                    Assert.IsTrue(sameRegion.IsSameRegion);
                    Assert.AreEqual(0, sameRegion.TotalTravelMinutesInGame);
                    Assert.IsTrue(atlasPanel.Hide());
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
