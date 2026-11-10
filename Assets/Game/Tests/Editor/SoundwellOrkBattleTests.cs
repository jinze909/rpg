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
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using Unity.Cinemachine;
using UnityEngine.UI;
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

            // M3: the real Unity orthographic camera is driven only by one
            // Cinemachine 3 Brain, with one scene-local follow camera. The
            // aspect-responsive lens is owned by that virtual camera.
            var rig = trial.TrialCamera.GetComponent<SoundwellCameraRig>();
            Assert.That(rig, Is.Not.Null, "The Soundwell camera needs a Cinemachine rig.");
            Assert.That(Object.FindObjectsByType<CinemachineBrain>(FindObjectsSortMode.None).Length,
                Is.EqualTo(1), "Exactly one Brain may drive the scene's camera.");
            Assert.That(Object.FindObjectsByType<CinemachineCamera>(FindObjectsSortMode.None).Length,
                Is.EqualTo(1), "Exactly one virtual camera should control Soundwell.");
            Assert.That(rig.VirtualCamera.Follow, Is.SameAs(rig.FramingTarget));
            Assert.That(rig.VirtualCamera.GetComponent<CinemachineFollow>(), Is.Not.Null);
            Assert.That(trial.TrialCamera.orthographic, Is.True);
            float savedAspect = trial.TrialCamera.aspect;
            try
            {
                trial.TrialCamera.aspect = 1f;
                rig.RefreshFraming();
                Assert.That(rig.VirtualCamera.Lens.OrthographicSize, Is.EqualTo(8.4f).Within(0.01f),
                    "Square viewports must retain the original Soundwell world label framing.");
                trial.TrialCamera.aspect = 16f / 9f;
                rig.RefreshFraming();
                Assert.That(rig.VirtualCamera.Lens.OrthographicSize, Is.EqualTo(5.5f).Within(0.01f),
                    "Widescreen must retain the original Soundwell framing.");
            }
            finally
            {
                trial.TrialCamera.aspect = savedAspect;
                rig.RefreshFraming();
            }
            Assert.That(Mathf.Abs(rig.FramingTarget.position.x), Is.LessThanOrEqualTo(0.851f),
                "The follow target must remain inside the horizontal trial framing bounds.");
            Assert.That(Mathf.Abs(rig.FramingTarget.position.y), Is.LessThanOrEqualTo(0.501f),
                "The follow target must remain inside the vertical trial framing bounds.");

            // M4: actual saved Unity Animator clips animate the entire placeholder
            // visual without writing to the leader Rigidbody2D or ORK combatant.
            var visual = GameObject.Find("PLACEHOLDER animated player")
                ?.GetComponent<SoundwellPlayerVisualAnimation>();
            Assert.That(visual, Is.Not.Null);
            Assert.That(visual.Controller, Is.Not.Null);
            Assert.That(visual.Controller.applyRootMotion, Is.False);
            Assert.That(visual.Controller.runtimeAnimatorController.animationClips.Select(c => c.name),
                Is.EquivalentTo(new[] { "Idle", "Move", "Action", "Hit" }));
            foreach (var state in new[] { "Idle", "Move", "Action", "Hit" })
                Assert.That(visual.Controller.HasState(0, Animator.StringToHash(state)), Is.True);
            Assert.That(visual.CurrentState, Is.EqualTo("Idle"));
            trial.ApplyInput(Vector2.right, false, false, false);
            yield return null;
            Assert.That(visual.CurrentState, Is.EqualTo("Move"));
            trial.ApplyInput(Vector2.zero, false, false, false);
            yield return null;
            Assert.That(visual.CurrentState, Is.EqualTo("Idle"));
            leaderBody.position = new Vector2(-0.55f, 0f);
            Physics2D.SyncTransforms();

            // M2: Test the *live* UGUI action canvas and its shared New Input
            // EventSystem before the same gate starts ORK combat. This catches
            // stale focus after a world action becomes unavailable.
            yield return null; // Let the scene recompute button interactability.
            var ringButton = GameObject.Find("Soundwell Ring button")?.GetComponent<Button>();
            var gateButton = GameObject.Find("Soundwell Gate button")?.GetComponent<Button>();
            Assert.That(ringButton, Is.Not.Null);
            Assert.That(gateButton, Is.Not.Null);
            Assert.That(gateButton.interactable, Is.True,
                "The real gate action must be enabled at the battle gate.");

            // The ring and gate interaction radii can legitimately overlap.
            // Query live availability rather than assuming the ring is disabled.
            // The view's documented focus priority is ring, wrap, carry, gate,
            // retry, back. At least one other action must be unavailable.
            var priorityButtons = new[] { "Ring", "Wrap", "Carry", "Gate", "Retry", "Back" }
                .Select(name => GameObject.Find("Soundwell " + name + " button")?.GetComponent<Button>())
                .ToArray();
            Assert.That(priorityButtons.All(button => button != null), Is.True,
                "All six real action buttons must be present in the UGUI canvas.");
            var firstAvailable = priorityButtons.First(button => button.interactable);
            var disabledButton = priorityButtons.FirstOrDefault(button => !button.interactable);
            Assert.That(disabledButton, Is.Not.Null,
                "At least one world-dependent action must be disabled at the gate.");
            trial.SetActionMenuOpen(true);
            Assert.That(trial.ActionMenuOpen, Is.True);
            Assert.That(SoundwellInputAdapter.IsUiContextActive, Is.True,
                "Focused UGUI must disable the gameplay action map.");
            var eventSystem = EventSystem.current;
            Assert.That(eventSystem, Is.Not.Null);
            Assert.That(Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length,
                Is.EqualTo(1), "There must be only one live UI EventSystem.");
            Assert.That(eventSystem.GetComponents<UnityEngine.EventSystems.BaseInputModule>()
                .Count(module => module.enabled), Is.EqualTo(1),
                "Only one active UI input module may dispatch Submit.");
            Assert.That(eventSystem.GetComponent<InputSystemUIInputModule>(), Is.Not.Null);
            Assert.That(eventSystem.sendNavigationEvents, Is.True);
            Assert.That(eventSystem.currentSelectedGameObject, Is.EqualTo(firstAvailable.gameObject),
                "Focus must begin on the first available action, not a stale disabled target.");

            // Simulate a pointer or controller holding focus on an action whose
            // world preconditions no longer hold. Refresh must repair focus,
            // without opening the gate or starting a duplicate ORK battle.
            eventSystem.SetSelectedGameObject(disabledButton.gameObject);
            yield return null;
            Assert.That(eventSystem.currentSelectedGameObject, Is.EqualTo(firstAvailable.gameObject),
                "Refreshing world availability must replace stale disabled-button focus.");
            Assert.That(trial.DirectCombatStarted, Is.False);
            Assert.That(trial.GateOpen, Is.False);
            trial.SetActionMenuOpen(false);
            Assert.That(SoundwellInputAdapter.IsGameplayContextActive, Is.True,
                "Exiting the UI must restore the single gameplay action map.");
            Assert.That(eventSystem.sendNavigationEvents, Is.False);
            Assert.That(eventSystem.currentSelectedGameObject, Is.Null,
                "Closing focus must not leave a gamepad Submit target selected.");

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
