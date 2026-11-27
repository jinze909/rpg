using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionAtmosphereOverrideAndPersistenceE2ETests
    {
        [Test]
        public void FourRegion_WeatherAndTimeOfDayService_RejectsUndefinedOverridesAndPreservesForecastParity()
        {
            IReadOnlyList<RpgExpandedMapSpec> specs = RpgExpandedWorldScale.AllSpecs;
            Assert.AreEqual(4, specs.Count);

            for (int r = 0; r < specs.Count; r++)
            {
                RpgWorldMapId region = specs[r].MapId;
                var service = new RpgWorld500xWeatherAndTimeOfDayService(
                    initialMinutes: 600f + (r * 45f),
                    initialDay: 2,
                    gameMinutesPerRealSecond: 1f);

                service.SetWeather(region, RpgWorld500xWeatherKind.GaleStorm);
                RpgWorld500xAtmosphereSnapshot before = service.GetSnapshot(region);
                Dictionary<string, int> saved = service.ExportPersistenceState();

                // Undefined weather and map IDs must not mutate live forecast or persistence state.
                service.SetWeather(region, (RpgWorld500xWeatherKind)999);
                service.SetWeather((RpgWorldMapId)255, RpgWorld500xWeatherKind.AshHaze);

                RpgWorld500xAtmosphereSnapshot after = service.GetSnapshot(region);
                Assert.AreEqual(before.Weather, after.Weather);
                Assert.AreEqual(before.WeatherDisplayName, after.WeatherDisplayName);
                Assert.AreEqual(before.MovementSpeedMultiplier, after.MovementSpeedMultiplier, 0.0001f);
                Assert.AreEqual(before.EncounterTensionMultiplier, after.EncounterTensionMultiplier, 0.0001f);
                Assert.AreEqual(RpgWorld500xWeatherKind.Clear, service.GetWeather((RpgWorldMapId)255));
                CollectionAssert.AreEquivalent(saved, service.ExportPersistenceState());

                // Restored instance advances along the exact same forecast schedule.
                var restored = new RpgWorld500xWeatherAndTimeOfDayService(gameMinutesPerRealSecond: 1f);
                restored.ImportPersistenceState(saved);
                Assert.AreEqual(before.Weather, restored.GetSnapshot(region).Weather);
                Assert.AreEqual(
                    restored.AdvanceRealSeconds(180f, region).Weather,
                    service.AdvanceRealSeconds(180f, region).Weather);

                service.SetWeather(region, RpgWorld500xWeatherKind.Clear);
                Assert.AreEqual(RpgWorld500xWeatherKind.Clear, service.GetWeather(region));
            }
        }

        [Test]
        public void FourRegion_WeatherAndTimeOfDayService_RestAtCampfireCalmsStormsAndPersistsAcrossAllFourRegions()
        {
            IReadOnlyList<RpgExpandedMapSpec> specs = RpgExpandedWorldScale.AllSpecs;
            Assert.AreEqual(4, specs.Count);

            for (int r = 0; r < specs.Count; r++)
            {
                RpgWorldMapId region = specs[r].MapId;
                var service = new RpgWorld500xWeatherAndTimeOfDayService(
                    initialMinutes: 1320f,
                    initialDay: 1,
                    gameMinutesPerRealSecond: 1f);

                service.SetWeather(region, RpgWorld500xWeatherKind.GaleStorm);
                Assert.AreEqual(RpgWorld500xWeatherKind.GaleStorm, service.GetWeather(region));

                RpgWorld500xAtmosphereSnapshot afterRest = service.RestAtCampfire(region);
                Assert.AreNotEqual(RpgWorld500xWeatherKind.GaleStorm, afterRest.Weather);
                Assert.IsFalse(string.IsNullOrEmpty(afterRest.FormattedClock));
                Assert.IsFalse(string.IsNullOrEmpty(afterRest.PhaseDisplayName));
                Assert.IsFalse(string.IsNullOrEmpty(afterRest.WeatherDisplayName));

                Dictionary<string, int> persisted = service.ExportPersistenceState();
                var reloaded = new RpgWorld500xWeatherAndTimeOfDayService(gameMinutesPerRealSecond: 1f);
                reloaded.ImportPersistenceState(persisted);
                RpgWorld500xAtmosphereSnapshot reloadedSnap = reloaded.GetSnapshot(region);
                Assert.AreEqual(afterRest.Weather, reloadedSnap.Weather);
                Assert.AreEqual(afterRest.FormattedClock, reloadedSnap.FormattedClock);
            }
        }
    }
}
