using System;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xWeatherGazetteerItineraryE2ETests
    {
        [Test]
        public void WeatherBadgeGazetteerSafestToggleAndItineraryProvisioning_IntegrateEndToEndWithZeroLeak()
        {
            var root = new GameObject("World500xWeatherGazetteerItineraryE2ERoot");
            try
            {
                // Warm up one-time UGUI/font caches before steady-state leak baseline.
                using (var warmupBoot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.Soundwell,
                    overrideTileSlices: new Sprite[] { null }))
                using (var warmupWeather = new RpgWorld500xWeatherAdvisoryHudBadge(root.transform, warmupBoot))
                using (var warmupGazetteer = new RpgWorld500xWorldGazetteerHudPanel(root.transform, warmupBoot))
                using (var warmupItinerary = new RpgWorld500xTravelItineraryHudPanel(root.transform, warmupBoot))
                {
                    warmupWeather.Refresh(1f);
                    warmupGazetteer.Show();
                    warmupGazetteer.TryClickClose();
                    warmupItinerary.Show(RpgWorldMapId.MineCamp);
                    warmupItinerary.TryClickClose();
                }

                int texBefore = Resources.FindObjectsOfTypeAll<Texture2D>().Length;
                int spriteBefore = Resources.FindObjectsOfTypeAll<Sprite>().Length;

                using (var boot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.Soundwell,
                    overrideTileSlices: new Sprite[] { null }))
                using (var weatherBadge = new RpgWorld500xWeatherAdvisoryHudBadge(root.transform, boot))
                using (var gazetteer = new RpgWorld500xWorldGazetteerHudPanel(root.transform, boot))
                using (var itinerary = new RpgWorld500xTravelItineraryHudPanel(root.transform, boot))
                {
                    RpgWorld500xGrandRuntimeFacade facade = boot.Driver.Facade;
                    // Start at 09:00. The 120-minute first leg then arrives at
                    // 11:00 in the same 180-minute weather slot. Otherwise the
                    // automatic regional forecast legitimately changes at 09:00.
                    facade.Atmosphere.AdvanceRealSeconds(30f, RpgWorldMapId.Soundwell);
                    Assert.AreEqual(540f, facade.Atmosphere.TimeOfDayMinutes, 0.01f);
                    foreach (RpgWorldMapId map in Enum.GetValues(typeof(RpgWorldMapId)))
                    {
                        facade.Atmosphere.SetWeather(map, RpgWorld500xWeatherKind.Clear);
                    }

                    facade.Atmosphere.SetWeather(RpgWorldMapId.MineCamp, RpgWorld500xWeatherKind.GaleStorm);
                    facade.BountyAndSupply.AddSaltSilver(300);

                    // 1. WeatherAdvisoryHudBadge surfaces 4-region hazard digest while active map is Safe.
                    weatherBadge.Refresh(5f);
                    Assert.AreEqual(RpgWorld500xTravelRiskLevel.Safe, weatherBadge.LastRiskLevel);
                    Assert.AreEqual(1, weatherBadge.HazardousRegionCount);
                    Assert.AreNotEqual(RpgWorldMapId.MineCamp, weatherBadge.SafestRegionMapId);
                    StringAssert.Contains("【四境气象速报】", weatherBadge.WorldBulletinText);
                    StringAssert.Contains("高危预警", weatherBadge.WorldBulletinText);

                    // 2. WorldGazetteerHudPanel toggles safest-route mode and searches across regions.
                    Assert.IsTrue(gazetteer.Show());
                    Assert.AreEqual(4, gazetteer.DisplayedRowCount);
                    Assert.IsFalse(gazetteer.PreferSafestRoute);
                    StringAssert.Contains("路径：最短耗时", gazetteer.StatusText);

                    Assert.IsTrue(gazetteer.TryToggleSafestRoute());
                    Assert.IsTrue(gazetteer.PreferSafestRoute);
                    StringAssert.Contains("路径：避险优先", gazetteer.StatusText);

                    Assert.IsTrue(gazetteer.TrySearch("声井"));
                    Assert.Greater(gazetteer.LastSearchHitCount, 0);
                    Assert.IsTrue(gazetteer.TryClickClose());
                    Assert.IsFalse(gazetteer.IsVisible);

                    // 3. TravelItineraryHudPanel plans 2-hop hazardous route to MineCamp, auto-provisions, and departs.
                    Assert.IsTrue(itinerary.Show(RpgWorldMapId.MineCamp));
                    Assert.AreEqual(2, itinerary.LastItinerary.HopCount);
                    Assert.AreEqual(RpgWorld500xTravelRiskLevel.Hazardous, itinerary.LastItinerary.HighestRiskLevel);
                    Assert.Greater(itinerary.LastProvisioningPlan.Lines.Count, 0);

                    Assert.IsTrue(itinerary.TryClickAutoProvision());
                    Assert.IsTrue(itinerary.LastProvisioningReceipt.FullyProvisioned);
                    Assert.Greater(itinerary.LastProvisioningReceipt.TotalSpentSaltSilver, 0);
                    Assert.AreEqual(0, itinerary.LastProvisioningPlan.Lines.Count);

                    Assert.IsTrue(itinerary.TryClickDepartFirstLeg());
                    Assert.AreEqual(RpgWorldMapId.RedSaltHarbor, facade.CurrentMapId);
                    Assert.AreEqual(660f, facade.Atmosphere.TimeOfDayMinutes, 0.01f);

                    // Weather badge reflects new active map after regional arrival.
                    weatherBadge.Refresh(10f);
                    Assert.AreEqual(RpgWorld500xTravelRiskLevel.Safe, weatherBadge.LastRiskLevel);
                    Assert.AreEqual(1, weatherBadge.HazardousRegionCount);

                    Assert.IsTrue(itinerary.TryClickClose());
                    Assert.IsFalse(itinerary.IsVisible);
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
    }
}
