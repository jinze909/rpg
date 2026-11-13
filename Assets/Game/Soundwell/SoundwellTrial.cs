using GamingIsLove.Makinom;
using GamingIsLove.ORKFramework;
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
        private const string BeastContactKey = "rpg.soundwell.signal-trial.player-caught-by-beast";
        private const string RescueKey = "rpg.soundwell.signal-trial.surveyor-rescued";
        private const string FalseMarkerClearedKey = "rpg.soundwell.signal-trial.false-route-corrected";
        private const string SafeRouteMarkedKey = "rpg.soundwell.signal-trial.safe-route-marked";
        private const string RopeObservedKey = "rpg.soundwell.signal-trial.rope-observed";
        private const string CartObservedKey = "rpg.soundwell.signal-trial.cart-observed";
        private const string CartKitReclaimedKey = "rpg.soundwell.signal-trial.cart-kit-reclaimed";
        private const string CartClueSharedKey = "rpg.soundwell.signal-trial.cart-clue-shared";
        private const string RopeRecoveredKey = "rpg.soundwell.signal-trial.rope-recovered";
        private const string EscortLineKey = "rpg.soundwell.signal-trial.surveyor-guide-line";
        private const string ClawsObservedKey = "rpg.soundwell.signal-trial.claws-observed";
        private const string RescueApproachKey = "rpg.soundwell.signal-trial.rescue-approach";
        private const string DirectCombatStartedKey = "rpg.soundwell.signal-trial.direct-combat-started";
        private const string DirectCombatCompleteKey = "rpg.soundwell.signal-trial.direct-combat-complete";
        // Scene-to-scene handoff only: ORK still owns quests, rewards, and saves.
        public const string EntryOutcomeKey = "rpg.soundwell.signal-trial.last-return-outcome";
        // Distinguish a real zero-contact retreat from the untouched first-launch default.
        public const string EntryOutcomeRecordedKey = "rpg.soundwell.signal-trial.last-return-recorded";
        // Additional scene-to-scene outcome, not an ORK item or persistent save.
        public const string EntryCartKitKey = "rpg.soundwell.signal-trial.last-return-cart-kit";
        public const string EntrySafeRouteKey = "rpg.soundwell.signal-trial.last-return-safe-route";
        public const string EntryBeastContactsKey = "rpg.soundwell.signal-trial.last-return-beast-contacts";
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
        private SoundwellOrkParty orkParty;
        private Rigidbody2D playerBody;
        private Vector2 movement;
        private Vector2 beastHome;
        private Vector2 bellHome;
        private Vector2 lastHeard;
        private float searchStarted;
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
        public Vector2 BeastPosition => beast != null ? (Vector2)beast.position : beastHome;
        public bool DirectCombatStarted => variables != null &&
            variables.GetInt(DirectCombatStartedKey) != 0;
        public bool DirectCombatComplete => variables != null &&
            variables.GetInt(DirectCombatCompleteKey) != 0;
        public bool CanEngageBeast => IsReady && AtGatePanel && !GateOpen &&
            !IsCarrying && !DirectCombatStarted && !DirectCombatComplete &&
            variables.GetInt(RescueApproachKey) == 0 && orkParty != null && orkParty.IsReady;
        // A live positional threat, never another saved or guessed target.
        public bool BeastBlocksEscape => GateOpen && !RescueComplete &&
            !DirectCombatComplete && beast != null &&
            Vector2.Distance(beast.position, EscapeLanePosition) < EscapeLaneDangerRadius;
        public int BeastContacts => variables != null ? variables.GetInt(BeastContactKey) : 0;
        public bool BeastContacted => BeastContacts > 0;
        public const float BeastContactRadius = 0.67f;
        public Vector2 BellPosition => bell.position;
        public bool IsCarrying => variables != null && variables.GetInt(CarryKey) != 0;
        public bool CanPlaceBell => IsCarrying && FindBellStand(playerBody.position, out _);
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
        public bool HasBellTarget => Current == Signal.Ringing;
        public bool IsSearching => Current == Signal.Searching;
        // Same expiry clock as the beast, not an independent UI timer.
        public float SearchRemainingFraction => IsSearching
            ? Mathf.Clamp01((SearchDurationSeconds - (Time.time - searchStarted)) /
                SearchDurationSeconds) : 0f;
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

            orkParty = GetComponent<SoundwellOrkParty>();
            SoundwellInputAdapter.SetGameplayContext();
            SoundwellInputAdapter.EnsureEventSystem(transform);
            playerBody = player.gameObject.AddComponent<Rigidbody2D>();
            playerBody.gravityScale = 0f;
            playerBody.constraints = RigidbodyConstraints2D.FreezeRotation;
            playerBody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            player.gameObject.AddComponent<CircleCollider2D>().radius = 0.3f;
            beastHome = beast.position;
            bellHome = bell.position;
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
            ResetTrial();
            initialized = true;
        }

        private void Update()
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
            if (!IsReady || bootstrapLoad != null || actionMenuOpen == open)
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

        private void FixedUpdate()
        {
            if (!IsReady)
                return;
            if (DirectCombatStarted)
            {
                movement = Vector2.zero;
                playerBody.linearVelocity = Vector2.zero;
                return;
            }
            playerBody.linearVelocity = movement * (IsCarrying ? Speed * 0.7f : Speed);
        }

        private void LateUpdate()
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

            // The lower route is physically unsafe, rather than a fake
            // dialogue lock. Any attempt to walk over its weak section drops
            // the player back inside. A broken plank remains impassable.
            // The lower plank is an exploration hazard. During the
            // turn-based ORK encounter, a scripted LateUpdate knockback
            // would silently override a combatant's authorized position,
            // bypassing the one-step movement budget and its collision check.
            if (ShouldRecoverFromLowerPlank(DirectCombatStarted, GateOpen,
                    playerBody.position))
            {
                if (!LowerPlankBroken)
                {
                    variables.Set(LowerPlankBrokenKey, 1);
                    // The weak board has a real physical consequence.
                    // Reuse the existing M4 Hit clip on its first collapse;
                    // no ORK damage or second physics state is manufactured.
                    view.AnimatePlayerHit();
                }
                movement = Vector2.zero;
                playerBody.linearVelocity = Vector2.zero;
                playerBody.position = LowerBoardRecovery;
            }

            Vector2 destination = beast != null ? (Vector2)beast.position : beastHome;
            if (!DirectCombatStarted && !DirectCombatComplete && beast != null)
            {
                destination = beastHome;
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
            }

            // A real encounter with the beast must have a consequence. The
            // locked gate protects the west side; only direct contact in the
            // open eastern room pushes the player back to RETREAT. Preserve
            // earned route and rescue clues, avoiding a hidden total reset.
            if (!DirectCombatStarted && !DirectCombatComplete && beast != null &&
                GateOpen && !RescueComplete && playerBody.position.x > 0.55f &&
                Vector2.Distance(playerBody.position, beast.position) <= BeastContactRadius)
            {
                // One encounter per physical knockback; the player is moved
                // away immediately, so the same collision can't count twice.
                variables.Set(BeastContactKey, Mathf.Min(999, BeastContacts + 1));
                view.AnimatePlayerHit();
                movement = Vector2.zero;
                playerBody.linearVelocity = Vector2.zero;
                playerBody.position = safePoint.position;
            }

            // Keep the ONE physical bell attached to its actual carrier after
            // *all* scene-owned relocations, including the weak plank recovery
            // and beast knockback. Updating before contact leaves the bell at
            // the old location for a frame and advertises a false drop point.
            // Only Makinom's existing carrying variable owns this decision.
            if (IsCarrying)
                bell.position = new Vector3(playerBody.position.x + 0.35f, playerBody.position.y, 0f);

            // The drawn intent marker receives exactly the destination already
            // used by the beast, never a separate visual guess.
            bool returning = !DirectCombatStarted && !DirectCombatComplete &&
                !HasBellTarget && !IsSearching && beast != null &&
                Vector2.Distance(beast.position, beastHome) > 0.15f;
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
        private static bool HasLiveGateActorInReach(Combatant member)
        {
            if (member == null || member.GameObject == null)
                return false;
            return Vector2.Distance(member.GameObject.transform.position,
                GatePanelPosition) <= GatePanelReach;
        }

        private bool CanReachBattleGate(Combatant member) =>
            DirectCombatStarted && !GateOpen && HasLiveGateActorInReach(member);

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
            if (actionMenuOpen || DirectCombatStarted)
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
            if (bootstrapLoad != null || DirectCombatStarted)
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
                else if (CanInspectRope)
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
                view.AnimatePlayerAction();
            }
            else if (ring)
            {
                if (!IsWrapped)
                    SetSignal(Signal.Ringing);
                view.PlayBell(IsWrapped);
                view.AnimatePlayerAction();
            }
        }

        private void ResetTrial()
        {
            movement = Vector2.zero;
            playerBody.linearVelocity = Vector2.zero;
            playerBody.position = safePoint.position;
            bell.position = bellHome;
            if (orkParty != null && orkParty.IsReady)
                orkParty.ResetEncounter();
            variables.Set(RescueApproachKey, 0);
            variables.Set(DirectCombatStartedKey, 0);
            variables.Set(DirectCombatCompleteKey, 0);
            variables.Set(CarryKey, 0);
            variables.Set(GateKey, 0);
            variables.Set(SurveyorClueKey, 0);
            variables.Set(PlankSecuredKey, 0);
            variables.Set(LowerPlankBrokenKey, 0);
            variables.Set(EscortKey, 0);
            variables.Set(BeastContactKey, 0);
            variables.Set(RescueKey, 0);
            variables.Set(FalseMarkerClearedKey, 0);
            variables.Set(SafeRouteMarkedKey, 0);
            variables.Set(RopeObservedKey, 0);
            variables.Set(CartObservedKey, 0);
            variables.Set(CartKitReclaimedKey, 0);
            variables.Set(CartClueSharedKey, 0);
            variables.Set(RopeRecoveredKey, 0);
            variables.Set(EscortLineKey, 0);
            variables.Set(ClawsObservedKey, 0);
            view.SetBattleMode(false);
            view.ResetPlayerAnimation();
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
        private void SetSignal(Signal signal)
        {
            if (signal == Signal.Ringing && variables.GetInt(RescueApproachKey) == 0)
                variables.Set(RescueApproachKey, 1);
            variables.Set(SignalKey, (int)signal);
        }

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
        public static SoundwellControlFrame FromActions(Vector2 movement, bool ring, bool wrap,
            bool retry, bool back, bool carry, bool gate) =>
            new SoundwellControlFrame(movement, ring, wrap, retry, back, carry, gate);

        // Pure mapping helper retained for hardware-free regression assertions.
        public static SoundwellControlFrame FromButtons(Vector2 movement,
            bool keyboardRing, bool keyboardWrap, bool keyboardRetry, bool keyboardBack,
            bool gamepadSouth, bool gamepadEast, bool gamepadWest, bool gamepadNorth,
            bool keyboardCarry = false, bool gamepadLeftShoulder = false,
            bool keyboardGate = false, bool gamepadRightShoulder = false) =>
            FromActions(movement, keyboardRing || gamepadSouth, keyboardWrap || gamepadWest,
                keyboardRetry || gamepadNorth, keyboardBack || gamepadEast,
                keyboardCarry || gamepadLeftShoulder, keyboardGate || gamepadRightShoulder);
    }
}
