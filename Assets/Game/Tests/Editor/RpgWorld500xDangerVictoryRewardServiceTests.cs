using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xDangerVictoryRewardServiceTests
    {
        [Test]
        public void ComputeReward_ScalesByFirstClearAndLockBreakMasteryWithSafeKeys()
        {
            RpgWorld500xMapLayout soundwell = RpgWorld500xMapLayout.Build(RpgWorldMapId.Soundwell);
            var codex = new RpgWorld500xThreatCodexService();
            List<RpgWorld500xDangerZoneDossier> dossiers = codex.BuildMapDossiers(soundwell);
            Assert.Greater(dossiers.Count, 0);

            string markerId = dossiers[0].MarkerId;
            string clearKey = RpgWorld500xDangerVictoryRewardService.FirstClearKey(markerId);
            Assert.IsTrue(RpgWorld500xEnrichedContentAuditor.IsSafeWorldKey(clearKey), clearKey);

            RpgWorld500xDangerVictoryReward firstNormal =
                RpgWorld500xDangerVictoryRewardService.ComputeReward(RpgWorldMapId.Soundwell, markerId, lockBreakAchieved: false, isFirstClear: true);
            RpgWorld500xDangerVictoryReward firstLockBreak =
                RpgWorld500xDangerVictoryRewardService.ComputeReward(RpgWorldMapId.Soundwell, markerId, lockBreakAchieved: true, isFirstClear: true);
            RpgWorld500xDangerVictoryReward repeatNormal =
                RpgWorld500xDangerVictoryRewardService.ComputeReward(RpgWorldMapId.Soundwell, markerId, lockBreakAchieved: false, isFirstClear: false);

            Assert.Greater(firstLockBreak.SaltSilverReward, firstNormal.SaltSilverReward);
            Assert.Greater(firstLockBreak.ReputationGain, firstNormal.ReputationGain);
            Assert.Greater(firstNormal.SaltSilverReward, repeatNormal.SaltSilverReward);
            Assert.AreEqual(1, firstNormal.BonusSupplyQuantity);
            Assert.AreEqual(0, repeatNormal.BonusSupplyQuantity);
            Assert.IsTrue(firstLockBreak.SummaryBannerZh.Contains("破锁精通"));
        }

        [Test]
        public void PreviewVictoryRewardAndRemainingFirstClearSummary_AreReadOnlyAndTrackClears()
        {
            RpgWorld500xMapLayout soundwell = RpgWorld500xMapLayout.Build(RpgWorldMapId.Soundwell);
            var store = new Dictionary<string, int>(StringComparer.Ordinal);
            Func<string, int> read = k => store.TryGetValue(k, out int v) ? v : 0;

            RpgWorld500xMapFirstClearSummary initial =
                RpgWorld500xDangerVictoryRewardService.SummarizeRemainingFirstClearRewards(soundwell, read);
            Assert.AreEqual(RpgWorldMapId.Soundwell, initial.MapId);
            Assert.Greater(initial.TotalDangerZones, 0);
            Assert.AreEqual(0, initial.ClearedDangerZones);
            Assert.AreEqual(initial.TotalDangerZones, initial.RemainingDangerZones);
            Assert.Greater(initial.RemainingLockBreakSaltSilver, initial.RemainingStandardSaltSilver);
            Assert.Greater(initial.RemainingLockBreakReputation, initial.RemainingStandardReputation);
            Assert.That(initial.SummaryZh, Does.Contain("【首破悬赏余量】"));

            var dangerIds = new List<string>();
            for (int i = 0; i < soundwell.Markers.Count; i++)
            {
                if (soundwell.Markers[i].Category == RpgMapMarkerCategory.DangerZone)
                {
                    dangerIds.Add(soundwell.Markers[i].Id);
                }
            }

            string firstId = dangerIds[0];
            Assert.IsFalse(RpgWorld500xDangerVictoryRewardService.IsFirstClearCompleted(firstId, read));
            RpgWorld500xDangerVictoryReward beforePreview =
                RpgWorld500xDangerVictoryRewardService.PreviewVictoryReward(RpgWorldMapId.Soundwell, firstId, lockBreakAchieved: false, read);
            Assert.IsTrue(beforePreview.IsFirstClear);
            Assert.AreEqual(0, store.Count);

            // Mark first DangerZone cleared and verify preview & remaining summary update accurately.
            store[RpgWorld500xDangerVictoryRewardService.FirstClearKey(firstId)] = 1;
            Assert.IsTrue(RpgWorld500xDangerVictoryRewardService.IsFirstClearCompleted(firstId, read));
            RpgWorld500xDangerVictoryReward afterPreview =
                RpgWorld500xDangerVictoryRewardService.PreviewVictoryReward(RpgWorldMapId.Soundwell, firstId, lockBreakAchieved: false, read);
            Assert.IsFalse(afterPreview.IsFirstClear);
            Assert.Less(afterPreview.SaltSilverReward, beforePreview.SaltSilverReward);

            RpgWorld500xMapFirstClearSummary afterOne =
                RpgWorld500xDangerVictoryRewardService.SummarizeRemainingFirstClearRewards(soundwell, read);
            Assert.AreEqual(1, afterOne.ClearedDangerZones);
            Assert.AreEqual(initial.TotalDangerZones - 1, afterOne.RemainingDangerZones);
            Assert.AreEqual(initial.RemainingStandardSaltSilver - beforePreview.SaltSilverReward, afterOne.RemainingStandardSaltSilver);

            // Clear all DangerZones -> remaining drops to zero with completed banner.
            for (int i = 0; i < dangerIds.Count; i++)
            {
                store[RpgWorld500xDangerVictoryRewardService.FirstClearKey(dangerIds[i])] = 1;
            }

            RpgWorld500xMapFirstClearSummary allCleared =
                RpgWorld500xDangerVictoryRewardService.SummarizeRemainingFirstClearRewards(soundwell, read);
            Assert.AreEqual(0, allCleared.RemainingDangerZones);
            Assert.AreEqual(0, allCleared.RemainingStandardSaltSilver);
            Assert.AreEqual(0, allCleared.RemainingLockBreakSaltSilver);
            Assert.That(allCleared.SummaryZh, Does.Contain("已全部完成首破"));
        }

        [Test]
        public void GrantVictoryReward_CreditsSilverReputationMarksFirstClearAndRespectsRepeatScaling()
        {
            var metaStore = new Dictionary<string, int>(StringComparer.Ordinal);
            Func<string, int> read = k => metaStore.TryGetValue(k, out int v) ? v : 0;
            Action<string, int> write = (k, v) => metaStore[k] = v;

            using (var boot = new RpgWorld500xSceneBootstrapper(
                initialMapId: RpgWorldMapId.RedSaltHarbor,
                overrideTileSlices: new Sprite[] { null }))
            {
                var campHaven = new RpgWorld500xCampHavenService(read, write);
                List<RpgWorld500xDangerZoneDossier> dossiers =
                    boot.Driver.Facade.ThreatCodex.BuildMapDossiers(boot.Driver.Facade.Session.Host.Layout);
                string markerId = dossiers[0].MarkerId;

                RpgWorld500xDangerVictoryReward first = RpgWorld500xDangerVictoryRewardService.GrantVictoryReward(
                    boot, campHaven, RpgWorldMapId.RedSaltHarbor, markerId, lockBreakAchieved: true, read, write);

                Assert.IsTrue(first.IsFirstClear);
                Assert.AreEqual(first.SaltSilverReward, boot.Driver.Facade.BountyAndSupply.SaltSilverBalance);
                Assert.AreEqual(first.ReputationGain, campHaven.Reputation.GetPoints(RpgWorldMapId.RedSaltHarbor));
                Assert.AreEqual(1, metaStore[RpgWorld500xDangerVictoryRewardService.FirstClearKey(markerId)]);

                // Second clear on same DangerZone is flagged as repeat clear with reduced silver.
                RpgWorld500xDangerVictoryReward repeat = RpgWorld500xDangerVictoryRewardService.GrantVictoryReward(
                    boot, campHaven, RpgWorldMapId.RedSaltHarbor, markerId, lockBreakAchieved: false, read, write);

                Assert.IsFalse(repeat.IsFirstClear);
                Assert.Less(repeat.SaltSilverReward, first.SaltSilverReward);
                Assert.AreEqual(first.SaltSilverReward + repeat.SaltSilverReward,
                    boot.Driver.Facade.BountyAndSupply.SaltSilverBalance);
            }
        }
    }
}
