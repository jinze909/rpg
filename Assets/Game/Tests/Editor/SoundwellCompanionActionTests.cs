using System.Collections;
using System.Linq;
using GamingIsLove.Makinom;
using GamingIsLove.ORKFramework;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Rpg.Tests.Editor
{
    public sealed class SoundwellCompanionActionTests
    {
        [Test]
        public void OrkStepMustKeepWholeCombatantInsideSavedBattleBounds()
        {
            // The previous centre-only check allowed a character's 2D
            // footprint to hang over a boundary despite a legal centre.
            // These examples cover leader/companion radii on both axes.
            Assert.That(SoundwellOrkParty.FitsBattleStepInArena(
                new Vector2(6.5f, 0f), 0.125f), Is.True);
            Assert.That(SoundwellOrkParty.FitsBattleStepInArena(
                new Vector2(6.6f, 0f), 0.125f), Is.False,
                "A companion near the east edge cannot walk its pixels outside the arena.");
            Assert.That(SoundwellOrkParty.FitsBattleStepInArena(
                new Vector2(-6.55f, 0f), 0.30f), Is.False,
                "The real leader collider must fit inside the west edge.");
            Assert.That(SoundwellOrkParty.FitsBattleStepInArena(
                new Vector2(0f, 2.65f), 0.125f), Is.True);
            Assert.That(SoundwellOrkParty.FitsBattleStepInArena(
                new Vector2(0f, 2.74f), 0.125f), Is.False,
                "A northbound step must reject a character whose centre is inside " +
                "but whose collider crosses the upper edge.");
            Assert.That(SoundwellOrkParty.FitsBattleStepInArena(
                new Vector2(0f, -2.70f), 0.30f), Is.False,
                "Scaled and leader-sized colliders need real southern clearance.");
            Assert.That(SoundwellOrkParty.FitsBattleStepInArena(
                Vector2.zero, 0.30f), Is.True);
        }
        [UnityTest]
        public IEnumerator OrkAcceptedCompanionAttackChangesOnlyItsPixelPose()
        {
            EditorSceneManager.OpenScene("Assets/Game/Scenes/Bootstrap.unity", OpenSceneMode.Single);
            yield return new EnterPlayMode(expectDomainReload: true);
            yield return RunLiveCompanionAttack();
            yield return new ExitPlayMode();
        }

        private static IEnumerator RunLiveCompanionAttack()
        {
            float deadline = Time.realtimeSinceStartup + 30f;
            while ((!Maki.Initialized || !ORK.Initialized) &&
                   Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(Maki.Initialized && ORK.Initialized, Is.True);
            var entry = Object.FindFirstObjectByType<SoundwellEntrance>();
            Assert.That(entry, Is.Not.Null);
            var load = entry.OpenTrial();
            Assert.That(load, Is.Not.Null);
            while (!load.isDone)
                yield return null;

            SoundwellTrial trial = null;
            SoundwellOrkParty party = null;
            deadline = Time.realtimeSinceStartup + 20f;
            while (Time.realtimeSinceStartup < deadline)
            {
                trial = Object.FindFirstObjectByType<SoundwellTrial>();
                party = Object.FindFirstObjectByType<SoundwellOrkParty>();
                if (trial != null && party != null && trial.IsReady && party.IsReady)
                    break;
                yield return null;
            }
            Assert.That(trial != null && party != null && trial.IsReady && party.IsReady,
                Is.True, "Companion attack must use real saved ORK combatants.");
            // The scene actor list can retire a follower during teardown
            // while ORK still owns the battle roster. One missing visual
            // must not make the regular world update throw or stop the trial.
            var flags = System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic;
            var actorField = typeof(SoundwellOrkParty).GetField("actors", flags);
            Assert.That(actorField, Is.Not.Null);
            var actorRoster = (IList)actorField.GetValue(party);
            GameObject savedFollower = (GameObject)actorRoster[1];
            Assert.That(savedFollower, Is.Not.Null);
            try
            {
                actorRoster[1] = null;
                yield return null;
                Assert.That(party.IsReady, Is.True,
                    "Losing one scene visual must not tear down the ORK party.");
            }
            finally
            {
                actorRoster[1] = savedFollower;
            }

            // A chooser can change while a delayed UGUI view refreshes.
            // Stale actor indices and a missing gate predicate must be
            // harmless, not tear down the live ORK party with IndexOutOfRange.
            Assert.That(party.CanGuard(-1), Is.False);
            Assert.That(party.CanGuard(party.Members.Count), Is.False);
            Assert.That(party.CanOperateGate(-1, _ => true), Is.False);
            Assert.That(party.CanOperateGate(party.Members.Count, _ => true), Is.False);
            Assert.That(party.CanOperateGate(0, null), Is.False);
            trial.ReadInput = false;
            var body = party.Leader.GetComponent<Rigidbody2D>();
            body.linearVelocity = Vector2.zero;
            body.position = new Vector2(-0.55f, 0f);
            Physics2D.SyncTransforms();
            Assert.That(trial.CanEngageBeast, Is.True);
            trial.ApplyInput(Vector2.zero, false, false, false, false, true);

            var button = GameObject.Find("Soundwell Attack button")?.GetComponent<Button>();
            Assert.That(button, Is.Not.Null);
            deadline = Time.realtimeSinceStartup + 10f;
            while (party.ReadyBaseAttackerIndex < 0 &&
                   Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(party.ReadyBaseAttackerIndex, Is.GreaterThanOrEqualTo(0),
                "Live ORK must offer an actual player turn.");
            int choosing = party.ChoosingRescuerIndex;
            Assert.That(choosing, Is.InRange(0, 2));
            // A delayed UGUI refresh can still ask about yesterday's ORK
            // chooser after a different rescuer receives the live turn.
            // Never offer Guard or Gate to that stale actor index.
            int offTurnRescuer = (choosing + 1) % party.Members.Count;
            Assert.That(party.CanGuard(offTurnRescuer), Is.False,
                "Guard must not be offered to a non-choosing ORK rescuer.");
            Assert.That(party.CanOperateGate(offTurnRescuer, _ => true), Is.False,
                "Gate must not be offered to a non-choosing ORK rescuer.");
            var memberField = typeof(SoundwellOrkParty).GetField("members", flags);
            Assert.That(memberField, Is.Not.Null);
            var memberRoster = (IList)memberField.GetValue(party);
            object savedMember = memberRoster[choosing];
            GameObject savedActor = (GameObject)actorRoster[choosing];
            try
            {
                memberRoster[choosing] = null;
                Assert.That(party.ChoosingRescuerIndex, Is.EqualTo(-1),
                    "A retired ORK roster entry must never be selected.");
                string retiredReadout = party.BuildBattleReadout();
                Assert.That(retiredReadout, Is.Not.Empty,
                    "Other live combatants and the beast must remain visible.");
                Assert.That(retiredReadout, Does.Not.Contain(
                    SoundwellUiLocalization.Get("battle.rescuer") + " " + (choosing + 1) + "  "),
                    "A retired ORK member must disappear from the readout without throwing.");
            }
            finally
            {
                memberRoster[choosing] = savedMember;
            }
            try
            {
                actorRoster[choosing] = null;
                Assert.That(party.ChoosingRescuerIndex, Is.EqualTo(-1),
                    "A vanished Unity scene actor cannot retain an ORK chooser.");
                Assert.That(party.CanGuard(choosing), Is.False,
                    "An orphaned ORK chooser must not offer Guard.");
                Assert.That(party.BuildBattleReadout(), Does.Not.Contain(
                    SoundwellUiLocalization.Get("battle.rescuer") + " " + (choosing + 1) + "  "),
                    "The HUD must not show a combatant whose Unity actor has vanished.");
            }
            finally
            {
                actorRoster[choosing] = savedActor;
            }
            Assert.That(party.ChoosingRescuerIndex, Is.EqualTo(choosing),
                "Restoring the real actor must restore the current ORK turn.");

            var second = party.Actors[1].GetComponent<SpriteRenderer>();
            var third = party.Actors[2].GetComponent<SpriteRenderer>();
            Assert.That(second, Is.Not.Null);
            Assert.That(third, Is.Not.Null);
            Sprite baseline = second.sprite;
            Sprite hoodedBaseline = third.sprite;
            Assert.That(baseline, Is.Not.Null);
            Assert.That(hoodedBaseline, Is.Not.Null);
            Assert.That(hoodedBaseline, Is.Not.SameAs(baseline),
                "Rescuer 3 must not be a recolored copy of rescuer 2.");
            Assert.That(baseline.texture.GetPixels32().SequenceEqual(
                hoodedBaseline.texture.GetPixels32()), Is.False,
                "Hooded rescuer 3 must have authored 16x16 outline differences.");
            Assert.That(hoodedBaseline.rect.size, Is.EqualTo(new Vector2(16f, 16f)));
            Assert.That(hoodedBaseline.pixelsPerUnit, Is.EqualTo(16f));
            Assert.That(hoodedBaseline.texture.filterMode, Is.EqualTo(FilterMode.Point));

            // Submit other party members only when ORK really chooses them.
            // No manual HP damage, synthetic target or fabricated turn.
            for (int i = 0; i < 3 && party.ReadyBaseAttackerIndex != 1; i++)
            {
                Assert.That(party.ReadyBaseAttackerIndex, Is.GreaterThanOrEqualTo(0),
                    "The second rescuer should receive a live action choice.");
                Assert.That(party.TryAttackBeast(), Is.True);
                yield return null;
            }
            Assert.That(party.ReadyBaseAttackerIndex, Is.EqualTo(1),
                "The live turn must reach rescuer 2 before checking its pose.");
            Assert.That(second.sprite, Is.SameAs(baseline));
            float beastHorizontalOffset = party.Beast.GameObject.transform.position.x -
                party.Actors[1].transform.position.x;
            Assert.That(Mathf.Abs(beastHorizontalOffset), Is.GreaterThan(0.1f),
                "The test needs a distinct horizontal ORK target to verify facing.");
            // Recreate the real defect: an exploring follower can face away
            // from the beast when its ORK attack becomes legal.
            second.flipX = beastHorizontalOffset > 0f;
            bool thirdFacingBefore = third.flipX;
            Assert.That(party.TryAttackBeast(), Is.True,
                "Only a genuine ORK-accepted action may animate its rescuer.");
            Assert.That(second.flipX, Is.EqualTo(beastHorizontalOffset < 0f),
                "The attacking companion must face ORK's real beast target.");
            Assert.That(third.flipX, Is.EqualTo(thirdFacingBefore),
                "One accepted attack must not turn an inactive companion.");
            Assert.That(second.sprite, Is.Not.SameAs(baseline),
                "Rescuer 2 needs a visible attack silhouette, not a frozen idle frame.");
            Assert.That(second.sprite.rect.size, Is.EqualTo(new Vector2(16f, 16f)));
            Assert.That(second.sprite.pixelsPerUnit, Is.EqualTo(16f));
            Assert.That(second.sprite.texture.filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(third.sprite, Is.SameAs(hoodedBaseline),
                "Rescuer 2 cannot animate an ally who did not attack.");

            // ORK can finish the final action and release the party before this
            // pose expires. The explorer's follower will then walk toward the
            // teleported test leader, so its legitimate walking frame is not
            // necessarily the pre-attack idle sprite. Verify the attack pose
            // ends, without mistaking ordinary post-battle walking for failure.
            Sprite acceptedAttackPose = second.sprite;
            yield return new WaitForSecondsRealtime(0.45f);
            Assert.That(Time.unscaledTime, Is.GreaterThanOrEqualTo(0.30f));
            Assert.That(second.sprite, Is.Not.SameAs(acceptedAttackPose),
                "The action must end without sticking on an attacking pose. " +
                "Scaled time: " + Time.time + "; unscaled time: " + Time.unscaledTime +
                "; active: " + party.isActiveAndEnabled + "; battle: " + party.BattleInProgress);
            if (party.BattleInProgress)
                Assert.That(second.sprite, Is.SameAs(baseline),
                    "During battle the action must return to the idle combat pose.");
        }

        [UnityTest]
        public IEnumerator LeaderActionAndHitFeedbackExpireDuringPausedOrkClock()
        {
            // Model ORK's scaled-time pause without invoking a fake action or
            // changing battle state. The real M4 Animator must still finish
            // its strictly visual one-shots in wall-clock time.
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode(expectDomainReload: true);
            var actor = new GameObject("Soundwell paused-time visual test");
            actor.AddComponent<SpriteRenderer>();
            var visual = actor.AddComponent<SoundwellPlayerVisualAnimation>();
            float originalTimeScale = Time.timeScale;
            try
            {
                visual.Initialize();
                Assert.That(visual.Controller, Is.Not.Null);
                Assert.That(visual.Controller.updateMode, Is.EqualTo(AnimatorUpdateMode.UnscaledTime));
                Time.timeScale = 0f;

                visual.PlayAction();
                Assert.That(visual.CurrentState, Is.EqualTo("Action"));
                yield return new WaitForSecondsRealtime(0.47f);
                visual.UpdateMotion(false);
                Assert.That(visual.CurrentState, Is.EqualTo("Idle"),
                    "Leader's action must not be trapped when ORK pauses scaled time.");

                visual.PlayHit();
                Assert.That(visual.CurrentState, Is.EqualTo("Hit"));
                yield return new WaitForSecondsRealtime(0.38f);
                visual.UpdateMotion(false);
                Assert.That(visual.CurrentState, Is.EqualTo("Idle"),
                    "A hit reaction must also expire without scaled-time progress.");
            }
            finally
            {
                Time.timeScale = originalTimeScale;
                Object.Destroy(actor);
            }
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
