using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    // Pure game-owned service regression. ORK combat, Makinom save state,
    // scene loading and the input adapter are verified by the full trial tests.
    public sealed class SoundwellTrialServiceTests
    {
        [Test]
        public void ExistingMakinomSignalIdsAndBellValuesRemainStable()
        {
            Assert.That(SoundwellSignalKeys.SignalKey,
                Is.EqualTo("rpg.soundwell.signal-trial.bell"));
            Assert.That(SoundwellSignalKeys.GateKey,
                Is.EqualTo("rpg.soundwell.signal-trial.inner-gate"));
            Assert.That(SoundwellSignalKeys.RescueKey,
                Is.EqualTo("rpg.soundwell.signal-trial.surveyor-rescued"));
            Assert.That((int)BellSignal.Ready, Is.EqualTo(0));
            Assert.That((int)BellSignal.Ringing, Is.EqualTo(1));
            Assert.That((int)BellSignal.Wrapped, Is.EqualTo(2));
            Assert.That((int)BellSignal.Searching, Is.EqualTo(3));
            Assert.That(SoundwellTrial.EntryOutcomeKey,
                Is.EqualTo(SoundwellSignalKeys.EntryOutcomeKey));
        }

        [Test]
        public void BeastSignalsControlDestinationAndCaptureOnlyInAnOpenDangerLane()
        {
            var root = new GameObject("isolated beast service");
            var leader = new GameObject("isolated leader");
            var actor = new GameObject("isolated beast");
            try
            {
                var body = leader.AddComponent<Rigidbody2D>();
                body.gravityScale = 0f;
                actor.transform.position = new Vector3(3f, 0f, 0f);
                var patrol = root.AddComponent<BeastPatrolAndChase>();
                patrol.Configure(actor.transform, body, actor.transform.position);
                int contacts = 0;
                patrol.PlayerCaught += () => contacts++;

                Vector2 bellTarget = new Vector2(1.15f, -1.95f);
                patrol.OnBellSignal(BellSignal.Ringing, bellTarget);
                Vector2 target = patrol.Tick(BellSignal.Ringing, bellTarget,
                    false, false, false, false);
                Assert.That(target, Is.EqualTo(bellTarget),
                    "The real bell position must drive pursuit, not a UI target.");

                // The same physical contact cannot count if the locked gate
                // separates the player, the survivor is safe, or ORK owns turns.
                body.position = new Vector2(3f, 0f);
                actor.transform.position = new Vector3(3f, 0f, 0f);
                patrol.Tick(BellSignal.Ready, bellTarget, false, false, false, false);
                patrol.Tick(BellSignal.Ready, bellTarget, false, false, true, true);
                patrol.Tick(BellSignal.Ready, bellTarget, true, false, true, false);
                patrol.Tick(BellSignal.Ready, bellTarget, false, true, true, false);
                Assert.That(contacts, Is.Zero);
                patrol.Tick(BellSignal.Ready, bellTarget, false, false, true, false);
                Assert.That(contacts, Is.EqualTo(1),
                    "Only a live open-lane encounter reports player capture.");

                // The 2D service raises an event. It never teleports the
                // player or writes a second beast-contact/quest counter.
                Assert.That(body.position, Is.EqualTo(new Vector2(3f, 0f)));
                patrol.ResetPatrol();
                Assert.That(patrol.Position, Is.EqualTo(new Vector2(3f, 0f)));
                Assert.That(patrol.IsReturning(false, false, BellSignal.Ready),
                    Is.False);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(leader);
                Object.DestroyImmediate(actor);
            }
        }
    }
}
