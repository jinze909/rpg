using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class BattleFeelAndTelegraphDirectorTests
    {
        [Test]
        public void ComputeSnapshotDeltasDetectsRealOrkEnemyAndAllyHpLossAndGuard()
        {
            var prev = new[]
            {
                new OrkPartyBridge.BattleUnitSnapshot("Defender 1", 20, 20, false, true, false),
                new OrkPartyBridge.BattleUnitSnapshot("Defender 2", 20, 20, false, false, false),
                new OrkPartyBridge.BattleUnitSnapshot("Spear squad", 30, 30, false, false, true)
            };
            var next = new[]
            {
                new OrkPartyBridge.BattleUnitSnapshot("Defender 1", 20, 20, true, false, false),
                new OrkPartyBridge.BattleUnitSnapshot("Defender 2", 14, 20, false, true, false),
                new OrkPartyBridge.BattleUnitSnapshot("Spear squad", 22, 30, false, false, true)
            };

            BattleSnapshotDelta delta = BattleFeelAndTelegraphDirector.ComputeSnapshotDeltas(prev, next);
            Assert.That(delta.TotalEnemyHpLoss, Is.EqualTo(8));
            Assert.That(delta.TotalAllyHpLoss, Is.EqualTo(6));
            Assert.That(delta.StruckAllyIndex, Is.EqualTo(1));
            Assert.That(delta.NewGuardAllyIndex, Is.EqualTo(0));

            BattleSnapshotDelta zero = BattleFeelAndTelegraphDirector.ComputeSnapshotDeltas(next, next);
            Assert.That(zero.TotalEnemyHpLoss, Is.Zero);
            Assert.That(zero.TotalAllyHpLoss, Is.Zero);
            Assert.That(zero.StruckAllyIndex, Is.EqualTo(-1));
            Assert.That(zero.NewGuardAllyIndex, Is.EqualTo(-1));
        }

        [Test]
        public void ThreatBannerAndThreeRoleHintsDifferentiateAllPartySlotsBilingually()
        {
            string chargeZh = BattleFeelAndTelegraphDirector.FormatThreatBanner(
                EnemyIntentKind.ChargedLunge, lockedSlot: 0, zh: true);
            string chargeEn = BattleFeelAndTelegraphDirector.FormatThreatBanner(
                EnemyIntentKind.ChargedLunge, lockedSlot: 0, zh: false);
            Assert.That(chargeZh, Does.Contain("1 号位"));
            Assert.That(chargeEn, Does.Contain("SLOT 1"));

            string slot0 = BattleFeelAndTelegraphDirector.FormatRoleHint(0, EnemyIntentKind.ChargedLunge, zh: true);
            string slot1 = BattleFeelAndTelegraphDirector.FormatRoleHint(1, EnemyIntentKind.Recovering, zh: true);
            string slot2 = BattleFeelAndTelegraphDirector.FormatRoleHint(2, EnemyIntentKind.ListeningAlert, zh: true);
            Assert.That(slot0, Does.Contain("前排护阵"));
            Assert.That(slot1, Does.Contain("破阵主力"));
            Assert.That(slot2, Does.Contain("侧翼支援"));
            Assert.That(slot0, Is.Not.EqualTo(slot1));
            Assert.That(slot1, Is.Not.EqualTo(slot2));
        }
    }
}
