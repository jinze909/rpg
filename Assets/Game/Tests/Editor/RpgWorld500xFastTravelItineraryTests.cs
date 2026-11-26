using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFastTravelItineraryTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void PlanItinerary_RejectsUnknownRegionsEvenForSameMap(bool safest)
        {
            var atmosphere = new RpgWorld500xWeatherAndTimeOfDayService(initialMinutes: 600f);
            var unknown = (RpgWorldMapId)255;
            var valid = RpgWorldMapId.Soundwell;
            var origins = new[] { unknown, unknown, valid };
            var destinations = new[] { unknown, valid, unknown };

            for (int i = 0; i < origins.Length; i++)
            {
                RpgWorld500xTravelItinerary result = safest
                    ? RpgWorld500xFastTravelItineraryService.PlanSafestItinerary(origins[i], destinations[i], atmosphere)
                    : RpgWorld500xFastTravelItineraryService.PlanShortestItinerary(origins[i], destinations[i], atmosphere);

                Assert.IsFalse(result.IsReachable);
                Assert.AreEqual(0, result.HopCount);
                Assert.AreEqual(0, result.TotalTravelMinutesInGame);
                Assert.IsEmpty(result.Legs);
                Assert.IsFalse(result.RecommendCampfireStop);
                StringAssert.Contains("未找到有效区域路径", result.SummaryZh);
                StringAssert.DoesNotContain("当前已在目的地", result.SummaryZh);
            }

            RpgWorld500xTravelItinerary local = safest
                ? RpgWorld500xFastTravelItineraryService.PlanSafestItinerary(valid, valid, atmosphere)
                : RpgWorld500xFastTravelItineraryService.PlanShortestItinerary(valid, valid, atmosphere);
            Assert.IsTrue(local.IsReachable);
            Assert.AreEqual(0, local.HopCount);
            StringAssert.Contains("当前已在目的地", local.SummaryZh);
        }

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
        public void PlanSafestItinerary_AvoidsStormyHubWhenAlternativeExistsAndPreservesReachabilityThroughArticulationHub()
        {
            var atmosphere = new RpgWorld500xWeatherAndTimeOfDayService(initialMinutes: 600f, initialDay: 1);
            atmosphere.SetWeather(RpgWorldMapId.Soundwell, RpgWorld500xWeatherKind.Clear);
            atmosphere.SetWeather(RpgWorldMapId.RedSaltHarbor, RpgWorld500xWeatherKind.GaleStorm);
            atmosphere.SetWeather(RpgWorldMapId.WhitewaterCrossing, RpgWorld500xWeatherKind.Clear);
            atmosphere.SetWeather(RpgWorldMapId.MineCamp, RpgWorld500xWeatherKind.Clear);

            // WhitewaterCrossing -> MineCamp has a direct 180-min clear mountain route avoiding stormy RedSaltHarbor.
            RpgWorld500xTravelItinerary safestDirect = RpgWorld500xFastTravelItineraryService.PlanSafestItinerary(
                RpgWorldMapId.WhitewaterCrossing, RpgWorldMapId.MineCamp, atmosphere);
            Assert.IsTrue(safestDirect.IsReachable);
            Assert.AreEqual(1, safestDirect.HopCount);
            Assert.AreEqual(180, safestDirect.TotalTravelMinutesInGame);
            Assert.AreEqual(RpgWorldMapId.MineCamp, safestDirect.Legs[0].Route.ToMap);
            Assert.AreEqual(RpgWorld500xTravelRiskLevel.Safe, safestDirect.HighestRiskLevel);
            Assert.IsFalse(safestDirect.RecommendCampfireStop);
            Assert.IsTrue(safestDirect.SummaryZh.Contains("安全绕行行程"));

            // Soundwell only connects via RedSaltHarbor (articulation hub): PlanSafestItinerary still stays reachable and flags Hazardous + campfire stop.
            RpgWorld500xTravelItinerary viaArticulation = RpgWorld500xFastTravelItineraryService.PlanSafestItinerary(
                RpgWorldMapId.Soundwell, RpgWorldMapId.MineCamp, atmosphere);
            Assert.IsTrue(viaArticulation.IsReachable);
            Assert.AreEqual(2, viaArticulation.HopCount);
            Assert.AreEqual(270, viaArticulation.TotalTravelMinutesInGame);
            Assert.AreEqual(RpgWorldMapId.RedSaltHarbor, viaArticulation.Legs[0].Route.ToMap);
            Assert.AreEqual(RpgWorldMapId.MineCamp, viaArticulation.Legs[1].Route.ToMap);
            Assert.AreEqual(RpgWorld500xTravelRiskLevel.Hazardous, viaArticulation.HighestRiskLevel);
            Assert.IsTrue(viaArticulation.RecommendCampfireStop);
            Assert.IsTrue(viaArticulation.SummaryZh.Contains("安全绕行行程"));

            // When RedSaltHarbor clears up, safest route matches the fastest 270-minute path cleanly.
            atmosphere.SetWeather(RpgWorldMapId.RedSaltHarbor, RpgWorld500xWeatherKind.Clear);
            RpgWorld500xTravelItinerary clearSafest = RpgWorld500xFastTravelItineraryService.PlanSafestItinerary(
                RpgWorldMapId.Soundwell, RpgWorldMapId.MineCamp, atmosphere);
            Assert.AreEqual(270, clearSafest.TotalTravelMinutesInGame);
            Assert.AreEqual(RpgWorldMapId.RedSaltHarbor, clearSafest.Legs[0].Route.ToMap);
            Assert.AreEqual(RpgWorld500xTravelRiskLevel.Safe, clearSafest.HighestRiskLevel);
        }
    }
}
