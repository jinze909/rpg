using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class BattleClashVfxDirectorTests
    {
        [Test]
        public void DetectContextDistinguishesSoloWhitewaterFromPartySoundwell()
        {
            Assert.That(BattleClashVfxDirector.DetectContext(whitewaterActive: true, allyCount: 1),
                Is.EqualTo(BattleContextMode.WhitewaterSolo));
            Assert.That(BattleClashVfxDirector.DetectContext(whitewaterActive: true, allyCount: 3),
                Is.EqualTo(BattleContextMode.WhitewaterBridgehead));
            Assert.That(BattleClashVfxDirector.DetectContext(whitewaterActive: false, allyCount: 3),
                Is.EqualTo(BattleContextMode.SoundwellParty));

            Assert.That(BattleClashVfxDirector.FormatContextSubtitle(
                BattleContextMode.WhitewaterSolo, zh: true), Does.Contain("独守渡口"));
            Assert.That(BattleClashVfxDirector.FormatContextSubtitle(
                BattleContextMode.WhitewaterSolo, zh: false), Does.Contain("SOLO HOLD"));
        }

        [Test]
        public void CardRecoilPeaksOnImpactAndRestoresExactZeroAtSettlement()
        {
            var (peakOffset, peakFlash) = BattleClashVfxDirector.EvaluateCardRecoil(
                0.05f, new Vector2(18f, 0f));
            Assert.That(peakOffset.magnitude, Is.GreaterThan(10f));
            Assert.That(peakFlash, Is.GreaterThan(0.8f));

            var (settledOffset, settledFlash) = BattleClashVfxDirector.EvaluateCardRecoil(
                1f, new Vector2(18f, 0f));
            Assert.That(settledOffset, Is.EqualTo(Vector2.zero));
            Assert.That(settledFlash, Is.Zero);
        }

        [Test]
        public void RecommendedCommandRequiresExplicitVerifiedOrkIntent()
        {
            Assert.That(BattleClashVfxDirector.ComputeRecommendedCommand(null),
                Is.EqualTo(RecommendedCommand.None),
                "Unverified/guessed enemy state must never pulse an allegedly safe command.");
            Assert.That(BattleClashVfxDirector.ComputeRecommendedCommand(EnemyIntentKind.ChargedLunge),
                Is.EqualTo(RecommendedCommand.Guard));
            Assert.That(BattleClashVfxDirector.ComputeRecommendedCommand(EnemyIntentKind.Recovering),
                Is.EqualTo(RecommendedCommand.Attack));
            Assert.That(BattleClashVfxDirector.ComputeRecommendedCommand(EnemyIntentKind.ListeningAlert),
                Is.EqualTo(RecommendedCommand.None));
        }
    }
}
