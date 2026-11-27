using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionItineraryAllPairsMatrixE2ETests
    {
        [Test]
        public void FastTravelItineraryService_AllSixteenRegionPairs_ShortestAndSafestStayReachableAndConsistentUnderClearWeather()
        {
            var specs = RpgExpandedWorldScale.AllSpecs;
            Assert.That(specs.Count, Is.EqualTo(4));

            var atmosphere = new RpgWorld500xWeatherAndTimeOfDayService(initialMinutes: 600f, initialDay: 1);
            for (int i = 0; i < specs.Count; i++)
            {
                atmosphere.SetWeather(specs[i].MapId, RpgWorld500xWeatherKind.Clear);
            }

            for (int i = 0; i < specs.Count; i++)
            {
                for (int j = 0; j < specs.Count; j++)
                {
                    var origin = specs[i].MapId;
                    var destination = specs[j].MapId;

                    var shortest = RpgWorld500xFastTravelItineraryService.PlanShortestItinerary(origin, destination, atmosphere);
                    var safest = RpgWorld500xFastTravelItineraryService.PlanSafestItinerary(origin, destination, atmosphere);

                    Assert.IsTrue(shortest.IsReachable);
                    Assert.IsTrue(safest.IsReachable);
                    Assert.That(shortest.HighestRiskLevel, Is.EqualTo(RpgWorld500xTravelRiskLevel.Safe));
                    Assert.That(safest.HighestRiskLevel, Is.EqualTo(RpgWorld500xTravelRiskLevel.Safe));

                    if (origin == destination)
                    {
                        Assert.That(shortest.HopCount, Is.EqualTo(0));
                        Assert.That(safest.HopCount, Is.EqualTo(0));
                        Assert.That(shortest.TotalTravelMinutesInGame, Is.EqualTo(0));
                        Assert.That(safest.TotalTravelMinutesInGame, Is.EqualTo(0));
                        Assert.IsEmpty(shortest.Legs);
                        Assert.IsEmpty(safest.Legs);
                        StringAssert.Contains("当前已在目的地", shortest.SummaryZh);
                        StringAssert.Contains("当前已在目的地", safest.SummaryZh);
                    }
                    else
                    {
                        Assert.That(shortest.HopCount, Is.GreaterThanOrEqualTo(1));
                        Assert.That(shortest.Legs.Count, Is.EqualTo(shortest.HopCount));
                        Assert.That(safest.Legs.Count, Is.EqualTo(safest.HopCount));
                        Assert.That(shortest.TotalTravelMinutesInGame, Is.GreaterThan(0));
                        Assert.That(safest.TotalTravelMinutesInGame, Is.EqualTo(shortest.TotalTravelMinutesInGame));
                        Assert.That(shortest.Legs[shortest.Legs.Count - 1].Route.ToMap, Is.EqualTo(destination));
                        Assert.That(safest.Legs[safest.Legs.Count - 1].Route.ToMap, Is.EqualTo(destination));

                        int summedMinutes = 0;
                        for (int k = 0; k < shortest.Legs.Count; k++)
                        {
                            summedMinutes += shortest.Legs[k].Route.TravelMinutesInGame;
                        }

                        Assert.That(summedMinutes, Is.EqualTo(shortest.TotalTravelMinutesInGame));
                        StringAssert.Contains("【跨境行程】", shortest.SummaryZh);
                        StringAssert.Contains("【安全绕行行程】", safest.SummaryZh);
                    }
                }
            }
        }

        [Test]
        public void FastTravelItineraryService_AllDistinctPairs_FlagsHazardousCampfireUnderAllStormAndSupportsNullAtmosphere()
        {
            var specs = RpgExpandedWorldScale.AllSpecs;
            Assert.That(specs.Count, Is.EqualTo(4));

            var stormAtmosphere = new RpgWorld500xWeatherAndTimeOfDayService(initialMinutes: 600f, initialDay: 1);
            for (int i = 0; i < specs.Count; i++)
            {
                stormAtmosphere.SetWeather(specs[i].MapId, RpgWorld500xWeatherKind.GaleStorm);
            }

            for (int i = 0; i < specs.Count; i++)
            {
                for (int j = 0; j < specs.Count; j++)
                {
                    var origin = specs[i].MapId;
                    var destination = specs[j].MapId;

                    var nullShortest = RpgWorld500xFastTravelItineraryService.PlanShortestItinerary(origin, destination, null);
                    var nullSafest = RpgWorld500xFastTravelItineraryService.PlanSafestItinerary(origin, destination, null);
                    Assert.IsTrue(nullShortest.IsReachable);
                    Assert.IsTrue(nullSafest.IsReachable);

                    if (origin == destination)
                    {
                        continue;
                    }

                    var stormShortest = RpgWorld500xFastTravelItineraryService.PlanShortestItinerary(origin, destination, stormAtmosphere);
                    var stormSafest = RpgWorld500xFastTravelItineraryService.PlanSafestItinerary(origin, destination, stormAtmosphere);

                    Assert.IsTrue(stormShortest.IsReachable);
                    Assert.IsTrue(stormSafest.IsReachable);
                    Assert.That(stormShortest.HighestRiskLevel, Is.EqualTo(RpgWorld500xTravelRiskLevel.Hazardous));
                    Assert.That(stormSafest.HighestRiskLevel, Is.EqualTo(RpgWorld500xTravelRiskLevel.Hazardous));
                    Assert.IsTrue(stormShortest.RecommendCampfireStop);
                    Assert.IsTrue(stormSafest.RecommendCampfireStop);
                }
            }
        }
    }
}
