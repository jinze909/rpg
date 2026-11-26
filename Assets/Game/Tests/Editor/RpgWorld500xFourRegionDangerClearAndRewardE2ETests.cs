using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionDangerClearAndRewardE2ETests
    {
        [Test]
        public void AllFourRegions_DangerZoneTriggerClearFirstClearSummaryAndGlobalLedgerStayConsistent()
        {
            var store = new Dictionary<string, int>(StringComparer.Ordinal);
            Func<string, int> read = k => store.TryGetValue(k, out int v) ? v : 0;
            Action<string, int> write = (k, v) => store[k] = v;

            RpgWorld500xGlobalProgress baseline = RpgWorld500xLedgerSummary.Capture(read);
            Assert.AreEqual(4, baseline.PerMap.Count);
            Assert.AreEqual(0, baseline.ClearedDangerZones);
            Assert.Greater(baseline.TotalDangerZones, 0);

            int totalClearedExpected = 0;
            foreach (RpgExpandedMapSpec spec in RpgExpandedWorldScale.AllSpecs)
            {
                RpgWorld500xMapLayout layout = RpgWorld500xMapLayout.Build(spec.MapId);
                var bridge = new RpgWorldOrkExplorationBridge(spec.MapId, read, write);

                RpgWorld500xMapFirstClearSummary initial =
                    RpgWorld500xDangerVictoryRewardService.SummarizeRemainingFirstClearRewards(layout, read);
                Assert.AreEqual(spec.MapId, initial.MapId);
                Assert.Greater(initial.TotalDangerZones, 0);
                Assert.AreEqual(0, initial.ClearedDangerZones);
                Assert.AreEqual(initial.TotalDangerZones, initial.RemainingDangerZones);
                Assert.Greater(initial.RemainingLockBreakSaltSilver, initial.RemainingStandardSaltSilver);
                StringAssert.Contains("【首破悬赏余量】", initial.SummaryZh);

                int clearedInRegion = 0;
                for (int i = 0; i < layout.Markers.Count; i++)
                {
                    RpgMapLandmarkMarker marker = layout.Markers[i];
                    if (marker.Category != RpgMapMarkerCategory.DangerZone)
                    {
                        continue;
                    }

                    Assert.IsFalse(RpgWorld500xDangerVictoryRewardService.IsFirstClearCompleted(marker.Id, read));
                    RpgWorld500xDangerVictoryReward previewBefore =
                        RpgWorld500xDangerVictoryRewardService.PreviewVictoryReward(
                            spec.MapId, marker.Id, lockBreakAchieved: true, read);
                    Assert.IsTrue(previewBefore.IsFirstClear);
                    Assert.Greater(previewBefore.SaltSilverReward, 0);
                    Assert.Greater(previewBefore.ReputationGain, 0);
                    StringAssert.Contains("破锁精通", previewBefore.SummaryBannerZh);

                    Vector2 pos = new Vector2(marker.Tile.x + 0.5f, marker.Tile.y + 0.5f);
                    Assert.IsTrue(bridge.TryTriggerDangerZoneEncounter(marker.Id, pos));
                    Assert.IsTrue(bridge.IsInDangerEncounter);
                    Assert.IsTrue(bridge.TryResolveDangerZoneVictory(marker.Id, true, out _));
                    Assert.IsFalse(bridge.IsInDangerEncounter);

                    write(RpgWorld500xDangerVictoryRewardService.FirstClearKey(marker.Id), 1);
                    Assert.IsTrue(RpgWorld500xDangerVictoryRewardService.IsFirstClearCompleted(marker.Id, read));

                    RpgWorld500xDangerVictoryReward previewAfter =
                        RpgWorld500xDangerVictoryRewardService.PreviewVictoryReward(
                            spec.MapId, marker.Id, lockBreakAchieved: true, read);
                    Assert.IsFalse(previewAfter.IsFirstClear);
                    Assert.Less(previewAfter.SaltSilverReward, previewBefore.SaltSilverReward);

                    clearedInRegion++;
                    totalClearedExpected++;
                }

                Assert.AreEqual(initial.TotalDangerZones, clearedInRegion);
                RpgWorld500xMapFirstClearSummary completed =
                    RpgWorld500xDangerVictoryRewardService.SummarizeRemainingFirstClearRewards(layout, read);
                Assert.AreEqual(0, completed.RemainingDangerZones);
                Assert.AreEqual(initial.TotalDangerZones, completed.ClearedDangerZones);
                Assert.AreEqual(0, completed.RemainingStandardSaltSilver);
                Assert.AreEqual(0, completed.RemainingLockBreakSaltSilver);
                StringAssert.Contains("已全部完成首破", completed.SummaryZh);
            }

            RpgWorld500xGlobalProgress finalProgress = RpgWorld500xLedgerSummary.Capture(read);
            Assert.AreEqual(baseline.TotalDangerZones, totalClearedExpected);
            Assert.AreEqual(baseline.TotalDangerZones, finalProgress.ClearedDangerZones);
            string ledgerSummary = RpgWorld500xLedgerSummary.FormatLedgerSummaryZh(finalProgress);
            StringAssert.Contains(
                "高危清障 " + finalProgress.TotalDangerZones + "/" + finalProgress.TotalDangerZones,
                ledgerSummary);
        }
    }
}
