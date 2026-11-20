using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Playable 2D exploration runtime for 4x-scale RPG maps (Soundwell 96x60
    /// and Red Salt Harbor 140x84). Provides axis-separated wall-sliding
    /// movement, iron-gate collision gating, district/zone entry events,
    /// proximity interaction trigger lookup, clamped smooth camera follow,
    /// and hitstop/screen-shake game-feel timers.
    /// </summary>
    public sealed class RpgLargeMapExplorerController
    {
        private readonly int width;
        private readonly int height;
        private readonly Func<int, int, bool> isWalkable;
        private readonly IReadOnlyList<RpgMapZone> zones;
        private readonly IReadOnlyList<RpgMapTriggerAnchor> triggers;

        public Vector2 Position { get; private set; }
        public Vector2Int Facing { get; private set; } = Vector2Int.down;
        public Bounds CameraBounds { get; }
        public float MoveSpeedTilesPerSecond { get; }
        public RpgMapZone? CurrentZone { get; private set; }
        public float HitstopRemaining { get; private set; }
        public float ShakeRemaining { get; private set; }
        public float ShakeIntensity { get; private set; }

        public event Action<RpgMapZone> ZoneEntered;

        public static RpgLargeMapExplorerController ForSoundwell(
            SoundwellLargeMapLayout layout, Func<bool> isGateOpen,
            float moveSpeedTilesPerSecond = 6.5f)
        {
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            if (isGateOpen == null) throw new ArgumentNullException(nameof(isGateOpen));

            var triggers = new[]
            {
                new RpgMapTriggerAnchor("soundwell.bell", layout.BronzeBellTile, 2.2f),
                new RpgMapTriggerAnchor("soundwell.wet-cloth", layout.WetClothTile, 2.2f),
                new RpgMapTriggerAnchor("soundwell.gate-lever", layout.GateLeverTile, 2.4f),
                new RpgMapTriggerAnchor("soundwell.extraction", layout.ExtractionTile, 2.5f)
            };

            return new RpgLargeMapExplorerController(
                layout.Width, layout.Height, layout.CameraBounds,
                new Vector2(layout.PlayerSpawn.x + 0.5f, layout.PlayerSpawn.y + 0.5f),
                (x, y) => SoundwellLargeMapLayout.IsWalkable(layout.Tiles[x, y], isGateOpen()),
                layout.Zones, triggers, moveSpeedTilesPerSecond);
        }

        public static RpgLargeMapExplorerController ForRedSaltHarbor(
            RedSaltHarborMapLayout layout, float moveSpeedTilesPerSecond = 7.0f)
        {
            if (layout == null) throw new ArgumentNullException(nameof(layout));

            return new RpgLargeMapExplorerController(
                layout.Width, layout.Height, layout.CameraBounds,
                new Vector2(layout.PlayerSpawn.x + 0.5f, layout.PlayerSpawn.y + 0.5f),
                (x, y) => RedSaltHarborMapLayout.IsWalkable(layout.Tiles[x, y]),
                layout.Districts, layout.Triggers, moveSpeedTilesPerSecond);
        }

        private RpgLargeMapExplorerController(int width, int height, Bounds cameraBounds,
            Vector2 startPosition, Func<int, int, bool> isWalkable,
            IReadOnlyList<RpgMapZone> zones, IReadOnlyList<RpgMapTriggerAnchor> triggers,
            float moveSpeedTilesPerSecond)
        {
            this.width = width;
            this.height = height;
            CameraBounds = cameraBounds;
            Position = startPosition;
            this.isWalkable = isWalkable;
            this.zones = zones ?? Array.Empty<RpgMapZone>();
            this.triggers = triggers ?? Array.Empty<RpgMapTriggerAnchor>();
            MoveSpeedTilesPerSecond = Mathf.Max(0.5f, moveSpeedTilesPerSecond);
            UpdateCurrentZone(raiseEvent: false);
        }

        // Re-entry uses the same collision footprint as ordinary movement.
        // This can restore a witnessed M05 tile, but never teleport through
        // walls, water, outside the current map, or a closed gate.
        public bool TryPlaceAtWalkableTile(Vector2Int tile)
        {
            var destination = new Vector2(tile.x + 0.5f, tile.y + 0.5f);
            if (!IsPositionWalkable(destination))
                return false;
            Position = destination;
            CurrentZone = null;
            UpdateCurrentZone(raiseEvent: false);
            return true;
        }

        public void TriggerHitstop(float seconds)
        {
            if (seconds > HitstopRemaining)
                HitstopRemaining = seconds;
        }

        public void TriggerScreenShake(float intensity, float durationSeconds)
        {
            if (intensity <= 0f || durationSeconds <= 0f) return;
            ShakeIntensity = Mathf.Max(ShakeIntensity, intensity);
            ShakeRemaining = Mathf.Max(ShakeRemaining, durationSeconds);
        }

        public bool StepMovement(Vector2 rawInput, float deltaTime)
        {
            if (deltaTime <= 0f) return false;

            if (HitstopRemaining > 0f)
            {
                HitstopRemaining = Mathf.Max(0f, HitstopRemaining - deltaTime);
                return false;
            }

            if (rawInput.sqrMagnitude < 0.0001f)
                return false;

            Vector2 dir = rawInput.sqrMagnitude > 1f ? rawInput.normalized : rawInput;
            if (Mathf.Abs(dir.x) >= Mathf.Abs(dir.y))
                Facing = dir.x >= 0f ? Vector2Int.right : Vector2Int.left;
            else
                Facing = dir.y >= 0f ? Vector2Int.up : Vector2Int.down;

            Vector2 delta = dir * (MoveSpeedTilesPerSecond * deltaTime);
            // Substep large frames so fast movement never tunnels through 1-tile gates.
            int steps = Mathf.Max(1, Mathf.CeilToInt(delta.magnitude / 0.35f));
            Vector2 stepDelta = delta / steps;
            bool moved = false;

            for (int i = 0; i < steps; i++)
            {
                Vector2 candidateX = new Vector2(Position.x + stepDelta.x, Position.y);
                if (IsPositionWalkable(candidateX))
                {
                    Position = candidateX;
                    moved = true;
                }

                Vector2 candidateY = new Vector2(Position.x, Position.y + stepDelta.y);
                if (IsPositionWalkable(candidateY))
                {
                    Position = candidateY;
                    moved = true;
                }
            }

            if (moved)
                UpdateCurrentZone(raiseEvent: true);
            return moved;
        }

        public Vector3 ComputeClampedCameraPosition(Vector3 currentCameraPos, float deltaTime,
            float halfViewWidth = RpgWorldMapScale.LogicalScreenTilesX * 0.5f,
            float halfViewHeight = RpgWorldMapScale.LogicalScreenTilesY * 0.5f,
            float smoothSpeed = 8f)
        {
            float minX = CameraBounds.min.x + halfViewWidth;
            float maxX = CameraBounds.max.x - halfViewWidth;
            float minY = CameraBounds.min.y + halfViewHeight;
            float maxY = CameraBounds.max.y - halfViewHeight;

            float targetX = minX <= maxX ? Mathf.Clamp(Position.x, minX, maxX) : CameraBounds.center.x;
            float targetY = minY <= maxY ? Mathf.Clamp(Position.y, minY, maxY) : CameraBounds.center.y;

            float t = deltaTime > 0f ? 1f - Mathf.Exp(-Mathf.Max(0.1f, smoothSpeed) * deltaTime) : 1f;
            float x = Mathf.Lerp(currentCameraPos.x, targetX, t);
            float y = Mathf.Lerp(currentCameraPos.y, targetY, t);

            if (ShakeRemaining > 0f && deltaTime > 0f)
            {
                ShakeRemaining = Mathf.Max(0f, ShakeRemaining - deltaTime);
                float wave = Mathf.Sin(ShakeRemaining * 90f) * ShakeIntensity;
                x += wave;
                y += Mathf.Cos(ShakeRemaining * 73f) * ShakeIntensity * 0.6f;
                if (ShakeRemaining <= 0f)
                    ShakeIntensity = 0f;
            }

            if (minX <= maxX) x = Mathf.Clamp(x, minX, maxX);
            if (minY <= maxY) y = Mathf.Clamp(y, minY, maxY);
            return new Vector3(x, y, currentCameraPos.z);
        }

        public bool TryFindNearestTrigger(out RpgMapTriggerAnchor trigger)
        {
            trigger = default;
            float bestDistSq = float.MaxValue;
            bool found = false;

            for (int i = 0; i < triggers.Count; i++)
            {
                RpgMapTriggerAnchor candidate = triggers[i];
                Vector2 center = new Vector2(candidate.Tile.x + 0.5f, candidate.Tile.y + 0.5f);
                float distSq = (center - Position).sqrMagnitude;
                float radiusSq = candidate.RadiusTiles * candidate.RadiusTiles;
                if (distSq <= radiusSq && distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    trigger = candidate;
                    found = true;
                }
            }
            return found;
        }

        private bool IsPositionWalkable(Vector2 worldPos)
        {
            const float radius = 0.28f;
            return IsTileWalkable(worldPos.x - radius, worldPos.y - radius) &&
                IsTileWalkable(worldPos.x + radius, worldPos.y - radius) &&
                IsTileWalkable(worldPos.x - radius, worldPos.y + radius) &&
                IsTileWalkable(worldPos.x + radius, worldPos.y + radius);
        }

        private bool IsTileWalkable(float x, float y)
        {
            int tx = Mathf.FloorToInt(x);
            int ty = Mathf.FloorToInt(y);
            if (tx < 0 || tx >= width || ty < 0 || ty >= height)
                return false;
            return isWalkable(tx, ty);
        }

        private void UpdateCurrentZone(bool raiseEvent)
        {
            var tile = new Vector2Int(Mathf.FloorToInt(Position.x), Mathf.FloorToInt(Position.y));
            for (int i = 0; i < zones.Count; i++)
            {
                if (zones[i].Bounds.Contains(tile))
                {
                    if (!CurrentZone.HasValue || CurrentZone.Value.Id != zones[i].Id)
                    {
                        CurrentZone = zones[i];
                        if (raiseEvent)
                            ZoneEntered?.Invoke(zones[i]);
                    }
                    return;
                }
            }
        }
    }
}
