using System;
using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    public sealed class AcousticIntentTelegraphGuardTests
    {
        [Test]
        public void Constructor_UndefinedInitialIntent_ThrowsArgumentOutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new AcousticIntentTelegraph(20, (EnemyIntentKind)999));
        }

        [Test]
        public void UndefinedAcousticAction_IsRejectedWithoutDecrementingBellCooldown()
        {
            var telegraph = new AcousticIntentTelegraph(
                startingNoise: 30, initialIntent: EnemyIntentKind.ChargedLunge);
            AcousticPresentationFeedback bell = telegraph.RecordPlayerAction(AcousticActionKind.DecoyBell);
            Assert.That(bell.Accepted, Is.True);
            Assert.That(telegraph.BellCooldownTurns, Is.EqualTo(2));
            int noiseAfterBell = telegraph.NoiseLevel;

            Assert.That(telegraph.CanTriggerAcousticAction((AcousticActionKind)999), Is.False);
            AcousticPresentationFeedback invalid = telegraph.RecordPlayerAction((AcousticActionKind)999);
            Assert.That(invalid.Accepted, Is.False);
            Assert.That(telegraph.BellCooldownTurns, Is.EqualTo(2));
            Assert.That(telegraph.NoiseLevel, Is.EqualTo(noiseAfterBell));
        }

        [Test]
        public void ComputeOrkHitFeedback_NegativeHpLoss_ProducesZeroImpactFeedback()
        {
            var feedback = AcousticIntentTelegraph.ComputeOrkHitFeedback(-5, -10, wasExposedTelegraph: true);
            Assert.That(feedback.hitstopSeconds, Is.Zero);
            Assert.That(feedback.shakeIntensity, Is.Zero);
        }
    }
}
