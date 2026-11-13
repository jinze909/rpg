using GamingIsLove.Makinom;
using GamingIsLove.ORKFramework;
using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using static Rpg.Soundwell.SoundwellSignalKeys;
using Signal = Rpg.Soundwell.BellSignal;

namespace Rpg.Soundwell
{
    public abstract class TrialFlowController : MonoBehaviour
    {
        // Preserve the original Bootstrap handoff API without another owner.
        public const string EntryOutcomeKey = SoundwellSignalKeys.EntryOutcomeKey;
        public const string EntryOutcomeRecordedKey = SoundwellSignalKeys.EntryOutcomeRecordedKey;
        public const string EntryCartKitKey = SoundwellSignalKeys.EntryCartKitKey;
        public const string EntrySafeRouteKey = SoundwellSignalKeys.EntrySafeRouteKey;
        public const string EntryBeastContactsKey = SoundwellSignalKeys.EntryBeastContactsKey;
        // A muffled ring leaves a short, legible search window; the beast
        // eventually gives up instead of tracking an inaudible source forever.
        public const float SearchDurationSeconds = BeastPatrolAndChase.SearchDurationSeconds;
        // Both source points are fixed and explicitly visible in the trial.
        public static readonly Vector2 SideBellStand = new Vector2(-2f, -1.95f);
        public static readonly Vector2 RopeEvidencePosition = new Vector2(-4.55f, 1.50f);
        public static readonly Vector2 CartEvidencePosition = new Vector2(-3.35f, 2.30f);
        public static readonly Vector2 ClawEvidencePosition = new Vector2(-1.85f, 1.50f);
        public static readonly Vector2 SurveyorPosition = new Vector2(3.1f, 0.6f);
        public static readonly Vector2 UpperBoardPosition = new Vector2(2.75f, 1.75f);
        public static readonly Vector2 LowerBoardPosition = new Vector2(2.75f, -1.75f);
        public static readonly Vector2 EscapeLanePosition = new Vector2(1.15f, 0f);
        public const float EscapeLaneDangerRadius = 1.55f;

        [SerializeField] protected Transform player;
        [SerializeField] protected Transform bell;
        [SerializeField] protected Transform beast;
        [SerializeField] protected Transform safePoint;
        [SerializeField] protected Camera trialCamera;

        private AsyncOperation bootstrapLoad;
        private VariableHandler variables;
        protected SoundwellView view;
        private SoundwellOrkParty orkParty;
        private Rigidbody2D playerBody;
        private PartyMovement2D partyMovement;
        private Vector2 movement;
        private Vector2 beastHome;
        private Vector2 bellHome;
        private TrialMechanismInteractable mechanisms;
        private BeastPatrolAndChase patrol;
        private TrialCheckpointManager checkpoint;
        private bool actionMenuOpen;
        private static readonly Vector2 GatePanelPosition = new Vector2(-0.55f, 0f);
        private const float GatePanelReach = 0.8f;
        private Func<Combatant, bool> canReachBattleGate;
        // ORK's BattleComponent is a one-encounter runtime owner; a fresh scene
        // is required to re-register a defeated combatant and start a new battle.
        private bool combatStartedInThisScene;
        [NonSerialized] private bool initialized;
        private float nextEventSystemAuditAt;

        public bool ReadInput { get; set; } = true;
        public bool IsReady => initialized && variables != null;
        public Camera TrialCamera => trialCamera;
        public Transform PlayerActor => player;
        public Vector2 PlayerPosition => playerBody.position;
        public Vector2 BeastPosition => patrol != null ? patrol.Position : beastHome;
        public bool DirectCombatStarted => variables != null &&
            variables.GetInt(DirectCombatStartedKey) != 0;
        public bool DirectCombatComplete => variables != null &&
            variables.GetInt(DirectCombatCompleteKey) != 0;
        public bool CanEngageBeast => IsReady && AtGatePanel && !GateOpen &&
            !IsCarrying && !DirectCombatStarted && !DirectCombatComplete &&
            // ORK's battle component owns a single encounter in this scene.
            // After defeat, Retry reloads a fresh roster; never advertise a
            // gate interaction that TryStartDirectEncounter cannot accept.
            !combatStartedInThisScene &&
            variables.GetInt(RescueApproachKey) == 0 && orkParty != null && orkParty.IsReady;
        // A live positional threat, never another saved or guessed target.
        public bool BeastBlocksEscape => GateOpen && !RescueComplete &&
            !DirectCombatComplete && beast != null &&
            Vector2.Distance(beast.position, EscapeLanePosition) < EscapeLaneDangerRadius;
        public int BeastContacts => variables != null ? variables.GetInt(BeastContactKey) : 0;
        public bool BeastContacted => BeastContacts > 0;
        public const float BeastContactRadius = BeastPatrolAndChase.ContactRadius;
        public Vector2 BellPosition => bell.position;
        public bool IsCarrying => mechanisms != null && mechanisms.IsCarrying;
        public bool CanPlaceBell => mechanisms != null && mechanisms.CanPlaceBell;
        public bool GateOpen => variables != null && variables.GetInt(GateKey) != 0;
        public bool AtGatePanel => IsReady &&
            Vector2.Distance(playerBody.position, GatePanelPosition) <= GatePanelReach;
        public bool CanOpenGate => AtGatePanel && !GateOpen &&
            (DirectCombatComplete || (variables != null &&
                variables.GetInt(RescueApproachKey) == 1 && HasBellTarget &&
                Vector2.Distance(bell.position, SideBellStand) < 0.1f &&
                beast != null && beast.position.y < -0.95f));
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
        public bool SafeRouteMarked => variables != null &&
            variables.GetInt(SafeRouteMarkedKey) != 0;
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
        public bool HasBellTarget => mechanisms != null && mechanisms.Current == Signal.Ringing;
        public bool IsSearching => mechanisms != null && mechanisms.Current == Signal.Searching;
        // Same expiry clock as the beast, not an independent UI timer.
        public float SearchRemainingFraction => IsSearching ? patrol.SearchRemainingFraction : 0f;
        public bool IsWrapped => (mechanisms != null && mechanisms.Current == Signal.Wrapped) || IsSearching;
        public bool ActionMenuOpen => actionMenuOpen;

        private bool AtBell => mechanisms != null && mechanisms.AtBell;
        private bool AtSafePoint => Vector2.Distance(playerBody.position, safePoint.position) <= 1.1f;

        protected void Start()
        {
            // The saved ORK starter initializes Makinom before this scene begins.
            variables = Maki.Initialized ? Maki.Game?.Variables : null;
            if (variables == null)
            {
                enabled = false;
                throw new InvalidOperationException("Makinom's game variables are unavailable in the Soundwell scene.");
            }

            orkParty = GetComponent<SoundwellOrkParty>();
            SoundwellInputAdapter.SetGameplayContext();
            SoundwellInputAdapter.EnsureEventSystem(transform);
            partyMovement = GetComponent<PartyMovement2D>();
            if (partyMovement == null)
                partyMovement = gameObject.AddComponent<PartyMovement2D>();
            playerBody = partyMovement.Configure(player, SoundwellPartyConfig.Load());
            beastHome = beast.position;
            bellHome = bell.position;
            mechanisms = GetComponent<TrialMechanismInteractable>();
            if (mechanisms == null)
                mechanisms = gameObject.AddComponent<TrialMechanismInteractable>();
            mechanisms.Configure(variables, playerBody, bell, bellHome);
            patrol = GetComponent<BeastPatrolAndChase>();
            if (patrol == null)
                patrol = gameObject.AddComponent<BeastPatrolAndChase>();
            patrol.Configure(beast, playerBody, beastHome);
            checkpoint = GetComponent<TrialCheckpointManager>();
            if (checkpoint == null)
                checkpoint = gameObject.AddComponent<TrialCheckpointManager>();
            checkpoint.Configure(variables, playerBody, safePoint, bell, bellHome);
            mechanisms.SignalChanged += signal =>
                patrol.OnBellSignal(signal, mechanisms.HearingPoint);
            patrol.SearchExpired += () => mechanisms.SetSignal(Signal.Wrapped);
            patrol.PlayerCaught += () =>
            {
                checkpoint.RecoverFromBeastContact();
                movement = Vector2.zero;
                view?.AnimatePlayerHit();
            };
            // M3: One Cinemachine Brain on the scene's real Camera and one
            // CinemachineCamera for the trial. SoundwellView never drives its lens.
            var cameraRig = trialCamera.gameObject.AddComponent<SoundwellCameraRig>();
            cameraRig.Initialize(trialCamera, player, transform);
            canReachBattleGate = CanReachBattleGate;
            view = new SoundwellView(transform, trialCamera, player, bell, beast, safePoint,
                () => ApplyInput(movement, true, false, false),
                () => ApplyInput(movement, false, true, false),
                () => ApplyInput(movement, false, false, true),
                () => { ReturnToBootstrap(); },
                () => ApplyInput(movement, false, false, false, true),
                () => ApplyInput(movement, false, false, false, false, true),
                () =>
                {
                    if (orkParty == null)
                        return;
                    // ORK alone decides eligibility, actor and target. Animate
                    // only the displayed leader after ORK accepts that actor's
                    // action; the other two rescuers must not borrow her sprite.
                    // This is feedback for submitting an action, not fake damage.
                    int attacker = orkParty.ReadyBaseAttackerIndex;
                    if (orkParty.TryAttackBeast() && attacker == 0)
                        view.AnimatePlayerAction();
                },
                () =>
                {
                    if (orkParty == null)
                        return;
                    int defender = orkParty.ChoosingRescuerIndex;
                    // ORK alone accepts Guard and advances the combat turn.
                    // The leader's pixel shield is feedback for that real
                    // acceptance; companions use their own existing renderer.
                    if (orkParty.TryGuard() && defender == 0)
                        view.AnimatePlayerGuard();
                },
                () => orkParty?.TryOperateGate(canReachBattleGate, () => variables.Set(GateKey, 1)),
                () =>
                {
                    // UGUI Cancel closes the exploration focus menu, but in the
                    // currently unconfigured ORK battle menu it must provide a
                    // reachable retreat instead of trapping the player in a turn.
                    if (view.MovePreviewOpen)
                        view.CloseBattleMovePreview();
                    else if (DirectCombatStarted)
                        ReturnToBootstrap();
                    else
                        SetActionMenuOpen(false);
                },
                (actor, direction) =>
                {
                    // The world-space pose follows an actual ORK movement
                    // acceptance; rejected previews draw no fake step.
                    bool accepted = orkParty != null && orkParty.TryStep(actor, direction);
                    if (accepted && actor == 0)
                        view.AnimatePlayerBattleStep(direction);
                    return accepted;
                });
            mechanisms.BellSoundRequested += muffled => view.PlayBell(muffled);
            mechanisms.ActionPoseRequested += () => view.AnimatePlayerAction();
            mechanisms.PulseResetRequested += () => view.ResetPulse();
            ResetTrial();
            initialized = true;
        }

        protected void Update()
        {
            if (!IsReady || !ReadInput || DirectCombatStarted)
                return;

            if (SoundwellInputAdapter.ToggleMenuPressed())
            {
                SetActionMenuOpen(!actionMenuOpen);
                return;
            }

            if (actionMenuOpen)
                return;

            ApplyControls(SoundwellInputAdapter.ReadGameplay());
        }

        public void SetActionMenuOpen(bool open)
        {
            // A delayed exploration UGUI callback must not reopen its menu
            // over ORK's live action chooser. Closing on retreat/retry remains
            // legal, but only the battle HUD owns navigation while fighting.
            if (!IsReady || bootstrapLoad != null || actionMenuOpen == open ||
                (open && DirectCombatStarted))
                return;

            actionMenuOpen = open;
            if (open)
            {
                SoundwellInputAdapter.SetUiContext();
                movement = Vector2.zero;
                playerBody.linearVelocity = Vector2.zero;
            }
            else
            {
                SoundwellInputAdapter.SetGameplayContext();
            }

            view.SetActionMenuOpen(open);
        }

        protected void FixedUpdate()
        {
            if (DirectCombatStarted)
                movement = Vector2.zero;
            partyMovement?.ApplyMovement(movement, IsReady && IsCarrying,
                IsReady && !DirectCombatStarted);
        }

        protected void LateUpdate()
        {
            if (!IsReady)
                return;

            // ORK can instantiate its own UI root after the initial scene setup.
            // Reconcile the EventSystem after framework/party startup so both
            // exploration and ORK battle menus share one InputSystemUIInputModule.
            if (Time.unscaledTime >= nextEventSystemAuditAt)
            {
                SoundwellInputAdapter.EnsureEventSystem(transform);
                nextEventSystemAuditAt = Time.unscaledTime + 1f;
            }

            if (DirectCombatStarted && orkParty != null)
            {
                if (orkParty.EncounterWon)
                {
                    variables.Set(DirectCombatStartedKey, 0);
                    variables.Set(DirectCombatCompleteKey, 1);
                    variables.Set(RescueApproachKey, 2);
                    SoundwellInputAdapter.SetGameplayContext();
                }
                else if (orkParty.EncounterFailed || orkParty.EncounterStartFailed)
                {
                    variables.Set(DirectCombatStartedKey, 0);
                    variables.Set(RescueApproachKey, 0);
                    variables.Set(GateKey, 0);
                    orkParty.ReleaseFailedEncounter();
                    SoundwellInputAdapter.SetGameplayContext();
                }
            }

            bool battleMode = DirectCombatStarted;
            view.SetBattleMode(battleMode);
            if (battleMode)
            {
                // The view reads a live ORK snapshot; it never resolves attacks
                // or maintains a second copy of combat state.
                view.SetBattleReadout(orkParty != null ? orkParty.BuildBattleReadout() : string.Empty);
                int activeRescuer = orkParty != null ? orkParty.ChoosingRescuerIndex : -1;
                // ORK may retire a combatant before the chooser HUD updates.
                // A valid index does not guarantee that Unity's actor survives.
                Transform activeActor = ResolveBattleActor(
                    orkParty != null ? orkParty.Actors : null, activeRescuer);
                view.SetBattleChoices(activeRescuer, activeActor,
                    orkParty != null && orkParty.ReadyBaseAttackerIndex == activeRescuer && activeRescuer >= 0,
                    orkParty != null && orkParty.CanGuard(activeRescuer),
                    orkParty != null && orkParty.CanOperateGate(activeRescuer, canReachBattleGate),
                    GateOpen);
                view.SetBattleMoveChoices(activeRescuer, activeActor,
                    orkParty != null && orkParty.CanStep(activeRescuer, Vector2.up),
                    orkParty != null && orkParty.CanStep(activeRescuer, Vector2.right),
                    orkParty != null && orkParty.CanStep(activeRescuer, Vector2.down),
                    orkParty != null && orkParty.CanStep(activeRescuer, Vector2.left));
                movement = Vector2.zero;
                playerBody.linearVelocity = Vector2.zero;
            }

            // Exploration hazards are delegated to checkpoint + patrol; ORK
            // turn movement retains its independent movement authority.
            if (ShouldRecoverFromLowerPlank(DirectCombatStarted, GateOpen,
                    playerBody.position))
            {
                if (checkpoint.RecoverLowerPlank(LowerPlankBroken))
                    view.AnimatePlayerHit();
                movement = Vector2.zero;
            }

            Vector2 destination = patrol.Tick(mechanisms.Current,
                mechanisms.HearingPoint, DirectCombatStarted,
                DirectCombatComplete, GateOpen, RescueComplete);

            // Keep the ONE physical bell attached to its actual carrier after
            // *all* scene-owned relocations, including the weak plank recovery
            // and beast knockback. Updating before contact leaves the bell at
            // the old location for a frame and advertises a false drop point.
            // Only Makinom's existing carrying variable owns this decision.
            if (IsCarrying)
                bell.position = new Vector3(playerBody.position.x + 0.35f, playerBody.position.y, 0f);

            // The drawn intent marker receives exactly the destination already
            // used by the beast, never a separate visual guess.
            bool returning = patrol.IsReturning(DirectCombatStarted,
                DirectCombatComplete, mechanisms.Current);
            view.AnimatePlayerMotion(movement.sqrMagnitude > 0.0001f && !battleMode && !ActionMenuOpen,
                movement.x);
            view.Refresh(HasBellTarget, IsSearching, returning, IsWrapped, AtBell, AtSafePoint,
                IsCarrying, CanPlaceBell, GateOpen, AtGatePanel, CanOpenGate,
                CanEngageBeast, DirectCombatComplete, AtSurveyor,
                HasSurveyorClue, AtUpperPlank, CanSecurePlank, PlankSecured,
                CanEscortSurveyor, SurveyorEscorting, RescueComplete, FalseMarkerCleared,
                SafeRouteMarked, AtRopeEvidence, AtCartEvidence, AtClawEvidence, CanInspectRope, CanRecoverRope,
                CanInspectCart, CanInspectClaws, CanTieEscortLine, CanShareCartClue,
                RopeObserved, RopeRecovered, CartObserved, CartClueShared, CartKitReclaimed,
                ClawsObserved, SurveyorLineTied,
                BeastBlocksEscape, BeastContacted, LowerPlankBroken,
                combatStartedInThisScene && !DirectCombatStarted && !DirectCombatComplete,
                SearchRemainingFraction, destination);
            // Makinom owns the rescue outcome. Neither opening the gate nor
            // reaching safety alone is enough: the survivor must physically
            // make it back to the same marked retreat point.
            if (bootstrapLoad == null && SurveyorEscorting && !RescueComplete && AtSafePoint &&
                Vector2.Distance(view.SurveyorWorldPosition, safePoint.position) <= 0.9f &&
                orkParty != null && orkParty.SettleRescue())
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

            // The optional map kit changes the world: after retrieving it, the
            // surveyor actually walks back and posts the corrected route at
            // his former misleading sign. Merely exiting cannot create it.
            if (CartKitReclaimed && FalseMarkerCleared && !SafeRouteMarked &&
                Vector2.Distance(view.SurveyorWorldPosition, view.FalseMarkerWorldPosition) <= 0.20f)
                variables.Set(SafeRouteMarkedKey, 1);
        }

        // The plank belongs to exploration physics, never ORK's turn-owned
        // combatant placement. Keep the rule independent of the asynchronous
        // ORK gate-action calculation: a queued gate action is not yet GateOpen.
        public static bool ShouldRecoverFromLowerPlank(
            bool directCombatStarted, bool gateOpen, Vector2 actorPosition) =>
            !directCombatStarted && gateOpen &&
            Vector2.Distance(actorPosition, LowerBoardPosition) <= 0.48f;

        // Resolve the actual ORK scene actor, never an inferred turn marker.
        // Unity destroyed objects compare null even if their list entry remains.
        public static Transform ResolveBattleActor(
            System.Collections.Generic.IReadOnlyList<GameObject> actors, int index)
        {
            if (actors == null || index < 0 || index >= actors.Count)
                return null;
            GameObject actor = actors[index];
            return actor != null ? actor.transform : null;
        }

        // An ORK turn may retire its combatant between input selection and
        // the gate reach callback during retreat or scene teardown.
        // Missing owners/visuals must never open the physical gate or throw.
        private bool CanReachBattleGate(Combatant member) =>
            DirectCombatStarted && !GateOpen && SoundwellTrial.HasLiveGateActorInReach(member);

        public AsyncOperation ReturnToBootstrap()
        {
            if (bootstrapLoad != null)
                return bootstrapLoad;

            SetActionMenuOpen(false);
            // Both retreat and victory can leave ORK's encounter owner alive
            // after the scene unloads. Release it before opening a fresh trial.
            if (combatStartedInThisScene && orkParty != null && orkParty.IsReady)
            {
                orkParty.ResetEncounter();
                variables.Set(DirectCombatStartedKey, 0);
                variables.Set(RescueApproachKey, 0);
            }
            // Preserve the actual last exit result for the Bootstrap UI.
            // A fresh incomplete retreat overwrites an earlier victory.
            if (variables != null)
            {
                variables.Set(EntryOutcomeKey, FalseMarkerCleared ? 2 : RescueComplete ? 1 : 0);
                variables.Set(EntryOutcomeRecordedKey, 1);
                // Overwrite this every exit, including an incomplete replay.
                // Merely finding or telling about the cart does not count.
                variables.Set(EntryCartKitKey,
                    RescueComplete && FalseMarkerCleared && CartKitReclaimed ? 1 : 0);
                variables.Set(EntrySafeRouteKey,
                    RescueComplete && FalseMarkerCleared && CartKitReclaimed && SafeRouteMarked ? 1 : 0);
                variables.Set(EntryBeastContactsKey, BeastContacts);
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
            // InputSystemUIInputModule is submitting or moving a focused UGUI Button.
            // A stale UI callback can arrive before Bootstrap/ORK has finished
            // initializing or after an exit has already started scene loading.
            // Do not act on incomplete scene owners or trigger a second unload.
            if (!IsReady || bootstrapLoad != null || actionMenuOpen || DirectCombatStarted)
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
            // Input events from a previous UGUI focus can reach this public
            // entry point before the scene is ready. Never dereference its
            // uninitialized player/variables or mutate a scene being unloaded.
            if (!IsReady || bootstrapLoad != null || DirectCombatStarted)
                return;

            movement = Vector2.ClampMagnitude(direction, 1f);
            if (retry && AtSafePoint)
            {
                // A Retry submitted from focused UGUI must hand control back
                // to exploration. Resetting Makinom state alone otherwise
                // leaves the UI action map enabled and movement suppressed.
                SetActionMenuOpen(false);
                if (combatStartedInThisScene)
                {
                    // Restart the live scene to create fresh ORK combatants,
                    // group registrations and battle components. Simply
                    // regenerating HP on a completed ORK battle cannot create
                    // a second encounter (Unity regression: 0 participants).
                    orkParty.ResetEncounter();
                    movement = Vector2.zero;
                    playerBody.linearVelocity = Vector2.zero;
                    bootstrapLoad = SceneManager.LoadSceneAsync(SoundwellEntrance.SceneName);
                }
                else
                {
                    ResetTrial();
                }
                return;
            }

            if (carry)
            {
                mechanisms.HandleCarry();
                return;
            }

            // Only the west-side panel can open the physical gate, and only
            // once the side bell has drawn the real beast away from its lane.
            if (gate)
            {
                if (CanEngageBeast)
                {
                    SetActionMenuOpen(false);
                    SoundwellInputAdapter.SetUiContext();
                    if (orkParty.TryStartDirectEncounter())
                    {
                        // ORK is the sole encounter authority. Never play an
                        // accepted-action pose before its actual Start succeeds.
                        // A stale battle owner can reject engagement even while
                        // the gate is spatially in reach.
                        view.AnimatePlayerAction();
                        combatStartedInThisScene = true;
                        variables.Set(RescueApproachKey, 2);
                        variables.Set(DirectCombatStartedKey, 1);
                        movement = Vector2.zero;
                        playerBody.linearVelocity = Vector2.zero;
                        view.SetBattleMode(true);
                    }
                    else
                    {
                        SoundwellInputAdapter.SetGameplayContext();
                    }
                }
                else
                    mechanisms.HandleWorldInteraction(
                        new TrialMechanismInteractable.WorldInteraction(
                            CanInspectRope, CanRecoverRope, CanInspectCart,
                            CanShareCartClue, CanInspectClaws, CanOpenGate,
                            CanTieEscortLine, CanEscortSurveyor, CanSecurePlank,
                            AtSurveyor && !HasSurveyorClue));
                return;
            }

            mechanisms.HandleBell(ring, wrap);
        }

        private void ResetTrial()
        {
            movement = Vector2.zero;
            checkpoint.ResetFlagsAndPosition();
            if (orkParty != null && orkParty.IsReady)
                orkParty.ResetEncounter();
            view.SetBattleMode(false);
            view.ResetPlayerAnimation();
            view.ResetSurveyor();
            patrol.ResetPatrol();
            mechanisms.SetSignal(Signal.Ready);
            view.ResetPulse();
        }

        protected void OnDestroy()
        {
            view?.Dispose();
        }
    }

    // The legacy InputManager already merges keyboard keys with joystick axes named
    // Horizontal/Vertical. One mapping layer prevents duplicate scene input handlers.
    // South/east/west/north map to Unity joystick buttons 0/1/2/3 respectively.
}
