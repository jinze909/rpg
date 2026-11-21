using System;
using UnityEngine;

namespace Rpg.Soundwell
{
    public enum RpgActorVisualState
    {
        Idle,
        Moving,
        AlertOrBrace
    }

    /// <summary>
    /// Reusable 16-PPU pixel-art actor sprite renderer and lightweight 2-frame
    /// walk/idle/alert-or-brace animator for 4x RPG maps (heroes, Shen Li,
    /// Sound-borrowing Beast, Spear Guards, and Outer Harbor story NPCs).
    /// Handles facing flipX and Y-sorted sortingOrder cleanly without depending
    /// on Animator controllers or owning any ORK combat authority.
    /// </summary>
    public sealed class RpgMapActorVisualBinder : IDisposable
    {
        private const float WalkFrameDurationSeconds = 0.22f;

        private readonly GameObject actorObject;
        private readonly SpriteRenderer spriteRenderer;
        private readonly Sprite idleSprite;
        private readonly Sprite walkSprite1;
        private readonly Sprite walkSprite2;
        private readonly Sprite alertOrBraceSprite;
        private readonly int baseSortingOrder;
        private float walkTimer;
        private bool walkToggle;
        private bool disposed;

        public GameObject ActorObject => actorObject;
        public SpriteRenderer SpriteRenderer => spriteRenderer;
        public RpgActorVisualState CurrentState { get; private set; } = RpgActorVisualState.Idle;
        public bool IsDisposed => disposed;

        public RpgMapActorVisualBinder(Transform parent, string name,
            Sprite idleSprite,
            Sprite walkSprite1 = null,
            Sprite walkSprite2 = null,
            Sprite alertOrBraceSprite = null,
            int baseSortingOrder = 6)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));

            this.idleSprite = idleSprite;
            this.walkSprite1 = walkSprite1 ?? idleSprite;
            this.walkSprite2 = walkSprite2 ?? this.walkSprite1;
            this.alertOrBraceSprite = alertOrBraceSprite ?? idleSprite;
            this.baseSortingOrder = baseSortingOrder;

            actorObject = new GameObject(string.IsNullOrEmpty(name) ? "MapActor" : name);
            actorObject.transform.SetParent(parent, false);
            spriteRenderer = actorObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = this.idleSprite;
            spriteRenderer.sortingOrder = baseSortingOrder;
        }

        public void Sync(Vector2 tilePosition, Vector2Int facing,
            RpgActorVisualState state, float deltaTime = 0f)
        {
            if (disposed || actorObject == null || spriteRenderer == null)
                return;

            actorObject.transform.localPosition = new Vector3(tilePosition.x, tilePosition.y, 0f);
            // Lower Y renders in front for classic 2D top-down RPG depth sorting.
            spriteRenderer.sortingOrder = baseSortingOrder +
                Mathf.Clamp(200 - Mathf.FloorToInt(tilePosition.y), 0, 200);

            if (facing.x < 0)
                spriteRenderer.flipX = true;
            else if (facing.x > 0)
                spriteRenderer.flipX = false;

            CurrentState = state;
            switch (state)
            {
                case RpgActorVisualState.AlertOrBrace:
                    walkTimer = 0f;
                    walkToggle = false;
                    spriteRenderer.sprite = alertOrBraceSprite;
                    break;

                case RpgActorVisualState.Moving:
                    if (deltaTime > 0f && !float.IsNaN(deltaTime) &&
                        !float.IsInfinity(deltaTime))
                    {
                        // A frame can span more than one animation beat.
                        // Preserve cycle parity and the fractional remainder
                        // rather than flipping once and accumulating lag.
                        walkTimer = Mathf.Repeat(
                            walkTimer + deltaTime, 2f * WalkFrameDurationSeconds);
                        walkToggle = walkTimer >= WalkFrameDurationSeconds;
                    }
                    spriteRenderer.sprite = walkToggle ? walkSprite2 : walkSprite1;
                    break;

                default:
                    walkTimer = 0f;
                    walkToggle = false;
                    spriteRenderer.sprite = idleSprite;
                    break;
            }
        }

        public void SetTint(Color color)
        {
            if (disposed || spriteRenderer == null) return;
            spriteRenderer.color = color;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (actorObject == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(actorObject);
            else UnityEngine.Object.DestroyImmediate(actorObject);
        }
    }
}
