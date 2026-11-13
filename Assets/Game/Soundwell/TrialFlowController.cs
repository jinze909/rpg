using GamingIsLove.Makinom;
using GamingIsLove.ORKFramework;
using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using static Rpg.Soundwell.SoundwellSignalKeys;
using Signal = Rpg.Soundwell.BellSignal;

namespace Rpg.Soundwell
{
    public abstract partial class TrialFlowController : MonoBehaviour
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
        public bool AtGatePanel => IsReady && mechanisms.NearGatePanel;
        public bool CanOpenGate => AtGatePanel && !GateOpen &&
            (DirectCombatComplete || (variables != null &&
                variables.GetInt(RescueApproachKey) == 1 && HasBellTarget &&
                Vector2.Distance(bell.position, SideBellStand) < 0.1f &&
                beast != null && beast.position.y < -0.95f));
        public bool AtSurveyor => GateOpen && !IsCarrying && mechanisms.NearSurveyor;
        public bool HasSurveyorClue => variables != null && variables.GetInt(SurveyorClueKey) != 0;
        public bool AtUpperPlank => GateOpen && !IsCarrying && mechanisms.NearUpperPlank;
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
        public bool AtRopeEvidence => IsReady && !IsCarrying && mechanisms.NearRope;
        public bool AtCartEvidence => IsReady && !IsCarrying && mechanisms.NearCart;
        public bool AtClawEvidence => IsReady && !IsCarrying && mechanisms.NearClaws;
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

    }
}
