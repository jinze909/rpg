using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xRuntimeDriverTests
    {
        [Test]
        public void RuntimeDriver_TicksCameraHudWaypointsSwitchesMapsAndCleansUpOnShutdown()
        {
            var hostGo = new GameObject("World500xRuntimeDriverHost");
            var camGo = new GameObject("World500xTestCamera");
            RpgWorld500xRuntimeDriver driver = null;
            try
            {
                var cam = camGo.AddComponent<Camera>();
                cam.transform.position = new Vector3(0f, 0f, -10f);

                int texBefore = Resources.FindObjectsOfTypeAll<Texture2D>().Length;
                int spriteBefore = Resources.FindObjectsOfTypeAll<Sprite>().Length;

                driver = hostGo.AddComponent<RpgWorld500xRuntimeDriver>();
                driver.FollowCamera = cam;
                driver.Initialize(
                    overrideMap: RpgWorldMapId.Soundwell,
                    overrideTileSlices: new Sprite[] { null });

                Assert.IsTrue(driver.IsInitialized);
                Assert.IsFalse(string.IsNullOrEmpty(driver.LastNearestWaypointReadoutZh));

                // Tick with movement + overview toggle
                RpgWorld500xEffectiveExplorationModifiers mods = driver.TickFrame(
                    Vector2.right, 0.2f, sprinting: false, interactPressed: false,
                    toggleJournalPressed: false, toggleOverviewPressed: true);
                Assert.IsFalse(string.IsNullOrEmpty(mods.CombinedStatusCaptionZh));
                Assert.IsFalse(string.IsNullOrEmpty(driver.LastEnvironmentCaptionZh));
                Assert.IsTrue(driver.Facade.Session.MinimapHud.IsOverviewMode);

                // Camera synced to player X/Y while preserving Z = -10
                Vector2 playerPos = driver.Facade.Session.Host.PlayerPosition;
                Assert.AreEqual(playerPos.x, cam.transform.position.x, 0.01f);
                Assert.AreEqual(playerPos.y, cam.transform.position.y, 0.01f);
                Assert.AreEqual(-10f, cam.transform.position.z, 0.001f);

                // Switch maps cleanly
                Assert.IsTrue(driver.SwitchMap(RpgWorldMapId.RedSaltHarbor));
                Assert.AreEqual(RpgWorldMapId.RedSaltHarbor, driver.Facade.CurrentMapId);
                Assert.IsFalse(string.IsNullOrEmpty(driver.LastNearestWaypointReadoutZh));

                // In Unity EditMode, DestroyImmediate does not invoke MonoBehaviour.OnDestroy
                // for non-ExecuteAlways components; invoke Shutdown() explicitly before destroy.
                driver.Shutdown();
                Assert.IsFalse(driver.IsInitialized);
                Assert.AreEqual(0, hostGo.transform.childCount);

                UnityEngine.Object.DestroyImmediate(hostGo);
                hostGo = null;
                driver = null;

                Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Texture2D>().Length, texBefore);
                Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Sprite>().Length, spriteBefore);
            }
            finally
            {
                if (driver != null)
                {
                    driver.Shutdown();
                }

                if (hostGo != null)
                {
                    UnityEngine.Object.DestroyImmediate(hostGo);
                }

                if (camGo != null)
                {
                    UnityEngine.Object.DestroyImmediate(camGo);
                }
            }
        }
    }
}
