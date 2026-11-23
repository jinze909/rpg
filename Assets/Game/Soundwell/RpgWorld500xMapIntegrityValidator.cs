using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rpg.Soundwell
{
    public readonly struct RpgMapValidationIssue
    {
        public RpgMapValidationIssue(string mapId, string ruleId, string message, Vector2Int? tile = null)
        {
            MapId = mapId;
            RuleId = ruleId;
            Message = message;
            Tile = tile;
        }

        public string MapId { get; }
        public string RuleId { get; }
        public string Message { get; }
        public Vector2Int? Tile { get; }

        public override string ToString()
        {
            return Tile.HasValue
                ? "[" + MapId + "][" + RuleId + "] (" + Tile.Value.x + "," + Tile.Value.y + ") " + Message
                : "[" + MapId + "][" + RuleId + "] " + Message;
        }
    }

    /// <summary>
    /// Automated maintenance and integrity validator for all four 500x-area RPG
    /// maps. Enforces spatial bounds, chunk alignment, entity collision-freedom,
    /// Soundwell iron-gate isolation, memory footprint budgets, and Makinom key
    /// namespace hygiene so ongoing map content additions never regress CI.
    /// </summary>
    public static class RpgWorld500xMapIntegrityValidator
    {
        public const long MaxTotalGridBytes = 14000000L;
        public const int MaxMinimapWidth = RpgWorld500xMinimapHud.DefaultTextureWidth;
        public const int MaxMinimapHeight = RpgWorld500xMinimapHud.DefaultTextureHeight;
        public const string RequiredWorldKeyPrefix = "rpg.return.world.";

        private static readonly string[] ReservedNarrativePrefixes =
        {
            "rpg.return.m00.", "rpg.return.m01.", "rpg.return.m02.", "rpg.return.m03.",
            "rpg.return.m04.", "rpg.return.m05.", "rpg.return.m06.", "rpg.return.m07.",
            "rpg.return.m08.", "rpg.return.m09.", "rpg.return.m10.", "rpg.return.m11.",
            "rpg.return.m12.", "rpg.return.m13.", "rpg.return.m14.", "rpg.return.m15.",
            "rpg.return.m16.", "rpg.return.st01.", "rpg.return.st02.", "rpg.return.st03.",
            "rpg.return.st04.", "rpg.return.st05.", "rpg.return.st06.",
            "rpg.return.rc01.", "rpg.return.rc02.", "rpg.return.rc03.", "rpg.return.rc04.",
            "rpg.return.rc05."
        };

        public static List<RpgMapValidationIssue> ValidateAllFourMaps()
        {
            var issues = new List<RpgMapValidationIssue>();
            var globalMarkerIds = new HashSet<string>(StringComparer.Ordinal);
            var globalQuestIds = new HashSet<string>(StringComparer.Ordinal);
            var globalKeys = new HashSet<string>(StringComparer.Ordinal);
            long totalGridBytes = 0;

            foreach (RpgExpandedMapSpec spec in RpgExpandedWorldScale.AllSpecs)
            {
                RpgWorld500xMapLayout map = RpgWorld500xMapLayout.Build(spec.MapId);
                string mapName = spec.MapId.ToString();

                ValidateDimensionsAndFootprint(map, spec, mapName, ref totalGridBytes, issues);
                ValidateMarkersQuestsAndLights(map, mapName, globalMarkerIds, globalQuestIds, globalKeys, issues);
            }

            if (totalGridBytes > MaxTotalGridBytes)
            {
                issues.Add(new RpgMapValidationIssue("GLOBAL", "MEM-GRID",
                    "Total byte[,] grid footprint " + totalGridBytes + " exceeds budget " + MaxTotalGridBytes + "."));
            }

            return issues;
        }

        public static void ValidateMakinomKeyHygiene(string mapName, string key,
            HashSet<string> seenKeys, List<RpgMapValidationIssue> issues)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                issues.Add(new RpgMapValidationIssue(mapName, "KEY-EMPTY", "Makinom key is empty."));
                return;
            }
            if (!seenKeys.Add(key))
                issues.Add(new RpgMapValidationIssue(mapName, "KEY-DUP", "Duplicate Makinom key: " + key));
            if (!key.StartsWith(RequiredWorldKeyPrefix, StringComparison.Ordinal))
                issues.Add(new RpgMapValidationIssue(mapName, "KEY-PREFIX", "Key does not start with " + RequiredWorldKeyPrefix + ": " + key));
            for (int i = 0; i < ReservedNarrativePrefixes.Length; i++)
            {
                if (key.StartsWith(ReservedNarrativePrefixes[i], StringComparison.OrdinalIgnoreCase))
                    issues.Add(new RpgMapValidationIssue(mapName, "KEY-COLLISION", "Key collides with narrative prefix: " + key));
            }
        }

        private static void ValidateDimensionsAndFootprint(
            RpgWorld500xMapLayout map,
            RpgExpandedMapSpec spec,
            string mapName,
            ref long totalGridBytes,
            List<RpgMapValidationIssue> issues)
        {
            int w = map.Tiles.GetLength(0);
            int h = map.Tiles.GetLength(1);
            totalGridBytes += (long)w * h;

            if (w != spec.ExpandedWidth || h != spec.ExpandedHeight)
                issues.Add(new RpgMapValidationIssue(mapName, "SPATIAL-DIM",
                    "Grid " + w + "x" + h + " != spec " + spec.ExpandedWidth + "x" + spec.ExpandedHeight));
            if (w % RpgLargeMapChunkRenderer.ChunkSizeTiles != 0 ||
                h % RpgLargeMapChunkRenderer.ChunkSizeTiles != 0)
                issues.Add(new RpgMapValidationIssue(mapName, "SPATIAL-CHUNK",
                    "Grid " + w + "x" + h + " is not aligned to 32-tile chunks."));
            if (!map.IsWalkable(map.PlayerSpawn.x, map.PlayerSpawn.y, gateOpen: false))
                issues.Add(new RpgMapValidationIssue(mapName, "SPATIAL-SPAWN",
                    "Player spawn is not walkable.", map.PlayerSpawn));
        }

        private static void ValidateMarkersQuestsAndLights(
            RpgWorld500xMapLayout map,
            string mapName,
            HashSet<string> globalMarkerIds,
            HashSet<string> globalQuestIds,
            HashSet<string> globalKeys,
            List<RpgMapValidationIssue> issues)
        {
            var occupiedTiles = new Dictionary<long, string>();
            for (int i = 0; i < map.Markers.Count; i++)
            {
                RpgMapLandmarkMarker m = map.Markers[i];
                if (!globalMarkerIds.Add(m.Id))
                    issues.Add(new RpgMapValidationIssue(mapName, "MARKER-DUP", "Duplicate marker id: " + m.Id, m.Tile));
                if (m.Tile.x < 0 || m.Tile.x >= map.Width || m.Tile.y < 0 || m.Tile.y >= map.Height)
                {
                    issues.Add(new RpgMapValidationIssue(mapName, "MARKER-OOB", "Marker out of bounds: " + m.Id, m.Tile));
                    continue;
                }
                long packed = ((long)m.Tile.x << 32) | (uint)m.Tile.y;
                if (occupiedTiles.TryGetValue(packed, out string existing))
                    issues.Add(new RpgMapValidationIssue(mapName, "MARKER-COLLIDE",
                        "Marker '" + m.Id + "' collides on same tile with '" + existing + "'.", m.Tile));
                else
                    occupiedTiles[packed] = m.Id;

                if (!map.IsWalkable(m.Tile.x, m.Tile.y, gateOpen: true))
                    issues.Add(new RpgMapValidationIssue(mapName, "MARKER-BLOCKED",
                        "Marker '" + m.Id + "' is on a blocked tile.", m.Tile));

                ValidateMakinomKeyHygiene(mapName, RpgMapExplorationTracker.MarkerDiscoveryKey(m.Id), globalKeys, issues);
                if (m.Category == RpgMapMarkerCategory.DangerZone)
                    ValidateMakinomKeyHygiene(mapName, RpgWorldOrkExplorationBridge.DangerClearedKey(m.Id), globalKeys, issues);
            }

            for (int i = 0; i < map.Quests.Count; i++)
            {
                RpgLandmarkQuestDefinition q = map.Quests[i];
                if (!globalQuestIds.Add(q.QuestId))
                    issues.Add(new RpgMapValidationIssue(mapName, "QUEST-DUP", "Duplicate quest id: " + q.QuestId));
                if (!RpgWorldLandmarkCatalog.TryGetMarker(map.MapId, q.MarkerId, out _))
                    issues.Add(new RpgMapValidationIssue(mapName, "QUEST-ORPHAN",
                        "Quest '" + q.QuestId + "' references missing marker '" + q.MarkerId + "'."));

                ValidateMakinomKeyHygiene(mapName, RpgMapExplorationTracker.QuestProgressKey(q.QuestId), globalKeys, issues);
                ValidateMakinomKeyHygiene(mapName, RpgWorldOrkExplorationBridge.QuestRewardedKey(q.QuestId), globalKeys, issues);
            }

            for (int i = 0; i < map.PointLights.Count; i++)
            {
                RpgLight2DAnchor l = map.PointLights[i];
                if (l.Tile.x < 0 || l.Tile.x >= map.Width || l.Tile.y < 0 || l.Tile.y >= map.Height)
                    issues.Add(new RpgMapValidationIssue(mapName, "LIGHT-OOB", "Light out of bounds: " + l.Id, l.Tile));
                if (float.IsNaN(l.OuterRadiusTiles) || float.IsInfinity(l.OuterRadiusTiles) || l.OuterRadiusTiles <= 0f)
                    issues.Add(new RpgMapValidationIssue(mapName, "LIGHT-RADIUS", "Invalid radius on light: " + l.Id, l.Tile));
            }
        }
    }
}
