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
        public void RetiredAllyDoesNotCreatePhantomDamageWhenRosterShrinks()
        {
            var before = new[]
            {
                new OrkPartyBridge.BattleUnitSnapshot("Defender 1", 20, 20, false, true, false),
                new OrkPartyBridge.BattleUnitSnapshot("Defender 2", 4, 20, false, false, false),
                new OrkPartyBridge.BattleUnitSnapshot("Spear squad", 30, 30, false, false, true)
            };
            var after = new[]
            {
                new OrkPartyBridge.BattleUnitSnapshot("Defender 2", 4, 20, false, false, false),
                new OrkPartyBridge.BattleUnitSnapshot("Spear squad", 30, 30, false, false, true)
            };

            BattleSnapshotDelta delta = BattleFeelAndTelegraphDirector.ComputeSnapshotDeltas(before, after);
            Assert.That(delta.TotalEnemyHpLoss, Is.Zero);
            Assert.That(delta.TotalAllyHpLoss, Is.Zero,
                "A removed first ally must not make the surviving ally appear to take 16 damage.");
            Assert.That(delta.StruckAllyIndex, Is.EqualTo(-1));
            Assert.That(delta.NewGuardAllyIndex, Is.EqualTo(-1));
        }

        [Test]
        public void ReorderedAndRelabeledSnapshotsOnlyReportMatchingOrkActors()
        {
            var before = new[]
            {
                new OrkPartyBridge.BattleUnitSnapshot("Defender 1", 20, 20, false, true, false),
                new OrkPartyBridge.BattleUnitSnapshot("Defender 2", 18, 20, false, false, false),
                new OrkPartyBridge.BattleUnitSnapshot("Spear squad", 30, 30, false, false, true)
            };
            var reordered = new[]
            {
                new OrkPartyBridge.BattleUnitSnapshot("Spear squad", 25, 30, false, false, true),
                new OrkPartyBridge.BattleUnitSnapshot("Defender 2", 14, 20, true, false, false)
            };
            BattleSnapshotDelta delta = BattleFeelAndTelegraphDirector.ComputeSnapshotDeltas(before, reordered);
            Assert.That(delta.TotalEnemyHpLoss, Is.EqualTo(5));
            Assert.That(delta.TotalAllyHpLoss, Is.EqualTo(4));
            Assert.That(delta.StruckAllyIndex, Is.Zero);
            Assert.That(delta.NewGuardAllyIndex, Is.Zero);

            // A localized or replaced label is not evidence of a new ORK hit.
            var relabeled = new[]
            {
                new OrkPartyBridge.BattleUnitSnapshot("救援者 2", 14, 20, true, false, false)
            };
            delta = BattleFeelAndTelegraphDirector.ComputeSnapshotDeltas(before, relabeled);
            Assert.That(delta.TotalAllyHpLoss, Is.Zero);
            Assert.That(delta.StruckAllyIndex, Is.EqualTo(-1));
            Assert.That(delta.NewGuardAllyIndex, Is.EqualTo(-1));

            delta = BattleFeelAndTelegraphDirector.ComputeSnapshotDeltas(
                new OrkPartyBridge.BattleUnitSnapshot[0], reordered);
            Assert.That(delta.TotalEnemyHpLoss, Is.Zero);
            Assert.That(delta.TotalAllyHpLoss, Is.Zero);
            Assert.That(delta.StruckAllyIndex, Is.EqualTo(-1));
            Assert.That(delta.NewGuardAllyIndex, Is.EqualTo(-1));
        }

        [Test]
        public void AdvisoryThreatBannerNeverInventsOrkTargetsAndRoleHintsRemainBilingual()
        {
            foreach (var intent in new[] {
                EnemyIntentKind.ChargedLunge,
                EnemyIntentKind.ListeningAlert,
                EnemyIntentKind.Recovering
            })
            {
                string zh = BattleFeelAndTelegraphDirector.FormatThreatBanner(intent, zh: true);
                string en = BattleFeelAndTelegraphDirector.FormatThreatBanner(intent, zh: false);
                Assert.That(zh, Does.Contain("未确认"));
                Assert.That(en, Does.Contain("unverified"));
                Assert.That(zh, Does.Not.Contain("锁定"));
                Assert.That(en, Does.Not.Contain("LOCKED"));
                Assert.That(en, Does.Not.Contain("SLOT"));
            }

            string slot0 = BattleFeelAndTelegraphDirector.FormatRoleHint(0, EnemyIntentKind.ChargedLunge, zh: true);
            string slot1 = BattleFeelAndTelegraphDirector.FormatRoleHint(1, EnemyIntentKind.Recovering, zh: true);
            string slot2 = BattleFeelAndTelegraphDirector.FormatRoleHint(2, EnemyIntentKind.ListeningAlert, zh: true);
            Assert.That(slot0, Does.Contain("前排护阵"));
            Assert.That(slot1, Does.Contain("破阵主力"));
            Assert.That(slot2, Does.Contain("侧翼支援"));
            Assert.That(slot0, Does.Contain("若遇蓄力"));
            Assert.That(slot1, Does.Contain("若见破绽"));
            Assert.That(slot0, Is.Not.EqualTo(slot1));
            Assert.That(slot1, Is.Not.EqualTo(slot2));
        }
    }
}
