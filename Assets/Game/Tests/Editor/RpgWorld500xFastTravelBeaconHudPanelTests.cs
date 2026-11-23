using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFastTravelBeaconHudPanelTests
    {
        [Test]
        public void FastTravelBeaconHudPanel_WarpsToDiscoveredBeaconRejectsLockedAndDisposesWithZeroLeak()
        {
            var root = new GameObject("World500xFastTravelBeaconHudPanelTestRoot");
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
                    Assert.Greater(panel.DisplayedBeaconRowCount, 0);

                    // Locked beacon cannot be warped to.
                    Assert.IsFalse(panel.TryClickWarp("rsh.salt-market"));

                    // Discover beacon, refresh panel, and warp via UI click.
                    Assert.IsTrue(boot.Driver.Facade.Session.Host.Tracker.TryDiscoverMarker("rsh.salt-market"));
                    panel.Refresh();
                    Assert.GreaterOrEqual(panel.UnlockedBeaconCount, 1);

                    Assert.IsTrue(panel.TryClickWarp("rsh.salt-market"));
                    Assert.AreEqual(new Vector2Int(1700, 1260), boot.Driver.Facade.Session.Host.CurrentTile);
                    Assert.IsTrue(panel.StatusText.Contains("已传送至"));

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
