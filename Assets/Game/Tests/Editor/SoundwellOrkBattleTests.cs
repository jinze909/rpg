using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GamingIsLove.Makinom;
using GamingIsLove.ORKFramework;
using GamingIsLove.ORKFramework.Components;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    /// <summary>
    /// Exercises the live Soundwell scene's ORK party setup, then starts one
    /// real Turn Based battle with its three rescuers and the borrowed-voice beast.
    /// </summary>
    public sealed class SoundwellOrkBattleTests
    {
        [UnityTest]
        public IEnumerator ThreePlayerCombatantsEnterOneOrkTurnBasedBattle()
        {
            EditorSceneManager.OpenScene("Assets/Game/Scenes/Bootstrap.unity", OpenSceneMode.Single);
            yield return new EnterPlayMode(expectDomainReload: true);
            yield return WaitForFrameworkInitialization();

            AsyncOperation load = SceneManager.LoadSceneAsync(SoundwellEntrance.SceneName);
            Assert.That(load, Is.Not.Null, "The saved Soundwell scene must load from the Bootstrap runtime.");
            while (!load.isDone)
                yield return null;

            GameObject root = GameObject.Find("Soundwell signal trial");
            Assert.That(root, Is.Not.Null, "The saved Soundwell scene root must exist.");
            SoundwellOrkParty party = root.GetComponent<SoundwellOrkParty>();
            SoundwellTrial trial = root.GetComponent<SoundwellTrial>();
            Assert.That(party, Is.Not.Null, "The live scene must own its ORK party setup.");
            Assert.That(trial, Is.Not.Null, "The live scene must own its rescue route state.");

            float partyDeadline = Time.realtimeSinceStartup + 12f;
            while (!party.IsReady && string.IsNullOrEmpty(party.InitializationError) &&
                   Time.realtimeSinceStartup < partyDeadline)
                yield return null;

            Assert.That(party.IsReady, Is.True,
                "The live Soundwell scene must register its ORK party. " + party.InitializationError);
            float trialDeadline = Time.realtimeSinceStartup + 8f;
            while (!trial.IsReady && Time.realtimeSinceStartup < trialDeadline)
                yield return null;
            Assert.That(trial.IsReady, Is.True, "The live rescue trial must initialize its Makinom state.");
            trial.ReadInput = false;
            Assert.That(party.Members.Count, Is.EqualTo(3));
            Assert.That(party.Actors.Count, Is.EqualTo(3));
            Assert.That(party.PlayerGroup.GetGroup().Count, Is.EqualTo(3));
            Assert.That(party.EnemyGroup.GetGroup().Count, Is.EqualTo(1));
            Assert.That(party.Members[0].GameObject.transform, Is.SameAs(party.Leader));
            Assert.That(party.Actors[1].GetComponent<SpriteRenderer>().enabled, Is.True);
            Assert.That(party.Actors[2].GetComponent<SpriteRenderer>().enabled, Is.True);
            Assert.That(party.PlayerGroup.BattleSystem, Is.SameAs(party.TurnBasedAsset.Settings));
            Assert.That(party.EnemyGroup.BattleSystem, Is.SameAs(party.TurnBasedAsset.Settings));

            Group playerGroup = party.PlayerGroup;
            Group enemyGroup = party.EnemyGroup;
            Rigidbody2D leaderBody = party.Leader.GetComponent<Rigidbody2D>();
            Assert.That(leaderBody, Is.Not.Null, "The ORK leader must share the live exploration body.");
            leaderBody.position = new Vector2(-0.55f, 0f);
            Physics2D.SyncTransforms();
            Assert.That(trial.CanEngageBeast, Is.True,
                "The undecided route must offer direct engagement at the real gate panel.");
            trial.ApplyInput(Vector2.zero, false, false, false, false, true);
            Assert.That(trial.DirectCombatStarted, Is.True,
                "The existing gate interaction must start the direct ORK rescue route.");
            Assert.That(ORK.Game.PlayerHandler.ActiveGroup, Is.SameAs(playerGroup));
            Assert.That(ORK.Game.ActiveGroup.Leader, Is.SameAs(party.Members[0]));

            float battleDeadline = Time.realtimeSinceStartup + 8f;
            while (ORK.Game.Combatants.GetInBattle().Count < 4 &&
                   Time.realtimeSinceStartup < battleDeadline)
                yield return null;

            List<Combatant> participants = ORK.Game.Combatants.GetInBattle();
            Assert.That(participants.Count, Is.EqualTo(4),
                "ORK must register all three scene rescuers and the beast in one battle.");
            foreach (Combatant member in party.Members)
            {
                Assert.That(participants, Does.Contain(member));
                Assert.That(member.GameObject, Is.Not.Null);
            }
            Assert.That(participants, Does.Contain(party.Beast));
            Assert.That(playerGroup.GetBattle().Count, Is.EqualTo(3));
            Assert.That(enemyGroup.GetBattle().Count, Is.EqualTo(1));

            float turnDeadline = Time.realtimeSinceStartup + 6f;
            while (!party.Members.Any(member => member.Actions.IsChoosing) &&
                   Time.realtimeSinceStartup < turnDeadline)
                yield return null;
            Assert.That(party.Members.Any(member => member.Actions.IsChoosing), Is.True,
                "The direct route must enter a real ORK player turn, not only register battle participants.");

            Assert.That(party.BattleInProgress, Is.True,
                "The ORK encounter must still be active when its enemy is defeated.");
            // HP is ORK status value ID 1 in this project. Apply a real ORK
            // status change, then ask ORK's death check to resolve OnMinimum.
            var beastHp = party.Beast.Status[1];
            beastHp.AddValue(-999, false, false, false, true, true, false, false,
                default(StatusValueChangeSource));
            Assert.That(beastHp.GetValue(), Is.EqualTo(0),
                "The ORK HP status must reach its configured minimum.");
            Assert.That(party.Beast.Status.CheckDeath(), Is.True,
                "ORK must resolve the beast's OnMinimum death condition.");
            Assert.That(party.Beast.Status.IsDead, Is.True,
                "ORK must record the beast as dead before the rescue gate can unlock.");
            // The test simulates a completed player attack without a UI menu;
            // use ORK's battle handler to report the encounter's end.
            ORK.Game.Combatants.EndBattle();
            float victoryDeadline = Time.realtimeSinceStartup + 8f;
            while (!trial.DirectCombatComplete && Time.realtimeSinceStartup < victoryDeadline)
                yield return null;
            Assert.That(trial.DirectCombatComplete, Is.True,
                "The gate must remain locked until ORK reports the beast defeated.");
            leaderBody.position = new Vector2(-0.55f, 0f);
            Physics2D.SyncTransforms();
            Assert.That(trial.CanOpenGate, Is.True,
                "A confirmed ORK victory must unlock the same gate without a bell lure.");
            trial.ApplyInput(Vector2.zero, false, false, false, false, true);
            Assert.That(trial.GateOpen, Is.True,
                "The direct-combat route must open the same physical gate used by the bell route.");

            LogAssert.Expect(UnityEngine.LogType.Log, "RPG_SOUNDWELL_ORK_BATTLE_VALIDATED");
            Debug.Log("RPG_SOUNDWELL_ORK_BATTLE_VALIDATED");
            LogAssert.NoUnexpectedReceived();
        }

        private static IEnumerator WaitForFrameworkInitialization()
        {
            double deadline = EditorApplication.timeSinceStartup + 30;
            while ((!Maki.Initialized || !ORK.Initialized) && EditorApplication.timeSinceStartup < deadline)
                yield return null;

            Assert.That(Maki.Initialized, Is.True, "Makinom did not finish loading the saved project.");
            Assert.That(ORK.Initialized, Is.True, "ORK did not initialize from the saved Bootstrap scene.");
        }

        [UnityTearDown]
        public IEnumerator RestoreEditMode()
        {
            if (EditorApplication.isPlaying && ORK.Initialized &&
                ORK.Game.Combatants.GetInBattle().Count > 0)
            {
                ORK.Game.Combatants.EndBattle();
                yield return null;
            }

            if (EditorApplication.isPlaying)
                yield return new ExitPlayMode();
        }
    }
}
