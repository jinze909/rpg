using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    public sealed class CombatLockBreakRulesTests
    {
        [Test]
        public void MomentumGaugeClampsToThreeAndValidatesSpending()
        {
            var gauge = new MomentumGauge(startingPoints: 1);
            gauge.Gain(5);
            Assert.That(gauge.Current, Is.EqualTo(MomentumGauge.MaxPoints));
            Assert.That(gauge.TrySpend(2), Is.True);
            Assert.That(gauge.Current, Is.EqualTo(1));
            Assert.That(gauge.TrySpend(2), Is.False);
            Assert.That(gauge.Current, Is.EqualTo(1));
        }

        [Test]
        public void EnemyLockBarMitigatesPartiallyAndCancelsFullyWhenAllLocksBreak()
        {
            var bar = new EnemyLockBar(CombatLockKind.WetClothMuffle, CombatLockKind.DecoyBellRing);
            Assert.That(bar.ThreatMitigationPercent, Is.Zero);
            Assert.That(bar.IsBroken, Is.False);

            Assert.That(bar.TryBreakLock(CombatLockKind.ShieldParry), Is.False);
            Assert.That(bar.TryBreakLock(CombatLockKind.WetClothMuffle), Is.True);
            Assert.That(bar.RemainingLocks, Is.EqualTo(1));
            Assert.That(bar.ThreatMitigationPercent, Is.EqualTo(25));
            Assert.That(bar.IsBroken, Is.False);

            Assert.That(bar.TryBreakLock(CombatLockKind.DecoyBellRing), Is.True);
            Assert.That(bar.IsBroken, Is.True);
            Assert.That(bar.ThreatMitigationPercent, Is.EqualTo(100));
        }

        [Test]
        public void S00SoloBridgeheadReachesThirdBellOnlyWhenSapperCableRushIsRepelled()
        {
            var s00 = new S00BridgeheadCableTracker(startingMomentum: 1);
            Assert.That(s00.CurrentThreat, Is.EqualTo(S00BridgeThreatKind.SpearThrustAtHero));

            // Turn 1: Shield Parry mitigates spear thrust -> Bell 1/3, +1 momentum.
            Assert.That(s00.ResolveHeroCounter(CombatLockKind.ShieldParry), Is.True);
            Assert.That(s00.BellsCompleted, Is.EqualTo(1));
            Assert.That(s00.Momentum.Current, Is.EqualTo(2));
            Assert.That(s00.CurrentThreat, Is.EqualTo(S00BridgeThreatKind.SapperRushCable));

            // An unanswered cable rush cannot be bypassed by repeatedly
            // choosing Shield Parry: the second bell stays stalled.
            for (int attempt = 0; attempt < 4; attempt++)
            {
                Assert.That(s00.ResolveHeroCounter(CombatLockKind.ShieldParry), Is.False);
                Assert.That(s00.BellsCompleted, Is.EqualTo(1));
                Assert.That(s00.CurrentThreat, Is.EqualTo(S00BridgeThreatKind.SapperRushCable));
                Assert.That(s00.CurrentLocks.RemainingLocks, Is.EqualTo(1));
                Assert.That(s00.ReadyForThirdBellCollapse, Is.False);
            }

            // Repel the actual cable threat -> bell 2/3; hold against the
            // next spear -> bell 3/3. Only then may the authored bridge end.
            Assert.That(s00.ResolveHeroCounter(CombatLockKind.BladeRepel), Is.True);
            Assert.That(s00.BellsCompleted, Is.EqualTo(2));
            Assert.That(s00.CurrentThreat, Is.EqualTo(S00BridgeThreatKind.SpearThrustAtHero));
            Assert.That(s00.ResolveHeroCounter(CombatLockKind.ShieldParry), Is.True);
            Assert.That(s00.BellsCompleted, Is.EqualTo(3));
            Assert.That(s00.ReadyForThirdBellCollapse, Is.True);
            Assert.That(s00.ResolveHeroCounter(CombatLockKind.BladeRepel), Is.False);
            Assert.That(s00.BellsCompleted, Is.EqualTo(3));
        }

        [Test]
        public void SoundwellAcousticBreakRequiresBothLocksToOpenNonViolentEscape()
        {
            var soundwell = new SoundwellAcousticBreakTracker(startingMomentum: 1);
            Assert.That(soundwell.CanTriggerNonViolentEscape, Is.False);

            Assert.That(soundwell.ApplyAcousticCounter(CombatLockKind.WetClothMuffle), Is.True);
            Assert.That(soundwell.IsBeastBroken, Is.False);
            Assert.That(soundwell.CanTriggerNonViolentEscape, Is.False);

            Assert.That(soundwell.ApplyAcousticCounter(CombatLockKind.DecoyBellRing, momentumCost: 1), Is.True);
            Assert.That(soundwell.IsBeastBroken, Is.True);
            Assert.That(soundwell.CanTriggerNonViolentEscape, Is.True);
            Assert.That(soundwell.Momentum.Current, Is.Zero);

            soundwell.ResetCycle();
            Assert.That(soundwell.IsBeastBroken, Is.False);
        }

        [Test]
        public void S00ThreatDescriptionsExistInBothLanguages()
        {
            Assert.That(S00BridgeheadCableTracker.DescribeThreat(
                S00BridgeThreatKind.SpearThrustAtHero, zh: true), Is.Not.Empty);
            Assert.That(S00BridgeheadCableTracker.DescribeThreat(
                S00BridgeThreatKind.SapperRushCable, zh: false), Is.Not.Empty);
        }
    }
}
