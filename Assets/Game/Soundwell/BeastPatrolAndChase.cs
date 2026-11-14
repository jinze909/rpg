using System;
using UnityEngine;

namespace Rpg.Soundwell
{
    // One visual beast motor, one Makinom bell state. Contacts and search
    // expiry are events, never direct writes to gates, quests or checkpoints.
    public sealed class BeastPatrolAndChase : MonoBehaviour
    {
        public const float ContactRadius = 0.67f;
        public const float SearchDurationSeconds = 3.5f;
        private Transform beast;
        private Rigidbody2D player;
        private Vector2 home;
        private Vector2 lastHeard;
        private float searchStarted = float.NegativeInfinity;
        // One contact is one caught event until the player actually leaves
        // the hazard; consecutive render/physics updates must not count as
        // separate captures of an overlapping rescuer.
        private bool contactLatched;

        public event Action SearchExpired;
        public event Action PlayerCaught;

        public Vector2 Position => beast != null ? (Vector2)beast.position : home;
        public bool WithinWarningRange(Vector2 worldPoint, float radius) =>
            beast != null && Vector2.Distance(Position, worldPoint) < radius;
        public float SearchRemainingFraction => Mathf.Clamp01(
            (SearchDurationSeconds - (Time.time - searchStarted)) / SearchDurationSeconds);

        public void Configure(Transform actor, Rigidbody2D leader, Vector2 originalPosition)
        {
            beast = actor != null ? actor : throw new ArgumentNullException(nameof(actor));
            player = leader != null ? leader : throw new ArgumentNullException(nameof(leader));
            home = originalPosition;
            ResetPatrol();
        }

        public void ResetPatrol()
        {
            if (beast != null)
            {
                beast.position = home;
                beast.rotation = Quaternion.Euler(0f, 0f, 180f);
            }
            lastHeard = home;
            searchStarted = float.NegativeInfinity;
            contactLatched = false;
        }

        public void OnBellSignal(BellSignal signal, Vector2 soundPoint)
        {
            if (signal == BellSignal.Ringing)
                lastHeard = soundPoint;
            if (signal == BellSignal.Searching)
            {
                lastHeard = soundPoint;
                searchStarted = Time.time;
            }
        }

        public Vector2 Tick(BellSignal bellSignal, Vector2 hearingPoint, bool directCombat,
            bool combatComplete, bool gateOpen, bool rescueComplete)
        {
            Vector2 destination = Position;
            if (!directCombat && !combatComplete && beast != null)
            {
                destination = home;
                if (bellSignal == BellSignal.Ringing)
                {
                    destination = hearingPoint;
                    lastHeard = destination;
                }
                else if (bellSignal == BellSignal.Searching)
                {
                    float time = Time.time - searchStarted;
                    if (time >= SearchDurationSeconds)
                        SearchExpired?.Invoke();
                    else
                        destination = lastHeard + new Vector2(
                            0.45f + 0.45f * Mathf.Cos(time * 2f),
                            0.8f * Mathf.Sin(time * 2f));
                }
                Vector2 position = beast.position;
                Vector2 next = Vector2.MoveTowards(position, destination,
                    2.5f * Time.deltaTime);
                Vector2 facing = next - position;
                if (facing.sqrMagnitude > 0.000001f)
                    beast.rotation = Quaternion.Euler(0f, 0f,
                        Mathf.Atan2(facing.y, facing.x) * Mathf.Rad2Deg);
                beast.position = next;
            }

            bool playerInDanger = beast != null && player != null &&
                !directCombat && !combatComplete && gateOpen && !rescueComplete &&
                player.position.x > 0.55f &&
                Vector2.Distance(player.position, Position) <= ContactRadius;
            if (!playerInDanger)
                contactLatched = false;
            else if (!contactLatched)
            {
                // Latch before notifying: a checkpoint callback may relocate
                // the Rigidbody or re-enter scene updates immediately.
                contactLatched = true;
                PlayerCaught?.Invoke();
            }

            return destination;
        }

        public bool IsReturning(bool directCombat, bool combatComplete, BellSignal signal) =>
            !directCombat && !combatComplete && beast != null &&
            signal != BellSignal.Ringing && signal != BellSignal.Searching &&
            Vector2.Distance(beast.position, home) > 0.15f;
    }
}
