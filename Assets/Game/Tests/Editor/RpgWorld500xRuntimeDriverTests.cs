using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xRuntimeDriverTests
    {
        [Test]
        public void RuntimeDriver_TicksCameraHudWaypointsSwitchesMapsAndCleansUpOnDestroy()
        {
            int texBefore = Resources.FindObjectsOfTypeAll<Texture2D>().Length;
            int spriteBefore = Resources.FindObjectsOfTypeAll<Sprite>().Length;

            var hostGo = new GameObject("World500xRuntimeDriverHost");
            var camGo = new GameObject("World500xTestCamera");
            try
            {
                var cam = camGo.AddComponent<Camera>();
                cam.transform.position = new Vector3(0f, 0f, -10f);

                var driver = hostGo.AddComponent<RpgWorld500xRuntimeDriver>();
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

                // Destroy host GameObject -> triggers OnDestroy -> Shutdown.
                UnityEngine.Object.DestroyImmediate(hostGo);
                hostGo = null;

                // The baseline was taken before creating our test Camera. It may
                // retain editor-native textures while it is alive, so tear down
                // both test-owned objects before asserting zero leaked assets.
                UnityEngine.Object.DestroyImmediate(camGo);
                camGo = null;

                Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Texture2D>().Length, texBefore);
                Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Sprite>().Length, spriteBefore);
            }
            finally
            {
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
