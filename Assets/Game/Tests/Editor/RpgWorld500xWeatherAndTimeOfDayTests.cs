using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xWeatherAndTimeOfDayTests
    {
        [Test]
        public void TimePhases_ResolveAccuratelyAcross24HourCycle()
        {
            Assert.AreEqual(RpgWorld500xTimePhase.Dawn, RpgWorld500xWeatherAndTimeOfDayService.ResolveTimePhase(360f));
            Assert.AreEqual(RpgWorld500xTimePhase.Day, RpgWorld500xWeatherAndTimeOfDayService.ResolveTimePhase(720f));
            Assert.AreEqual(RpgWorld500xTimePhase.Dusk, RpgWorld500xWeatherAndTimeOfDayService.ResolveTimePhase(1080f));
            Assert.AreEqual(RpgWorld500xTimePhase.Night, RpgWorld500xWeatherAndTimeOfDayService.ResolveTimePhase(1320f));
            Assert.AreEqual(RpgWorld500xTimePhase.Night, RpgWorld500xWeatherAndTimeOfDayService.ResolveTimePhase(120f));
            Assert.AreEqual("06:00", RpgWorld500xWeatherAndTimeOfDayService.FormatClock(360f));

            var service = new RpgWorld500xWeatherAndTimeOfDayService(initialMinutes: 1400f, initialDay: 1, gameMinutesPerRealSecond: 10f);
            RpgWorld500xAtmosphereSnapshot snapshot = service.AdvanceRealSeconds(6f, RpgWorldMapId.Soundwell);
            Assert.AreEqual(2, snapshot.DayCount);
            Assert.AreEqual("00:20", snapshot.FormattedClock);
            Assert.AreEqual(RpgWorld500xTimePhase.Night, snapshot.Phase);
        }

        [Test]
        public void ClockAdvanceHandlesMultiDayStepsAndExtremeElapsedTimeWithoutOverflow()
        {
            var service = new RpgWorld500xWeatherAndTimeOfDayService(
                initialMinutes: 1400f, initialDay: 3, gameMinutesPerRealSecond: 10f);
            var multiDay = service.AdvanceRealSeconds(300f, RpgWorldMapId.Soundwell);
            Assert.AreEqual(6, multiDay.DayCount);
            Assert.AreEqual(80f, multiDay.TimeOfDayMinutes, 0.001f);

            var extreme = service.AdvanceRealSeconds(float.MaxValue, RpgWorldMapId.Soundwell);
            Assert.AreEqual(int.MaxValue, extreme.DayCount);
            Assert.GreaterOrEqual(extreme.TimeOfDayMinutes, 0f);
            Assert.Less(extreme.TimeOfDayMinutes, 1440f);
            Assert.IsTrue(Enum.IsDefined(typeof(RpgWorld500xWeatherKind), extreme.Weather));
            Assert.AreEqual(int.MaxValue, service.RestAtCampfire(RpgWorldMapId.Soundwell).DayCount);
            Assert.AreEqual(int.MaxValue, service.AdvanceRealSeconds(1440f, RpgWorldMapId.Soundwell).DayCount);

            var invalidRate = new RpgWorld500xWeatherAndTimeOfDayService(
                initialMinutes: 480f, gameMinutesPerRealSecond: float.PositiveInfinity);
            Assert.AreEqual(482f, invalidRate.AdvanceRealSeconds(1f, RpgWorldMapId.Soundwell).TimeOfDayMinutes);
        }

        [Test]
        public void RemoteWeatherAdvancesWithGlobalClockAndCampfireRest()
        {
            var service = new RpgWorld500xWeatherAndTimeOfDayService(
                initialMinutes: 600f, initialDay: 1, gameMinutesPerRealSecond: 1f);
            service.SetWeather(RpgWorldMapId.RedSaltHarbor, RpgWorld500xWeatherKind.AshHaze);
            service.AdvanceRealSeconds(1f, RpgWorldMapId.Soundwell);
            Assert.AreEqual(RpgWorld500xWeatherKind.AshHaze, service.GetWeather(RpgWorldMapId.RedSaltHarbor));

            service.AdvanceRealSeconds(180f, RpgWorldMapId.Soundwell);
            var expected = new RpgWorld500xWeatherAndTimeOfDayService(initialMinutes: 781f, initialDay: 1);
            Assert.AreEqual(expected.GetWeather(RpgWorldMapId.RedSaltHarbor),
                service.GetWeather(RpgWorldMapId.RedSaltHarbor));

            service.SetWeather(RpgWorldMapId.RedSaltHarbor, RpgWorld500xWeatherKind.AshHaze);
            service.SetWeather(RpgWorldMapId.Soundwell, RpgWorld500xWeatherKind.GaleStorm);
            service.RestAtCampfire(RpgWorldMapId.Soundwell);
            expected = new RpgWorld500xWeatherAndTimeOfDayService(
                initialMinutes: RpgWorld500xWeatherAndTimeOfDayService.DefaultDawnMinutes, initialDay: 2);
            Assert.AreEqual(expected.GetWeather(RpgWorldMapId.RedSaltHarbor),
                service.GetWeather(RpgWorldMapId.RedSaltHarbor));
            Assert.AreEqual(RpgWorld500xWeatherKind.MistFog, service.GetWeather(RpgWorldMapId.Soundwell));
        }

        [Test]
        public void AllFourMaps_ProduceRegionSpecificWeatherFlavorAndValidModifiers()
        {
            var service = new RpgWorld500xWeatherAndTimeOfDayService(initialMinutes: 600f, initialDay: 1);
            var maps = (RpgWorldMapId[])Enum.GetValues(typeof(RpgWorldMapId));
            var weathers = (RpgWorld500xWeatherKind[])Enum.GetValues(typeof(RpgWorld500xWeatherKind));

            for (int m = 0; m < maps.Length; m++)
            {
                for (int w = 0; w < weathers.Length; w++)
                {
                    service.SetWeather(maps[m], weathers[w]);
                    RpgWorld500xAtmosphereSnapshot snap = service.GetSnapshot(maps[m]);
                    Assert.IsFalse(string.IsNullOrEmpty(snap.PhaseDisplayName));
                    Assert.IsFalse(string.IsNullOrEmpty(snap.WeatherDisplayName));
                    Assert.AreEqual(255, snap.AmbientTint.a);
                    Assert.GreaterOrEqual(snap.PatrolDetectionMultiplier, 0.65f);
                    Assert.LessOrEqual(snap.PatrolDetectionMultiplier, 1.20f);
                    Assert.GreaterOrEqual(snap.MovementSpeedMultiplier, 0.88f);
                    Assert.LessOrEqual(snap.MovementSpeedMultiplier, 1.05f);
                    Assert.GreaterOrEqual(snap.EncounterTensionMultiplier, 0.90f);
                    Assert.LessOrEqual(snap.EncounterTensionMultiplier, 1.35f);
                    Assert.GreaterOrEqual(snap.LanternHaloScale, 0.60f);
                    Assert.LessOrEqual(snap.LanternHaloScale, 1.60f);
                }
            }
        }

        [Test]
        public void SetWeather_RejectsUndefinedOverridesWithoutChangingLiveOrSavedForecast()
        {
            var service = new RpgWorld500xWeatherAndTimeOfDayService(
                initialMinutes: 600f, initialDay: 2, gameMinutesPerRealSecond: 1f);
            var map = RpgWorldMapId.RedSaltHarbor;
            service.SetWeather(map, RpgWorld500xWeatherKind.GaleStorm);
            var before = service.GetSnapshot(map);
            var saved = service.ExportPersistenceState();

            service.SetWeather(map, (RpgWorld500xWeatherKind)999);
            service.SetWeather((RpgWorldMapId)255, RpgWorld500xWeatherKind.AshHaze);

            var after = service.GetSnapshot(map);
            Assert.AreEqual(before.Weather, after.Weather);
            Assert.AreEqual(before.WeatherDisplayName, after.WeatherDisplayName);
            Assert.AreEqual(before.MovementSpeedMultiplier, after.MovementSpeedMultiplier);
            Assert.AreEqual(before.EncounterTensionMultiplier, after.EncounterTensionMultiplier);
            Assert.AreEqual(RpgWorld500xWeatherKind.Clear, service.GetWeather((RpgWorldMapId)255));
            CollectionAssert.AreEquivalent(saved, service.ExportPersistenceState());

            var restored = new RpgWorld500xWeatherAndTimeOfDayService(gameMinutesPerRealSecond: 1f);
            restored.ImportPersistenceState(saved);
            Assert.AreEqual(before.Weather, restored.GetSnapshot(map).Weather);
            Assert.AreEqual(restored.AdvanceRealSeconds(180f, map).Weather,
                service.AdvanceRealSeconds(180f, map).Weather);
            service.SetWeather(map, RpgWorld500xWeatherKind.Clear);
            Assert.AreEqual(RpgWorld500xWeatherKind.Clear, service.GetWeather(map));
        }

        [Test]
        public void RestAtCampfire_AdvancesToDawnAndCalmsSevereStorms()
        {
            var service = new RpgWorld500xWeatherAndTimeOfDayService(initialMinutes: 1300f, initialDay: 3);
            service.SetWeather(RpgWorldMapId.RedSaltHarbor, RpgWorld500xWeatherKind.GaleStorm);

            RpgWorld500xAtmosphereSnapshot morning = service.RestAtCampfire(RpgWorldMapId.RedSaltHarbor);
            Assert.AreEqual(4, morning.DayCount);
            Assert.AreEqual(RpgWorld500xWeatherAndTimeOfDayService.DefaultDawnMinutes, morning.TimeOfDayMinutes);
            Assert.AreEqual(RpgWorld500xTimePhase.Dawn, morning.Phase);
            Assert.AreEqual("06:00", morning.FormattedClock);
            Assert.AreEqual(RpgWorld500xWeatherKind.Clear, morning.Weather);
        }

        [Test]
        public void Modifiers_FogAndNightReducePatrolDetectionAndExpandLanternHalo()
        {
            var dayService = new RpgWorld500xWeatherAndTimeOfDayService(initialMinutes: 720f, initialDay: 1);
            dayService.SetWeather(RpgWorldMapId.Soundwell, RpgWorld500xWeatherKind.Clear);
            RpgWorld500xAtmosphereSnapshot clearDay = dayService.GetSnapshot(RpgWorldMapId.Soundwell);

            var nightService = new RpgWorld500xWeatherAndTimeOfDayService(initialMinutes: 1320f, initialDay: 1);
            nightService.SetWeather(RpgWorldMapId.Soundwell, RpgWorld500xWeatherKind.MistFog);
            RpgWorld500xAtmosphereSnapshot foggyNight = nightService.GetSnapshot(RpgWorldMapId.Soundwell);

            Assert.Less(foggyNight.PatrolDetectionMultiplier, clearDay.PatrolDetectionMultiplier);
            Assert.Greater(foggyNight.LanternHaloScale, clearDay.LanternHaloScale);
            Assert.Greater(foggyNight.EncounterTensionMultiplier, clearDay.EncounterTensionMultiplier);
        }

        [Test]
        public void PersistenceRoundTrip_PreservesClockImmediatelyBeforeMidnight()
        {
            AssertClockRoundTripBeforeBoundary(1439.999f);
        }

        [Test]
        public void PersistenceRoundTrip_PreservesClockImmediatelyBeforeDay()
        {
            AssertClockRoundTripBeforeBoundary(479.999f);
        }

        [Test]
        public void PersistenceRoundTrip_PreservesClockImmediatelyBeforeDusk()
        {
            AssertClockRoundTripBeforeBoundary(1019.999f);
        }

        [Test]
        public void PersistenceRoundTrip_PreservesClockImmediatelyBeforeNight()
        {
            AssertClockRoundTripBeforeBoundary(1199.999f);
        }

        private static void AssertClockRoundTripBeforeBoundary(float minutes)
        {
            var source = new RpgWorld500xWeatherAndTimeOfDayService(initialMinutes: minutes, initialDay: 5);
            var restored = new RpgWorld500xWeatherAndTimeOfDayService();
            restored.ImportPersistenceState(source.ExportPersistenceState());

            Assert.AreEqual(source.DayCount, restored.DayCount);
            Assert.AreEqual(source.TimeOfDayMinutes, restored.TimeOfDayMinutes);
            Assert.AreEqual(source.CurrentPhase, restored.CurrentPhase);
            Assert.AreEqual(RpgWorld500xWeatherAndTimeOfDayService.FormatClock(minutes),
                restored.GetSnapshot(RpgWorldMapId.Soundwell).FormattedClock);

            // Reloading must not change the next midnight/phase transition.
            var expected = source.AdvanceRealSeconds(0.01f, RpgWorldMapId.Soundwell);
            var actual = restored.AdvanceRealSeconds(0.01f, RpgWorldMapId.Soundwell);
            Assert.AreEqual(expected.DayCount, actual.DayCount);
            Assert.AreEqual(expected.TimeOfDayMinutes, actual.TimeOfDayMinutes);
            Assert.AreEqual(expected.Phase, actual.Phase);
            Assert.AreEqual(expected.Weather, actual.Weather);
        }

        [Test]
        public void PartialPersistence_ReconcilesMissingAndInvalidRegionalWeatherBeforeFirstTick()
        {
            var restored = new RpgWorld500xWeatherAndTimeOfDayService(initialMinutes: 480f, initialDay: 1);
            restored.SetWeather(RpgWorldMapId.Soundwell, RpgWorld500xWeatherKind.AshHaze);
            restored.SetWeather(RpgWorldMapId.MineCamp, RpgWorld500xWeatherKind.RainDrizzle);
            var state = new Dictionary<string, string>
            {
                [RpgWorld500xWeatherAndTimeOfDayService.DayCountKey] = "5",
                [RpgWorld500xWeatherAndTimeOfDayService.TimeMinutesKey] = "1125",
                [RpgWorld500xWeatherAndTimeOfDayService.GetWeatherKey(RpgWorldMapId.RedSaltHarbor)] = "999",
                [RpgWorld500xWeatherAndTimeOfDayService.GetWeatherKey(RpgWorldMapId.WhitewaterCrossing)] = "not-weather",
                [RpgWorld500xWeatherAndTimeOfDayService.GetWeatherKey(RpgWorldMapId.MineCamp)] = "RainDrizzle"
            };

            restored.ImportPersistenceState(state);
            var expected = new RpgWorld500xWeatherAndTimeOfDayService(initialMinutes: 1125f, initialDay: 5);
            foreach (var map in new[] { RpgWorldMapId.Soundwell, RpgWorldMapId.RedSaltHarbor, RpgWorldMapId.WhitewaterCrossing })
            {
                var snapshot = restored.GetSnapshot(map);
                Assert.AreEqual(5, snapshot.DayCount);
                Assert.AreEqual("18:45", snapshot.FormattedClock);
                Assert.AreEqual(expected.GetWeather(map), snapshot.Weather, map.ToString());
            }

            // An explicitly saved, valid weather value is authoritative, even
            // when it is not part of the region's automatic weather cycle.
            Assert.AreEqual(RpgWorld500xWeatherKind.RainDrizzle, restored.GetWeather(RpgWorldMapId.MineCamp));
            restored.AdvanceRealSeconds(1f, RpgWorldMapId.Soundwell);
            Assert.AreEqual(RpgWorld500xWeatherKind.RainDrizzle, restored.GetWeather(RpgWorldMapId.MineCamp));
        }

        [Test]
        public void PartialPersistence_SameSlotRetainsUnspecifiedWeatherOverride()
        {
            var restored = new RpgWorld500xWeatherAndTimeOfDayService(initialMinutes: 600f, initialDay: 2);
            restored.SetWeather(RpgWorldMapId.Soundwell, RpgWorld500xWeatherKind.GaleStorm);
            restored.ImportPersistenceState(new Dictionary<string, string>
            {
                [RpgWorld500xWeatherAndTimeOfDayService.TimeMinutesKey] = "610"
            });

            Assert.AreEqual(RpgWorld500xWeatherKind.GaleStorm, restored.GetSnapshot(RpgWorldMapId.Soundwell).Weather);
        }

        [Test]
        public void PersistenceRoundTrip_UsesWorldNamespaceKeysAndRestoresExactState()
        {
            var source = new RpgWorld500xWeatherAndTimeOfDayService(initialMinutes: 1125f, initialDay: 5);
            source.SetWeather(RpgWorldMapId.MineCamp, RpgWorld500xWeatherKind.AshHaze);
            source.SetWeather(RpgWorldMapId.WhitewaterCrossing, RpgWorld500xWeatherKind.RainDrizzle);

            Dictionary<string, string> exported = source.ExportPersistenceState();
            foreach (string key in exported.Keys)
            {
                Assert.IsTrue(key.StartsWith("rpg.return.world.atmosphere.", StringComparison.Ordinal),
                    $"Unexpected persistence key namespace: {key}");
            }

            var restored = new RpgWorld500xWeatherAndTimeOfDayService(initialMinutes: 0f, initialDay: 1);
            restored.ImportPersistenceState(exported);

            Assert.AreEqual(5, restored.DayCount);
            Assert.AreEqual(1125f, restored.TimeOfDayMinutes, 0.05f);
            Assert.AreEqual(RpgWorld500xWeatherKind.AshHaze, restored.GetWeather(RpgWorldMapId.MineCamp));
            Assert.AreEqual(RpgWorld500xWeatherKind.RainDrizzle, restored.GetWeather(RpgWorldMapId.WhitewaterCrossing));
        }
    }
}
