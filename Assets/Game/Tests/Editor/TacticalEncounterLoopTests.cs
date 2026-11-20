using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    public sealed class TacticalEncounterLoopTests
    {
        [Test]
        public void WetClothMuffleBreaksListeningIntentWithoutOwningHpOrOutcome()
        {
            var telegraph = new AcousticIntentTelegraph(
                startingNoise: 25, initialIntent: EnemyIntentKind.ListeningAlert);
            Assert.That(telegraph.CurrentIntent, Is.EqualTo(EnemyIntentKind.ListeningAlert));

            AcousticPresentationFeedback muffle =
                telegraph.RecordPlayerAction(AcousticActionKind.WetClothMuffle);
            Assert.That(muffle.Accepted, Is.True);
            Assert.That(muffle.InterruptedIntent, Is.True);
            Assert.That(muffle.EnemyExposedForTelegraph, Is.True);
            Assert.That(muffle.CanOfferDisengage, Is.True);
            Assert.That(telegraph.NoiseLevel, Is.Zero);
            Assert.That(telegraph.EnemyIntentExposed, Is.True);

            AcousticPresentationFeedback attack =
                telegraph.RecordPlayerAction(AcousticActionKind.OrkAttack);
            Assert.That(attack.Accepted, Is.True);
            Assert.That(attack.HitstopSeconds, Is.GreaterThanOrEqualTo(0.07f));
            Assert.That(attack.ShakeIntensity, Is.GreaterThanOrEqualTo(0.28f));
            Assert.That(telegraph.EnemyIntentExposed, Is.False);
            Assert.That(telegraph.NoiseLevel, Is.EqualTo(25));
        }

        [Test]
        public void DecoyBellInterruptsChargedLungeEnforcesCooldownAndOpensDisengageHint()
        {
            var telegraph = new AcousticIntentTelegraph(
                startingNoise: 30, initialIntent: EnemyIntentKind.ChargedLunge);

            AcousticPresentationFeedback bell =
                telegraph.RecordPlayerAction(AcousticActionKind.DecoyBell);
            Assert.That(bell.Accepted, Is.True);
            Assert.That(bell.InterruptedIntent, Is.True);
            Assert.That(bell.CanOfferDisengage, Is.True);
            Assert.That(telegraph.CurrentIntent, Is.EqualTo(EnemyIntentKind.Recovering));
            Assert.That(telegraph.BellCooldownTurns, Is.EqualTo(2));
            Assert.That(telegraph.CanTriggerAcousticAction(AcousticActionKind.DecoyBell), Is.False);

            AcousticPresentationFeedback rejected =
                telegraph.RecordPlayerAction(AcousticActionKind.DecoyBell);
            Assert.That(rejected.Accepted, Is.False);

            telegraph.AdvanceTelegraphAfterOrkEnemyTurn();
            Assert.That(telegraph.CurrentIntent, Is.EqualTo(EnemyIntentKind.ListeningAlert));
        }

        [Test]
        public void OrkHitFeedbackRequiresActualOrkHpLossAndZeroLossProducesNoShake()
        {
            var none = AcousticIntentTelegraph.ComputeOrkHitFeedback(0, 0, wasExposedTelegraph: true);
            Assert.That(none.hitstopSeconds, Is.Zero);
            Assert.That(none.shakeIntensity, Is.Zero);

            var enemyHit = AcousticIntentTelegraph.ComputeOrkHitFeedback(10, 0, wasExposedTelegraph: true);
            Assert.That(enemyHit.hitstopSeconds, Is.GreaterThanOrEqualTo(0.08f));
            Assert.That(enemyHit.shakeIntensity, Is.GreaterThanOrEqualTo(0.30f));

            var partyHit = AcousticIntentTelegraph.ComputeOrkHitFeedback(0, 8, wasExposedTelegraph: false);
            Assert.That(partyHit.hitstopSeconds, Is.GreaterThan(0f));
            Assert.That(partyHit.shakeIntensity, Is.GreaterThan(0.2f));
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
                Assert.That(AcousticIntentTelegraph.DescribeIntent(intent, zh: false), Is.Not.Empty);
                Assert.That(AcousticIntentTelegraph.DescribeIntent(intent, zh: true), Is.Not.Empty);
            }
        }
    }
}
