using GamingIsLove.Makinom;
using System;
using UnityEngine;
using UnityEngine.SceneManagement;

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

        private AsyncOperation bootstrapLoad;
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
            view = new SoundwellView(transform, trialCamera, player, bell, beast, safePoint,
                () => ApplyInput(movement, true, false, false),
                () => ApplyInput(movement, false, true, false),
                () => ApplyInput(movement, false, false, true),
                () => { ReturnToBootstrap(); });
            ResetTrial();
            initialized = true;
        }

        private void Update()
        {
            if (!IsReady || !ReadKeyboard)
                return;

            ApplyControls(SoundwellControlFrame.ReadUnity());
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

        public AsyncOperation ReturnToBootstrap()
        {
            if (bootstrapLoad != null)
                return bootstrapLoad;

            bootstrapLoad = SceneManager.LoadSceneAsync(SoundwellEntrance.BootstrapSceneName);
            movement = Vector2.zero;
            if (IsReady)
                playerBody.linearVelocity = Vector2.zero;
            return bootstrapLoad;
        }

        // Keyboard, gamepad and the integration harness share this action boundary.
        public void ApplyControls(SoundwellControlFrame controls)
        {
            if (controls.Back)
            {
                ReturnToBootstrap();
                return;
            }
            ApplyInput(controls.Movement, controls.Ring, controls.Wrap, controls.Retry);
        }

        public void ApplyInput(Vector2 direction, bool ring, bool wrap, bool retry)
        {
            if (bootstrapLoad != null)
                return;

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

        private void OnDestroy()
        {
            view?.Dispose();
        }
    }

    // The legacy InputManager already merges keyboard keys with joystick axes named
    // Horizontal/Vertical. One mapping layer prevents duplicate scene input handlers.
    // South/east/west/north map to Unity joystick buttons 0/1/2/3 respectively.
    public readonly struct SoundwellControlFrame
    {
        public Vector2 Movement { get; }
        public bool Ring { get; }
        public bool Wrap { get; }
        public bool Retry { get; }
        public bool Back { get; }

        private SoundwellControlFrame(Vector2 movement, bool ring, bool wrap, bool retry, bool back)
        {
            Movement = Vector2.ClampMagnitude(movement, 1f);
            Ring = ring;
            Wrap = wrap;
            Retry = retry;
            Back = back;
        }

        public static bool EnterRequested(bool keyboardF2, bool gamepadSouth) =>
            keyboardF2 || gamepadSouth;

        // Pure mapping for independent, hardware-free Play Mode assertions.
        public static SoundwellControlFrame FromButtons(Vector2 movement,
            bool keyboardRing, bool keyboardWrap, bool keyboardRetry, bool keyboardBack,
            bool gamepadSouth, bool gamepadEast, bool gamepadWest, bool gamepadNorth) =>
            new SoundwellControlFrame(movement, keyboardRing || gamepadSouth,
                keyboardWrap || gamepadWest, keyboardRetry || gamepadNorth,
                keyboardBack || gamepadEast);

        public static SoundwellControlFrame ReadUnity() => FromButtons(
            new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")),
            Input.GetKeyDown(KeyCode.E), Input.GetKeyDown(KeyCode.Q),
            Input.GetKeyDown(KeyCode.R), Input.GetKeyDown(KeyCode.Escape),
            Input.GetKeyDown(KeyCode.JoystickButton0), Input.GetKeyDown(KeyCode.JoystickButton1),
            Input.GetKeyDown(KeyCode.JoystickButton2), Input.GetKeyDown(KeyCode.JoystickButton3));
    }
}
