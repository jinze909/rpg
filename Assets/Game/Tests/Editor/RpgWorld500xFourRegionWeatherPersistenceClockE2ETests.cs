using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionWeatherPersistenceClockE2ETests
    {
        [Test]
        public void WeatherAndTimeOfDayService_AcrossAllFourRegions_RoundTripsPerRegionWeatherAndDayClockWithSafeKeys()
        {
            var specs = RpgExpandedWorldScale.AllSpecs;
            Assert.That(specs.Count, Is.EqualTo(4));

            var kinds = new[]
            {
                RpgWorld500xWeatherKind.Clear,
                RpgWorld500xWeatherKind.MistFog,
                RpgWorld500xWeatherKind.AshHaze,
                RpgWorld500xWeatherKind.GaleStorm
            };

            for (int shift = 0; shift < specs.Count; shift++)
            {
                var source = new RpgWorld500xWeatherAndTimeOfDayService(
                    initialMinutes: 360f + shift * 240f,
                    initialDay: 2 + shift);

                for (int i = 0; i < specs.Count; i++)
                {
                    source.SetWeather(specs[i].MapId, kinds[(i + shift) % kinds.Length]);
                }

                Dictionary<string, string> exported = source.ExportPersistenceState();
                Assert.That(exported.Count, Is.GreaterThan(0));
                foreach (string key in exported.Keys)
                {
                    Assert.IsTrue(RpgWorld500xEnrichedContentAuditor.IsSafeWorldKey(key), key);
                }

                var target = new RpgWorld500xWeatherAndTimeOfDayService(initialMinutes: 0f, initialDay: 1);
                target.ImportPersistenceState(exported);

                Assert.That(target.DayCount, Is.EqualTo(source.DayCount));
                Assert.That(target.TimeOfDayMinutes, Is.EqualTo(source.TimeOfDayMinutes).Within(0.05f));
                Assert.That(target.CurrentPhase, Is.EqualTo(source.CurrentPhase));

                for (int i = 0; i < specs.Count; i++)
                {
                    var mapId = specs[i].MapId;
                    Assert.That(target.GetWeather(mapId), Is.EqualTo(kinds[(i + shift) % kinds.Length]));
                }
            }
        }

        [Test]
        public void WeatherAndTimeOfDayService_ResolvesAllFourTimePhasesAcrossExactClockWindows()
        {
            var cases = new[]
            {
                (minutes: 120f, expected: RpgWorld500xTimePhase.Night),
                (minutes: 360f, expected: RpgWorld500xTimePhase.Dawn),
                (minutes: 720f, expected: RpgWorld500xTimePhase.Day),
                (minutes: 1080f, expected: RpgWorld500xTimePhase.Dusk),
                (minutes: 1320f, expected: RpgWorld500xTimePhase.Night)
            };

            for (int i = 0; i < cases.Length; i++)
            {
                var svc = new RpgWorld500xWeatherAndTimeOfDayService(initialMinutes: cases[i].minutes, initialDay: 1);
                Assert.That(svc.CurrentPhase, Is.EqualTo(cases[i].expected), "minutes=" + cases[i].minutes);
            }
        }
    }
}
