using System.Collections.Generic;
using UnityEngine;

namespace Rpg.Soundwell
{
    /// <summary>Leader input movement, Rigidbody2D sync and physical step geometry; no ORK reference.</summary>
    public sealed class PartyMovement2D : MonoBehaviour
    {
        private readonly List<RaycastHit2D> stepHits = new List<RaycastHit2D>(8);
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
            if (actorObject == null ||
                !(direction == Vector2.up || direction == Vector2.down ||
                  direction == Vector2.left || direction == Vector2.right))
                return false;
            Transform actor = actorObject.transform;
            Vector2 from = actor.position;
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
            if (!FitsBattleStepInArena(destination + worldColliderOffset, radius))
                return false;
            Physics2D.SyncTransforms();
            Physics2D.CircleCast(colliderFrom, radius, direction,
                new ContactFilter2D { useTriggers = false }, stepHits, distance);
            foreach (RaycastHit2D hit in stepHits)
                if (!hit.collider.transform.IsChildOf(actor))
                    return false;
            foreach (GameObject other in actors)
                if (other != null && other != actorObject &&
                    BlocksBattleStep(other.transform.position, from, direction, distance, 0.40f))
                    return false;
            return beastObject == null ||
                !BlocksBattleStep(beastObject.transform.position, from, direction, distance, 0.50f);
        }

        private static bool BlocksBattleStep(Vector2 obstacle, Vector2 from,
            Vector2 direction, float distance, float clearance)
        {
            float along = Mathf.Clamp(Vector2.Dot(obstacle - from, direction), 0f, distance);
            return Vector2.Distance(obstacle, from + direction * along) < clearance;
        }

        public static bool FitsBattleStepInArena(Vector2 centre, float footprintRadius) =>
            footprintRadius >= 0f &&
            Mathf.Abs(centre.x) + footprintRadius <= 6.7f &&
            Mathf.Abs(centre.y) + footprintRadius <= 2.85f;
    }
}
