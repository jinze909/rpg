using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class OrkCombatLockBreakAdapterTests
    {
        [Test]
        public void S00AdapterTranslatesVerifiedOrkGuardAndAttackSnapshotsIntoThreeBellProgress()
        {
            var root = new GameObject("S00 ORK adapter test");
            var tracker = new S00BridgeheadCableTracker(startingMomentum: 1);
            CombatLockBreakHudView hud = null;
            var observed = new List<(CombatLockKind action, bool broke)>();
            try
            {
                hud = new CombatLockBreakHudView(root.transform, tracker);
                var adapter = new OrkCombatLockBreakAdapter(tracker, hud);
                adapter.CounterObserved += (a, b) => observed.Add((a, b));

                // Baseline snapshot: no transition yet.
                var s0 = new[]
                {
                    new OrkPartyBridge.BattleUnitSnapshot("Hero", 24, 24, false, true, false),
                    new OrkPartyBridge.BattleUnitSnapshot("Spear squad", 30, 30, false, false, true)
                };
                Assert.That(adapter.ObserveOrkSnapshots(s0), Is.False);
                Assert.That(tracker.BellsCompleted, Is.Zero);

                // Verified ORK Defend -> ShieldParry breaks SpearThrustAtHero (Bell 1/3).
                var s1 = new[]
                {
                    new OrkPartyBridge.BattleUnitSnapshot("Hero", 24, 24, true, false, false),
                    new OrkPartyBridge.BattleUnitSnapshot("Spear squad", 30, 30, false, false, true)
                };
                Assert.That(adapter.ObserveOrkSnapshots(s1), Is.True);
                Assert.That(tracker.BellsCompleted, Is.EqualTo(1));
                Assert.That(tracker.CurrentThreat, Is.EqualTo(S00BridgeThreatKind.SapperRushCable));
                Assert.That(hud.ObjectiveLabel.text, Does.Contain("1/3"));

                // Verified ORK Attack lands (-8 enemy HP) -> BladeRepel breaks SapperRushCable (Bell 2/3).
                var s2 = new[]
                {
                    new OrkPartyBridge.BattleUnitSnapshot("Hero", 24, 24, false, true, false),
                    new OrkPartyBridge.BattleUnitSnapshot("Spear squad", 22, 30, false, false, true)
                };
                Assert.That(adapter.ObserveOrkSnapshots(s2), Is.True);
                Assert.That(tracker.BellsCompleted, Is.EqualTo(2));

                // Verified ORK Defend -> Bell 3/3 -> Third Bell collapse ready.
                var s3 = new[]
                {
                    new OrkPartyBridge.BattleUnitSnapshot("Hero", 20, 24, true, false, false),
                    new OrkPartyBridge.BattleUnitSnapshot("Spear squad", 22, 30, false, false, true)
                };
                Assert.That(adapter.ObserveOrkSnapshots(s3), Is.True);
                Assert.That(tracker.ReadyForThirdBellCollapse, Is.True);
                Assert.That(hud.ObjectiveLabel.text, Does.Contain("3/3"));
                Assert.That(observed.Count, Is.EqualTo(3));
                Assert.That(observed[0], Is.EqualTo((CombatLockKind.ShieldParry, true)));
                Assert.That(observed[1], Is.EqualTo((CombatLockKind.BladeRepel, true)));
                Assert.That(observed[2], Is.EqualTo((CombatLockKind.ShieldParry, true)));
            }
            finally
            {
                hud?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void SoundwellAdapterBuildsMomentumOnOrkGuardAndResetsBrokenCycleAfterEnemyStrike()
        {
            var tracker = new SoundwellAcousticBreakTracker(startingMomentum: 1);
            var adapter = new OrkCombatLockBreakAdapter(tracker);
            bool resetFired = false;
            adapter.AcousticCycleReset += () => resetFired = true;

            var s0 = new[]
            {
                new OrkPartyBridge.BattleUnitSnapshot("Rescuer 1", 20, 20, false, true, false),
                new OrkPartyBridge.BattleUnitSnapshot("Beast", 40, 40, false, false, true)
            };
            Assert.That(adapter.ObserveOrkSnapshots(s0), Is.False);

            // ORK Guard builds +1 shared momentum.
            var s1 = new[]
            {
                new OrkPartyBridge.BattleUnitSnapshot("Rescuer 1", 20, 20, true, false, false),
                new OrkPartyBridge.BattleUnitSnapshot("Beast", 40, 40, false, false, true)
            };
            Assert.That(adapter.ObserveOrkSnapshots(s1), Is.True);
            Assert.That(tracker.Momentum.Current, Is.EqualTo(2));

            // Break both acoustic locks -> beast broken.
            Assert.That(adapter.ObserveSoundwellAcousticAction(CombatLockKind.WetClothMuffle), Is.True);
            Assert.That(adapter.ObserveSoundwellAcousticAction(CombatLockKind.DecoyBellRing, momentumCost: 1), Is.True);
            Assert.That(tracker.IsBeastBroken, Is.True);
            Assert.That(tracker.Momentum.Current, Is.EqualTo(1));

            // Authoritative ORK enemy strike on ally resets the broken cycle cleanly.
            var s2 = new[]
            {
                new OrkPartyBridge.BattleUnitSnapshot("Rescuer 1", 15, 20, false, true, false),
                new OrkPartyBridge.BattleUnitSnapshot("Beast", 40, 40, false, false, true)
            };
            Assert.That(adapter.ObserveOrkSnapshots(s2), Is.True);
            Assert.That(tracker.IsBeastBroken, Is.False);
            Assert.That(resetFired, Is.True);
        }

        [Test]
        public void NullTrackersAndEmptySnapshotsHandledSafely()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new OrkCombatLockBreakAdapter((S00BridgeheadCableTracker)null));
            Assert.Throws<ArgumentNullException>(() =>
                new OrkCombatLockBreakAdapter((SoundwellAcousticBreakTracker)null));

            var adapter = new OrkCombatLockBreakAdapter(new S00BridgeheadCableTracker());
            Assert.That(adapter.ObserveOrkSnapshots(null), Is.False);
            Assert.That(adapter.ObserveOrkSnapshots(Array.Empty<OrkPartyBridge.BattleUnitSnapshot>()), Is.False);
            Assert.That(adapter.ObserveSoundwellAcousticAction(CombatLockKind.WetClothMuffle), Is.False);
        }
    }
}
