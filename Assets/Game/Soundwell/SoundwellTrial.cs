using GamingIsLove.Makinom;
using System;
using UnityEngine;

namespace Rpg.Soundwell
{
    public sealed class SoundwellTrial : MonoBehaviour
    {
        private enum Signal { Ready, Ringing, Wrapped, Searching }
        private const string SignalKey = "rpg.soundwell.signal-trial.bell";
        private const float Reach = 1.25f;
        private const float Speed = 4f;

        [SerializeField] private Transform player;
        [SerializeField] private Transform bell;
        [SerializeField] private Transform beast;
        [SerializeField] private Transform safePoint;
        [SerializeField] private Camera trialCamera;

        private VariableHandler variables;
        private SoundwellView view;
        private Rigidbody2D playerBody;
        private Vector2 movement;
        private Vector2 beastHome;
        private Vector2 lastHeard;
        private float searchStarted;
        [NonSerialized] private bool initialized;

        public bool ReadKeyboard { get; set; } = true;
        public bool IsReady => initialized && variables != null;
        public Camera TrialCamera => trialCamera;
        public Vector2 PlayerPosition => playerBody.position;
        public Vector2 BeastPosition => beast.position;
        public bool HasBellTarget => Current == Signal.Ringing;
        public bool IsSearching => Current == Signal.Searching;
        public bool IsWrapped => Current == Signal.Wrapped || IsSearching;

        private Signal Current => (Signal)variables.GetInt(SignalKey);
        private bool AtBell => Vector2.Distance(playerBody.position, bell.position) <= Reach;
        private bool AtSafePoint => Vector2.Distance(playerBody.position, safePoint.position) <= 1.1f;

        private void Start()
        {
            // The saved ORK starter initializes Makinom before this scene begins.
            variables = Maki.Initialized ? Maki.Game?.Variables : null;
            if (variables == null)
            {
                enabled = false;
                throw new InvalidOperationException("Makinom's game variables are unavailable in the Soundwell scene.");
            }

            playerBody = player.gameObject.AddComponent<Rigidbody2D>();
            playerBody.gravityScale = 0f;
            playerBody.constraints = RigidbodyConstraints2D.FreezeRotation;
            playerBody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            player.gameObject.AddComponent<CircleCollider2D>().radius = 0.3f;
            beastHome = beast.position;
            view = new SoundwellView(transform, trialCamera, player, bell, beast, safePoint);
            ResetTrial();
            initialized = true;
        }

        private void Update()
        {
            if (!IsReady || !ReadKeyboard)
                return;

            Vector2 direction = new Vector2(
                Axis(KeyCode.A, KeyCode.LeftArrow, KeyCode.D, KeyCode.RightArrow),
                Axis(KeyCode.S, KeyCode.DownArrow, KeyCode.W, KeyCode.UpArrow));
            ApplyInput(direction, Input.GetKeyDown(KeyCode.E), Input.GetKeyDown(KeyCode.Q),
                Input.GetKeyDown(KeyCode.R));
        }

        private void FixedUpdate()
        {
            if (IsReady)
                playerBody.linearVelocity = movement * Speed;
        }

        private void LateUpdate()
        {
            if (!IsReady)
                return;

            Vector2 destination = beastHome;
            if (HasBellTarget)
            {
                destination = HearingPoint();
                lastHeard = destination;
            }
            else if (IsSearching)
            {
                float time = Time.time - searchStarted;
                destination = lastHeard + new Vector2(0.45f + 0.45f * Mathf.Cos(time * 2f),
                    0.8f * Mathf.Sin(time * 2f));
            }

            Vector2 position = beast.position;
            Vector2 next = Vector2.MoveTowards(position, destination, 2.5f * Time.deltaTime);
            Vector2 facing = next - position;
            if (facing.sqrMagnitude > 0.000001f)
                beast.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(facing.y, facing.x) * Mathf.Rad2Deg);
            beast.position = next;

            view.Refresh(HasBellTarget, IsSearching, IsWrapped, AtBell, AtSafePoint);
        }

        // Both the keyboard and the integration test use this scene input boundary.
        public void ApplyInput(Vector2 direction, bool ring, bool wrap, bool retry)
        {
            movement = Vector2.ClampMagnitude(direction, 1f);
            if (retry && AtSafePoint)
            {
                ResetTrial();
                return;
            }

            if (!AtBell)
                return;

            if (wrap)
            {
                if (IsWrapped)
                    SetSignal(Signal.Ready);
                else if (HasBellTarget)
                {
                    lastHeard = HearingPoint();
                    searchStarted = Time.time;
                    SetSignal(Signal.Searching);
                    view.PlayBell(true);
                }
                else
                    SetSignal(Signal.Wrapped);
            }
            else if (ring)
            {
                if (!IsWrapped)
                    SetSignal(Signal.Ringing);
                view.PlayBell(IsWrapped);
            }
        }

        private void ResetTrial()
        {
            movement = Vector2.zero;
            playerBody.linearVelocity = Vector2.zero;
            playerBody.position = safePoint.position;
            beast.position = beastHome;
            beast.rotation = Quaternion.Euler(0f, 0f, 180f);
            lastHeard = beastHome;
            SetSignal(Signal.Ready);
            view.ResetPulse();
        }

        private Vector2 HearingPoint() => new Vector2(1.15f, bell.position.y);
        private void SetSignal(Signal signal) => variables.Set(SignalKey, (int)signal);

        private static int Axis(KeyCode low, KeyCode alternateLow, KeyCode high, KeyCode alternateHigh)
        {
            return (Input.GetKey(high) || Input.GetKey(alternateHigh) ? 1 : 0)
                - (Input.GetKey(low) || Input.GetKey(alternateLow) ? 1 : 0);
        }

        private void OnDestroy()
        {
            view?.Dispose();
        }
    }
}
