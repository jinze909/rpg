using System;
using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    public sealed class CombatLockBreakRulesGuardTests
    {
        [Test]
        public void MomentumGauge_LargeGainDoesNotOverflow_AndNegativeSpendIsRejected()
        {
            var gauge = new MomentumGauge(startingPoints: 1);
            gauge.Gain(int.MaxValue);
            Assert.That(gauge.Current, Is.EqualTo(MomentumGauge.MaxPoints));

            Assert.That(gauge.TrySpend(-1), Is.False);
            Assert.That(gauge.Current, Is.EqualTo(MomentumGauge.MaxPoints));
            Assert.That(gauge.TrySpend(0), Is.True);
        }

        [Test]
        public void EnemyLockBar_RejectsNullEmptyOrUndefinedLockKinds()
        {
            Assert.Throws<ArgumentException>(() => new EnemyLockBar(null));
            Assert.Throws<ArgumentException>(() => new EnemyLockBar(Array.Empty<CombatLockKind>()));
            Assert.Throws<ArgumentOutOfRangeException>(() => new EnemyLockBar((CombatLockKind)999));
        }

        [Test]
        public void S00BridgeheadCableTracker_UndefinedCounterAction_DoesNotAdvanceBellOrRotateThreat()
        {
            var tracker = new S00BridgeheadCableTracker(startingMomentum: 1);
            Assert.That(tracker.ResolveHeroCounter((CombatLockKind)999), Is.False);
            Assert.That(tracker.BellsCompleted, Is.Zero);
            Assert.That(tracker.CurrentThreat, Is.EqualTo(S00BridgeThreatKind.SpearThrustAtHero));
            Assert.That(tracker.Momentum.Current, Is.EqualTo(1));
        }

        [Test]
        public void SoundwellAcousticBreakTracker_RejectsNegativeMomentumCostAndUndefinedAction()
        {
            var tracker = new SoundwellAcousticBreakTracker(startingMomentum: 1);
            Assert.That(tracker.ApplyAcousticCounter(CombatLockKind.WetClothMuffle, momentumCost: -1), Is.False);
            Assert.That(tracker.ApplyAcousticCounter((CombatLockKind)999, momentumCost: 0), Is.False);
            Assert.That(tracker.AcousticLocks.RemainingLocks, Is.EqualTo(2));
            Assert.That(tracker.Momentum.Current, Is.EqualTo(1));
        }
    }
}
