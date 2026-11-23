using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rpg.Soundwell
{
    public readonly struct RpgWorld500xMapContentSummary
    {
        public RpgWorld500xMapContentSummary(
            RpgWorldMapId mapId,
            int landmarkCount,
            int cacheCount,
            int shortcutCount,
            int fieldNpcCount,
            int storySpotCount,
            int ambientZoneCount)
        {
            MapId = mapId;
            LandmarkCount = landmarkCount;
            CacheCount = cacheCount;
            ShortcutCount = shortcutCount;
            FieldNpcCount = fieldNpcCount;
            StorySpotCount = storySpotCount;
            AmbientZoneCount = ambientZoneCount;
        }

        public RpgWorldMapId MapId { get; }
        public int LandmarkCount { get; }
        public int CacheCount { get; }
        public int ShortcutCount { get; }
        public int FieldNpcCount { get; }
        public int StorySpotCount { get; }
        public int AmbientZoneCount { get; }
        public int TotalInteractiveCount =>
            LandmarkCount + CacheCount + ShortcutCount + FieldNpcCount + StorySpotCount + AmbientZoneCount;
    }

    public sealed class RpgWorld500xContentAuditReport
    {
        private readonly List<string> _errors = new List<string>();
        private readonly List<RpgWorld500xMapContentSummary> _summaries = new List<RpgWorld500xMapContentSummary>();

        public IReadOnlyList<string> Errors => _errors;
        public IReadOnlyList<RpgWorld500xMapContentSummary> Summaries => _summaries;
        public bool IsValid => _errors.Count == 0;

        public int TotalLandmarks { get; private set; }
        public int TotalCaches { get; private set; }
        public int TotalShortcuts { get; private set; }
        public int TotalFieldNpcs { get; private set; }
        public int TotalStorySpots { get; private set; }
        public int TotalAmbientZones { get; private set; }

        internal void AddError(string error)
        {
            if (!string.IsNullOrEmpty(error))
            {
                _errors.Add(error);
            }
        }

        internal void AddSummary(RpgWorld500xMapContentSummary summary)
        {
            _summaries.Add(summary);
            TotalLandmarks += summary.LandmarkCount;
            TotalCaches += summary.CacheCount;
            TotalShortcuts += summary.ShortcutCount;
            TotalFieldNpcs += summary.FieldNpcCount;
            TotalStorySpots += summary.StorySpotCount;
            TotalAmbientZones += summary.AmbientZoneCount;
        }
    }

    /// <summary>
    /// Cross-system map maintenance auditor for the 500x-area RPG world.
    /// Verifies zero coordinate collisions across landmarks, caches, shortcut triggers,
    /// field NPCs, and story spots, plus global ID/Makinom key hygiene, walkability,
    /// unlock-span bounds, rumor referential integrity, and ambient cycle validity.
    /// </summary>
    public static class RpgWorld500xEnrichedContentAuditor
    {
        private static readonly string[] ReservedNarrativePrefixes =
        {
            "rpg.return.m0", "rpg.return.m1",
            "rpg.return.st0", "rpg.return.rc0"
        };

        public static bool IsSafeWorldKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            if (!key.StartsWith("rpg.return.world.", StringComparison.Ordinal))
            {
                return false;
            }

            string lower = key.ToLowerInvariant();
            for (int i = 0; i < ReservedNarrativePrefixes.Length; i++)
            {
                if (lower.StartsWith(ReservedNarrativePrefixes[i], StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        public static RpgWorld500xContentAuditReport AuditAllMaps()
        {
            var report = new RpgWorld500xContentAuditReport();
            var globalIds = new HashSet<string>(StringComparer.Ordinal);
            var globalKeys = new HashSet<string>(StringComparer.Ordinal);

            foreach (RpgExpandedMapSpec spec in RpgExpandedWorldScale.AllSpecs)
            {
                AuditSingleMap(spec, globalIds, globalKeys, report);
            }

            return report;
        }

        private static void AuditSingleMap(
            RpgExpandedMapSpec spec,
            HashSet<string> globalIds,
            HashSet<string> globalKeys,
            RpgWorld500xContentAuditReport report)
        {
            RpgWorld500xMapLayout map = RpgWorld500xMapLayout.Build(spec.MapId);
            var occupiedTiles = new Dictionary<Vector2Int, string>();

            for (int i = 0; i < map.Markers.Count; i++)
            {
                RpgWorldMarkerDef marker = map.Markers[i];
                RegisterTile(occupiedTiles, marker.Tile, "Landmark:" + marker.Id, spec.DisplayNameEn, report);
            }

            IReadOnlyList<RpgSupplyCacheDef> caches = RpgWorld500xContentCatalog.GetCaches(spec.MapId);
            for (int i = 0; i < caches.Count; i++)
            {
                RpgSupplyCacheDef c = caches[i];
                ValidateIdAndKey("Cache", c.Id, c.MakinomKey, globalIds, globalKeys, report);
                ValidateWalkableInterior(map, c.Tile, "Cache:" + c.Id, spec.DisplayNameEn, report);
                RegisterTile(occupiedTiles, c.Tile, "Cache:" + c.Id, spec.DisplayNameEn, report);

                if (string.IsNullOrWhiteSpace(c.TitleZh) || string.IsNullOrWhiteSpace(c.RewardSummaryZh))
                {
                    report.AddError($"[{spec.DisplayNameEn}] Cache {c.Id} missing Chinese title or reward summary.");
                }

                if (c.SaltSilverReward <= 0)
                {
                    report.AddError($"[{spec.DisplayNameEn}] Cache {c.Id} must have positive SaltSilverReward.");
                }
            }

            IReadOnlyList<RpgWorldShortcutDef> shortcuts = RpgWorld500xContentCatalog.GetShortcuts(spec.MapId);
            for (int i = 0; i < shortcuts.Count; i++)
            {
                RpgWorldShortcutDef sc = shortcuts[i];
                ValidateIdAndKey("Shortcut", sc.Id, sc.MakinomKey, globalIds, globalKeys, report);
                ValidateWalkableInterior(map, sc.TriggerTile, "Shortcut:" + sc.Id, spec.DisplayNameEn, report);
                RegisterTile(occupiedTiles, sc.TriggerTile, "Shortcut:" + sc.Id, spec.DisplayNameEn, report);

                if (sc.UnlockSpan.width <= 0 || sc.UnlockSpan.height <= 0 ||
                    sc.UnlockSpan.xMin < 1 || sc.UnlockSpan.yMin < 1 ||
                    sc.UnlockSpan.xMax >= map.Width || sc.UnlockSpan.yMax >= map.Height)
                {
                    report.AddError($"[{spec.DisplayNameEn}] Shortcut {sc.Id} UnlockSpan {sc.UnlockSpan} is out of interior bounds.");
                }

                if (string.IsNullOrWhiteSpace(sc.TitleZh) || string.IsNullOrWhiteSpace(sc.UnlockedToastZh))
                {
                    report.AddError($"[{spec.DisplayNameEn}] Shortcut {sc.Id} missing Chinese title or unlock toast.");
                }
            }

            IReadOnlyList<RpgFieldNpcDef> npcs = RpgWorld500xLivingWorldCatalog.GetFieldNpcs(spec.MapId);
            for (int i = 0; i < npcs.Count; i++)
            {
                RpgFieldNpcDef npc = npcs[i];
                ValidateIdAndKey("FieldNpc", npc.Id, npc.RumorMakinomKey, globalIds, globalKeys, report);
                ValidateWalkableInterior(map, npc.Tile, "FieldNpc:" + npc.Id, spec.DisplayNameEn, report);
                RegisterTile(occupiedTiles, npc.Tile, "FieldNpc:" + npc.Id, spec.DisplayNameEn, report);

                if (string.IsNullOrWhiteSpace(npc.DisplayNameZh) || string.IsNullOrWhiteSpace(npc.AmbientLineZh))
                {
                    report.AddError($"[{spec.DisplayNameEn}] FieldNpc {npc.Id} missing display name or ambient line.");
                }

                if (!string.IsNullOrEmpty(npc.RumorTargetMarkerId))
                {
                    if (!RpgWorldLandmarkCatalog.TryGetMarker(spec.MapId, npc.RumorTargetMarkerId, out _))
                    {
                        report.AddError($"[{spec.DisplayNameEn}] FieldNpc {npc.Id} rumor target '{npc.RumorTargetMarkerId}' not found on map.");
                    }

                    if (string.IsNullOrWhiteSpace(npc.RumorHintZh))
                    {
                        report.AddError($"[{spec.DisplayNameEn}] FieldNpc {npc.Id} has rumor target but empty RumorHintZh.");
                    }
                }
            }

            IReadOnlyList<RpgStorySpotDef> spots = RpgWorld500xLivingWorldCatalog.GetStorySpots(spec.MapId);
            for (int i = 0; i < spots.Count; i++)
            {
                RpgStorySpotDef spot = spots[i];
                ValidateIdAndKey("StorySpot", spot.Id, spot.MakinomKey, globalIds, globalKeys, report);
                ValidateWalkableInterior(map, spot.Tile, "StorySpot:" + spot.Id, spec.DisplayNameEn, report);
                RegisterTile(occupiedTiles, spot.Tile, "StorySpot:" + spot.Id, spec.DisplayNameEn, report);

                if (string.IsNullOrWhiteSpace(spot.TitleZh) || string.IsNullOrWhiteSpace(spot.DescriptionZh))
                {
                    report.AddError($"[{spec.DisplayNameEn}] StorySpot {spot.Id} missing Chinese title or description.");
                }
            }

            IReadOnlyList<RpgAmbientMicroEventZoneDef> zones = RpgWorld500xLivingWorldCatalog.GetAmbientZones(spec.MapId);
            for (int i = 0; i < zones.Count; i++)
            {
                RpgAmbientMicroEventZoneDef z = zones[i];
                if (string.IsNullOrWhiteSpace(z.Id) || !globalIds.Add(z.Id))
                {
                    report.AddError($"[{spec.DisplayNameEn}] AmbientZone duplicate or empty id: {z.Id}");
                }

                if (z.CenterTile.x < 1 || z.CenterTile.x >= map.Width - 1 ||
                    z.CenterTile.y < 1 || z.CenterTile.y >= map.Height - 1)
                {
                    report.AddError($"[{spec.DisplayNameEn}] AmbientZone {z.Id} center {z.CenterTile} is out of bounds.");
                }

                if (z.RadiusTiles <= 0f || z.ActiveSeconds <= 0f || z.CycleSeconds <= z.ActiveSeconds)
                {
                    report.AddError($"[{spec.DisplayNameEn}] AmbientZone {z.Id} has invalid radius/cycle parameters.");
                }

                if (string.IsNullOrWhiteSpace(z.CaptionZh))
                {
                    report.AddError($"[{spec.DisplayNameEn}] AmbientZone {z.Id} missing Chinese caption.");
                }
            }

            report.AddSummary(new RpgWorld500xMapContentSummary(
                spec.MapId,
                map.Markers.Count,
                caches.Count,
                shortcuts.Count,
                npcs.Count,
                spots.Count,
                zones.Count));
        }

        private static void ValidateIdAndKey(
            string kind,
            string id,
            string key,
            HashSet<string> globalIds,
            HashSet<string> globalKeys,
            RpgWorld500xContentAuditReport report)
        {
            if (string.IsNullOrWhiteSpace(id) || !globalIds.Add(id))
            {
                report.AddError($"{kind} duplicate or empty id: {id}");
            }

            if (!IsSafeWorldKey(key))
            {
                report.AddError($"{kind} {id} has unsafe Makinom key: {key}");
            }
            else if (!globalKeys.Add(key))
            {
                report.AddError($"{kind} {id} duplicate Makinom key: {key}");
            }
        }

        private static void ValidateWalkableInterior(
            RpgWorld500xMapLayout map,
            Vector2Int tile,
            string label,
            string mapName,
            RpgWorld500xContentAuditReport report)
        {
            if (tile.x < 1 || tile.x >= map.Width - 1 || tile.y < 1 || tile.y >= map.Height - 1)
            {
                report.AddError($"[{mapName}] {label} tile {tile} is out of interior bounds.");
                return;
            }

            if (!map.IsWalkable(tile.x, tile.y, gateOpen: true))
            {
                report.AddError($"[{mapName}] {label} tile {tile} is not walkable.");
            }
        }

        private static void RegisterTile(
            Dictionary<Vector2Int, string> occupied,
            Vector2Int tile,
            string label,
            string mapName,
            RpgWorld500xContentAuditReport report)
        {
            if (occupied.TryGetValue(tile, out string existing))
            {
                report.AddError($"[{mapName}] Tile collision at {tile}: {existing} vs {label}");
                return;
            }

            occupied[tile] = label;
        }
    }
}
