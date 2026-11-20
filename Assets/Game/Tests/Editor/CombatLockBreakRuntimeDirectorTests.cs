using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Tests.Editor
{
    public sealed class CombatLockBreakRuntimeDirectorTests
    {
        [Test]
        public void InferBattleModeDistinguishesS00WhitewaterFromSoundwellBeast()
        {
            var s00Solo = new[]
            {
                new OrkPartyBridge.BattleUnitSnapshot("Hero", 24, 24, false, true, false),
                new OrkPartyBridge.BattleUnitSnapshot("Spear squad", 30, 30, false, false, true)
            };
            Assert.That(
                CombatLockBreakRuntimeDirector.InferBattleModeFromSnapshots(s00Solo),
                Is.EqualTo(BattleContextMode.WhitewaterSolo));

            var soundwellParty = new[]
            {
                new OrkPartyBridge.BattleUnitSnapshot("Rescuer 1", 20, 20, false, true, false),
                new OrkPartyBridge.BattleUnitSnapshot("Rescuer 2", 20, 20, false, false, false),
                new OrkPartyBridge.BattleUnitSnapshot("Rescuer 3", 20, 20, false, false, false),
                new OrkPartyBridge.BattleUnitSnapshot("Sound-borrowing Beast", 40, 40, false, false, true)
            };
            Assert.That(
                CombatLockBreakRuntimeDirector.InferBattleModeFromSnapshots(soundwellParty),
                Is.EqualTo(BattleContextMode.SoundwellParty));

            var whitewaterByScene = new[]
            {
                new OrkPartyBridge.BattleUnitSnapshot("Hero", 24, 24, false, true, false),
                new OrkPartyBridge.BattleUnitSnapshot("Enemy", 30, 30, false, false, true)
            };
            Assert.That(
                CombatLockBreakRuntimeDirector.InferBattleModeFromSnapshots(
                    whitewaterByScene, "WhitewaterCrossing"),
                Is.EqualTo(BattleContextMode.WhitewaterSolo));
        }

        [Test]
        public void DirectorAttachesVisualOnlyHudProcessesOrkSnapshotsAndDetachesCleanly()
        {
            var go = new GameObject("CombatLockBreakRuntimeDirector test");
            var director = go.AddComponent<CombatLockBreakRuntimeDirector>();
            try
            {
                director.AttachForBattle(BattleContextMode.WhitewaterSolo, startingMomentum: 1);
                Assert.That(director.IsBattleAttached, Is.True);
                Assert.That(director.ActiveMode, Is.EqualTo(BattleContextMode.WhitewaterSolo));
                Assert.That(director.HudView, Is.Not.Null);
                Assert.That(go.GetComponentInChildren<GraphicRaycaster>(), Is.Null,
                    "Runtime lock-break HUD must never block ORK battle clicks.");

                var s0 = new[]
                {
                    new OrkPartyBridge.BattleUnitSnapshot("Hero", 24, 24, false, true, false),
                    new OrkPartyBridge.BattleUnitSnapshot("Spear squad", 30, 30, false, false, true)
                };
                var s1 = new[]
                {
                    new OrkPartyBridge.BattleUnitSnapshot("Hero", 24, 24, true, false, false),
                    new OrkPartyBridge.BattleUnitSnapshot("Spear squad", 30, 30, false, false, true)
                };
                Assert.That(director.ObserveSnapshots(s0), Is.False);
                Assert.That(director.ObserveSnapshots(s1), Is.True);
                Assert.That(director.Adapter.S00Tracker.BellsCompleted, Is.EqualTo(1));
                Assert.That(director.HudView.ObjectiveLabel.text, Does.Contain("1/3"));

                // Switch to Soundwell mode cleanly and verify acoustic action forwarding.
                director.AttachForBattle(BattleContextMode.SoundwellParty, startingMomentum: 1);
                Assert.That(director.ActiveMode, Is.EqualTo(BattleContextMode.SoundwellParty));
                Assert.That(director.ObserveSoundwellAcousticAction(CombatLockKind.WetClothMuffle), Is.True);
                Assert.That(director.ObserveSoundwellAcousticAction(CombatLockKind.DecoyBellRing, 1), Is.True);
                Assert.That(director.Adapter.SoundwellTracker.IsBeastBroken, Is.True);
                Assert.That(director.HudView.ObjectiveLabel.text, Does.Contain("BREAK"));

                director.DetachBattle();
                Assert.That(director.IsBattleAttached, Is.False);
                Assert.That(director.HudView, Is.Null);
                Assert.That(go.transform.childCount, Is.Zero);
            }
            finally
            {
                director.DetachBattle();
                UnityEngine.Object.DestroyImmediate(go);
            }
        }
    }
}
