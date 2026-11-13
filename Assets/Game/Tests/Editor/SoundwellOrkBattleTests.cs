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
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
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
        // Run the full combat path before the separate retreat test. Returning
        // through Bootstrap can leave a pending async scene transition while
        // the next test's fresh Play Mode domain is being initialized.
        [UnityTest, Order(10)]
        public IEnumerator ThreePlayerCombatantsEnterOneOrkTurnBasedBattle()
        {
            EditorSceneManager.OpenScene("Assets/Game/Scenes/Bootstrap.unity", OpenSceneMode.Single);
            yield return new EnterPlayMode(expectDomainReload: true);
            yield return WaitForFrameworkInitialization();

            // The real attack integration is long-lived and must not be
            // instantiated as one UnityTest iterator before a Play Mode
            // domain reload. Construct its independent state machine only
            // after the framework and saved Bootstrap are live.
            yield return RunLiveBattleAndRescueAfterInitialization();
        }

        private static IEnumerator RunLiveBattleAndRescueAfterInitialization()
        {
            // Enter through the real Bootstrap UI owner, matching the proven
            // Soundwell player path instead of issuing an unrelated scene load.
            // The previous candidate completed LoadSceneAsync but found no live
            // trial root at the next statement (CI run 43016048935).
            SoundwellEntrance entrance = Object.FindFirstObjectByType<SoundwellEntrance>();
            Assert.That(entrance, Is.Not.Null,
                "Bootstrap must have its saved Soundwell entry component.");
            AsyncOperation load = entrance.OpenTrial();
            Assert.That(load, Is.Not.Null,
                "The real Bootstrap entry must begin loading Soundwell.");
            float loadDeadline = Time.realtimeSinceStartup + 12f;
            while (!load.isDone && Time.realtimeSinceStartup < loadDeadline)
                yield return null;
            Assert.That(load.isDone, Is.True, "Soundwell entry scene loading timed out.");

            // Resolve scene root and both required components synchronously,
            // within the same player-loop iteration. Earlier CI attempts
            // retained a GameObject local across a UnityTest yield and then
            // failed on GetComponent before any attack assertion could run.
            GameObject root = null;
            SoundwellOrkParty party = null;
            SoundwellTrial trial = null;
            float sceneDeadline = Time.realtimeSinceStartup + 8f;
            while (Time.realtimeSinceStartup < sceneDeadline)
            {
                if (TryFindLiveTrialOwners(out root, out party, out trial))
                    break;
                yield return null;
            }
            if (root == null || party == null || trial == null)
            {
                Scene activeScene = SceneManager.GetActiveScene();
                string names = activeScene.IsValid() && activeScene.isLoaded
                    ? string.Join(", ", activeScene.GetRootGameObjects()
                        .Where(item => item != null).Select(item => item.name))
                    : "(unloaded)";
                Assert.Fail("No active Soundwell trial with its two real owner components; " +
                    "scene=" + activeScene.name + ", roots=" + names +
                    ", trial=" + (trial != null) + ", party=" + (party != null));
                yield break;
            }

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
            SoundwellTrialTests.RecordRescueRouteStart("COMBAT", trial, party);
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
            // Followers must be visible pixel characters, not the old
            // 1x1 whiteTexture sprite scaled to a nearly invisible size.
            for (int i = 1; i < party.Actors.Count; i++)
            {
                var follower = party.Actors[i].GetComponent<SpriteRenderer>();
                Assert.That(follower.sprite, Is.Not.Null);
                Assert.That(follower.sprite.pixelsPerUnit, Is.EqualTo(16f));
                Assert.That(follower.sprite.rect.size, Is.EqualTo(new Vector2(16f, 16f)));
                Assert.That(follower.sprite.texture.filterMode, Is.EqualTo(FilterMode.Point));
                Assert.That(follower.sprite.texture.wrapMode, Is.EqualTo(TextureWrapMode.Clamp));
                Assert.That(follower.transform.localScale, Is.EqualTo(Vector3.one));
                var pixels = follower.sprite.texture.GetPixels32();
                Assert.That(pixels.Count(pixel => pixel.a > 0), Is.GreaterThan(70),
                    "A rescuer needs a readable humanoid silhouette, not a single solid pixel.");
                Assert.That(pixels.Count(pixel => pixel.a == 0), Is.GreaterThan(70),
                    "A rescuer silhouette needs transparent outline pixels.");
            }
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
            // Leader and beast must be distinct, point-filtered 16px sprites,
            // not white rectangles; the ORK scene actors remain untouched.
            var pixelLeader = GameObject.Find("PLACEHOLDER player silhouette")
                ?.GetComponent<SpriteRenderer>();
            var pixelBeast = GameObject.Find("PLACEHOLDER beast")
                ?.GetComponent<SpriteRenderer>();
            Assert.That(pixelLeader, Is.Not.Null);
            Assert.That(pixelBeast, Is.Not.Null);
            foreach (var actor in new[] { pixelLeader, pixelBeast })
            {
                Assert.That(actor.sprite.rect.size, Is.EqualTo(new Vector2(16f, 16f)));
                Assert.That(actor.sprite.pixelsPerUnit, Is.EqualTo(16f));
                Assert.That(actor.sprite.texture.filterMode, Is.EqualTo(FilterMode.Point));
                Assert.That(actor.sprite.texture.wrapMode, Is.EqualTo(TextureWrapMode.Clamp));
                Assert.That(actor.transform.localScale, Is.EqualTo(Vector3.one));
                var colorPixels = actor.sprite.texture.GetPixels32();
                Assert.That(colorPixels.Count(pixel => pixel.a > 0), Is.GreaterThan(65));
                Assert.That(colorPixels.Count(pixel => pixel.a == 0), Is.GreaterThan(40));
            }
            Assert.That(pixelLeader.transform.parent, Is.SameAs(visual.transform));
            Assert.That(pixelBeast.transform.parent, Is.SameAs(party.Beast.GameObject.transform));
            // Story 003's person to rescue must use the same authored 16px
            // character grammar, not whiteTexture rectangles and a floating face.
            var surveyorFigure = GameObject.Find("Surveyor figure");
            var pixelSurveyor = GameObject.Find("PLACEHOLDER trapped surveyor")
                ?.GetComponent<SpriteRenderer>();
            Assert.That(surveyorFigure, Is.Not.Null);
            Assert.That(pixelSurveyor, Is.Not.Null);
            Assert.That(pixelSurveyor.transform.parent, Is.SameAs(surveyorFigure.transform));
            Assert.That(pixelSurveyor.sprite, Is.Not.Null);
            Assert.That(pixelSurveyor.sprite.rect.size, Is.EqualTo(new Vector2(16f, 16f)));
            Assert.That(pixelSurveyor.sprite.pixelsPerUnit, Is.EqualTo(16f));
            Assert.That(pixelSurveyor.sprite.texture.filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(pixelSurveyor.sprite.texture.wrapMode, Is.EqualTo(TextureWrapMode.Clamp));
            Assert.That(pixelSurveyor.transform.localScale, Is.EqualTo(Vector3.one));
            var surveyorPixels = pixelSurveyor.sprite.texture.GetPixels32();
            Assert.That(surveyorPixels.Count(pixel => pixel.a > 0), Is.GreaterThan(65));
            Assert.That(surveyorPixels.Count(pixel => pixel.a == 0), Is.GreaterThan(40));
            Assert.That(GameObject.Find("Surveyor face"), Is.Null,
                "The surveyor's face is embedded in his one strict pixel silhouette.");
            // Point-filtered pixels must not spin diagonally when the real
            // beast actor aims toward its sound target. Verify both world
            // headings in the running saved scene; no second AI transform.
            var beastActor = party.Beast.GameObject.transform;
            var initialBeastRotation = beastActor.rotation;
            beastActor.rotation = Quaternion.Euler(0f, 0f, 180f);
            yield return null;
            Assert.That(Quaternion.Angle(pixelBeast.transform.rotation,
                    Quaternion.identity), Is.LessThan(0.01f),
                "Left-facing pixel art must remain upright even when its ORK actor rotates.");
            Assert.That(pixelBeast.flipX, Is.True);
            beastActor.rotation = Quaternion.Euler(0f, 0f, 0f);
            yield return null;
            Assert.That(Quaternion.Angle(pixelBeast.transform.rotation,
                    Quaternion.identity), Is.LessThan(0.01f));
            Assert.That(pixelBeast.flipX, Is.False,
                "Facing right should flip the same visible sprite, not alter ORK AI.");
            beastActor.rotation = initialBeastRotation;
            yield return null;


            Assert.That(visual.Controller.runtimeAnimatorController.animationClips.Select(c => c.name),
                Is.EquivalentTo(new[] { "Idle", "Move", "Action", "Hit" }));
            foreach (var state in new[] { "Idle", "Move", "Action", "Hit" })
                Assert.That(visual.Controller.HasState(0, Animator.StringToHash(state)), Is.True);
            Assert.That(visual.CurrentState, Is.EqualTo("Idle"));
            var idleLeaderSprite = pixelLeader.sprite;
            trial.ApplyInput(Vector2.right, false, false, false);
            yield return null;
            Assert.That(visual.CurrentState, Is.EqualTo("Move"));
            // A second authored 16px frame must really appear during locomotion,
            // while retaining one Animator and never altering the ORK character.
            float walkFrameDeadline = Time.realtimeSinceStartup + 0.6f;
            while (pixelLeader.sprite == idleLeaderSprite &&
                   Time.realtimeSinceStartup < walkFrameDeadline)
                yield return null;
            Assert.That(pixelLeader.sprite, Is.Not.SameAs(idleLeaderSprite),
                "Moving leader needs a second visible pixel pose.");
            Assert.That(pixelLeader.sprite.pixelsPerUnit, Is.EqualTo(16f));
            Assert.That(pixelLeader.sprite.rect.size, Is.EqualTo(new Vector2(16f, 16f)));
            Assert.That(pixelLeader.sprite.texture.filterMode, Is.EqualTo(FilterMode.Point));
            trial.ApplyInput(Vector2.zero, false, false, false);
            yield return null;
            Assert.That(visual.CurrentState, Is.EqualTo("Idle"));
            Assert.That(pixelLeader.sprite, Is.SameAs(idleLeaderSprite),
                "Idle must restore the same original pixel pose.");
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
            var battleAttackButton = GameObject.Find("Soundwell Attack button")?.GetComponent<Button>();
            Assert.That(battleAttackButton, Is.Not.Null);
            Assert.That(new[] { battleRetreat.gameObject, battleAttackButton.gameObject },
                Does.Contain(EventSystem.current.currentSelectedGameObject),
                "Combat setup must focus a visible battle action, not a hidden world button.");
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

            // Attack must be an actual ORK ability from the active combatant,
            // targeted at the saved beast. Do not treat manual status subtraction
            // or a mocked battle-end event as proof of a usable player action.
            var attackButton = battleAttackButton;
            Assert.That(attackButton, Is.Not.Null);
            Assert.That(attackButton.gameObject.activeInHierarchy, Is.True);
            float attackReadyDeadline = Time.realtimeSinceStartup + 6f;
            while (!attackButton.interactable && Time.realtimeSinceStartup < attackReadyDeadline)
                yield return null;
            Assert.That(attackButton.interactable, Is.True,
                "ORK must expose a legal player attack before testing default focus.");
            // The world-space cue identifies the actual current ORK beast
            // target, but has no collider, input or alternative combat authority.
            var targetCue = GameObject.Find("PLACEHOLDER ORK selected beast target")
                ?.GetComponent<SpriteRenderer>();
            Assert.That(targetCue, Is.Not.Null);
            Assert.That(targetCue.enabled, Is.True,
                "Only a legal ORK attack should reveal the target cue.");
            Assert.That(targetCue.sprite.texture.width, Is.EqualTo(16));
            Assert.That(targetCue.sprite.texture.height, Is.EqualTo(16));
            Assert.That(targetCue.sprite.pixelsPerUnit, Is.EqualTo(16f));
            Assert.That(targetCue.sprite.texture.filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(targetCue.transform.localScale, Is.EqualTo(Vector3.one));
            Assert.That(targetCue.GetComponent<Collider2D>(), Is.Null);
            Assert.That(targetCue.transform.position.x,
                Is.EqualTo(party.Beast.GameObject.transform.position.x).Within(0.01f));
            Assert.That(targetCue.transform.position.y,
                Is.EqualTo(party.Beast.GameObject.transform.position.y + 1.12f).Within(0.01f));
            // The paired visual cue must sit over ORK's currently legal
            // attacker's real GameObject, not a duplicate turn queue.
            var turnCue = GameObject.Find("PLACEHOLDER ORK current rescuer")
                ?.GetComponent<SpriteRenderer>();
            Assert.That(turnCue, Is.Not.Null);
            Assert.That(turnCue.enabled, Is.True);
            Assert.That(turnCue.sprite.texture.width, Is.EqualTo(16));
            Assert.That(turnCue.sprite.texture.height, Is.EqualTo(16));
            Assert.That(turnCue.sprite.pixelsPerUnit, Is.EqualTo(16f));
            Assert.That(turnCue.sprite.texture.filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(turnCue.transform.localScale, Is.EqualTo(Vector3.one));
            Assert.That(turnCue.GetComponent<Collider2D>(), Is.Null);
            int actualAttacker = party.ReadyBaseAttackerIndex;
            Assert.That(actualAttacker, Is.InRange(0, party.Actors.Count - 1));
            Assert.That(turnCue.transform.position.x,
                Is.EqualTo(party.Actors[actualAttacker].transform.position.x).Within(0.01f));
            Assert.That(turnCue.transform.position.y,
                Is.EqualTo(party.Actors[actualAttacker].transform.position.y + 1.12f).Within(0.01f));
            Assert.That(EventSystem.current.currentSelectedGameObject,
                Is.SameAs(attackButton.gameObject),
                "First legal attack must take default Submit focus instead of retreating.");
            // UI-only availability matrix on the ACTUAL battle canvas and
            // EventSystem. Do not manufacture ORK turns or hit points: this
            // tests which action Gamepad Submit would activate on startup.
            var viewField = typeof(SoundwellTrial).GetField(
                "view", System.Reflection.BindingFlags.Instance |
                        System.Reflection.BindingFlags.NonPublic);
            Assert.That(viewField, Is.Not.Null);
            object liveView = viewField.GetValue(trial);
            Assert.That(liveView, Is.Not.Null);
            var setBattleMode = liveView.GetType().GetMethod("SetBattleMode");
            var setBattleChoices = liveView.GetType().GetMethod("SetBattleChoices");
            Assert.That(setBattleMode, Is.Not.Null);
            Assert.That(setBattleChoices, Is.Not.Null);
            var focusGuardButton = GameObject.Find("Soundwell Guard button")?.GetComponent<Button>();
            var focusGateButton = GameObject.Find("Soundwell Battle Gate button")?.GetComponent<Button>();
            Assert.That(focusGuardButton, Is.Not.Null);
            Assert.That(focusGateButton, Is.Not.Null);
            System.Action<bool, bool, bool> presentLegalChoices = (legalAttack, legalGuard, legalGate) =>
            {
                setBattleMode.Invoke(liveView, new object[] { false });
                setBattleMode.Invoke(liveView, new object[] { true });
                setBattleChoices.Invoke(liveView, new object[]
                {
                    actualAttacker, party.Actors[actualAttacker].transform,
                    legalAttack, legalGuard, legalGate, false
                });
            };
            presentLegalChoices(false, true, false);
            Assert.That(EventSystem.current.currentSelectedGameObject,
                Is.SameAs(focusGuardButton.gameObject),
                "Guard-only ORK choice must not default gamepad Submit to Retreat.");
            EventSystem.current.SetSelectedGameObject(battleRetreat.gameObject);
            setBattleChoices.Invoke(liveView, new object[]
            {
                actualAttacker, party.Actors[actualAttacker].transform,
                false, true, false, false
            });
            Assert.That(EventSystem.current.currentSelectedGameObject,
                Is.SameAs(battleRetreat.gameObject),
                "After initial focus handoff, a deliberately selected Retreat must remain selected.");
            presentLegalChoices(false, false, true);
            Assert.That(EventSystem.current.currentSelectedGameObject,
                Is.SameAs(focusGateButton.gameObject),
                "Gate-only ORK choice must not default Submit to Retreat.");
            // UI-only legality matrix on the actual UGUI/EventSystem. This
            // cannot spend ORK movement or invent a legal combatant turn.
            var setBattleMoveChoices = liveView.GetType().GetMethod("SetBattleMoveChoices");
            var focusMoveButton = GameObject.Find("Soundwell Move button")?.GetComponent<Button>();
            Assert.That(setBattleMoveChoices, Is.Not.Null);
            Assert.That(focusMoveButton, Is.Not.Null);
            presentLegalChoices(false, false, false);
            setBattleMoveChoices.Invoke(liveView, new object[]
            {
                actualAttacker, party.Actors[actualAttacker].transform, true, false, false, false
            });
            Assert.That(focusMoveButton.interactable, Is.True);
            Assert.That(EventSystem.current.currentSelectedGameObject,
                Is.SameAs(focusMoveButton.gameObject),
                "A move-only chooser must focus enabled Move instead of disabled Attack.");
            Vector3 beforePreview = party.Actors[actualAttacker].transform.position;
            ExecuteEvents.Execute(focusMoveButton.gameObject,
                new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
            var cancelPreview = GameObject.Find("Soundwell Cancel Move button")?.GetComponent<Button>();
            Assert.That(cancelPreview, Is.Not.Null,
                "Controller Submit on Move should open a reversible destination preview.");
            Assert.That(cancelPreview.gameObject.activeInHierarchy, Is.True);
            Assert.That(party.Actors[actualAttacker].transform.position,
                Is.EqualTo(beforePreview),
                "Preview must not move the ORK actor before Confirm.");
            cancelPreview.onClick.Invoke();
            Assert.That(EventSystem.current.currentSelectedGameObject,
                Is.SameAs(focusMoveButton.gameObject),
                "Cancel must return to Move without unintentionally retreating.");
            EventSystem.current.SetSelectedGameObject(battleRetreat.gameObject);
            setBattleChoices.Invoke(liveView, new object[]
            {
                actualAttacker, party.Actors[actualAttacker].transform,
                false, false, false, false
            });
            setBattleMoveChoices.Invoke(liveView, new object[]
            {
                actualAttacker, party.Actors[actualAttacker].transform, true, false, false, false
            });
            Assert.That(EventSystem.current.currentSelectedGameObject,
                Is.SameAs(battleRetreat.gameObject),
                "Deliberate Retreat focus must survive a move-only chooser refresh.");
            setBattleMoveChoices.Invoke(liveView, new object[]
            {
                actualAttacker, party.Actors[actualAttacker].transform, false, false, false, false
            });
            presentLegalChoices(false, false, true);
            setBattleChoices.Invoke(liveView, new object[] { -1, null, false, false, false, false });
            EventSystem battleEvents = EventSystem.current;
            Assert.That(battleEvents.currentSelectedGameObject, Is.SameAs(attackButton.gameObject),
                "Losing the selected choice during resolution must leave a safe Submit target.");
            Assert.That(attackButton.interactable, Is.False);
            Assert.That(battleRetreat.interactable, Is.True);
            ExecuteEvents.Execute(battleEvents.currentSelectedGameObject,
                new BaseEventData(battleEvents), ExecuteEvents.submitHandler);
            Assert.That(trial.DirectCombatStarted, Is.True,
                "Submit while ORK has no legal choice must leave the encounter running.");
            setBattleChoices.Invoke(liveView, new object[]
            {
                actualAttacker, party.Actors[actualAttacker].transform, false, true, false, false
            });
            Assert.That(battleEvents.currentSelectedGameObject, Is.SameAs(focusGuardButton.gameObject),
                "A new legal choice must take focus after resolution.");
            presentLegalChoices(true, true, true);
            Assert.That(EventSystem.current.currentSelectedGameObject,
                Is.SameAs(attackButton.gameObject),
                "When an attack is legal it remains the default choice.");
            // The subsequent frame restores the live ORK-computed choice matrix.
            yield return null;

            var attackRect = (RectTransform)attackButton.transform;
            var retreatRect = (RectTransform)battleRetreat.transform;
            Assert.That(attackRect.anchorMin, Is.EqualTo(new Vector2(0.5f, 0f)));
            Assert.That(retreatRect.anchorMin, Is.EqualTo(attackRect.anchorMin));
            Assert.That(retreatRect.anchoredPosition.y, Is.EqualTo(attackRect.anchoredPosition.y));
            Assert.That(retreatRect.anchoredPosition.x - attackRect.anchoredPosition.x,
                Is.GreaterThan((attackRect.rect.width + retreatRect.rect.width) / 2f),
                "Battle controls must share a clear bottom row without overlapping.");
            Bounds battleFraming = turnCue.bounds;
            battleFraming.Encapsulate(targetCue.bounds);
            battleFraming.Encapsulate(pixelBeast.bounds);
            SoundwellSmokeHost.Capture(trial, "15-ork-current-rescuer",
                visiblePoints: new[] { battleFraming.min, battleFraming.max });
            EventSystem.current.SetSelectedGameObject(battleRetreat.gameObject);
            yield return null;
            Assert.That(EventSystem.current.currentSelectedGameObject,
                Is.SameAs(battleRetreat.gameObject),
                "A deliberate Retreat selection must keep focus during the same ORK turn.");
            EventSystem.current.SetSelectedGameObject(attackButton.gameObject);
            // Diagnose ORK's SAVED, real combatant ability state when the
            // configured base attack is missing or unselectable. Keep the
            // failure strict: no synthetic combat damage or action bypass.
            string attackAvailability = string.Join("; ", party.Members.Select((member, index) =>
            {
                var baseAttack = member.Abilities.GetCurrentBaseAttack();
                // Diagnostic probes must never replace a strict NUnit failure
                // with an unrelated NullReference from an unusable ORK ability.
                string canChoose, canUse, noCost, canTarget;
                try
                {
                    bool choose = baseAttack != null && baseAttack.CanChoose(member, true, true);
                    canChoose = choose.ToString();
                    canUse = (baseAttack != null && baseAttack.CanUse(member, true, true)).ToString();
                    noCost = (baseAttack != null && baseAttack.CanChoose(member, false, false)).ToString();
                    canTarget = !choose ? "not evaluated (cannot choose)" :
                        baseAttack.CanTarget(member, party.Beast).ToString();
                }
                catch (System.Exception error)
                {
                    return "member " + index + " ability probe threw " +
                        error.GetType().Name + ": " + error.Message;
                }
                return "member " + index +
                    " choosing=" + member.Actions.IsChoosing +
                    " dead=" + member.Status.IsDead +
                    " blockers=" + DescribeRuntimeActionBlockers(member, baseAttack) +
                    " baseAttack=" + (baseAttack != null) +
                    " canChoose=" + canChoose +
                    " canUse=" + canUse +
                    " canChooseNoCost=" + noCost +
                    " canTargetBeast=" + canTarget;
            }));
            string baseAttackApi = string.Join("; ", party.Members[0].Abilities.GetType()
                .GetMethods(System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.Instance)
                .Where(method => method.Name.Contains("BaseAttack") ||
                    method.Name.Contains("Ability"))
                .Select(method => method.ToString())
                .Distinct().Take(24));
            Assert.That(party.ReadyBaseAttackerIndex, Is.GreaterThanOrEqualTo(0),
                "A saved ORK player combatant must have a selectable base attack on its turn. " +
                attackAvailability + " | public ORK API: " + baseAttackApi +
                " | current base attack: " +
                DescribeBaseAttackState(party.Members[0].Abilities.GetCurrentBaseAttack()));
            Assert.That(attackButton.interactable, Is.True,
                "The real UGUI action must enable only for the current ORK attack chooser.");
            // Read the actual live HUD *before* the queued team attacks resolve.
            // A turn indicator correctly disappears once the beast is defeated.
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
                "The active ORK chooser must be visible before attacks execute.");
            Assert.That(battleReadout.raycastTarget, Is.False);
            Assert.That(battleHud.GetComponent<GraphicRaycaster>(), Is.Null,
                "A status-only overlay cannot intercept ORK input.");
            SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
            yield return null;
            Assert.That(battleReadout.text, Does.Contain("救援队员 1  生命 " +
                party.Members[0].Status[1].GetValue()));
            Assert.That(battleReadout.text, Does.Contain("野兽  生命 " +
                party.Beast.Status[1].GetValue()));
            Assert.That(battleReadout.text, Does.Not.Contain(
                SoundwellUiLocalization.Get("battle.menu_pending")),
                "A ready ORK base attack must not display the unavailable-attack message.");
            SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
            yield return null;
            Assert.That(battleReadout.text, Does.Contain("Rescuer 1  HP " +
                party.Members[0].Status[1].GetValue()));
            float beastHpBeforeAttack = party.Beast.Status[1].GetValue();
            var attackUser = party.Members[party.ReadyBaseAttackerIndex];
            var configuredAttack = ORK.Abilities.Get(0).GetLevel(1).active;
            var configuredTarget = configuredAttack.targetChange[0];
            var configuredStatusChange = configuredTarget.change[0].settings;
            string damagePipelineSnapshot =
                "targetChangeEligible=" + configuredTarget.CanUse(attackUser, party.Beast) +
                " activeHitChance=" + DescribeConfigObject(configuredAttack.hitChance) +
                " statusChange=" + DescribeConfigObject(configuredStatusChange) +
                " targetChange=" + DescribeConfigObject(configuredTarget) +
                " battleAnimation=" + DescribeConfigObject(configuredAttack.battleAnimation) +
                " systemSchematics=" + DescribeConfigObject(configuredAttack.systemActionSchematics);
            var gateAction = GameObject.Find("Soundwell Battle Gate button")?.GetComponent<Button>();
            var guardAction = GameObject.Find("Soundwell Guard button")?.GetComponent<Button>();
            Assert.That(gateAction, Is.Not.Null);
            Assert.That(guardAction, Is.Not.Null);
            Assert.That(trial.GateOpen, Is.False);
            Assert.That(party.ChoosingRescuerIndex, Is.EqualTo(0));
            Assert.That(gateAction.interactable, Is.True,
                "The rescuer next to the latch can spend the turn opening it.");
            // Probe real 2D plank physics while the ORK encounter owns
            // placement, BEFORE committing the gate action. ORK resolves
            // queued actions only after the rest of the party chooses;
            // requiring GateOpen before Guard/Attack deadlocks the test.
            // Moving the leader after queueing Gate also invalidates the
            // recorded ORK action's physical reach at calculation time.
            Assert.That(trial.DirectCombatStarted, Is.True);
            Assert.That(SoundwellTrial.ShouldRecoverFromLowerPlank(
                true, true, SoundwellTrial.LowerBoardPosition), Is.False,
                "Even an opened gate cannot make exploration physics teleport an ORK combatant.");
            Assert.That(SoundwellTrial.ShouldRecoverFromLowerPlank(
                false, true, SoundwellTrial.LowerBoardPosition), Is.True,
                "The original weak-plank consequence must remain active outside ORK battle.");
            Assert.That(SoundwellTrial.ShouldRecoverFromLowerPlank(
                false, false, SoundwellTrial.LowerBoardPosition), Is.False,
                "The locked gate must still protect the unopened lower route.");
            Assert.That(trial.LowerPlankBroken, Is.False);
            Vector2 battleLeaderPosition = leaderBody.position;
            leaderBody.position = SoundwellTrial.LowerBoardPosition;
            Physics2D.SyncTransforms();
            yield return null;
            Assert.That(Vector2.Distance(trial.PlayerPosition,
                SoundwellTrial.LowerBoardPosition), Is.LessThan(0.03f),
                "Exploration plank recovery must not override ORK-owned combatant positioning.");
            Assert.That(trial.LowerPlankBroken, Is.False,
                "Combat movement must not silently trigger a second exploration plank outcome.");
            leaderBody.position = battleLeaderPosition;
            Physics2D.SyncTransforms();
            yield return null;
            Assert.That(trial.AtGatePanel, Is.True,
                "Restore the real rescuer to the latch before committing ORK's gate action.");
            // Physics.SyncTransforms updates the 2D body immediately, but the
            // ORK chooser and the battle HUD refresh on their own frame. Do not
            // assume the Gate button has republished its ORK eligibility in
            // the same frame as restoring the rigidbody from the plank probe.
            float restoredGateDeadline = Time.realtimeSinceStartup + 2f;
            while (!gateAction.interactable && Time.realtimeSinceStartup < restoredGateDeadline)
                yield return null;
            Assert.That(gateAction.interactable, Is.True,
                "The restored latch action must become a legal ORK turn choice after refresh.");
            gateAction.onClick.Invoke();

            float choiceDeadline = Time.realtimeSinceStartup + 6f;
            while (party.ChoosingRescuerIndex <= 0 && Time.realtimeSinceStartup < choiceDeadline)
                yield return null;
            int guardIndex = party.ChoosingRescuerIndex;
            Assert.That(guardIndex, Is.InRange(1, 2));

            // After the real ORK gate interaction spends the leader's turn,
            // the visible world marker must follow the *new* chooser rather
            // than continuing to identify the prior leader at the latch.
            yield return null;
            Assert.That(turnCue.enabled, Is.True,
                "A guard-only turn still needs a visible active-rescuer marker.");
            Assert.That(turnCue.transform.position.x,
                Is.EqualTo(party.Actors[guardIndex].transform.position.x).Within(0.01f));
            Assert.That(turnCue.transform.position.y,
                Is.EqualTo(party.Actors[guardIndex].transform.position.y + 1.12f).Within(0.01f),
                "The marker must follow the actual ORK guard chooser.");
            Assert.That(guardAction.interactable, Is.True);
            Assert.That(gateAction.interactable, Is.False,
                "The latch is already queued or open and cannot consume a second turn.");
            var defenderVisual = party.Actors[guardIndex].GetComponent<SpriteRenderer>();
            Assert.That(defenderVisual, Is.Not.Null);
            Sprite idleBeforeGuard = defenderVisual.sprite;
            guardAction.onClick.Invoke();
            // The UGUI click must be accepted by the real ORK shortcut before
            // presentation changes. Capture immediately; ORK may advance the
            // next turn and clear Defending before any later yield.
            Sprite guardPose = defenderVisual.sprite;
            Assert.That(guardPose, Is.Not.Null);
            Assert.That(guardPose, Is.Not.SameAs(idleBeforeGuard));
            Assert.That(guardPose.name, Does.Contain("guard shield"));
            // The shield is drawn on the left edge of its 16px sprite.
            // During the REAL accepted ORK guard action it must face the
            // living beast, regardless of the follower's last walk heading.
            Assert.That(party.Beast.GameObject, Is.Not.Null);
            float guardThreatX = party.Beast.GameObject.transform.position.x -
                party.Actors[guardIndex].transform.position.x;
            Assert.That(Mathf.Abs(guardThreatX), Is.GreaterThan(0.01f),
                "Guard facing requires a non-overlapping live threat and defender.");
            Assert.That(defenderVisual.flipX, Is.EqualTo(guardThreatX > 0f),
                "The guard shield must face the real ORK enemy, not an earlier walk direction.");

            choiceDeadline = Time.realtimeSinceStartup + 6f;
            while ((party.ReadyBaseAttackerIndex < 0 ||
                    party.ReadyBaseAttackerIndex == guardIndex ||
                    party.ReadyBaseAttackerIndex == 0) && Time.realtimeSinceStartup < choiceDeadline)
                yield return null;
            Assert.That(party.ReadyBaseAttackerIndex, Is.EqualTo(3 - guardIndex),
                "Opening, guarding and attacking must spend three distinct ORK turns.");
            // A completed Guard must also transfer the HUD/world cue to the
            // remaining live ORK attacker, not leave it above the defender.
            yield return null;
            int finishingAttacker = party.ReadyBaseAttackerIndex;
            Assert.That(finishingAttacker, Is.EqualTo(3 - guardIndex));
            Assert.That(turnCue.enabled, Is.True);
            Assert.That(turnCue.transform.position.x,
                Is.EqualTo(party.Actors[finishingAttacker].transform.position.x).Within(0.01f));
            Assert.That(turnCue.transform.position.y,
                Is.EqualTo(party.Actors[finishingAttacker].transform.position.y + 1.12f).Within(0.01f),
                "The turn marker must switch after an ORK-accepted Guard.");
            // Turn progression above demonstrates ORK accepted the command;
            // the shield captured on click does not invent a gameplay status.
            Assert.That(guardPose.rect.size, Is.EqualTo(new Vector2(16f, 16f)));
            Assert.That(guardPose.pixelsPerUnit, Is.EqualTo(16f));
            Assert.That(guardPose.texture.filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(attackButton.interactable, Is.True);
            attackButton.onClick.Invoke();
            float attackResolutionDeadline = Time.realtimeSinceStartup + 8f;
            while (party.Beast.Status[1].GetValue() >= beastHpBeforeAttack &&
                   !party.Beast.Status.IsDead &&
                   Time.realtimeSinceStartup < attackResolutionDeadline)
                yield return null;
            Assert.That(party.Beast.Status[1].GetValue(), Is.LessThan(beastHpBeforeAttack),
                "A player UGUI click must resolve its target and HP through a real ORK base attack. " +
                "ActiveAbility=" + DescribeActiveAttackSetup(
                    party.Members[0].Abilities.GetCurrentBaseAttack()) +
                " turn=" + party.Members[0].Actions.IsChoosing +
                " battle=" + party.BattleInProgress +
                " pipeline=" + damagePipelineSnapshot);

            // The queued, player-selected ORK attack has already reduced
            // the beast's real HP. Its configured death check and battle end
            // must now complete naturally; no test-side HP mutation or
            // manual Combatants.EndBattle() may manufacture victory.
            float victoryDeadline = Time.realtimeSinceStartup + 12f;
            while ((!party.Beast.Status.IsDead || !trial.DirectCombatComplete) &&
                   Time.realtimeSinceStartup < victoryDeadline)
                yield return null;
            Assert.That(party.Beast.Status.IsDead, Is.True,
                "The actual ORK attack must resolve the beast's OnMinimum death.");
            Assert.That(trial.DirectCombatComplete, Is.True,
                "The actual ORK battle must end and unlock the route without a test-side EndBattle.");
            Assert.That(party.BattleInProgress, Is.False,
                "The natural ORK attack must release all battle participants.");
            yield return null;
            Assert.That(battleHud.activeInHierarchy, Is.False,
                "The battle-only HUD must disappear after ORK ends the encounter.");
            // The real attack was submitted through a focused UGUI button.
            // When that button is hidden on victory, no stale battle-only
            // selection may capture the next gamepad/keyboard Submit.
            GameObject victoryFocus = EventSystem.current.currentSelectedGameObject;
            Assert.That(victoryFocus == null || victoryFocus.activeInHierarchy, Is.True,
                "ORK victory must not leave EventSystem focused on a hidden button.");
            Assert.That(victoryFocus, Is.Not.SameAs(attackButton.gameObject));
            Assert.That(victoryFocus, Is.Not.SameAs(guardAction.gameObject));
            Assert.That(victoryFocus, Is.Not.SameAs(gateAction.gameObject));
            Assert.That(targetCue.enabled, Is.False,
                "The target cue must vanish on real ORK victory, not linger over the rescue route.");
            Assert.That(turnCue.enabled, Is.False,
                "The active rescuer cue must also disappear on ORK victory.");
            var explorationBackLabel = GameObject.Find("Soundwell Back button")?.GetComponentInChildren<Text>();
            Assert.That(explorationBackLabel, Is.Not.Null);
            Assert.That(explorationBackLabel.text,
                Is.EqualTo(SoundwellUiLocalization.Get("action.back")),
                "After ORK victory, the same UGUI action must return to its exploration label.");
            Assert.That(retreatRect.anchoredPosition.y,
                Is.Not.EqualTo(attackRect.anchoredPosition.y),
                "The reused Retreat button must return to the exploration layout.");
            leaderBody.position = new Vector2(-0.55f, 0f);
            Physics2D.SyncTransforms();
            Assert.That(trial.GateOpen, Is.True,
                "The queued battle interaction must open the real gate and survive victory.");

            // Story 003: ORK victory unlocks a physical route, not the rescue
            // outcome. Reuse this already-verified live scene/ORK party rather
            // than starting a duplicate Play Mode fixture for the same fight.
            Assert.That(trial.DirectCombatComplete && !trial.RescueComplete &&
                !trial.SurveyorEscorting, Is.True,
                "Defeating the beast alone must not rescue the surveyor.");
            Assert.That(SoundwellOrkParty.RescueSettled, Is.False);
            Assert.That(party.Members.All(member =>
                member.Status[SoundwellOrkParty.ExperienceStatus].GetValue() == 0), Is.True);
            leaderBody.position = SoundwellTrial.SurveyorPosition;
            Physics2D.SyncTransforms();
            Assert.That(trial.AtSurveyor, Is.True);
            trial.ApplyInput(Vector2.zero, false, false, false, false, true);
            Assert.That(trial.HasSurveyorClue && !trial.RescueComplete, Is.True,
                "The surveyor's route clue must still be requested by the player.");
            leaderBody.position = SoundwellTrial.UpperBoardPosition;
            Physics2D.SyncTransforms();
            Assert.That(trial.CanSecurePlank, Is.True);
            trial.ApplyInput(Vector2.zero, false, false, false, false, true);
            Assert.That(trial.PlankSecured && !trial.SurveyorEscorting, Is.True,
                "Bracing the plank alone must not initiate or finish rescue.");
            float escortDeadline = Time.realtimeSinceStartup + 10f;
            while (!trial.CanEscortSurveyor && Time.realtimeSinceStartup < escortDeadline)
                yield return null;
            Assert.That(trial.CanEscortSurveyor, Is.True,
                "The survivor must physically reach the braced upper board.");
            trial.ApplyInput(Vector2.zero, false, false, false, false, true);
            Assert.That(trial.SurveyorEscorting && !trial.RescueComplete, Is.True,
                "Player action must start escort without rewarding early victory.");

            // Guide the same live surveyor through the actual doorway. No
            // forced NPC teleport, duplicate quest flag or simulated rescue.
            leaderBody.position = new Vector2(0.45f, 0f);
            Physics2D.SyncTransforms();
            escortDeadline = Time.realtimeSinceStartup + 10f;
            while (trial.SurveyorWorldPosition.x > 0.85f &&
                   Time.realtimeSinceStartup < escortDeadline)
                yield return null;
            Assert.That(trial.SurveyorWorldPosition.x, Is.LessThanOrEqualTo(0.85f),
                "The survivor must enter the open gate on his own.");
            leaderBody.position = new Vector2(-0.85f, 0f);
            Physics2D.SyncTransforms();
            escortDeadline = Time.realtimeSinceStartup + 10f;
            while (trial.SurveyorWorldPosition.x > -0.65f &&
                   Time.realtimeSinceStartup < escortDeadline)
                yield return null;
            Assert.That(trial.SurveyorWorldPosition.x, Is.LessThanOrEqualTo(-0.65f),
                "The survivor must cross to the western side of the gate.");
            Assert.That(trial.RescueComplete, Is.False,
                "Crossing the gate cannot substitute for reaching RETREAT.");

            // Keep each leader step close enough for the real distance-based
            // follow AI; a sprint far ahead deliberately causes an escort pause.
            for (float x = -1.55f; x > originalSafePoint.x + 0.5f; x -= 1.25f)
            {
                leaderBody.position = new Vector2(x, originalSafePoint.y);
                Physics2D.SyncTransforms();
                float expectedFollowerX = x + 0.75f;
                escortDeadline = Time.realtimeSinceStartup + 10f;
                while (trial.SurveyorWorldPosition.x > expectedFollowerX &&
                       Time.realtimeSinceStartup < escortDeadline)
                    yield return null;
                Assert.That(trial.SurveyorWorldPosition.x, Is.LessThanOrEqualTo(expectedFollowerX),
                    "The surveyor must follow each real guide position toward safety.");
                // A guide step near RETREAT can already count as arrival
                // when both actors are physically within the safe radius.
                if (Vector2.Distance(leaderBody.position, originalSafePoint) > 1.1f)
                    Assert.That(trial.RescueComplete, Is.False,
                        "A remote escort checkpoint cannot complete the rescue.");
            }
            leaderBody.position = originalSafePoint;
            Physics2D.SyncTransforms();
            escortDeadline = Time.realtimeSinceStartup + 15f;
            while (!trial.RescueComplete && Time.realtimeSinceStartup < escortDeadline)
                yield return null;
            Assert.That(trial.RescueComplete, Is.True,
                "The player and actual survivor must both reach RETREAT.");
            SoundwellTrialTests.AssertRescueReward(party);
            Assert.That(Vector2.Distance(trial.SurveyorWorldPosition, originalSafePoint),
                Is.LessThanOrEqualTo(0.98f));
            Assert.That(trial.DirectCombatComplete, Is.True,
                "Rescue must retain the real ORK combat route's victory.");
            escortDeadline = Time.realtimeSinceStartup + 10f;
            while (!trial.FalseMarkerCleared && Time.realtimeSinceStartup < escortDeadline)
                yield return null;
            Assert.That(trial.FalseMarkerCleared, Is.True,
                "The saved surveyor must physically remove his misleading marker.");
            LogAssert.Expect(UnityEngine.LogType.Log, "RPG_SOUNDWELL_ROUTE_COST:COMBAT:world=1:turn=3:escort=3");
            Debug.Log("RPG_SOUNDWELL_ROUTE_COST:COMBAT:world=1:turn=3:escort=3");
            LogAssert.Expect(UnityEngine.LogType.Log, "RPG_SOUNDWELL_DIRECT_RESCUE_VALIDATED");
            Debug.Log("RPG_SOUNDWELL_DIRECT_RESCUE_VALIDATED");

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

            // Next-stage progress handoff: the rescue reward must survive a
            // scene retry, a second ORK battle and an actual Bootstrap return.
            // The entry report may read only the live ORK group/quest: never
            // count the scene replay as another earned reward.
            var progressEntry = Object.FindFirstObjectByType<SoundwellEntrance>();
            Assert.That(progressEntry, Is.Not.Null);
            progressEntry.RefreshReturnOutcome(true);
            Assert.That(SoundwellOrkParty.RescueSettled, Is.True);
            var returningGroup = ORK.Game.PlayerHandler.GetGroup(0);
            Assert.That(returningGroup, Is.Not.Null);
            var returningMembers = returningGroup.GetGroup();
            Assert.That(returningMembers.Count, Is.EqualTo(3));
            foreach (var member in returningMembers)
                Assert.That(member.Status[SoundwellOrkParty.ExperienceStatus].GetValue(),
                    Is.EqualTo(SoundwellOrkParty.RescueExperience),
                    "Revisiting the rescue must not lose or duplicate ORK experience.");
            string progressLine = SoundwellUiLocalization.Get("battle.rescuer") +
                " 1-3: 10 / 10 / 10";
            Assert.That(progressEntry.ReturnSummaryText, Does.Contain(progressLine),
                "The Bootstrap must show the actual ORK banked experience for all three rescuers.");
            SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
            progressEntry.RefreshReturnOutcome(true);
            Assert.That(progressEntry.ReturnSummaryText, Does.Contain(
                SoundwellUiLocalization.Get("battle.rescuer") + " 1-3: 10 / 10 / 10"),
                "A language change must not lose the persistent ORK progress report.");
            SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);

            LogAssert.Expect(UnityEngine.LogType.Log, "RPG_SOUNDWELL_ORK_BATTLE_VALIDATED");
            Debug.Log("RPG_SOUNDWELL_ORK_BATTLE_VALIDATED");
            LogAssert.NoUnexpectedReceived();
        }

        // Inspect only PUBLIC runtime data of the saved ORK ability on
        // failure. This keeps licensed ORK source out of CI logs and allows
        // configuration/eligibility faults to be distinguished before edits.
        // Inspect the saved ORK combatant state at the actual failing turn.
        // Reflection is read-only and never modifies an ORK action blocker.
        // Read-only ORK configuration snapshot. A selectable attack is not
        // sufficient: a real attack must define ORK target status changes.
        // This helps identify the missing public setting without copying
        // proprietary framework method bodies into CI logs.
        private static string DescribeConfigObject(object value)
        {
            if (value == null) return "null";
            const System.Reflection.BindingFlags flags =
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.Instance;
            var parts = value.GetType().GetFields(flags).Take(32).Select(field =>
            {
                object current = field.GetValue(value);
                if (current == null) return field.Name + "=null";
                if (current is System.Collections.ICollection items)
                    return field.Name + "=count:" + items.Count;
                if (current is string || current.GetType().IsPrimitive ||
                    current.GetType().IsEnum)
                    return field.Name + "=" + current;
                return field.Name + "=configured";
            });
            return value.GetType().Name + " {" + string.Join(", ", parts) + "}";
        }

        private static string DescribeActiveAttackSetup(object shortcut)
        {
            if (shortcut == null)
                return "no shortcut";
            const System.Reflection.BindingFlags flags =
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance;
            var get = shortcut.GetType().GetMethod("GetActiveLevel", flags);
            object active = null;
            try { active = get?.Invoke(shortcut, null); }
            catch (System.Exception error) { return "GetActiveLevel failed: " + error.GetType().Name; }
            if (active == null)
                return "no active level";
            var members = active.GetType().GetFields(flags)
                .Where(field => !field.IsStatic)
                .Select(field =>
                {
                    string kind = "unreadable";
                    try
                    {
                        object value = field.GetValue(active);
                        kind = value == null ? "null" :
                            value is System.Collections.ICollection collection
                                ? "count=" + collection.Count : "set";
                    }
                    catch (System.Exception error) { kind = error.GetType().Name; }
                    return field.Name + ":" + field.FieldType.Name + "=" + kind;
                }).Take(50);
            return active.GetType().Name + " {" + string.Join(", ", members) + "}";
        }

        private static string DescribeRuntimeActionBlockers(Combatant member, object attack)
        {
            const System.Reflection.BindingFlags flags =
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance;
            string ReadFlag(object owner, string name)
            {
                if (owner == null)
                    return "owner missing";
                var type = owner.GetType();
                try
                {
                    var property = type.GetProperty(name, flags);
                    if (property != null && property.GetIndexParameters().Length == 0)
                        return System.Convert.ToString(property.GetValue(owner));
                    var field = type.GetField(name, flags);
                    return field != null ? System.Convert.ToString(field.GetValue(owner))
                        : "not exposed";
                }
                catch (System.Exception error)
                {
                    return error.GetType().Name;
                }
            }
            string activeLevel = "not exposed";
            if (attack != null)
            {
                var method = attack.GetType().GetMethod("GetActiveLevel", flags);
                if (method != null && method.GetParameters().Length == 0)
                {
                    try { activeLevel = (method.Invoke(attack, null) != null).ToString(); }
                    catch (System.Exception error) { activeLevel = error.GetType().Name; }
                }
            }
            return "{BlockActionUse=" + ReadFlag(member.Actions, "BlockActionUse") +
                ", IsChoosing=" + ReadFlag(member.Actions, "IsChoosing") +
                ", InBattle=" + ReadFlag(member.Battle, "InBattle") +
                ", ActiveLevelExists=" + activeLevel + "}";
        }

        private static string DescribeBaseAttackState(object attack)
        {
            if (attack == null)
                return "null";
            const System.Reflection.BindingFlags flags =
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public;
            var type = attack.GetType();
            var properties = type.GetProperties(flags)
                .Where(property => property.GetIndexParameters().Length == 0 &&
                    (property.PropertyType.IsPrimitive || property.PropertyType.IsEnum ||
                     property.PropertyType == typeof(string)))
                .Take(32)
                .Select(property =>
                {
                    try { return property.Name + "=" + property.GetValue(attack); }
                    catch (System.Exception) { return property.Name + "=<unreadable>"; }
                });
            var fields = type.GetFields(flags)
                .Where(field => field.FieldType.IsPrimitive || field.FieldType.IsEnum ||
                    field.FieldType == typeof(string))
                .Take(32)
                .Select(field =>
                {
                    try { return field.Name + "=" + field.GetValue(attack); }
                    catch (System.Exception) { return field.Name + "=<unreadable>"; }
                });
            var usefulMethods = type.GetMethods(flags)
                .Where(method => method.Name.Contains("Can") || method.Name.Contains("Use") ||
                    method.Name.Contains("Ability") || method.Name.Contains("Attack"))
                .Select(method => method.ToString()).Distinct().Take(36);
            var useableMethod = type.GetMethods(flags).FirstOrDefault(method =>
                method.Name == "IsUseable" && method.GetParameters().Length == 1 &&
                method.GetParameters()[0].ParameterType.IsEnum);
            var useableStates = useableMethod == null ? "unavailable" :
                string.Join(", ", System.Enum.GetValues(useableMethod.GetParameters()[0].ParameterType)
                    .Cast<object>().Select(option =>
                    {
                        try { return option + "=" + useableMethod.Invoke(attack, new[] { option }); }
                        catch (System.Exception) { return option + "=<unreadable>"; }
                    }));
            return "type=" + type.FullName +
                " useableIn={" + useableStates + "}" +
                " scalarProperties={" + string.Join(", ", properties) +
                "} scalarFields={" + string.Join(", ", fields) +
                "} publicMethods={" + string.Join(", ", usefulMethods) + "}";
        }

        // Keep Unity component lookup outside the large UnityTest iterator.
        // No Unity Object reference is carried across the wait's yield before
        // its components are resolved. This also prevents a stale Bootstrap
        // object from masquerading as the live persisted Soundwell scene.
        private static bool TryFindLiveTrialOwners(
            out GameObject root, out SoundwellOrkParty party, out SoundwellTrial trial)
        {
            root = null;
            party = null;
            trial = null;
            Scene active = SceneManager.GetActiveScene();
            if (!active.IsValid() || !active.isLoaded ||
                active.name != SoundwellEntrance.SceneName)
                return false;
            foreach (GameObject candidate in active.GetRootGameObjects())
            {
                if (candidate == null || candidate.name != "Soundwell signal trial")
                    continue;
                SoundwellOrkParty liveParty = candidate.GetComponent<SoundwellOrkParty>();
                SoundwellTrial liveTrial = candidate.GetComponent<SoundwellTrial>();
                if (liveParty == null || liveTrial == null)
                    continue;
                root = candidate;
                party = liveParty;
                trial = liveTrial;
                return true;
            }
            return false;
        }

        [UnityTest, Order(20)]
        public IEnumerator BattleBackCancelEventRetreatsAndReleasesOrkParticipants()
        {
            EditorSceneManager.OpenScene("Assets/Game/Scenes/Bootstrap.unity", OpenSceneMode.Single);
            yield return new EnterPlayMode(expectDomainReload: true);
            yield return WaitForFrameworkInitialization();

            var entrance = Object.FindFirstObjectByType<SoundwellEntrance>();
            Assert.That(entrance != null, Is.True,
                "Battle retreat must enter through Bootstrap's single scene loader.");
            AsyncOperation load = entrance.OpenTrial();
            Assert.That(load, Is.Not.Null);
            while (!load.isDone)
                yield return null;

            GameObject root = null;
            float sceneDeadline = Time.realtimeSinceStartup + 8f;
            while (Time.realtimeSinceStartup < sceneDeadline)
            {
                if (SceneManager.GetActiveScene().name == SoundwellEntrance.SceneName)
                    root = GameObject.Find("Soundwell signal trial");
                if (root != null)
                    break;
                yield return null;
            }
            Assert.That(root != null, Is.True,
                "Battle retreat must enter the saved trial scene before reading its ORK party.");
            var party = root.GetComponent<SoundwellOrkParty>();
            var trial = root.GetComponent<SoundwellTrial>();
            Assert.That(party, Is.Not.Null);
            Assert.That(trial, Is.Not.Null);

            float deadline = Time.realtimeSinceStartup + 12f;
            while ((!party.IsReady || !trial.IsReady) &&
                   string.IsNullOrEmpty(party.InitializationError) &&
                   Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(party.IsReady && trial.IsReady, Is.True,
                "The saved ORK/Makinom rescue scene must initialize: " + party.InitializationError);

            trial.ReadInput = false;
            Rigidbody2D leaderBody = party.Leader.GetComponent<Rigidbody2D>();
            Assert.That(leaderBody, Is.Not.Null);
            leaderBody.position = new Vector2(-0.55f, 0f);
            Physics2D.SyncTransforms();
            Assert.That(trial.CanEngageBeast, Is.True);
            trial.ApplyInput(Vector2.zero, false, false, false, false, true);

            deadline = Time.realtimeSinceStartup + 8f;
            while (ORK.Game.Combatants.GetInBattle().Count < 4 &&
                   Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(ORK.Game.Combatants.GetInBattle().Count, Is.EqualTo(4));
            Assert.That(trial.DirectCombatStarted, Is.True);
            yield return null; // LateUpdate enters the battle-only action UI.

            var back = GameObject.Find("Soundwell Back button")?.GetComponent<Button>();
            var attack = GameObject.Find("Soundwell Attack button")?.GetComponent<Button>();
            Assert.That(back, Is.Not.Null);
            Assert.That(attack, Is.Not.Null);
            Assert.That(back.gameObject.activeInHierarchy, Is.True);
            Assert.That(back.interactable, Is.True,
                "Retreat must remain a reachable, legal battle action.");
            Assert.That(back.GetComponentInChildren<Text>().text,
                Is.EqualTo(SoundwellUiLocalization.Get("action.retreat")),
                "Battle Cancel is an explicit ORK encounter withdrawal, not an ambiguous Back.");
            // A newly legal attack becomes the default Submit action, so this
            // regression must not expect Retreat to hold focus forever.
            // Cancel must also work while the legal Attack button is focused.
            float chooseDeadline = Time.realtimeSinceStartup + 6f;
            while (!attack.interactable && Time.realtimeSinceStartup < chooseDeadline)
                yield return null;
            Assert.That(attack.interactable, Is.True,
                "The battle must expose a real ORK attack before testing Cancel.");
            EventSystem events = EventSystem.current;
            Assert.That(events, Is.Not.Null);
            Assert.That(events.currentSelectedGameObject, Is.SameAs(attack.gameObject),
                "A legal attack should be the default Submit focus, not an accidental retreat.");

            // The lead rescuer must show a distinct shield on a REAL ORK
            // guard submission, just as the two companions already do. This
            // tests the shipped battle button, not a manufactured defense flag.
            var guard = GameObject.Find("Soundwell Guard button")?.GetComponent<Button>();
            var leadPixel = GameObject.Find("PLACEHOLDER player silhouette")
                ?.GetComponent<SpriteRenderer>();
            Assert.That(guard, Is.Not.Null);
            Assert.That(leadPixel, Is.Not.Null);
            Assert.That(party.ChoosingRescuerIndex, Is.EqualTo(0));
            Assert.That(guard.interactable, Is.True,
                "The first ORK rescuer must have a legal Guard choice.");
            Sprite idleLeader = leadPixel.sprite;
            // Check the 16px leader step pose without spending an ORK turn
            // or modifying the formation used by this guard regression.
            var leaderViewField = typeof(SoundwellTrial).GetField("view",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic);
            Assert.That(leaderViewField, Is.Not.Null);
            object leaderView = leaderViewField.GetValue(trial);
            Assert.That(leaderView, Is.Not.Null);
            // SoundwellView is intentionally internal to the runtime asmdef:
            // exercise its live presentation through reflection from tests.
            var showLeaderStep = leaderView.GetType().GetMethod("AnimatePlayerBattleStep",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public);
            Assert.That(showLeaderStep, Is.Not.Null);
            Vector2 originalLeaderPosition = leaderBody.position;
            showLeaderStep.Invoke(leaderView, new object[] { Vector2.left });
            Assert.That(leadPixel.sprite.name, Does.Contain("leader walk"));
            Assert.That(leadPixel.sprite.rect.size, Is.EqualTo(new Vector2(16f, 16f)));
            Assert.That(leadPixel.sprite.pixelsPerUnit, Is.EqualTo(16f));
            Assert.That(leadPixel.sprite.texture.filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(leadPixel.flipX, Is.True);
            yield return null;
            Assert.That(leadPixel.sprite.name, Does.Contain("leader walk"),
                "Battle LateUpdate must not erase the accepted-step visual immediately.");
            Assert.That((Vector2)leaderBody.position, Is.EqualTo(originalLeaderPosition),
                "The presentation-only step pose must never move an ORK combatant.");
            yield return new WaitForSecondsRealtime(0.50f);
            Assert.That(leadPixel.sprite.name, Does.Not.Contain("leader walk"),
                "The leader battle step pose must expire on unscaled time.");
            guard.onClick.Invoke();
            Assert.That(leadPixel.sprite, Is.Not.SameAs(idleLeader),
                "An ORK-accepted leader Guard must change the actual actor silhouette.");
            Assert.That(leadPixel.sprite.name, Does.Contain("leader guard shield"));
            Assert.That(leadPixel.sprite.rect.size, Is.EqualTo(new Vector2(16f, 16f)));
            Assert.That(leadPixel.sprite.pixelsPerUnit, Is.EqualTo(16f));
            Assert.That(leadPixel.sprite.texture.filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(leadPixel.transform.localScale, Is.EqualTo(Vector3.one));
            yield return null;
            Assert.That(leadPixel.sprite.name, Does.Contain("leader guard shield"),
                "LateUpdate must not overwrite the accepted shield pose immediately.");
            // ORK can pause the scaled clock while other turns resolve. Guard
            // feedback is only a short pose: the leader cannot remain frozen
            // behind a shield after the accepted ORK turn has moved on.
            yield return new WaitForSecondsRealtime(0.95f);
            Assert.That(leadPixel.sprite, Is.Not.Null);
            Assert.That(leadPixel.sprite.name, Does.Not.Contain("leader guard shield"),
                "The accepted leader Guard pose must expire on unscaled time without " +
                "holding the character in a permanent shield animation.");
            deadline = Time.realtimeSinceStartup + 8f;
            while ((party.ChoosingRescuerIndex == 0 || !attack.interactable) &&
                   Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(party.ChoosingRescuerIndex, Is.InRange(1, 2),
                "Leader Guard must actually consume one ORK turn, not just draw a shield.");
            Assert.That(attack.interactable, Is.True);
            Assert.That(events.currentSelectedGameObject, Is.SameAs(attack.gameObject),
                "The next ORK chooser must keep a legal Submit target.");

            // A second rescuer can preview an eight-pixel step and cancel
            // without spending the movement allowance or primary action.
            int movingRescuer = party.ChoosingRescuerIndex;
            string stepLabel = SoundwellUiLocalization.Get("battle.move");
            Assert.That(party.BuildBattleReadout(), Does.Contain(stepLabel + " 1/1"),
                "The live chooser should see its one unspent ORK step in the HUD.");
            Vector2 beforeStep = party.Actors[movingRescuer].transform.position;
            Assert.That(party.CanStep(movingRescuer, Vector2.left), Is.True,
                "The westward step from the live guard follow-up must be reachable.");
            Assert.That(party.CanStep(movingRescuer, Vector2.one), Is.False,
                "A diagonal command cannot bypass the one-cardinal-step limit.");
            var moveButton = GameObject.Find("Soundwell Move button")?.GetComponent<Button>();
            // GameObject.Find omits inactive UGUI preview controls. They
            // exist in the same HUD before Move opens; resolve them through
            // their parent Transform so this assertion tests the real UI.
            var westButton = moveButton?.transform.parent
                .Find("Soundwell Move West button")?.GetComponent<Button>();
            var commitMove = moveButton?.transform.parent
                .Find("Soundwell Confirm Move button")?.GetComponent<Button>();
            var cancelMove = moveButton?.transform.parent
                .Find("Soundwell Cancel Move button")?.GetComponent<Button>();
            var stepPreview = GameObject.Find("PLACEHOLDER ORK step destination")
                ?.GetComponent<SpriteRenderer>();
            Assert.That(moveButton, Is.Not.Null);
            Assert.That(westButton, Is.Not.Null);
            Assert.That(commitMove, Is.Not.Null);
            Assert.That(cancelMove, Is.Not.Null);
            Assert.That(stepPreview, Is.Not.Null);
            Assert.That(moveButton.interactable, Is.True);
            Assert.That(stepPreview.enabled, Is.False);
            moveButton.onClick.Invoke();
            Assert.That(westButton.gameObject.activeInHierarchy, Is.True);
            Assert.That(commitMove.gameObject.activeInHierarchy, Is.True);
            Assert.That(cancelMove.gameObject.activeInHierarchy, Is.True);
            Assert.That(attack.gameObject.activeInHierarchy, Is.False,
                "Choosing a destination must hide the clickable ORK primary action.");
            Assert.That(guard.gameObject.activeInHierarchy, Is.False);
            Assert.That(back.gameObject.activeInHierarchy, Is.False,
                "Retreat is reached by Cancel after the destination preview closes.");
            Assert.That(moveButton.gameObject.activeInHierarchy, Is.False);
            // Focus navigation alone previews a different legal destination.
            // The first direction is preselected by the modal, but the
            // player must see the westward landing point before pressing Submit.
            events.SetSelectedGameObject(westButton.gameObject);
            yield return null;
            Assert.That(events.currentSelectedGameObject, Is.SameAs(westButton.gameObject));
            Assert.That(stepPreview.enabled, Is.True);
            SpriteRenderer selectedRescuer = party.Actors[movingRescuer]
                .GetComponent<SpriteRenderer>();
            Assert.That(selectedRescuer, Is.Not.Null);
            Assert.That(stepPreview.sprite, Is.SameAs(selectedRescuer.sprite),
                "The uncommitted landing preview must depict the actual ORK rescuer, " +
                "including the third rescuer's distinct hood, not a generic target arrow.");
            Assert.That(stepPreview.sprite.rect.size, Is.EqualTo(new Vector2(16f, 16f)));
            Assert.That(stepPreview.sprite.pixelsPerUnit, Is.EqualTo(16f));
            Assert.That(stepPreview.sprite.texture.filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(stepPreview.flipX, Is.True,
                "A westward landing preview must face west before the ORK step commits.");
            Assert.That((Vector2)stepPreview.transform.position,
                Is.EqualTo(beforeStep + Vector2.left * SoundwellOrkParty.BattleStepDistance),
                "Gamepad focus must preview a valid step without a first Submit.");
            Assert.That((Vector2)party.Actors[movingRescuer].transform.position, Is.EqualTo(beforeStep),
                "Inspecting a direction cannot move the live ORK combatant.");
            Assert.That(party.BuildBattleReadout(), Does.Contain(stepLabel + " 1/1"),
                "Focus-only movement preview cannot consume the ORK movement budget.");
            westButton.onClick.Invoke();
            Assert.That(stepPreview.enabled, Is.True);
            Assert.That((Vector2)stepPreview.transform.position,
                Is.EqualTo(beforeStep + Vector2.left * SoundwellOrkParty.BattleStepDistance));
            // The actual gamepad Cancel path must exit move preview only,
            // without retreating from ORK combat or spending this actor's step.
            // This guards focus on the hidden-confirmation UGUI overlay; a
            // direct button invocation would bypass the InputSystemUIInputModule.
            Gamepad previewCancelPad = InputSystem.AddDevice<Gamepad>();
            try
            {
                InputSystem.QueueStateEvent(previewCancelPad,
                    new GamepadState().WithButton(GamepadButton.East));
                deadline = Time.realtimeSinceStartup + 2f;
                while (stepPreview.enabled && Time.realtimeSinceStartup < deadline)
                    yield return null;
                Assert.That(stepPreview.enabled, Is.False,
                    "Gamepad B must close the focused move preview.");
                Assert.That(trial.DirectCombatStarted, Is.True,
                    "Cancel during move preview must not retreat from the ORK encounter.");
            }
            finally
            {
                InputSystem.RemoveDevice(previewCancelPad);
            }
            Assert.That(party.ChoosingRescuerIndex, Is.EqualTo(movingRescuer));
            Assert.That(attack.gameObject.activeInHierarchy, Is.True);
            Assert.That(guard.gameObject.activeInHierarchy, Is.True);
            Assert.That(back.gameObject.activeInHierarchy, Is.True);
            Assert.That(moveButton.gameObject.activeInHierarchy, Is.True);
            Assert.That(events.currentSelectedGameObject, Is.SameAs(moveButton.gameObject),
                "Cancel returns Submit to a live Move target without spending the step.");
            Assert.That((Vector2)party.Actors[movingRescuer].transform.position,
                Is.EqualTo(beforeStep),
                "Cancel cannot secretly move the ORK actor or spend its turn.");
            Assert.That(party.BuildBattleReadout(), Does.Contain(stepLabel + " 1/1"),
                "Cancelling the preview must leave the visible movement budget untouched.");

            // A collider appearing along the sweep must invalidate the step
            // without moving the chooser or spending its allowance.
            var obstruction = new GameObject("Step sweep regression obstruction");
            // The destination was valid when UGUI presented it. A physical
            // blocker appearing between preview and Confirm must be rejected
            // immediately, before LateUpdate refreshes the legal directions.
            moveButton.onClick.Invoke();
            westButton.onClick.Invoke();
            Assert.That(stepPreview.enabled, Is.True);
            obstruction.transform.position = beforeStep + Vector2.left * 0.30f;
            obstruction.AddComponent<BoxCollider2D>().size = Vector2.one * 0.10f;
            Physics2D.SyncTransforms();
            Assert.That(party.CanStep(movingRescuer, Vector2.left), Is.False);
            Assert.That(commitMove.interactable, Is.True,
                "The stale preview is deliberately tested before its next UI refresh.");
            commitMove.onClick.Invoke();
            Assert.That(stepPreview.enabled, Is.False,
                "Rejected movement must immediately remove its now-unsafe world marker.");
            Assert.That(commitMove.gameObject.activeInHierarchy, Is.False,
                "Rejected movement must close the stale Confirm overlay.");
            Assert.That((Vector2)party.Actors[movingRescuer].transform.position, Is.EqualTo(beforeStep));
            Assert.That(party.ChoosingRescuerIndex, Is.EqualTo(movingRescuer));
            Assert.That(attack.interactable, Is.True,
                "Rejected step cannot spend the current ORK primary action.");
            // Both the ORK action adapter and the actual UGUI direction
            // must reject a new collider across the swept step. A failed
            // confirmation cannot spend a turn or consume the movement budget.
            yield return null; // Publish the physics change to the live HUD.
            Assert.That(party.CanStep(movingRescuer, Vector2.left), Is.False);
            Assert.That(party.TryStep(movingRescuer, Vector2.left), Is.False,
                "A blocked move must fail even if gameplay calls TryStep directly.");
            Assert.That((Vector2)party.Actors[movingRescuer].transform.position,
                Is.EqualTo(beforeStep), "A blocked step cannot teleport the ORK actor.");
            Assert.That(party.BuildBattleReadout(), Does.Contain(stepLabel + " 1/1"),
                "Physics-rejected movement must not silently consume the displayed allowance.");
            moveButton.onClick.Invoke();
            Assert.That(westButton.interactable, Is.False,
                "The live movement menu must disable directions blocked by geometry.");
            cancelMove.onClick.Invoke();
            Object.Destroy(obstruction);
            yield return null;
            Assert.That(party.CanStep(movingRescuer, Vector2.left), Is.True,
                "Removing the obstruction must restore the unspent step.");

            moveButton.onClick.Invoke();
            westButton.onClick.Invoke();
            Assert.That(commitMove.interactable, Is.True);
            commitMove.onClick.Invoke();
            Assert.That(attack.gameObject.activeInHierarchy, Is.True);
            Assert.That(moveButton.gameObject.activeInHierarchy, Is.True);
            Assert.That(moveButton.interactable, Is.False);
            Assert.That(events.currentSelectedGameObject, Is.SameAs(attack.gameObject),
                "A confirmed step must immediately restore the still-legal ORK primary action.");
            yield return null;
            Assert.That(party.ChoosingRescuerIndex, Is.EqualTo(movingRescuer),
                "The confirmed step preserves the same rescuer's primary action.");
            Assert.That((Vector2)party.Actors[movingRescuer].transform.position,
                Is.EqualTo(beforeStep + Vector2.left * SoundwellOrkParty.BattleStepDistance),
                "The real actor must arrive at the previewed position.");
            var stepActorSprite = party.Actors[movingRescuer].GetComponent<SpriteRenderer>();
            Assert.That(stepActorSprite, Is.Not.Null);
            Assert.That(stepActorSprite.sprite.name, Does.Contain("walking"),
                "A legal companion ORK step must show the existing 16px walk pose, not teleport in idle.");
            Assert.That(stepActorSprite.sprite.rect.size, Is.EqualTo(new Vector2(16f, 16f)));
            Assert.That(stepActorSprite.sprite.pixelsPerUnit, Is.EqualTo(16f));
            Assert.That(stepActorSprite.sprite.texture.filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(stepActorSprite.flipX, Is.True,
                "The visual step must face west with the actual ORK movement.");
            yield return new WaitForSecondsRealtime(0.46f);
            Assert.That(stepActorSprite.sprite.name, Does.Not.Contain("walking"),
                "Movement feedback must expire without freezing the actor in the walk frame.");
            Assert.That(party.ChoosingRescuerIndex, Is.EqualTo(movingRescuer));
            Assert.That(party.CanStep(movingRescuer, Vector2.left), Is.False,
                "The same ORK turn cannot spend a second step.");
            Assert.That(party.BuildBattleReadout(), Does.Contain(stepLabel + " 0/1"),
                "After an accepted ORK step the HUD must immediately show no movement left.");
            Assert.That(moveButton.interactable, Is.False);
            Assert.That(attack.interactable, Is.True);
            Assert.That(trial.GateOpen, Is.False);
            Assert.That(events.currentSelectedGameObject, Is.SameAs(attack.gameObject));

            // Feed Gamepad B through the live input module. The selected
            // Attack button must receive Cancel without a hierarchy dispatch.
            Gamepad gamepad = InputSystem.AddDevice<Gamepad>();
            try
            {
                InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.East));
                deadline = Time.realtimeSinceStartup + 2f;
                while (trial != null && trial.DirectCombatStarted && Time.realtimeSinceStartup < deadline)
                    yield return null;
                Assert.That(trial == null || !trial.DirectCombatStarted, Is.True,
                    "Gamepad B on the focused Attack must start a real ORK retreat.");
            }
            finally
            {
                InputSystem.RemoveDevice(gamepad);
            }

            deadline = Time.realtimeSinceStartup + 8f;
            while (SceneManager.GetActiveScene().name != SoundwellEntrance.BootstrapSceneName &&
                   Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(SceneManager.GetActiveScene().name,
                Is.EqualTo(SoundwellEntrance.BootstrapSceneName));
            Assert.That(ORK.Game.Combatants.GetInBattle(), Is.Empty,
                "Cancel retreat must release the ORK global battle registry.");
        }

        [UnityTest, Order(30)]
        public IEnumerator BattleStepChangesGateAssignmentFromSameOpening()
        {
            EditorSceneManager.OpenScene("Assets/Game/Scenes/Bootstrap.unity", OpenSceneMode.Single);
            yield return new EnterPlayMode(expectDomainReload: true);
            yield return WaitForFrameworkInitialization();

            var opening = new Vector2[5];
            var openingHealth = new float[4];
            yield return CompareGateAssignment(false, opening, openingHealth);
            yield return CompareGateAssignment(true, opening, openingHealth);
        }

        private static IEnumerator CompareGateAssignment(bool useStep, Vector2[] opening, float[] openingHealth)
        {
            var entrance = Object.FindFirstObjectByType<SoundwellEntrance>();
            Assert.That(entrance, Is.Not.Null);
            AsyncOperation load = entrance.OpenTrial();
            Assert.That(load, Is.Not.Null);
            float deadline = Time.realtimeSinceStartup + 12f;
            while (!load.isDone && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(load.isDone, Is.True, "The comparison must enter a fresh Soundwell scene.");

            SoundwellOrkParty party = null;
            SoundwellTrial trial = null;
            deadline = Time.realtimeSinceStartup + 12f;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (TryFindLiveTrialOwners(out _, out party, out trial) && party.IsReady && trial.IsReady)
                    break;
                yield return null;
            }
            Assert.That(party != null && party.IsReady && trial != null && trial.IsReady, Is.True,
                "Both variants must initialize the same live rescue scene.");
            trial.ReadInput = false;

            // Approach the gate at the same point in each run. Followers settle
            // through the normal exploration formation before ORK starts.
            var start = new Vector2(-0.90f, 0f);
            var leaderBody = party.Leader.GetComponent<Rigidbody2D>();
            leaderBody.position = start;
            Physics2D.SyncTransforms();
            deadline = Time.realtimeSinceStartup + 6f;
            while (party.Actors.Skip(1).Any(actor => actor.transform.position.x < -1.36f) &&
                   Time.realtimeSinceStartup < deadline)
                yield return null;
            // Allow LateUpdate to finish the last fraction of the formation move.
            yield return null;
            Assert.That(trial.CanEngageBeast, Is.True);
            trial.ApplyInput(Vector2.zero, false, false, false, false, true);
            Assert.That(trial.DirectCombatStarted, Is.True,
                "Both visits must start a new ORK encounter through the gate.");

            deadline = Time.realtimeSinceStartup + 8f;
            while (party.ChoosingRescuerIndex < 0 && Time.realtimeSinceStartup < deadline)
                yield return null;
            yield return null;
            Assert.That(party.ChoosingRescuerIndex, Is.EqualTo(0));
            Assert.That(trial.GateOpen, Is.False);
            Assert.That(trial.RescueComplete, Is.False);

            Vector2[] positions = party.Actors.Select(actor => (Vector2)actor.transform.position)
                .Concat(new[] { trial.BeastPosition, trial.BellPosition }).ToArray();
            float[] health = party.Members.Select(member => (float)member.Status[1].GetValue())
                .Append((float)party.Beast.Status[1].GetValue()).ToArray();
            if (useStep)
            {
                for (int i = 0; i < positions.Length; i++)
                    Assert.That(Vector2.Distance(positions[i], opening[i]), Is.LessThan(0.015f),
                        "The formation, beast and bell must start in the same places.");
                Assert.That(health, Is.EqualTo(openingHealth));
            }
            else
            {
                positions.CopyTo(opening, 0);
                health.CopyTo(openingHealth, 0);
            }

            var gate = GameObject.Find("Soundwell Battle Gate button").GetComponent<Button>();
            var guard = GameObject.Find("Soundwell Guard button").GetComponent<Button>();
            var attack = GameObject.Find("Soundwell Attack button").GetComponent<Button>();
            var gateCue = GameObject.Find("PLACEHOLDER ORK legal gate target")
                ?.GetComponent<SpriteRenderer>();
            var gateControl = GameObject.Find("PLACEHOLDER gate control");
            Assert.That(gateCue, Is.Not.Null);
            Assert.That(gateControl, Is.Not.Null);
            Assert.That(gateCue.transform.IsChildOf(gateControl.transform), Is.True,
                "The legal-interaction cue must remain attached to the physical gate control.");
            Assert.That(gateCue.sprite.rect.size, Is.EqualTo(new Vector2(16f, 16f)));
            Assert.That(gateCue.sprite.pixelsPerUnit, Is.EqualTo(16f));
            Assert.That(gateCue.sprite.texture.filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(gateCue.transform.lossyScale.x, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(gateCue.transform.lossyScale.y, Is.EqualTo(1f).Within(0.0001f),
                "The physical gate's nonuniform scale must not distort a 16px icon.");
            Assert.That(Vector2.Distance(gateCue.transform.position,
                gateControl.transform.position + Vector3.up * 0.875f),
                Is.LessThan(0.0001f),
                "The pixel-safe target must still track the real gate control.");
            Assert.That(gate.interactable && guard.interactable, Is.True);
            Assert.That(gateCue.enabled, Is.True,
                "A live ORK chooser in reach must visibly mark the real gate.");
            (useStep ? guard : gate).onClick.Invoke();

            yield return WaitForNextGateComparisonTurn(party, 0);
            int companion = party.ChoosingRescuerIndex;
            Assert.That(companion, Is.InRange(1, 2));
            if (useStep)
            {
                Assert.That(trial.GateOpen, Is.False);
                Assert.That(party.TryStep(0, Vector2.right), Is.False,
                    "A confirmation from the previous chooser cannot move the current one.");
                Assert.That(gate.interactable, Is.False,
                    "The companion's fixed position cannot reach the closed gate.");
                Assert.That(gateCue.enabled, Is.False,
                    "The gate marker must disappear when the current ORK actor is out of reach.");
                Assert.That(party.CanStep(companion, Vector2.right), Is.True);
                var move = GameObject.Find("Soundwell Move button").GetComponent<Button>();
                Assert.That(move.interactable, Is.True);
                move.onClick.Invoke();
                var east = move.transform.parent.Find("Soundwell Move East button").GetComponent<Button>();
                var confirm = move.transform.parent.Find("Soundwell Confirm Move button").GetComponent<Button>();
                Assert.That(east.interactable, Is.True);
                east.onClick.Invoke();
                Assert.That(confirm.interactable, Is.True);
                SoundwellSmokeHost.Capture(trial, "17-ork-step-gate-preview");
                confirm.onClick.Invoke();
                yield return null;
                Assert.That(party.ChoosingRescuerIndex, Is.EqualTo(companion));
                Assert.That(gate.interactable, Is.True,
                    "The same companion can now use its primary action on the gate.");
                Assert.That(gateCue.enabled, Is.True,
                    "After the accepted ORK step, the physical gate must be marked as usable.");
                SoundwellSmokeHost.Capture(trial, "18-ork-step-companion-gate");
                gate.onClick.Invoke();
            }
            else
            {
                Assert.That(guard.interactable, Is.True);
                guard.onClick.Invoke();
            }

            yield return WaitForNextGateComparisonTurn(party, companion);
            yield return null; // Publish the resolved gate and live ORK menu.
            Assert.That(gateCue.enabled, Is.False,
                "A resolved gate action or an out-of-range chooser must clear its target cue.");
            Assert.That(party.ChoosingRescuerIndex, Is.EqualTo(3 - companion));
            Assert.That(attack.interactable, Is.True);
            attack.onClick.Invoke();
            deadline = Time.realtimeSinceStartup + 8f;
            while (!trial.DirectCombatComplete && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(trial.DirectCombatComplete, Is.True,
                "Both assignments must win after the same three primary actions.");
            // ORK can present the next choice while an earlier action is queued.
            // Inspect the latch after real action resolution, not during selection.
            Assert.That(trial.GateOpen, Is.True,
                "The assigned rescuer's queued action must open the actual gate.");
            Assert.That(trial.RescueComplete, Is.False,
                "The survivor still needs an escort after either gate assignment.");

            string result = useStep
                ? $"RPG_SOUNDWELL_GATE_COMPARISON:STEP:gate={companion + 1}:guard=1:attack={4 - companion}:primary=3:steps=1"
                : $"RPG_SOUNDWELL_GATE_COMPARISON:FIXED:gate=1:guard={companion + 1}:attack={4 - companion}:primary=3:steps=0";
            LogAssert.Expect(UnityEngine.LogType.Log, result);
            Debug.Log(result);

            AsyncOperation returnLoad = trial.ReturnToBootstrap();
            Assert.That(returnLoad, Is.Not.Null);
            deadline = Time.realtimeSinceStartup + 12f;
            while ((!returnLoad.isDone ||
                    SceneManager.GetActiveScene().name != SoundwellEntrance.BootstrapSceneName) &&
                   Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(returnLoad.isDone, Is.True, "The first route must finish loading Bootstrap before replay.");
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(SoundwellEntrance.BootstrapSceneName));
            Assert.That(ORK.Game.Combatants.GetInBattle(), Is.Empty);
            Assert.That(ORK.Control.InBattle, Is.False,
                "Returning after victory must release ORK's battle control before replay.");
            yield return null; // Let the new Bootstrap entry initialize before opening the next trial.
        }

        private static IEnumerator WaitForNextGateComparisonTurn(SoundwellOrkParty party, int previous)
        {
            float deadline = Time.realtimeSinceStartup + 8f;
            while ((party.ChoosingRescuerIndex < 0 || party.ChoosingRescuerIndex == previous) &&
                   Time.realtimeSinceStartup < deadline)
                yield return null;
            yield return null;
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
