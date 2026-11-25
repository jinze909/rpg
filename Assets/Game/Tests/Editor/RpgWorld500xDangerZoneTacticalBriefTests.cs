using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xDangerZoneTacticalBriefTests
    {
        [Test]
        public void BuildRegionBrief_RanksNearestUnclearedScalesWithWeatherAndTracksFirstClears()
        {
            using (var boot = new RpgWorld500xSceneBootstrapper(
                initialMapId: RpgWorldMapId.Soundwell,
                overrideTileSlices: new Sprite[] { null }))
            {
                var clearFlags = new Dictionary<string, int>(StringComparer.Ordinal);
                Func<string, int> read = k => clearFlags.TryGetValue(k, out int v) ? v : 0;

                boot.Driver.Facade.Atmosphere.SetWeather(RpgWorldMapId.Soundwell, RpgWorld500xWeatherKind.Clear);
                RpgWorld500xRegionThreatBrief clearBrief =
                    RpgWorld500xDangerZoneTacticalBriefService.BuildRegionBrief(boot, elapsedSeconds: 5f, overrideGetInt: read);

                Assert.AreEqual(RpgWorldMapId.Soundwell, clearBrief.MapId);
                Assert.Greater(clearBrief.TotalDangerZones, 0);
                Assert.AreEqual(0, clearBrief.ClearedDangerZones);
                Assert.AreEqual(clearBrief.TotalDangerZones, clearBrief.RemainingDangerZones);
                Assert.IsTrue(clearBrief.HasNearestUncleared);
                Assert.AreEqual(clearBrief.Entries[0].MarkerId, clearBrief.NearestUnclearedZone.MarkerId);
                Assert.Greater(clearBrief.NearestUnclearedZone.EstimatedLockBreakSilver,
                    clearBrief.NearestUnclearedZone.EstimatedStandardSilver);
                Assert.IsTrue(clearBrief.FormattedBriefZh.Contains("危险区战备简报"));

                int clearLevel = clearBrief.NearestUnclearedZone.EffectiveThreatLevel;

                // GaleStorm elevates effective threat level; MistFog lowers it for stealth advantage.
                boot.Driver.Facade.Atmosphere.SetWeather(RpgWorldMapId.Soundwell, RpgWorld500xWeatherKind.GaleStorm);
                RpgWorld500xRegionThreatBrief stormBrief =
                    RpgWorld500xDangerZoneTacticalBriefService.BuildRegionBrief(boot, elapsedSeconds: 5f, overrideGetInt: read);
                Assert.GreaterOrEqual(stormBrief.NearestUnclearedZone.EffectiveThreatLevel, clearLevel);

                boot.Driver.Facade.Atmosphere.SetWeather(RpgWorldMapId.Soundwell, RpgWorld500xWeatherKind.MistFog);
                RpgWorld500xRegionThreatBrief fogBrief =
                    RpgWorld500xDangerZoneTacticalBriefService.BuildRegionBrief(boot, elapsedSeconds: 5f, overrideGetInt: read);
                Assert.LessOrEqual(fogBrief.NearestUnclearedZone.EffectiveThreatLevel, clearLevel);

                // Marking the nearest zone cleared moves it after remaining uncleared targets.
                string firstId = clearBrief.NearestUnclearedZone.MarkerId;
                clearFlags[RpgWorld500xDangerVictoryRewardService.FirstClearKey(firstId)] = 1;

                RpgWorld500xRegionThreatBrief afterClear =
                    RpgWorld500xDangerZoneTacticalBriefService.BuildRegionBrief(boot, elapsedSeconds: 5f, overrideGetInt: read);
                Assert.AreEqual(1, afterClear.ClearedDangerZones);
                Assert.AreEqual(clearBrief.TotalDangerZones - 1, afterClear.RemainingDangerZones);
                if (afterClear.RemainingDangerZones > 0)
                {
                    Assert.IsTrue(afterClear.HasNearestUncleared);
                    Assert.AreNotEqual(firstId, afterClear.NearestUnclearedZone.MarkerId);
                }
            }
        }
    }
}
