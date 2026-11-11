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
            yield return new WaitForSeconds(SoundwellTrial.SearchDurationSeconds);
            Assert.That(trial.IsSearching, Is.False);
            Assert.That(trial.IsWrapped, Is.True);
            Assert.That(intent.enabled, Is.False,
                "The beast must stop advertising an expired search target.");
            Assert.That(countdown.enabled, Is.False,
                "The search timer must disappear when the same AI clock expires.");
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
