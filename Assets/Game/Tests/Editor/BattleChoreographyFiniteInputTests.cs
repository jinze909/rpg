using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class BattleChoreographyFiniteInputTests
    {
        [Test]
        public void ComputeLungeAndRecoilReturnZeroForNonFiniteOrNonPositiveInputs()
        {
            Assert.That(BattleExchangeChoreography.ComputeLungeOffset(
                float.NaN, Vector2.right, 0.8f), Is.EqualTo(Vector2.zero));
            Assert.That(BattleExchangeChoreography.ComputeLungeOffset(
                0.35f, new Vector2(float.PositiveInfinity, 0f), 0.8f), Is.EqualTo(Vector2.zero));
            Assert.That(BattleExchangeChoreography.ComputeLungeOffset(
                0.35f, Vector2.right, float.PositiveInfinity), Is.EqualTo(Vector2.zero));

            var nanTime = BattleExchangeChoreography.ComputeHitRecoilAndFlash(
                float.NaN, Vector2.left, actualOrkHpLoss: 6);
            Assert.That(nanTime.recoilOffset, Is.EqualTo(Vector2.zero));
            Assert.That(nanTime.flashAmount, Is.Zero);

            var infDir = BattleExchangeChoreography.ComputeHitRecoilAndFlash(
                0.1f, new Vector2(float.NaN, 1f), actualOrkHpLoss: 6);
            Assert.That(infDir.recoilOffset, Is.EqualTo(Vector2.zero));
            Assert.That(infDir.flashAmount, Is.Zero);

            var badDist = BattleExchangeChoreography.ComputeHitRecoilAndFlash(
                0.1f, Vector2.left, actualOrkHpLoss: 6, maxRecoilDistance: float.PositiveInfinity);
            Assert.That(badDist.recoilOffset, Is.EqualTo(Vector2.zero));
            Assert.That(badDist.flashAmount, Is.Zero);

            var negDist = BattleExchangeChoreography.ComputeHitRecoilAndFlash(
                0.1f, Vector2.left, actualOrkHpLoss: 6, maxRecoilDistance: -0.5f);
            Assert.That(negDist.recoilOffset, Is.EqualTo(Vector2.zero));
            Assert.That(negDist.flashAmount, Is.Zero);
        }

        [Test]
        public void CreateOrUpdateThreatLineDisablesLineWhenEndpointsAreNonFinite()
        {
            var root = new GameObject("BattleChoreographyFiniteLineRoot");
            LineRenderer line = null;
            try
            {
                BattleThreatVisualCue charge = BattleRoleKit.BuildThreatVisual(
                    BattleThreatKind.LockedCharge, lockedAllyIndex: 1);

                line = BattleExchangeChoreography.CreateOrUpdateThreatLine(
                    root.transform, null,
                    new Vector3(float.NaN, 0f, 0f), Vector3.zero, charge);
                Assert.That(line.enabled, Is.False);

                BattleExchangeChoreography.CreateOrUpdateThreatLine(
                    root.transform, line,
                    new Vector3(4f, 0f, 0f), new Vector3(0f, float.PositiveInfinity, 0f), charge);
                Assert.That(line.enabled, Is.False);

                // Valid finite endpoints still enable and position the threat line cleanly.
                BattleExchangeChoreography.CreateOrUpdateThreatLine(
                    root.transform, line,
                    new Vector3(4f, 0f, 0f), Vector3.zero, charge);
                Assert.That(line.enabled, Is.True);
                Assert.That(line.GetPosition(0), Is.EqualTo(new Vector3(4f, 0f, 0f)));
                Assert.That(line.GetPosition(1), Is.EqualTo(Vector3.zero));
            }
            finally
            {
                if (line != null)
                    BattleExchangeChoreography.SafeDestroy(line.gameObject);
                BattleExchangeChoreography.SafeDestroy(root);
            }
        }
    }
}
