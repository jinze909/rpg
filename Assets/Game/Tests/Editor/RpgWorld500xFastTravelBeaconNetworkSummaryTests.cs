using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFastTravelBeaconNetworkSummaryTests
    {
        [Test]
        public void FastTravelBeaconHudPanel_ExposesNetworkSummaryAndNearestBeaconReadoutsWithZeroLeak()
        {
            var root = new GameObject("World500xBeaconNetworkSummaryHudTestRoot");
            try
            {
                // Warm up one-time UGUI/font caches before steady-state leak baseline.
                using (var warmupBoot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.RedSaltHarbor,
                    overrideTileSlices: new Sprite[] { null }))
                using (var warmupPanel = new RpgWorld500xFastTravelBeaconHudPanel(root.transform, warmupBoot))
                {
                    warmupPanel.Show();
                    warmupPanel.Hide();
                }

                int texBefore = Resources.FindObjectsOfTypeAll<Texture2D>().Length;
                int spriteBefore = Resources.FindObjectsOfTypeAll<Sprite>().Length;

                using (var boot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.RedSaltHarbor,
                    overrideTileSlices: new Sprite[] { null }))
                using (var panel = new RpgWorld500xFastTravelBeaconHudPanel(root.transform, boot))
                {
                    Assert.IsTrue(panel.Show());
                    int initialUnlocked = panel.UnlockedBeaconCount;
                    int total = panel.TotalBeaconCount;
                    Assert.Greater(total, 1);
                    Assert.AreEqual(total, panel.DisplayedBeaconRowCount);
                    Assert.AreEqual(total, panel.LastNetworkSummary.TotalBeaconCount);
                    Assert.AreEqual(initialUnlocked, panel.LastNetworkSummary.DiscoveredBeaconCount);
                    StringAssert.Contains("【传送烽火】", panel.NetworkSummaryText);

                    string targetUndiscoveredId = panel.NearestUndiscoveredBeaconId;
                    Assert.IsNotEmpty(targetUndiscoveredId);
                    Assert.GreaterOrEqual(panel.NearestUndiscoveredBeaconDistanceTiles, 0f);

                    // Discover the nearest undiscovered beacon -> coverage increases and nearest discovered updates after warp.
                    Assert.IsTrue(boot.Driver.Facade.Session.Host.Tracker.TryDiscoverMarker(targetUndiscoveredId));
                    panel.Refresh();
                    Assert.AreEqual(initialUnlocked + 1, panel.UnlockedBeaconCount);
                    Assert.AreEqual(initialUnlocked + 1, panel.LastNetworkSummary.DiscoveredBeaconCount);
                    Assert.AreNotEqual(targetUndiscoveredId, panel.NearestUndiscoveredBeaconId);

                    Assert.IsTrue(panel.TryClickWarp(targetUndiscoveredId));
                    Assert.AreEqual(targetUndiscoveredId, panel.NearestDiscoveredBeaconId);
                    Assert.GreaterOrEqual(panel.NearestDiscoveredBeaconDistanceTiles, 0f);
                    StringAssert.Contains("已传送至", panel.StatusText);

                    Assert.IsTrue(panel.Hide());
                    Assert.IsFalse(panel.IsVisible);
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
