using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    /// <summary>
    /// Four-region E2E regression tests for RpgWorld500xPlayModeController verifying
    /// that invalid re-initialization rejects undefined map IDs before tearing down
    /// the live session, menus, save slots, or save snapshot, and releases all native
    /// Texture2D/Sprite assets cleanly across Soundwell, RedSaltHarbor,
    /// WhitewaterCrossing, and MineCamp.
    /// </summary>
    public sealed class RpgWorld500xFourRegionPlayModeControllerInitGuardE2ETests
    {
        [Test]
        public void AllFourRegions_InvalidReinitializePreservesLiveSessionMenusSaveSnapshotAndDisposesCleanly()
        {
            IReadOnlyList<RpgExpandedMapSpec> specs = RpgExpandedWorldScale.AllSpecs;
            Assert.AreEqual(4, specs.Count);

            int texBefore = Resources.FindObjectsOfTypeAll<Texture2D>().Length;
            int spriteBefore = Resources.FindObjectsOfTypeAll<Sprite>().Length;

            for (int r = 0; r < specs.Count; r++)
            {
                RpgExpandedMapSpec spec = specs[r];
                var hostGo = new GameObject("FourRegionPlayModeInitGuard_" + spec.MapId);
                var ctrl = hostGo.AddComponent<RpgWorld500xPlayModeController>();

                try
                {
                    ctrl.Initialize(
                        overrideMap: spec.MapId,
                        saveStorage: new RpgWorld500xInMemorySaveSlotStorage(),
                        overrideTileSlices: new Sprite[] { null });

                    Assert.IsTrue(ctrl.IsInitialized, $"Controller failed to initialize on {spec.MapId}.");
                    RpgWorld500xSceneBootstrapper boot = ctrl.Bootstrapper;
                    RpgWorld500xMenuRouter menu = ctrl.MenuRouter;
                    RpgWorld500xSaveSlotManager slots = ctrl.SaveSlotManager;
                    RpgWorld500xExplorationHost liveHost = boot.Driver.Facade.Session.Host;

                    boot.Driver.Facade.BountyAndSupply.AddSaltSilver(45 + r * 10);
                    string savedBefore = boot.ExportSaveJson();
                    int childrenBefore = hostGo.transform.childCount;

                    Assert.Throws<ArgumentOutOfRangeException>(() =>
                        ctrl.Initialize(
                            overrideMap: (RpgWorldMapId)999,
                            overrideTileSlices: new Sprite[] { null }));
                    Assert.Throws<ArgumentOutOfRangeException>(() =>
                        ctrl.Initialize(
                            overrideMap: (RpgWorldMapId)(-1),
                            overrideTileSlices: new Sprite[] { null }));

                    Assert.IsTrue(ctrl.IsInitialized, $"Live controller lost initialized state on {spec.MapId}.");
                    Assert.AreSame(boot, ctrl.Bootstrapper);
                    Assert.AreSame(menu, ctrl.MenuRouter);
                    Assert.AreSame(slots, ctrl.SaveSlotManager);
                    Assert.AreSame(liveHost, ctrl.Bootstrapper.Driver.Facade.Session.Host);
                    Assert.AreEqual(spec.MapId, boot.Driver.Facade.CurrentMapId);
                    Assert.AreEqual(childrenBefore, hostGo.transform.childCount);
                    Assert.AreEqual(savedBefore, boot.ExportSaveJson());
                }
                finally
                {
                    ctrl.Shutdown();
                    UnityEngine.Object.DestroyImmediate(hostGo);
                }
            }

            Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Texture2D>().Length, texBefore,
                "Texture2D leak detected after four-region PlayModeController init-guard sweep.");
            Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Sprite>().Length, spriteBefore,
                "Sprite leak detected after four-region PlayModeController init-guard sweep.");
        }
    }
}
