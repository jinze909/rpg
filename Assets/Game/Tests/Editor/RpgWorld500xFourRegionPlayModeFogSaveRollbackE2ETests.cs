using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionPlayModeFogSaveRollbackE2ETests
    {
        [Test]
        public void AllFourRegions_PlayModeControllerFogCoverageRollsBackOnSlotLoadAndSurvivesRegionRoundTrip()
        {
            IReadOnlyList<RpgExpandedMapSpec> specs = RpgExpandedWorldScale.AllSpecs;
            Assert.AreEqual(4, specs.Count);

            for (int i = 0; i < specs.Count; i++)
            {
                RpgWorldMapId mapId = specs[i].MapId;
                RpgWorldMapId otherMap = specs[(i + 1) % specs.Count].MapId;

                var host = new GameObject("FourRegionFogSaveRollback_" + mapId);
                var ctrl = host.AddComponent<RpgWorld500xPlayModeController>();
                try
                {
                    ctrl.Initialize(
                        overrideMap: mapId,
                        saveStorage: new RpgWorld500xInMemorySaveSlotStorage(),
                        overrideTileSlices: new Sprite[] { null });

                    var boot = ctrl.Bootstrapper;
                    var manager = ctrl.SaveSlotManager;

                    // 1. Capture baseline revealed fog chunks after initial Play Mode frame and save to Slot1.
                    ctrl.ProcessInputFrame(new RpgWorld500xInputFrame(), 0.05f);
                    int baselineVisited = boot.Driver.Facade.DistrictTracker.VisitedChunkCount;
                    Assert.Greater(baselineVisited, 0, mapId.ToString());
                    Assert.IsTrue(
                        manager.TrySaveSlot(boot, RpgWorld500xSaveSlotId.Slot1, out _, out string saveErr),
                        saveErr);

                    // 2. Explore distant regional cache tiles to reveal additional fog chunks.
                    IReadOnlyList<RpgSupplyCacheDef> caches = RpgWorld500xContentCatalog.GetCaches(mapId);
                    Assert.Greater(caches.Count, 0, mapId.ToString());
                    int expandedVisited = baselineVisited;
                    for (int c = 0; c < caches.Count; c++)
                    {
                        if (boot.Driver.Facade.Session.Host.TryPlaceAtWalkableTile(caches[c].Tile))
                        {
                            ctrl.ProcessInputFrame(new RpgWorld500xInputFrame(), 0.05f);
                            expandedVisited = boot.Driver.Facade.DistrictTracker.VisitedChunkCount;
                            if (expandedVisited > baselineVisited)
                            {
                                break;
                            }
                        }
                    }

                    Assert.Greater(expandedVisited, baselineVisited, "Expected distant tile to expand fog in " + mapId);

                    // 3. Reload Slot1 on the same map and verify post-save fog exploration rolls back cleanly.
                    Assert.IsTrue(
                        manager.TryLoadSlot(boot, RpgWorld500xSaveSlotId.Slot1, out _, out string loadErr),
                        loadErr);
                    Assert.AreEqual(mapId, boot.Driver.Facade.CurrentMapId, mapId.ToString());
                    Assert.AreEqual(baselineVisited, boot.Driver.Facade.DistrictTracker.VisitedChunkCount, mapId.ToString());

                    // 4. Round-trip to another region and back; persisted fog count must stay intact.
                    Assert.IsTrue(boot.SwitchMap(otherMap), mapId + "->" + otherMap);
                    Assert.IsTrue(boot.SwitchMap(mapId), otherMap + "->" + mapId);
                    Assert.AreEqual(baselineVisited, boot.Driver.Facade.DistrictTracker.VisitedChunkCount, mapId.ToString());

                    ctrl.Shutdown();
                    Assert.AreEqual(0, host.transform.childCount, mapId.ToString());
                }
                finally
                {
                    ctrl.Shutdown();
                    Object.DestroyImmediate(host);
                }
            }
        }
    }
}
