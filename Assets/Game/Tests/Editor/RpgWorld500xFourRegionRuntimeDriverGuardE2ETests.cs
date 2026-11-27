using System;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionRuntimeDriverGuardE2ETests
    {
        [Test]
        public void AllFourRegions_InvalidReinitializationPreservesLiveDriverRuntimeWithZeroLeak()
        {
            // Warm up one-time UGUI/font caches before steady-state leak baseline.
            var warmupGo = new GameObject("World500xFourRegionDriverWarmupHost");
            var warmupDriver = warmupGo.AddComponent<RpgWorld500xRuntimeDriver>();
            try
            {
                warmupDriver.Initialize(
                    overrideMap: RpgWorldMapId.Soundwell,
                    overrideTileSlices: new Sprite[] { null });
                warmupDriver.TickFrame(Vector2.zero, 0.1f);
            }
            finally
            {
                warmupDriver.Shutdown();
                UnityEngine.Object.DestroyImmediate(warmupGo);
            }

            int texBefore = Resources.FindObjectsOfTypeAll<Texture2D>().Length;
            int spriteBefore = Resources.FindObjectsOfTypeAll<Sprite>().Length;

            RpgWorldMapId[] regions =
            {
                RpgWorldMapId.Soundwell,
                RpgWorldMapId.RedSaltHarbor,
                RpgWorldMapId.WhitewaterCrossing,
                RpgWorldMapId.MineCamp
            };

            var hostGo = new GameObject("World500xFourRegionDriverGuardE2EHost");
            var driver = hostGo.AddComponent<RpgWorld500xRuntimeDriver>();
            try
            {
                driver.Initialize(
                    overrideMap: regions[0],
                    overrideTileSlices: new Sprite[] { null });

                for (int i = 0; i < regions.Length; i++)
                {
                    RpgWorldMapId mapId = regions[i];
                    if (i > 0)
                    {
                        Assert.IsTrue(driver.SwitchMap(mapId), "Expected map switch to " + mapId);
                    }

                    driver.TickFrame(Vector2.right, 0.2f);
                    Assert.AreEqual(mapId, driver.Facade.CurrentMapId);

                    var liveFacade = driver.Facade;
                    var liveHost = liveFacade.Session.Host;
                    Vector2 playerPos = liveHost.PlayerPosition;
                    int liveChildren = hostGo.transform.childCount;
                    string liveCaption = driver.LastEnvironmentCaptionZh;
                    string liveWaypoint = driver.LastNearestWaypointReadoutZh;

                    Assert.Throws<ArgumentOutOfRangeException>(() =>
                        driver.Initialize(
                            overrideMap: (RpgWorldMapId)999,
                            overrideTileSlices: new Sprite[] { null }));

                    Assert.IsTrue(driver.IsInitialized);
                    Assert.AreEqual(mapId, driver.Facade.CurrentMapId);
                    Assert.AreSame(liveFacade, driver.Facade);
                    Assert.AreSame(liveHost, driver.Facade.Session.Host);
                    Assert.AreEqual(playerPos, driver.Facade.Session.Host.PlayerPosition);
                    Assert.AreEqual(liveChildren, hostGo.transform.childCount);
                    Assert.AreEqual(liveCaption, driver.LastEnvironmentCaptionZh);
                    Assert.AreEqual(liveWaypoint, driver.LastNearestWaypointReadoutZh);
                }
            }
            finally
            {
                driver.Shutdown();
                UnityEngine.Object.DestroyImmediate(hostGo);
            }

            Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Texture2D>().Length, texBefore);
            Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Sprite>().Length, spriteBefore);
        }
    }
}
