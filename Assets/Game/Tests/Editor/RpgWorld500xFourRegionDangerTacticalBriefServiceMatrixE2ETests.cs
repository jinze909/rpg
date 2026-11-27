using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionDangerTacticalBriefServiceMatrixE2ETests
    {
        [Test]
        public void DangerBriefAndFirstClearRewardServices_AcrossAllFourRegions_StaySynchronizedAcrossPersistedFlagValues()
        {
            var specs = RpgExpandedWorldScale.AllSpecs;
            Assert.That(specs.Count, Is.EqualTo(4));

            for (int i = 0; i < specs.Count; i++)
            {
                var mapId = specs[i].MapId;
                using (var boot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: mapId,
                    overrideTileSlices: new Sprite[] { null }))
                {
                    var flags = new Dictionary<string, int>(StringComparer.Ordinal);
                    Func<string, int> read = k => flags.TryGetValue(k, out int v) ? v : 0;

                    var baseline = RpgWorld500xDangerZoneTacticalBriefService.BuildRegionBrief(
                        boot, elapsedSeconds: 5f, overrideGetInt: read);
                    Assert.That(baseline.TotalDangerZones, Is.GreaterThan(0));
                    string markerId = baseline.NearestUnclearedZone.MarkerId;
                    string key = RpgWorld500xDangerVictoryRewardService.FirstClearKey(markerId);

                    foreach (int persistedValue in new[] { 0, 1, 2, -1 })
                    {
                        flags[key] = persistedValue;
                        var brief = RpgWorld500xDangerZoneTacticalBriefService.BuildRegionBrief(
                            boot, elapsedSeconds: 5f, overrideGetInt: read);
                        var preview = RpgWorld500xDangerVictoryRewardService.PreviewVictoryReward(
                            mapId, markerId, false, read);
                        var remaining = RpgWorld500xDangerVictoryRewardService.SummarizeRemainingFirstClearRewards(
                            boot.Driver.Facade.Session.Host.Layout, read);
                        var payouts = RpgWorld500xDangerZoneTacticalBriefService.SummarizeRemainingPayouts(brief);

                        Assert.That(!preview.IsFirstClear, Is.EqualTo(persistedValue != 0));
                        Assert.That(brief.ClearedDangerZones, Is.EqualTo(remaining.ClearedDangerZones));
                        Assert.That(payouts.RemainingZones, Is.EqualTo(remaining.RemainingDangerZones));
                        Assert.That(payouts.RemainingStandardSilver, Is.EqualTo(remaining.RemainingStandardSaltSilver));
                        Assert.That(payouts.RemainingLockBreakSilver, Is.EqualTo(remaining.RemainingLockBreakSaltSilver));
                        Assert.That(flags[key], Is.EqualTo(persistedValue));
                    }
                }
            }
        }

        [Test]
        public void DangerZoneTacticalBriefService_AcrossAllFourRegions_ScalesWithStormFiltersEntriesAndZeroesWhenCleared()
        {
            var specs = RpgExpandedWorldScale.AllSpecs;
            Assert.That(specs.Count, Is.EqualTo(4));

            for (int i = 0; i < specs.Count; i++)
            {
                var mapId = specs[i].MapId;
                using (var boot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: mapId,
                    overrideTileSlices: new Sprite[] { null }))
                {
                    var flags = new Dictionary<string, int>(StringComparer.Ordinal);
                    Func<string, int> read = k => flags.TryGetValue(k, out int v) ? v : 0;

                    boot.Driver.Facade.Atmosphere.SetWeather(mapId, RpgWorld500xWeatherKind.Clear);
                    var clearBrief = RpgWorld500xDangerZoneTacticalBriefService.BuildRegionBrief(
                        boot, elapsedSeconds: 5f, overrideGetInt: read);
                    var clearPayout = RpgWorld500xDangerZoneTacticalBriefService.SummarizeRemainingPayouts(clearBrief);
                    Assert.That(clearPayout.MapId, Is.EqualTo(mapId));
                    Assert.That(clearPayout.RemainingZones, Is.EqualTo(clearBrief.TotalDangerZones));
                    Assert.That(clearPayout.RemainingStandardSilver, Is.GreaterThan(0));
                    Assert.That(clearPayout.RemainingLockBreakSilver, Is.GreaterThan(clearPayout.RemainingStandardSilver));
                    Assert.That(clearPayout.LockBreakBonusSilver, Is.EqualTo(clearPayout.RemainingLockBreakSilver - clearPayout.RemainingStandardSilver));
                    Assert.IsFalse(string.IsNullOrEmpty(clearPayout.TopRecommendedSupplyId));
                    StringAssert.Contains("清剿收益预估", clearPayout.SummaryZh);

                    boot.Driver.Facade.Atmosphere.SetWeather(mapId, RpgWorld500xWeatherKind.GaleStorm);
                    var stormBrief = RpgWorld500xDangerZoneTacticalBriefService.BuildRegionBrief(
                        boot, elapsedSeconds: 5f, overrideGetInt: read);
                    var stormPayout = RpgWorld500xDangerZoneTacticalBriefService.SummarizeRemainingPayouts(stormBrief);
                    Assert.That(stormPayout.PeakEffectiveThreatLevel, Is.GreaterThanOrEqualTo(clearPayout.PeakEffectiveThreatLevel));

                    var sampleTier = stormBrief.Entries[0].Tier;
                    int sampleLevel = stormBrief.Entries[0].EffectiveThreatLevel;
                    var filtered = RpgWorld500xDangerZoneTacticalBriefService.FilterEntries(
                        stormBrief,
                        unclearedOnly: true,
                        minEffectiveThreatLevel: sampleLevel,
                        tierFilter: sampleTier);
                    Assert.That(filtered.Count, Is.GreaterThan(0));
                    for (int j = 0; j < filtered.Count; j++)
                    {
                        Assert.IsFalse(filtered[j].IsCleared);
                        Assert.That(filtered[j].Tier, Is.EqualTo(sampleTier));
                        Assert.That(filtered[j].EffectiveThreatLevel, Is.GreaterThanOrEqualTo(sampleLevel));
                    }

                    for (int j = 0; j < stormBrief.Entries.Count; j++)
                    {
                        flags[RpgWorld500xDangerVictoryRewardService.FirstClearKey(stormBrief.Entries[j].MarkerId)] = 1;
                    }

                    var clearedBrief = RpgWorld500xDangerZoneTacticalBriefService.BuildRegionBrief(
                        boot, elapsedSeconds: 5f, overrideGetInt: read);
                    var clearedPayout = RpgWorld500xDangerZoneTacticalBriefService.SummarizeRemainingPayouts(clearedBrief);
                    Assert.That(clearedPayout.RemainingZones, Is.EqualTo(0));
                    Assert.That(clearedPayout.RemainingStandardSilver, Is.EqualTo(0));
                    Assert.That(clearedPayout.RemainingLockBreakSilver, Is.EqualTo(0));
                    Assert.That(clearedPayout.PeakEffectiveThreatLevel, Is.EqualTo(0));
                    Assert.That(clearedPayout.TopRecommendedSupplyId, Is.EqualTo(string.Empty));
                    StringAssert.Contains("已全部首破", clearedPayout.SummaryZh);
                    Assert.IsEmpty(RpgWorld500xDangerZoneTacticalBriefService.FilterEntries(clearedBrief, unclearedOnly: true));
                }
            }
        }
    }
}
