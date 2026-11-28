using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    [TestFixture]
    public sealed class RpgWorld500xFourRegionStatusOverlayHudSyncE2ETests
    {
        [Test]
        public void AllFourRegions_StatusOverlayHud_SyncsDistrictEconomyAndMapSwitchesWithZeroLeak()
        {
            var specs = RpgExpandedWorldScale.AllSpecs;
            Assert.AreEqual(4, specs.Count, "Expected exactly 4 regions in 500x world scale.");

            var root = new GameObject("FourRegionStatusOverlayHudSyncRoot");
            RpgWorld500xRuntimeDriver driver = null;
            RpgWorld500xStatusOverlayHud hud = null;
            try
            {
                driver = root.AddComponent<RpgWorld500xRuntimeDriver>();
                hud = new RpgWorld500xStatusOverlayHud(root.transform);

                Assert.IsFalse(hud.SyncFromDriver(null), "SyncFromDriver(null) must return false.");
                Assert.IsFalse(hud.SyncFromDriver(driver), "SyncFromDriver on uninitialized driver must return false.");

                driver.Initialize(overrideMap: specs[0].MapId, overrideTileSlices: new Sprite[] { null });

                int expectedSilver = 0;
                foreach (RpgExpandedMapSpec spec in specs)
                {
                    if (driver.Facade.CurrentMapId != spec.MapId)
                    {
                        Assert.IsTrue(driver.SwitchMap(spec.MapId), $"SwitchMap to {spec.MapId} should succeed.");
                    }

                    driver.TickFrame(Vector2.zero, 0.1f);
                    driver.Facade.BountyAndSupply.AddSaltSilver(25);
                    expectedSilver += 25;

                    Assert.IsTrue(hud.SyncFromDriver(driver), $"SyncFromDriver should succeed in {spec.MapId}.");
                    Assert.IsFalse(string.IsNullOrEmpty(hud.EnvironmentReadout), $"EnvironmentReadout should be populated in {spec.MapId}.");
                    StringAssert.Contains("盐银 " + expectedSilver, hud.WaypointAndEconomyReadout);
                    StringAssert.Contains("探索度", hud.WaypointAndEconomyReadout);
                }

                hud.Dispose();
                hud.Dispose();
                Assert.IsTrue(hud.IsDisposed);
                Assert.IsFalse(hud.SyncFromDriver(driver), "Disposed HUD must reject SyncFromDriver.");
                hud = null;

                driver.Shutdown();
                Assert.AreEqual(0, root.transform.childCount, "Shutting down driver and HUD must leave zero children.");
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
