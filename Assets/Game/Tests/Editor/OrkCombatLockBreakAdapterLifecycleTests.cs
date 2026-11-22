using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class OrkCombatLockBreakAdapterLifecycleTests
    {
        [Test]
        public void S00AdapterSkipsPhantomCounterEventsAfterThirdBellReadyAndRefreshesHudBeforeEvent()
        {
            var root = new GameObject("S00 ORK adapter lifecycle test");
            var tracker = new S00BridgeheadCableTracker(startingMomentum: 1);
            CombatLockBreakHudView hud = null;
            var observed = new List<(CombatLockKind action, bool broke, string objectiveDuringEvent)>();
            try
            {
                hud = new CombatLockBreakHudView(root.transform, tracker);
                var adapter = new OrkCombatLockBreakAdapter(tracker, hud);
                adapter.CounterObserved += (action, broke) =>
                    observed.Add((action, broke, hud.ObjectiveLabel.text));

                var s0 = new[]
                {
                    new OrkPartyBridge.BattleUnitSnapshot("Hero", 24, 24, false, true, false),
                    new OrkPartyBridge.BattleUnitSnapshot("Spear squad", 30, 30, false, false, true)
                };
                Assert.That(adapter.ObserveOrkSnapshots(s0), Is.False);

                // Bell 1/3: ShieldParry breaks SpearThrustAtHero; HUD must already show 1/3 inside callback.
                var s1 = new[]
                {
                    new OrkPartyBridge.BattleUnitSnapshot("Hero", 24, 24, true, false, false),
                    new OrkPartyBridge.BattleUnitSnapshot("Spear squad", 30, 30, false, false, true)
                };
                Assert.That(adapter.ObserveOrkSnapshots(s1), Is.True);
                Assert.That(observed.Count, Is.EqualTo(1));
                Assert.That(observed[0].objectiveDuringEvent, Does.Contain("1/3"));

                // Bell 2/3: BladeRepel breaks SapperRushCable.
                var s2 = new[]
                {
                    new OrkPartyBridge.BattleUnitSnapshot("Hero", 24, 24, false, true, false),
                    new OrkPartyBridge.BattleUnitSnapshot("Spear squad", 22, 30, false, false, true)
                };
                Assert.That(adapter.ObserveOrkSnapshots(s2), Is.True);

                // Bell 3/3: ReadyForThirdBellCollapse becomes true.
                var s3 = new[]
                {
                    new OrkPartyBridge.BattleUnitSnapshot("Hero", 20, 24, true, false, false),
                    new OrkPartyBridge.BattleUnitSnapshot("Spear squad", 22, 30, false, false, true)
                };
                Assert.That(adapter.ObserveOrkSnapshots(s3), Is.True);
                Assert.That(tracker.ReadyForThirdBellCollapse, Is.True);
                Assert.That(observed.Count, Is.EqualTo(3));

                // Subsequent ORK Attack/Defend deltas after 3/3 must not fire phantom counter events.
                var s4 = new[]
                {
                    new OrkPartyBridge.BattleUnitSnapshot("Hero", 20, 24, false, true, false),
                    new OrkPartyBridge.BattleUnitSnapshot("Spear squad", 14, 30, false, false, true)
                };
                Assert.That(adapter.ObserveOrkSnapshots(s4), Is.False);
                Assert.That(observed.Count, Is.EqualTo(3));
            }
            finally
            {
                hud?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ExternallyDestroyedHudDoesNotThrowDuringSnapshotOrAcousticUpdates()
        {
            var root = new GameObject("Soundwell ORK adapter destroyed HUD test");
            var tracker = new SoundwellAcousticBreakTracker(startingMomentum: 1);
            CombatLockBreakHudView hud = null;
            try
            {
                hud = new CombatLockBreakHudView(root.transform, tracker);
                var adapter = new OrkCombatLockBreakAdapter(tracker, hud);

                UnityEngine.Object.DestroyImmediate(hud.ObjectiveLabel.gameObject);
                Assert.That(hud.IsDisposed, Is.True);

                var s0 = new[]
                {
                    new OrkPartyBridge.BattleUnitSnapshot("Rescuer 1", 20, 20, false, true, false),
                    new OrkPartyBridge.BattleUnitSnapshot("Beast", 40, 40, false, false, true)
                };
                var s1 = new[]
                {
                    new OrkPartyBridge.BattleUnitSnapshot("Rescuer 1", 20, 20, true, false, false),
                    new OrkPartyBridge.BattleUnitSnapshot("Beast", 40, 40, false, false, true)
                };

                Assert.DoesNotThrow(() => adapter.ObserveOrkSnapshots(s0));
                Assert.That(adapter.ObserveOrkSnapshots(s1), Is.True);
                Assert.That(tracker.Momentum.Current, Is.EqualTo(2));

                Assert.That(adapter.ObserveSoundwellAcousticAction(CombatLockKind.WetClothMuffle), Is.True);
                Assert.That(adapter.ObserveSoundwellAcousticAction(CombatLockKind.DecoyBellRing, momentumCost: 1), Is.True);
                Assert.That(tracker.IsBeastBroken, Is.True);
            }
            finally
            {
                hud?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void SoundwellAdapterRejectsUndefinedLockKindAndNegativeMomentumCost()
        {
            var tracker = new SoundwellAcousticBreakTracker(startingMomentum: 2);
            var adapter = new OrkCombatLockBreakAdapter(tracker);
            int eventsFired = 0;
            adapter.CounterObserved += (_, __) => eventsFired++;

            Assert.That(adapter.ObserveSoundwellAcousticAction((CombatLockKind)99), Is.False);
            Assert.That(adapter.ObserveSoundwellAcousticAction(CombatLockKind.WetClothMuffle, momentumCost: -1), Is.False);
            Assert.That(tracker.Momentum.Current, Is.EqualTo(2));
            Assert.That(tracker.IsBeastBroken, Is.False);
            Assert.That(eventsFired, Is.Zero);
        }
    }
}
