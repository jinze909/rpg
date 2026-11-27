using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionStatusOverlayHudE2ETests
    {
        private static readonly RpgWorldMapId[] FourRegions =
        {
            RpgWorldMapId.Soundwell,
            RpgWorldMapId.RedSaltHarbor,
            RpgWorldMapId.WhitewaterCrossing,
            RpgWorldMapId.MineCamp
        };

        [Test]
        public void FourRegion_StatusOverlayHud_SyncsEnvironmentWaypointEconomyAndToastsAcrossAllFourMapsWithZeroSpriteLeak()
        {
            var root = new GameObject("World500xFourRegionStatusOverlayRoot");
            RpgWorld500xRuntimeDriver driver = null;
            RpgWorld500xStatusOverlayHud hud = null;
            try
            {
                driver = root.AddComponent<RpgWorld500xRuntimeDriver>();

                // Warm up one-time UGUI/font and per-region caches before steady-state leak baseline.
                driver.Initialize(overrideMap: RpgWorldMapId.Soundwell, overrideTileSlices: new Sprite[] { null });
                for (int w = 1; w < FourRegions.Length; w++)
                {
                    Assert.IsTrue(driver.SwitchMap(FourRegions[w]));
                }

                hud = new RpgWorld500xStatusOverlayHud(root.transform);
                Assert.IsTrue(hud.SyncFromDriver(driver));
                hud.Dispose();
                hud = null;
                driver.Shutdown();

                int spriteBefore = Resources.FindObjectsOfTypeAll<Sprite>().Length;

                driver.Initialize(overrideMap: RpgWorldMapId.Soundwell, overrideTileSlices: new Sprite[] { null });
                hud = new RpgWorld500xStatusOverlayHud(root.transform);

                for (int i = 0; i < FourRegions.Length; i++)
                {
                    RpgWorldMapId region = FourRegions[i];
                    if (driver.Facade.CurrentMapId != region)
                    {
                        Assert.IsTrue(driver.SwitchMap(region));
                    }

                    driver.TickFrame(Vector2.right, 0.2f);
                    driver.Facade.BountyAndSupply.AddSaltSilver(35);

                    int expectedSilver = driver.Facade.BountyAndSupply.SaltSilverBalance;
                    int expectedPercent = driver.Facade.GetCompletionSummary().OverallCompletionPercent;

                    Assert.IsTrue(hud.SyncFromDriver(driver), $"StatusOverlayHud failed to sync on {region}.");
                    Assert.IsFalse(string.IsNullOrEmpty(hud.EnvironmentReadout));
                    StringAssert.Contains("盐银 " + expectedSilver, hud.WaypointAndEconomyReadout);
                    StringAssert.Contains("探索度 " + expectedPercent + "%", hud.WaypointAndEconomyReadout);

                    bool hasToasts = driver.RecentToasts.Count > 0;
                    Assert.AreEqual(hasToasts, hud.IsToastVisible);
                    if (hasToasts)
                    {
                        Assert.AreEqual(driver.RecentToasts[driver.RecentToasts.Count - 1], hud.ToastReadout);
                    }
                    else
                    {
                        Assert.AreEqual(string.Empty, hud.ToastReadout);
                    }
                }

                // Idempotent dispose and post-dispose sync rejection.
                hud.Dispose();
                hud.Dispose();
                Assert.IsTrue(hud.IsDisposed);
                Assert.IsFalse(hud.SyncFromDriver(driver));
                hud = null;

                driver.Shutdown();
                Assert.AreEqual(0, root.transform.childCount);
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

        [Test]
        public void FourRegion_StatusOverlayHud_RejectsNullUninitializedAndPostShutdownDriver()
        {
            var root = new GameObject("World500xFourRegionStatusOverlayGuardRoot");
            RpgWorld500xRuntimeDriver driver = null;
            RpgWorld500xStatusOverlayHud hud = null;
            try
            {
                driver = root.AddComponent<RpgWorld500xRuntimeDriver>();
                hud = new RpgWorld500xStatusOverlayHud(root.transform);

                Assert.IsFalse(hud.SyncFromDriver(null));
                Assert.IsFalse(hud.SyncFromDriver(driver));

                driver.Initialize(overrideMap: RpgWorldMapId.MineCamp, overrideTileSlices: new Sprite[] { null });
                Assert.IsTrue(hud.SyncFromDriver(driver));
                Assert.IsFalse(string.IsNullOrEmpty(hud.EnvironmentReadout));

                driver.Shutdown();
                Assert.IsFalse(hud.SyncFromDriver(driver));

                // External parent teardown marks HUD disposed cleanly without throwing on Dispose.
                UnityEngine.Object.DestroyImmediate(root);
                root = null;
                driver = null;
                Assert.IsTrue(hud.IsDisposed);
                Assert.DoesNotThrow(() => hud.Dispose());
            }
            finally
            {
                hud?.Dispose();
                if (driver != null)
                {
                    driver.Shutdown();
                }

                if (root != null)
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }
        }
    }
}
