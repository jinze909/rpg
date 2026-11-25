using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFastTravelItineraryTests
    {
        [Test]
        public void PlanShortestItinerary_FindsOptimalMultiHopRouteAndFlagsHazardousWeatherLegs()
        {
            var atmosphere = new RpgWorld500xWeatherAndTimeOfDayService(initialMinutes: 600f, initialDay: 1);
            atmosphere.SetWeather(RpgWorldMapId.Soundwell, RpgWorld500xWeatherKind.Clear);
            atmosphere.SetWeather(RpgWorldMapId.RedSaltHarbor, RpgWorld500xWeatherKind.Clear);
            atmosphere.SetWeather(RpgWorldMapId.WhitewaterCrossing, RpgWorld500xWeatherKind.Clear);
            atmosphere.SetWeather(RpgWorldMapId.MineCamp, RpgWorld500xWeatherKind.GaleStorm);

            // Same-map itinerary -> 0 hops, 0 minutes.
            RpgWorld500xTravelItinerary local = RpgWorld500xFastTravelItineraryService.PlanShortestItinerary(
                RpgWorldMapId.Soundwell, RpgWorldMapId.Soundwell, atmosphere);
            Assert.IsTrue(local.IsReachable);
            Assert.AreEqual(0, local.HopCount);
            Assert.AreEqual(0, local.TotalTravelMinutesInGame);
            Assert.IsFalse(local.RecommendCampfireStop);

            // Direct 1-hop: Soundwell -> RedSaltHarbor (120 min, Safe).
            RpgWorld500xTravelItinerary direct = RpgWorld500xFastTravelItineraryService.PlanShortestItinerary(
                RpgWorldMapId.Soundwell, RpgWorldMapId.RedSaltHarbor, atmosphere);
            Assert.IsTrue(direct.IsReachable);
            Assert.AreEqual(1, direct.HopCount);
            Assert.AreEqual(120, direct.TotalTravelMinutesInGame);
            Assert.AreEqual(RpgWorld500xTravelRiskLevel.Safe, direct.HighestRiskLevel);
            Assert.IsFalse(direct.RecommendCampfireStop);

            // Optimal 2-hop: Soundwell -> RedSaltHarbor (120) -> MineCamp (150) = 270 min, Hazardous destination.
            RpgWorld500xTravelItinerary multiHop = RpgWorld500xFastTravelItineraryService.PlanShortestItinerary(
                RpgWorldMapId.Soundwell, RpgWorldMapId.MineCamp, atmosphere);
            Assert.IsTrue(multiHop.IsReachable);
            Assert.AreEqual(2, multiHop.HopCount);
            Assert.AreEqual(270, multiHop.TotalTravelMinutesInGame);
            Assert.AreEqual(RpgWorldMapId.RedSaltHarbor, multiHop.Legs[0].Route.ToMap);
            Assert.AreEqual(RpgWorldMapId.MineCamp, multiHop.Legs[1].Route.ToMap);
            Assert.AreEqual(RpgWorld500xTravelRiskLevel.Hazardous, multiHop.HighestRiskLevel);
            Assert.IsTrue(multiHop.RecommendCampfireStop);
            Assert.IsTrue(multiHop.SummaryZh.Contains("建议途经营火休整"));
        }

        [Test]
        public void PlanSafestItinerary_DetoursAroundHazardousHubAndMatchesShortestWhenClear()
        {
            var atmosphere = new RpgWorld500xWeatherAndTimeOfDayService(initialMinutes: 600f, initialDay: 1);
            atmosphere.SetWeather(RpgWorldMapId.Soundwell, RpgWorld500xWeatherKind.Clear);
            atmosphere.SetWeather(RpgWorldMapId.RedSaltHarbor, RpgWorld500xWeatherKind.GaleStorm);
            atmosphere.SetWeather(RpgWorldMapId.WhitewaterCrossing, RpgWorld500xWeatherKind.Clear);
            atmosphere.SetWeather(RpgWorldMapId.MineCamp, RpgWorld500xWeatherKind.Clear);

            // Shortest-time route goes through stormy RedSaltHarbor (Hazardous).
            RpgWorld500xTravelItinerary shortest = RpgWorld500xFastTravelItineraryService.PlanShortestItinerary(
                RpgWorldMapId.Soundwell, RpgWorldMapId.MineCamp, atmosphere);
            Assert.AreEqual(RpgWorldMapId.RedSaltHarbor, shortest.Legs[0].Route.ToMap);
            Assert.AreEqual(RpgWorld500xTravelRiskLevel.Hazardous, shortest.HighestRiskLevel);

            // Safest route detours via clear WhitewaterCrossing to avoid the GaleStorm hub.
            RpgWorld500xTravelItinerary safest = RpgWorld500xFastTravelItineraryService.PlanSafestItinerary(
                RpgWorldMapId.Soundwell, RpgWorldMapId.MineCamp, atmosphere);
            Assert.IsTrue(safest.IsReachable);
            Assert.AreEqual(2, safest.HopCount);
            Assert.AreEqual(RpgWorldMapId.WhitewaterCrossing, safest.Legs[0].Route.ToMap);
            Assert.AreEqual(RpgWorldMapId.MineCamp, safest.Legs[1].Route.ToMap);
            Assert.AreEqual(RpgWorld500xTravelRiskLevel.Safe, safest.HighestRiskLevel);
            Assert.IsTrue(safest.SummaryZh.Contains("安全绕行行程"));

            // When RedSaltHarbor clears up, safest route falls back to the fastest 270-minute path.
            atmosphere.SetWeather(RpgWorldMapId.RedSaltHarbor, RpgWorld500xWeatherKind.Clear);
            RpgWorld500xTravelItinerary clearSafest = RpgWorld500xFastTravelItineraryService.PlanSafestItinerary(
                RpgWorldMapId.Soundwell, RpgWorldMapId.MineCamp, atmosphere);
            Assert.AreEqual(270, clearSafest.TotalTravelMinutesInGame);
            Assert.AreEqual(RpgWorldMapId.RedSaltHarbor, clearSafest.Legs[0].Route.ToMap);
        }
    }
}
