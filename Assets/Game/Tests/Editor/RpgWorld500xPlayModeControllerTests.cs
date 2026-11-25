using System;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xPlayModeControllerTests
    {
        [Test]
        public void PlayModeController_WiresModalRaycastersAndReusesSceneEventSystem()
        {
            var hostGo = new GameObject("World500xPointerInputTestHost");
            var sceneInput = new GameObject("Existing scene input", typeof(UnityEngine.EventSystems.EventSystem));
            var ctrl = hostGo.AddComponent<RpgWorld500xPlayModeController>();
            try
            {
                ctrl.Initialize(
                    overrideMap: RpgWorldMapId.Soundwell,
                    saveStorage: new RpgWorld500xInMemorySaveSlotStorage(),
                    overrideTileSlices: new Sprite[] { null });
                var uiRoot = hostGo.transform.Find("World500xPlayModeUiRoot");
                Assert.IsNotNull(uiRoot);
                Assert.IsNull(uiRoot.GetComponent<UnityEngine.EventSystems.EventSystem>(),
                    "Do not create a duplicate scene EventSystem.");
                var canvases = uiRoot.GetComponentsInChildren<Canvas>(true);
                Assert.AreEqual(10, canvases.Length);
                foreach (Canvas canvas in canvases)
                    Assert.IsNotNull(canvas.GetComponent<UnityEngine.UI.GraphicRaycaster>(), canvas.name);

                Assert.IsTrue(ctrl.MenuRouter.OpenModal(RpgWorld500xModalKind.CampMerchant));
                var merchant = uiRoot.Find("World500xCampMerchantHudPanel");
                Assert.IsTrue(merchant.GetComponent<UnityEngine.UI.GraphicRaycaster>().isActiveAndEnabled);
                ctrl.MenuRouter.CloseAll();
                Assert.IsFalse(merchant.GetComponent<UnityEngine.UI.GraphicRaycaster>().isActiveAndEnabled);

                // Journal is remounted per map outside the controller's UI root.
                var session = ctrl.Bootstrapper.Driver.Facade.Session;
                Assert.IsNotNull(session.JournalPanel.RootObject.GetComponent<UnityEngine.UI.GraphicRaycaster>());
                Assert.IsTrue(ctrl.Bootstrapper.SwitchMap(RpgWorldMapId.RedSaltHarbor));
                Assert.IsTrue(ctrl.MenuRouter.OpenModal(RpgWorld500xModalKind.QuestJournal));
                Assert.IsTrue(session.JournalPanel.RootObject.GetComponent<UnityEngine.UI.GraphicRaycaster>().isActiveAndEnabled);
            }
            finally
            {
                ctrl.Shutdown();
                UnityEngine.Object.DestroyImmediate(sceneInput);
                UnityEngine.Object.DestroyImmediate(hostGo);
            }
        }

        [Test]
        public void PlayModeController_CampMerchantSharesWalletStockAndReputationWithSave()
        {
            var hostGo = new GameObject("World500xSharedCampStoreTestHost");
            var ctrl = hostGo.AddComponent<RpgWorld500xPlayModeController>();
            try
            {
                ctrl.Initialize(
                    overrideMap: RpgWorldMapId.RedSaltHarbor,
                    saveStorage: new RpgWorld500xInMemorySaveSlotStorage(),
                    overrideTileSlices: new Sprite[] { null });
                var boot = ctrl.Bootstrapper;
                var supply = boot.Driver.Facade.BountyAndSupply;
                supply.AddSaltSilver(100);
                ctrl.CampHaven.Reputation.AddPoints(RpgWorldMapId.RedSaltHarbor, 800);
                string before = boot.ExportSaveJson();

                Assert.IsTrue(ctrl.MenuRouter.OpenModal(RpgWorld500xModalKind.CampMerchant));
                Assert.IsTrue(ctrl.MenuRouter.MerchantPanel.TryClickBuySupply("flare"));
                Assert.AreEqual(79, supply.SaltSilverBalance);
                Assert.AreEqual(1, supply.GetOwnedSupplyCount("flare"));
                Assert.IsTrue(ctrl.MenuRouter.MerchantPanel.FeedbackText.Contains("消耗盐银 21"));
                Assert.IsTrue(ctrl.MenuRouter.MerchantPanel.HeaderText.Contains("盐银余额 79"));

                string after = boot.ExportSaveJson();
                Assert.IsTrue(boot.TryImportSaveJson(before, out string restoreError), restoreError);
                Assert.AreEqual(100, supply.SaltSilverBalance);
                Assert.AreEqual(0, supply.GetOwnedSupplyCount("flare"));
                Assert.AreEqual(800, ctrl.CampHaven.Reputation.GetPoints(RpgWorldMapId.RedSaltHarbor));

                Assert.IsTrue(boot.TryImportSaveJson(after, out string purchaseError), purchaseError);
                Assert.AreEqual(79, supply.SaltSilverBalance);
                Assert.AreEqual(1, supply.GetOwnedSupplyCount("flare"));
                Assert.AreEqual(800, ctrl.CampHaven.Reputation.GetPoints(RpgWorldMapId.RedSaltHarbor));

                ctrl.Shutdown();
                ctrl.Initialize(
                    overrideMap: RpgWorldMapId.RedSaltHarbor,
                    saveStorage: new RpgWorld500xInMemorySaveSlotStorage(),
                    overrideTileSlices: new Sprite[] { null });
                Assert.AreEqual(0, ctrl.CampHaven.Reputation.GetPoints(RpgWorldMapId.RedSaltHarbor));
                Assert.AreEqual(0, ctrl.Bootstrapper.Driver.Facade.BountyAndSupply.GetOwnedSupplyCount("flare"));
            }
            finally
            {
                ctrl.Shutdown();
                UnityEngine.Object.DestroyImmediate(hostGo);
            }
        }

        [Test]
        public void PlayModeController_DrivesAll9ModalsOverviewReconPulseQuickAutoSaveAndDisposesWithZeroLeak()
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
                Assert.IsNotNull(ctrl.ReconPulseHud);
                Assert.IsNotNull(ctrl.WeatherAdvisoryHud);

                // Weather advisory HUD badge updates live on atmosphere change.
                ctrl.Bootstrapper.Driver.Facade.Atmosphere.SetWeather(RpgWorldMapId.Soundwell, RpgWorld500xWeatherKind.GaleStorm);
                ctrl.ProcessInputFrame(new RpgWorld500xInputFrame(), 0.05f);
                Assert.AreEqual(RpgWorld500xTravelRiskLevel.Hazardous, ctrl.WeatherAdvisoryHud.LastRiskLevel);
                Assert.IsTrue(ctrl.WeatherAdvisoryHud.LastRecommendCampfireRest);

                // Toggle overview map when no modal is open.
                ctrl.ProcessInputFrame(new RpgWorld500xInputFrame { ToggleOverviewMapPressed = true }, 0.05f);
                Assert.IsTrue(ctrl.Bootstrapper.Driver.Facade.Session.MinimapHud.IsOverviewMode);

                // Open ExpeditionMilestones modal -> movement and recon pulse are blocked.
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
                    InteractPressed = true,
                    ReconPulsePressed = true
                }, 0.4f);
                Assert.AreEqual(posWhileOpen, ctrl.Bootstrapper.Driver.Facade.Session.Host.PlayerPosition);
                Assert.AreEqual(0, ctrl.ReconPulseHud.LastPingCount);

                // Esc dismisses the modal, but the same input frame must not
                // move the party, interact with the world, toggle overview, or pulse recon.
                int interactionCount = ctrl.Bootstrapper.Driver.Facade.ActivityDirector.InteractionCount;
                bool overviewBeforeClose = ctrl.Bootstrapper.Driver.Facade.Session.MinimapHud.IsOverviewMode;
                ctrl.ProcessInputFrame(new RpgWorld500xInputFrame
                {
                    CloseModalPressed = true,
                    Move = Vector2.right,
                    Sprint = true,
                    InteractPressed = true,
                    ToggleOverviewMapPressed = true,
                    ReconPulsePressed = true
                }, 0.5f);
                Assert.AreEqual(RpgWorld500xModalKind.None, ctrl.MenuRouter.ActiveModal);
                Assert.AreEqual(posWhileOpen, ctrl.Bootstrapper.Driver.Facade.Session.Host.PlayerPosition,
                    "Closing a modal must not move the party until the next input frame.");
                Assert.AreEqual(interactionCount, ctrl.Bootstrapper.Driver.Facade.ActivityDirector.InteractionCount,
                    "Closing a modal must not activate a world object in the same input frame.");
                Assert.AreEqual(overviewBeforeClose,
                    ctrl.Bootstrapper.Driver.Facade.Session.MinimapHud.IsOverviewMode,
                    "The overview shortcut must not leak through an exiting modal.");
                Assert.AreEqual(0, ctrl.ReconPulseHud.LastPingCount,
                    "The recon pulse shortcut must not leak through an exiting modal.");

                // On the next open-world frame, ReconPulsePressed triggers the scan cleanly.
                ctrl.ProcessInputFrame(new RpgWorld500xInputFrame { ReconPulsePressed = true }, 0.1f);
                Assert.Greater(ctrl.ReconPulseHud.LastPingCount, 0);
                Assert.IsFalse(ctrl.ReconPulseHud.IsScanButtonInteractable);

                // Quick auto-save writes AutoSave slot cleanly.
                ctrl.Bootstrapper.Driver.Facade.BountyAndSupply.AddSaltSilver(75);
                ctrl.ProcessInputFrame(new RpgWorld500xInputFrame
                {
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
