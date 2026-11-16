using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    // The rescue lane is authored relative to the Soundwell trial root.
    // Moving the entire trial must not create phantom captures on the west
    // side or make the east-side beast harmless.
    public sealed class BeastPatrolCorridorTests
    {
        [Test]
        public void CarriedBellHandOffsetTracksRotatedTrialAndPhysicalLeader()
        {
            var root = new GameObject("rotated Soundwell carried-bell root");
            try
            {
                root.transform.position = new Vector3(7f, -3f, 0f);
                var trial = root.AddComponent<SoundwellTrial>();
                Vector2 physicalLeader = new Vector2(9f, 1f);
                foreach (float angle in new[] { 0f, 90f, 180f })
                {
                    root.transform.rotation = Quaternion.Euler(0f, 0f, angle);
                    Vector2 expected = physicalLeader + (Vector2)
                        root.transform.TransformVector(new Vector3(0.35f, 0f, 0f));
                    Vector2 held = trial.CarriedBellWorldPosition(physicalLeader);
                    Assert.That(Vector2.Distance(held, expected), Is.LessThan(0.0001f),
                        "The same bell must be carried to the visible hand in a rotated trial.");
                    Assert.That(Vector2.Distance(held, physicalLeader),
                        Is.EqualTo(0.35f).Within(0.0001f));
                    if (angle == 90f)
                        Assert.That(held.x, Is.EqualTo(physicalLeader.x).Within(0.0001f),
                            "The old fixed world-X offset would put the bell beside the wrong hand.");
                }
                root.transform.localScale = new Vector3(2f, 1f, 1f);
                root.transform.rotation = Quaternion.identity;
                Vector2 scaled = trial.CarriedBellWorldPosition(physicalLeader);
                Assert.That(scaled.x - physicalLeader.x, Is.EqualTo(0.7f).Within(0.0001f),
                    "The offset must follow the authored room scale rather than detach visually.");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void PatrolSweepStopsAtRealSolidWallsAndIgnoresTriggerZones()
        {
            var root = new GameObject("patrol obstruction root");
            var beast = new GameObject("patrolling beast");
            var rescuer = new GameObject("safe player");
            var wall = new GameObject("physical stone wall");
            var trigger = new GameObject("nonblocking trigger");
            try
            {
                var body = rescuer.AddComponent<Rigidbody2D>();
                body.bodyType = RigidbodyType2D.Kinematic;
                var patrol = root.AddComponent<BeastPatrolAndChase>();
                patrol.Configure(beast.transform, body, Vector2.zero);
                wall.transform.position = new Vector3(0.5f, 0f, 0f);
                wall.AddComponent<BoxCollider2D>().size = new Vector2(0.2f, 2f);
                trigger.transform.position = new Vector3(0.25f, 0.7f, 0f);
                var sensor = trigger.AddComponent<BoxCollider2D>();
                sensor.isTrigger = true;
                Physics2D.SyncTransforms();

                var sweep = typeof(BeastPatrolAndChase).GetMethod("ConstrainBeastMotion",
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance);
                Assert.That(sweep, Is.Not.Null);
                Vector2 blocked = (Vector2)sweep.Invoke(patrol,
                    new object[] { Vector2.zero, Vector2.right });
                Assert.That(blocked.x, Is.LessThan(0.40f),
                    "Static wall collision must stop the visible beast before crossing it.");
                Vector2 clear = (Vector2)sweep.Invoke(patrol,
                    new object[] { Vector2.zero, Vector2.up });
                Assert.That(clear.y, Is.EqualTo(1f).Within(0.0001f),
                    "A nonblocking trigger cannot act like a solid wall.");
            }
            finally
            {
                Object.DestroyImmediate(trigger);
                Object.DestroyImmediate(wall);
                Object.DestroyImmediate(rescuer);
                Object.DestroyImmediate(beast);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void BeastContactFollowsTrialOriginAndRearmsAfterExit()
        {
            // Keep all three translated positions in one strictly named NUnit
            // case so the P0 exact-manifest collector can verify every run.
            foreach (float originX in new[] { -9f, 0f, 12f })
                AssertContactAtOrigin(originX);
        }

        [Test]
        public void RetiredBeastDoesNotCaptureOrWarnUntilItReturnsToTheScene()
        {
            var root = new GameObject("Soundwell disabled threat root");
            var beast = new GameObject("retired ORK beast actor");
            var rescuer = new GameObject("rescue player");
            try
            {
                var body = rescuer.AddComponent<Rigidbody2D>();
                body.bodyType = RigidbodyType2D.Kinematic;
                body.gravityScale = 0f;
                body.position = new Vector2(0.72f, 0f);
                var patrol = root.AddComponent<BeastPatrolAndChase>();
                Vector2 beastHome = new Vector2(0.75f, 0f);
                patrol.Configure(beast.transform, body, beastHome);
                int captures = 0;
                patrol.PlayerCaught += () => captures++;

                beast.SetActive(false);
                Assert.That(patrol.WithinWarningRange(body.position, 1.55f), Is.False,
                    "A retired scene actor cannot block the rescue lane.");
                patrol.Tick(BellSignal.Ready, beastHome,
                    false, false, true, false);
                Assert.That(captures, Is.Zero,
                    "A disabled ORK beast must not capture the player invisibly.");

                beast.SetActive(true);
                Assert.That(patrol.WithinWarningRange(body.position, 1.55f), Is.True);
                patrol.Tick(BellSignal.Ready, beastHome,
                    false, false, true, false);
                Assert.That(captures, Is.EqualTo(1),
                    "Reactivating the existing threat restores its physical contact.");
                beast.SetActive(false);
                patrol.Tick(BellSignal.Ready, beastHome,
                    false, false, true, false);
                beast.SetActive(true);
                patrol.Tick(BellSignal.Ready, beastHome,
                    false, false, true, false);
                Assert.That(captures, Is.EqualTo(2),
                    "The contact latch must clear while the actor is absent.");
            }
            finally
            {
                Object.DestroyImmediate(rescuer);
                Object.DestroyImmediate(beast);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void RestoredSearchingBellGetsAFullWindowWithoutPhantomExpiry()
        {
            var root = new GameObject("restored Soundwell search");
            var beast = new GameObject("searching beast");
            var rescuer = new GameObject("safe rescuer");
            try
            {
                var body = rescuer.AddComponent<Rigidbody2D>();
                body.bodyType = RigidbodyType2D.Kinematic;
                body.position = new Vector2(-4f, 0f);
                var patrol = root.AddComponent<BeastPatrolAndChase>();
                patrol.Configure(beast.transform, body, new Vector2(1f, 0f));
                int expired = 0;
                patrol.SearchExpired += () => expired++;

                // The save restores Searching directly; no OnBellSignal
                // callback has run on this newly constructed component.
                patrol.Tick(BellSignal.Searching, new Vector2(2f, 0f),
                    false, false, false, false);
                Assert.That(expired, Is.Zero,
                    "The first restored search frame must not expire immediately.");
                Assert.That(patrol.SearchRemainingFraction, Is.GreaterThan(0.95f));

                patrol.Tick(BellSignal.Searching, new Vector2(2f, 0f),
                    false, false, false, false);
                Assert.That(expired, Is.Zero,
                    "A repeated render frame cannot prematurely consume the search.");

                // A later restored search after leaving the old bell state
                // must start fresh instead of inheriting an expired timer.
                patrol.Tick(BellSignal.Ready, new Vector2(2f, 0f),
                    false, false, false, false);
                patrol.Tick(BellSignal.Searching, new Vector2(3f, 0f),
                    false, false, false, false);
                Assert.That(expired, Is.Zero);
                Assert.That(patrol.SearchRemainingFraction, Is.GreaterThan(0.95f));
            }
            finally
            {
                Object.DestroyImmediate(rescuer);
                Object.DestroyImmediate(beast);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void SearchingBeastScansAndResetsFacingWithRotatedTrial()
        {
            var root = new GameObject("rotated Soundwell search root");
            var beast = new GameObject("rotated searching beast");
            var rescuer = new GameObject("rotated trial rescuer");
            try
            {
                root.transform.position = new Vector3(9f, -4f, 0f);
                root.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
                var body = rescuer.AddComponent<Rigidbody2D>();
                body.bodyType = RigidbodyType2D.Kinematic;
                body.position = root.transform.TransformPoint(new Vector2(-3f, 0f));
                var patrol = root.AddComponent<BeastPatrolAndChase>();
                Vector2 home = root.transform.TransformPoint(new Vector2(3f, 0f));
                patrol.Configure(beast.transform, body, home);

                Quaternion expectedFacing = root.transform.rotation *
                    Quaternion.Euler(0f, 0f, 180f);
                Assert.That(Quaternion.Angle(beast.transform.rotation, expectedFacing),
                    Is.LessThan(0.001f),
                    "The beast must face along the rotated level when reset.");

                Vector2 bell = root.transform.TransformPoint(new Vector2(2f, -0.7f));
                patrol.OnBellSignal(BellSignal.Searching, bell);
                Vector2 scanned = patrol.Tick(BellSignal.Searching, bell,
                    false, false, false, false);
                Vector2 expected = bell + (Vector2)root.transform.TransformVector(
                    new Vector2(0.9f, 0f));
                Assert.That(Vector2.Distance(scanned, expected),
                    Is.LessThan(0.04f),
                    "The beast's real search arc should rotate with the visible bell lane.");
                Assert.That(Vector2.Distance(scanned, bell + new Vector2(0.9f, 0f)),
                    Is.GreaterThan(0.8f),
                    "The legacy unrotated world-X scan is not the moved level's search arc.");

                patrol.ResetPatrol();
                Assert.That(Quaternion.Angle(beast.transform.rotation, expectedFacing),
                    Is.LessThan(0.001f),
                    "A repeated search reset cannot restore the old world-space facing.");
                Assert.That(Vector2.Distance(patrol.Position, home),
                    Is.LessThan(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(rescuer);
                Object.DestroyImmediate(beast);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void WeakPlankContactAndRecoveryFollowTheVisibleTrialRoot()
        {
            var root = new GameObject("offset and rotated Soundwell weak plank");
            try
            {
                root.transform.position = new Vector3(9f, -4f, 0f);
                root.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
                var checkpoint = root.AddComponent<TrialCheckpointManager>();
                Vector2 atPlank = root.transform.TransformPoint(
                    SoundwellTrial.LowerBoardPosition);
                Vector2 nearPlank = root.transform.TransformPoint(
                    SoundwellTrial.LowerBoardPosition + Vector2.right * 0.45f);
                Vector2 outsidePlank = root.transform.TransformPoint(
                    SoundwellTrial.LowerBoardPosition + Vector2.right * 0.5f);
                Vector2 expectedRecovery = root.transform.TransformPoint(
                    new Vector2(2.75f, -0.25f));

                Assert.That(checkpoint.ShouldRecoverAtVisibleLowerPlank(
                    false, true, atPlank), Is.True,
                    "Stepping on the actual rendered weak plank must cause a fall.");
                Assert.That(checkpoint.ShouldRecoverAtVisibleLowerPlank(
                    false, true, nearPlank), Is.True);
                Assert.That(checkpoint.ShouldRecoverAtVisibleLowerPlank(
                    false, true, outsidePlank), Is.False);
                Assert.That(checkpoint.ShouldRecoverAtVisibleLowerPlank(
                    false, true, SoundwellTrial.LowerBoardPosition), Is.False,
                    "The old world-space location must never cause a phantom fall.");
                Assert.That(checkpoint.ShouldRecoverAtVisibleLowerPlank(
                    true, true, atPlank), Is.False,
                    "ORK combat movement cannot trigger an exploration fall.");
                Assert.That(checkpoint.ShouldRecoverAtVisibleLowerPlank(
                    false, false, atPlank), Is.False,
                    "The gated-off plank cannot trigger early recovery.");
                Assert.That(Vector2.Distance(checkpoint.LowerPlankRecoveryWorldPosition,
                    expectedRecovery), Is.LessThan(0.0001f),
                    "Recovery must land beside the moved plank, not at its former world point.");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void AssertContactAtOrigin(float originX)
        {
            var root = new GameObject("translated Soundwell trial root");
            var beast = new GameObject("borrowed-voice beast");
            var rescuer = new GameObject("rescue leader");
            try
            {
                const float laneY = 3f;
                root.transform.position = new Vector3(originX, laneY, 0f);
                Vector2 beastHome = new Vector2(originX + 0.75f, laneY);
                var rescuerBody = rescuer.AddComponent<Rigidbody2D>();
                rescuerBody.bodyType = RigidbodyType2D.Kinematic;
                rescuerBody.gravityScale = 0f;
                var patrol = root.AddComponent<BeastPatrolAndChase>();
                patrol.Configure(beast.transform, rescuerBody, beastHome);
                int contacts = 0;
                patrol.PlayerCaught += () => contacts++;

                // A player on the west side stays safe even while physically
                // within the beast contact radius and after opening the gate.
                rescuerBody.position = new Vector2(originX + 0.50f, laneY);
                patrol.Tick(BellSignal.Ready, beastHome, false, false, true, false);
                Assert.That(contacts, Is.Zero,
                    "The safe corridor must move along with its parent trial.");

                // Gate state is authoritative: proximity alone cannot catch
                // the player when the gate is shut.
                rescuerBody.position = new Vector2(originX + 0.64f, laneY);
                patrol.Tick(BellSignal.Ready, beastHome, false, false, false, false);
                Assert.That(contacts, Is.Zero);
                patrol.Tick(BellSignal.Ready, beastHome, false, false, true, false);
                Assert.That(contacts, Is.EqualTo(1),
                    "Crossing the local danger boundary must trigger capture.");
                patrol.Tick(BellSignal.Ready, beastHome, false, false, true, false);
                Assert.That(contacts, Is.EqualTo(1),
                    "An overlapping rescuer may only be caught once per contact.");

                // Leaving and returning to the hazard starts a fresh contact,
                // but ORK combat and a completed rescue suppress it.
                rescuerBody.position = new Vector2(originX + 0.50f, laneY);
                patrol.Tick(BellSignal.Ready, beastHome, false, false, true, false);
                rescuerBody.position = new Vector2(originX + 0.64f, laneY);
                patrol.Tick(BellSignal.Ready, beastHome, false, false, true, false);
                Assert.That(contacts, Is.EqualTo(2));
                patrol.Tick(BellSignal.Ready, beastHome, true, false, true, false);
                patrol.Tick(BellSignal.Ready, beastHome, false, false, true, true);
                Assert.That(contacts, Is.EqualTo(2),
                    "Combat and completed-rescue state must never emit exploration captures.");
            }
            finally
            {
                Object.DestroyImmediate(rescuer);
                Object.DestroyImmediate(beast);
                Object.DestroyImmediate(root);
            }
        }
    }
}
