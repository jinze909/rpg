using System.Collections;
using GamingIsLove.Makinom;
using GamingIsLove.ORKFramework;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Rpg.Tests.Editor
{
    // Verify the non-combat lure independently of the pending ORK Attack work.
    // The visible target and search countdown must follow the actual beast AI.
    public sealed class SoundwellThreatPreviewTests
    {
        [UnityTest]
        public IEnumerator SideBellLureDisplaysRealPursuitAndExpiresAfterWrapping()
        {
            EditorSceneManager.OpenScene("Assets/Game/Scenes/Bootstrap.unity", OpenSceneMode.Single);
            yield return new EnterPlayMode(expectDomainReload: true);

            float deadline = Time.realtimeSinceStartup + 30f;
            while ((!Maki.Initialized || !ORK.Initialized) &&
                   Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(Maki.Initialized && ORK.Initialized, Is.True,
                "The saved Bootstrap must initialize both framework owners.");

            var entrance = Object.FindFirstObjectByType<SoundwellEntrance>();
            Assert.That(entrance, Is.Not.Null);
            AsyncOperation loading = entrance.OpenTrial();
            Assert.That(loading, Is.Not.Null);
            while (!loading.isDone && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(loading.isDone, Is.True, "The saved trial scene must load.");

            var root = GameObject.Find("Soundwell signal trial");
            Assert.That(root, Is.Not.Null);
            var trial = root.GetComponent<SoundwellTrial>();
            Assert.That(trial, Is.Not.Null);
            deadline = Time.realtimeSinceStartup + 12f;
            while (!trial.IsReady && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(trial.IsReady, Is.True);
            trial.ReadInput = false;

            // Both named source positions must be visually distinguishable
            // authored pixel objects, not smooth-scaled rectangles. The
            // visuals do not move the bell or add any alternate sound target.
            var entranceStand = GameObject.Find("PLACEHOLDER entrance bell stand")
                ?.GetComponent<SpriteRenderer>();
            var sideStand = GameObject.Find("PLACEHOLDER side bell stand")
                ?.GetComponent<SpriteRenderer>();
            Assert.That(entranceStand, Is.Not.Null);
            Assert.That(sideStand, Is.Not.Null);
            Assert.That(entranceStand.sprite, Is.SameAs(sideStand.sprite));
            Assert.That(entranceStand.sprite.texture.width, Is.EqualTo(16));
            Assert.That(entranceStand.sprite.texture.height, Is.EqualTo(16));
            Assert.That(entranceStand.sprite.texture.filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(entranceStand.sprite.pixelsPerUnit, Is.EqualTo(16f));
            Assert.That(entranceStand.transform.localScale, Is.EqualTo(Vector3.one));
            Assert.That(sideStand.transform.localScale, Is.EqualTo(Vector3.one));
            Assert.That((Vector2)sideStand.transform.position,
                Is.EqualTo(SoundwellTrial.SideBellStand));
            Assert.That(entranceStand.GetComponent<Collider2D>(), Is.Null);
            Assert.That(sideStand.GetComponent<Collider2D>(), Is.Null);
            Assert.That(entranceStand.color, Is.Not.EqualTo(sideStand.color),
                "The two true source positions need distinct readable pixel colors.");

            // UX-001: visiting the locked gate alone cannot reveal the side
            // bell stand solution. The optional claw evidence offers the
            // precise clue only after the player chooses to inspect it.
            // This never gates the already-verified clue-free bell-only rescue.
            var hint = GameObject.Find("Soundwell trial hint")?.GetComponent<Text>();
            Assert.That(hint, Is.Not.Null);
            var clueBody = trial.PlayerActor.GetComponent<Rigidbody2D>();
            Assert.That(clueBody, Is.Not.Null);
            Vector2 startingPosition = clueBody.position;
            Vector2 startingBellPosition = trial.BellPosition;
            Assert.That(trial.ClawsObserved, Is.False);
            clueBody.position = new Vector2(-0.55f, 0f);
            Physics2D.SyncTransforms();
            yield return null;
            Assert.That(trial.AtGatePanel, Is.True);
            Assert.That(hint.text, Does.Not.Contain("SIDE BELL STAND"));
            Assert.That(hint.text, Does.Not.Contain("SIDE STAND"),
                "Approaching the gate must not hand out the complete lure solution.");

            clueBody.position = SoundwellTrial.ClawEvidencePosition;
            Physics2D.SyncTransforms();
            Assert.That(trial.CanInspectClaws, Is.True);
            trial.ApplyInput(Vector2.zero, false, false, false, false, true);
            Assert.That(trial.ClawsObserved, Is.True,
                "The precise hint requires inspecting the real clue.");
            clueBody.position = new Vector2(-0.55f, 0f);
            Physics2D.SyncTransforms();
            yield return null;
            Assert.That(hint.text, Does.Contain("SIDE BELL STAND").Or.Contain("SIDE STAND"),
                "The physical claw clue must unlock the specific side-stand advice.");
            clueBody.position = startingPosition;
            Physics2D.SyncTransforms();
            yield return null;

            // A strict 16px leader must face its actual travel direction;
            // moving west may not look like backpedalling. This changes only
            // SpriteRenderer.flipX, never the ORK actor/physics direction.
            var playerSprite = GameObject.Find("PLACEHOLDER player silhouette")
                ?.GetComponent<SpriteRenderer>();
            Assert.That(playerSprite, Is.Not.Null);
            trial.ApplyInput(Vector2.right, false, false, false);
            yield return null;
            Assert.That(playerSprite.flipX, Is.False,
                "Walking east must face east on the actual player sprite.");
            trial.ApplyInput(Vector2.left, false, false, false);
            yield return null;
            Assert.That(playerSprite.flipX, Is.True,
                "Walking west must flip the player sprite, not the actor.");
            trial.ApplyInput(Vector2.up, false, false, false);
            yield return null;
            Assert.That(playerSprite.flipX, Is.True,
                "Walking vertically must retain the last horizontal facing.");
            trial.ApplyInput(Vector2.zero, false, false, false);
            yield return null;
            Assert.That(playerSprite.flipX, Is.True,
                "Stopping must not snap the player back east.");
            trial.ApplyInput(Vector2.zero, false, false, true);
            yield return null;
            Assert.That(playerSprite.flipX, Is.False,
                "Retry must restore the deterministic starting facing.");

            var body = trial.PlayerActor.GetComponent<Rigidbody2D>();
            Assert.That(body, Is.Not.Null);
            // Use actual carry/drop controls, not a manufactured Makinom flag.
            body.position = trial.BellPosition;
            Physics2D.SyncTransforms();
            trial.ApplyInput(Vector2.zero, false, false, false, true);
            Assert.That(trial.IsCarrying, Is.True);
            // Carry cannot place a third bell sound source into arbitrary
            // geometry. The same physical bell follows the carrier; only a
            // marked stand permits Place.
            body.position = new Vector2(-4f, 0f);
            Physics2D.SyncTransforms();
            Assert.That(trial.CanPlaceBell, Is.False);
            trial.ApplyInput(Vector2.zero, false, false, false, true);
            Assert.That(trial.IsCarrying, Is.True,
                "An invalid off-stand drop must leave the actual bell carried.");
            yield return null;
            Assert.That(Vector2.Distance(trial.BellPosition,
                body.position + new Vector2(0.35f, 0f)), Is.LessThan(0.05f),
                "The carried bell must follow the player after a blocked drop.");
            body.position = SoundwellTrial.SideBellStand;
            Physics2D.SyncTransforms();
            Assert.That(trial.CanPlaceBell, Is.True);
            trial.ApplyInput(Vector2.zero, false, false, false, true);
            Assert.That(trial.IsCarrying, Is.False);
            Assert.That(trial.BellPosition, Is.EqualTo(SoundwellTrial.SideBellStand));

            var intent = GameObject.Find("PLACEHOLDER beast intent marker")
                ?.GetComponent<SpriteRenderer>();
            var countdown = GameObject.Find("PLACEHOLDER beast search timer fill")
                ?.GetComponent<SpriteRenderer>();
            Assert.That(intent, Is.Not.Null);
            Assert.That(countdown, Is.Not.Null);
            Assert.That(intent.enabled, Is.False);
            Assert.That(countdown.enabled, Is.False);

            // The marker is the same world target the beast chases, not a
            // separate guessed point or a permanently highlighted hint.
            var lureTarget = new Vector2(1.15f, trial.BellPosition.y);
            float beforeDistance = Vector2.Distance(trial.BeastPosition, lureTarget);
            Assert.That(beforeDistance, Is.GreaterThan(0.1f));
            trial.ApplyInput(Vector2.zero, true, false, false);
            Assert.That(trial.HasBellTarget, Is.True);
            // The physical lure must use its single routed SFX source. A
            // continuous audible strike may loop, but the wrapped bell must
            // never leave that loud loop playing underneath the rescue.
            var bellAudio = System.Array.Find(
                Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None),
                source => source.clip != null && source.clip.name == "Copper bell");
            Assert.That(bellAudio, Is.Not.Null,
                "Ringing the live bell must produce the audible copper-bell clip.");
            Assert.That(bellAudio.loop, Is.True);
            Assert.That(bellAudio.outputAudioMixerGroup, Is.Not.Null);
            Assert.That(bellAudio.outputAudioMixerGroup.name, Is.EqualTo("SFX"));
            yield return new WaitForSeconds(0.2f);
            Assert.That(intent.enabled, Is.True);
            Assert.That(Vector2.Distance((Vector2)intent.transform.position, lureTarget),
                Is.LessThan(0.001f), "The on-screen marker must point to the actual lure.");
            Assert.That(Vector2.Distance(trial.BeastPosition, lureTarget),
                Is.LessThan(beforeDistance), "The real beast must advance toward the displayed lure.");

            // Wrapping does not immediately erase the consequence: the beast
            // searches around the last heard position for its real timer.
            trial.ApplyInput(Vector2.zero, false, true, false);
            Assert.That(trial.IsSearching, Is.True);
            Assert.That(bellAudio.clip, Is.Not.Null);
            Assert.That(bellAudio.clip.name, Is.EqualTo("Wrapped bell"),
                "Wrapping must replace the audible loop with the short muted cue.");
            Assert.That(bellAudio.loop, Is.False,
                "The muted cue must not continue looping while the beast searches.");
            yield return new WaitForSeconds(0.2f);
            Assert.That(intent.enabled && countdown.enabled, Is.True);
            float initialWidth = countdown.transform.localScale.x;
            yield return new WaitForSeconds(0.5f);
            Assert.That(countdown.transform.localScale.x, Is.LessThan(initialWidth),
                "The drawn countdown must shrink with the real search clock.");
            // A fixed WaitForSeconds can resume on the same player-loop
            // update as the final countdown frame. Give the real scene's
            // LateUpdate a bounded chance to expire the actual Makinom state,
            // but fail if the search never ends (rather than faking completion).
            float expiryDeadline = Time.realtimeSinceStartup +
                SoundwellTrial.SearchDurationSeconds + 5f;
            while (trial.IsSearching && Time.realtimeSinceStartup < expiryDeadline)
                yield return null;
            Assert.That(trial.IsSearching, Is.False,
                "The real beast must stop searching after the bell is wrapped; " +
                "remaining fraction: " + trial.SearchRemainingFraction);
            yield return null; // UI refresh consumes the resolved Makinom state.
            Assert.That(trial.IsWrapped, Is.True);
            Assert.That(intent.enabled, Is.False,
                "The beast must stop advertising an expired search target.");
            Assert.That(countdown.enabled, Is.False,
                "The search timer must disappear when the same AI clock expires.");

            // A silent, wrapped bell cannot keep an expired lure alive or
            // unlock the gate merely because the beast remains near that lane.
            body.position = new Vector2(-0.55f, 0f);
            Physics2D.SyncTransforms();
            Assert.That(trial.AtGatePanel, Is.True);
            Assert.That(trial.CanOpenGate, Is.False,
                "A completed search must not count as an active side-bell lure.");

            // Repeatedly trying the wrapped bell must remain silent. The
            // player must deliberately unwrap and ring it to rearm the lure.
            body.position = SoundwellTrial.SideBellStand;
            Physics2D.SyncTransforms();
            trial.ApplyInput(Vector2.zero, true, false, false);
            yield return null;
            Assert.That(trial.IsWrapped, Is.True,
                "Ringing while wrapped must not silently discard the cloth.");
            Assert.That(trial.HasBellTarget, Is.False,
                "A muted strike must not re-create a pursuit target.");
            Assert.That(trial.IsSearching, Is.False);
            Assert.That(intent.enabled, Is.False,
                "The intent marker must reflect the actual muted AI state.");
            Assert.That(countdown.enabled, Is.False);

            trial.ApplyInput(Vector2.zero, false, true, false);
            Assert.That(trial.IsWrapped, Is.False,
                "Removing the cloth must restore a ready, not already-ringing bell.");
            Assert.That(trial.HasBellTarget, Is.False);
            Assert.That(bellAudio.clip, Is.Null,
                "Unwrapping must stop and clear the previous muffled cue.");
            Assert.That(bellAudio.isPlaying, Is.False);
            trial.ApplyInput(Vector2.zero, true, false, false);
            yield return null;
            Assert.That(trial.HasBellTarget, Is.True,
                "A fresh audible strike must resume the real lure.");
            Assert.That(bellAudio.clip, Is.Not.Null);
            Assert.That(bellAudio.clip.name, Is.EqualTo("Copper bell"));
            Assert.That(bellAudio.loop, Is.True,
                "Rearming the same bell must restore its audible continuous signal.");
            Assert.That(intent.enabled, Is.True,
                "The resumed pursuit must once again have a visible target.");
            Assert.That(Vector2.Distance((Vector2)intent.transform.position, lureTarget),
                Is.LessThan(0.001f),
                "A resumed lure must target the same physical side-bell location.");

            // The rearmed lure should create a real, bounded opening, not
            // only a green UI hint. Keep the player at the saved west-side
            // panel and wait for the actual beast to clear the escape lane.
            // Both the decision and the collider belong to the live scene.
            var gateBlocker = GameObject.Find("PLACEHOLDER soundwell gate")
                ?.GetComponent<BoxCollider2D>();
            Assert.That(gateBlocker, Is.Not.Null,
                "The saved soundwell gate must still own the physical blocker.");
            Assert.That(gateBlocker.enabled, Is.True,
                "The gate must remain solid before the player opens it.");
            body.position = new Vector2(-0.55f, 0f);
            Physics2D.SyncTransforms();
            Assert.That(trial.AtGatePanel, Is.True);
            Assert.That(trial.GateOpen, Is.False);
            float gateDeadline = Time.realtimeSinceStartup + 8f;
            while (!trial.CanOpenGate && Time.realtimeSinceStartup < gateDeadline)
                yield return null;
            Assert.That(trial.CanOpenGate, Is.True,
                "An audible side lure must lead the real beast below the gate-lane threshold.");
            Assert.That(trial.BeastPosition.y, Is.LessThan(-0.95f),
                "The opening condition must reflect the beast's actual position.");
            trial.ApplyInput(Vector2.zero, false, false, false, false, true);
            Assert.That(trial.GateOpen, Is.True,
                "The player's real gate action must open the escape route.");
            yield return null;
            Assert.That(gateBlocker.enabled, Is.False,
                "The opened gate must physically release its collider, not merely change UI copy.");

            // Taking the already-ringing bell is not a new sound source:
            // the physical lift must silence the continuous SFX and clear the
            // beast's live pursuit target. Verify the game-owned input route,
            // not a direct Makinom signal or AudioSource mutation in the test.
            body.position = SoundwellTrial.SideBellStand;
            Physics2D.SyncTransforms();
            Assert.That(trial.HasBellTarget, Is.True);
            Assert.That(bellAudio.loop, Is.True);
            trial.ApplyInput(Vector2.zero, false, false, false, true);
            Assert.That(trial.IsCarrying, Is.True);
            Assert.That(trial.HasBellTarget, Is.False,
                "Lifting a ringing bell must not leave an invisible monster lure.");
            Assert.That(trial.IsSearching, Is.False,
                "Lifting the bell must not fabricate a wrapped-bell search.");
            Assert.That(bellAudio.isPlaying, Is.False,
                "Carried bell must stop the actual continuous SFX.");
            Assert.That(bellAudio.loop, Is.False);
            Assert.That(bellAudio.clip, Is.Null);
            yield return null; // Let the visual read the new Makinom signal.
            Assert.That(intent.enabled, Is.False,
                "The world-space beast target must disappear with the live lure.");
            Assert.That(countdown.enabled, Is.False);
            Assert.That(trial.GateOpen, Is.True,
                "Lifting the bell must not undo a gate the player already opened.");

            // Reuse the same lifted bell to recreate a REAL wrap/search state.
            // Picking it up during the beast's search must end both the
            // countdown and wrapped-bell audio, not merely clear pursuit.
            // The already-open physical gate remains open throughout.
            Assert.That(trial.CanPlaceBell, Is.True);
            trial.ApplyInput(Vector2.zero, false, false, false, true);
            Assert.That(trial.IsCarrying, Is.False);
            Assert.That(trial.BellPosition, Is.EqualTo(SoundwellTrial.SideBellStand));
            trial.ApplyInput(Vector2.zero, true, false, false);
            Assert.That(trial.HasBellTarget, Is.True);
            trial.ApplyInput(Vector2.zero, false, true, false);
            Assert.That(trial.IsSearching, Is.True);
            yield return null;
            Assert.That(intent.enabled, Is.True);
            Assert.That(countdown.enabled, Is.True,
                "Wrapping must start the same live beast search timer.");
            trial.ApplyInput(Vector2.zero, false, false, false, true);
            Assert.That(trial.IsCarrying, Is.True);
            Assert.That(trial.IsSearching, Is.False,
                "Picking up the wrapped bell must clear the live search state.");
            Assert.That(trial.IsWrapped, Is.False);
            Assert.That(trial.HasBellTarget, Is.False);
            Assert.That(bellAudio.isPlaying, Is.False);
            Assert.That(bellAudio.loop, Is.False);
            Assert.That(bellAudio.clip, Is.Null,
                "The collected wrapped bell must not retain its muted audio cue.");
            yield return null;
            Assert.That(intent.enabled, Is.False,
                "The world-space last-heard marker must clear when the bell is recovered.");
            Assert.That(countdown.enabled, Is.False,
                "Search countdown cannot survive a real bell pickup.");
            Assert.That(trial.GateOpen, Is.True);
            Assert.That(gateBlocker.enabled, Is.False);

            body.position = new Vector2(-4f, 0f);
            Physics2D.SyncTransforms();
            yield return null;
            Assert.That(Vector2.Distance(trial.BellPosition,
                body.position + new Vector2(0.35f, 0f)), Is.LessThan(0.05f),
                "The single physical bell must follow the rescuer after its sound stops.");
            Assert.That(trial.CanPlaceBell, Is.False,
                "The silent carried bell still cannot be dropped off a marked stand.");
            trial.ApplyInput(Vector2.zero, false, false, false, true);
            Assert.That(trial.IsCarrying, Is.True);

            // Retrying a successful lure from the real RETREAT control must
            // atomically clear the previous sound source, threat and opened
            // passage. Otherwise the next run can inherit an audible phantom
            // bell or an already-unlocked gate despite a reset Makinom signal.
            body.position = startingPosition;
            Physics2D.SyncTransforms();
            trial.ApplyInput(Vector2.zero, false, false, true);
            Assert.That(trial.HasBellTarget, Is.False);
            Assert.That(trial.IsSearching, Is.False);
            Assert.That(trial.IsWrapped, Is.False);
            Assert.That(trial.IsCarrying, Is.False);
            Assert.That(trial.BellPosition, Is.EqualTo(startingBellPosition));
            Assert.That(trial.GateOpen, Is.False);
            Assert.That(trial.ClawsObserved, Is.False,
                "Retry must clear even previously inspected optional evidence.");
            Assert.That(bellAudio.isPlaying, Is.False,
                "Retry must stop the old looping bell in the actual AudioSource.");
            Assert.That(bellAudio.loop, Is.False);
            Assert.That(bellAudio.clip, Is.Null);
            yield return null;
            Assert.That(intent.enabled, Is.False);
            Assert.That(countdown.enabled, Is.False);
            Assert.That(gateBlocker.enabled, Is.True,
                "Retry must restore the real physical gate, not just its caption.");

            // Retry is also available through the player's focused UGUI
            // action menu. Clicking it must reset and return controls to
            // gameplay without leaving an invisible Submit target selected.
            var retryButton = GameObject.Find("Soundwell Retry button")?.GetComponent<Button>();
            Assert.That(retryButton, Is.Not.Null);
            trial.SetActionMenuOpen(true);
            yield return null;
            Assert.That(retryButton.interactable, Is.True);
            Assert.That(trial.ActionMenuOpen, Is.True);
            Assert.That(SoundwellInputAdapter.IsUiContextActive, Is.True);
            retryButton.onClick.Invoke();
            Assert.That(trial.ActionMenuOpen, Is.False,
                "UI Retry must close the exploration action menu.");
            Assert.That(SoundwellInputAdapter.IsGameplayContextActive, Is.True,
                "The saved gameplay action map must resume after UI Retry.");
            var eventSystem = UnityEngine.EventSystems.EventSystem.current;
            Assert.That(eventSystem, Is.Not.Null);
            Assert.That(eventSystem.sendNavigationEvents, Is.False);
            Assert.That(eventSystem.currentSelectedGameObject, Is.Null,
                "Retry must not keep a stale focused UGUI Submit target.");

            Debug.Log("RPG_SOUNDWELL_THREAT_PREVIEW_VALIDATED");
            yield return new ExitPlayMode();
        }

        [UnityTearDown]
        public IEnumerator RestoreEditMode()
        {
            if (Application.isPlaying)
                yield return new ExitPlayMode();
        }
    }
}
