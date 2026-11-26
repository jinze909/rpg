using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rpg.Soundwell
{
    public readonly struct RpgWorld500xRoutePlan
    {
        public RpgWorld500xRoutePlan(
            RpgWorldMapId mapId,
            Vector2Int destinationTile,
            float directWalkTiles,
            bool recommendFastTravel,
            string bestBeaconId,
            string bestBeaconTitleZh,
            Vector2Int bestBeaconTile,
            float walkAfterBeaconTiles,
            float tilesSavedByFastTravel,
            float estimatedWalkSeconds,
            float estimatedSprintSeconds,
            bool crossesDangerZone,
            string summaryZh)
        {
            MapId = mapId;
            DestinationTile = destinationTile;
            DirectWalkTiles = directWalkTiles;
            RecommendFastTravel = recommendFastTravel;
            BestBeaconId = bestBeaconId ?? string.Empty;
            BestBeaconTitleZh = bestBeaconTitleZh ?? string.Empty;
            BestBeaconTile = bestBeaconTile;
            WalkAfterBeaconTiles = walkAfterBeaconTiles;
            TilesSavedByFastTravel = tilesSavedByFastTravel;
            EstimatedWalkSeconds = estimatedWalkSeconds;
            EstimatedSprintSeconds = estimatedSprintSeconds;
            CrossesDangerZone = crossesDangerZone;
            SummaryZh = summaryZh ?? string.Empty;
        }

        public RpgWorldMapId MapId { get; }
        public Vector2Int DestinationTile { get; }
        public float DirectWalkTiles { get; }
        public bool RecommendFastTravel { get; }
        public string BestBeaconId { get; }
        public string BestBeaconTitleZh { get; }
        public Vector2Int BestBeaconTile { get; }
        public float WalkAfterBeaconTiles { get; }
        public float TilesSavedByFastTravel { get; }
        public float EstimatedWalkSeconds { get; }
        public float EstimatedSprintSeconds { get; }
        public bool CrossesDangerZone { get; }
        public string SummaryZh { get; }
    }

    /// <summary>
    /// Compares direct traversal vs. warping to the nearest discovered fast-travel
    /// beacon across 500x-area maps, estimating walk/sprint duration under active
    /// weather modifiers and flagging whether the recommended walking leg crosses
    /// near any DangerZone marker.
    /// </summary>
    public static class RpgWorld500xFastTravelRoutePlanner
    {
        public const float BaseWalkTilesPerSecond = 6.5f;
        public const float BaseSprintTilesPerSecond = 11.5f;
        public const float DefaultFastTravelOverheadTiles = 16f;
        public const float DefaultDangerProximityTiles = 48f;

        public static RpgWorld500xRoutePlan PlanRoute(
            RpgWorldMapId mapId,
            Vector2 playerTile,
            Vector2Int destinationTile,
            RpgMapExplorationTracker tracker,
            float moveSpeedMultiplier = 1f,
            float fastTravelOverheadTiles = DefaultFastTravelOverheadTiles,
            float dangerProximityTiles = DefaultDangerProximityTiles,
            bool fastTravelLockedByEncounter = false)
        {
            Vector2 safePlayer = IsFinite(playerTile) ? playerTile : Vector2.zero;
            Vector2 destCenter = new Vector2(destinationTile.x + 0.5f, destinationTile.y + 0.5f);
            float directDist = Vector2.Distance(safePlayer, destCenter);

            float safeSpeedMult = (!float.IsNaN(moveSpeedMultiplier) && !float.IsInfinity(moveSpeedMultiplier) && moveSpeedMultiplier > 0.1f)
                ? moveSpeedMultiplier
                : 1f;
            float overhead = (!float.IsNaN(fastTravelOverheadTiles) && !float.IsInfinity(fastTravelOverheadTiles) && fastTravelOverheadTiles >= 0f)
                ? fastTravelOverheadTiles
                : DefaultFastTravelOverheadTiles;
            float dangerRadius = (!float.IsNaN(dangerProximityTiles) && !float.IsInfinity(dangerProximityTiles) && dangerProximityTiles > 0f)
                ? dangerProximityTiles
                : DefaultDangerProximityTiles;

            IReadOnlyList<RpgMapLandmarkMarker> markers = RpgWorldLandmarkCatalog.GetMarkers(mapId);

            bool hasBeacon = false;
            string bestBeaconId = string.Empty;
            string bestBeaconTitle = string.Empty;
            Vector2Int bestBeaconTile = default;
            float bestBeaconWalkDist = float.MaxValue;

            if (tracker != null && !fastTravelLockedByEncounter)
            {
                for (int i = 0; i < markers.Count; i++)
                {
                    RpgMapLandmarkMarker m = markers[i];
                    if (!m.SupportsFastTravel || !tracker.IsMarkerDiscovered(m.Id))
                    {
                        continue;
                    }

                    Vector2 beaconCenter = new Vector2(m.Tile.x + 0.5f, m.Tile.y + 0.5f);
                    float legDist = Vector2.Distance(beaconCenter, destCenter);
                    if (legDist < bestBeaconWalkDist)
                    {
                        hasBeacon = true;
                        bestBeaconWalkDist = legDist;
                        bestBeaconId = m.Id;
                        bestBeaconTitle = m.TitleZh;
                        bestBeaconTile = m.Tile;
                    }
                }
            }

            bool recommendFastTravel = hasBeacon && (bestBeaconWalkDist + overhead < directDist);
            float effectiveWalkDist = recommendFastTravel ? bestBeaconWalkDist : directDist;
            float tilesSaved = recommendFastTravel ? Mathf.Max(0f, directDist - bestBeaconWalkDist) : 0f;

            Vector2 walkStart = recommendFastTravel
                ? new Vector2(bestBeaconTile.x + 0.5f, bestBeaconTile.y + 0.5f)
                : safePlayer;
            bool crossesDanger = SegmentNearAnyDangerZone(markers, walkStart, destCenter, dangerRadius);

            float walkSeconds = effectiveWalkDist / (BaseWalkTilesPerSecond * safeSpeedMult);
            float sprintSeconds = effectiveWalkDist / (BaseSprintTilesPerSecond * safeSpeedMult);

            int walkTilesCeil = Mathf.CeilToInt(effectiveWalkDist);
            int savedTilesRound = Mathf.RoundToInt(tilesSaved);
            int walkSecCeil = Mathf.CeilToInt(walkSeconds);

            string summary;
            if (recommendFastTravel)
            {
                summary = "建议传送至【" + bestBeaconTitle + "】再步行 " + walkTilesCeil +
                          " 格（节省 " + savedTilesRound + " 格，约 " + walkSecCeil + " 秒）";
            }
            else if (fastTravelLockedByEncounter)
            {
                summary = "遭遇战进行中，传送暂不可用；脱战后步行约 " + walkTilesCeil + " 格（" + walkSecCeil + " 秒）";
            }
            else
            {
                summary = "直行更近（" + walkTilesCeil + " 格，约 " + walkSecCeil + " 秒）";
            }

            if (crossesDanger)
            {
                summary += " · 途经危险区";
            }

            return new RpgWorld500xRoutePlan(
                mapId,
                destinationTile,
                directDist,
                recommendFastTravel,
                hasBeacon ? bestBeaconId : string.Empty,
                hasBeacon ? bestBeaconTitle : string.Empty,
                hasBeacon ? bestBeaconTile : default,
                hasBeacon ? bestBeaconWalkDist : directDist,
                tilesSaved,
                walkSeconds,
                sprintSeconds,
                crossesDanger,
                summary);
        }

        private static bool SegmentNearAnyDangerZone(
            IReadOnlyList<RpgMapLandmarkMarker> markers,
            Vector2 start,
            Vector2 end,
            float radius)
        {
            float radiusSq = radius * radius;
            for (int i = 0; i < markers.Count; i++)
            {
                RpgMapLandmarkMarker m = markers[i];
                if (m.Category != RpgMapMarkerCategory.DangerZone)
                {
                    continue;
                }

                Vector2 point = new Vector2(m.Tile.x + 0.5f, m.Tile.y + 0.5f);
                if (PointToSegmentDistanceSq(point, start, end) <= radiusSq)
                {
                    return true;
                }
            }

            return false;
        }

        private static float PointToSegmentDistanceSq(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float lenSq = ab.sqrMagnitude;
            if (lenSq < 0.0001f)
            {
                return (p - a).sqrMagnitude;
            }

            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / lenSq);
            Vector2 proj = a + (ab * t);
            return (p - proj).sqrMagnitude;
        }

        private static bool IsFinite(Vector2 v)
        {
            return !float.IsNaN(v.x) && !float.IsInfinity(v.x) &&
                   !float.IsNaN(v.y) && !float.IsInfinity(v.y);
        }
    }
}
