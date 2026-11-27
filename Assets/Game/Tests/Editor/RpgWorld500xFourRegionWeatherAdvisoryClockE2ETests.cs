using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionWeatherAdvisoryClockE2ETests
    {
        [Test]
        public void AllFourRegions_WeatherAdvisoryPhaseCountdownsMatchLiveClockAcrossAllBoundaries()
        {
            float[] before = { 299.99997f, 479.99997f, 1019.99994f, 1199.9999f };
            float[] at = { 300f, 480f, 1020f, 1200f };
            RpgWorld500xTimePhase[] next =
            {
                RpgWorld500xTimePhase.Dawn,
                RpgWorld500xTimePhase.Day,
                RpgWorld500xTimePhase.Dusk,
                RpgWorld500xTimePhase.Night
            };

            int verifiedRegions = 0;
            foreach (RpgExpandedMapSpec spec in RpgExpandedWorldScale.AllSpecs)
            {
                for (int i = 0; i < before.Length; i++)
                {
                    var clock = new RpgWorld500xWeatherAndTimeOfDayService(initialMinutes: before[i]);
                    var report = RpgWorld500xWeatherTravelAdvisoryService.BuildAdvisory(
                        clock.GetSnapshot(spec.MapId));

                    Assert.AreNotEqual(next[i], clock.CurrentPhase,
                        "Expected phase before boundary in " + spec.MapId);
                    Assert.AreEqual(next[i], report.NextPhase,
                        "Expected next phase countdown in " + spec.MapId);
                    Assert.AreEqual(1, report.MinutesUntilNextPhase,
                        "Expected 1 minute until boundary in " + spec.MapId);
                    Assert.IsFalse(string.IsNullOrEmpty(report.TacticalAdvisoryZh));

                    RpgWorld500xWeatherTravelAdvisoryService.ResolveNextPhase(
                        at[i], out var following, out int minutes);
                    Assert.AreEqual(next[(i + 1) % next.Length], following);
                    Assert.Greater(minutes, 1);
                }

                verifiedRegions++;
            }

            Assert.AreEqual(4, verifiedRegions);

            // Negative wraparound and non-finite inputs normalize deterministically.
            RpgWorld500xWeatherTravelAdvisoryService.ResolveNextPhase(-60f, out var wrapped, out int wrappedMinutes);
            Assert.AreEqual(RpgWorld500xTimePhase.Dawn, wrapped);
            Assert.AreEqual(360, wrappedMinutes);

            RpgWorld500xWeatherTravelAdvisoryService.ResolveNextPhase(float.NaN, out var nanPhase, out int nanMinutes);
            Assert.AreEqual(RpgWorld500xTimePhase.Dawn, nanPhase);
            Assert.AreEqual(300, nanMinutes);

            RpgWorld500xWeatherTravelAdvisoryService.ResolveNextPhase(
                float.PositiveInfinity, out var infPhase, out int infMinutes);
            Assert.AreEqual(RpgWorld500xTimePhase.Dawn, infPhase);
            Assert.AreEqual(300, infMinutes);
        }
    }
}
