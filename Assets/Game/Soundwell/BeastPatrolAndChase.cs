using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rpg.Soundwell
{
    // One visual beast motor, one Makinom bell state. Contacts and search
    // expiry are events, never direct writes to gates, quests or checkpoints.
    public sealed class BeastPatrolAndChase : MonoBehaviour
    {
        public const float ContactRadius = 0.67f;
        public const float SearchDurationSeconds = 3.5f;
        // Local to this trial root, so moving the whole level cannot shift
        // the beast contact zone away from its intended east-side corridor.
        [SerializeField] private float dangerZoneStartLocalX = 0.55f;
        private Transform beast;
        private Rigidbody2D player;
        private Vector2 home;
        private Vector2 lastHeard;
        private float searchStarted = float.NegativeInfinity;
        // Search expiry is one state transition, not one callback per frame.
        // Re-arm only when the bell starts a new search or the trial resets.
        private bool searchExpiryNotified;
        // One contact is one caught event until the player actually leaves
        // the hazard; consecutive render/physics updates must not count as
        // separate captures of an overlapping rescuer.
        private bool contactLatched;

        // First physics-safety increment: visual patrol may not sweep through
        // saved solid Tilemap/BoxCollider walls. A fixed-step Rigidbody motor
        // can replace Transform movement later without removing this clearance.
        [SerializeField, Min(0.05f)] private float beastClearanceRadius = 0.20f;
        private static readonly ContactFilter2D SolidSceneFilter =
            new ContactFilter2D { useTriggers = false };
        private readonly List<RaycastHit2D> solidHits = new List<RaycastHit2D>(8);

        private Vector2 ConstrainBeastMotion(Vector2 from, Vector2 intended)
        {
            Vector2 step = intended - from;
            if (step.sqrMagnitude <= 0.000001f)
                return intended;
            float length = step.magnitude;
            Vector2 direction = step / length;
            Physics2D.CircleCast(from, beastClearanceRadius, direction,
                SolidSceneFilter, solidHits, length);
            float permitted = length;
            foreach (RaycastHit2D hit in solidHits)
            {
                if (hit.collider == null || hit.collider.transform.IsChildOf(beast))
                    continue;
                // ORK characters and the player remain controlled by ORK /
                // their own Rigidbody2D; only stationary level geometry
                // blocks this scene-owned patrol motion.
                Rigidbody2D body = hit.collider.attachedRigidbody;
                if (body != null && body.bodyType != RigidbodyType2D.Static)
                    continue;
                permitted = Mathf.Min(permitted, Mathf.Max(0f, hit.distance - 0.01f));
            }
            return from + direction * permitted;
        }

        public event Action SearchExpired;
        public event Action PlayerCaught;

        public Vector2 Position => beast != null ? (Vector2)beast.position : home;
        public bool WithinWarningRange(Vector2 worldPoint, float radius) =>
            beast != null && beast.gameObject.activeInHierarchy &&
            Vector2.Distance(Position, worldPoint) < radius;
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
                // The original resting pose is relative to the authored
                // level, not the unrotated world. A rotated trial must not
                // make the returning beast face an unrelated compass heading.
                beast.rotation = transform.rotation * Quaternion.Euler(0f, 0f, 180f);
            }
            lastHeard = home;
            searchStarted = float.NegativeInfinity;
            searchExpiryNotified = false;
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
                searchExpiryNotified = false;
            }
        }

        public Vector2 Tick(BellSignal bellSignal, Vector2 hearingPoint, bool directCombat,
            bool combatComplete, bool gateOpen, bool rescueComplete)
        {
            // A reloaded Makinom Searching flag can arrive before the
            // matching transition event. Do not treat an uninitialized
            // timer (-Infinity) as an already-expired search.
            if (bellSignal != BellSignal.Searching)
                searchStarted = float.NegativeInfinity;

            Vector2 destination = Position;
            if (!directCombat && !combatComplete && beast != null &&
                beast.gameObject.activeInHierarchy)
            {
                destination = home;
                if (bellSignal == BellSignal.Ringing)
                {
                    destination = hearingPoint;
                    lastHeard = destination;
                }
                else if (bellSignal == BellSignal.Searching)
                {
                    if (float.IsNegativeInfinity(searchStarted))
                    {
                        searchStarted = Time.time;
                        lastHeard = hearingPoint;
                        searchExpiryNotified = false;
                    }
                    float time = Time.time - searchStarted;
                    if (time >= SearchDurationSeconds)
                    {
                        if (!searchExpiryNotified)
                        {
                            // Mark before callback: a signal handler may
                            // re-enter Tick while the previous search ends.
                            searchExpiryNotified = true;
                            SearchExpired?.Invoke();
                        }
                    }
                    else
                        // Search the same authored local arc after the whole
                        // trial has moved or rotated. The bell's lastHeard
                        // remains a real world-space position, not a second
                        // Makinom search destination.
                        destination = lastHeard + (Vector2)transform.TransformVector(
                            new Vector2(0.45f + 0.45f * Mathf.Cos(time * 2f),
                                0.8f * Mathf.Sin(time * 2f)));
                }
                Vector2 position = beast.position;
                Vector2 next = ConstrainBeastMotion(position,
                    Vector2.MoveTowards(position, destination, 2.5f * Time.deltaTime));
                Vector2 facing = next - position;
                if (facing.sqrMagnitude > 0.000001f)
                    beast.rotation = Quaternion.Euler(0f, 0f,
                        Mathf.Atan2(facing.y, facing.x) * Mathf.Rad2Deg);
                beast.position = next;
            }

            bool playerInDanger = beast != null && beast.gameObject.activeInHierarchy &&
                player != null &&
                !directCombat && !combatComplete && gateOpen && !rescueComplete &&
                // The visible east corridor is local to this scene root.
                // Subtracting only world X fails when a level is rotated.
                transform.InverseTransformPoint(player.position).x > dangerZoneStartLocalX &&
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
            beast.gameObject.activeInHierarchy &&
            signal != BellSignal.Ringing && signal != BellSignal.Searching &&
            Vector2.Distance(beast.position, home) > 0.15f;
    }
}
