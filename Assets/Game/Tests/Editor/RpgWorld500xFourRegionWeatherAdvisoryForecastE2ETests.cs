using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionWeatherAdvisoryForecastE2ETests
    {
        [Test]
        public void WeatherTravelAdvisoryService_AcrossAllFourRegions_ClassifiesClearFogAndStormWithConsistentPhaseCountdowns()
        {
            var specs = RpgExpandedWorldScale.AllSpecs;
            Assert.That(specs.Count, Is.EqualTo(4));

            for (int i = 0; i < specs.Count; i++)
            {
                var mapId = specs[i].MapId;
                var clock = new RpgWorld500xWeatherAndTimeOfDayService(initialMinutes: 600f, initialDay: 1);

                clock.SetWeather(mapId, RpgWorld500xWeatherKind.Clear);
                var clear = RpgWorld500xWeatherTravelAdvisoryService.BuildAdvisory(
                    clock.GetSnapshot(mapId), elapsedSeconds: 5f);
                Assert.That(clear.RiskLevel, Is.EqualTo(RpgWorld500xTravelRiskLevel.Safe));
                Assert.IsFalse(clear.RecommendCampfireRest);
                Assert.That(clear.NextPhase, Is.EqualTo(RpgWorld500xTimePhase.Dusk));
                Assert.That(clear.MinutesUntilNextPhase, Is.GreaterThan(0));
                Assert.That(clear.AmbientWindows.Count, Is.GreaterThan(0));

                clock.SetWeather(mapId, RpgWorld500xWeatherKind.MistFog);
                var fog = RpgWorld500xWeatherTravelAdvisoryService.BuildAdvisory(
                    clock.GetSnapshot(mapId), elapsedSeconds: 5f);
                Assert.That(fog.RiskLevel, Is.EqualTo(RpgWorld500xTravelRiskLevel.StealthAdvantage));
                Assert.IsFalse(fog.RecommendCampfireRest);

                clock.SetWeather(mapId, RpgWorld500xWeatherKind.GaleStorm);
                var storm = RpgWorld500xWeatherTravelAdvisoryService.BuildAdvisory(
                    clock.GetSnapshot(mapId), elapsedSeconds: 5f);
                Assert.That(storm.RiskLevel, Is.EqualTo(RpgWorld500xTravelRiskLevel.Hazardous));
                Assert.IsTrue(storm.RecommendCampfireRest);
                StringAssert.Contains("营火休整", storm.TacticalAdvisoryZh);
            }
        }

        [Test]
        public void WeatherTravelAdvisoryService_AcrossAllFourRegions_MatchesClockAtAllFourPhaseBoundariesAndWrapsNegativeMinutes()
        {
            var specs = RpgExpandedWorldScale.AllSpecs;
            float[] before = { 299.99997f, 479.99997f, 1019.99994f, 1199.9999f };
            float[] at = { 300f, 480f, 1020f, 1200f };
            RpgWorld500xTimePhase[] next =
            {
                RpgWorld500xTimePhase.Dawn,
                RpgWorld500xTimePhase.Day,
                RpgWorld500xTimePhase.Dusk,
                RpgWorld500xTimePhase.Night
            };

            for (int r = 0; r < specs.Count; r++)
            {
                var mapId = specs[r].MapId;
                for (int i = 0; i < before.Length; i++)
                {
                    var clock = new RpgWorld500xWeatherAndTimeOfDayService(initialMinutes: before[i], initialDay: 1);
                    var report = RpgWorld500xWeatherTravelAdvisoryService.BuildAdvisory(clock.GetSnapshot(mapId));
                    Assert.That(clock.CurrentPhase, Is.Not.EqualTo(next[i]));
                    Assert.That(report.NextPhase, Is.EqualTo(next[i]));
                    Assert.That(report.MinutesUntilNextPhase, Is.EqualTo(1));

                    RpgWorld500xWeatherTravelAdvisoryService.ResolveNextPhase(at[i], out var following, out int minutes);
                    Assert.That(following, Is.EqualTo(next[(i + 1) % next.Length]));
                    Assert.That(minutes, Is.GreaterThan(1));
                }
            }

            RpgWorld500xWeatherTravelAdvisoryService.ResolveNextPhase(-60f, out var wrapped, out int wrappedMinutes);
            Assert.That(wrapped, Is.EqualTo(RpgWorld500xTimePhase.Dawn));
            Assert.That(wrappedMinutes, Is.EqualTo(360));

            RpgWorld500xWeatherTravelAdvisoryService.ResolveNextPhase(float.NaN, out var nanPhase, out int nanMinutes);
            Assert.That(nanPhase, Is.EqualTo(RpgWorld500xTimePhase.Dawn));
            Assert.That(nanMinutes, Is.EqualTo(300));
        }
    }
}
