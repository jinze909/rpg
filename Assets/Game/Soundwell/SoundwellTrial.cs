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
        private const string CarryKey = "rpg.soundwell.signal-trial.carrying-bell";
        private const string GateKey = "rpg.soundwell.signal-trial.inner-gate";
        private const string SurveyorClueKey = "rpg.soundwell.signal-trial.surveyor-route";
        private const string PlankSecuredKey = "rpg.soundwell.signal-trial.upper-plank-secured";
        private const string LowerPlankBrokenKey = "rpg.soundwell.signal-trial.lower-plank-broken";
        private const string EscortKey = "rpg.soundwell.signal-trial.surveyor-escorted";
        private const string RescueKey = "rpg.soundwell.signal-trial.surveyor-rescued";
        private const string FalseMarkerClearedKey = "rpg.soundwell.signal-trial.false-route-corrected";
        private const string RopeObservedKey = "rpg.soundwell.signal-trial.rope-observed";
        private const string CartObservedKey = "rpg.soundwell.signal-trial.cart-observed";
        private const string CartKitReclaimedKey = "rpg.soundwell.signal-trial.cart-kit-reclaimed";
        private const string CartClueSharedKey = "rpg.soundwell.signal-trial.cart-clue-shared";
        private const string RopeRecoveredKey = "rpg.soundwell.signal-trial.rope-recovered";
        private const string EscortLineKey = "rpg.soundwell.signal-trial.surveyor-guide-line";
        private const string ClawsObservedKey = "rpg.soundwell.signal-trial.claws-observed";
        // Scene-to-scene handoff only: ORK still owns quests, rewards, and saves.
        public const string EntryOutcomeKey = "rpg.soundwell.signal-trial.last-return-outcome";
        // Additional scene-to-scene outcome, not an ORK item or persistent save.
        public const string EntryCartKitKey = "rpg.soundwell.signal-trial.last-return-cart-kit";
        private const float Reach = 1.25f;
        private const float StandReach = 0.72f;
        private const float Speed = 4f;
        // A muffled ring leaves a short, legible search window; the beast
        // eventually gives up instead of tracking an inaudible source forever.
        public const float SearchDurationSeconds = 3.5f;
        // Both source points are fixed and explicitly visible in the trial.
        public static readonly Vector2 SideBellStand = new Vector2(-2f, -1.95f);
        public static readonly Vector2 RopeEvidencePosition = new Vector2(-4.55f, 1.50f);
        public static readonly Vector2 CartEvidencePosition = new Vector2(-3.35f, 2.30f);
        public static readonly Vector2 ClawEvidencePosition = new Vector2(-1.85f, 1.50f);
        public static readonly Vector2 SurveyorPosition = new Vector2(3.1f, 0.6f);
        public static readonly Vector2 UpperBoardPosition = new Vector2(2.75f, 1.75f);
        public static readonly Vector2 LowerBoardPosition = new Vector2(2.75f, -1.75f);
        private static readonly Vector2 LowerBoardRecovery = new Vector2(2.75f, -0.25f);
        public static readonly Vector2 EscapeLanePosition = new Vector2(1.15f, 0f);
        public const float EscapeLaneDangerRadius = 1.55f;

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
        private Vector2 bellHome;
        private Vector2 lastHeard;
        private float searchStarted;
        private bool actionMenuOpen;
        [NonSerialized] private bool initialized;

        public bool ReadKeyboard { get; set; } = true;
        public bool IsReady => initialized && variables != null;
        public Camera TrialCamera => trialCamera;
        public Vector2 PlayerPosition => playerBody.position;
        public Vector2 BeastPosition => beast.position;
        // A live positional threat, never another saved or guessed target.
        public bool BeastBlocksEscape => Vector2.Distance(beast.position, EscapeLanePosition) <
            EscapeLaneDangerRadius;
        public Vector2 BellPosition => bell.position;
        public bool IsCarrying => variables != null && variables.GetInt(CarryKey) != 0;
        public bool CanPlaceBell => IsCarrying && FindBellStand(playerBody.position, out _);
        public bool GateOpen => variables != null && variables.GetInt(GateKey) != 0;
        public bool AtGatePanel => IsReady &&
            Vector2.Distance(playerBody.position, new Vector2(-0.55f, 0f)) <= 0.8f;
        public bool CanOpenGate => AtGatePanel && !GateOpen && HasBellTarget &&
            Vector2.Distance(bell.position, SideBellStand) < 0.1f && beast.position.y < -0.95f;
        public bool AtSurveyor => GateOpen && !IsCarrying &&
            Vector2.Distance(playerBody.position, SurveyorPosition) <= 1.05f;
        public bool HasSurveyorClue => variables != null && variables.GetInt(SurveyorClueKey) != 0;
        public bool AtUpperPlank => GateOpen && !IsCarrying &&
            Vector2.Distance(playerBody.position, UpperBoardPosition) <= 0.68f;
        public bool PlankSecured => variables != null && variables.GetInt(PlankSecuredKey) != 0;
        public bool LowerPlankBroken => variables != null && variables.GetInt(LowerPlankBrokenKey) != 0;
        public bool CanSecurePlank => AtUpperPlank && HasSurveyorClue && !PlankSecured;
        public bool SurveyorEscorting => variables != null && variables.GetInt(EscortKey) != 0;
        public bool RescueComplete => variables != null && variables.GetInt(RescueKey) != 0;
        public bool FalseMarkerCleared => variables != null &&
            variables.GetInt(FalseMarkerClearedKey) != 0;
        public bool RopeObserved => variables != null && variables.GetInt(RopeObservedKey) != 0;
        public bool CartObserved => variables != null && variables.GetInt(CartObservedKey) != 0;
        public bool CartKitReclaimed => variables != null && variables.GetInt(CartKitReclaimedKey) != 0;
        public bool CartClueShared => variables != null && variables.GetInt(CartClueSharedKey) != 0;
        public bool RopeRecovered => variables != null && variables.GetInt(RopeRecoveredKey) != 0;
        public bool SurveyorLineTied => variables != null && variables.GetInt(EscortLineKey) != 0;
        public bool ClawsObserved => variables != null && variables.GetInt(ClawsObservedKey) != 0;
        public bool AtRopeEvidence => IsReady && !IsCarrying &&
            Vector2.Distance(playerBody.position, RopeEvidencePosition) <= 0.72f;
        public bool AtCartEvidence => IsReady && !IsCarrying &&
            Vector2.Distance(playerBody.position, CartEvidencePosition) <= 0.72f;
        public bool AtClawEvidence => IsReady && !IsCarrying &&
            Vector2.Distance(playerBody.position, ClawEvidencePosition) <= 0.72f;
        public bool CanInspectRope => AtRopeEvidence && !RopeObserved;
        public bool CanRecoverRope => AtRopeEvidence && RopeObserved && !RopeRecovered;
        public bool CanInspectCart => AtCartEvidence && !CartObserved;
        // The clue is a player decision, not an automatically unlocked reward.
        // The rescued surveyor must be nearby to hear the player.
        public bool CanShareCartClue => IsReady && RescueComplete && FalseMarkerCleared &&
            CartObserved && !CartClueShared && !CartKitReclaimed && !IsCarrying &&
            Vector2.Distance(playerBody.position, SurveyorWorldPosition) <= 1.65f;
        public bool CanInspectClaws => AtClawEvidence && !ClawsObserved;
        public Vector2 SurveyorWorldPosition => view != null ? view.SurveyorWorldPosition : SurveyorPosition;
        public bool SurveyorWaitingForLeader => view != null && view.WaitingForEscortLeader;
        public bool CanEscortSurveyor => AtUpperPlank && PlankSecured && !SurveyorEscorting &&
            !RescueComplete && Vector2.Distance(SurveyorWorldPosition, UpperBoardPosition) < 0.18f;
        public bool CanTieEscortLine => CanEscortSurveyor && RopeRecovered && !SurveyorLineTied;
        public bool HasBellTarget => Current == Signal.Ringing;
        public bool IsSearching => Current == Signal.Searching;
        public bool IsWrapped => Current == Signal.Wrapped || IsSearching;
        public bool ActionMenuOpen => actionMenuOpen;

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
            bellHome = bell.position;
            view = new SoundwellView(transform, trialCamera, player, bell, beast, safePoint,
                () => ApplyInput(movement, true, false, false),
                () => ApplyInput(movement, false, true, false),
                () => ApplyInput(movement, false, false, true),
                () => { ReturnToBootstrap(); },
                () => ApplyInput(movement, false, false, false, true),
                () => ApplyInput(movement, false, false, false, false, true));
            ResetTrial();
            initialized = true;
        }

        private void Update()
        {
            if (!IsReady || !ReadKeyboard)
                return;

            if (Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.JoystickButton7))
            {
                SetActionMenuOpen(!actionMenuOpen);
                return;
            }

            if (actionMenuOpen)
            {
                if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.JoystickButton1))
                    SetActionMenuOpen(false);
                return;
            }

            ApplyControls(SoundwellControlFrame.ReadUnity());
        }

        public void SetActionMenuOpen(bool open)
        {
            if (!IsReady || bootstrapLoad != null || actionMenuOpen == open)
                return;

            actionMenuOpen = open;
            if (open)
            {
                movement = Vector2.zero;
                playerBody.linearVelocity = Vector2.zero;
            }

            view.SetActionMenuOpen(open);
        }

        private void FixedUpdate()
        {
            if (IsReady)
                playerBody.linearVelocity = movement * (IsCarrying ? Speed * 0.7f : Speed);
        }

        private void LateUpdate()
        {
            if (!IsReady)
                return;

            // The lower route is physically unsafe, rather than a fake
            // dialogue lock. Any attempt to walk over its weak section drops
            // the player back inside. A broken plank remains impassable.
            if (GateOpen && Vector2.Distance(playerBody.position, LowerBoardPosition) <= 0.48f)
            {
                if (!LowerPlankBroken)
                    variables.Set(LowerPlankBrokenKey, 1);
                movement = Vector2.zero;
                playerBody.linearVelocity = Vector2.zero;
                playerBody.position = LowerBoardRecovery;
            }

            // While lifted, the single saved bell object follows the player;
            // it is silent until placed, so no mobile infinite decoy is created.
            if (IsCarrying)
                bell.position = new Vector3(playerBody.position.x + 0.35f, playerBody.position.y, 0f);

            Vector2 destination = beastHome;
            if (HasBellTarget)
            {
                destination = HearingPoint();
                lastHeard = destination;
            }
            else if (IsSearching)
            {
                float time = Time.time - searchStarted;
                if (time >= SearchDurationSeconds)
                {
                    // Keep the cloth wrapped: its silence is still a meaningful
                    // choice, and only the bell's owner can remove it.
                    SetSignal(Signal.Wrapped);
                }
                else
                {
                    destination = lastHeard + new Vector2(0.45f + 0.45f * Mathf.Cos(time * 2f),
                        0.8f * Mathf.Sin(time * 2f));
                }
            }

            Vector2 position = beast.position;
            Vector2 next = Vector2.MoveTowards(position, destination, 2.5f * Time.deltaTime);
            Vector2 facing = next - position;
            if (facing.sqrMagnitude > 0.000001f)
                beast.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(facing.y, facing.x) * Mathf.Rad2Deg);
            beast.position = next;

            // The drawn intent marker receives exactly the destination already
            // used by the beast, never a separate visual guess.
            bool returning = !HasBellTarget && !IsSearching &&
                Vector2.Distance(beast.position, beastHome) > 0.15f;
            view.Refresh(HasBellTarget, IsSearching, returning, IsWrapped, AtBell, AtSafePoint,
                IsCarrying, CanPlaceBell, GateOpen, AtGatePanel, CanOpenGate, AtSurveyor,
                HasSurveyorClue, AtUpperPlank, CanSecurePlank, PlankSecured,
                CanEscortSurveyor, SurveyorEscorting, RescueComplete, FalseMarkerCleared,
                AtRopeEvidence, AtCartEvidence, AtClawEvidence, CanInspectRope, CanRecoverRope,
                CanInspectCart, CanInspectClaws, CanTieEscortLine, CanShareCartClue,
                RopeObserved, RopeRecovered, CartObserved, CartClueShared, CartKitReclaimed,
                ClawsObserved, SurveyorLineTied,
                BeastBlocksEscape, LowerPlankBroken, destination);
            // Makinom owns the rescue outcome. Neither opening the gate nor
            // reaching safety alone is enough: the survivor must physically
            // make it back to the same marked retreat point.
            if (SurveyorEscorting && !RescueComplete && AtSafePoint &&
                Vector2.Distance(view.SurveyorWorldPosition, safePoint.position) <= 0.9f)
                variables.Set(RescueKey, 1);

            // The surveyor does what he promised after being saved. This
            // second, observable outcome occurs only when his actual figure
            // reaches his misleading route marker, not on rescue alone.
            if (RescueComplete && !FalseMarkerCleared &&
                Vector2.Distance(view.SurveyorWorldPosition, view.FalseMarkerWorldPosition) <= 0.20f)
                variables.Set(FalseMarkerClearedKey, 1);

            // The optional cart clue pays off only through the survivor's
            // physical return to the cart, AFTER he removes his false sign.
            // No equipment, quest or reward state is fabricated by the UI.
            if (RescueComplete && FalseMarkerCleared && CartObserved && CartClueShared &&
                !CartKitReclaimed &&
                Vector2.Distance(view.SurveyorWorldPosition, CartEvidencePosition) <= 0.20f)
                variables.Set(CartKitReclaimedKey, 1);
        }

        public AsyncOperation ReturnToBootstrap()
        {
            if (bootstrapLoad != null)
                return bootstrapLoad;

            SetActionMenuOpen(false);
            // Preserve the actual last exit result for the Bootstrap UI.
            // A fresh incomplete retreat overwrites an earlier victory.
            if (variables != null)
            {
                variables.Set(EntryOutcomeKey, FalseMarkerCleared ? 2 : RescueComplete ? 1 : 0);
                // Overwrite this every exit, including an incomplete replay.
                // Merely finding or telling about the cart does not count.
                variables.Set(EntryCartKitKey,
                    RescueComplete && FalseMarkerCleared && CartKitReclaimed ? 1 : 0);
            }
            bootstrapLoad = SceneManager.LoadSceneAsync(SoundwellEntrance.BootstrapSceneName);
            movement = Vector2.zero;
            if (IsReady)
                playerBody.linearVelocity = Vector2.zero;
            return bootstrapLoad;
        }

        // Keyboard, gamepad and the integration harness share this action boundary.
        public void ApplyControls(SoundwellControlFrame controls)
        {
            // Do not double-handle the same gamepad A / directional event when
            // StandaloneInputModule is submitting or moving a focused UGUI Button.
            if (actionMenuOpen)
                return;

            if (controls.Back)
            {
                ReturnToBootstrap();
                return;
            }
            ApplyInput(controls.Movement, controls.Ring, controls.Wrap, controls.Retry,
                controls.Carry, controls.Gate);
        }

        public void ApplyInput(Vector2 direction, bool ring, bool wrap, bool retry,
            bool carry = false, bool gate = false)
        {
            if (bootstrapLoad != null)
                return;

            movement = Vector2.ClampMagnitude(direction, 1f);
            if (retry && AtSafePoint)
            {
                ResetTrial();
                return;
            }

            // Carry/drop is a real player action, with its state held in the
            // existing Makinom game variables alongside the bell signal.
            if (carry)
            {
                if (IsCarrying)
                {
                    // A legal drop has one of two stable sound sources. An invalid
                    // drop cannot create an arbitrary third destination.
                    if (FindBellStand(playerBody.position, out Vector2 stand))
                    {
                        variables.Set(CarryKey, 0);
                        bell.position = new Vector3(stand.x, stand.y, 0f);
                    }
                }
                else if (AtBell)
                {
                    variables.Set(CarryKey, 1);
                    SetSignal(Signal.Ready);
                    view.ResetPulse();
                }
                return;
            }

            // Only the west-side panel can open the physical gate, and only
            // once the side bell has drawn the real beast away from its lane.
            if (gate)
            {
                if (CanInspectRope)
                    variables.Set(RopeObservedKey, 1);
                else if (CanRecoverRope)
                    variables.Set(RopeRecoveredKey, 1);
                else if (CanInspectCart)
                    variables.Set(CartObservedKey, 1);
                else if (CanShareCartClue)
                    variables.Set(CartClueSharedKey, 1);
                else if (CanInspectClaws)
                    variables.Set(ClawsObservedKey, 1);
                else if (CanOpenGate)
                    variables.Set(GateKey, 1);
                else if (CanTieEscortLine)
                    variables.Set(EscortLineKey, 1);
                else if (CanEscortSurveyor)
                {
                    variables.Set(EscortKey, 1);
                }
                else if (CanSecurePlank)
                {
                    // Interaction must happen at the load-bearing upper plank,
                    // after the surveyor identifies it. His movement then uses
                    // this single Makinom variable, never a UI-only reward.
                    variables.Set(PlankSecuredKey, 1);
                }
                else if (AtSurveyor && !HasSurveyorClue)
                {
                    variables.Set(SurveyorClueKey, 1);
                }
                return;
            }

            if (IsCarrying || !AtBell)
                return;

            if (wrap)
            {
                if (IsWrapped)
                {
                    SetSignal(Signal.Ready);
                    // Unwrapping a muffled bell must not leave a stale sound or pulse.
                    view.ResetPulse();
                }
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
            bell.position = bellHome;
            variables.Set(CarryKey, 0);
            variables.Set(GateKey, 0);
            variables.Set(SurveyorClueKey, 0);
            variables.Set(PlankSecuredKey, 0);
            variables.Set(LowerPlankBrokenKey, 0);
            variables.Set(EscortKey, 0);
            variables.Set(RescueKey, 0);
            variables.Set(FalseMarkerClearedKey, 0);
            variables.Set(RopeObservedKey, 0);
            variables.Set(CartObservedKey, 0);
            variables.Set(CartKitReclaimedKey, 0);
            variables.Set(CartClueSharedKey, 0);
            variables.Set(RopeRecoveredKey, 0);
            variables.Set(EscortLineKey, 0);
            variables.Set(ClawsObservedKey, 0);
            view.ResetSurveyor();
            beast.position = beastHome;
            beast.rotation = Quaternion.Euler(0f, 0f, 180f);
            lastHeard = beastHome;
            searchStarted = float.NegativeInfinity;
            SetSignal(Signal.Ready);
            view.ResetPulse();
        }

        private bool FindBellStand(Vector2 position, out Vector2 stand)
        {
            // Distinct, nonoverlapping pick-up stands. Use the real saved
            // scene bell position for the entrance; never approximate its x/y.
            if (Vector2.Distance(position, SideBellStand) <= StandReach)
            {
                stand = SideBellStand;
                return true;
            }
            if (Vector2.Distance(position, bellHome) <= StandReach)
            {
                stand = bellHome;
                return true;
            }
            stand = default;
            return false;
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
        public bool Carry { get; }
        public bool Gate { get; }

        private SoundwellControlFrame(Vector2 movement, bool ring, bool wrap,
            bool retry, bool back, bool carry, bool gate)
        {
            Movement = Vector2.ClampMagnitude(movement, 1f);
            Ring = ring;
            Wrap = wrap;
            Retry = retry;
            Back = back;
            Carry = carry;
            Gate = gate;
        }

        public static bool EnterRequested(bool keyboardF2, bool gamepadSouth) =>
            keyboardF2 || gamepadSouth;

        // Pure mapping for independent, hardware-free Play Mode assertions.
        public static SoundwellControlFrame FromButtons(Vector2 movement,
            bool keyboardRing, bool keyboardWrap, bool keyboardRetry, bool keyboardBack,
            bool gamepadSouth, bool gamepadEast, bool gamepadWest, bool gamepadNorth,
            bool keyboardCarry = false, bool gamepadLeftShoulder = false,
            bool keyboardGate = false, bool gamepadRightShoulder = false) =>
            new SoundwellControlFrame(movement, keyboardRing || gamepadSouth,
                keyboardWrap || gamepadWest, keyboardRetry || gamepadNorth,
                keyboardBack || gamepadEast, keyboardCarry || gamepadLeftShoulder,
                keyboardGate || gamepadRightShoulder);

        public static SoundwellControlFrame ReadUnity() => FromButtons(
            new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")),
            Input.GetKeyDown(KeyCode.E), Input.GetKeyDown(KeyCode.Q),
            Input.GetKeyDown(KeyCode.R), Input.GetKeyDown(KeyCode.Escape),
            Input.GetKeyDown(KeyCode.JoystickButton0), Input.GetKeyDown(KeyCode.JoystickButton1),
            Input.GetKeyDown(KeyCode.JoystickButton2), Input.GetKeyDown(KeyCode.JoystickButton3),
            Input.GetKeyDown(KeyCode.F), Input.GetKeyDown(KeyCode.JoystickButton4),
            Input.GetKeyDown(KeyCode.G), Input.GetKeyDown(KeyCode.JoystickButton5));
    }
}
