using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rpg.Soundwell
{
    public enum RpgWorldPatrolState
    {
        Patrolling = 0,
        Suspicious = 1,
        AlertChasing = 2,
        Cleared = 3
    }

    public readonly struct RpgWorldPatrolSnapshot
    {
        public RpgWorldPatrolSnapshot(string markerId, Vector2 position,
            RpgWorldPatrolState state, float distanceToPlayer)
        {
            MarkerId = markerId;
            Position = position;
            State = state;
            DistanceToPlayer = distanceToPlayer;
        }

        public string MarkerId { get; }
        public Vector2 Position { get; }
        public RpgWorldPatrolState State { get; }
        public float DistanceToPlayer { get; }
    }

    /// <summary>
    /// Drives living patrol units around every DangerZone landmark across all
    /// four 500x-area RPG maps. Scales detection range with player sprinting,
    /// triggers RpgWorldOrkExplorationBridge encounters on contact, synchronizes
    /// cleared danger zones, and supplies live threat coordinates for
    /// RpgWorld500xMinimapHud.UpdateThreatMarker.
    /// </summary>
    public sealed class RpgWorld500xPatrolAndEncounterDirector
    {
        public const float PatrolSpeedTilesPerSec = 3.2f;
        public const float ChaseSpeedTilesPerSec = 5.4f;
        public const float BaseDetectionRadiusTiles = 14f;
        public const float SprintDetectionBonusTiles = 8f;
        public const float ContactEncounterRadiusTiles = 2.5f;
        public const float LeashRadiusTiles = 36f;

        private sealed class PatrolUnit
        {
            public string MarkerId;
            public Vector2 Anchor;
            public Vector2[] Waypoints;
            public int WaypointIndex;
            public Vector2 Position;
            public RpgWorldPatrolState State;
        }

        private readonly RpgWorld500xMapLayout layout;
        private readonly RpgWorldOrkExplorationBridge bridge;
        private readonly List<PatrolUnit> units = new List<PatrolUnit>();

        public int TotalUnitCount => units.Count;

        public int ActiveUnitCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < units.Count; i++)
                {
                    if (units[i].State != RpgWorldPatrolState.Cleared)
                        count++;
                }
                return count;
            }
        }

        public RpgWorld500xPatrolAndEncounterDirector(
            RpgWorld500xMapLayout layout,
            RpgWorldOrkExplorationBridge bridge)
        {
            this.layout = layout ?? throw new ArgumentNullException(nameof(layout));
            this.bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));

            IReadOnlyList<RpgMapLandmarkMarker> markers = layout.Markers;
            for (int i = 0; i < markers.Count; i++)
            {
                RpgMapLandmarkMarker m = markers[i];
                if (m.Category != RpgMapMarkerCategory.DangerZone)
                    continue;

                Vector2 anchor = new Vector2(m.Tile.x + 0.5f, m.Tile.y + 0.5f);
                var unit = new PatrolUnit
                {
                    MarkerId = m.Id,
                    Anchor = anchor,
                    Waypoints = BuildPatrolLoop(anchor, layout),
                    WaypointIndex = 0,
                    Position = anchor,
                    State = bridge.IsDangerZoneCleared(m.Id)
                        ? RpgWorldPatrolState.Cleared
                        : RpgWorldPatrolState.Patrolling
                };
                units.Add(unit);
            }
        }

        public string Step(Vector2 playerTile, bool playerSprinting, float deltaSeconds,
            RpgWorld500xMinimapHud optionalHud = null)
        {
            if (!IsFinite(playerTile) || !IsFinite(deltaSeconds) || deltaSeconds <= 0f)
                return string.Empty;

            string triggeredMarkerId = string.Empty;
            float detectRadius = BaseDetectionRadiusTiles +
                (playerSprinting ? SprintDetectionBonusTiles : 0f);
            float suspiciousRadius = detectRadius * 1.35f;

            for (int i = 0; i < units.Count; i++)
            {
                PatrolUnit unit = units[i];
                if (bridge.IsDangerZoneCleared(unit.MarkerId))
                {
                    unit.State = RpgWorldPatrolState.Cleared;
                    continue;
                }

                if (bridge.IsInDangerEncounter)
                    continue;

                float distToPlayer = Vector2.Distance(unit.Position, playerTile);
                float distToAnchor = Vector2.Distance(unit.Position, unit.Anchor);

                if (distToPlayer <= ContactEncounterRadiusTiles)
                {
                    unit.State = RpgWorldPatrolState.AlertChasing;
                    if (bridge.TryTriggerDangerZoneEncounter(unit.MarkerId, playerTile))
                        triggeredMarkerId = unit.MarkerId;
                    continue;
                }

                if (distToPlayer <= detectRadius && distToAnchor <= LeashRadiusTiles)
                {
                    unit.State = RpgWorldPatrolState.AlertChasing;
                    MoveToward(unit, playerTile, ChaseSpeedTilesPerSec * deltaSeconds);
                }
                else if (distToPlayer <= suspiciousRadius && distToAnchor <= LeashRadiusTiles)
                {
                    unit.State = RpgWorldPatrolState.Suspicious;
                    MoveToward(unit, playerTile, PatrolSpeedTilesPerSec * 0.7f * deltaSeconds);
                }
                else
                {
                    unit.State = RpgWorldPatrolState.Patrolling;
                    Vector2 target = unit.Waypoints[unit.WaypointIndex];
                    MoveToward(unit, target, PatrolSpeedTilesPerSec * deltaSeconds);
                    if (Vector2.Distance(unit.Position, target) < 0.6f)
                        unit.WaypointIndex = (unit.WaypointIndex + 1) % unit.Waypoints.Length;
                }
            }

            if (optionalHud != null && !optionalHud.IsDisposed)
            {
                if (TryGetNearestActiveThreat(playerTile, out RpgWorldPatrolSnapshot threat))
                    optionalHud.UpdateThreatMarker(threat.Position, visible: true);
                else
                    optionalHud.UpdateThreatMarker(Vector2.zero, visible: false);
            }

            return triggeredMarkerId;
        }

        public bool TryGetNearestActiveThreat(Vector2 fromTile, out RpgWorldPatrolSnapshot snapshot)
        {
            snapshot = default;
            if (!IsFinite(fromTile))
                return false;

            float bestDist = float.PositiveInfinity;
            bool found = false;
            for (int i = 0; i < units.Count; i++)
            {
                PatrolUnit u = units[i];
                if (u.State == RpgWorldPatrolState.Cleared || bridge.IsDangerZoneCleared(u.MarkerId))
                    continue;
                float d = Vector2.Distance(fromTile, u.Position);
                if (d < bestDist)
                {
                    bestDist = d;
                    snapshot = new RpgWorldPatrolSnapshot(u.MarkerId, u.Position, u.State, d);
                    found = true;
                }
            }
            return found;
        }

        private void MoveToward(PatrolUnit unit, Vector2 target, float maxStep)
        {
            Vector2 delta = target - unit.Position;
            float dist = delta.magnitude;
            if (dist < 0.0001f)
                return;
            Vector2 next = dist <= maxStep
                ? target
                : unit.Position + (delta / dist) * maxStep;
            int tx = Mathf.FloorToInt(next.x);
            int ty = Mathf.FloorToInt(next.y);
            if (layout.IsWalkable(tx, ty, gateOpen: true))
                unit.Position = next;
        }

        private static Vector2[] BuildPatrolLoop(Vector2 anchor, RpgWorld500xMapLayout map)
        {
            Vector2[] offsets =
            {
                new Vector2(-8f, -6f),
                new Vector2(8f, -6f),
                new Vector2(8f, 6f),
                new Vector2(-8f, 6f)
            };
            var pts = new List<Vector2>(offsets.Length);
            for (int i = 0; i < offsets.Length; i++)
            {
                Vector2 p = anchor + offsets[i];
                if (map.IsWalkable(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.y), gateOpen: true))
                    pts.Add(p);
            }
            if (pts.Count == 0)
                pts.Add(anchor);
            return pts.ToArray();
        }

        private static bool IsFinite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
        private static bool IsFinite(Vector2 v) => IsFinite(v.x) && IsFinite(v.y);
    }
}
