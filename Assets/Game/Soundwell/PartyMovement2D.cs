using System.Collections.Generic;
using UnityEngine;

namespace Rpg.Soundwell
{
    /// <summary>Leader input movement, Rigidbody2D sync and physical step geometry; no ORK reference.</summary>
    public sealed class PartyMovement2D : MonoBehaviour
    {
        private readonly List<RaycastHit2D> stepHits = new List<RaycastHit2D>(8);
        // Every action preview uses the same trigger-free physical query.
        // Avoid recreating a ContactFilter2D on every candidate direction.
        private static readonly ContactFilter2D StepSolidFilter =
            new ContactFilter2D { useTriggers = false };
        // Per-level battle bounds; old Soundwell values remain the default.
        [SerializeField] private Vector2 battleArenaHalfExtents = new Vector2(6.7f, 2.85f);
        private Rigidbody2D leaderBody;
        private SoundwellPartyConfig config;
        private bool frameworkReady;

        public Rigidbody2D Configure(Transform leader, SoundwellPartyConfig settings)
        {
            config = settings;
            leaderBody = leader.GetComponent<Rigidbody2D>();
            if (leaderBody == null)
                leaderBody = leader.gameObject.AddComponent<Rigidbody2D>();
            leaderBody.gravityScale = 0f;
            leaderBody.constraints = RigidbodyConstraints2D.FreezeRotation;
            leaderBody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            var collider = leader.GetComponent<CircleCollider2D>();
            if (collider == null)
                collider = leader.gameObject.AddComponent<CircleCollider2D>();
            collider.radius = 0.3f;
            return leaderBody;
        }

        public void SetFrameworkReady() => frameworkReady = true;

        public void ApplyMovement(Vector2 direction, bool carrying, bool allowExploration)
        {
            if (leaderBody == null || config == null) return;
            leaderBody.linearVelocity = frameworkReady && allowExploration
                ? Vector2.ClampMagnitude(direction, 1f) * config.LeaderMoveSpeed *
                    (carrying ? config.CarryingSpeedMultiplier : 1f)
                : Vector2.zero;
        }

        public void MoveBattleActor(GameObject actor, Vector2 destination)
        {
            if (actor == null) return;
            Rigidbody2D body = actor.GetComponent<Rigidbody2D>();
            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
                body.position = destination;
            }
            else
                actor.transform.position = destination;
            Physics2D.SyncTransforms();
        }

        public bool TryStepDestination(GameObject actorObject, Vector2 direction,
            IReadOnlyList<GameObject> actors, GameObject beastObject, float distance,
            out Vector2 destination)
        {
            destination = default;
            // ORK can retire a scene actor before a queued menu refreshes.
            // A disabled combatant must not spend a step or reserve space.
            // A broken/zero step length must not consume a rescuer's sole
            // movement allowance without a real physical displacement.
            if (actorObject == null || !actorObject.activeInHierarchy ||
                !(distance > 0f) || float.IsInfinity(distance) ||
                !(direction == Vector2.up || direction == Vector2.down ||
                  direction == Vector2.left || direction == Vector2.right))
                return false;
            Transform actor = actorObject.transform;
            // ORK's most recent accepted step may have teleported a physics
            // body before its Transform catches up with the next render. Use
            // the physical position for eligibility and the new destination,
            // just as the checkpoint/formation owner does.
            Rigidbody2D actorBody = actorObject.GetComponent<Rigidbody2D>();
            Vector2 from = actorBody != null ? actorBody.position : (Vector2)actor.position;
            destination = from + direction * distance;
            CircleCollider2D bodyCollider = actor.GetComponent<CircleCollider2D>();
            float worldScale = Mathf.Max(Mathf.Abs(actor.lossyScale.x),
                Mathf.Abs(actor.lossyScale.y));
            float radius = (bodyCollider != null ? bodyCollider.radius : 0.125f) *
                worldScale;
            Vector2 worldColliderOffset = bodyCollider != null
                ? (Vector2)actor.TransformVector(bodyCollider.offset)
                : Vector2.zero;
            Vector2 colliderFrom = from + worldColliderOffset;
            // The arena walls live under the trial root. Preview and actual
            // ORK step eligibility must use their local authored limits,
            // even after a scene-level translation or rotation.
            Vector2 arenaLocalCentre = transform.InverseTransformPoint(
                destination + worldColliderOffset);
            // Circle footprints are measured in world units; when a scene is
            // scaled, conservatively convert to its tightest local clearance.
            Vector3 rootScale = transform.lossyScale;
            float minScale = Mathf.Min(Mathf.Abs(rootScale.x), Mathf.Abs(rootScale.y));
            if (minScale <= 0.0001f ||
                !FitsBattleStepInArena(arenaLocalCentre, radius / minScale,
                    battleArenaHalfExtents))
                return false;
            Physics2D.SyncTransforms();
            Physics2D.CircleCast(colliderFrom, radius, direction,
                StepSolidFilter, stepHits, distance);
            foreach (RaycastHit2D hit in stepHits)
                if (!hit.collider.transform.IsChildOf(actor))
                    return false;
            foreach (GameObject other in actors)
                if (other != null && other != actorObject && other.activeInHierarchy &&
                    BlocksBattleStep(other, colliderFrom, direction, distance, radius, 0.40f))
                    return false;
            return beastObject == null || !beastObject.activeInHierarchy ||
                !BlocksBattleStep(beastObject, colliderFrom, direction, distance, radius, 0.50f);
        }

        private static bool BlocksBattleStep(GameObject obstacleObject, Vector2 from,
            Vector2 direction, float distance, float movingRadius, float minimumClearance)
        {
            // Some ORK actor visuals use trigger or temporarily disabled
            // physics colliders. CircleCast intentionally ignores triggers,
            // but their actual occupied space must still prevent overlap.
            // Compare world-space collider centres, not just pivot positions.
            Transform obstacle = obstacleObject.transform;
            Rigidbody2D obstacleBody = obstacleObject.GetComponent<Rigidbody2D>();
            Vector2 centre = obstacleBody != null ? obstacleBody.position : (Vector2)obstacle.position;
            float clearance = minimumClearance;
            CircleCollider2D footprint = obstacle.GetComponent<CircleCollider2D>();
            if (footprint != null)
            {
                centre += (Vector2)obstacle.TransformVector(footprint.offset);
                float scale = Mathf.Max(Mathf.Abs(obstacle.lossyScale.x),
                    Mathf.Abs(obstacle.lossyScale.y));
                clearance = Mathf.Max(minimumClearance, movingRadius +
                    footprint.radius * scale);
            }
            // ORK sometimes represents a live combatant by a trigger-only
            // rotated box. CircleCast excludes that trigger, so check its
            // actual swept outline rather than its world AABB: a 45-degree
            // box has empty AABB corners that must remain walkable.
            if (footprint == null)
            {
                BoxCollider2D box = obstacle.GetComponent<BoxCollider2D>();
                if (box != null)
                    return SweptCircleTouchesBox(from, from + direction * distance,
                        Mathf.Max(movingRadius, minimumClearance * 0.5f),
                        obstacle, box);
            }
            float along = Mathf.Clamp(Vector2.Dot(centre - from, direction), 0f, distance);
            return Vector2.Distance(centre, from + direction * along) < clearance;
        }

        // Closest distance from a cardinal step segment to the true four
        // rotated box edges. The centre-in-box check also catches steps that
        // start entirely inside a trigger footprint. No temporary colliders,
        // allocations, or extra movement authority are introduced.
        private static bool SweptCircleTouchesBox(Vector2 from, Vector2 to,
            float padding, Transform obstacle, BoxCollider2D box)
        {
            Vector2 fromLocal = (Vector2)obstacle.InverseTransformPoint(from) - box.offset;
            Vector2 toLocal = (Vector2)obstacle.InverseTransformPoint(to) - box.offset;
            float halfWidth = box.size.x * 0.5f;
            float halfHeight = box.size.y * 0.5f;
            if ((Mathf.Abs(fromLocal.x) <= halfWidth &&
                 Mathf.Abs(fromLocal.y) <= halfHeight) ||
                (Mathf.Abs(toLocal.x) <= halfWidth &&
                 Mathf.Abs(toLocal.y) <= halfHeight))
                return true;

            Vector2 centre = obstacle.TransformPoint(box.offset);
            Vector2 horizontal = obstacle.TransformVector(
                new Vector3(halfWidth, 0f, 0f));
            Vector2 vertical = obstacle.TransformVector(
                new Vector3(0f, halfHeight, 0f));
            Vector2 a = centre - horizontal - vertical;
            Vector2 b = centre + horizontal - vertical;
            Vector2 c = centre + horizontal + vertical;
            Vector2 d = centre - horizontal + vertical;
            float squaredPadding = padding * padding;
            return SegmentTouchesEdge(from, to, a, b, squaredPadding) ||
                   SegmentTouchesEdge(from, to, b, c, squaredPadding) ||
                   SegmentTouchesEdge(from, to, c, d, squaredPadding) ||
                   SegmentTouchesEdge(from, to, d, a, squaredPadding);
        }

        private static bool SegmentTouchesEdge(Vector2 from, Vector2 to,
            Vector2 a, Vector2 b, float squaredPadding)
        {
            Vector2 step = to - from;
            Vector2 edge = b - a;
            float cross = step.x * edge.y - step.y * edge.x;
            if (Mathf.Abs(cross) > 0.000001f)
            {
                Vector2 gap = a - from;
                float along = (gap.x * edge.y - gap.y * edge.x) / cross;
                float across = (gap.x * step.y - gap.y * step.x) / cross;
                if (along >= 0f && along <= 1f &&
                    across >= 0f && across <= 1f)
                    return true;
            }
            return SquaredDistanceToSegment(from, a, b) <= squaredPadding ||
                SquaredDistanceToSegment(to, a, b) <= squaredPadding ||
                SquaredDistanceToSegment(a, from, to) <= squaredPadding ||
                SquaredDistanceToSegment(b, from, to) <= squaredPadding;
        }

        private static float SquaredDistanceToSegment(Vector2 point,
            Vector2 start, Vector2 end)
        {
            Vector2 segment = end - start;
            float lengthSquared = segment.sqrMagnitude;
            if (lengthSquared <= 0.000001f)
                return (point - start).sqrMagnitude;
            float fraction = Mathf.Clamp01(Vector2.Dot(point - start, segment) /
                lengthSquared);
            return (point - (start + segment * fraction)).sqrMagnitude;
        }

        // Preserve the existing entrypoint for callers and authored tests.
        public static bool FitsBattleStepInArena(Vector2 centre, float footprintRadius) =>
            FitsBattleStepInArena(centre, footprintRadius, new Vector2(6.7f, 2.85f));

        public static bool FitsBattleStepInArena(
            Vector2 centre, float footprintRadius, Vector2 halfExtents) =>
            footprintRadius >= 0f && !float.IsNaN(footprintRadius) &&
            halfExtents.x > 0f && halfExtents.y > 0f &&
            !float.IsNaN(halfExtents.x) && !float.IsNaN(halfExtents.y) &&
            Mathf.Abs(centre.x) + footprintRadius <= halfExtents.x &&
            Mathf.Abs(centre.y) + footprintRadius <= halfExtents.y;
    }
}
