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
        public void BeastContactFollowsTrialOriginAndRearmsAfterExit()
        {
            // Keep all three translated positions in one strictly named NUnit
            // case so the P0 exact-manifest collector can verify every run.
            foreach (float originX in new[] { -9f, 0f, 12f })
                AssertContactAtOrigin(originX);
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
