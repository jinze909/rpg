using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionPlayModeAll16HotkeysE2ETests
    {
        private static readonly KeyCode[] All16ModalKeys =
        {
            KeyCode.J, KeyCode.M, KeyCode.B, KeyCode.K,
            KeyCode.T, KeyCode.N, KeyCode.O, KeyCode.L,
            KeyCode.U, KeyCode.C, KeyCode.H, KeyCode.V,
            KeyCode.P, KeyCode.X, KeyCode.G, KeyCode.Y
        };

        [Test]
        public void AllFourRegions_PlayModeControllerDrivesAll16ModalHotkeysDockSyncAndCloseCleanly()
        {
            var resolvedModals = new List<RpgWorld500xModalKind>(16);
            var unique = new HashSet<RpgWorld500xModalKind>();
            for (int i = 0; i < All16ModalKeys.Length; i++)
            {
                Assert.IsTrue(
                    RpgWorld500xPlayModeController.TryResolveModalHotkey(All16ModalKeys[i], out RpgWorld500xModalKind modal),
                    "Expected hotkey mapping for " + All16ModalKeys[i]);
                Assert.AreNotEqual(RpgWorld500xModalKind.None, modal);
                Assert.IsTrue(unique.Add(modal), "Duplicate modal mapping: " + modal);
                resolvedModals.Add(modal);
            }

            Assert.AreEqual(16, resolvedModals.Count);
            Assert.IsFalse(RpgWorld500xPlayModeController.TryResolveModalHotkey(KeyCode.W, out _));
            Assert.IsFalse(RpgWorld500xPlayModeController.TryResolveModalHotkey(KeyCode.R, out _));
            Assert.IsFalse(RpgWorld500xPlayModeController.TryResolveModalHotkey(KeyCode.Escape, out _));

            IReadOnlyList<RpgExpandedMapSpec> specs = RpgExpandedWorldScale.AllSpecs;
            Assert.AreEqual(4, specs.Count);

            foreach (RpgExpandedMapSpec spec in specs)
            {
                var host = new GameObject("FourRegionAll16Hotkeys_" + spec.MapId);
                var ctrl = host.AddComponent<RpgWorld500xPlayModeController>();
                try
                {
                    ctrl.Initialize(
                        overrideMap: spec.MapId,
                        saveStorage: new RpgWorld500xInMemorySaveSlotStorage(),
                        overrideTileSlices: new Sprite[] { null });

                    for (int i = 0; i < resolvedModals.Count; i++)
                    {
                        RpgWorld500xModalKind modal = resolvedModals[i];
                        ctrl.ProcessInputFrame(new RpgWorld500xInputFrame
                        {
                            RequestedModalToggle = modal
                        }, 0.05f);

                        Assert.AreEqual(modal, ctrl.MenuRouter.ActiveModal, spec.MapId + ":" + modal);
                        Assert.AreEqual(modal, ctrl.QuickMenuDockHud.ActiveHighlightedModal, spec.MapId + ":" + modal);
                    }

                    // Pressing the active modal's hotkey again toggles it closed cleanly.
                    RpgWorld500xModalKind lastModal = resolvedModals[resolvedModals.Count - 1];
                    ctrl.ProcessInputFrame(new RpgWorld500xInputFrame
                    {
                        RequestedModalToggle = lastModal
                    }, 0.05f);
                    Assert.AreEqual(RpgWorld500xModalKind.None, ctrl.MenuRouter.ActiveModal, spec.MapId.ToString());
                    Assert.AreEqual(RpgWorld500xModalKind.None, ctrl.QuickMenuDockHud.ActiveHighlightedModal, spec.MapId.ToString());

                    // Re-open and verify CloseModalPressed closes cleanly.
                    ctrl.ProcessInputFrame(new RpgWorld500xInputFrame
                    {
                        RequestedModalToggle = RpgWorld500xModalKind.WorldAtlas
                    }, 0.05f);
                    Assert.AreEqual(RpgWorld500xModalKind.WorldAtlas, ctrl.MenuRouter.ActiveModal, spec.MapId.ToString());

                    ctrl.ProcessInputFrame(new RpgWorld500xInputFrame { CloseModalPressed = true }, 0.05f);
                    Assert.AreEqual(RpgWorld500xModalKind.None, ctrl.MenuRouter.ActiveModal, spec.MapId.ToString());
                    Assert.AreEqual(RpgWorld500xModalKind.None, ctrl.QuickMenuDockHud.ActiveHighlightedModal, spec.MapId.ToString());

                    ctrl.Shutdown();
                    Assert.AreEqual(0, host.transform.childCount, spec.MapId.ToString());
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
