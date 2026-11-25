using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xWeatherTravelAdvisoryTests
    {
        [Test]
        public void BuildAdvisory_ClassifiesClearFogAndGaleStormWithAccuratePhaseCountdowns()
        {
            var svc = new RpgWorld500xWeatherAndTimeOfDayService(initialMinutes: 600f, initialDay: 1);

            // Clear Day (10:00 -> 420 minutes until Dusk at 17:00)
            svc.SetWeather(RpgWorldMapId.Soundwell, RpgWorld500xWeatherKind.Clear);
            RpgWorld500xTravelAdvisoryReport safe =
                RpgWorld500xWeatherTravelAdvisoryService.BuildAdvisory(svc.GetSnapshot(RpgWorldMapId.Soundwell), elapsedSeconds: 5f);
            Assert.AreEqual(RpgWorld500xTravelRiskLevel.Safe, safe.RiskLevel);
            Assert.IsFalse(safe.RecommendCampfireRest);
            Assert.AreEqual(RpgWorld500xTimePhase.Dusk, safe.NextPhase);
            Assert.AreEqual(420, safe.MinutesUntilNextPhase);
            Assert.AreEqual(3, safe.AmbientWindows.Count);

            // MistFog Day -> StealthAdvantage
            svc.SetWeather(RpgWorldMapId.WhitewaterCrossing, RpgWorld500xWeatherKind.MistFog);
            RpgWorld500xTravelAdvisoryReport stealth =
                RpgWorld500xWeatherTravelAdvisoryService.BuildAdvisory(svc.GetSnapshot(RpgWorldMapId.WhitewaterCrossing), elapsedSeconds: 5f);
            Assert.AreEqual(RpgWorld500xTravelRiskLevel.StealthAdvantage, stealth.RiskLevel);
            Assert.IsFalse(stealth.RecommendCampfireRest);
            Assert.IsTrue(stealth.TacticalAdvisoryZh.Contains("潜行"));

            // GaleStorm Night -> Hazardous + RecommendCampfireRest
            var nightSvc = new RpgWorld500xWeatherAndTimeOfDayService(initialMinutes: 1380f, initialDay: 2);
            nightSvc.SetWeather(RpgWorldMapId.RedSaltHarbor, RpgWorld500xWeatherKind.GaleStorm);
            RpgWorld500xTravelAdvisoryReport hazard =
                RpgWorld500xWeatherTravelAdvisoryService.BuildAdvisory(nightSvc.GetSnapshot(RpgWorldMapId.RedSaltHarbor), elapsedSeconds: 20f);
            Assert.AreEqual(RpgWorld500xTravelRiskLevel.Hazardous, hazard.RiskLevel);
            Assert.IsTrue(hazard.RecommendCampfireRest);
            Assert.AreEqual(RpgWorld500xTimePhase.Dawn, hazard.NextPhase);
            Assert.AreEqual(360, hazard.MinutesUntilNextPhase); // 60 min to midnight + 300 min to 05:00 Dawn
            Assert.IsTrue(hazard.TacticalAdvisoryZh.Contains("营火休整"));
        }

        [Test]
        public void ForecastAmbientWindows_TracksActiveAndCooldownSecondsDeterministically()
        {
            // Whitewater pontoon FogGust: cycle=38s, active=12s.
            var activeList = RpgWorld500xWeatherTravelAdvisoryService.ForecastAmbientWindows(
                RpgWorldMapId.WhitewaterCrossing, elapsedSeconds: 5f);
            Assert.AreEqual(3, activeList.Count);
            Assert.IsTrue(activeList[0].IsCurrentlyActive);
            Assert.AreEqual(7f, activeList[0].SecondsUntilNextChange, 0.01f);

            var cooldownList = RpgWorld500xWeatherTravelAdvisoryService.ForecastAmbientWindows(
                RpgWorldMapId.WhitewaterCrossing, elapsedSeconds: 20f);
            Assert.IsFalse(cooldownList[0].IsCurrentlyActive);
            Assert.AreEqual(18f, cooldownList[0].SecondsUntilNextChange, 0.01f);
        }
    }
}
