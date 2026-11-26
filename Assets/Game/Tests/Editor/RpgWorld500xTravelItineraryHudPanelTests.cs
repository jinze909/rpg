using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xTravelItineraryHudPanelTests
    {
        [Test]
        public void TravelItineraryHudPanel_PlansMultiHopDepartsFirstLegRespectsLockAndZeroLeak()
        {
            var root = new GameObject("World500xTravelItineraryHudPanelTestRoot");
            try
            {
                // Warm up one-time UGUI/font caches before steady-state leak baseline.
                using (var warmupBoot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.Soundwell,
                    overrideTileSlices: new Sprite[] { null }))
                using (var warmupPanel = new RpgWorld500xTravelItineraryHudPanel(root.transform, warmupBoot))
                {
                    warmupPanel.Show(RpgWorldMapId.RedSaltHarbor);
                    warmupPanel.TryClickClose();
                }

                int texBefore = Resources.FindObjectsOfTypeAll<Texture2D>().Length;
                int spriteBefore = Resources.FindObjectsOfTypeAll<Sprite>().Length;

                using (var boot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.Soundwell,
                    overrideTileSlices: new Sprite[] { null }))
                using (var panel = new RpgWorld500xTravelItineraryHudPanel(root.transform, boot))
                {
                    // Hidden panel rejects clicks AND direct stale button callbacks without travelling.
                    Assert.IsFalse(panel.TryClickDepartFirstLeg());
                    Assert.IsFalse(panel.TrySelectDestination(RpgWorldMapId.MineCamp));
                    Button departBtn = root.transform
                        .Find("World500xTravelItineraryHudPanel/ItineraryModalBox/DepartFirstLegButton")
                        .GetComponent<Button>();
                    departBtn.onClick.Invoke();
                    Assert.AreEqual(RpgWorldMapId.Soundwell, boot.Driver.Facade.CurrentMapId);

                    Assert.IsTrue(panel.Show(RpgWorldMapId.MineCamp));
                    Assert.AreEqual(2, panel.LastItinerary.HopCount);
                    Assert.AreEqual(2, panel.DisplayedLegRowCount);
                    Assert.AreEqual(270, panel.LastItinerary.TotalTravelMinutesInGame);

                    // Parent CanvasGroup lock blocks destination switch, depart, close, AND direct stale depart clicks.
                    CanvasGroup group = root.AddComponent<CanvasGroup>();
                    group.interactable = false;
                    Assert.IsFalse(panel.TrySelectDestination(RpgWorldMapId.RedSaltHarbor));
                    Assert.IsFalse(panel.TryClickDepartFirstLeg());
                    Assert.IsFalse(panel.TryClickClose());
                    departBtn.onClick.Invoke();
                    Assert.AreEqual(RpgWorldMapId.Soundwell, boot.Driver.Facade.CurrentMapId);
                    group.interactable = true;

                    // Depart first leg: Soundwell -> RedSaltHarbor, remaining itinerary becomes 1 hop to MineCamp.
                    Assert.IsTrue(panel.TryClickDepartFirstLeg());
                    Assert.AreEqual(RpgWorldMapId.RedSaltHarbor, boot.Driver.Facade.CurrentMapId);
                    Assert.AreEqual(1, panel.LastItinerary.HopCount);
                    Assert.AreEqual(1, panel.DisplayedLegRowCount);
                    Assert.IsTrue(panel.FeedbackText.Contains("已沿"));

                    // Selecting current map disables depart button cleanly.
                    Assert.IsTrue(panel.TrySelectDestination(RpgWorldMapId.RedSaltHarbor));
                    Assert.AreEqual(0, panel.LastItinerary.HopCount);
                    Assert.IsFalse(panel.TryClickDepartFirstLeg());

                    Assert.IsTrue(panel.TryClickClose());
                    Assert.IsFalse(panel.IsVisible);
                }

                Assert.AreEqual(0, root.transform.childCount);
                Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Texture2D>().Length, texBefore);
                Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Sprite>().Length, spriteBefore);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TravelItineraryHudPanel_AutoProvisionsSelectedMultiHopDestinationRespectsLockAndDisablesWhenStocked()
        {
            var root = new GameObject("World500xItineraryProvisioningBtnTestRoot");
            try
            {
                using (var boot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.Soundwell,
                    overrideTileSlices: new Sprite[] { null }))
                using (var panel = new RpgWorld500xTravelItineraryHudPanel(root.transform, boot))
                {
                    RpgWorld500xGrandRuntimeFacade facade = boot.Driver.Facade;
                    facade.Atmosphere.SetWeather(RpgWorldMapId.Soundwell, RpgWorld500xWeatherKind.Clear);
                    facade.Atmosphere.SetWeather(RpgWorldMapId.RedSaltHarbor, RpgWorld500xWeatherKind.Clear);
                    facade.Atmosphere.SetWeather(RpgWorldMapId.MineCamp, RpgWorld500xWeatherKind.GaleStorm);
                    facade.BountyAndSupply.AddSaltSilver(300);

                    // Hidden panel must reject auto-provision click AND direct stale callback without spending Salt-Silver.
                    Assert.IsFalse(panel.TryClickAutoProvision());
                    Button provisionBtn = root.transform
                        .Find("World500xTravelItineraryHudPanel/ItineraryModalBox/AutoProvisionItineraryButton")
                        .GetComponent<Button>();
                    provisionBtn.onClick.Invoke();
                    Assert.AreEqual(300, facade.BountyAndSupply.SaltSilverBalance);

                    Assert.IsTrue(panel.Show(RpgWorldMapId.MineCamp));
                    Assert.Greater(panel.LastProvisioningPlan.Lines.Count, 0);

                    // Parent CanvasGroup lock blocks auto-provision click AND direct stale callback cleanly.
                    CanvasGroup group = root.AddComponent<CanvasGroup>();
                    group.interactable = false;
                    Assert.IsFalse(panel.TryClickAutoProvision());
                    provisionBtn.onClick.Invoke();
                    Assert.AreEqual(300, facade.BountyAndSupply.SaltSilverBalance);
                    group.interactable = true;

                    // Clicking auto-provision stocks all required 2-hop hazardous supplies and disables the button once stocked.
                    Assert.IsTrue(panel.TryClickAutoProvision());
                    Assert.IsTrue(panel.LastProvisioningReceipt.FullyProvisioned);
                    Assert.Greater(panel.LastProvisioningReceipt.TotalSpentSaltSilver, 0);
                    Assert.AreEqual(2, facade.BountyAndSupply.GetOwnedSupplyCount("rations"));
                    Assert.AreEqual(1, facade.BountyAndSupply.GetOwnedSupplyCount("lamp_oil"));
                    Assert.AreEqual(1, facade.BountyAndSupply.GetOwnedSupplyCount("flare"));
                    Assert.AreEqual(0, panel.LastProvisioningPlan.Lines.Count);
                    Assert.IsFalse(panel.TryClickAutoProvision());

                    // Immediately depart first leg to RedSaltHarbor.
                    Assert.IsTrue(panel.TryClickDepartFirstLeg());
                    Assert.AreEqual(RpgWorldMapId.RedSaltHarbor, facade.CurrentMapId);
                }

                Assert.AreEqual(0, root.transform.childCount);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TravelItineraryHudPanel_HonorsActiveSupplyBuffsToAvoidRedundantProvisioningSpend()
        {
            var root = new GameObject("World500xItinerarySupplyBuffTestRoot");
            try
            {
                var buffStore = new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    ["rpg.return.world.supply.item.lamp_oil"] = 1
                };
                var supplyUse = new RpgWorld500xFieldSupplyUseService(
                    k => buffStore.TryGetValue(k, out int v) ? v : 0,
                    (k, v) => buffStore[k] = v);
                Assert.IsTrue(supplyUse.TryConsumeSupply("lamp_oil", 2f).Succeeded);

                using (var boot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.Soundwell,
                    overrideTileSlices: new Sprite[] { null }))
                using (var panel = new RpgWorld500xTravelItineraryHudPanel(root.transform, boot, supplyUse))
                {
                    RpgWorld500xGrandRuntimeFacade facade = boot.Driver.Facade;
                    facade.Atmosphere.SetWeather(RpgWorldMapId.Soundwell, RpgWorld500xWeatherKind.Clear);
                    facade.Atmosphere.SetWeather(RpgWorldMapId.RedSaltHarbor, RpgWorld500xWeatherKind.Clear);
                    facade.Atmosphere.SetWeather(RpgWorldMapId.MineCamp, RpgWorld500xWeatherKind.GaleStorm);
                    facade.BountyAndSupply.AddSaltSilver(300);

                    Assert.IsTrue(panel.Show(RpgWorldMapId.MineCamp, elapsedSeconds: 10f));
                    Assert.IsTrue(panel.TryClickAutoProvision());
                    Assert.IsTrue(panel.LastProvisioningReceipt.FullyProvisioned);
                    // Active lamp_oil buff avoids redundant 22 Salt-Silver purchase while still stocking rations & flare.
                    Assert.AreEqual(0, facade.BountyAndSupply.GetOwnedSupplyCount("lamp_oil"));
                    Assert.AreEqual(2, facade.BountyAndSupply.GetOwnedSupplyCount("rations"));
                    Assert.AreEqual(1, facade.BountyAndSupply.GetOwnedSupplyCount("flare"));
                }

                Assert.AreEqual(0, root.transform.childCount);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
