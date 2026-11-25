using System;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xEndToEndJourneyTests
    {
        [Test]
        public void EndToEndJourney_RestocksConsumesSupplySavesTravelsCrossRegionRollsBackAndZeroLeak()
        {
            var hostGo = new GameObject("World500xEndToEndJourneyTestHost");
            var ctrl = hostGo.AddComponent<RpgWorld500xPlayModeController>();
            try
            {
                // Warm up one-time UGUI/font caches before steady-state leak baseline.
                ctrl.Initialize(
                    overrideMap: RpgWorldMapId.Soundwell,
                    saveStorage: new RpgWorld500xInMemorySaveSlotStorage(),
                    overrideTileSlices: new Sprite[] { null });
                ctrl.MenuRouter.OpenModal(RpgWorld500xModalKind.TravelItinerary);
                ctrl.Shutdown();

                int texBefore = Resources.FindObjectsOfTypeAll<Texture2D>().Length;
                int spriteBefore = Resources.FindObjectsOfTypeAll<Sprite>().Length;

                DateTime clock = new DateTime(2026, 11, 25, 9, 0, 0, DateTimeKind.Utc);
                ctrl.Initialize(
                    overrideMap: RpgWorldMapId.Soundwell,
                    saveStorage: new RpgWorld500xInMemorySaveSlotStorage(),
                    utcNowProvider: () => clock,
                    overrideTileSlices: new Sprite[] { null });

                Assert.IsNotNull(ctrl.QuickMenuDockHud);
                Assert.AreEqual(16, ctrl.QuickMenuDockHud.DisplayedButtonCount);
                Assert.AreEqual(RpgWorld500xModalKind.None, ctrl.QuickMenuDockHud.ActiveHighlightedModal);

                // 1. Seed wallet and set GaleStorm on Soundwell; open ExpeditionReadiness via P hotkey.
                ctrl.Bootstrapper.Driver.Facade.BountyAndSupply.AddSaltSilver(300);
                ctrl.Bootstrapper.Driver.Facade.Atmosphere.SetWeather(RpgWorldMapId.Soundwell, RpgWorld500xWeatherKind.GaleStorm);

                Assert.IsTrue(RpgWorld500xPlayModeController.TryResolveModalHotkey(KeyCode.P, out RpgWorld500xModalKind prepModal));
                ctrl.ProcessInputFrame(new RpgWorld500xInputFrame { RequestedModalToggle = prepModal }, 0.1f);
                Assert.AreEqual(RpgWorld500xModalKind.ExpeditionReadiness, ctrl.MenuRouter.ActiveModal);
                Assert.AreEqual(RpgWorld500xModalKind.ExpeditionReadiness, ctrl.QuickMenuDockHud.ActiveHighlightedModal);
                Assert.IsTrue(ctrl.MenuRouter.ReadinessPanel.TryClickBuyRecommended());
                Assert.GreaterOrEqual(ctrl.Bootstrapper.Driver.Facade.BountyAndSupply.GetOwnedSupplyCount("lamp_oil"), 1);

                // Top up multi-hop cross-region rations for Soundwell -> MineCamp via CampProvisioningService.
                Assert.IsTrue(ctrl.MenuRouter.ReadinessPanel.TryAutoProvisionForDestination(
                    RpgWorldMapId.MineCamp, allowPartialIfUnderBudget: false));
                Assert.IsTrue(ctrl.MenuRouter.ReadinessPanel.LastProvisioningReceipt.FullyProvisioned);
                Assert.AreEqual(2, ctrl.Bootstrapper.Driver.Facade.BountyAndSupply.GetOwnedSupplyCount("rations"));

                // Close readiness modal, then consume lamp_oil (slot 3) on the next open-world frame.
                ctrl.ProcessInputFrame(new RpgWorld500xInputFrame { CloseModalPressed = true }, 0.1f);
                Assert.AreEqual(RpgWorld500xModalKind.None, ctrl.MenuRouter.ActiveModal);
                Assert.AreEqual(RpgWorld500xModalKind.None, ctrl.QuickMenuDockHud.ActiveHighlightedModal);

                ctrl.ProcessInputFrame(new RpgWorld500xInputFrame { RequestedSupplySlotIndex = 3 }, 0.1f);
                Assert.IsTrue(ctrl.SupplyHotbarHud.ActiveBuffsText.Contains("灯油照明"));

                RpgWorld500xExpeditionDigest digest = ctrl.BuildExpeditionDigest();
                Assert.AreEqual(RpgWorldMapId.Soundwell, digest.MapId);
                Assert.IsTrue(digest.FormattedDigestZh.Contains("灯油照明"));

                int savedSilver = ctrl.Bootstrapper.Driver.Facade.BountyAndSupply.SaltSilverBalance;

                // 2. Save checkpoint to Slot1 via SaveSlots modal (O hotkey).
                Assert.IsTrue(RpgWorld500xPlayModeController.TryResolveModalHotkey(KeyCode.O, out RpgWorld500xModalKind saveModal));
                ctrl.ProcessInputFrame(new RpgWorld500xInputFrame { RequestedModalToggle = saveModal }, 0.1f);
                Assert.AreEqual(RpgWorld500xModalKind.SaveSlots, ctrl.MenuRouter.ActiveModal);
                Assert.AreEqual(RpgWorld500xModalKind.SaveSlots, ctrl.QuickMenuDockHud.ActiveHighlightedModal);
                Assert.IsTrue(ctrl.MenuRouter.SaveSlotPanel.TryClickSave(RpgWorld500xSaveSlotId.Slot1));

                // 3. Plan multi-hop itinerary to MineCamp (Y hotkey) and depart first leg -> RedSaltHarbor.
                Assert.IsTrue(RpgWorld500xPlayModeController.TryResolveModalHotkey(KeyCode.Y, out RpgWorld500xModalKind itineraryModal));
                ctrl.ProcessInputFrame(new RpgWorld500xInputFrame { RequestedModalToggle = itineraryModal }, 0.1f);
                Assert.AreEqual(RpgWorld500xModalKind.TravelItinerary, ctrl.MenuRouter.ActiveModal);
                Assert.AreEqual(RpgWorld500xModalKind.TravelItinerary, ctrl.QuickMenuDockHud.ActiveHighlightedModal);
                Assert.IsTrue(ctrl.MenuRouter.ItineraryPanel.TrySelectDestination(RpgWorldMapId.MineCamp));
                Assert.AreEqual(2, ctrl.MenuRouter.ItineraryPanel.LastItinerary.HopCount);
                Assert.IsTrue(ctrl.MenuRouter.ItineraryPanel.TryClickDepartFirstLeg());
                Assert.AreEqual(RpgWorldMapId.RedSaltHarbor, ctrl.Bootstrapper.Driver.Facade.CurrentMapId);

                // 4. Inspect BountyBoard (V hotkey) and launch/toggle WorldGazetteer via QuickMenuDockHud button click on RedSaltHarbor.
                Assert.IsTrue(RpgWorld500xPlayModeController.TryResolveModalHotkey(KeyCode.V, out RpgWorld500xModalKind bountyModal));
                ctrl.ProcessInputFrame(new RpgWorld500xInputFrame { RequestedModalToggle = bountyModal }, 0.1f);
                Assert.AreEqual(RpgWorld500xModalKind.BountyBoard, ctrl.MenuRouter.ActiveModal);
                Assert.AreEqual(RpgWorld500xModalKind.BountyBoard, ctrl.QuickMenuDockHud.ActiveHighlightedModal);
                Assert.AreEqual(3, ctrl.MenuRouter.BountyBoardPanel.DisplayedRowCount);

                Assert.IsTrue(ctrl.QuickMenuDockHud.TryClickModalButton(RpgWorld500xModalKind.WorldGazetteer));
                Assert.AreEqual(RpgWorld500xModalKind.WorldGazetteer, ctrl.MenuRouter.ActiveModal);
                Assert.AreEqual(RpgWorld500xModalKind.WorldGazetteer, ctrl.QuickMenuDockHud.ActiveHighlightedModal);
                Assert.AreEqual(4, ctrl.MenuRouter.GazetteerPanel.DisplayedRowCount);

                // Clicking the active QuickMenuDockHud button toggles it closed cleanly.
                Assert.IsTrue(ctrl.QuickMenuDockHud.TryClickModalButton(RpgWorld500xModalKind.WorldGazetteer));
                Assert.AreEqual(RpgWorld500xModalKind.None, ctrl.MenuRouter.ActiveModal);
                Assert.AreEqual(RpgWorld500xModalKind.None, ctrl.QuickMenuDockHud.ActiveHighlightedModal);

                // 5. Mutate wallet on RedSaltHarbor, then reload Slot1 -> rolls back cleanly to Soundwell, savedSilver & provisioned rations.
                ctrl.Bootstrapper.Driver.Facade.BountyAndSupply.AddSaltSilver(55);
                ctrl.ProcessInputFrame(new RpgWorld500xInputFrame { RequestedModalToggle = saveModal }, 0.1f);
                Assert.IsTrue(ctrl.MenuRouter.SaveSlotPanel.TryClickLoad(RpgWorld500xSaveSlotId.Slot1));
                Assert.AreEqual(RpgWorldMapId.Soundwell, ctrl.Bootstrapper.Driver.Facade.CurrentMapId);
                Assert.AreEqual(savedSilver, ctrl.Bootstrapper.Driver.Facade.BountyAndSupply.SaltSilverBalance);
                Assert.AreEqual(2, ctrl.Bootstrapper.Driver.Facade.BountyAndSupply.GetOwnedSupplyCount("rations"));

                ctrl.Shutdown();
                Assert.IsTrue(ctrl.QuickMenuDockHud == null || ctrl.QuickMenuDockHud.IsDisposed);
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
