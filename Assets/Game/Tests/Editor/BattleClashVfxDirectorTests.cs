using System.Reflection;
using NUnit.Framework;
using UnityEngine.UI;
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
        public void VerifiedCommandPulseRestoresEveryButtonsAuthoredNonUnitScale()
        {
            var directorRoot = new GameObject("Battle pulse scale restore test");
            var guard = new GameObject("S00 ORK guard",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            var attack = new GameObject("S00 ORK attack",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            Vector3 guardScale = new Vector3(0.82f, 1.13f, 0.74f);
            Vector3 attackScale = new Vector3(1.16f, 0.76f, 0.62f);
            guard.transform.localScale = guardScale;
            attack.transform.localScale = attackScale;
            try
            {
                var director = directorRoot.AddComponent<BattleClashVfxDirector>();
                MethodInfo pulse = typeof(BattleClashVfxDirector).GetMethod(
                    "UpdateRecommendedButtonPulse",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(pulse, Is.Not.Null);

                director.SetVerifiedOrkIntent(EnemyIntentKind.ChargedLunge);
                pulse.Invoke(director, null);
                Assert.That(guard.transform.localScale.x,
                    Is.InRange(guardScale.x, guardScale.x * 1.046f));
                Assert.That(guard.transform.localScale.z, Is.EqualTo(guardScale.z));

                // Switching the ORK-verified recommendation restores Guard
                // even while the attack button starts its own pulse.
                director.SetVerifiedOrkIntent(EnemyIntentKind.Recovering);
                pulse.Invoke(director, null);
                Assert.That(guard.transform.localScale, Is.EqualTo(guardScale));
                Assert.That(attack.transform.localScale.y,
                    Is.InRange(attackScale.y, attackScale.y * 1.046f));
                Assert.That(attack.transform.localScale.z, Is.EqualTo(attackScale.z));

                // Clearing an unavailable/unknown ORK intent cannot leave the
                // previously highlighted button resized or distorted.
                director.SetVerifiedOrkIntent(null);
                Assert.That(attack.transform.localScale, Is.EqualTo(attackScale));
                pulse.Invoke(director, null);
                Assert.That(guard.transform.localScale, Is.EqualTo(guardScale));
                Assert.That(attack.transform.localScale, Is.EqualTo(attackScale));
            }
            finally
            {
                Object.DestroyImmediate(directorRoot);
                Object.DestroyImmediate(guard);
                Object.DestroyImmediate(attack);
            }
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
