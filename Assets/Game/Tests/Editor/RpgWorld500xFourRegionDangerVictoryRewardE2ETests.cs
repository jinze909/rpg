using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionDangerVictoryRewardE2ETests
    {
        [Test]
        public void DangerVictoryRewardService_AcrossAllFourRegions_ScalesLockBreakTracksIncrementalClearsAndZeroesOnCompletion()
        {
            var specs = RpgExpandedWorldScale.AllSpecs;
            Assert.That(specs.Count, Is.EqualTo(4));

            for (int i = 0; i < specs.Count; i++)
            {
                var mapId = specs[i].MapId;
                var layout = RpgWorld500xMapLayout.Build(mapId);
                var store = new Dictionary<string, int>(StringComparer.Ordinal);
                Func<string, int> read = k => store.TryGetValue(k, out int v) ? v : 0;

                var dangerIds = new List<string>();
                for (int m = 0; m < layout.Markers.Count; m++)
                {
                    if (layout.Markers[m].Category == RpgMapMarkerCategory.DangerZone)
                    {
                        dangerIds.Add(layout.Markers[m].Id);
                    }
                }

                Assert.That(dangerIds.Count, Is.GreaterThan(0));

                var initial = RpgWorld500xDangerVictoryRewardService.SummarizeRemainingFirstClearRewards(layout, read);
                Assert.That(initial.MapId, Is.EqualTo(mapId));
                Assert.That(initial.TotalDangerZones, Is.EqualTo(dangerIds.Count));
                Assert.That(initial.ClearedDangerZones, Is.EqualTo(0));
                Assert.That(initial.RemainingDangerZones, Is.EqualTo(dangerIds.Count));
                Assert.That(initial.RemainingLockBreakSaltSilver, Is.GreaterThan(initial.RemainingStandardSaltSilver));
                Assert.That(initial.RemainingLockBreakReputation, Is.GreaterThan(initial.RemainingStandardReputation));
                StringAssert.Contains("【首破悬赏余量】", initial.SummaryZh);
                StringAssert.Contains(specs[i].DisplayNameZh, initial.SummaryZh);

                string firstId = dangerIds[0];
                var firstStd = RpgWorld500xDangerVictoryRewardService.PreviewVictoryReward(mapId, firstId, false, read);
                var firstLb = RpgWorld500xDangerVictoryRewardService.PreviewVictoryReward(mapId, firstId, true, read);
                Assert.IsTrue(firstStd.IsFirstClear);
                Assert.IsTrue(firstLb.IsFirstClear);
                Assert.That(firstLb.SaltSilverReward, Is.GreaterThan(firstStd.SaltSilverReward));
                Assert.That(firstLb.ReputationGain, Is.GreaterThan(firstStd.ReputationGain));
                StringAssert.Contains("破锁精通", firstLb.SummaryBannerZh);
                Assert.That(store.Count, Is.EqualTo(0));

                store[RpgWorld500xDangerVictoryRewardService.FirstClearKey(firstId)] = 1;
                Assert.IsTrue(RpgWorld500xDangerVictoryRewardService.IsFirstClearCompleted(firstId, read));

                var repeatStd = RpgWorld500xDangerVictoryRewardService.PreviewVictoryReward(mapId, firstId, false, read);
                Assert.IsFalse(repeatStd.IsFirstClear);
                Assert.That(repeatStd.SaltSilverReward, Is.LessThan(firstStd.SaltSilverReward));

                var afterOne = RpgWorld500xDangerVictoryRewardService.SummarizeRemainingFirstClearRewards(layout, read);
                Assert.That(afterOne.ClearedDangerZones, Is.EqualTo(1));
                Assert.That(afterOne.RemainingDangerZones, Is.EqualTo(dangerIds.Count - 1));
                Assert.That(afterOne.RemainingStandardSaltSilver, Is.EqualTo(initial.RemainingStandardSaltSilver - firstStd.SaltSilverReward));

                for (int d = 0; d < dangerIds.Count; d++)
                {
                    store[RpgWorld500xDangerVictoryRewardService.FirstClearKey(dangerIds[d])] = 1;
                }

                var allCleared = RpgWorld500xDangerVictoryRewardService.SummarizeRemainingFirstClearRewards(layout, read);
                Assert.That(allCleared.RemainingDangerZones, Is.EqualTo(0));
                Assert.That(allCleared.RemainingStandardSaltSilver, Is.EqualTo(0));
                Assert.That(allCleared.RemainingLockBreakSaltSilver, Is.EqualTo(0));
                Assert.That(allCleared.RemainingStandardReputation, Is.EqualTo(0));
                Assert.That(allCleared.RemainingLockBreakReputation, Is.EqualTo(0));
                StringAssert.Contains("已全部完成首破", allCleared.SummaryZh);
            }
        }

        [Test]
        public void DangerVictoryRewardService_AcrossAllFourRegions_NullLayoutThrowsAndKeysRemainSafeAndIsolated()
        {
            Assert.Throws<ArgumentNullException>(() =>
                RpgWorld500xDangerVictoryRewardService.SummarizeRemainingFirstClearRewards(null, null));

            var specs = RpgExpandedWorldScale.AllSpecs;
            var seenKeys = new HashSet<string>(StringComparer.Ordinal);
            var sharedStore = new Dictionary<string, int>(StringComparer.Ordinal);
            Func<string, int> read = k => sharedStore.TryGetValue(k, out int v) ? v : 0;

            for (int i = 0; i < specs.Count; i++)
            {
                var layout = RpgWorld500xMapLayout.Build(specs[i].MapId);
                for (int m = 0; m < layout.Markers.Count; m++)
                {
                    if (layout.Markers[m].Category != RpgMapMarkerCategory.DangerZone)
                    {
                        continue;
                    }

                    string key = RpgWorld500xDangerVictoryRewardService.FirstClearKey(layout.Markers[m].Id);
                    Assert.IsTrue(RpgWorld500xEnrichedContentAuditor.IsSafeWorldKey(key), key);
                    Assert.IsTrue(seenKeys.Add(key), "Duplicate first-clear key: " + key);
                    if (i == 0)
                    {
                        sharedStore[key] = 1;
                    }
                }
            }

            for (int i = 1; i < specs.Count; i++)
            {
                var otherLayout = RpgWorld500xMapLayout.Build(specs[i].MapId);
                var otherSummary = RpgWorld500xDangerVictoryRewardService.SummarizeRemainingFirstClearRewards(otherLayout, read);
                Assert.That(otherSummary.ClearedDangerZones, Is.EqualTo(0));
                Assert.That(otherSummary.RemainingDangerZones, Is.EqualTo(otherSummary.TotalDangerZones));
            }
        }
    }
}
