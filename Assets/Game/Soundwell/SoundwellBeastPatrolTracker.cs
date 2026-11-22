using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rpg.Soundwell
{
    public enum SoundwellBeastAlertState
    {
        Patrolling,
        InvestigatingBell,
        Alerted,
        StaggeredBroken
    }

    /// <summary>
    /// 2D patrol and acoustic-detection runtime for the Sound-borrowing Beast on
    /// the 96x60 SoundwellLargeMapLayout. Drives 4-waypoint patrol, wet-cloth
    /// hearing attenuation, bronze-bell decoy luring, and broken-stagger pauses
    /// synchronized with SoundwellAcousticBreakTracker.
    /// Strictly subordinate to ORK 3: owns zero HP, damage, turn, or defeat
    /// authority; raises EncounterTriggered only to hand off to ORK battle.
    /// </summary>
    public sealed class SoundwellBeastPatrolTracker
    {
        public const float BaseHearingRadiusTiles = 7.0f;
        public const float MuffledHearingRadiusTiles = 2.5f;
        public const float ContactRadiusTiles = 1.25f;

        private readonly IReadOnlyList<Vector2Int> waypoints;
        private readonly SoundwellAcousticBreakTracker acousticTracker;
        private int waypointIndex;
        private Vector2? decoyTarget;
        private float decoyHoldRemaining;
        private Vector2 lastHeardPlayerPos;
        private bool encounterRaisedForCurrentContact;

        public Vector2 Position { get; private set; }
        public int CurrentWaypointIndex => waypointIndex;
        public float PatrolSpeedTilesPerSecond { get; }
        public float ChaseSpeedTilesPerSecond { get; }
        public SoundwellBeastAlertState State { get; private set; } = SoundwellBeastAlertState.Patrolling;

        public float ActiveHearingRadiusTiles =>
            acousticTracker != null &&
            !acousticTracker.AcousticLocks.HasRemainingLock(CombatLockKind.WetClothMuffle)
                ? MuffledHearingRadiusTiles
                : BaseHearingRadiusTiles;

        public event Action<SoundwellBeastAlertState> StateChanged;
        public event Action EncounterTriggered;

        public SoundwellBeastPatrolTracker(SoundwellLargeMapLayout layout,
            SoundwellAcousticBreakTracker acousticTracker,
            float patrolSpeedTilesPerSecond = 3.2f,
            float chaseSpeedTilesPerSecond = 4.6f)
        {
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            if (layout.BeastPatrolWaypoints == null || layout.BeastPatrolWaypoints.Count == 0)
                throw new ArgumentException("Layout must define at least one patrol waypoint.", nameof(layout));

            waypoints = layout.BeastPatrolWaypoints;
            this.acousticTracker = acousticTracker ?? throw new ArgumentNullException(nameof(acousticTracker));
            PatrolSpeedTilesPerSecond = IsFinite(patrolSpeedTilesPerSecond)
                ? Mathf.Max(0.5f, patrolSpeedTilesPerSecond)
                : 3.2f;
            ChaseSpeedTilesPerSecond = IsFinite(chaseSpeedTilesPerSecond)
                ? Mathf.Max(PatrolSpeedTilesPerSecond, chaseSpeedTilesPerSecond)
                : Mathf.Max(PatrolSpeedTilesPerSecond, 4.6f);

            waypointIndex = 0;
            Position = TileCenter(waypoints[0]);
            if (waypoints.Count > 1)
                waypointIndex = 1;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        public void NotifyBronzeBellRung(Vector2Int bellTile, float investigateSeconds = 4.5f)
        {
            decoyTarget = TileCenter(bellTile);
            decoyHoldRemaining = IsFinite(investigateSeconds)
                ? Mathf.Max(0.5f, investigateSeconds)
                : 4.5f;
            if (!acousticTracker.IsBeastBroken)
                SetState(SoundwellBeastAlertState.InvestigatingBell);
        }

        public void Step(float deltaTime, Vector2 playerPosition, bool playerMoved)
        {
            if (!IsFinite(deltaTime) || deltaTime <= 0f)
                return;

            if (acousticTracker.IsBeastBroken)
            {
                encounterRaisedForCurrentContact = false;
                SetState(SoundwellBeastAlertState.StaggeredBroken);
                return;
            }

            if (State == SoundwellBeastAlertState.StaggeredBroken)
                SetState(SoundwellBeastAlertState.Patrolling);

            bool hasFinitePlayer = IsFinite(playerPosition.x) && IsFinite(playerPosition.y);
            float distToPlayer = hasFinitePlayer
                ? Vector2.Distance(Position, playerPosition)
                : float.PositiveInfinity;
            if (hasFinitePlayer && distToPlayer <= ContactRadiusTiles)
            {
                SetState(SoundwellBeastAlertState.Alerted);
                if (!encounterRaisedForCurrentContact)
                {
                    encounterRaisedForCurrentContact = true;
                    EncounterTriggered?.Invoke();
                }
                return;
            }
            encounterRaisedForCurrentContact = false;

            // Bronze-bell decoy takes priority over normal patrol while active.
            if (decoyTarget.HasValue)
            {
                SetState(SoundwellBeastAlertState.InvestigatingBell);
                MoveTowards(decoyTarget.Value, ChaseSpeedTilesPerSecond * deltaTime);
                if (Vector2.Distance(Position, decoyTarget.Value) <= 0.25f)
                {
                    decoyHoldRemaining -= deltaTime;
                    if (decoyHoldRemaining <= 0f)
                    {
                        decoyTarget = null;
                        SetState(SoundwellBeastAlertState.Patrolling);
                    }
                }
                return;
            }

            if (hasFinitePlayer && playerMoved && distToPlayer <= ActiveHearingRadiusTiles)
            {
                lastHeardPlayerPos = playerPosition;
                SetState(SoundwellBeastAlertState.Alerted);
            }

            if (State == SoundwellBeastAlertState.Alerted)
            {
                MoveTowards(lastHeardPlayerPos, ChaseSpeedTilesPerSecond * deltaTime);
                if (Vector2.Distance(Position, lastHeardPlayerPos) <= 0.25f)
                    SetState(SoundwellBeastAlertState.Patrolling);
                return;
            }

            // Default cyclic patrol across Soundwell waypoints.
            Vector2 target = TileCenter(waypoints[waypointIndex]);
            MoveTowards(target, PatrolSpeedTilesPerSecond * deltaTime);
            if (Vector2.Distance(Position, target) <= 0.15f)
                waypointIndex = (waypointIndex + 1) % waypoints.Count;
        }

        private void MoveTowards(Vector2 target, float maxDelta)
        {
            Position = Vector2.MoveTowards(Position, target, maxDelta);
        }

        private void SetState(SoundwellBeastAlertState next)
        {
            if (State == next) return;
            State = next;
            StateChanged?.Invoke(next);
        }

        private static Vector2 TileCenter(Vector2Int tile)
        {
            return new Vector2(tile.x + 0.5f, tile.y + 0.5f);
        }
    }
}
