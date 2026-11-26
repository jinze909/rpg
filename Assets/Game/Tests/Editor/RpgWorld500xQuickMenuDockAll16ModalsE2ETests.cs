using System;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xQuickMenuDockAll16ModalsE2ETests
    {
        [Test]
        public void QuickMenuDockHud_CyclesAll16ModalsEnforcesMovementGuardRespectsCanvasGroupLockWithZeroLeak()
        {
            var uiRoot = new GameObject("World500xQuickMenuDock16ModalsUiRoot");
            // Warm up one-time UGUI/font caches before steady-state leak baseline.
            var warmupGo = new GameObject("World500xQuickMenuDock16WarmupHost");
            var warmupCtrl = warmupGo.AddComponent<RpgWorld500xPlayModeController>();
            try
            {
                warmupCtrl.Initialize(
                    overrideMap: RpgWorldMapId.Soundwell,
                    saveStorage: new RpgWorld500xInMemorySaveSlotStorage(),
                    overrideTileSlices: new Sprite[] { null });
                using (var warmupDock = new RpgWorld500xQuickMenuDockHud(uiRoot.transform, warmupCtrl.MenuRouter))
                {
                    foreach (RpgWorld500xModalKind kind in (RpgWorld500xModalKind[])Enum.GetValues(typeof(RpgWorld500xModalKind)))
                    {
                        if (kind == RpgWorld500xModalKind.None) continue;
                        warmupDock.TryClickModalButton(kind);
                    }

                    warmupCtrl.MenuRouter.CloseAll();
                }
            }
            finally
            {
                warmupCtrl.Shutdown();
                UnityEngine.Object.DestroyImmediate(warmupGo);
            }

            int texBefore = Resources.FindObjectsOfTypeAll<Texture2D>().Length;
            int spriteBefore = Resources.FindObjectsOfTypeAll<Sprite>().Length;

            var hostGo = new GameObject("World500xQuickMenuDock16E2EHost");
            var ctrl = hostGo.AddComponent<RpgWorld500xPlayModeController>();
            try
            {
                ctrl.Initialize(
                    overrideMap: RpgWorldMapId.Soundwell,
                    saveStorage: new RpgWorld500xInMemorySaveSlotStorage(),
                    overrideTileSlices: new Sprite[] { null });

                var router = ctrl.MenuRouter;
                var host = ctrl.Bootstrapper.Driver.Facade.Session.Host;

                using (var dock = new RpgWorld500xQuickMenuDockHud(uiRoot.transform, router))
                {
                    int testedModalCount = 0;
                    RpgWorld500xModalKind lastModal = RpgWorld500xModalKind.None;

                    foreach (RpgWorld500xModalKind kind in (RpgWorld500xModalKind[])Enum.GetValues(typeof(RpgWorld500xModalKind)))
                    {
                        if (kind == RpgWorld500xModalKind.None)
                        {
                            continue;
                        }

                        Assert.IsTrue(dock.TryClickModalButton(kind), "Failed to open modal via dock: " + kind);
                        Assert.AreEqual(kind, router.ActiveModal);
                        Assert.AreEqual(kind, dock.ActiveHighlightedModal);
                        Assert.IsTrue(router.HasAnyModalOpen);

                        // Movement input must remain suppressed while any modal is open.
                        Vector2 posBefore = host.PlayerPosition;
                        router.StepFrameWithModalGuard(Vector2.right, 0.25f, sprinting: true, interactPressed: true);
                        Assert.AreEqual(posBefore, host.PlayerPosition, "Player moved while modal open: " + kind);

                        lastModal = kind;
                        testedModalCount++;
                    }

                    Assert.AreEqual(16, testedModalCount);

                    // Clicking the active modal button toggles it closed cleanly.
                    Assert.IsTrue(dock.TryClickModalButton(lastModal));
                    Assert.AreEqual(RpgWorld500xModalKind.None, router.ActiveModal);
                    Assert.AreEqual(RpgWorld500xModalKind.None, dock.ActiveHighlightedModal);
                    Assert.IsFalse(router.HasAnyModalOpen);

                    // Parent CanvasGroup lock blocks dock clicks.
                    CanvasGroup group = uiRoot.AddComponent<CanvasGroup>();
                    group.interactable = false;
                    Assert.IsFalse(dock.TryClickModalButton(RpgWorld500xModalKind.TravelItinerary));
                    Assert.AreEqual(RpgWorld500xModalKind.None, router.ActiveModal);
                    group.interactable = true;
                }

                Assert.AreEqual(0, uiRoot.transform.childCount);
            }
            finally
            {
                ctrl.Shutdown();
                UnityEngine.Object.DestroyImmediate(hostGo);
                UnityEngine.Object.DestroyImmediate(uiRoot);
            }

            Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Texture2D>().Length, texBefore);
            Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Sprite>().Length, spriteBefore);
        }
    }
}
