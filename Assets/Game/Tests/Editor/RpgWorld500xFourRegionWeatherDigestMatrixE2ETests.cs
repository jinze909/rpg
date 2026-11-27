using System;
using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionWeatherDigestMatrixE2ETests
    {
        [Test]
        public void BuildWorldWeatherDigest_RotatesSafestAndStealthRegionsAcrossAllFourMaps()
        {
            var specs = RpgExpandedWorldScale.AllSpecs;
            Assert.That(specs.Count, Is.EqualTo(4));

            for (int i = 0; i < specs.Count; i++)
            {
                var atmosphere = new RpgWorld500xWeatherAndTimeOfDayService(initialMinutes: 600f, initialDay: 1);
                var safestMap = specs[i].MapId;
                var stealthMap = specs[(i + 1) % specs.Count].MapId;

                for (int j = 0; j < specs.Count; j++)
                {
                    var mapId = specs[j].MapId;
                    if (mapId == safestMap)
                    {
                        atmosphere.SetWeather(mapId, RpgWorld500xWeatherKind.Clear);
                    }
                    else if (mapId == stealthMap)
                    {
                        atmosphere.SetWeather(mapId, RpgWorld500xWeatherKind.MistFog);
                    }
                    else
                    {
                        atmosphere.SetWeather(mapId, RpgWorld500xWeatherKind.GaleStorm);
                    }
                }

                var digest = RpgWorld500xWeatherTravelAdvisoryService.BuildWorldWeatherDigest(atmosphere, elapsedSeconds: 5f);
                Assert.That(digest.RegionReports.Count, Is.EqualTo(4));
                Assert.That(digest.SafestMapId, Is.EqualTo(safestMap));
                Assert.That(digest.HazardousRegionCount, Is.EqualTo(2));
                Assert.That(digest.StealthWindowRegionCount, Is.EqualTo(1));
                Assert.That(digest.WorldBulletinZh, Does.Contain("【四境气象速报】"));
                Assert.That(digest.WorldBulletinZh, Does.Contain("高危预警"));
                Assert.That(digest.WorldBulletinZh, Does.Contain("潜行良机"));
                Assert.That(digest.WorldBulletinZh, Does.Contain("行路平稳"));

                for (int j = 0; j < specs.Count; j++)
                {
                    Assert.That(digest.WorldBulletinZh, Does.Contain(specs[j].DisplayNameZh));
                }
            }
        }

        [Test]
        public void BuildWorldWeatherDigest_HandlesAllClearAllStormAndNullAtmosphereGuard()
        {
            Assert.Throws<ArgumentNullException>(() =>
                RpgWorld500xWeatherTravelAdvisoryService.BuildWorldWeatherDigest(null));

            var specs = RpgExpandedWorldScale.AllSpecs;
            var atmosphere = new RpgWorld500xWeatherAndTimeOfDayService(initialMinutes: 600f, initialDay: 1);

            for (int i = 0; i < specs.Count; i++)
            {
                atmosphere.SetWeather(specs[i].MapId, RpgWorld500xWeatherKind.Clear);
            }

            var clearDigest = RpgWorld500xWeatherTravelAdvisoryService.BuildWorldWeatherDigest(atmosphere, elapsedSeconds: 5f);
            Assert.That(clearDigest.RegionReports.Count, Is.EqualTo(4));
            Assert.That(clearDigest.HazardousRegionCount, Is.EqualTo(0));
            Assert.That(clearDigest.StealthWindowRegionCount, Is.EqualTo(0));
            Assert.That(clearDigest.WorldBulletinZh, Does.Contain("【四境气象速报】"));
            Assert.That(clearDigest.WorldBulletinZh, Does.Contain("行路平稳"));

            for (int i = 0; i < specs.Count; i++)
            {
                atmosphere.SetWeather(specs[i].MapId, RpgWorld500xWeatherKind.GaleStorm);
            }

            var stormDigest = RpgWorld500xWeatherTravelAdvisoryService.BuildWorldWeatherDigest(atmosphere, elapsedSeconds: 5f);
            Assert.That(stormDigest.RegionReports.Count, Is.EqualTo(4));
            Assert.That(stormDigest.HazardousRegionCount, Is.EqualTo(4));
            Assert.That(stormDigest.StealthWindowRegionCount, Is.EqualTo(0));
            Assert.That(stormDigest.WorldBulletinZh, Does.Contain("【四境气象速报】"));
            Assert.That(stormDigest.WorldBulletinZh, Does.Contain("高危预警"));
        }
    }
}
