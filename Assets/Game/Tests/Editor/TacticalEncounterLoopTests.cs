using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    public sealed class TacticalEncounterLoopTests
    {
        [Test]
        public void WetClothMuffleCountersListeningAlertAndEnablesCriticalStrike()
        {
            var loop = new TacticalEncounterLoop(maxPartyHp: 30, maxEnemyHp: 28);
            Assert.That(loop.NextEnemyIntent, Is.EqualTo(EnemyIntentKind.ListeningAlert));

            TacticalTurnFeedback muffle = loop.ExecuteTurn(TacticalVerb.MuffleWithWetCloth);
            Assert.That(muffle.Accepted, Is.True);
            Assert.That(muffle.EnemyDamageDealt, Is.Zero);
            Assert.That(muffle.EnemyBecameExposed, Is.True);
            Assert.That(loop.NoiseLevel, Is.Zero);
            Assert.That(loop.EnemyExposed, Is.True);
            Assert.That(loop.CanDisengageSafely, Is.True);

            TacticalTurnFeedback crit = loop.ExecuteTurn(TacticalVerb.Strike);
            Assert.That(crit.Accepted, Is.True);
            Assert.That(crit.IsCritical, Is.True);
            Assert.That(crit.PlayerDamageDealt, Is.EqualTo(TacticalEncounterLoop.ExposedStrikeDamage));
            Assert.That(crit.HitstopSeconds, Is.GreaterThanOrEqualTo(0.08f));
            Assert.That(crit.ShakeIntensity, Is.GreaterThanOrEqualTo(0.30f));
        }

        [Test]
        public void DecoyBellInterruptsChargedLungeEnforcesCooldownAndAllowsSafeDisengage()
        {
            var loop = new TacticalEncounterLoop(maxPartyHp: 30, maxEnemyHp: 28);
            loop.ExecuteTurn(TacticalVerb.GuardAndCover);
            Assert.That(loop.NextEnemyIntent, Is.EqualTo(EnemyIntentKind.ChargedLunge));

            TacticalTurnFeedback bell = loop.ExecuteTurn(TacticalVerb.RingDecoyBell);
            Assert.That(bell.Accepted, Is.True);
            Assert.That(bell.InterruptedIntent, Is.True);
            Assert.That(bell.EnemyDamageDealt, Is.Zero);
            Assert.That(loop.EnemyExposed, Is.True);
            Assert.That(loop.BellCooldown, Is.EqualTo(2));
            Assert.That(loop.CanUseVerb(TacticalVerb.RingDecoyBell), Is.False);
            Assert.That(loop.CanUseVerb(TacticalVerb.Disengage), Is.True);

            TacticalTurnFeedback escape = loop.ExecuteTurn(TacticalVerb.Disengage);
            Assert.That(escape.Accepted, Is.True);
            Assert.That(escape.Outcome, Is.EqualTo(TacticalEncounterOutcome.DisengagedSafely));
            Assert.That(loop.CanUseVerb(TacticalVerb.Strike), Is.False);
        }

        [Test]
        public void GuardHalvesDamageRecoversFocusAndUnsafeDisengageIsRejected()
        {
            var loop = new TacticalEncounterLoop(
                maxPartyHp: 30, maxEnemyHp: 28, startingFocus: 1, startingNoise: 45);
            Assert.That(loop.CanUseVerb(TacticalVerb.Disengage), Is.False);
            TacticalTurnFeedback blockedEscape = loop.ExecuteTurn(TacticalVerb.Disengage);
            Assert.That(blockedEscape.Accepted, Is.False);

            TacticalTurnFeedback guard = loop.ExecuteTurn(TacticalVerb.GuardAndCover);
            Assert.That(guard.Accepted, Is.True);
            Assert.That(loop.Focus, Is.EqualTo(2));
            Assert.That(guard.EnemyDamageDealt, Is.EqualTo(2),
                "Noise dropped from 45 to 30, raw 4 halved to 2 while guarding.");
        }

        [Test]
        public void IntentDescriptionsCoverAllStatesInEnglishAndChinese()
        {
            foreach (EnemyIntentKind intent in new[]
            {
                EnemyIntentKind.ListeningAlert,
                EnemyIntentKind.ChargedLunge,
                EnemyIntentKind.Recovering
            })
            {
                Assert.That(TacticalEncounterLoop.DescribeIntent(intent, zh: false), Is.Not.Empty);
                Assert.That(TacticalEncounterLoop.DescribeIntent(intent, zh: true), Is.Not.Empty);
            }
        }
    }
}
