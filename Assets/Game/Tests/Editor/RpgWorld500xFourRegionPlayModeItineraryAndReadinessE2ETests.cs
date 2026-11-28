using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionPlayModeItineraryAndReadinessE2ETests
    {
        [Test]
        public void AllFourRegions_PlayModeControllerReadinessProvisionAndItineraryDepartureWorkEndToEnd()
        {
            IReadOnlyList<RpgExpandedMapSpec> specs = RpgExpandedWorldScale.AllSpecs;
            Assert.AreEqual(4, specs.Count);

            foreach (RpgExpandedMapSpec spec in specs)
            {
                RpgWorldMapId targetMap = spec.MapId == RpgWorldMapId.MineCamp
                    ? RpgWorldMapId.Soundwell
                    : RpgWorldMapId.MineCamp;

                var host = new GameObject("FourRegionItineraryReadiness_" + spec.MapId);
                var ctrl = host.AddComponent<RpgWorld500xPlayModeController>();
                try
                {
                    ctrl.Initialize(
                        overrideMap: spec.MapId,
                        saveStorage: new RpgWorld500xInMemorySaveSlotStorage(),
                        overrideTileSlices: new Sprite[] { null });

                    var facade = ctrl.Bootstrapper.Driver.Facade;
                    var shop = facade.BountyAndSupply;
                    facade.Atmosphere.SetWeather(spec.MapId, RpgWorld500xWeatherKind.Clear);
                    facade.Atmosphere.SetWeather(targetMap, RpgWorld500xWeatherKind.GaleStorm);
                    shop.AddSaltSilver(300);

                    // 1. Open ExpeditionReadiness via hotkey and auto-buy recommended supplies.
                    ctrl.ProcessInputFrame(new RpgWorld500xInputFrame
                    {
                        RequestedModalToggle = RpgWorld500xModalKind.ExpeditionReadiness
                    }, 0.05f);
                    Assert.AreEqual(RpgWorld500xModalKind.ExpeditionReadiness, ctrl.MenuRouter.ActiveModal, spec.MapId.ToString());

                    var buyRecBtn = host.transform
                        .Find("World500xPlayModeUiRoot/World500xExpeditionReadinessHudPanel/ReadinessModalBox/BuyRecommendedSuppliesButton")
                        .GetComponent<Button>();
                    Assert.IsNotNull(buyRecBtn, spec.MapId.ToString());
                    Assert.IsTrue(buyRecBtn.IsInteractable(), spec.MapId.ToString());
                    buyRecBtn.onClick.Invoke();
                    Assert.Less(shop.SaltSilverBalance, 300, spec.MapId.ToString());

                    // 2. Switch to TravelItinerary via hotkey, select targetMap, auto-provision if needed, and depart first leg.
                    ctrl.ProcessInputFrame(new RpgWorld500xInputFrame
                    {
                        RequestedModalToggle = RpgWorld500xModalKind.TravelItinerary
                    }, 0.05f);
                    Assert.AreEqual(RpgWorld500xModalKind.TravelItinerary, ctrl.MenuRouter.ActiveModal, spec.MapId.ToString());

                    var itineraryModal = host.transform.Find(
                        "World500xPlayModeUiRoot/World500xTravelItineraryHudPanel/ItineraryModalBox");
                    Assert.IsNotNull(itineraryModal, spec.MapId.ToString());

                    var destBtn = itineraryModal.Find("Dest_" + targetMap).GetComponent<Button>();
                    var provisionBtn = itineraryModal.Find("AutoProvisionItineraryButton").GetComponent<Button>();
                    var departBtn = itineraryModal.Find("DepartFirstLegButton").GetComponent<Button>();
                    Assert.IsNotNull(destBtn, spec.MapId.ToString());
                    Assert.IsNotNull(provisionBtn, spec.MapId.ToString());
                    Assert.IsNotNull(departBtn, spec.MapId.ToString());

                    destBtn.onClick.Invoke();
                    if (provisionBtn.IsInteractable())
                    {
                        provisionBtn.onClick.Invoke();
                    }

                    Assert.IsTrue(departBtn.IsInteractable(), spec.MapId.ToString());
                    departBtn.onClick.Invoke();
                    Assert.AreNotEqual(spec.MapId, facade.CurrentMapId, spec.MapId.ToString());

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
