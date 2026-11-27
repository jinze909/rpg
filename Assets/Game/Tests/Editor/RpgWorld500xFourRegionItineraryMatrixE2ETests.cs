using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionItineraryMatrixE2ETests
    {
        [Test]
        public void AllFourRegions_ShortestAndSafestItinerariesAreReachableContiguousAndWeatherAware()
        {
            RpgWorldMapId[] regions =
            {
                RpgWorldMapId.Soundwell,
                RpgWorldMapId.RedSaltHarbor,
                RpgWorldMapId.WhitewaterCrossing,
                RpgWorldMapId.MineCamp
            };

            var clearAtmosphere = new RpgWorld500xWeatherAndTimeOfDayService(initialMinutes: 600f);
            var stormAtmosphere = new RpgWorld500xWeatherAndTimeOfDayService(initialMinutes: 600f);
            for (int r = 0; r < regions.Length; r++)
            {
                clearAtmosphere.SetWeather(regions[r], RpgWorld500xWeatherKind.Clear);
                stormAtmosphere.SetWeather(regions[r], RpgWorld500xWeatherKind.GaleStorm);
            }

            // Unknown endpoints fail closed against every valid region for both modes.
            var unknown = (RpgWorldMapId)255;
            for (int r = 0; r < regions.Length; r++)
            {
                foreach (bool safest in new[] { false, true })
                {
                    var fromUnknown = safest
                        ? RpgWorld500xFastTravelItineraryService.PlanSafestItinerary(unknown, regions[r], clearAtmosphere)
                        : RpgWorld500xFastTravelItineraryService.PlanShortestItinerary(unknown, regions[r], clearAtmosphere);
                    var toUnknown = safest
                        ? RpgWorld500xFastTravelItineraryService.PlanSafestItinerary(regions[r], unknown, clearAtmosphere)
                        : RpgWorld500xFastTravelItineraryService.PlanShortestItinerary(regions[r], unknown, clearAtmosphere);

                    Assert.IsFalse(fromUnknown.IsReachable);
                    Assert.IsFalse(toUnknown.IsReachable);
                    StringAssert.Contains("未找到有效区域路径", fromUnknown.SummaryZh);
                    StringAssert.Contains("未找到有效区域路径", toUnknown.SummaryZh);
                }
            }

            // All 16 ordered pairs across the 4 regions are reachable and contiguous.
            for (int i = 0; i < regions.Length; i++)
            {
                for (int j = 0; j < regions.Length; j++)
                {
                    RpgWorldMapId from = regions[i];
                    RpgWorldMapId to = regions[j];

                    var shortestClear = RpgWorld500xFastTravelItineraryService.PlanShortestItinerary(from, to, clearAtmosphere);
                    var safestClear = RpgWorld500xFastTravelItineraryService.PlanSafestItinerary(from, to, clearAtmosphere);
                    var shortestStorm = RpgWorld500xFastTravelItineraryService.PlanShortestItinerary(from, to, stormAtmosphere);

                    Assert.IsTrue(shortestClear.IsReachable);
                    Assert.IsTrue(safestClear.IsReachable);
                    Assert.IsTrue(shortestStorm.IsReachable);
                    Assert.GreaterOrEqual(safestClear.TotalTravelMinutesInGame, shortestClear.TotalTravelMinutesInGame);

                    // Campfire stop is recommended on Hazardous risk or long journeys (>= 240 in-game minutes).
                    Assert.AreEqual(RpgWorld500xTravelRiskLevel.Safe, shortestClear.HighestRiskLevel);
                    Assert.AreEqual(shortestClear.TotalTravelMinutesInGame >= 240, shortestClear.RecommendCampfireStop);
                    Assert.AreEqual(RpgWorld500xTravelRiskLevel.Hazardous, shortestStorm.HighestRiskLevel);
                    Assert.IsTrue(shortestStorm.RecommendCampfireStop);

                    if (from == to)
                    {
                        Assert.AreEqual(0, shortestClear.HopCount);
                        Assert.AreEqual(0, shortestClear.TotalTravelMinutesInGame);
                        Assert.IsEmpty(shortestClear.Legs);
                        StringAssert.Contains("当前已在目的地", shortestClear.SummaryZh);
                    }
                    else
                    {
                        Assert.GreaterOrEqual(shortestClear.HopCount, 1);
                        Assert.AreEqual(shortestClear.HopCount, shortestClear.Legs.Count);
                        Assert.Greater(shortestClear.TotalTravelMinutesInGame, 0);
                        Assert.AreEqual(from, shortestClear.Legs[0].Route.FromMap);
                        for (int h = 0; h + 1 < shortestClear.Legs.Count; h++)
                        {
                            Assert.AreEqual(shortestClear.Legs[h].Route.ToMap, shortestClear.Legs[h + 1].Route.FromMap);
                        }
                        Assert.AreEqual(to, shortestClear.Legs[shortestClear.Legs.Count - 1].Route.ToMap);
                    }
                }
            }
        }
    }
}
