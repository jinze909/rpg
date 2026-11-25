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
