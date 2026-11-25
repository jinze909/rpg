using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rpg.Soundwell
{
    public enum RpgWorld500xWaypointCategory : byte
    {
        UndiscoveredLandmark = 1,
        UnlootedCache = 2,
        LockedShortcut = 3,
        UninspectedStorySpot = 4,
        FieldNpc = 5
    }

    public readonly struct RpgWorld500xWaypointGuidance
    {
        public RpgWorld500xWaypointGuidance(
            RpgWorld500xWaypointCategory category,
            string entityId,
            string targetLabelZh,
            string districtNameZh,
            Vector2Int targetTile,
            float distanceTiles,
            string compassDirectionZh,
            bool isArrived,
            string hudReadoutZh)
        {
            Category = category;
            EntityId = entityId ?? string.Empty;
            TargetLabelZh = targetLabelZh ?? string.Empty;
            DistrictNameZh = districtNameZh ?? string.Empty;
            TargetTile = targetTile;
            DistanceTiles = distanceTiles;
            CompassDirectionZh = compassDirectionZh ?? string.Empty;
            IsArrived = isArrived;
            HudReadoutZh = hudReadoutZh ?? string.Empty;
        }

        public RpgWorld500xWaypointCategory Category { get; }
        public string EntityId { get; }
        public string TargetLabelZh { get; }
        public string DistrictNameZh { get; }
        public Vector2Int TargetTile { get; }
        public float DistanceTiles { get; }
        public string CompassDirectionZh { get; }
        public bool IsArrived { get; }
        public string HudReadoutZh { get; }
    }

    /// <summary>
    /// Waypoint & compass navigation assistant for the 500x-area RPG maps.
    /// Locates the nearest actionable undiscovered landmarks, unlooted supply caches,
    /// locked shortcuts, uninspected story spots, or field NPCs and formats crisp
    /// 8-direction Chinese compass HUD readouts with district context.
    /// </summary>
    public static class RpgWorld500xWaypointNavigator
    {
        public static string ResolveCompassDirectionZh(Vector2 delta)
        {
            if (float.IsNaN(delta.x) || float.IsInfinity(delta.x) ||
                float.IsNaN(delta.y) || float.IsInfinity(delta.y) ||
                (Mathf.Abs(delta.x) < 0.001f && Mathf.Abs(delta.y) < 0.001f))
            {
                return "当前位置";
            }

            float angleDeg = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            if (angleDeg < 0f)
            {
                angleDeg += 360f;
            }

            if (angleDeg >= 337.5f || angleDeg < 22.5f) return "正东";
            if (angleDeg < 67.5f) return "东北";
            if (angleDeg < 112.5f) return "正北";
            if (angleDeg < 157.5f) return "西北";
            if (angleDeg < 202.5f) return "正西";
            if (angleDeg < 247.5f) return "西南";
            if (angleDeg < 292.5f) return "正南";
            return "东南";
        }

        public static RpgWorld500xWaypointGuidance BuildGuidance(
            RpgWorldMapId mapId,
            Vector2 playerTile,
            RpgWorld500xWaypointCategory category,
            string entityId,
            string labelZh,
            Vector2Int targetTile,
            float arrivalThresholdTiles = 8f)
        {
            Vector2 targetCenter = new Vector2(targetTile.x + 0.5f, targetTile.y + 0.5f);
            Vector2 delta = targetCenter - playerTile;
            float distance = delta.magnitude;
            string compass = ResolveCompassDirectionZh(delta);
            bool arrived = distance <= Math.Max(0f, arrivalThresholdTiles);

            string districtName = RpgWorld500xDistrictCatalog.TryResolveDistrictAtTile(mapId, targetTile, out RpgWorld500xDistrictDef dist)
                ? dist.DisplayNameZh
                : string.Empty;

            string safeLabel = string.IsNullOrEmpty(labelZh) ? entityId : labelZh;
            string readout;
            if (arrived)
            {
                readout = string.IsNullOrEmpty(districtName)
                    ? "已抵达 · " + safeLabel
                    : "已抵达 · " + safeLabel + "（" + districtName + "）";
            }
            else
            {
                // Never understate the remaining grid distance in the compass HUD.
                int distInt = Mathf.Max(1, Mathf.CeilToInt(distance));
                readout = string.IsNullOrEmpty(districtName)
                    ? compass + " " + distInt + " 格 · " + safeLabel
                    : compass + " " + distInt + " 格 · " + safeLabel + "（" + districtName + "）";
            }

            return new RpgWorld500xWaypointGuidance(
                category,
                entityId,
                safeLabel,
                districtName,
                targetTile,
                distance,
                compass,
                arrived,
                readout);
        }

        public static bool TryFindNearestTarget(
            RpgWorld500xMapLayout layout,
            Vector2 playerTile,
            RpgWorld500xWaypointCategory category,
            out RpgWorld500xWaypointGuidance guidance,
            RpgMapExplorationTracker explorationTracker = null,
            RpgWorld500xCachesAndShortcutsController cachesCtrl = null,
            Func<string, int> getInt = null,
            float arrivalThresholdTiles = 8f)
        {
            guidance = default;
            if (layout == null || !IsFinite(playerTile))
            {
                return false;
            }

            var candidates = new List<RpgWorld500xWaypointGuidance>();
            CollectCategoryCandidates(
                layout, playerTile, category, explorationTracker, cachesCtrl, getInt, arrivalThresholdTiles, candidates);

            if (candidates.Count == 0)
            {
                return false;
            }

            int bestIndex = 0;
            for (int i = 1; i < candidates.Count; i++)
            {
                if (candidates[i].DistanceTiles < candidates[bestIndex].DistanceTiles)
                {
                    bestIndex = i;
                }
            }

            guidance = candidates[bestIndex];
            return true;
        }

        public static List<RpgWorld500xWaypointGuidance> FindTopNearbyPointsOfInterest(
            RpgWorld500xMapLayout layout,
            Vector2 playerTile,
            int maxResults,
            RpgMapExplorationTracker explorationTracker = null,
            RpgWorld500xCachesAndShortcutsController cachesCtrl = null,
            Func<string, int> getInt = null,
            float arrivalThresholdTiles = 8f)
        {
            var results = new List<RpgWorld500xWaypointGuidance>();
            if (layout == null || !IsFinite(playerTile) || maxResults <= 0)
            {
                return results;
            }

            var all = new List<RpgWorld500xWaypointGuidance>();
            CollectCategoryCandidates(layout, playerTile, RpgWorld500xWaypointCategory.UndiscoveredLandmark,
                explorationTracker, cachesCtrl, getInt, arrivalThresholdTiles, all);
            CollectCategoryCandidates(layout, playerTile, RpgWorld500xWaypointCategory.UnlootedCache,
                explorationTracker, cachesCtrl, getInt, arrivalThresholdTiles, all);
            CollectCategoryCandidates(layout, playerTile, RpgWorld500xWaypointCategory.LockedShortcut,
                explorationTracker, cachesCtrl, getInt, arrivalThresholdTiles, all);
            CollectCategoryCandidates(layout, playerTile, RpgWorld500xWaypointCategory.UninspectedStorySpot,
                explorationTracker, cachesCtrl, getInt, arrivalThresholdTiles, all);
            CollectCategoryCandidates(layout, playerTile, RpgWorld500xWaypointCategory.FieldNpc,
                explorationTracker, cachesCtrl, getInt, arrivalThresholdTiles, all);

            all.Sort((a, b) => a.DistanceTiles.CompareTo(b.DistanceTiles));
            int count = Math.Min(maxResults, all.Count);
            for (int i = 0; i < count; i++)
            {
                results.Add(all[i]);
            }

            return results;
        }

        private static void CollectCategoryCandidates(
            RpgWorld500xMapLayout layout,
            Vector2 playerTile,
            RpgWorld500xWaypointCategory category,
            RpgMapExplorationTracker explorationTracker,
            RpgWorld500xCachesAndShortcutsController cachesCtrl,
            Func<string, int> getInt,
            float arrivalThresholdTiles,
            List<RpgWorld500xWaypointGuidance> output)
        {
            RpgWorldMapId mapId = layout.MapId;
            Func<string, int> read = getInt ?? (_ => 0);

            switch (category)
            {
                case RpgWorld500xWaypointCategory.UndiscoveredLandmark:
                    for (int i = 0; i < layout.Markers.Count; i++)
                    {
                        RpgMapLandmarkMarker m = layout.Markers[i];
                        // The live tracker owns discovery when present; standalone callers
                        // can still supply the persisted discovery store.
                        bool discovered = explorationTracker != null
                            ? explorationTracker.IsMarkerDiscovered(m.Id)
                            : read(RpgMapExplorationTracker.MarkerDiscoveryKey(m.Id)) == 1;
                        if (discovered)
                        {
                            continue;
                        }

                        output.Add(BuildGuidance(mapId, playerTile, category, m.Id, m.TitleZh, m.Tile, arrivalThresholdTiles));
                    }
                    break;

                case RpgWorld500xWaypointCategory.UnlootedCache:
                    IReadOnlyList<RpgSupplyCacheDef> caches = RpgWorld500xContentCatalog.GetCaches(mapId);
                    for (int i = 0; i < caches.Count; i++)
                    {
                        RpgSupplyCacheDef c = caches[i];
                        if ((cachesCtrl != null && cachesCtrl.IsCacheLooted(c.Id)) || read(c.MakinomKey) == 1)
                        {
                            continue;
                        }

                        output.Add(BuildGuidance(mapId, playerTile, category, c.Id, c.TitleZh, c.Tile, arrivalThresholdTiles));
                    }
                    break;

                case RpgWorld500xWaypointCategory.LockedShortcut:
                    IReadOnlyList<RpgWorldShortcutDef> shortcuts = RpgWorld500xContentCatalog.GetShortcuts(mapId);
                    for (int i = 0; i < shortcuts.Count; i++)
                    {
                        RpgWorldShortcutDef sc = shortcuts[i];
                        if ((cachesCtrl != null && cachesCtrl.IsShortcutUnlocked(sc.Id)) || read(sc.MakinomKey) == 1)
                        {
                            continue;
                        }

                        output.Add(BuildGuidance(mapId, playerTile, category, sc.Id, sc.TitleZh, sc.TriggerTile, arrivalThresholdTiles));
                    }
                    break;

                case RpgWorld500xWaypointCategory.UninspectedStorySpot:
                    IReadOnlyList<RpgStorySpotDef> spots = RpgWorld500xLivingWorldCatalog.GetStorySpots(mapId);
                    for (int i = 0; i < spots.Count; i++)
                    {
                        RpgStorySpotDef spot = spots[i];
                        if (read(spot.MakinomKey) == 1)
                        {
                            continue;
                        }

                        output.Add(BuildGuidance(mapId, playerTile, category, spot.Id, spot.TitleZh, spot.Tile, arrivalThresholdTiles));
                    }
                    break;

                case RpgWorld500xWaypointCategory.FieldNpc:
                    IReadOnlyList<RpgFieldNpcDef> npcs = RpgWorld500xLivingWorldCatalog.GetFieldNpcs(mapId);
                    for (int i = 0; i < npcs.Count; i++)
                    {
                        RpgFieldNpcDef npc = npcs[i];
                        // Completed rumor conversations should not permanently
                        // occupy the nearest unfinished exploration objective.
                        if (read(npc.RumorMakinomKey) == 1)
                            continue;
                        output.Add(BuildGuidance(mapId, playerTile, category, npc.Id, npc.DisplayNameZh, npc.Tile, arrivalThresholdTiles));
                    }
                    break;
            }
        }

        private static bool IsFinite(Vector2 v)
        {
            return !float.IsNaN(v.x) && !float.IsInfinity(v.x) &&
                   !float.IsNaN(v.y) && !float.IsInfinity(v.y);
        }
    }
}
