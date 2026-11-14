using System.Collections;
using System.Linq;
using GamingIsLove.Makinom;
using GamingIsLove.ORKFramework;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.Audio;
using UnityEngine.UI;

namespace Rpg.Tests.Editor
{
    public sealed class SoundwellTrialTests
    {
        [Test]
        public void StaleGameplayInputBeforeTrialInitializationCannotMutateOrExitTheScene()
        {
            // A scene-load/UI handoff may deliver the last keyboard/controller
            // press before SoundwellTrial.Start creates its ORK/Makinom owners.
            // Both entry points must ignore it instead of dereferencing the
            // missing player body or starting an unintended Bootstrap load.
            var root = new GameObject("Uninitialized Soundwell input regression");
            try
            {
                var trial = root.AddComponent<SoundwellTrial>();
                Assert.That(trial.IsReady, Is.False);
                Assert.DoesNotThrow(() => trial.ApplyInput(Vector2.left,
                    true, true, true, true, true));
                var lateBack = SoundwellControlFrame.FromActions(Vector2.left,
                    true, true, true, true, true, true);
                Assert.DoesNotThrow(() => trial.ApplyControls(lateBack));
                Assert.That(trial.IsReady, Is.False);
                Assert.That(trial.ActionMenuOpen, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void BattleChooserActorLookupIgnoresDestroyedOrMissingActors()
        {
            var actor = new GameObject("Soundwell ORK chooser actor lookup");
            var actors = new[] { actor };
            try
            {
                Assert.That(SoundwellTrial.ResolveBattleActor(actors, 0),
                    Is.SameAs(actor.transform),
                    "A live ORK combatant must retain its real world transform.");
                Assert.That(SoundwellTrial.ResolveBattleActor(actors, -1), Is.Null);
                Assert.That(SoundwellTrial.ResolveBattleActor(actors, actors.Length), Is.Null);
                Assert.That(SoundwellTrial.ResolveBattleActor(null, 0), Is.Null);
                Object.DestroyImmediate(actor);
                Assert.That(SoundwellTrial.ResolveBattleActor(actors, 0), Is.Null,
                    "A destroyed ORK actor must not throw while publishing battle HUD state.");
            }
            finally
            {
                if (actor != null)
                    Object.DestroyImmediate(actor);
            }
        }

        [Test]
        public void RetiredOrkGateActorCannotOpenPhysicalGate()
        {
            // ORK can call a cached reach predicate after its scene-owned
            // actor has retired. Exercise the exact predicate with no actor;
            // the existing live battle test covers actual gate use.
            var predicate = typeof(SoundwellTrial).GetMethod(
                "HasLiveGateActorInReach",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Static);
            Assert.That(predicate, Is.Not.Null);
            Assert.That(predicate.Invoke(null, new object[] { null }),
                Is.EqualTo(false),
                "A missing ORK member may not open the gate or dereference GameObject.");

            // Check the extracted public signal registry without inventing a
            // new Makinom/ORK authority or changing the locked test manifest.
            Assert.That(SoundwellSignalKeys.SignalKey,
                Is.EqualTo("rpg.soundwell.signal-trial.bell"));
            Assert.That(SoundwellSignalKeys.GateKey,
                Is.EqualTo("rpg.soundwell.signal-trial.inner-gate"));
            Assert.That(SoundwellSignalKeys.RescueKey,
                Is.EqualTo("rpg.soundwell.signal-trial.surveyor-rescued"));
            Assert.That((int)BellSignal.Ready, Is.Zero);
            Assert.That((int)BellSignal.Ringing, Is.EqualTo(1));
            Assert.That((int)BellSignal.Wrapped, Is.EqualTo(2));
            Assert.That((int)BellSignal.Searching, Is.EqualTo(3));
            AssertIndependentBeastPatrolSignals();
        }

        // This is a helper of the existing registered EditMode regression,
        // not a new undisclosed NUnit test that bypasses strict CI evidence.
        private static void AssertIndependentBeastPatrolSignals()
        {
            var root = new GameObject("isolated beast service");
            var leader = new GameObject("isolated leader");
            var actor = new GameObject("isolated beast");
            try
            {
                Rigidbody2D body = leader.AddComponent<Rigidbody2D>();
                body.gravityScale = 0f;
                actor.transform.position = new Vector3(3f, 0f, 0f);
                var patrol = root.AddComponent<BeastPatrolAndChase>();
                patrol.Configure(actor.transform, body, actor.transform.position);
                int contacts = 0;
                patrol.PlayerCaught += () => contacts++;
                Vector2 sound = new Vector2(1.15f, -1.95f);
                patrol.OnBellSignal(BellSignal.Ringing, sound);
                Assert.That(patrol.Tick(BellSignal.Ringing, sound, false, false,
                    false, false), Is.EqualTo(sound),
                    "Beast chase must use the live bell target, not a UI guess.");

                body.position = new Vector2(3f, 0f);
                actor.transform.position = new Vector3(3f, 0f, 0f);
                patrol.Tick(BellSignal.Ready, sound, false, false, false, false);
                patrol.Tick(BellSignal.Ready, sound, false, false, true, true);
                patrol.Tick(BellSignal.Ready, sound, true, false, true, false);
                patrol.Tick(BellSignal.Ready, sound, false, true, true, false);
                Assert.That(contacts, Is.Zero);
                patrol.Tick(BellSignal.Ready, sound, false, false, true, false);
                Assert.That(contacts, Is.EqualTo(1),
                    "Contact fires only in an open unsafe exploration lane.");
                Assert.That(body.position, Is.EqualTo(new Vector2(3f, 0f)),
                    "Only TrialCheckpointManager may relocate a captured player.");
                patrol.ResetPatrol();
                Assert.That(patrol.Position, Is.EqualTo(new Vector2(3f, 0f)));
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(leader);
                Object.DestroyImmediate(actor);
            }
        }

        [UnityTest]
        public IEnumerator PlayerCanRingWrapRetreatAndRetry()
        {
            EditorSceneManager.OpenScene("Assets/Game/Scenes/Bootstrap.unity", OpenSceneMode.Single);
            yield return new EnterPlayMode(expectDomainReload: true);
            yield return RunTrialInPlayerLoop();
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator BeastKnockbackKeepsCarriedBellWithRescuerOnContactFrame()
        {
            // Reproduce the real non-combat opening: move the single saved
            // bell, lure the beast, open the physical gate, lift the bell
            // again and step into its actual live contact radius.
            EditorSceneManager.OpenScene("Assets/Game/Scenes/Bootstrap.unity", OpenSceneMode.Single);
            yield return new EnterPlayMode(expectDomainReload: true);
            yield return VerifyCarriedBellFollowsBeastKnockback();
            yield return new ExitPlayMode();
        }

        private static IEnumerator VerifyCarriedBellFollowsBeastKnockback()
        {
            float deadline = Time.realtimeSinceStartup + 30f;
            while ((!Maki.Initialized || !ORK.Initialized) &&
                   Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(Maki.Initialized && ORK.Initialized, Is.True);

            var entry = Object.FindFirstObjectByType<SoundwellEntrance>();
            Assert.That(entry, Is.Not.Null);
            AsyncOperation loading = entry.OpenTrial();
            Assert.That(loading, Is.Not.Null);
            deadline = Time.realtimeSinceStartup + 20f;
            while (!loading.isDone && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(loading.isDone, Is.True);

            SoundwellTrial trial = null;
            deadline = Time.realtimeSinceStartup + 25f;
            while (Time.realtimeSinceStartup < deadline)
            {
                trial = Object.FindFirstObjectByType<SoundwellTrial>();
                if (trial != null && trial.IsReady)
                    break;
                yield return null;
            }
            Assert.That(trial != null && trial.IsReady, Is.True);
            trial.ReadInput = false;
            Rigidbody2D body = trial.PlayerActor.GetComponent<Rigidbody2D>();
            Assert.That(body, Is.Not.Null);
            Vector2 retreat = trial.PlayerPosition;

            var carryPose = trial.PlayerActor.GetComponentInChildren<SoundwellPlayerVisualAnimation>();
            Assert.That(carryPose, Is.Not.Null);
            MoveTestLeader(body, trial.BellPosition);
            trial.ApplyInput(Vector2.zero, false, false, false, true);
            Assert.That(trial.IsCarrying, Is.True);
            Assert.That(carryPose.CurrentState, Is.EqualTo("Action"),
                "The actual lift must animate the leader, like an accepted ring/wrap.");
            // A carry button away from either stand must not discard the bell
            // or silently play a new successful interaction pose.
            MoveTestLeader(body, new Vector2(0f, 0.1f));
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.That(carryPose.CurrentState, Is.Not.EqualTo("Action"));
            trial.ApplyInput(Vector2.zero, false, false, false, true);
            Assert.That(trial.IsCarrying, Is.True);
            Assert.That(carryPose.CurrentState, Is.Not.EqualTo("Action"),
                "A rejected drop must not pretend an interaction happened.");
            MoveTestLeader(body, SoundwellTrial.SideBellStand);
            Assert.That(trial.CanPlaceBell, Is.True);
            trial.ApplyInput(Vector2.zero, false, false, false, true);
            Assert.That(trial.IsCarrying, Is.False);
            Assert.That(carryPose.CurrentState, Is.EqualTo("Action"),
                "Only an actual placement at a saved stand should replay the action pose.");
            trial.ApplyInput(Vector2.zero, true, false, false);
            Assert.That(trial.HasBellTarget, Is.True);
            MoveTestLeader(body, new Vector2(-0.55f, 0f));
            deadline = Time.realtimeSinceStartup + 8f;
            while (!trial.CanOpenGate && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(trial.CanOpenGate, Is.True,
                "Only the actual ringing side stand can divert the beast.");
            trial.ApplyInput(Vector2.zero, false, false, false, false, true);
            Assert.That(trial.GateOpen, Is.True);
            yield return null;
            var escapeAfterGate = GameObject.Find("Soundwell escape status")?.GetComponent<Text>();
            Assert.That(escapeAfterGate, Is.Not.Null);
            Assert.That(escapeAfterGate.enabled, Is.True,
                "Opening the real gate must reveal the live escape-lane warning again.");

            MoveTestLeader(body, SoundwellTrial.SideBellStand);
            trial.ApplyInput(Vector2.zero, false, false, false, true);
            Assert.That(trial.IsCarrying, Is.True);
            Assert.That(trial.HasBellTarget, Is.False,
                "Picking up the same bell silences the lure.");
            Vector2 beastAtContact = trial.BeastPosition;
            Assert.That(beastAtContact.x, Is.GreaterThan(0.55f),
                "Only actual contact east of the gate triggers the knockback.");
            int priorContacts = trial.BeastContacts;
            MoveTestLeader(body, beastAtContact);
            yield return null;
            Assert.That(trial.BeastContacts, Is.EqualTo(priorContacts + 1),
                "The real beast, not a manually raised state, must cause the hit.");
            Assert.That(Vector2.Distance(trial.PlayerPosition, retreat),
                Is.LessThan(0.05f),
                "The player must be physically knocked back to RETREAT.");
            Assert.That(trial.IsCarrying, Is.True,
                "Knockback must not silently drop or duplicate the only bell.");
            Assert.That(Vector2.Distance(trial.BellPosition,
                trial.PlayerPosition + new Vector2(0.35f, 0f)),
                Is.LessThan(0.001f),
                "On the very same contact frame, the bell must follow the relocated player.");
            Assert.That(trial.GateOpen, Is.True,
                "The earned gate opening remains intact after the collision.");
        }

        [UnityTest]
        public IEnumerator AcceptedOrkLeaderAttackShowsActualPlayerActionClip()
        {
            EditorSceneManager.OpenScene("Assets/Game/Scenes/Bootstrap.unity", OpenSceneMode.Single);
            yield return new EnterPlayMode(expectDomainReload: true);
            yield return VerifyAttackVisualInLiveBattle();
            yield return new ExitPlayMode();
        }

        private static IEnumerator VerifyAttackVisualInLiveBattle()
        {
            float deadline = Time.realtimeSinceStartup + 30f;
            while ((!Maki.Initialized || !ORK.Initialized) &&
                   Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(Maki.Initialized && ORK.Initialized, Is.True);

            var entry = Object.FindFirstObjectByType<SoundwellEntrance>();
            Assert.That(entry, Is.Not.Null);
            var loading = entry.OpenTrial();
            Assert.That(loading, Is.Not.Null);
            deadline = Time.realtimeSinceStartup + 20f;
            while (!loading.isDone && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(loading.isDone, Is.True);

            SoundwellTrial trial = null;
            SoundwellOrkParty party = null;
            deadline = Time.realtimeSinceStartup + 20f;
            while (Time.realtimeSinceStartup < deadline)
            {
                trial = Object.FindFirstObjectByType<SoundwellTrial>();
                party = Object.FindFirstObjectByType<SoundwellOrkParty>();
                if (trial != null && trial.IsReady && party != null && party.IsReady)
                    break;
                yield return null;
            }
            Assert.That(trial != null && trial.IsReady && party != null && party.IsReady,
                Is.True, "The saved Soundwell and three ORK combatants must load.");

            // M9: the scene's dynamic threat, gate and escape labels must
            // translate from saved StringTables, not stay English when the
            // player switches language. No duplicate UI/state is instantiated.
            SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
            yield return null;
            var beastStatus = GameObject.Find("Soundwell beast status")?.GetComponent<Text>();
            var gateStatus = GameObject.Find("Soundwell gate status")?.GetComponent<Text>();
            var escapeStatus = GameObject.Find("Soundwell escape status")?.GetComponent<Text>();
            Assert.That(beastStatus, Is.Not.Null);
            Assert.That(gateStatus, Is.Not.Null);
            Assert.That(escapeStatus, Is.Not.Null);
            Assert.That(escapeStatus.enabled, Is.False,
                "A locked gate must not advertise a clear exit before an actual route exists.");
            Assert.That(beastStatus.text, Is.EqualTo("BEAST  /  LISTENING"));
            Assert.That(gateStatus.text, Is.EqualTo("GATE  /  LOCKED"));
            Assert.That(escapeStatus.text, Is.EqualTo("ESCAPE LANE  /  CLEAR"));
            SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
            yield return null;
            Assert.That(beastStatus.text, Is.EqualTo("野兽 / 监听中"));
            Assert.That(gateStatus.text, Is.EqualTo("闸门 / 已锁定"));
            Assert.That(escapeStatus.text, Is.EqualTo("撤离通道 / 畅通"));
            Assert.That(gateStatus.font, Is.Not.Null);
            Assert.That(gateStatus.font.HasCharacter('闸'), Is.True,
                "The dynamic Chinese state text needs a font with actual CJK glyphs.");
            SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
            yield return null;
            Assert.That(gateStatus.text, Is.EqualTo("GATE  /  LOCKED"));

            trial.ReadInput = false;
            var leader = party.Leader.GetComponent<Rigidbody2D>();
            MoveTestLeader(leader, new Vector2(-0.55f, 0f));
            VerifyBattleCameraKeepsBothExitsVisible(leader);
            Assert.That(trial.CanEngageBeast, Is.True);
            // The action that actually launches the ORK encounter must show
            // its caption in both shipped languages, not fixed English.
            var encounterChoice = GameObject.Find("Soundwell Gate button")
                ?.GetComponentInChildren<Text>();
            Assert.That(encounterChoice, Is.Not.Null);
            yield return null;
            Assert.That(encounterChoice.text, Is.EqualTo("ENGAGE BEAST [G / RB]"));
            SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
            yield return null;
            Assert.That(encounterChoice.text, Is.EqualTo("迎战野兽 [G / RB]"));
            Assert.That(encounterChoice.font.HasCharacter('兽'), Is.True);
            SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
            yield return null;
            Assert.That(encounterChoice.text, Is.EqualTo("ENGAGE BEAST [G / RB]"));
            trial.ApplyInput(Vector2.zero, false, false, false, false, true);
            Assert.That(trial.DirectCombatStarted, Is.True);

            var attack = GameObject.Find("Soundwell Attack button")?.GetComponent<Button>();
            var visual = GameObject.Find("PLACEHOLDER animated player")
                ?.GetComponent<SoundwellPlayerVisualAnimation>();
            Assert.That(attack, Is.Not.Null);
            Assert.That(visual, Is.Not.Null);
            // Battle entry itself animates the player. Wait for the *actual*
            // animation controller to return to Idle instead of assuming a
            // single 0.5-second frame delay is enough in GameCI Play Mode.
            // If the visual genuinely gets stuck, fail rather than resetting
            // animation in the test and masking the gameplay regression.
            float settleDeadline = Time.realtimeSinceStartup + 4f;
            while (visual.CurrentState != "Idle" &&
                   Time.realtimeSinceStartup < settleDeadline)
                yield return null;
            Assert.That(visual.CurrentState, Is.EqualTo("Idle"),
                "The battle-entry one-shot must settle before an ORK attack.");
            deadline = Time.realtimeSinceStartup + 10f;
            while (party.ReadyBaseAttackerIndex < 0 &&
                   Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(party.ReadyBaseAttackerIndex, Is.GreaterThanOrEqualTo(0),
                "The real ORK turn must present an actionable rescuer.");
            // The party can have other legal selectors first. Submit through
            // the same UGUI button; only the actual leader animates.
            for (int choice = 0; choice < 3 && party.ReadyBaseAttackerIndex != 0; choice++)
            {
                Assert.That(attack.interactable, Is.True);
                attack.onClick.Invoke();
                Assert.That(visual.CurrentState, Is.EqualTo("Idle"),
                    "A companion cannot cause the leader's attack animation.");
                yield return null;
            }
            Assert.That(party.ReadyBaseAttackerIndex, Is.EqualTo(0),
                "This test must cover the leader's own legal action.");
            Assert.That(attack.interactable, Is.True);
            attack.onClick.Invoke();
            Assert.That(visual.CurrentState, Is.EqualTo("Action"),
                "Submitting a legal ORK leader attack must play M4's authored Action clip.");
            Assert.That(party.Members[0].Actions.IsChoosing, Is.False,
                "The feedback must accompany an ORK-accepted action, not a fake UI attack.");
            SoundwellSmokeHost.Capture(trial, "16-ork-leader-action",
                visiblePoints: new[] { visual.transform.position, party.Beast.GameObject.transform.position });
        }

        [UnityTest]
        public IEnumerator BellOnlyRescueWorksWithoutOptionalCluesOrEscortRope()
        {
            // The full smoke path already tests collecting rope and cart clues.
            // This separate live-scene regression protects the *optional* branch:
            // the sound-only rescue must stay achievable with neither pickup.
            EditorSceneManager.OpenScene("Assets/Game/Scenes/Bootstrap.unity", OpenSceneMode.Single);
            yield return new EnterPlayMode(expectDomainReload: true);
            yield return RunBellOnlyRescueAfterInitialization();
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator PausedEscortRetryClearsWaitingVisualBeforeNextFrame()
        {
            // The real bell rescue can be abandoned while the survivor is
            // waiting behind the player. Retry must restore the world actor,
            // not just clear Makinom escort variables on the next frame.
            EditorSceneManager.OpenScene("Assets/Game/Scenes/Bootstrap.unity", OpenSceneMode.Single);
            yield return new EnterPlayMode(expectDomainReload: true);
            yield return RunBellOnlyRescueAfterInitialization(retryDuringEscortPause: true);
            yield return new ExitPlayMode();
        }

        private static IEnumerator RunBellOnlyRescueAfterInitialization(bool retryDuringEscortPause = false)
        {
            float deadline = Time.realtimeSinceStartup + 30f;
            while ((!Maki.Initialized || !ORK.Initialized) &&
                   Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(Maki.Initialized && ORK.Initialized, Is.True);

            var entry = Object.FindFirstObjectByType<SoundwellEntrance>();
            Assert.That(entry, Is.Not.Null);
            var entryButtons = entry.GetComponentsInChildren<Button>(true);
            var startButton = entryButtons.Single(button =>
                button.gameObject.name == "Enter Soundwell button");
            var sfxButton = entryButtons.Single(button =>
                button.gameObject.name == "Soundwell SFX toggle");
            var languageButton = entryButtons.Single(button =>
                button.gameObject.name == "Soundwell entry Language button");
            Assert.That(startButton.interactable && sfxButton.interactable &&
                languageButton.interactable, Is.True);
            var load = entry.OpenTrial();
            Assert.That(load, Is.Not.Null);
            Assert.That(startButton.interactable || sfxButton.interactable ||
                languageButton.interactable, Is.False,
                "An accepted entry must lock all UGUI settings until scene handoff.");
            Assert.That(entry.OpenTrial(), Is.SameAs(load),
                "Repeated Entry/Submit during loading must share one scene transition.");
            deadline = Time.realtimeSinceStartup + 12f;
            while (!load.isDone && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(load.isDone, Is.True);

            SoundwellTrial trial = null;
            SoundwellOrkParty party = null;
            deadline = Time.realtimeSinceStartup + 15f;
            while (Time.realtimeSinceStartup < deadline)
            {
                trial = Object.FindFirstObjectByType<SoundwellTrial>();
                party = Object.FindFirstObjectByType<SoundwellOrkParty>();
                if (trial != null && party != null && trial.IsReady && party.IsReady)
                    break;
                yield return null;
            }
            Assert.That(trial != null && trial.IsReady && party != null && party.IsReady,
                Is.True, "Use real saved Soundwell and three-member ORK group.");
            trial.ReadInput = false;
            var playerBody = party.Leader.GetComponent<Rigidbody2D>();
            Assert.That(playerBody, Is.Not.Null);
            Vector2 safe = playerBody.position;
            Vector2 initialBell = trial.BellPosition;
            Vector2 initialBeast = trial.BeastPosition;
            // Only the completed comparison path emits a shared opening.
            // Abandon-and-retry is a separate control regression, not a
            // second route-cost sample against the COMBAT opening.
            if (!retryDuringEscortPause)
                RecordRescueRouteStart("BELL", trial, party);

            // The current live route-choice hint must switch EN/zh without
            // auto-entering ORK combat, opening the gate or granting a clue.
            MoveTestLeader(playerBody, new Vector2(-0.55f, 0f));
            Assert.That(trial.CanEngageBeast, Is.True);
            var choiceHint = GameObject.Find("Soundwell trial hint")?.GetComponent<Text>();
            Assert.That(choiceHint, Is.Not.Null);
            SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
            yield return null;
            Assert.That(choiceHint.text, Is.EqualTo(
                SoundwellUiLocalization.Get("hint.direct_unknown")));
            Assert.That(choiceHint.text, Does.Contain("正面路线"));
            Assert.That(SoundwellUiLocalization.Get("hint.direct_with_claw"),
                Does.Contain("侧铃架"));
            Assert.That(SoundwellUiLocalization.Get("hint.beast_contact"),
                Does.Contain("击退"));
            Assert.That(SoundwellUiLocalization.Get("hint.beast_defeated"),
                Does.Contain("三名队员"));
            Assert.That(trial.DirectCombatStarted || trial.GateOpen, Is.False,
                "Changing locale cannot select battle, open the gate or spend a turn.");
            SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
            yield return null;
            Assert.That(choiceHint.text, Is.EqualTo(
                SoundwellUiLocalization.Get("hint.direct_unknown")));
            MoveTestLeader(playerBody, safe);
            yield return null;

            // Only the real carry/place/ring actions and the two named stands
            // establish the lure. Do not set Makinom or ORK state in the test.
            MoveTestLeader(playerBody, trial.BellPosition);
            trial.ApplyInput(Vector2.zero, false, false, false, true);
            Assert.That(trial.IsCarrying, Is.True);
            MoveTestLeader(playerBody, SoundwellTrial.SideBellStand);
            trial.ApplyInput(Vector2.zero, false, false, false, true);
            Assert.That(trial.IsCarrying, Is.False);
            Assert.That(Vector2.Distance(trial.BellPosition,
                SoundwellTrial.SideBellStand), Is.LessThan(0.1f));
            trial.ApplyInput(Vector2.zero, true, false, false);
            Assert.That(trial.HasBellTarget, Is.True);
            deadline = Time.realtimeSinceStartup + 8f;
            while (trial.BeastPosition.y >= -0.95f && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(trial.BeastPosition.y, Is.LessThan(-0.95f),
                "The beast must really follow the moved bell before the gate unlocks.");

            MoveTestLeader(playerBody, new Vector2(-0.55f, 0f));
            Assert.That(trial.CanOpenGate, Is.True,
                "A placed side bell and real diverted beast must allow the noncombat route.");
            trial.ApplyInput(Vector2.zero, false, false, false, false, true);
            Assert.That(trial.GateOpen, Is.True);
            Assert.That(trial.DirectCombatStarted || trial.DirectCombatComplete, Is.False,
                "The bell route must not fake an ORK battle victory.");

            // The lower plank really collapses under the player, shows the
            // saved M4 Hit animation and pushes the leader to the existing
            // lower-board recovery tile. It must not grant rescue/ORK victory
            // or silently close the already earned gate.
            var plankHitVisual = GameObject.Find("PLACEHOLDER animated player")
                ?.GetComponent<SoundwellPlayerVisualAnimation>();
            Assert.That(plankHitVisual, Is.Not.Null);
            var weakPlank = GameObject.Find("PLACEHOLDER unsafe lower plank")
                ?.GetComponent<SpriteRenderer>();
            Assert.That(weakPlank, Is.Not.Null);
            var intactPlankSprite = weakPlank.sprite;
            Assert.That(intactPlankSprite, Is.Not.Null);
            Assert.That(intactPlankSprite.texture.width, Is.EqualTo(26));
            Assert.That(intactPlankSprite.texture.height, Is.EqualTo(6));
            Assert.That(intactPlankSprite.texture.filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(intactPlankSprite.pixelsPerUnit, Is.EqualTo(16f));
            Assert.That(weakPlank.transform.localScale, Is.EqualTo(Vector3.one),
                "The weak plank must be authored in whole source pixels, not stretched.");
            Assert.That(trial.LowerPlankBroken, Is.False);
            MoveTestLeader(playerBody, SoundwellTrial.LowerBoardPosition);
            yield return null;
            Assert.That(trial.LowerPlankBroken, Is.True);
            Assert.That(weakPlank.sprite, Is.Not.SameAs(intactPlankSprite),
                "The collapse must open a visible crack, not just fade a solid board.");
            Assert.That(weakPlank.sprite.texture.filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(weakPlank.transform.localScale, Is.EqualTo(Vector3.one));
            Assert.That(plankHitVisual.CurrentState, Is.EqualTo("Hit"),
                "The actual first collapse must play the saved hit reaction.");
            Assert.That(Vector2.Distance(trial.PlayerPosition,
                SoundwellTrial.LowerBoardPosition), Is.GreaterThan(0.48f),
                "The player cannot stand on the collapsed weak board.");
            Assert.That(trial.GateOpen, Is.True);
            Assert.That(trial.RescueComplete || trial.DirectCombatComplete, Is.False);
            Vector2 plankRecovery = trial.PlayerPosition;
            MoveTestLeader(playerBody, SoundwellTrial.LowerBoardPosition);
            yield return null;
            Assert.That(trial.PlayerPosition, Is.EqualTo(plankRecovery),
                "Walking onto an already broken plank must remain impassable.");
            SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
            yield return null;
            var plankWarning = GameObject.Find("Soundwell surveyor response")?.GetComponent<Text>();
            Assert.That(plankWarning, Is.Not.Null);
            Assert.That(plankWarning.text, Is.EqualTo(
                SoundwellUiLocalization.Get("surveyor.plank_snapped")));
            SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
            yield return null;

            // Optional exploration must remain optional, including the guide rope.
            Assert.That(trial.RopeObserved || trial.RopeRecovered ||
                trial.CartObserved || trial.CartKitReclaimed ||
                trial.ClawsObserved, Is.False);
            MoveTestLeader(playerBody, SoundwellTrial.SurveyorPosition);
            Assert.That(trial.AtSurveyor, Is.True);
            var contextualHint = GameObject.Find("Soundwell trial hint")?.GetComponent<Text>();
            Assert.That(contextualHint, Is.Not.Null);
            SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
            yield return null;
            Assert.That(contextualHint.text,
                Is.EqualTo(SoundwellUiLocalization.Get("hint.surveyor_ask")),
                "The live unasked surveyor prompt must switch into Chinese.");
            SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
            yield return null;
            var worldPose = trial.PlayerActor.GetComponentInChildren<SoundwellPlayerVisualAnimation>();
            Assert.That(worldPose, Is.Not.Null);
            float worldPoseDeadline = Time.realtimeSinceStartup + 3f;
            while (worldPose.CurrentState != "Idle" && Time.realtimeSinceStartup < worldPoseDeadline)
                yield return null;
            Assert.That(worldPose.CurrentState, Is.EqualTo("Idle"));
            trial.ApplyInput(Vector2.zero, false, false, false, false, true);
            Assert.That(trial.HasSurveyorClue, Is.True);
            Assert.That(worldPose.CurrentState, Is.EqualTo("Action"),
                "A real spatial world interaction should use the same player action cue as a real bell pickup.");
            yield return new WaitForSecondsRealtime(0.45f);
            yield return null;
            Assert.That(worldPose.CurrentState, Is.EqualTo("Idle"));
            trial.ApplyInput(Vector2.zero, false, false, false, false, true);
            Assert.That(worldPose.CurrentState, Is.EqualTo("Idle"),
                "The already-earned clue must not animate a fake second inspection.");
            // The actual survivor's warning changes language without
            // rewriting Makinom's plank evidence or advancing the rescue.
            var plankSpeech = GameObject.Find("Soundwell surveyor response")?.GetComponent<Text>();
            Assert.That(plankSpeech, Is.Not.Null);
            SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
            yield return null;
            Assert.That(plankSpeech.text, Is.EqualTo(
                SoundwellUiLocalization.Get("surveyor.plank_clue")));
            Assert.That(plankSpeech.text, Does.Contain("路标"));
            Assert.That(SoundwellUiLocalization.Get("surveyor.plank_unsafe"),
                Does.Contain("撑不住"));
            Assert.That(SoundwellUiLocalization.Get("surveyor.plank_snapped"),
                Does.Contain("木板断了"));
            SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
            yield return null;
            Assert.That(plankSpeech.text, Is.EqualTo(
                SoundwellUiLocalization.Get("surveyor.plank_clue")));
            MoveTestLeader(playerBody, SoundwellTrial.UpperBoardPosition);
            Assert.That(trial.CanSecurePlank, Is.True);
            SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
            yield return null;
            Assert.That(contextualHint.text,
                Is.EqualTo(SoundwellUiLocalization.Get("hint.plank_brace")),
                "The actual brace-plank instruction must be localized before taking the action.");
            SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
            yield return null;
            trial.ApplyInput(Vector2.zero, false, false, false, false, true);
            Assert.That(trial.PlankSecured, Is.True);
            SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
            yield return null;
            Assert.That(plankSpeech.text, Is.EqualTo(
                SoundwellUiLocalization.Get("surveyor.plank_braced")));
            Assert.That(plankSpeech.text, Does.Contain("固定好了"));
            Assert.That(trial.PlankSecured, Is.True);
            SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
            yield return null;
            Assert.That(plankSpeech.text, Is.EqualTo(
                SoundwellUiLocalization.Get("surveyor.plank_braced")));

            deadline = Time.realtimeSinceStartup + 10f;
            while (!trial.CanEscortSurveyor && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(trial.CanEscortSurveyor, Is.True);
            Assert.That(trial.CanTieEscortLine, Is.False,
                "No rope must disable only the optional tied-guide action.");
            trial.ApplyInput(Vector2.zero, false, false, false, false, true);
            Assert.That(trial.SurveyorEscorting, Is.True);
            Assert.That(trial.SurveyorLineTied, Is.False);

            // Only the test-controlled leader moves in small increments. The
            // survivor must follow under his actual scene-local movement logic;
            // no NPC teleport, direct reward flag or forced route completion.
            // The saved escort route deliberately waits on the braced plank
            // until the player first crosses the gate (leader.x <= 0.85).
            // Do not demand close following while the survivor is waiting.
            MoveTestLeader(playerBody, new Vector2(1.4f, 0.7f));
            yield return null;
            Assert.That(Vector2.Distance(trial.SurveyorWorldPosition,
                SoundwellTrial.UpperBoardPosition), Is.LessThan(0.18f),
                "The surveyor must wait on the proven plank until the guide crosses the gate.");
            // The *live* trapped surveyor's gate instruction must be readable
            // in the selected language while he waits on the real plank;
            // changing language must not advance the escort or gate.
            var plankResponse = GameObject.Find("Soundwell surveyor response")?.GetComponent<Text>();
            Assert.That(plankResponse, Is.Not.Null);
            SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
            yield return null;
            Assert.That(trial.SurveyorEscorting, Is.True);
            Assert.That(trial.RescueComplete, Is.False);
            Assert.That(plankResponse.text, Is.EqualTo(
                SoundwellUiLocalization.Get("surveyor.wait_at_plank")));
            Assert.That(plankResponse.text, Does.Contain("踏板"));
            Assert.That(SoundwellUiLocalization.Get("surveyor.following"),
                Does.Contain("身后"));
            Assert.That(SoundwellUiLocalization.Get("hint.escort_gate_first"),
                Does.Contain("闸门"));
            SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
            yield return null;
            Assert.That(plankResponse.text, Is.EqualTo(
                SoundwellUiLocalization.Get("surveyor.wait_at_plank")));
            var firstSteps = new[]
            {
                new Vector2(0.6f, 0.0f), new Vector2(-0.3f, 0.0f)
            };
            foreach (Vector2 step in firstSteps)
            {
                MoveTestLeader(playerBody, step);
                deadline = Time.realtimeSinceStartup + 8f;
                while (Vector2.Distance(trial.SurveyorWorldPosition, step) > 1.35f &&
                       Time.realtimeSinceStartup < deadline)
                    yield return null;
                Assert.That(Vector2.Distance(trial.SurveyorWorldPosition, step),
                    Is.LessThanOrEqualTo(1.35f),
                    "Without a rope, the actual surveyor must remain close to the guide.");
            }
            for (float x = -1.15f; x > safe.x + 0.4f; x -= 0.9f)
            {
                Vector2 step = new Vector2(x, safe.y);
                MoveTestLeader(playerBody, step);
                deadline = Time.realtimeSinceStartup + 8f;
                while (Vector2.Distance(trial.SurveyorWorldPosition, step) > 1.35f &&
                       Time.realtimeSinceStartup < deadline)
                    yield return null;
                Assert.That(Vector2.Distance(trial.SurveyorWorldPosition, step),
                    Is.LessThanOrEqualTo(1.35f),
                    "The survivor must physically follow each return waypoint.");
            }
            // The real escort hysteresis must be readable on the actual
            // 16px surveyor, not only in a HUD message. Keep the NPC's route
            // and state under the scene's own movement/refresh logic.
            var surveyorSprite = GameObject.Find("PLACEHOLDER trapped surveyor")
                ?.GetComponent<SpriteRenderer>();
            Assert.That(surveyorSprite, Is.Not.Null);
            yield return null;
            Color followingColor = surveyorSprite.color;
            Vector2 pausePosition = trial.SurveyorWorldPosition;
            Assert.That(pausePosition.x, Is.LessThan(-0.65f),
                "The leader-distance pause begins only after the surveyor crosses the gate.");
            // Move to the real open gateway, well beyond the 3.15-unit
            // no-rope breakaway threshold, not a guessed offset that can
            // land inside geometry. Wait for the live LateUpdate to publish
            // its escort hysteresis before asserting the display state.
            MoveTestLeader(playerBody, new Vector2(1.4f, 0f));
            deadline = Time.realtimeSinceStartup + 3f;
            while (!trial.SurveyorWaitingForLeader && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(trial.SurveyorWaitingForLeader, Is.True,
                "The NPC must actually pause when the guide leaves; NPC=" +
                trial.SurveyorWorldPosition + ", leader=" + playerBody.position +
                ", completed=" + trial.RescueComplete);
            yield return null;
            Assert.That(surveyorSprite.color,
                Is.EqualTo(new Color(1f, 0.73f, 0.42f)),
                "Abandoning the guide must visibly amber-mark the waiting surveyor.");
            Assert.That(Vector2.Distance(trial.SurveyorWorldPosition, pausePosition),
                Is.LessThan(0.05f),
                "The waiting surveyor must not teleport to the distant guide.");
            // Language changes while the NPC is genuinely waiting must update
            // both instructions without moving the NPC or changing escort state.
            var pausedHint = GameObject.Find("Soundwell trial hint")?.GetComponent<Text>();
            var pausedResponse = GameObject.Find("Soundwell surveyor response")?.GetComponent<Text>();
            Assert.That(pausedHint, Is.Not.Null);
            Assert.That(pausedResponse, Is.Not.Null);
            SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
            yield return null;
            Assert.That(trial.SurveyorWaitingForLeader, Is.True);
            Assert.That(pausedHint.text, Is.EqualTo(
                SoundwellUiLocalization.Get("hint.escort_paused")));
            Assert.That(pausedResponse.text, Is.EqualTo(
                SoundwellUiLocalization.Get("surveyor.wait_for_leader")));
            Assert.That(pausedHint.text, Does.Contain("护送暂停"));
            Assert.That(pausedResponse.text, Does.Contain("走得太远"));
            Assert.That(SoundwellUiLocalization.Get("hint.escape_blocked"),
                Does.Contain("撤离受阻"));
            Assert.That(SoundwellUiLocalization.Get("surveyor.beast_gate"),
                Does.Contain("野兽"));
            SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
            yield return null;
            Assert.That(trial.SurveyorWaitingForLeader, Is.True);
            Assert.That(pausedHint.text, Is.EqualTo(
                SoundwellUiLocalization.Get("hint.escort_paused")));
            Assert.That(pausedResponse.text, Is.EqualTo(
                SoundwellUiLocalization.Get("surveyor.wait_for_leader")));
            if (retryDuringEscortPause)
            {
                // The player has returned to RETREAT but has not brought the
                // actual surveyor home. Retry must not bank a false rescue or
                // leave the waiting (amber) NPC from the abandoned attempt.
                MoveTestLeader(playerBody, safe);
                Assert.That(trial.RescueComplete, Is.False);
                trial.ApplyInput(Vector2.zero, false, false, true);
                Assert.That(trial.SurveyorWaitingForLeader, Is.False);
                Assert.That(surveyorSprite.color, Is.EqualTo(followingColor),
                    "Retry must clear the amber waiting sprite before the next render.");
                Assert.That(Vector2.Distance(trial.SurveyorWorldPosition,
                    SoundwellTrial.SurveyorPosition), Is.LessThan(0.001f),
                    "Retry must return the actual survivor to the trapped position.");
                Assert.That(trial.SurveyorEscorting, Is.False);
                Assert.That(trial.GateOpen, Is.False);
                Assert.That(trial.RescueComplete, Is.False);
                yield return null;
                MoveTestLeader(playerBody, new Vector2(-0.55f, 0f));
                Assert.That(trial.CanEngageBeast, Is.True,
                    "An abandoned bell escort must still allow the original combat route.");
                yield break;
            }
            MoveTestLeader(playerBody, pausePosition);
            deadline = Time.realtimeSinceStartup + 3f;
            while (trial.SurveyorWaitingForLeader && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(trial.SurveyorWaitingForLeader, Is.False,
                "Returning to the NPC must clear the same escort pause state.");
            yield return null;
            Assert.That(surveyorSprite.color, Is.EqualTo(followingColor),
                "Returning to the surveyor must restore the normal follow sprite.");
            Assert.That(trial.RescueComplete, Is.False,
                "Near the exit is not equivalent to bringing the survivor all the way home.");
            MoveTestLeader(playerBody, safe);
            deadline = Time.realtimeSinceStartup + 15f;
            while (!trial.RescueComplete && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(trial.RescueComplete, Is.True,
                "The noncombat route must complete only when the actual survivor reaches RETREAT.");
            AssertRescueReward(party);
            Assert.That(Vector2.Distance(trial.SurveyorWorldPosition, safe),
                Is.LessThanOrEqualTo(0.98f));
            Assert.That(trial.RopeRecovered || trial.CartKitReclaimed ||
                trial.DirectCombatComplete, Is.False);

            // The survivor's first response and rescue completion hint must
            // both follow the selected EN/zh UI locale on the *live* scene,
            // without rewriting Makinom's rescue state or ORK battle data.
            var rescueHint = GameObject.Find("Soundwell trial hint")?.GetComponent<Text>();
            var survivorResponse = GameObject.Find("Soundwell surveyor response")?.GetComponent<Text>();
            Assert.That(rescueHint, Is.Not.Null);
            Assert.That(survivorResponse, Is.Not.Null);
            SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
            yield return null;
            Assert.That(trial.RescueComplete, Is.True);
            Assert.That(rescueHint.text, Is.EqualTo(
                SoundwellUiLocalization.Get("hint.rescue_complete")));
            Assert.That(survivorResponse.text, Is.EqualTo(
                SoundwellUiLocalization.Get("surveyor.rescued")));
            Assert.That(rescueHint.text, Does.Contain("救援成功"));
            Assert.That(survivorResponse.text, Does.Contain("测路人"));
            // The earned post-rescue surveyor, physical evidence and cart-route
            // feedback must all use saved EN/zh StringTables, not English literals.
            string[] earnedFeedbackKeys =
            {
                "surveyor.route_posted",
                "surveyor.kit_reclaimed",
                "surveyor.cart_mentioned",
                "surveyor.sign_removed",
                "observed.cart",
                "observed.rope",
                "observed.claws",
                "hint.route_posted",
                "hint.kit_reclaimed",
                "hint.cart_tell_nearby",
                "hint.cart_tell_later",
                "hint.cart_fetching",
                "hint.sign_removed",
            };
            string[] chineseFeedback = earnedFeedbackKeys.Select(SoundwellUiLocalization.Get).ToArray();
            Assert.That(chineseFeedback.All(value => value.Any(character => character >= '\u4e00' && character <= '\u9fff')),
                Is.True, "Every earned surveyor and route line must contain readable Chinese.");
            var chineseFont = SoundwellUiLocalization.PresentationFont(
                Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));
            Assert.That(rescueHint.font, Is.SameAs(chineseFont));
            Assert.That(survivorResponse.font, Is.SameAs(chineseFont));
            SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
            yield return null;
            Assert.That(rescueHint.text, Is.EqualTo(
                SoundwellUiLocalization.Get("hint.rescue_complete")));
            Assert.That(survivorResponse.text, Is.EqualTo(
                SoundwellUiLocalization.Get("surveyor.rescued")));
            for (int index = 0; index < earnedFeedbackKeys.Length; index++)
                Assert.That(SoundwellUiLocalization.Get(earnedFeedbackKeys[index]),
                    Is.Not.EqualTo(chineseFeedback[index]),
                    "Switching back to English must restore each authored line.");

            // The optional claw observation is a player-earned clue, not a
            // persistent hint to reveal on an otherwise fresh attempt. Do it
            // after finishing the fixed-cost rescue route to leave the route
            // comparison's recorded interaction counts unchanged.
            Assert.That(trial.ClawsObserved, Is.False);
            MoveTestLeader(playerBody, SoundwellTrial.ClawEvidencePosition);
            Assert.That(trial.CanInspectClaws, Is.True);
            trial.ApplyInput(Vector2.zero, false, false, false, false, true);
            Assert.That(trial.ClawsObserved, Is.True,
                "The existing world interaction must actually earn the claw clue.");
            MoveTestLeader(playerBody, safe);
            // A successful sound-only rescue can be replayed in-place.
            // Invoke the normal Retry input at the real retreat mark: no
            // test-side Makinom variable reset or fake ORK victory.
            Assert.That(trial.PlayerPosition, Is.EqualTo(safe));
            trial.ApplyInput(Vector2.zero, false, false, true);
            yield return null;
            Assert.That(trial.RescueComplete, Is.False,
                "Retry must clear the earned rescue outcome for another attempt.");
            // Retry resets the Makinom encounter, not the completed ORK quest
            // or the earned group experience. A replay must not erase or farm
            // the reward from the first physical escort to safety.
            AssertRescueReward(party);
            Assert.That(trial.GateOpen, Is.False,
                "The noncombat latch must be closed for a fresh route choice.");
            Assert.That(trial.SurveyorEscorting, Is.False,
                "The previous guide cannot remain attached to the next attempt.");
            Assert.That(trial.PlankSecured, Is.False);
            Assert.That(trial.LowerPlankBroken, Is.False,
                "Retry must restore the physical weak plank for a fresh attempt.");
            Assert.That(weakPlank.sprite, Is.SameAs(intactPlankSprite),
                "Retry must restore the intact pixel silhouette as well as collision.");
            Assert.That(trial.HasSurveyorClue, Is.False);
            Assert.That(trial.ClawsObserved, Is.False,
                "Retry must clear the previously inspected claw trail for fair route discovery.");
            Assert.That(trial.IsCarrying || trial.HasBellTarget ||
                trial.IsSearching || trial.IsWrapped, Is.False,
                "Retry must clear the old bell lure, wrap and carry state.");
            Assert.That(Vector2.Distance(trial.BellPosition, initialBell),
                Is.LessThan(0.1f), "The physical bell must return to its saved stand.");
            Assert.That(Vector2.Distance(trial.BeastPosition, initialBeast),
                Is.LessThan(0.1f), "The beast must restart at the original encounter.");
            MoveTestLeader(playerBody, new Vector2(-0.55f, 0f));
            Assert.That(trial.CanEngageBeast, Is.True,
                "After a bell-route replay, the real ORK combat route must be available again.");

            // ORK can refuse a new encounter if its battle owner still holds
            // an earlier start. The visible button must not play a fake
            // successful attack when that real framework call declines it.
            var leaderPose = trial.PlayerActor.GetComponentInChildren<SoundwellPlayerVisualAnimation>();
            Assert.That(leaderPose, Is.Not.Null);
            Assert.That(leaderPose.CurrentState, Is.EqualTo("Idle"));
            var startedField = typeof(SoundwellOrkParty).GetField("battleStarted",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);
            Assert.That(startedField, Is.Not.Null);
            bool originalStarted = (bool)startedField.GetValue(party);
            try
            {
                startedField.SetValue(party, true);
                trial.ApplyInput(Vector2.zero, false, false, false, false, true);
                Assert.That(trial.DirectCombatStarted, Is.False,
                    "A declined ORK encounter must not create a Makinom battle flag.");
                Assert.That(leaderPose.CurrentState, Is.EqualTo("Idle"),
                    "A rejected Engage must not play a successful action animation.");
                Assert.That(SoundwellInputAdapter.IsGameplayContextActive, Is.True,
                    "A failed encounter start must return input to exploration.");
            }
            finally
            {
                startedField.SetValue(party, originalStarted);
            }
            Assert.That(trial.CanEngageBeast, Is.True,
                "Restoring the ORK battle owner must preserve the real encounter option.");

            LogAssert.Expect(UnityEngine.LogType.Log, "RPG_SOUNDWELL_ROUTE_COST:BELL:world=4:turn=0:escort=3");
            Debug.Log("RPG_SOUNDWELL_ROUTE_COST:BELL:world=4:turn=0:escort=3");
            Debug.Log("RPG_SOUNDWELL_BELL_ONLY_RESCUE_VALIDATED");
        }

        // M3/M8: exercise the *live battle camera* through the existing ORK
        // Attack path, not a synthetic scene or a second camera controller.
        // Cover both extreme player positions and narrow viewports without
        // changing the player physics or committing a viewport preference.
        internal static void AssertRescueReward(SoundwellOrkParty party)
        {
            Assert.That(SoundwellOrkParty.RescueSettled, Is.True);
            foreach (var member in party.Members)
                Assert.That(member.Status[SoundwellOrkParty.ExperienceStatus].GetValue(),
                    Is.EqualTo(SoundwellOrkParty.RescueExperience));

            var questSetting = ORK.Quests.Get(SoundwellOrkParty.RescueQuestGuid);
            var restoredQuests = new QuestHandler();
            try
            {
                restoredQuests.LoadGame(ORK.Game.Quests.SaveGame());
                Assert.That(restoredQuests.GetQuest(questSetting)?.IsFinished(), Is.True,
                    "ORK must serialize the completed rescue under its stable quest GUID.");
            }
            finally
            {
                ORK.Game.PlayerHandler.Changed -= restoredQuests.PlayerChanged;
            }

            Assert.That(party.SettleRescue(), Is.True);
            foreach (var member in party.Members)
                Assert.That(member.Status[SoundwellOrkParty.ExperienceStatus].GetValue(),
                    Is.EqualTo(SoundwellOrkParty.RescueExperience),
                    "Settling the same rescue again must not grant duplicate experience.");
        }

        internal static void RecordRescueRouteStart(string route, SoundwellTrial trial, SoundwellOrkParty party)
        {
            Vector2 leader = trial.PlayerPosition;
            Vector2 bell = trial.BellPosition;
            Vector2 beast = trial.BeastPosition;
            string hp = string.Join(",", party.Members.Select(member =>
                System.FormattableString.Invariant($"{member.Status[1].GetValue():F2}")));
            string state = System.FormattableString.Invariant(
                $"{leader.x:F2},{leader.y:F2}|{bell.x:F2},{bell.y:F2}|{beast.x:F2},{beast.y:F2}|{hp}|{party.Beast.Status[1].GetValue():F2}|{trial.GateOpen}|{trial.IsCarrying}|{trial.RescueComplete}");
            string marker = $"RPG_SOUNDWELL_ROUTE_START:{route}:{state}";
            LogAssert.Expect(UnityEngine.LogType.Log, marker);
            Debug.Log(marker);
        }

        private static void VerifyBattleCameraKeepsBothExitsVisible(Rigidbody2D leader)
        {
            var rig = Object.FindFirstObjectByType<SoundwellCameraRig>();
            Assert.That(rig, Is.Not.Null, "Live Soundwell requires the one saved Cinemachine rig.");
            Assert.That(rig.VirtualCamera, Is.Not.Null);
            Assert.That(rig.FramingTarget, Is.Not.Null);
            var camera = rig.GetComponent<Camera>();
            Assert.That(camera, Is.Not.Null);
            Assert.That(camera.orthographic, Is.True);

            Vector2 originalPosition = leader.position;
            float originalAspect = camera.aspect;
            try
            {
                // 21:9 monitors and portrait windows must preserve the same
                // authored exits and pixel framing as the usual desktop ratios.
                foreach (float aspect in new[] { 21f / 9f, 16f / 9f, 4f / 3f, 1f, 3f / 4f, 9f / 16f })
                {
                    camera.aspect = aspect;
                    foreach (float playerX in new[] { -7f, 7f })
                    foreach (float playerY in new[] { -3f, 3f })
                    {
                        MoveTestLeader(leader, new Vector2(playerX, playerY));
                        rig.RefreshFraming();
                        float halfHeight = rig.VirtualCamera.Lens.OrthographicSize;
                        float halfWidth = halfHeight * aspect;
                        Vector3 center = rig.FramingTarget.position;
                        // Both authored source/retreat ends at +/-8.1 require
                        // 0.12 world-unit breathing space. Allow half a source
                        // pixel because the framing target is quantized at 16 PPU.
                        const float requiredEdge = 8.1f + 0.12f - 1f / 32f;
                        Assert.That(halfWidth - Mathf.Abs(center.x),
                            Is.GreaterThanOrEqualTo(requiredEdge),
                            "Cinemachine may follow, but cannot crop either Soundwell exit at aspect " + aspect);
                        Assert.That(halfHeight - Mathf.Abs(center.y),
                            Is.GreaterThanOrEqualTo(3.2f + 0.12f),
                            "Camera follow must keep the trial's upper and lower edges visible.");
                        Assert.That(center.x * SoundwellCameraRig.PixelsPerUnit,
                            Is.EqualTo(Mathf.Round(center.x * SoundwellCameraRig.PixelsPerUnit))
                                .Within(0.001f),
                            "Horizontal framing must stay on the 16-PPU pixel grid.");
                        Assert.That(center.y * SoundwellCameraRig.PixelsPerUnit,
                            Is.EqualTo(Mathf.Round(center.y * SoundwellCameraRig.PixelsPerUnit))
                                .Within(0.001f),
                            "Vertical framing must stay on the 16-PPU pixel grid.");
                    }
                }
            }
            finally
            {
                camera.aspect = originalAspect;
                MoveTestLeader(leader, originalPosition);
                rig.RefreshFraming();
            }
        }

        private static void MoveTestLeader(Rigidbody2D body, Vector2 position)
        {
            // Teleport *only* the player during this focused branching test.
            // The full smoke path separately verifies real walking/input.
            body.linearVelocity = Vector2.zero;
            body.position = position;
            Physics2D.SyncTransforms();
        }

        [UnityTest]
        public IEnumerator ReturnScreenKeepsRescueOutcomeLocalizedAcrossLanguageChanges()
        {
            EditorSceneManager.OpenScene("Assets/Game/Scenes/Bootstrap.unity", OpenSceneMode.Single);
            yield return new EnterPlayMode(expectDomainReload: true);
            double deadline = EditorApplication.timeSinceStartup + 30;
            while ((!Maki.Initialized || !ORK.Initialized) &&
                   EditorApplication.timeSinceStartup < deadline)
                yield return null;
            Assert.That(Maki.Initialized && ORK.Initialized, Is.True);

            var entry = Object.FindFirstObjectByType<SoundwellEntrance>();
            Assert.That(entry, Is.Not.Null, "The saved Bootstrap entry must be present.");
            var variables = Maki.Game.Variables;
            Assert.That(variables, Is.Not.Null);
            SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
            variables.Set(SoundwellTrial.EntryOutcomeKey, 2);
            variables.Set(SoundwellTrial.EntryCartKitKey, 1);
            variables.Set(SoundwellTrial.EntrySafeRouteKey, 1);
            variables.Set(SoundwellTrial.EntryBeastContactsKey, 0);
            entry.RefreshReturnOutcome();
            Assert.That(entry.ReturnSummaryText, Does.Contain("SURVEYOR SAFE"));
            Assert.That(entry.ReturnSummaryText, Does.Contain("MAP KIT RECOVERED"));
            Assert.That(entry.ReturnSummaryText, Does.Contain("SAFE ROUTE POSTED"));

            // A language toggle must refresh the existing summary immediately,
            // even when Makinom's outcome variables have not changed.
            SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
            Assert.That(entry.ReturnSummaryText, Does.Contain("测路人获救"));
            Assert.That(entry.ReturnSummaryText, Does.Contain("地图工具已取回"));
            Assert.That(entry.ReturnSummaryText, Does.Contain("安全返程路线已标记"));
            Assert.That(entry.GetComponentsInChildren<Text>(true).Any(label =>
                label.text.Contains("进入试炼") &&
                label.font.HasCharacter('中')), Is.True,
                "The entry prompt must use the authored Chinese font and translation.");

            variables.Set(SoundwellTrial.EntryBeastContactsKey, 2);
            entry.RefreshReturnOutcome();
            Assert.That(entry.ReturnSummaryText, Does.Contain("遭遇野兽次数：2"));
            variables.Set(SoundwellTrial.EntryOutcomeKey, 0);
            entry.RefreshReturnOutcome();
            Assert.That(entry.ReturnSummaryText, Does.Contain("撤退"));
            Assert.That(entry.ReturnSummaryText, Does.Contain("遭遇野兽次数：2"));
            SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
            Assert.That(entry.ReturnSummaryText, Does.Contain("WITHDREW / BEAST CONTACTS: 2"));

            // A first launch with no return history must stay quiet; a real
            // retreat with no beast contact must still acknowledge the exit.
            variables.Set(SoundwellTrial.EntryBeastContactsKey, 0);
            variables.Set(SoundwellTrial.EntryOutcomeRecordedKey, 0);
            entry.RefreshReturnOutcome();
            Assert.That(entry.ReturnSummaryText, Is.Empty,
                "A fresh entry cannot be misreported as a retreat.");
            variables.Set(SoundwellTrial.EntryOutcomeRecordedKey, 1);
            entry.RefreshReturnOutcome();
            Assert.That(entry.ReturnSummaryText, Does.Contain("WITHDREW / BEAST CONTACTS: 0"),
                "Returning safely without encountering the beast still needs a visible result.");
            SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
            Assert.That(entry.ReturnSummaryText, Does.Contain("撤退"));
            SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);

            // A player must be able to use the same SFX mixer control from
            // actual UGUI, with the button following EN/zh and the saved mute
            // choice surviving fresh AudioSource routing.
            Button sfxButton = entry.GetComponentsInChildren<Button>(true)
                .Single(button => button.gameObject.name == "Soundwell SFX toggle");
            Text sfxLabel = sfxButton.GetComponentInChildren<Text>();
            AudioMixer mixer = Resources.Load<AudioMixer>(SoundwellAudioBus.MixerResource);
            Assert.That(mixer, Is.Not.Null);
            Assert.That(sfxLabel, Is.Not.Null);
            bool hadMutePref = PlayerPrefs.HasKey(SoundwellAudioBus.MutePreferenceKey);
            int oldMutePref = PlayerPrefs.GetInt(SoundwellAudioBus.MutePreferenceKey, 0);
            try
            {
                bool before = SoundwellAudioBus.IsMuted;
                Assert.That(sfxLabel.text, Is.EqualTo(SoundwellUiLocalization.Get(
                    before ? "entry.sfx_unmute" : "entry.sfx_mute")));
                sfxButton.onClick.Invoke();
                Assert.That(SoundwellAudioBus.IsMuted, Is.EqualTo(!before));
                Assert.That(SoundwellAudioBus.TryGetVolume(mixer, out float changedDb), Is.True);
                Assert.That(changedDb, Is.EqualTo(before ? 0f : -80f).Within(0.1f));
                Assert.That(sfxLabel.text, Is.EqualTo(SoundwellUiLocalization.Get(
                    before ? "entry.sfx_mute" : "entry.sfx_unmute")));

                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
                Assert.That(sfxLabel.text, Is.EqualTo(SoundwellUiLocalization.Get(
                    before ? "entry.sfx_mute" : "entry.sfx_unmute")));
                Assert.That(sfxLabel.font.HasCharacter('音'), Is.True);
                var bell = new GameObject("SFX button routed bell").AddComponent<AudioSource>();
                try
                {
                    SoundwellAudioBus.Route(bell);
                    Assert.That(bell.outputAudioMixerGroup.name, Is.EqualTo("SFX"));
                    Assert.That(SoundwellAudioBus.TryGetVolume(mixer, out float routedDb), Is.True);
                    Assert.That(routedDb, Is.EqualTo(changedDb).Within(0.1f));
                }
                finally
                {
                    Object.Destroy(bell.gameObject);
                }

                sfxButton.onClick.Invoke();
                Assert.That(SoundwellAudioBus.IsMuted, Is.EqualTo(before));
                Assert.That(sfxLabel.text, Is.EqualTo(SoundwellUiLocalization.Get(
                    before ? "entry.sfx_unmute" : "entry.sfx_mute")));
            }
            finally
            {
                SoundwellAudioBus.SetMuted(mixer, oldMutePref != 0);
                if (!hadMutePref)
                    PlayerPrefs.DeleteKey(SoundwellAudioBus.MutePreferenceKey);
                PlayerPrefs.Save();
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
            }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator PlayerLanguageChoiceSurvivesFreshPlayAndUpdatesEntryUi()
        {
            // Simulate a player toggling language before a full Play Mode
            // domain restart; the next Bootstrap UI must be born in Chinese.
            const string key = SoundwellUiLocalization.LanguagePreferenceKey;
            // UnityTest domain reloads do not retain captured coroutine
            // locals reliably. Preserve the previous editor session values
            // outside the test iterator so cleanup cannot hide the real
            // Play Mode assertion failure or discard a developer's setting.
            const string saved = "rpg.soundwell.m9-test.saved.";
            SessionState.SetBool(saved + "has", PlayerPrefs.HasKey(key));
            SessionState.SetString(saved + "value", PlayerPrefs.GetString(key, string.Empty));
            SessionState.SetString(saved + "language", SoundwellUiLocalization.Language);
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                SoundwellUiLocalization.Toggle();
                Assert.That(SoundwellUiLocalization.Language,
                    Is.EqualTo(SoundwellUiLocalization.Chinese));
                Assert.That(PlayerPrefs.GetString(key),
                    Is.EqualTo(SoundwellUiLocalization.Chinese),
                    "The player's own UI toggle must persist the chosen language.");

                // Non-player diagnostic changes must not overwrite the
                // preference that a fresh game session will restore.
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                Assert.That(PlayerPrefs.GetString(key),
                    Is.EqualTo(SoundwellUiLocalization.Chinese));

                EditorSceneManager.OpenScene("Assets/Game/Scenes/Bootstrap.unity", OpenSceneMode.Single);
                yield return new EnterPlayMode(expectDomainReload: true);
                Assert.That(SoundwellUiLocalization.Language,
                    Is.EqualTo(SoundwellUiLocalization.Chinese),
                    "The language must restore before a new Bootstrap scene is presented.");
                var entry = Object.FindFirstObjectByType<SoundwellEntrance>();
                Assert.That(entry, Is.Not.Null);
                Assert.That(entry.GetComponentsInChildren<Text>(true).Any(label =>
                    label.text.Contains("进入试炼") && label.font.HasCharacter('中')), Is.True,
                    "The freshly constructed entry UI must use the saved Chinese table and glyphs.");
                yield return new ExitPlayMode();
            }
            finally
            {
                string previousLanguage = SessionState.GetString(saved + "language",
                    SoundwellUiLocalization.English);
                SoundwellUiLocalization.SetLanguage(
                    previousLanguage == SoundwellUiLocalization.Chinese
                        ? SoundwellUiLocalization.Chinese : SoundwellUiLocalization.English);
                if (SessionState.GetBool(saved + "has", false))
                    PlayerPrefs.SetString(key, SessionState.GetString(saved + "value",
                        SoundwellUiLocalization.English));
                else
                    PlayerPrefs.DeleteKey(key);
                PlayerPrefs.Save();
            }
        }

        [UnityTearDown]
        public IEnumerator RestoreEditMode()
        {
            if (Application.isPlaying)
                yield return new ExitPlayMode();
        }

        private static IEnumerator RunTrialInPlayerLoop()
        {
            double deadline = EditorApplication.timeSinceStartup + 30;
            while ((!Maki.Initialized || !ORK.Initialized) && EditorApplication.timeSinceStartup < deadline)
                yield return null;

            Assert.That(Maki.Initialized && ORK.Initialized, Is.True, "The saved Bootstrap project must start.");
            // Bootstrap has its own 30-second watchdog. The full end-to-end
            // play-through now contains an additional real cart inspection,
            // physical rescue, sign correction and map-kit recovery. Give the
            // scenario a separate bounded window; individual interaction
            // waits in the smoke host still fail after 10 seconds.
            deadline = EditorApplication.timeSinceStartup + 90;
            var root = new GameObject("Soundwell CI runner");
            Object.DontDestroyOnLoad(root);
            var host = root.AddComponent<SoundwellSmokeHost>();
            LogAssert.Expect(UnityEngine.LogType.Log, "RPG_SOUNDWELL_SIGNAL_VALIDATED");
            host.Run();
            while (!host.ExitCode.HasValue && EditorApplication.timeSinceStartup < deadline)
                yield return null;
            Assert.That(host.ExitCode, Is.EqualTo(0), "The real Play Mode trial must complete.");
            LogAssert.NoUnexpectedReceived();
        }
    }
}
