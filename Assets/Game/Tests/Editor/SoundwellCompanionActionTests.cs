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
        public void BeastHitRecoilUsesOnlyRealOrkDamageAndPreservesSixteenPixelSprite()
        {
            var flags = System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Static;
            var type = typeof(SoundwellView);
            var damage = type.GetMethod("HasRealOrkBeastDamage", flags);
            var pose = type.GetMethod("WithBeastRecoilPose", flags);
            var idleField = type.GetField("BeastPixels", flags);
            Assert.That(damage, Is.Not.Null);
            Assert.That(pose, Is.Not.Null);
            Assert.That(idleField, Is.Not.Null);

            bool TookDamage(float? prior, float current) =>
                (bool)damage.Invoke(null, new object[] { prior, current });
            Assert.That(TookDamage(null, 10f), Is.False,
                "First ORK HP observation cannot invent an injury.");
            Assert.That(TookDamage(10f, 10f), Is.False);
            Assert.That(TookDamage(10f, 12f), Is.False,
                "Healing is not an attack.");
            Assert.That(TookDamage(10f, 9.9995f), Is.False,
                "A tiny HP roundoff cannot trigger a second hit.");
            Assert.That(TookDamage(10f, 8f), Is.True,
                "A real ORK HP decrease must show the new hit frame.");
            Assert.That(TookDamage(10f, float.NaN), Is.False);
            Assert.That(TookDamage(10f, float.PositiveInfinity), Is.False);

            var idle = (string[])idleField.GetValue(null);
            var recoiling = (string[])pose.Invoke(null, null);
            Assert.That(idle.Length, Is.EqualTo(16));
            Assert.That(recoiling.Length, Is.EqualTo(16));
            Assert.That(recoiling, Is.Not.EqualTo(idle));
            for (int row = 0; row < 16; row++)
            {
                Assert.That(idle[row].Length, Is.EqualTo(16));
                Assert.That(recoiling[row].Length, Is.EqualTo(16),
                    "The hit frame must not change sprite scale or footprint.");
                Assert.That(recoiling[row], row >= 1 && row <= 8
                    ? Is.EqualTo("." + idle[row].Substring(0, 15))
                    : Is.EqualTo(idle[row]),
                    "Only the upper body may recoil; keep both feet anchored.");
            }
        }

        [Test]
        public void SoundwellViewRejectsInvalidActorAndPlankPixelsBeforeTextureAllocation()
        {
            var method = typeof(SoundwellView).GetMethod("CreatePixelSprite",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Static);
            Assert.That(method, Is.Not.Null);
            const string invalidName = "R5 invalid SoundwellView grid allocation";
            var actor = Enumerable.Repeat("................", 16).ToArray();
            var plank = Enumerable.Repeat("..........................", 6).ToArray();
            var brokenActor = (string[])actor.Clone();
            brokenActor[15] = "...";
            var nullActor = (string[])actor.Clone();
            nullActor[6] = null;
            var brokenPlank = (string[])plank.Clone();
            brokenPlank[5] = "..........";

            try
            {
                foreach (string[] rows in new string[][]
                    { null, new string[0], new string[16], brokenActor, nullActor,
                      brokenPlank })
                {
                    object[] args = { invalidName, rows, null };
                    var error = Assert.Throws<System.Reflection.TargetInvocationException>(
                        () => { method.Invoke(null, args); });
                    Assert.That(error.InnerException,
                        Is.TypeOf<System.InvalidOperationException>());
                    Assert.That(args[2], Is.Null,
                        "Invalid art must never expose a newly allocated native texture.");
                    Assert.That(Resources.FindObjectsOfTypeAll<Texture2D>()
                        .Any(t => t.name == invalidName), Is.False,
                        "Malformed actor or plank pixels must not leak Texture2D.");
                }

                // Both dimensions are actually authored in the current scene.
                foreach (string[] rows in new[] { actor, plank })
                {
                    object[] args = { "R5 validated view pose", rows, null };
                    Sprite sprite = null;
                    Texture2D texture = null;
                    try
                    {
                        sprite = (Sprite)method.Invoke(null, args);
                        texture = (Texture2D)args[2];
                        Assert.That(sprite, Is.Not.Null);
                        Assert.That(texture, Is.Not.Null);
                        Assert.That(texture.filterMode, Is.EqualTo(FilterMode.Point));
                        Assert.That(texture.width, Is.EqualTo(rows[0].Length));
                        Assert.That(texture.height, Is.EqualTo(rows.Length));
                        Assert.That(sprite.pixelsPerUnit, Is.EqualTo(16f));
                    }
                    finally
                    {
                        if (sprite != null)
                            Object.DestroyImmediate(sprite);
                        if (texture != null)
                            Object.DestroyImmediate(texture);
                    }
                }
            }
            finally
            {
                foreach (var orphan in Resources.FindObjectsOfTypeAll<Texture2D>()
                    .Where(t => t.name == invalidName))
                    Object.DestroyImmediate(orphan);
            }
        }

        [Test]
        public void S00BridgeheadFocusRecoversGuardWithoutStealingAnIntentionalSelection()
        {
            // Unit-test the same focus decision used each live ORK HUD frame.
            // This does not create a pretend chooser or grant any ORK action.
            var root = new GameObject("S00 temporary focus parent");
            Button MakeButton(string name)
            {
                var obj = new GameObject(name, typeof(RectTransform),
                    typeof(CanvasRenderer), typeof(Image), typeof(Button));
                obj.transform.SetParent(root.transform, false);
                return obj.GetComponent<Button>();
            }

            try
            {
                var attack = MakeButton("ORK attack");
                var guard = MakeButton("ORK guard");
                var retreat = MakeButton("Player selected Leave");
                var stale = MakeButton("Old disabled chooser");
                var method = typeof(WhitewaterDefenseEncounter).GetMethod(
                    "ChooseAvailableActionFocus",
                    System.Reflection.BindingFlags.Static |
                    System.Reflection.BindingFlags.NonPublic);
                Assert.That(method, Is.Not.Null);
                Button Pick(GameObject selected) =>
                    (Button)method.Invoke(null, new object[] { selected, attack, guard, retreat });

                // Preparing/retrying ORK must never expose a phantom command.
                // A gamepad can still select Leave while the chooser is absent.
                var suspend = typeof(WhitewaterDefenseEncounter).GetMethod(
                    "DisableActionsUntilOrkReady",
                    System.Reflection.BindingFlags.Static |
                    System.Reflection.BindingFlags.NonPublic);
                Assert.That(suspend, Is.Not.Null);
                suspend.Invoke(null, new object[] { attack, guard });
                Assert.That(attack.interactable, Is.False);
                Assert.That(guard.interactable, Is.False);
                Assert.That(Pick(attack.gameObject), Is.SameAs(retreat),
                    "Preparing the bridgehead must not select a nonexistent ORK command.");

                attack.interactable = false;
                guard.interactable = true;
                stale.interactable = false;
                Assert.That(Pick(attack.gameObject), Is.SameAs(guard),
                    "A disabled Attack must not trap a gamepad when Guard is legal.");
                Assert.That(Pick(stale.gameObject), Is.SameAs(guard));
                Assert.That(Pick(guard.gameObject), Is.Null,
                    "Already focused legal Guard must not be reset every frame.");
                Assert.That(Pick(retreat.gameObject), Is.Null,
                    "An intentional focused Leave must never be stolen.");
                attack.interactable = true;
                Assert.That(Pick(stale.gameObject), Is.SameAs(attack),
                    "Prefer the legal ORK Attack after a fresh chooser arrives.");
                attack.interactable = false;
                guard.interactable = false;
                Assert.That(Pick(stale.gameObject), Is.SameAs(retreat),
                    "Between ORK turns, gamepad focus must fall back to a real Leave action.");
                Assert.That(Pick(retreat.gameObject), Is.Null,
                    "An existing valid Leave selection must not be stolen.");
                guard.interactable = true;
                guard.gameObject.SetActive(false);
                Assert.That(Pick(stale.gameObject), Is.SameAs(retreat),
                    "A disabled ORK command may not trap focus when Leave is available.");
                retreat.interactable = false;
                Assert.That(Pick(stale.gameObject), Is.Null,
                    "Never select a disabled Leave or fabricate a legal ORK turn.");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void OrkBattleReadoutMustNotMislabelAnActiveNonAttackChooserAsPending()
        {
            // ORK may offer Guard, Gate or its independent step even when the
            // base Attack target is not currently usable. That is still a
            // real active chooser, not a loading/pending battle menu.
            Assert.That(SoundwellOrkParty.IsBattleChooserPending(0, false, 4f), Is.False);
            Assert.That(SoundwellOrkParty.IsBattleChooserPending(1, false, 4f), Is.False);
            Assert.That(SoundwellOrkParty.IsBattleChooserPending(2, false, 1f), Is.False);
            Assert.That(SoundwellOrkParty.IsBattleChooserPending(-1, false, 4f), Is.True,
                "Only an unresolved living-beast turn should show the waiting caption.");
            Assert.That(SoundwellOrkParty.IsBattleChooserPending(-1, true, 0f), Is.False);
            Assert.That(SoundwellOrkParty.IsBattleChooserPending(-1, false, 0f), Is.False,
                "An already-depleted beast must not leave a false pending command.");
            // ORK may disable a combatant's scene actor while its managed
            // chooser index is still being cleared. The UGUI must never
            // enable Attack/Guard/Gate/Move for a retired world actor.
            var actor = new GameObject("retired ORK chooser scene actor");
            try
            {
                var roster = new[] { actor };
                Assert.That(SoundwellTrial.ResolveBattleActor(roster, 0),
                    Is.SameAs(actor.transform));
                actor.SetActive(false);
                Assert.That(SoundwellTrial.ResolveBattleActor(roster, 0), Is.Null,
                    "A disabled combatant must not advertise a live battle chooser.");
                actor.SetActive(true);
                Assert.That(SoundwellTrial.ResolveBattleActor(roster, 0),
                    Is.SameAs(actor.transform),
                    "A re-enabled real actor must be eligible for normal UGUI focus.");
            }
            finally
            {
                Object.DestroyImmediate(actor);
            }

            // Disabled ORK scene actors must be rejected BEFORE the battle
            // owner or any turn is created, including the visual-only beast.
            // A roster of managed Combatants can remain valid after its
            // actual scene actors are deactivated.
            var leader = new GameObject("battle entry leader");
            var second = new GameObject("battle entry second rescuer");
            var third = new GameObject("battle entry third rescuer");
            var beast = new GameObject("battle entry beast");
            var parent = new GameObject("inactive battle entry parent");
            try
            {
                var livingRoster = new[] { leader, second, third };
                Assert.That(SoundwellOrkParty.HasLiveEncounterActors(livingRoster, beast),
                    Is.True);
                beast.SetActive(false);
                Assert.That(SoundwellOrkParty.HasLiveEncounterActors(livingRoster, beast),
                    Is.False, "A retired beast cannot start an invisible battle.");
                beast.SetActive(true);
                second.transform.SetParent(parent.transform);
                parent.SetActive(false);
                Assert.That(SoundwellOrkParty.HasLiveEncounterActors(livingRoster, beast),
                    Is.False, "A rescuer under an inactive parent cannot enter a turn.");
                parent.SetActive(true);
                Assert.That(SoundwellOrkParty.HasLiveEncounterActors(livingRoster, beast),
                    Is.True, "Restoring the scene actor should restore eligibility.");
                Assert.That(SoundwellOrkParty.HasLiveEncounterActors(
                    new GameObject[] { leader, null, third }, beast), Is.False);
                Assert.That(SoundwellOrkParty.HasLiveEncounterActors(
                    new[] { leader, second }, beast), Is.False);
                Assert.That(SoundwellOrkParty.HasLiveEncounterActors(null, beast), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(leader);
                Object.DestroyImmediate(second);
                Object.DestroyImmediate(third);
                Object.DestroyImmediate(beast);
                Object.DestroyImmediate(parent);
            }
        }

        [Test]
        public void MovedSideBellCanBePlacedAtItsVisibleWorldStand()
        {
            var root = new GameObject("translated and rotated bell stand");
            try
            {
                root.transform.position = new Vector3(7f, -5f, 0f);
                root.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
                var mechanism = root.AddComponent<TrialMechanismInteractable>();
                Vector2 visibleStand = root.transform.TransformPoint(
                    SoundwellTrial.SideBellStand);
                Assert.That(Vector2.Distance(
                    mechanism.SideBellStandWorldPosition, visibleStand),
                    Is.LessThan(0.0001f));
                Assert.That(mechanism.TryFindBellStand(visibleStand, out Vector2 placed),
                    Is.True, "A player standing at the visible side bell must be able to place it.");
                Assert.That(Vector2.Distance(placed, visibleStand), Is.LessThan(0.0001f),
                    "The one physical bell must land on the rendered stand.");
                Assert.That(mechanism.TryFindBellStand(
                    visibleStand + Vector2.up * 0.73f, out _), Is.False,
                    "A distant placement must not snap to the side stand.");
                Assert.That(mechanism.TryFindBellStand(
                    SoundwellTrial.SideBellStand, out _), Is.False,
                    "The former world-origin stand must not accept a phantom placement.");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void LowerPlankExplorationKnockbackMustNotOverrideAnOrkStep()
        {
            // The weak plank is a real exploration hazard after the gate opens.
            // In ORK's turn-based battle, the live combatant's accepted step
            // belongs to ORK and may not be overwritten by scene LateUpdate.
            Vector2 board = SoundwellTrial.LowerBoardPosition;
            Assert.That(SoundwellTrial.ShouldRecoverFromLowerPlank(
                false, true, board), Is.True,
                "A rescuer standing on the open-gate weak plank must be pushed back.");
            Assert.That(SoundwellTrial.ShouldRecoverFromLowerPlank(
                false, true, board + Vector2.right * 0.47f), Is.True,
                "The dangerous exploration footprint includes its near edge.");
            Assert.That(SoundwellTrial.ShouldRecoverFromLowerPlank(
                false, true, board + Vector2.right * 0.50f), Is.False,
                "A rescuer outside the weak plank footprint must not be relocated.");
            Assert.That(SoundwellTrial.ShouldRecoverFromLowerPlank(
                false, false, board), Is.False,
                "A closed gate must not trigger an unreachable exploration hazard.");
            Assert.That(SoundwellTrial.ShouldRecoverFromLowerPlank(
                true, false, board), Is.False);
            Assert.That(SoundwellTrial.ShouldRecoverFromLowerPlank(
                true, true, board), Is.False,
                "Opening the gate during battle must not undo ORK-owned movement.");
        }

        [Test]
        public void RotatedTriggerBoxAllowsEmptyCornerButBlocksItsRealBody()
        {
            // ORK's trigger-only actors are excluded from CircleCast, but
            // their visible rotated body must still occupy the true shape.
            // The old world AABB rejected the first unobstructed step.
            var root = new GameObject("Soundwell rotated box motor");
            var moving = new GameObject("moving rescuer");
            var blocker = new GameObject("rotated ORK box actor");
            try
            {
                var motor = root.AddComponent<PartyMovement2D>();
                moving.transform.position = new Vector2(-4f, -1f);
                blocker.transform.position = new Vector2(-3.3f, -0.6f);
                blocker.transform.rotation = Quaternion.Euler(0f, 0f, 45f);
                var box = blocker.AddComponent<BoxCollider2D>();
                box.size = new Vector2(0.3f, 0.8f);
                box.isTrigger = true;
                Physics2D.SyncTransforms();
                var members = new[] { moving, blocker };
                Assert.That(motor.TryStepDestination(moving, Vector2.right,
                    members, null, 0.5f, out Vector2 clearStep), Is.True,
                    "An empty corner of the rotated box's world AABB must be walkable.");
                Assert.That(clearStep, Is.EqualTo(new Vector2(-3.5f, -1f)));

                blocker.transform.position = new Vector2(-3.65f, -1f);
                Physics2D.SyncTransforms();
                Assert.That(motor.TryStepDestination(moving, Vector2.right,
                    members, null, 0.5f, out _), Is.False,
                    "A step passing through the actual rotated box must remain blocked.");
            }
            finally
            {
                Object.DestroyImmediate(blocker);
                Object.DestroyImmediate(moving);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void FormationTrajectoryRingKeepsLatestSamplesWithoutShiftingOldest()
        {
            var root = new GameObject("Soundwell ring sampling regression");
            var leader = new GameObject("trajectory leader");
            var settings = ScriptableObject.CreateInstance<SoundwellPartyConfig>();
            try
            {
                leader.transform.SetParent(root.transform);
                var follower = root.AddComponent<PartyFormationFollower>();
                follower.Configure(leader.transform, root.transform, settings);
                var record = typeof(PartyFormationFollower).GetMethod("RecordLeader",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic);
                var count = typeof(PartyFormationFollower).GetField("historyCount",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic);
                var first = typeof(PartyFormationFollower).GetMethod("HistoryAt",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic);
                Assert.That(record, Is.Not.Null);
                Assert.That(count, Is.Not.Null);
                Assert.That(first, Is.Not.Null);
                for (int i = 1; i <= 80; i++)
                {
                    leader.transform.position = new Vector3(i * 0.05f, 0f, 0f);
                    record.Invoke(follower, null);
                }
                Assert.That((int)count.GetValue(follower), Is.EqualTo(32));
                var oldest = first.Invoke(follower, new object[] { 0 });
                var position = (Vector3)oldest.GetType().GetField("Position").GetValue(oldest);
                Assert.That(position.x, Is.EqualTo(49f * 0.05f).Within(0.001f),
                    "The ring must preserve the most recent 32 actual leader samples.");
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void AuthoredArenaExtentsConstrainTheWholeCombatantFootprint()
        {
            // Future trial rooms can define different collision boundaries.
            var smaller = new Vector2(2f, 1f);
            Assert.That(PartyMovement2D.FitsBattleStepInArena(
                new Vector2(1.7f, 0f), 0.25f, smaller), Is.True);
            Assert.That(PartyMovement2D.FitsBattleStepInArena(
                new Vector2(1.8f, 0f), 0.25f, smaller), Is.False);
            Assert.That(PartyMovement2D.FitsBattleStepInArena(
                new Vector2(0f, -0.8f), 0.25f, smaller), Is.False);
            Assert.That(PartyMovement2D.FitsBattleStepInArena(
                Vector2.zero, 0.1f, new Vector2(-1f, 1f)), Is.False);
            Assert.That(PartyMovement2D.FitsBattleStepInArena(
                Vector2.zero, 0.1f, smaller), Is.True);
        }

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

            // A retired, disabled ORK scene actor has no active collider or
            // movement authority. It must not invisibly block a current
            // rescuer's otherwise legal step in the live roster.
            var motorRoot = new GameObject("Soundwell step occupancy regression");
            var moving = new GameObject("active rescuer");
            var retired = new GameObject("retired rescuer");
            var beast = new GameObject("retired beast");
            try
            {
                var motor = motorRoot.AddComponent<PartyMovement2D>();
                moving.transform.position = new Vector3(-5f, -2f, 0f);
                retired.transform.position = new Vector3(-4.5f, -2f, 0f);
                beast.transform.position = new Vector3(-4.5f, -2f, 0f);
                var team = new[] { moving, retired };

                // First prove these exact positions DO block when active.
                Assert.That(motor.TryStepDestination(moving, Vector2.right,
                    team, null, 0.5f, out _), Is.False);
                retired.SetActive(false);
                Assert.That(motor.TryStepDestination(moving, Vector2.right,
                    team, null, 0.5f, out Vector2 clearStep), Is.True,
                    "A disabled ORK companion must not retain an invisible block.");
                Assert.That(clearStep, Is.EqualTo(new Vector2(-4.5f, -2f)));

                Assert.That(motor.TryStepDestination(moving, Vector2.right,
                    new[] { moving }, beast, 0.5f, out _), Is.False);
                beast.SetActive(false);
                Assert.That(motor.TryStepDestination(moving, Vector2.right,
                    new[] { moving }, beast, 0.5f, out _), Is.True,
                    "A retired beast must not block a post-encounter step.");

                // A trigger is ignored by the solid-world CircleCast, yet a
                // live ORK actor still occupies space. Its rendered pivot may
                // be off the lane while its offset collision footprint is not.
                retired.SetActive(true);
                retired.transform.position = new Vector2(-4.5f, -1.2f);
                var offsetFootprint = retired.AddComponent<CircleCollider2D>();
                offsetFootprint.isTrigger = true;
                offsetFootprint.radius = 0.24f;
                offsetFootprint.offset = new Vector2(0f, -0.5f);
                Physics2D.SyncTransforms();
                Assert.That(motor.TryStepDestination(moving, Vector2.right,
                    team, null, 0.5f, out _), Is.False,
                    "An offset trigger footprint must block overlapping a live companion.");
                offsetFootprint.offset = new Vector2(0f, 0.4f);
                Physics2D.SyncTransforms();
                Assert.That(motor.TryStepDestination(moving, Vector2.right,
                    team, null, 0.5f, out _), Is.True,
                    "Moving the occupied footprint away must reopen the legal step.");

                retired.SetActive(false);
                beast.SetActive(true);
                beast.transform.position = new Vector2(-4.5f, -1.2f);
                var beastFootprint = beast.AddComponent<CircleCollider2D>();
                beastFootprint.isTrigger = true;
                beastFootprint.radius = 0.24f;
                beastFootprint.offset = new Vector2(0f, -0.5f);
                Physics2D.SyncTransforms();
                Assert.That(motor.TryStepDestination(moving, Vector2.right,
                    new[] { moving }, beast, 0.5f, out _), Is.False,
                    "A living beast's offset trigger body cannot be stepped through.");
                beast.SetActive(false);
                moving.SetActive(false);
                Assert.That(motor.TryStepDestination(moving, Vector2.right,
                    team, beast, 0.5f, out _), Is.False,
                    "A disabled rescuer cannot be allowed to move on a stale chooser.");

                // ORK may move a Rigidbody2D first and its sprite Transform
                // may not be sampled until a subsequent frame. Next-step
                // eligibility must start at the actual physics position.
                moving.SetActive(true);
                var movingBody = moving.AddComponent<Rigidbody2D>();
                movingBody.gravityScale = 0f;
                movingBody.bodyType = RigidbodyType2D.Kinematic;
                movingBody.position = new Vector2(-3f, -2f);
                Assert.That(motor.TryStepDestination(moving, Vector2.right,
                    new[] { moving }, null, 0.5f, out Vector2 physicsStep), Is.True,
                    "A live ORK actor must be allowed to step after a Rigidbody relocation.");
                Assert.That(physicsStep, Is.EqualTo(new Vector2(-2.5f, -2f)),
                    "A valid step must originate from Rigidbody2D.position, not stale rendered coordinates.");
            }
            finally
            {
                Object.DestroyImmediate(beast);
                Object.DestroyImmediate(retired);
                Object.DestroyImmediate(moving);
                Object.DestroyImmediate(motorRoot);
            }
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
            var encounterStartedField = typeof(SoundwellTrial).GetField(
                "combatStartedInThisScene", flags);
            Assert.That(encounterStartedField, Is.Not.Null);
            // A defeated ORK encounter cannot simply start again in the
            // same scene: Retry must reload its saved battle components.
            encounterStartedField.SetValue(trial, true);
            Assert.That(trial.CanEngageBeast, Is.False,
                "A retired ORK battle owner cannot offer a second gate encounter.");
            // After an encounter fails the gate is intentionally disabled
            // until a fresh scene. The player must be told where to retry,
            // not shown the usual bell-search hint at the unusable gate.
            yield return null;
            var trialHint = GameObject.Find("Soundwell trial hint")?.GetComponent<Text>();
            Assert.That(trialHint, Is.Not.Null);
            Assert.That(trialHint.text, Is.EqualTo(
                SoundwellUiLocalization.Get("hint.return_to_reset")),
                "A spent ORK encounter must direct the player toward the real Retry point.");
            encounterStartedField.SetValue(trial, false);
            Assert.That(trial.CanEngageBeast, Is.True,
                "A fresh scene must still offer its first genuine ORK battle.");
            trial.ApplyInput(Vector2.zero, false, false, false, false, true);
            Assert.That(trial.DirectCombatStarted, Is.True,
                "The saved world gate must enter a real ORK encounter.");
            Assert.That(trial.ActionMenuOpen, Is.False);
            trial.SetActionMenuOpen(true);
            Assert.That(trial.ActionMenuOpen, Is.False,
                "A late exploration menu callback cannot steal live ORK battle focus.");

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
            // Simulate a delayed view-only ORK callback with a retired
            // scene actor. The managed ORK roster stays intact. Neither
            // primary commands nor the optional step may become clickable.
            var viewField = typeof(TrialFlowController).GetField("view", flags);
            Assert.That(viewField, Is.Not.Null);
            var liveView = (SoundwellView)viewField.GetValue(trial);
            Assert.That(liveView, Is.Not.Null);
            var retiredCallbackActor = new GameObject("retired scene actor callback");
            try
            {
                retiredCallbackActor.SetActive(false);
                liveView.SetBattleChoices(choosing, retiredCallbackActor.transform,
                    true, true, true, false);
                liveView.SetBattleMoveChoices(choosing, retiredCallbackActor.transform,
                    true, true, true, true);
                foreach (string actionName in new[]
                {
                    "Soundwell Attack button", "Soundwell Guard button",
                    "Soundwell Battle Gate button", "Soundwell Move button"
                })
                {
                    var staleCommand = GameObject.Find(actionName)?.GetComponent<Button>();
                    Assert.That(staleCommand, Is.Not.Null);
                    Assert.That(staleCommand.interactable, Is.False,
                        "A disabled scene actor must not expose " + actionName + ".");
                }
                var staleChevron = GameObject.Find("PLACEHOLDER ORK current rescuer")
                    ?.GetComponent<SpriteRenderer>();
                Assert.That(staleChevron, Is.Not.Null);
                Assert.That(staleChevron.enabled, Is.False,
                    "A retired actor must not retain the active ORK chevron.");
            }
            finally
            {
                Object.Destroy(retiredCallbackActor);
            }
            yield return null;
            Assert.That(party.ChoosingRescuerIndex, Is.EqualTo(choosing),
                "Rejecting a stale view callback cannot spend or change the ORK turn.");
            Assert.That(button.interactable, Is.True,
                "The actual living actor must regain its legal ORK attack command.");
            // A delayed UGUI refresh can still ask about yesterday's ORK
            // chooser after a different rescuer receives the live turn.
            // Never offer Guard or Gate to that stale actor index.
            int offTurnRescuer = (choosing + 1) % party.Members.Count;
            Assert.That(party.CanGuard(offTurnRescuer), Is.False,
                "Guard must not be offered to a non-choosing ORK rescuer.");
            Assert.That(party.CanOperateGate(offTurnRescuer, _ => true), Is.False,
                "Gate must not be offered to a non-choosing ORK rescuer.");
            Assert.That(party.TryOperateGate(_ => true, null), Is.False,
                "A missing gate callback must not enqueue an ORK action or consume the turn.");
            Assert.That(party.ChoosingRescuerIndex, Is.EqualTo(choosing));
            Assert.That(party.ReadyBaseAttackerIndex, Is.EqualTo(choosing),
                "Rejecting the invalid gate must leave the real ORK primary action available.");
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
        public IEnumerator RotatedTrialFollowersKeepLocalOffsetsAtSpawnAndCheckpoint()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode(expectDomainReload: true);

            GameObject root = null;
            try
            {
                root = new GameObject("rotated Soundwell formation root");
                root.transform.position = new Vector3(11f, -6f, 0f);
                root.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
                var leader = new GameObject("real formation leader");
                leader.transform.SetParent(root.transform, false);
                leader.transform.localPosition = new Vector3(-2f, 0f, 0f);
                var body = leader.AddComponent<Rigidbody2D>();
                body.bodyType = RigidbodyType2D.Kinematic;
                body.position = leader.transform.position;

                var config = SoundwellPartyConfig.Load();
                var formation = root.AddComponent<PartyFormationFollower>();
                formation.Configure(leader.transform, root.transform, config);
                var second = formation.CreateFollower(1, "rotated second rescuer",
                    config.SecondFollowerOffset, Color.white);
                var third = formation.CreateFollower(2, "rotated third rescuer",
                    config.ThirdFollowerOffset, Color.white);
                foreach (var entry in new[] {
                    (actor: second, offset: config.SecondFollowerOffset),
                    (actor: third, offset: config.ThirdFollowerOffset) })
                {
                    Vector3 expected = leader.transform.position +
                        root.transform.TransformVector(entry.offset);
                    Assert.That(Vector3.Distance(entry.actor.transform.position, expected),
                        Is.LessThan(0.001f),
                        "A follower must spawn beside the real leader in trial-local formation.");
                }

                second.SetActive(true);
                third.SetActive(true);
                formation.SetFrameworkReady();
                Vector2 checkpoint = root.transform.TransformPoint(new Vector2(1f, -0.5f));
                body.position = checkpoint;
                formation.RegroupAfterCheckpoint(checkpoint);
                formation.RenderFollowers(false, false, false);
                foreach (var entry in new[] {
                    (actor: second, offset: config.SecondFollowerOffset),
                    (actor: third, offset: config.ThirdFollowerOffset) })
                {
                    Vector3 expected = (Vector3)checkpoint +
                        root.transform.TransformVector(entry.offset);
                    Assert.That(Vector3.Distance(entry.actor.transform.position, expected),
                        Is.LessThan(0.001f),
                        "Checkpoint regroup and the next follower render must use the rotated formation.");
                }
            }
            finally
            {
                if (root != null)
                    Object.Destroy(root);
            }
            yield return null;
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator MostRecentAcceptedCompanionPoseCannotReplayEarlierAction()
        {
            // ORK may hand the next rescuer a turn before a previous visual
            // one-shot expires. The visible sprite must follow the latest
            // accepted command, not replay stale Attack/Guard/Step art.
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode(expectDomainReload: true);
            var root = new GameObject("Soundwell companion latest-action regression");
            var leader = new GameObject("formation leader");
            leader.transform.SetParent(root.transform, false);
            var formation = root.AddComponent<PartyFormationFollower>();
            try
            {
                formation.Configure(leader.transform, root.transform,
                    SoundwellPartyConfig.Load());
                var second = formation.CreateFollower(1, "rescuer 2",
                    new Vector2(-1f, 0f), Color.white);
                var third = formation.CreateFollower(2, "rescuer 3",
                    new Vector2(-2f, 0f), Color.white);
                second.SetActive(true);
                third.SetActive(true);
                formation.SetFrameworkReady();

                // A rigidbody target can be moved before its Transform has
                // rendered the next physics step. The currently accepted
                // ORK action must face the REAL target on either side.
                var physicalThreat = new GameObject("ORK physics-first facing target");
                physicalThreat.transform.SetParent(root.transform, false);
                var threatBody = physicalThreat.AddComponent<Rigidbody2D>();
                threatBody.bodyType = RigidbodyType2D.Kinematic;
                threatBody.gravityScale = 0f;
                foreach (int index in new[] { 1, 2 })
                {
                    var actor = index == 1 ? second : third;
                    var visual = actor.GetComponent<SpriteRenderer>();
                    formation.ShowAttackPose(index, null);
                    formation.RenderFollowers(true, false, false);
                    Assert.That(visual.sprite.name, Does.Contain("attack"));

                    formation.ShowGuardPose(index, null, true);
                    formation.RenderFollowers(true, false, false);
                    Assert.That(visual.sprite.name, Does.Contain("guard"),
                        "Fresh ORK Guard must not be overwritten by a prior Attack window.");

                    formation.ShowStepPose(index, Vector2.left);
                    formation.RenderFollowers(true, false, false);
                    Assert.That(visual.sprite.name, Does.Contain("walking"),
                        "Fresh ORK Step must not replay the old timed Guard.");

                    formation.ShowAttackPose(index, null);
                    formation.RenderFollowers(true, false, false);
                    Assert.That(visual.sprite.name, Does.Contain("attack"),
                        "Fresh ORK Attack must replace Step without moving the scene actor.");

                    foreach (bool threatOnRight in new[] { true, false })
                    {
                        float physicsX = threatOnRight ? 8f : -8f;
                        physicalThreat.transform.position = new Vector3(-physicsX, 0f, 0f);
                        threatBody.position = new Vector2(physicsX, 0f);
                        formation.ShowAttackPose(index, physicalThreat.transform);
                        Assert.That(visual.flipX, Is.EqualTo(!threatOnRight),
                            "Attack pose must face the Rigidbody2D target even before its Transform catches up.");
                        formation.ShowGuardPose(index, physicalThreat.transform, true);
                        Assert.That(visual.flipX, Is.EqualTo(threatOnRight),
                            "The left-hand guard shield must flip toward the actual physical threat.");
                    }
                }

                // A companion should react to an actual lower ORK HP reading,
                // but never to the first snapshot or repeated unchanged HP.
                // These presentation-only inputs cannot mutate combat state.
                var secondVisual = second.GetComponent<SpriteRenderer>();
                var thirdVisual = third.GetComponent<SpriteRenderer>();
                Color secondTint = secondVisual.color;
                Color thirdTint = thirdVisual.color;
                formation.RefreshCompanionHealth(1, 5f, true);
                formation.RefreshCompanionHealth(2, 5f, true);
                formation.RenderFollowers(true, false, false);
                Assert.That(secondVisual.color, Is.EqualTo(secondTint));
                Assert.That(thirdVisual.color, Is.EqualTo(thirdTint));
                formation.RefreshCompanionHealth(1, 5f, true);
                formation.RefreshCompanionHealth(2, 5f, true);
                formation.RenderFollowers(true, false, false);
                Assert.That(secondVisual.color, Is.EqualTo(secondTint),
                    "An unchanged ORK HP value cannot manufacture a hit flash.");
                Assert.That(thirdVisual.color, Is.EqualTo(thirdTint));
                formation.RefreshCompanionHealth(1, 4f, true);
                formation.RefreshCompanionHealth(2, 4f, true);
                formation.RenderFollowers(true, false, false);
                Assert.That(secondVisual.color, Is.Not.EqualTo(secondTint));
                Assert.That(thirdVisual.color, Is.Not.EqualTo(thirdTint));
                yield return new WaitForSecondsRealtime(0.38f);
                formation.RenderFollowers(true, false, false);
                Assert.That(secondVisual.color, Is.EqualTo(secondTint),
                    "The damage cue must expire independently of ORK turn timing.");
                Assert.That(thirdVisual.color, Is.EqualTo(thirdTint));
                formation.RefreshCompanionHealth(1, 3f, true);
                formation.RefreshCompanionHealth(2, 3f, true);
                formation.RenderFollowers(true, false, false);
                Assert.That(secondVisual.color, Is.Not.EqualTo(secondTint));
                Assert.That(thirdVisual.color, Is.Not.EqualTo(thirdTint));
                formation.ResetPoses();
                formation.RenderFollowers(false, false, false);
                Assert.That(secondVisual.color, Is.EqualTo(secondTint),
                    "Retreat or replay must clear the previous fight's hit cue.");
                Assert.That(thirdVisual.color, Is.EqualTo(thirdTint));
            }
            finally
            {
                Object.Destroy(root);
            }
            yield return null;
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator FollowerExplorationFreezesButBattleCueExpiresDuringPausedOrkClock()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode(expectDomainReload: true);
            var root = new GameObject("paused Soundwell formation");
            var leader = new GameObject("paused formation leader");
            var config = SoundwellPartyConfig.Load();
            float oldScale = Time.timeScale;
            try
            {
                leader.transform.SetParent(root.transform, false);
                var formation = root.AddComponent<PartyFormationFollower>();
                formation.Configure(leader.transform, root.transform, config);
                GameObject actor = formation.CreateFollower(1, "paused rescuer",
                    config.SecondFollowerOffset, Color.white);
                actor.SetActive(true);
                formation.SetFrameworkReady();
                var renderer = actor.GetComponent<SpriteRenderer>();
                Sprite idle = renderer.sprite;
                Vector3 before = actor.transform.position;
                Time.timeScale = 0f;
                leader.transform.position += Vector3.right;
                formation.RenderFollowers(false, false, false);
                Assert.That(Vector3.Distance(actor.transform.position, before),
                    Is.LessThan(0.00001f),
                    "Exploration followers must not slide while Time.timeScale is zero.");
                formation.ShowAttackPose(1, null);
                formation.RenderFollowers(true, false, false);
                Assert.That(renderer.sprite, Is.Not.SameAs(idle),
                    "The real ORK attack still shows a short visible pose.");
                yield return new WaitForSecondsRealtime(0.45f);
                formation.RenderFollowers(true, false, false);
                Assert.That(renderer.sprite, Is.SameAs(idle),
                    "The ORK battle pause must not leave a rescuer stuck attacking.");
            }
            finally
            {
                Time.timeScale = oldScale;
                Object.Destroy(root);
            }
            yield return new ExitPlayMode();
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
                yield return new WaitForSecondsRealtime(0.16f);
                // Two fast contact notifications are one on-screen reaction,
                // not permission to hold the first hit frame indefinitely.
                visual.PlayHit();
                Assert.That(visual.CurrentState, Is.EqualTo("Hit"));
                yield return new WaitForSecondsRealtime(0.24f);
                visual.UpdateMotion(false);
                Assert.That(visual.CurrentState, Is.EqualTo("Idle"),
                    "Repeated contact must not restart or extend an in-flight hit reaction.");
                visual.PlayHit();
                Assert.That(visual.CurrentState, Is.EqualTo("Hit"),
                    "A later, separate hit must still show new feedback.");
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
