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

            var body = trial.PlayerActor.GetComponent<Rigidbody2D>();
            Assert.That(body, Is.Not.Null);
            // Use actual carry/drop controls, not a manufactured Makinom flag.
            body.position = trial.BellPosition;
            Physics2D.SyncTransforms();
            trial.ApplyInput(Vector2.zero, false, false, false, true);
            Assert.That(trial.IsCarrying, Is.True);
            body.position = SoundwellTrial.SideBellStand;
            Physics2D.SyncTransforms();
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
            trial.ApplyInput(Vector2.zero, true, false, false);
            yield return null;
            Assert.That(trial.HasBellTarget, Is.True,
                "A fresh audible strike must resume the real lure.");
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
