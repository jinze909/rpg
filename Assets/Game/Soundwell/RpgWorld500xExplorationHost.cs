using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Unified 500x-area RPG world exploration host for all four playable maps
    /// (Soundwell, Red Salt Harbor, Whitewater Crossing, Mine Camp). Wires
    /// RpgWorld500xMapLayout, camera-windowed RpgLargeMapChunkRenderer,
    /// RpgWorldOrkExplorationBridge, and 16-PPU RpgMapActorVisualBinder into one
    /// collision-safe, streaming, fast-travel-enabled exploration runtime.
    /// </summary>
    public sealed class RpgWorld500xExplorationHost : IDisposable
    {
        public const float DefaultBaseMoveSpeed = 9.0f;
        // Prevent frame hitches or injected deltaTime from crossing solid map tiles.
        private const float MaxMovementDistancePerStep = 32f;
        private const float MaxCollisionSubstepTiles = 0.45f;

        private readonly RpgWorld500xMapLayout layout;
        private readonly RpgWorldOrkExplorationBridge bridge;
        private readonly RpgLargeMapChunkRenderer chunkRenderer;
        private readonly RpgMapActorVisualBinder heroVisual;
        private Vector2 playerPosition;
        private Vector2 facing = Vector2.up;
        private bool ironGateOpen;
        private bool disposed;

        public RpgWorld500xMapLayout Layout => layout;
        public RpgWorldOrkExplorationBridge Bridge => bridge;
        public RpgMapExplorationTracker Tracker => bridge.Tracker;
        public RpgLargeMapChunkRenderer ChunkRenderer => chunkRenderer;
        public RpgMapActorVisualBinder HeroVisual => heroVisual;
        public Vector2 PlayerPosition => playerPosition;
        public Vector2Int CurrentTile => new Vector2Int(
            Mathf.FloorToInt(playerPosition.x),
            Mathf.FloorToInt(playerPosition.y));
        public Vector2 Facing => facing;
        public RpgMapZone? CurrentDistrict => layout.FindDistrictAtTile(CurrentTile);
        public bool IronGateOpen => ironGateOpen;
        public float BaseMoveSpeed { get; set; } = DefaultBaseMoveSpeed;
        public bool IsDisposed => disposed || chunkRenderer == null || chunkRenderer.IsDisposed;

        public RpgWorld500xExplorationHost(
            Transform parent,
            RpgWorldMapId mapId,
            Func<string, int> getInt = null,
            Action<string, int> setInt = null,
            Action<string> onPartyRecover = null,
            Action<string, string> onRewardGranted = null,
            int streamingRadiusChunks = RpgExpandedWorldScale.DefaultStreamingRadiusChunks,
            Sprite[] tileSlices = null)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));

            layout = RpgWorld500xMapLayout.Build(mapId);
            bridge = new RpgWorldOrkExplorationBridge(
                mapId, getInt, setInt, onPartyRecover, onRewardGranted);
            playerPosition = new Vector2(layout.PlayerSpawn.x + 0.5f, layout.PlayerSpawn.y + 0.5f);

            chunkRenderer = layout.CreateStreamingRenderer(parent, streamingRadiusChunks, tileSlices);
            try
            {
                RpgPixelArtManifest art = RpgPixelArtManifest.LoadDefault();
                heroVisual = new RpgMapActorVisualBinder(
                    chunkRenderer.RootObject.transform,
                    "World500xHero_" + mapId,
                    art != null ? art.FerryGuideIdle : null,
                    art != null ? art.FerryGuideWalk1 : null,
                    art != null ? art.FerryGuideWalk2 : null,
                    art != null ? art.FerryGuideBrace : null);
                SyncHero(moving: false, deltaSeconds: 0f);
                bridge.UpdatePlayerPosition(playerPosition);
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public void SetIronGateOpen(bool open)
        {
            ironGateOpen = open;
        }

        public bool StepMovement(Vector2 input, float deltaSeconds, bool sprinting = false)
        {
            if (IsDisposed)
                return false;
            if (bridge.IsInDangerEncounter ||
                !IsFinite(input) || !IsFinite(deltaSeconds) || deltaSeconds <= 0f ||
                input.sqrMagnitude < 0.0001f)
            {
                SyncHero(moving: false, deltaSeconds: 0f);
                return false;
            }

            float speed = RpgWorldOrkExplorationBridge.ComputeEffectiveMoveSpeed(
                BaseMoveSpeed, sprinting, bridge.IsInDangerEncounter);
            if (!IsFinite(speed) || speed <= 0f)
            {
                SyncHero(moving: false, deltaSeconds: 0f);
                return false;
            }

            // Scale oversized finite inputs before normalization: squaring an
            // extreme component can overflow and collapse the direction to zero.
            float maxComponent = Mathf.Max(Mathf.Abs(input.x), Mathf.Abs(input.y));
            Vector2 dir = maxComponent > 1f
                ? (input / maxComponent).normalized
                : input.normalized;
            facing = dir;
            float travel = Mathf.Min(speed * deltaSeconds, MaxMovementDistancePerStep);
            if (!IsFinite(travel) || travel <= 0f)
            {
                SyncHero(moving: false, deltaSeconds: 0f);
                return false;
            }

            Vector2 delta = dir * travel;
            // A single endpoint check can tunnel through the four-tile iron gate
            // when sprinting or under a frame hitch. Sample every traversed tile
            // with bounded, axis-separated steps while preserving wall sliding.
            int substeps = Mathf.Max(1, Mathf.CeilToInt(
                Mathf.Max(Mathf.Abs(delta.x), Mathf.Abs(delta.y)) / MaxCollisionSubstepTiles));
            Vector2 perStep = delta / substeps;
            Vector2 candidate = playerPosition;
            bool moved = false;
            for (int i = 0; i < substeps; i++)
            {
                if (Mathf.Abs(perStep.x) > 0.0001f)
                {
                    Vector2 tryX = new Vector2(candidate.x + perStep.x, candidate.y);
                    if (IsPositionWalkable(tryX))
                    {
                        candidate = tryX;
                        moved = true;
                    }
                }

                if (Mathf.Abs(perStep.y) > 0.0001f)
                {
                    Vector2 tryY = new Vector2(candidate.x, candidate.y + perStep.y);
                    if (IsPositionWalkable(tryY))
                    {
                        candidate = tryY;
                        moved = true;
                    }
                }
            }

            if (moved)
            {
                playerPosition = candidate;
                chunkRenderer.UpdateStreamingFocus(playerPosition);
                bridge.UpdatePlayerPosition(playerPosition);
            }

            SyncHero(moving: moved, deltaSeconds: deltaSeconds);
            return moved;
        }

        public Vector3 StepCamera(Vector3 currentCameraPosition, float deltaSeconds)
        {
            if (IsDisposed || !IsFinite(currentCameraPosition) ||
                !IsFinite(deltaSeconds) || deltaSeconds <= 0f)
                return currentCameraPosition;

            float halfW = RpgWorldMapScale.LogicalScreenTilesX * 0.5f;
            float halfH = RpgWorldMapScale.LogicalScreenTilesY * 0.5f;
            float minX = layout.CameraBounds.min.x + halfW;
            float maxX = layout.CameraBounds.max.x - halfW;
            float minY = layout.CameraBounds.min.y + halfH;
            float maxY = layout.CameraBounds.max.y - halfH;

            float targetX = minX <= maxX
                ? Mathf.Clamp(playerPosition.x, minX, maxX) : layout.CameraBounds.center.x;
            float targetY = minY <= maxY
                ? Mathf.Clamp(playerPosition.y, minY, maxY) : layout.CameraBounds.center.y;
            float t = 1f - Mathf.Exp(-8f * deltaSeconds);
            return new Vector3(
                Mathf.Clamp(Mathf.Lerp(currentCameraPosition.x, targetX, t), minX, maxX),
                Mathf.Clamp(Mathf.Lerp(currentCameraPosition.y, targetY, t), minY, maxY),
                currentCameraPosition.z);
        }

        public bool TryPlaceAtWalkableTile(Vector2Int tile)
        {
            return TryPlaceAtWalkablePosition(new Vector2(tile.x + 0.5f, tile.y + 0.5f));
        }

        internal static bool CanPlaceAtPosition(RpgWorld500xMapLayout targetLayout, Vector2 position, bool gateOpen)
        {
            return IsFinite(position) && position.x >= 0f && position.x < targetLayout.Width &&
                position.y >= 0f && position.y < targetLayout.Height &&
                targetLayout.IsWalkable(Mathf.FloorToInt(position.x), Mathf.FloorToInt(position.y), gateOpen);
        }

        public bool TryPlaceAtWalkablePosition(Vector2 position)
        {
            if (IsDisposed || !CanPlaceAtPosition(layout, position, ironGateOpen))
                return false;
            playerPosition = position;
            SyncHero(moving: false, deltaSeconds: 0f);
            chunkRenderer.UpdateStreamingFocus(playerPosition);
            bridge.UpdatePlayerPosition(playerPosition);
            return true;
        }

        public bool TryFastTravelToBeacon(string markerId, out RpgFastTravelResult result)
        {
            result = default;
            if (IsDisposed)
                return false;
            if (!bridge.TryFastTravel(markerId, (x, y) => layout.IsWalkable(x, y, ironGateOpen), out result))
                return false;

            playerPosition = result.DestinationWorldCenter;
            SyncHero(moving: false, deltaSeconds: 0f);
            chunkRenderer.UpdateStreamingFocus(playerPosition);
            bridge.UpdatePlayerPosition(playerPosition);
            return true;
        }

        public bool FindNearbyMarker(out RpgMapLandmarkMarker nearest)
        {
            nearest = default;
            if (IsDisposed)
                return false;

            IReadOnlyList<RpgMapLandmarkMarker> markers = layout.Markers;
            float bestSq = float.PositiveInfinity;
            bool found = false;
            for (int i = 0; i < markers.Count; i++)
            {
                RpgMapLandmarkMarker m = markers[i];
                float dx = playerPosition.x - (m.Tile.x + 0.5f);
                float dy = playerPosition.y - (m.Tile.y + 0.5f);
                float distSq = dx * dx + dy * dy;
                float radius = Mathf.Max(4f, m.DiscoveryRadiusTiles);
                if (distSq <= radius * radius && distSq < bestSq)
                {
                    bestSq = distSq;
                    nearest = m;
                    found = true;
                }
            }
            return found;
        }

        public bool TryRestAtNearbyCamp(out RpgWorldCampRestResult result)
        {
            result = default;
            if (IsDisposed)
                return false;
            IReadOnlyList<RpgMapLandmarkMarker> markers = layout.Markers;
            for (int i = 0; i < markers.Count; i++)
            {
                if (markers[i].Category == RpgMapMarkerCategory.CampAndSupply &&
                    bridge.TryRestAtCamp(markers[i].Id, playerPosition, out result))
                    return true;
            }
            return false;
        }

        public bool TryTriggerNearbyDangerEncounter(out string triggeredMarkerId)
        {
            triggeredMarkerId = string.Empty;
            if (IsDisposed)
                return false;
            IReadOnlyList<RpgMapLandmarkMarker> markers = layout.Markers;
            for (int i = 0; i < markers.Count; i++)
            {
                if (markers[i].Category == RpgMapMarkerCategory.DangerZone &&
                    bridge.TryTriggerDangerZoneEncounter(markers[i].Id, playerPosition))
                {
                    triggeredMarkerId = markers[i].Id;
                    return true;
                }
            }
            return false;
        }

        public bool TryResolveActiveDangerVictory(bool lockBreakAchieved,
            out RpgDangerZoneEncounterResult result)
        {
            result = default;
            if (IsDisposed || !bridge.IsInDangerEncounter)
                return false;
            return bridge.TryResolveDangerZoneVictory(
                bridge.ActiveEncounterMarkerId, lockBreakAchieved, out result);
        }

        public bool TryInteractNearbyLandmarkQuest(int steps, out RpgLandmarkQuestRewardResult result)
        {
            result = default;
            if (IsDisposed || bridge.IsInDangerEncounter ||
                !FindNearbyMarker(out RpgMapLandmarkMarker marker) ||
                // Danger-zone linked quests are awarded only by the encounter victory path.
                // Generic interaction must never bypass the danger or grant its reward.
                marker.Category == RpgMapMarkerCategory.DangerZone ||
                string.IsNullOrEmpty(marker.LinkedQuestId))
                return false;
            return bridge.TryAdvanceLandmarkQuest(marker.LinkedQuestId, steps, out result);
        }

        private bool IsPositionWalkable(Vector2 pos)
        {
            int tx = Mathf.FloorToInt(pos.x);
            int ty = Mathf.FloorToInt(pos.y);
            return layout.IsWalkable(tx, ty, ironGateOpen);
        }

        private void SyncHero(bool moving, float deltaSeconds)
        {
            if (heroVisual == null || heroVisual.IsDisposed)
                return;
            Vector2Int cardinal = Mathf.Abs(facing.x) >= Mathf.Abs(facing.y)
                ? (facing.x >= 0f ? Vector2Int.right : Vector2Int.left)
                : (facing.y >= 0f ? Vector2Int.up : Vector2Int.down);
            heroVisual.Sync(playerPosition, cardinal,
                moving ? RpgActorVisualState.Moving : RpgActorVisualState.Idle,
                deltaSeconds);
        }

        private static bool IsFinite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
        private static bool IsFinite(Vector2 v) => IsFinite(v.x) && IsFinite(v.y);
        private static bool IsFinite(Vector3 v) => IsFinite(v.x) && IsFinite(v.y) && IsFinite(v.z);

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            heroVisual?.Dispose();
            chunkRenderer?.Dispose();
        }
    }
}
