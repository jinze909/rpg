using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class BattleRoleAndChoreographyTests
    {
        [Test]
        public void ThreePartySlotsExposeDistinctRolesAndNonDuplicateTacticalChoices()
        {
            var seenVerbs = new HashSet<RoleTacticalVerb>();
            for (int slot = 0; slot < 3; slot++)
            {
                PartyCombatRole role = BattleRoleKit.RoleForSlot(slot);
                Assert.That(BattleRoleKit.RoleName(role, zh: true), Is.Not.Empty);
                Assert.That(BattleRoleKit.RoleName(role, zh: false), Is.Not.Empty);

                var (primary, secondary) = BattleRoleKit.GetTwoChoices(
                    slot, BattleThreatKind.LockedCharge, lockedAllyIndex: 1);
                Assert.That(primary.Verb, Is.Not.EqualTo(secondary.Verb));
                Assert.That(primary.LabelZh, Is.Not.Empty);
                Assert.That(secondary.LabelZh, Is.Not.Empty);
                seenVerbs.Add(primary.Verb);
                seenVerbs.Add(secondary.Verb);
            }

            // 3 distinct members must yield 6 distinct verbs, not 3 identical clones.
            Assert.That(seenVerbs.Count, Is.EqualTo(6));
        }

        [Test]
        public void RecommendationsReactToLiveThreatWithoutRequiringTextWalls()
        {
            var (_, vanguardCover) = BattleRoleKit.GetTwoChoices(
                0, BattleThreatKind.LockedCharge, lockedAllyIndex: 2);
            Assert.That(vanguardCover.Verb, Is.EqualTo(RoleTacticalVerb.OrkGuardCoverAlly));
            Assert.That(vanguardCover.RecommendedNow, Is.True);

            var (_, breakerBell) = BattleRoleKit.GetTwoChoices(
                1, BattleThreatKind.LockedCharge, lockedAllyIndex: 2);
            Assert.That(breakerBell.Verb, Is.EqualTo(RoleTacticalVerb.DecoyBellInterrupt));
            Assert.That(breakerBell.RecommendedNow, Is.True);

            var (supportMuffle, _) = BattleRoleKit.GetTwoChoices(
                2, BattleThreatKind.ListeningScan, lockedAllyIndex: -1);
            Assert.That(supportMuffle.Verb, Is.EqualTo(RoleTacticalVerb.WetClothMuffle));
            Assert.That(supportMuffle.RecommendedNow, Is.True);
        }

        [Test]
        public void LungeAdvancesTowardTargetAndReturnsCleanlyToOrigin()
        {
            Vector2 start = BattleExchangeChoreography.ComputeLungeOffset(0f, Vector2.right, 0.8f);
            Vector2 peak = BattleExchangeChoreography.ComputeLungeOffset(0.35f, Vector2.right, 0.8f);
            Vector2 end = BattleExchangeChoreography.ComputeLungeOffset(1f, Vector2.right, 0.8f);

            Assert.That(start.magnitude, Is.LessThan(0.001f));
            Assert.That(peak.x, Is.GreaterThan(0.75f));
            Assert.That(end.magnitude, Is.LessThan(0.001f));
        }

        [Test]
        public void HitRecoilAndFlashStrictlyRequireActualOrkHpLoss()
        {
            var zeroLoss = BattleExchangeChoreography.ComputeHitRecoilAndFlash(
                0.1f, Vector2.left, actualOrkHpLoss: 0);
            Assert.That(zeroLoss.recoilOffset, Is.EqualTo(Vector2.zero));
            Assert.That(zeroLoss.flashAmount, Is.Zero);

            var realLoss = BattleExchangeChoreography.ComputeHitRecoilAndFlash(
                0.05f, Vector2.left, actualOrkHpLoss: 9);
            Assert.That(realLoss.recoilOffset.x, Is.LessThan(-0.3f));
            Assert.That(realLoss.flashAmount, Is.GreaterThan(0.8f));

            var settled = BattleExchangeChoreography.ComputeHitRecoilAndFlash(
                1f, Vector2.left, actualOrkHpLoss: 9);
            Assert.That(settled.recoilOffset, Is.EqualTo(Vector2.zero));
            Assert.That(settled.flashAmount, Is.Zero);
        }

        [Test]
        public void ThreatTelegraphLineEnablesOnlyForLockedChargeAndDisposesCleanly()
        {
            var root = new GameObject("Battle threat line test");
            LineRenderer line = null;
            try
            {
                BattleThreatVisualCue charge = BattleRoleKit.BuildThreatVisual(
                    BattleThreatKind.LockedCharge, lockedAllyIndex: 1);
                line = BattleExchangeChoreography.CreateOrUpdateThreatLine(
                    root.transform, null, new Vector3(4f, 0f, 0f), Vector3.zero, charge);
                Assert.That(line.enabled, Is.True);
                Assert.That(line.GetPosition(0), Is.EqualTo(new Vector3(4f, 0f, 0f)));
                Assert.That(line.GetPosition(1), Is.EqualTo(Vector3.zero));

                BattleThreatVisualCue staggered = BattleRoleKit.BuildThreatVisual(
                    BattleThreatKind.StaggeredOpening, lockedAllyIndex: -1);
                BattleExchangeChoreography.CreateOrUpdateThreatLine(
                    root.transform, line, new Vector3(4f, 0f, 0f), Vector3.zero, staggered);
                Assert.That(line.enabled, Is.False);
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
