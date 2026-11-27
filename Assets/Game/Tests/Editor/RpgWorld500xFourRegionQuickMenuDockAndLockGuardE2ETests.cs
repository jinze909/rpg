using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    /// <summary>
    /// Four-region E2E regression tests for RpgWorld500xQuickMenuDockHud wired inside
    /// RpgWorld500xPlayModeController covering all 16 modal launch buttons, active
    /// highlight synchronization, ancestor CanvasGroup lock/unlock recovery, and zero
    /// native Texture2D/Sprite leaks across Soundwell, RedSaltHarbor,
    /// WhitewaterCrossing, and MineCamp.
    /// </summary>
    public sealed class RpgWorld500xFourRegionQuickMenuDockAndLockGuardE2ETests
    {
        [Test]
        public void AllFourRegions_QuickMenuDockLaunchesAll16ModalsRespectsAncestorLockAndDisposesCleanly()
        {
            var allModals = (RpgWorld500xModalKind[])Enum.GetValues(typeof(RpgWorld500xModalKind));
            var nonNoneModals = new List<RpgWorld500xModalKind>();
            for (int i = 0; i < allModals.Length; i++)
            {
                if (allModals[i] != RpgWorld500xModalKind.None)
                {
                    nonNoneModals.Add(allModals[i]);
                }
            }

            Assert.AreEqual(16, nonNoneModals.Count);

            // Warm up one-time UGUI/font caches before steady-state leak baseline.
            var warmupGo = new GameObject("FourRegionQuickMenuDockWarmup");
            var warmupCtrl = warmupGo.AddComponent<RpgWorld500xPlayModeController>();
            try
            {
                warmupCtrl.Initialize(
                    overrideMap: RpgWorldMapId.Soundwell,
                    saveStorage: new RpgWorld500xInMemorySaveSlotStorage(),
                    overrideTileSlices: new Sprite[] { null });
            }
            finally
            {
                warmupCtrl.Shutdown();
                UnityEngine.Object.DestroyImmediate(warmupGo);
            }

            int texBefore = Resources.FindObjectsOfTypeAll<Texture2D>().Length;
            int spriteBefore = Resources.FindObjectsOfTypeAll<Sprite>().Length;

            IReadOnlyList<RpgExpandedMapSpec> specs = RpgExpandedWorldScale.AllSpecs;
            Assert.AreEqual(4, specs.Count);

            for (int r = 0; r < specs.Count; r++)
            {
                RpgExpandedMapSpec spec = specs[r];
                var hostGo = new GameObject("FourRegionQuickMenuDock_" + spec.MapId);
                var ctrl = hostGo.AddComponent<RpgWorld500xPlayModeController>();

                try
                {
                    ctrl.Initialize(
                        overrideMap: spec.MapId,
                        saveStorage: new RpgWorld500xInMemorySaveSlotStorage(),
                        overrideTileSlices: new Sprite[] { null });

                    RpgWorld500xQuickMenuDockHud dock = ctrl.QuickMenuDockHud;
                    Assert.IsNotNull(dock, $"QuickMenuDockHud missing on {spec.MapId}.");
                    Assert.AreEqual(16, dock.DisplayedButtonCount);

                    // Launch every one of the 16 modals through the dock and verify highlight sync.
                    for (int m = 0; m < nonNoneModals.Count; m++)
                    {
                        RpgWorld500xModalKind kind = nonNoneModals[m];
                        Assert.IsTrue(dock.TryClickModalButton(kind),
                            $"Dock button click failed for {kind} on {spec.MapId}.");
                        Assert.AreEqual(kind, ctrl.MenuRouter.ActiveModal,
                            $"MenuRouter.ActiveModal mismatch for {kind} on {spec.MapId}.");
                        Assert.AreEqual(kind, dock.ActiveHighlightedModal,
                            $"QuickMenuDockHud.ActiveHighlightedModal mismatch for {kind} on {spec.MapId}.");
                    }

                    // Ancestor CanvasGroup lock must block dock clicks without altering active modal.
                    RpgWorld500xModalKind lockedModal = ctrl.MenuRouter.ActiveModal;
                    CanvasGroup ancestorGroup = hostGo.AddComponent<CanvasGroup>();
                    ancestorGroup.interactable = false;
                    Assert.IsFalse(dock.TryClickModalButton(lockedModal),
                        $"Dock allowed click under ancestor CanvasGroup lock on {spec.MapId}.");
                    Assert.AreEqual(lockedModal, ctrl.MenuRouter.ActiveModal);
                    Assert.AreEqual(lockedModal, dock.ActiveHighlightedModal);

                    // Unlocking restores normal toggle-close behavior.
                    ancestorGroup.interactable = true;
                    Assert.IsTrue(dock.TryClickModalButton(lockedModal),
                        $"Dock failed to toggle close after CanvasGroup unlock on {spec.MapId}.");
                    Assert.AreEqual(RpgWorld500xModalKind.None, ctrl.MenuRouter.ActiveModal);
                    Assert.AreEqual(RpgWorld500xModalKind.None, dock.ActiveHighlightedModal);
                }
                finally
                {
                    ctrl.Shutdown();
                    UnityEngine.Object.DestroyImmediate(hostGo);
                }
            }

            Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Texture2D>().Length, texBefore,
                "Texture2D leak detected after four-region QuickMenuDockHud sweep.");
            Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Sprite>().Length, spriteBefore,
                "Sprite leak detected after four-region QuickMenuDockHud sweep.");
        }
    }
}
