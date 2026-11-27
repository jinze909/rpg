using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    /// <summary>
    /// Four-region E2E regression tests for RpgWorld500xDangerVictoryRewardService verifying
    /// safe globally unique first-clear keys, first-clear/repeat/Lock-Break reward scaling,
    /// read-only victory previews, and progressive remaining-reward summaries across
    /// Soundwell, RedSaltHarbor, WhitewaterCrossing, and MineCamp.
    /// </summary>
    public sealed class RpgWorld500xFourRegionDangerVictoryRewardMatrixE2ETests
    {
        private static readonly RpgWorldMapId[] AllRegions =
        {
            RpgWorldMapId.Soundwell,
            RpgWorldMapId.RedSaltHarbor,
            RpgWorldMapId.WhitewaterCrossing,
            RpgWorldMapId.MineCamp
        };

        [Test]
        public void AllFourRegions_AllDangerZones_FirstClearKeysAreSafeGloballyUniqueAndScaleCorrectly()
        {
            var globalClearKeys = new HashSet<string>(StringComparer.Ordinal);
            int totalDangerZones = 0;

            for (int r = 0; r < AllRegions.Length; r++)
            {
                RpgWorldMapId mapId = AllRegions[r];
                RpgWorld500xMapLayout layout = RpgWorld500xMapLayout.Build(mapId);
                int regionDangerZones = 0;

                for (int i = 0; i < layout.Markers.Count; i++)
                {
                    RpgMapLandmarkMarker marker = layout.Markers[i];
                    if (marker.Category != RpgMapMarkerCategory.DangerZone)
                    {
                        continue;
                    }

                    regionDangerZones++;
                    totalDangerZones++;

                    string clearKey = RpgWorld500xDangerVictoryRewardService.FirstClearKey(marker.Id);
                    Assert.IsTrue(RpgWorld500xEnrichedContentAuditor.IsSafeWorldKey(clearKey),
                        $"Unsafe first-clear key '{clearKey}' on {mapId}.");
                    Assert.IsTrue(globalClearKeys.Add(clearKey),
                        $"Duplicate first-clear key '{clearKey}' across regions.");

                    RpgWorld500xDangerVictoryReward firstStd = RpgWorld500xDangerVictoryRewardService.ComputeReward(
                        mapId, marker.Id, lockBreakAchieved: false, isFirstClear: true);
                    RpgWorld500xDangerVictoryReward firstLb = RpgWorld500xDangerVictoryRewardService.ComputeReward(
                        mapId, marker.Id, lockBreakAchieved: true, isFirstClear: true);
                    RpgWorld500xDangerVictoryReward repeatStd = RpgWorld500xDangerVictoryRewardService.ComputeReward(
                        mapId, marker.Id, lockBreakAchieved: false, isFirstClear: false);
                    RpgWorld500xDangerVictoryReward repeatLb = RpgWorld500xDangerVictoryRewardService.ComputeReward(
                        mapId, marker.Id, lockBreakAchieved: true, isFirstClear: false);

                    Assert.Greater(firstLb.SaltSilverReward, firstStd.SaltSilverReward, $"Lock-Break silver scaling on {mapId}/{marker.Id}.");
                    Assert.Greater(firstStd.SaltSilverReward, repeatStd.SaltSilverReward, $"First-clear silver scaling on {mapId}/{marker.Id}.");
                    Assert.Greater(firstLb.ReputationGain, firstStd.ReputationGain, $"Lock-Break rep scaling on {mapId}/{marker.Id}.");
                    Assert.Greater(firstStd.ReputationGain, repeatStd.ReputationGain, $"First-clear rep scaling on {mapId}/{marker.Id}.");

                    Assert.AreEqual(1, firstStd.BonusSupplyQuantity, $"First-clear should grant bonus supply on {mapId}/{marker.Id}.");
                    Assert.AreEqual(1, repeatLb.BonusSupplyQuantity, $"Repeat Lock-Break should grant bonus supply on {mapId}/{marker.Id}.");
                    Assert.AreEqual(0, repeatStd.BonusSupplyQuantity, $"Repeat standard clear should not grant bonus supply on {mapId}/{marker.Id}.");
                    Assert.IsFalse(string.IsNullOrWhiteSpace(firstStd.BonusSupplyId), $"Missing BonusSupplyId on {mapId}/{marker.Id}.");
                    Assert.IsFalse(string.IsNullOrWhiteSpace(firstStd.BonusSupplyNameZh), $"Missing BonusSupplyNameZh on {mapId}/{marker.Id}.");

                    StringAssert.Contains("首破", firstStd.SummaryBannerZh);
                    StringAssert.Contains("破锁精通", firstLb.SummaryBannerZh);
                    StringAssert.Contains("清剿", repeatStd.SummaryBannerZh);
                    StringAssert.Contains("破锁精通", repeatLb.SummaryBannerZh);
                }

                Assert.Greater(regionDangerZones, 0, $"Region {mapId} should contain DangerZone markers.");
            }

            Assert.AreEqual(totalDangerZones, globalClearKeys.Count);
        }

        [Test]
        public void AllFourRegions_SummarizeRemainingFirstClearRewards_TracksProgressiveClearsToZero()
        {
            for (int r = 0; r < AllRegions.Length; r++)
            {
                RpgWorldMapId mapId = AllRegions[r];
                RpgWorld500xMapLayout layout = RpgWorld500xMapLayout.Build(mapId);
                var store = new Dictionary<string, int>(StringComparer.Ordinal);
                Func<string, int> read = k => store.TryGetValue(k, out int v) ? v : 0;

                var dangerIds = new List<string>();
                for (int i = 0; i < layout.Markers.Count; i++)
                {
                    if (layout.Markers[i].Category == RpgMapMarkerCategory.DangerZone)
                    {
                        dangerIds.Add(layout.Markers[i].Id);
                    }
                }

                Assert.Greater(dangerIds.Count, 0, $"Region {mapId} must have DangerZone markers.");

                RpgWorld500xMapFirstClearSummary current =
                    RpgWorld500xDangerVictoryRewardService.SummarizeRemainingFirstClearRewards(layout, read);
                Assert.AreEqual(mapId, current.MapId);
                Assert.AreEqual(dangerIds.Count, current.TotalDangerZones);
                Assert.AreEqual(0, current.ClearedDangerZones);
                Assert.AreEqual(dangerIds.Count, current.RemainingDangerZones);
                Assert.Greater(current.RemainingLockBreakSaltSilver, current.RemainingStandardSaltSilver);
                Assert.Greater(current.RemainingLockBreakReputation, current.RemainingStandardReputation);
                StringAssert.Contains("【首破悬赏余量】", current.SummaryZh);

                // Preview must stay strictly read-only.
                RpgWorld500xDangerVictoryReward initialPreview =
                    RpgWorld500xDangerVictoryRewardService.PreviewVictoryReward(mapId, dangerIds[0], lockBreakAchieved: true, read);
                Assert.IsTrue(initialPreview.IsFirstClear);
                Assert.AreEqual(0, store.Count, $"PreviewVictoryReward mutated store on {mapId}.");

                for (int i = 0; i < dangerIds.Count; i++)
                {
                    string markerId = dangerIds[i];
                    RpgWorld500xDangerVictoryReward stdBefore =
                        RpgWorld500xDangerVictoryRewardService.PreviewVictoryReward(mapId, markerId, lockBreakAchieved: false, read);
                    RpgWorld500xDangerVictoryReward lbBefore =
                        RpgWorld500xDangerVictoryRewardService.PreviewVictoryReward(mapId, markerId, lockBreakAchieved: true, read);
                    Assert.IsTrue(stdBefore.IsFirstClear);
                    Assert.IsTrue(lbBefore.IsFirstClear);

                    store[RpgWorld500xDangerVictoryRewardService.FirstClearKey(markerId)] = 1;
                    Assert.IsTrue(RpgWorld500xDangerVictoryRewardService.IsFirstClearCompleted(markerId, read));

                    RpgWorld500xDangerVictoryReward afterPreview =
                        RpgWorld500xDangerVictoryRewardService.PreviewVictoryReward(mapId, markerId, lockBreakAchieved: false, read);
                    Assert.IsFalse(afterPreview.IsFirstClear);
                    Assert.AreEqual(0, afterPreview.BonusSupplyQuantity);

                    RpgWorld500xMapFirstClearSummary next =
                        RpgWorld500xDangerVictoryRewardService.SummarizeRemainingFirstClearRewards(layout, read);
                    Assert.AreEqual(i + 1, next.ClearedDangerZones);
                    Assert.AreEqual(dangerIds.Count - (i + 1), next.RemainingDangerZones);
                    Assert.AreEqual(current.RemainingStandardSaltSilver - stdBefore.SaltSilverReward, next.RemainingStandardSaltSilver);
                    Assert.AreEqual(current.RemainingLockBreakSaltSilver - lbBefore.SaltSilverReward, next.RemainingLockBreakSaltSilver);
                    Assert.AreEqual(current.RemainingStandardReputation - stdBefore.ReputationGain, next.RemainingStandardReputation);
                    Assert.AreEqual(current.RemainingLockBreakReputation - lbBefore.ReputationGain, next.RemainingLockBreakReputation);

                    current = next;
                }

                Assert.AreEqual(0, current.RemainingDangerZones);
                Assert.AreEqual(0, current.RemainingStandardSaltSilver);
                Assert.AreEqual(0, current.RemainingLockBreakSaltSilver);
                Assert.AreEqual(0, current.RemainingStandardReputation);
                Assert.AreEqual(0, current.RemainingLockBreakReputation);
                StringAssert.Contains("已全部完成首破", current.SummaryZh);
            }
        }
    }
}
