using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xStatusOverlayHudTests
    {
        [Test]
        public void StatusOverlayHud_SyncsDistrictClockWaypointEconomyAndDisposesCleanly()
        {
            var root = new GameObject("World500xStatusOverlayHudTestRoot");
            RpgWorld500xRuntimeDriver driver = null;
            RpgWorld500xStatusOverlayHud hud = null;
            try
            {
                driver = root.AddComponent<RpgWorld500xRuntimeDriver>();

                // Warm up one-time UGUI/font caches before steady-state leak baseline.
                driver.Initialize(overrideMap: RpgWorldMapId.Soundwell, overrideTileSlices: new Sprite[] { null });
                hud = new RpgWorld500xStatusOverlayHud(root.transform);
                hud.SyncFromDriver(driver);
                hud.Dispose();
                hud = null;
                driver.Shutdown();

                int texBefore = Resources.FindObjectsOfTypeAll<Texture2D>().Length;
                int spriteBefore = Resources.FindObjectsOfTypeAll<Sprite>().Length;

                driver.Initialize(overrideMap: RpgWorldMapId.Soundwell, overrideTileSlices: new Sprite[] { null });
                hud = new RpgWorld500xStatusOverlayHud(root.transform);

                driver.TickFrame(Vector2.right, 0.2f);
                driver.Facade.BountyAndSupply.AddSaltSilver(45);

                Assert.IsTrue(hud.SyncFromDriver(driver));
                Assert.IsFalse(string.IsNullOrEmpty(hud.EnvironmentReadout));
                Assert.IsTrue(hud.WaypointAndEconomyReadout.Contains("盐银 45"));
                Assert.IsTrue(hud.WaypointAndEconomyReadout.Contains("探索度"));

                // Switch to RedSaltHarbor and confirm HUD updates cleanly
                Assert.IsTrue(driver.SwitchMap(RpgWorldMapId.RedSaltHarbor));
                driver.TickFrame(Vector2.zero, 0.1f);
                Assert.IsTrue(hud.SyncFromDriver(driver));
                Assert.IsFalse(string.IsNullOrEmpty(hud.EnvironmentReadout));

                hud.Dispose();
                hud.Dispose();
                Assert.IsTrue(hud.IsDisposed);
                Assert.IsFalse(hud.SyncFromDriver(driver));
                hud = null;

                driver.Shutdown();
                Assert.AreEqual(0, root.transform.childCount);

                Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Texture2D>().Length, texBefore);
                Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Sprite>().Length, spriteBefore);
            }
            finally
            {
                hud?.Dispose();
                if (driver != null)
                {
                    driver.Shutdown();
                }

                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
