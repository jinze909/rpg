using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xPlayModeControllerTests
    {
        [Test]
        public void PlayModeController_MapsAll16ModalsToUniqueNonCollidingHotkeysAndDrivesNewPanels()
        {
            KeyCode[] keys =
            {
                KeyCode.J, KeyCode.M, KeyCode.B, KeyCode.K,
                KeyCode.T, KeyCode.N, KeyCode.O, KeyCode.L,
                KeyCode.U, KeyCode.C, KeyCode.H, KeyCode.V,
                KeyCode.P, KeyCode.X, KeyCode.G, KeyCode.Y
            };

            var mappedModals = new HashSet<RpgWorld500xModalKind>();
            for (int i = 0; i < keys.Length; i++)
            {
                Assert.IsTrue(RpgWorld500xPlayModeController.TryResolveModalHotkey(keys[i], out RpgWorld500xModalKind modal),
                    "Expected hotkey mapping for " + keys[i]);
                Assert.AreNotEqual(RpgWorld500xModalKind.None, modal);
                Assert.IsTrue(mappedModals.Add(modal), "Duplicate modal mapping: " + modal);
            }

            Assert.AreEqual(16, mappedModals.Count);
            Assert.IsFalse(RpgWorld500xPlayModeController.TryResolveModalHotkey(KeyCode.W, out _));
            Assert.IsFalse(RpgWorld500xPlayModeController.TryResolveModalHotkey(KeyCode.R, out _));
            Assert.IsFalse(RpgWorld500xPlayModeController.TryResolveModalHotkey(KeyCode.Escape, out _));

            var hostGo = new GameObject("World500xPlayMode16PanelHotkeyTestHost");
            var ctrl = hostGo.AddComponent<RpgWorld500xPlayModeController>();
            try
            {
                ctrl.Initialize(
                    overrideMap: RpgWorldMapId.Soundwell,
                    saveStorage: new RpgWorld500xInMemorySaveSlotStorage(),
                    overrideTileSlices: new Sprite[] { null });

                Assert.IsNotNull(ctrl.QuickMenuDockHud);
                Assert.AreEqual(16, ctrl.QuickMenuDockHud.DisplayedButtonCount);

                RpgWorld500xModalKind[] newModals =
                {
                    RpgWorld500xModalKind.CacheAndShortcut,
                    RpgWorld500xModalKind.ExpeditionDigest,
                    RpgWorld500xModalKind.BountyBoard,
                    RpgWorld500xModalKind.ExpeditionReadiness,
                    RpgWorld500xModalKind.DangerTacticalBrief,
                    RpgWorld500xModalKind.WorldGazetteer,
                    RpgWorld500xModalKind.TravelItinerary
                };

                for (int i = 0; i < newModals.Length; i++)
                {
                    ctrl.ProcessInputFrame(new RpgWorld500xInputFrame
                    {
                        RequestedModalToggle = newModals[i]
                    }, 0.05f);
                    Assert.AreEqual(newModals[i], ctrl.MenuRouter.ActiveModal);
                    Assert.AreEqual(newModals[i], ctrl.QuickMenuDockHud.ActiveHighlightedModal);
                }

                ctrl.ProcessInputFrame(new RpgWorld500xInputFrame { CloseModalPressed = true }, 0.05f);
                Assert.AreEqual(RpgWorld500xModalKind.None, ctrl.MenuRouter.ActiveModal);
                Assert.AreEqual(RpgWorld500xModalKind.None, ctrl.QuickMenuDockHud.ActiveHighlightedModal);
            }
            finally
            {
                ctrl.Shutdown();
                UnityEngine.Object.DestroyImmediate(hostGo);
            }
        }

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
                Assert.AreEqual(19, canvases.Length);
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
        public void PlayModeController_ResolveActiveDangerVictory_CreditsSilverReputationAndBanner()
        {
            var hostGo = new GameObject("World500xDangerVictoryTestHost");
            var ctrl = hostGo.AddComponent<RpgWorld500xPlayModeController>();
            try
            {
                ctrl.Initialize(
                    overrideMap: RpgWorldMapId.Soundwell,
                    saveStorage: new RpgWorld500xInMemorySaveSlotStorage(),
                    overrideTileSlices: new Sprite[] { null });

                // Without an active danger encounter, resolving returns false cleanly.
                Assert.IsFalse(ctrl.TryResolveActiveDangerVictory(true, out _, out _));

                // Trigger DangerZone sw.sunken-drain at (680, 640) via bridge and resolve with Lock-Break.
                RpgWorldOrkExplorationBridge bridge = ctrl.Bootstrapper.Driver.Facade.Session.Host.Bridge;
                Assert.IsTrue(bridge.TryTriggerDangerZoneEncounter("sw.sunken-drain", new Vector2(680f, 640f)));
                Assert.IsTrue(ctrl.TryResolveActiveDangerVictory(
                    lockBreakAchieved: true,
                    out RpgDangerZoneEncounterResult encounter,
                    out RpgWorld500xDangerVictoryReward reward));

                Assert.AreEqual("sw.sunken-drain", encounter.MarkerId);
                Assert.IsTrue(encounter.LockBreakBonus);
                Assert.Greater(reward.SaltSilverReward, 0);
                Assert.Greater(reward.ReputationGain, 0);
                Assert.AreEqual(reward.SaltSilverReward, ctrl.Bootstrapper.Driver.Facade.BountyAndSupply.SaltSilverBalance);
                Assert.AreEqual(reward.ReputationGain, ctrl.CampHaven.Reputation.GetPoints(RpgWorldMapId.Soundwell));
                Assert.IsTrue(ctrl.LastDangerVictoryBannerZh.Contains("破锁精通"));

                // Production entry omits explicit store delegates; first clear and loot must still persist.
                string clearKey = RpgWorld500xDangerVictoryRewardService.FirstClearKey(encounter.MarkerId);
                Assert.IsTrue(reward.IsFirstClear);
                Assert.AreEqual(1, ctrl.Bootstrapper.Store[clearKey]);
                Assert.AreEqual(1, reward.BonusSupplyQuantity);
                Assert.IsNotEmpty(reward.BonusSupplyId);
                Assert.AreEqual(reward.BonusSupplyQuantity,
                    ctrl.Bootstrapper.Driver.Facade.BountyAndSupply.GetOwnedSupplyCount(reward.BonusSupplyId));

                string saved = ctrl.Bootstrapper.ExportSaveJson();
                using (var restored = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.Soundwell,
                    overrideTileSlices: new Sprite[] { null }))
                {
                    Assert.IsTrue(restored.TryImportSaveJson(saved, out string error), error);
                    Assert.AreEqual(1, restored.Store[clearKey]);
                    Assert.AreEqual(reward.BonusSupplyQuantity,
                        restored.Driver.Facade.BountyAndSupply.GetOwnedSupplyCount(reward.BonusSupplyId));
                    var preview = RpgWorld500xDangerVictoryRewardService.PreviewVictoryReward(
                        RpgWorldMapId.Soundwell, encounter.MarkerId, false,
                        key => restored.Store.TryGetValue(key, out int value) ? value : 0);
                    Assert.IsFalse(preview.IsFirstClear);
                    Assert.AreEqual(0, preview.BonusSupplyQuantity);
                }

                // Duplicate resolution is rejected cleanly.
                Assert.IsFalse(ctrl.TryResolveActiveDangerVictory(true, out _, out _));
            }
            finally
            {
                ctrl.Shutdown();
                UnityEngine.Object.DestroyImmediate(hostGo);
            }
        }

        [UnityTest]
        public IEnumerator PlayModeController_SupplyHotbarReservesSeparateHudRowAndCapturesEvidence()
        {
            yield return new EnterPlayMode(expectDomainReload: true);
            var hostGo = new GameObject("World500xSupplyHudLayoutEvidence");
            var ctrl = hostGo.AddComponent<RpgWorld500xPlayModeController>();
            try
            {
                ctrl.Initialize(overrideMap: RpgWorldMapId.Soundwell,
                    saveStorage: new RpgWorld500xInMemorySaveSlotStorage(),
                    overrideTileSlices: new Sprite[] { null });
                var ui = hostGo.transform.Find("World500xPlayModeUiRoot");
                var supply = ui.Find("World500xFieldSupplyHotbarHud/SupplyHotbarPanel") as RectTransform;
                var recon = ui.Find("World500xReconPulseHudBar/ReconPulsePanel") as RectTransform;
                var weather = ui.Find("World500xWeatherAdvisoryHudBadge/WeatherAdvisoryCard") as RectTransform;
                Assert.IsNotNull(supply);
                Assert.IsNotNull(recon);
                Assert.IsNotNull(weather);
                Assert.AreEqual(0f, supply.anchorMin.y);
                Assert.AreEqual(0f, supply.pivot.y);
                Assert.GreaterOrEqual(supply.anchoredPosition.y,
                    Mathf.Max(recon.anchoredPosition.y + recon.sizeDelta.y,
                        weather.anchoredPosition.y + weather.sizeDelta.y) + 8f);
                var shop = ctrl.Bootstrapper.Driver.Facade.BountyAndSupply;
                shop.AddSaltSilver(100);
                Assert.AreEqual(RpgCampSupplyPurchaseStatus.Succeeded, shop.TryPurchaseSupply("lamp_oil", 2));
                ctrl.ProcessInputFrame(new RpgWorld500xInputFrame { RequestedSupplySlotIndex = 3 }, 0.1f);
                Assert.AreEqual(1, shop.GetOwnedSupplyCount("lamp_oil"));
                Font expectedFont = Resources.Load<Font>("Soundwell/Fonts/SoundwellChinese");
                Assert.IsNotNull(expectedFont, "The pinned OFL font must be imported.");
                foreach (var label in supply.GetComponentsInChildren<UnityEngine.UI.Text>(true))
                {
                    Assert.AreSame(expectedFont, label.font);
                    Assert.GreaterOrEqual(label.fontSize, 14);
                    foreach (char glyph in label.text)
                    {
                        if (!char.IsWhiteSpace(glyph))
                            Assert.IsTrue(label.font.HasCharacter(glyph), "Missing hotbar glyph: " + glyph);
                    }
                }
                yield return null;
                Canvas.ForceUpdateCanvases();
                SoundwellSmokeHost.CaptureStoryCanvas(supply.GetComponentInParent<Canvas>(),
                    "world500x-live-supply-hotbar-active");
            }
            finally
            {
                ctrl.Shutdown();
                UnityEngine.Object.Destroy(hostGo);
            }
            yield return null;
            yield return new ExitPlayMode();
        }

        [Test]
        public void PlayModeController_SupplyHotbarConsumesPersistedStockAndBuildsExpeditionDigest()
        {
            var hostGo = new GameObject("World500xSupplyHotbarDigestTestHost");
            var ctrl = hostGo.AddComponent<RpgWorld500xPlayModeController>();
            try
            {
                ctrl.Initialize(
                    overrideMap: RpgWorldMapId.Soundwell,
                    saveStorage: new RpgWorld500xInMemorySaveSlotStorage(),
                    overrideTileSlices: new Sprite[] { null });

                var shop = ctrl.Bootstrapper.Driver.Facade.BountyAndSupply;
                shop.AddSaltSilver(100);
                // Slot 3 is lamp_oil (22 Salt-Silver).
                Assert.AreEqual(RpgCampSupplyPurchaseStatus.Succeeded, shop.TryPurchaseSupply("lamp_oil", 1));
                Assert.AreEqual(1, shop.GetOwnedSupplyCount("lamp_oil"));

                var button = hostGo.transform.Find(
                    "World500xPlayModeUiRoot/World500xFieldSupplyHotbarHud/SupplyHotbarPanel/SupplySlot_lamp_oil")
                    .GetComponent<UnityEngine.UI.Button>();
                ctrl.SupplyHotbarHud.Refresh(0f);
                Assert.IsTrue(button.IsInteractable());

                // Modal open blocks hotbar consumption from both hardware and UGUI.
                ctrl.ProcessInputFrame(new RpgWorld500xInputFrame
                {
                    RequestedModalToggle = RpgWorld500xModalKind.WorldAtlas,
                    RequestedSupplySlotIndex = 3
                }, 0.1f);
                Assert.AreEqual(1, shop.GetOwnedSupplyCount("lamp_oil"));

                Assert.IsFalse(button.IsInteractable());
                button.OnPointerClick(new UnityEngine.EventSystems.PointerEventData(
                    UnityEngine.EventSystems.EventSystem.current)
                {
                    button = UnityEngine.EventSystems.PointerEventData.InputButton.Left
                });
                Assert.AreEqual(1, shop.GetOwnedSupplyCount("lamp_oil"));

                // Dismissing modal does not leak hotbar consumption in the same frame.
                ctrl.ProcessInputFrame(new RpgWorld500xInputFrame
                {
                    CloseModalPressed = true,
                    RequestedSupplySlotIndex = 3
                }, 0.1f);
                Assert.AreEqual(1, shop.GetOwnedSupplyCount("lamp_oil"));

                Assert.IsFalse(button.IsInteractable());
                ctrl.ProcessInputFrame(default, 0.1f);
                Assert.IsTrue(button.IsInteractable());

                // Next open-world frame consumes lamp_oil from shared persisted store and activates buff.
                ctrl.ProcessInputFrame(new RpgWorld500xInputFrame
                {
                    RequestedSupplySlotIndex = 3
                }, 0.1f);
                Assert.AreEqual(0, shop.GetOwnedSupplyCount("lamp_oil"));
                Assert.IsTrue(ctrl.SupplyHotbarHud.ActiveBuffsText.Contains("灯油照明"));

                RpgWorld500xExpeditionDigest digest = ctrl.BuildExpeditionDigest();
                Assert.AreEqual(RpgWorldMapId.Soundwell, digest.MapId);
                Assert.AreEqual(78, digest.SaltSilverBalance);
                Assert.IsTrue(digest.FormattedDigestZh.Contains("灯油照明"));
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
                Assert.IsNotNull(ctrl.SupplyHotbarHud);

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
