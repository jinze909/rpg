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
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using UnityEngine.Tilemaps;
using UnityEngine.Audio;
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
            // M5: the saved scene, rather than transient Wall() objects, owns
            // the static stone boundary. No duplicate BoxCollider2D may exist
            // for a wall already blocked by the TilemapCollider2D.
            var tilemaps = Object.FindObjectsByType<Tilemap>(FindObjectsSortMode.None);
            Assert.That(tilemaps.Length, Is.EqualTo(1), "Soundwell needs one saved wall Tilemap.");
            Tilemap wallMap = tilemaps[0];
            var wallCollider = wallMap.GetComponent<TilemapCollider2D>();
            Assert.That(wallCollider, Is.Not.Null);
            Assert.That(wallMap.GetComponent<TilemapRenderer>()?.sortingLayerName,
                Is.EqualTo("Default"));
            Assert.That(wallMap.GetTile(new Vector3Int(-35, 0, 0)), Is.TypeOf<RuleTile>(),
                "The west enclosure must be a persisted Rule Tile, not a runtime BoxCollider.");
            Assert.That(wallMap.GetTile(new Vector3Int(1, 9, 0)), Is.TypeOf<RuleTile>(),
                "The safety grating upper rail must physically seal the beast lane.");
            Assert.That(wallMap.GetTile(new Vector3Int(1, -10, 0)), Is.TypeOf<RuleTile>(),
                "The lower rail must remain a solid boundary.");
            Assert.That(wallMap.GetTile(new Vector3Int(1, 0, 0)), Is.Null,
                "The only central opening must be controlled by the Makinom gate.");
            Assert.That(GameObject.Find("West wall"), Is.Null);
            Assert.That(GameObject.Find("Safety grating upper"), Is.Null);
            var gateCollider = GameObject.Find("PLACEHOLDER soundwell gate")
                ?.GetComponent<BoxCollider2D>();
            Assert.That(gateCollider, Is.Not.Null);
            Physics2D.SyncTransforms();
            var westRay = Physics2D.Raycast(new Vector2(-6.3f, 0f), Vector2.left, 1.4f);
            Assert.That(westRay.collider, Is.SameAs(wallCollider),
                "The player's west retreat boundary must now be physically solid Tilemap cells.");
            Assert.That(Physics2D.OverlapPoint(new Vector2(0.25f, 0f)), Is.SameAs(gateCollider),
                "A closed physical gate must remain the sole central obstacle.");
            // M6: the actual copper bell routes to one persistent mixer bus,
            // and its SFX attenuation can mute and restore without replacing
            // the AudioSource, changing ORK state or inventing fake audio.
            var bellSource = GameObject.Find("Copper bell and wet cloth")?.GetComponent<AudioSource>();
            Assert.That(bellSource, Is.Not.Null);
            Assert.That(bellSource.playOnAwake, Is.False);
            Assert.That(bellSource.outputAudioMixerGroup, Is.Not.Null,
                "The bell must not bypass the mixer and route straight to AudioListener.");
            Assert.That(bellSource.outputAudioMixerGroup.name, Is.EqualTo("SFX"));
            AudioMixer soundwellMixer = bellSource.outputAudioMixerGroup.audioMixer;
            Assert.That(soundwellMixer, Is.Not.Null);
            Assert.That(soundwellMixer.FindMatchingGroups("").Select(g => g.name),
                Is.EquivalentTo(new[] { "Master", "Music", "Ambient", "SFX" }));
            Assert.That(soundwellMixer.FindSnapshot("Snapshot"), Is.Not.Null);
            Assert.That(SoundwellAudioBus.TryGetVolume(soundwellMixer, out float normalDb), Is.True);
            Assert.That(normalDb, Is.EqualTo(0f).Within(0.1f));
            Assert.That(SoundwellAudioBus.SetMuted(soundwellMixer, true), Is.True);
            Assert.That(SoundwellAudioBus.TryGetVolume(soundwellMixer, out float mutedDb), Is.True);
            Assert.That(mutedDb, Is.LessThanOrEqualTo(-79f));
            Assert.That(SoundwellAudioBus.SetMuted(soundwellMixer, false), Is.True);
            Assert.That(SoundwellAudioBus.TryGetVolume(soundwellMixer, out float restoredDb), Is.True);
            Assert.That(restoredDb, Is.EqualTo(0f).Within(0.1f));
            // Saved StringTables drive a live UGUI button, not duplicate data.
            var title = root.GetComponentsInChildren<Text>(true)
                .SingleOrDefault(label => label.text == "SOUNDWELL  /  SIGNAL TRIAL");
            Assert.That(title, Is.Not.Null);
            var localeButton = root.GetComponentsInChildren<Button>(true)
                .SingleOrDefault(button => button.gameObject.name == "Soundwell Language button");
            Assert.That(localeButton, Is.Not.Null);
            Assert.That(SoundwellUiLocalization.Language, Is.EqualTo(SoundwellUiLocalization.English));
            localeButton.onClick.Invoke();
            Assert.That(SoundwellUiLocalization.Language, Is.EqualTo(SoundwellUiLocalization.Chinese));
            Assert.That(title.text, Is.EqualTo("声井 / 信号试炼"));
            Assert.That(title.font, Is.Not.Null);
            Assert.That(title.font.HasCharacter('声') && title.font.HasCharacter('中'),
                Is.True, "Chinese StringTable text must use a real font with CJK glyphs.");
            Assert.That(localeButton.GetComponentInChildren<Text>().font.HasCharacter('中'), Is.True,
                "The locale toggle must not render only '/ EN' with invisible Chinese glyphs.");
            Assert.That(SoundwellUiLocalization.Get("diagnostic.fallback"),
                Is.EqualTo("English table fallback"));
            Assert.That(SoundwellUiLocalization.Get("diagnostic.unknown"),
                Is.EqualTo("diagnostic.unknown"));
            localeButton.onClick.Invoke();
            Assert.That(title.text, Is.EqualTo("SOUNDWELL  /  SIGNAL TRIAL"));
            LogAssert.Expect(UnityEngine.LogType.Log, "RPG_SOUNDWELL_LOCALIZATION_PLAY_VALIDATED");
            Debug.Log("RPG_SOUNDWELL_LOCALIZATION_PLAY_VALIDATED");
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
            Vector2 originalSafePoint = leaderBody.position;
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
            var pixelCamera = trial.TrialCamera.GetComponent<PixelPerfectCamera>();
            Assert.That(pixelCamera, Is.Not.Null, "The real camera must use URP Pixel Perfect.");
            Assert.That(trial.TrialCamera.GetComponents<PixelPerfectCamera>().Length, Is.EqualTo(1));
            Assert.That(pixelCamera.assetsPPU, Is.EqualTo(16));
            Assert.That(pixelCamera.refResolutionX, Is.EqualTo(320));
            Assert.That(pixelCamera.refResolutionY, Is.EqualTo(180));
            Assert.That(pixelCamera.cropFrame, Is.EqualTo(PixelPerfectCamera.CropFrame.Windowbox));
            Assert.That(pixelCamera.gridSnapping, Is.EqualTo(PixelPerfectCamera.GridSnapping.PixelSnapping));
            Assert.That(typeof(CinemachineExtension).IsAssignableFrom(typeof(CinemachinePixelPerfect)), Is.True,
                "CM3 must load its URP extension, not a fallback dummy MonoBehaviour.");
            Assert.That(rig.VirtualCamera.GetComponent<CinemachinePixelPerfect>(), Is.Not.Null);
            var gameplayHud = trial.GetComponentsInChildren<Canvas>(true)
                .Single(c => c.gameObject.name == "Soundwell actions HUD");
            Assert.That(gameplayHud.renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay),
                "High-resolution UGUI must not be rendered at 320x180.");
            Assert.That(gameplayHud.GetComponent<CanvasScaler>().referenceResolution,
                Is.EqualTo(new Vector2(960f, 540f)));
            float savedAspect = trial.TrialCamera.aspect;
            try
            {
                trial.TrialCamera.aspect = 1f;
                rig.RefreshFraming();
                Assert.That(rig.VirtualCamera.Lens.OrthographicSize, Is.EqualTo(8.5f).Within(0.01f),
                    "Square viewports must show both Soundwell exits.");
                trial.TrialCamera.aspect = 16f / 9f;
                rig.RefreshFraming();
                Assert.That(rig.VirtualCamera.Lens.OrthographicSize, Is.EqualTo(5.625f).Within(0.01f),
                    "Widescreen lens must match 320x180 at 16 PPU.");
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
            Assert.That(rig.FramingTarget.position.x * 16f,
                Is.EqualTo(Mathf.Round(rig.FramingTarget.position.x * 16f)).Within(0.001f));
            Assert.That(rig.FramingTarget.position.y * 16f,
                Is.EqualTo(Mathf.Round(rig.FramingTarget.position.y * 16f)).Within(0.001f));
            LogAssert.Expect(UnityEngine.LogType.Log, "RPG_SOUNDWELL_PIXEL_PERFECT_VALIDATED");
            Debug.Log("RPG_SOUNDWELL_PIXEL_PERFECT_VALIDATED");

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
            // The active player turn must never trap the player with all
            // exploration controls and all exit actions hidden.
            yield return null;
            Assert.That(Object.FindObjectsByType<ButtonPlayerController>(
                    FindObjectsSortMode.None).Any(controller => controller.isActiveAndEnabled),
                Is.False,
                "ORK must not run its 3D Button controller on the 2D Rigidbody2D rescue leader.");
            var battleRetreat = GameObject.Find("Soundwell Back button")?.GetComponent<Button>();
            Assert.That(battleRetreat, Is.Not.Null);
            Assert.That(battleRetreat.gameObject.activeInHierarchy, Is.True,
                "A focused UGUI exit must remain visible during ORK combat.");
            Assert.That(battleRetreat.interactable, Is.True);
            Assert.That(ringButton.gameObject.activeInHierarchy, Is.False,
                "Exploration ring/interaction must remain suspended during combat.");
            Assert.That(EventSystem.current.currentSelectedGameObject,
                Is.SameAs(battleRetreat.gameObject),
                "Keyboard/gamepad Submit must reach the only scene-level retreat action.");
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
            // The battle screen cannot be blank: the provisional, non-interactive
            // readout must show live ORK HP/turn state without a parallel action UI.
            yield return null;
            var battleHud = GameObject.Find("Soundwell ORK battle HUD");
            Assert.That(battleHud, Is.Not.Null,
                "The battle should have a player-visible status HUD.");
            var battleReadout = GameObject.Find("Soundwell ORK battle readout")?.GetComponent<Text>();
            Assert.That(battleReadout, Is.Not.Null);
            Assert.That(battleReadout.text, Does.Contain("Rescuer 1  HP " +
                party.Members[0].Status[1].GetValue()));
            Assert.That(battleReadout.text, Does.Contain("Beast  HP " +
                party.Beast.Status[1].GetValue()));
            Assert.That(battleReadout.text, Does.Contain("< TURN"),
                "The live ORK chooser must be visible to the player.");
            Assert.That(battleReadout.raycastTarget, Is.False);
            Assert.That(battleHud.GetComponent<GraphicRaycaster>(), Is.Null,
                "A status-only overlay cannot intercept ORK input.");

            // This HUD is game-owned UGUI, not ORK battle text. It must
            // follow the same persisted Unity Localization tables as the
            // surrounding Soundwell controls, including the active turn.
            SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
            yield return null;
            Assert.That(battleReadout.text, Does.Contain("救援队员 1  生命 " +
                party.Members[0].Status[1].GetValue()));
            Assert.That(battleReadout.text, Does.Contain("野兽  生命 " +
                party.Beast.Status[1].GetValue()));
            Assert.That(battleReadout.text, Does.Contain("ORK 攻击指令菜单尚未接入。"));
            SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
            yield return null;
            Assert.That(battleReadout.text, Does.Contain("Rescuer 1  HP " +
                party.Members[0].Status[1].GetValue()));

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
            yield return null;
            Assert.That(battleHud.activeInHierarchy, Is.False,
                "The battle-only HUD must disappear after ORK ends the encounter.");
            leaderBody.position = new Vector2(-0.55f, 0f);
            Physics2D.SyncTransforms();
            Assert.That(trial.CanOpenGate, Is.True,
                "A confirmed ORK victory must unlock the same gate without a bell lure.");
            trial.ApplyInput(Vector2.zero, false, false, false, false, true);
            Assert.That(trial.GateOpen, Is.True,
                "The direct-combat route must open the same physical gate used by the bell route.");

            // A defeated ORK battle owns retired combatants. A real Retry
            // reloads the saved Soundwell scene, recreating one fresh group
            // rather than attempting StartGroup on the retired BattleComponent.
            leaderBody.position = originalSafePoint;
            Physics2D.SyncTransforms();
            trial.ApplyInput(Vector2.zero, false, false, true);
            float reloadDeadline = Time.realtimeSinceStartup + 15f;
            while (GameObject.Find("Soundwell signal trial") == root &&
                   Time.realtimeSinceStartup < reloadDeadline)
                yield return null;
            GameObject replayRoot = GameObject.Find("Soundwell signal trial");
            Assert.That(replayRoot, Is.Not.Null, "Retry must load a new saved Soundwell scene.");
            Assert.That(replayRoot, Is.Not.SameAs(root),
                "A completed ORK battle must recreate its scene and combatants before replay.");
            trial = replayRoot.GetComponent<SoundwellTrial>();
            party = replayRoot.GetComponent<SoundwellOrkParty>();
            Assert.That(trial, Is.Not.Null);
            Assert.That(party, Is.Not.Null);
            float replayReadyDeadline = Time.realtimeSinceStartup + 15f;
            while ((!trial.IsReady || !party.IsReady) &&
                   string.IsNullOrEmpty(party.InitializationError) &&
                   Time.realtimeSinceStartup < replayReadyDeadline)
                yield return null;
            Assert.That(trial.IsReady && party.IsReady, Is.True,
                "Replay needs fresh Makinom and ORK registrations: " + party.InitializationError);
            Assert.That(trial.GateOpen, Is.False, "Retry must restore the saved gate state.");
            Assert.That(trial.DirectCombatComplete, Is.False);
            Assert.That(party.Beast.Status.IsDead, Is.False);
            Assert.That(ORK.Control.InBattle, Is.False,
                "The previous ORK battle must release its global control state before retry.");
            leaderBody = party.Leader.GetComponent<Rigidbody2D>();
            Assert.That(leaderBody, Is.Not.Null);
            leaderBody.position = new Vector2(-0.55f, 0f);
            Physics2D.SyncTransforms();
            Assert.That(trial.CanEngageBeast, Is.True);
            trial.ApplyInput(Vector2.zero, false, false, false, false, true);
            Assert.That(trial.DirectCombatStarted, Is.True);
            float secondBattleDeadline = Time.realtimeSinceStartup + 8f;
            while (ORK.Game.Combatants.GetInBattle().Count < 4 &&
                   Time.realtimeSinceStartup < secondBattleDeadline)
                yield return null;
            Assert.That(ORK.Game.Combatants.GetInBattle().Count, Is.EqualTo(4));
            var retreat = GameObject.Find("Soundwell Back button")?.GetComponent<Button>();
            Assert.That(retreat, Is.Not.Null);
            Assert.That(retreat.gameObject.activeInHierarchy, Is.True);
            retreat.onClick.Invoke(); // The actual player's UGUI action.
            Assert.That(trial.DirectCombatStarted, Is.False,
                "Scene withdrawal must clear Makinom's in-progress combat flag.");
            float returnDeadline = Time.realtimeSinceStartup + 8f;
            while (SceneManager.GetActiveScene().name != SoundwellEntrance.BootstrapSceneName &&
                   Time.realtimeSinceStartup < returnDeadline)
                yield return null;
            Assert.That(SceneManager.GetActiveScene().name,
                Is.EqualTo(SoundwellEntrance.BootstrapSceneName));
            Assert.That(ORK.Game.Combatants.GetInBattle(), Is.Empty,
                "Leaving during a live fight must not strand any ORK battle participants.");

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
