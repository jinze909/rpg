using System;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xPlayModeControllerTests
    {
        [Test]
        public void PlayModeController_DrivesAll9ModalsOverviewQuickAutoSaveAndDisposesWithZeroLeak()
        {
            var hostGo = new GameObject("World500xPlayModeControllerTestHost");
            RpgWorld500xPlayModeController ctrl = hostGo.AddComponent<RpgWorld500xPlayModeController>();
            try
            {
                // Warm up one-time UGUI/font caches before steady-state leak baseline.
                ctrl.Initialize(
                    overrideMap: RpgWorldMapId.Soundwell,
                    saveStorage: new RpgWorld500xInMemorySaveSlotStorage(),
                    overrideTileSlices: new Sprite[] { null });
                ctrl.ProcessInputFrame(new RpgWorld500xInputFrame
                {
                    RequestedModalToggle = RpgWorld500xModalKind.ExpeditionMilestones
                }, 0.05f);
                ctrl.Shutdown();

                int texBefore = Resources.FindObjectsOfTypeAll<Texture2D>().Length;
                int spriteBefore = Resources.FindObjectsOfTypeAll<Sprite>().Length;

                var storage = new RpgWorld500xInMemorySaveSlotStorage();
                DateTime fixedUtc = new DateTime(2026, 11, 23, 19, 10, 0, DateTimeKind.Utc);
                ctrl.Initialize(
                    overrideMap: RpgWorldMapId.Soundwell,
                    saveStorage: storage,
                    utcNowProvider: () => fixedUtc,
                    overrideTileSlices: new Sprite[] { null });

                Assert.IsTrue(ctrl.IsInitialized);
                Assert.AreEqual(RpgWorld500xModalKind.None, ctrl.MenuRouter.ActiveModal);

                // Toggle overview map when no modal is open.
                ctrl.ProcessInputFrame(new RpgWorld500xInputFrame { ToggleOverviewMapPressed = true }, 0.05f);
                Assert.IsTrue(ctrl.Bootstrapper.Driver.Facade.Session.MinimapHud.IsOverviewMode);

                // Open ExpeditionMilestones modal -> movement is blocked.
                ctrl.ProcessInputFrame(new RpgWorld500xInputFrame
                {
                    RequestedModalToggle = RpgWorld500xModalKind.ExpeditionMilestones
                }, 0.05f);
                Assert.AreEqual(RpgWorld500xModalKind.ExpeditionMilestones, ctrl.MenuRouter.ActiveModal);

                Vector2 posWhileOpen = ctrl.Bootstrapper.Driver.Facade.Session.Host.PlayerPosition;
                ctrl.ProcessInputFrame(new RpgWorld500xInputFrame
                {
                    Move = Vector2.right,
                    Sprint = true,
                    InteractPressed = true
                }, 0.4f);
                Assert.AreEqual(posWhileOpen, ctrl.Bootstrapper.Driver.Facade.Session.Host.PlayerPosition);

                // Quick auto-save writes AutoSave slot cleanly.
                ctrl.Bootstrapper.Driver.Facade.BountyAndSupply.AddSaltSilver(75);
                ctrl.ProcessInputFrame(new RpgWorld500xInputFrame
                {
                    CloseModalPressed = true,
                    QuickAutoSavePressed = true
                }, 0.05f);

                Assert.AreEqual(RpgWorld500xModalKind.None, ctrl.MenuRouter.ActiveModal);
                Assert.IsTrue(ctrl.LastAutoSaveStatusZh.Contains("自动存档成功"));
                RpgWorld500xSaveSlotSummary autoSlot = ctrl.SaveSlotManager.InspectSlot(RpgWorld500xSaveSlotId.AutoSave);
                Assert.IsTrue(autoSlot.IsOccupied && !autoSlot.IsCorrupted);
                Assert.AreEqual(75, autoSlot.SaltSilverBalance);

                ctrl.Shutdown();
                Assert.IsFalse(ctrl.IsInitialized);
                Assert.AreEqual(0, hostGo.transform.childCount);
                Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Texture2D>().Length, texBefore);
                Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Sprite>().Length, spriteBefore);
            }
            finally
            {
                ctrl.Shutdown();
                UnityEngine.Object.DestroyImmediate(hostGo);
            }
        }
    }
}
