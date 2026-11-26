using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rpg.Soundwell
{
    public enum RpgWorld500xReconPingKind : byte
    {
        HiddenCache = 1,
        LockedShortcut = 2,
        UnheardRumorNpc = 3,
        UninspectedStorySpot = 4,
        AmbientEventZone = 5
    }

    public readonly struct RpgWorld500xReconPing
    {
        public RpgWorld500xReconPing(
            RpgWorld500xReconPingKind kind,
            string entityId,
            string titleZh,
            Vector2Int tile,
            float distanceTiles,
            string directionZh,
            string formattedPingZh)
        {
            Kind = kind;
            EntityId = entityId ?? string.Empty;
            TitleZh = titleZh ?? string.Empty;
            Tile = tile;
            DistanceTiles = distanceTiles;
            DirectionZh = directionZh ?? string.Empty;
            FormattedPingZh = formattedPingZh ?? string.Empty;
        }

        public RpgWorld500xReconPingKind Kind { get; }
        public string EntityId { get; }
        public string TitleZh { get; }
        public Vector2Int Tile { get; }
        public float DistanceTiles { get; }
        public string DirectionZh { get; }
        public string FormattedPingZh { get; }
    }

    /// <summary>
    /// Field Recon Pulse / Tactical Scanner for 500x-area RPG maps. Scans within a
    /// weather-modulated radius around the player and returns distance-sorted,
    /// 8-direction Chinese compass pings for unlooted caches, locked shortcuts,
    /// unheard rumor NPCs, uninspected story spots, and nearby ambient zones,
    /// with optional kind filtering and cooldown-free nearest-by-kind lookup.
    /// </summary>
    public sealed class RpgWorld500xFieldReconScannerService
    {
        public const float DefaultBaseScanRadiusTiles = 420f;
        public const float DefaultCooldownSeconds = 4f;

        private readonly Func<string, int> _getInt;
        private readonly float _cooldownSeconds;
        private float _lastScanTimeSeconds = float.NegativeInfinity;

        public float CooldownSeconds => _cooldownSeconds;

        public RpgWorld500xFieldReconScannerService(
            Func<string, int> getInt = null,
            float cooldownSeconds = DefaultCooldownSeconds)
        {
            _getInt = getInt ?? (_ => 0);
            _cooldownSeconds = (!float.IsNaN(cooldownSeconds) && !float.IsInfinity(cooldownSeconds) && cooldownSeconds >= 0f)
                ? cooldownSeconds
                : DefaultCooldownSeconds;
        }

        public float GetRemainingCooldown(float nowSeconds)
        {
            if (!IsFinite(nowSeconds) || nowSeconds < 0f)
            {
                return _cooldownSeconds;
            }

            if (float.IsNegativeInfinity(_lastScanTimeSeconds))
            {
                return 0f;
            }

            float elapsed = nowSeconds - _lastScanTimeSeconds;
            if (elapsed < 0f)
            {
                return 0f;
            }

            return Mathf.Max(0f, _cooldownSeconds - elapsed);
        }

        public bool TryPulseScan(
            RpgWorldMapId mapId,
            Vector2 playerTile,
            float nowSeconds,
            out List<RpgWorld500xReconPing> pings,
            out string summaryBannerZh,
            float baseRadiusTiles = DefaultBaseScanRadiusTiles,
            float visibilityMultiplier = 1f,
            int maxResults = 8,
            RpgWorld500xReconPingKind? filterKind = null)
        {
            pings = new List<RpgWorld500xReconPing>();
            summaryBannerZh = string.Empty;

            if (!IsFinite(playerTile) || !IsFinite(nowSeconds) || nowSeconds < 0f)
            {
                summaryBannerZh = "勘测坐标或时钟无效";
                return false;
            }

            // Invalid map requests must not consume a valid session's pulse cooldown.
            if (!TryGetPlayableMapSpec(mapId, out RpgExpandedMapSpec spec))
            {
                summaryBannerZh = "勘测区域无效";
                return false;
            }

            // Invalid positions and categories are not real scans and must
            // never start or reset a valid player's pulse cooldown.
            if (!IsInsideMap(playerTile, spec))
            {
                summaryBannerZh = "勘测坐标超出区域";
                return false;
            }

            if (filterKind.HasValue && !Enum.IsDefined(typeof(RpgWorld500xReconPingKind), filterKind.Value))
            {
                summaryBannerZh = "勘测分类无效";
                return false;
            }

            float remaining = GetRemainingCooldown(nowSeconds);
            if (remaining > 0.0001f)
            {
                summaryBannerZh = "勘测脉冲冷却中（剩余 " + Mathf.CeilToInt(remaining) + " 秒）";
                return false;
            }

            _lastScanTimeSeconds = nowSeconds;

            float safeRadius = (IsFinite(baseRadiusTiles) && baseRadiusTiles > 0f)
                ? baseRadiusTiles
                : DefaultBaseScanRadiusTiles;
            float safeVis = (IsFinite(visibilityMultiplier) && visibilityMultiplier > 0.1f)
                ? Mathf.Clamp(visibilityMultiplier, 0.35f, 1.50f)
                : 1f;
            float effectiveRadius = safeRadius * safeVis;
            int cap = Math.Max(1, maxResults);

            CollectByFilter(mapId, playerTile, effectiveRadius, filterKind, pings);

            pings.Sort((a, b) =>
            {
                int cmp = a.DistanceTiles.CompareTo(b.DistanceTiles);
                return cmp != 0 ? cmp : string.Compare(a.EntityId, b.EntityId, StringComparison.Ordinal);
            });

            if (pings.Count > cap)
            {
                pings.RemoveRange(cap, pings.Count - cap);
            }

            if (pings.Count == 0)
            {
                summaryBannerZh = "半径 " + Mathf.RoundToInt(effectiveRadius) + " 格内暂无未探索目标";
            }
            else
            {
                summaryBannerZh = "勘测发现 " + pings.Count + " 处近处目标 · 最近：" + pings[0].FormattedPingZh;
            }

            return true;
        }

        public bool TryFindNearestByKind(
            RpgWorldMapId mapId,
            Vector2 playerTile,
            RpgWorld500xReconPingKind kind,
            out RpgWorld500xReconPing nearest,
            float maxRadiusTiles = 2000f)
        {
            nearest = default;
            if (!IsFinite(playerTile) ||
                !TryGetPlayableMapSpec(mapId, out RpgExpandedMapSpec spec) ||
                !IsInsideMap(playerTile, spec) ||
                !Enum.IsDefined(typeof(RpgWorld500xReconPingKind), kind))
            {
                return false;
            }

            float safeRadius = (IsFinite(maxRadiusTiles) && maxRadiusTiles > 0f)
                ? maxRadiusTiles
                : 2000f;
            var candidates = new List<RpgWorld500xReconPing>();
            CollectByFilter(mapId, playerTile, safeRadius, kind, candidates);
            if (candidates.Count == 0)
            {
                return false;
            }

            candidates.Sort((a, b) =>
            {
                int cmp = a.DistanceTiles.CompareTo(b.DistanceTiles);
                return cmp != 0 ? cmp : string.Compare(a.EntityId, b.EntityId, StringComparison.Ordinal);
            });
            nearest = candidates[0];
            return true;
        }

        public static string ResolveDirection8Zh(Vector2 fromTile, Vector2Int toTile)
        {
            float dx = (toTile.x + 0.5f) - fromTile.x;
            float dy = (toTile.y + 0.5f) - fromTile.y;
            if ((dx * dx) + (dy * dy) < 1f)
            {
                return "脚下";
            }

            float angle = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
            if (angle < 0f)
            {
                angle += 360f;
            }

            int sector = Mathf.RoundToInt(angle / 45f) % 8;
            switch (sector)
            {
                case 0: return "正东";
                case 1: return "东北";
                case 2: return "正北";
                case 3: return "西北";
                case 4: return "正西";
                case 5: return "西南";
                case 6: return "正南";
                case 7: return "东南";
                default: return "正东";
            }
        }

        private void CollectByFilter(
            RpgWorldMapId mapId,
            Vector2 playerTile,
            float radius,
            RpgWorld500xReconPingKind? filterKind,
            List<RpgWorld500xReconPing> output)
        {
            if (!filterKind.HasValue || filterKind.Value == RpgWorld500xReconPingKind.HiddenCache)
            {
                CollectUnlootedCaches(mapId, playerTile, radius, output);
            }

            if (!filterKind.HasValue || filterKind.Value == RpgWorld500xReconPingKind.LockedShortcut)
            {
                CollectLockedShortcuts(mapId, playerTile, radius, output);
            }

            if (!filterKind.HasValue || filterKind.Value == RpgWorld500xReconPingKind.UnheardRumorNpc)
            {
                CollectUnheardRumors(mapId, playerTile, radius, output);
            }

            if (!filterKind.HasValue || filterKind.Value == RpgWorld500xReconPingKind.UninspectedStorySpot)
            {
                CollectUninspectedStorySpots(mapId, playerTile, radius, output);
            }

            if (!filterKind.HasValue || filterKind.Value == RpgWorld500xReconPingKind.AmbientEventZone)
            {
                CollectAmbientZones(mapId, playerTile, radius, output);
            }
        }

        private void CollectUnlootedCaches(
            RpgWorldMapId mapId,
            Vector2 playerTile,
            float radius,
            List<RpgWorld500xReconPing> output)
        {
            IReadOnlyList<RpgSupplyCacheDef> caches = RpgWorld500xContentCatalog.GetCaches(mapId);
            for (int i = 0; i < caches.Count; i++)
            {
                RpgSupplyCacheDef c = caches[i];
                if (_getInt(c.MakinomKey) == 1)
                {
                    continue;
                }

                TryAppendPing(RpgWorld500xReconPingKind.HiddenCache, "暗藏补给", c.Id, c.TitleZh, c.Tile, playerTile, radius, output);
            }
        }

        private void CollectLockedShortcuts(
            RpgWorldMapId mapId,
            Vector2 playerTile,
            float radius,
            List<RpgWorld500xReconPing> output)
        {
            IReadOnlyList<RpgWorldShortcutDef> shortcuts = RpgWorld500xContentCatalog.GetShortcuts(mapId);
            for (int i = 0; i < shortcuts.Count; i++)
            {
                RpgWorldShortcutDef sc = shortcuts[i];
                if (_getInt(sc.MakinomKey) == 1)
                {
                    continue;
                }

                TryAppendPing(RpgWorld500xReconPingKind.LockedShortcut, "待开捷径", sc.Id, sc.TitleZh, sc.TriggerTile, playerTile, radius, output);
            }
        }

        private void CollectUnheardRumors(
            RpgWorldMapId mapId,
            Vector2 playerTile,
            float radius,
            List<RpgWorld500xReconPing> output)
        {
            IReadOnlyList<RpgFieldNpcDef> npcs = RpgWorld500xLivingWorldCatalog.GetFieldNpcs(mapId);
            for (int i = 0; i < npcs.Count; i++)
            {
                RpgFieldNpcDef npc = npcs[i];
                if (_getInt(npc.RumorMakinomKey) == 1)
                {
                    continue;
                }

                TryAppendPing(RpgWorld500xReconPingKind.UnheardRumorNpc, "野外见闻", npc.Id, npc.DisplayNameZh, npc.Tile, playerTile, radius, output);
            }
        }

        private void CollectUninspectedStorySpots(
            RpgWorldMapId mapId,
            Vector2 playerTile,
            float radius,
            List<RpgWorld500xReconPing> output)
        {
            IReadOnlyList<RpgStorySpotDef> spots = RpgWorld500xLivingWorldCatalog.GetStorySpots(mapId);
            for (int i = 0; i < spots.Count; i++)
            {
                RpgStorySpotDef spot = spots[i];
                if (_getInt(spot.MakinomKey) == 1)
                {
                    continue;
                }

                TryAppendPing(RpgWorld500xReconPingKind.UninspectedStorySpot, "风物轶事", spot.Id, spot.TitleZh, spot.Tile, playerTile, radius, output);
            }
        }

        private void CollectAmbientZones(
            RpgWorldMapId mapId,
            Vector2 playerTile,
            float radius,
            List<RpgWorld500xReconPing> output)
        {
            IReadOnlyList<RpgAmbientMicroEventZoneDef> zones = RpgWorld500xLivingWorldCatalog.GetAmbientZones(mapId);
            for (int i = 0; i < zones.Count; i++)
            {
                RpgAmbientMicroEventZoneDef z = zones[i];
                TryAppendPing(RpgWorld500xReconPingKind.AmbientEventZone, "环境异象", z.Id, z.CaptionZh, z.CenterTile, playerTile, radius, output);
            }
        }

        private static void TryAppendPing(
            RpgWorld500xReconPingKind kind,
            string kindTagZh,
            string id,
            string titleZh,
            Vector2Int tile,
            Vector2 playerTile,
            float radius,
            List<RpgWorld500xReconPing> output)
        {
            float dist = Vector2.Distance(playerTile, new Vector2(tile.x + 0.5f, tile.y + 0.5f));
            if (dist > radius)
            {
                return;
            }

            string dir = ResolveDirection8Zh(playerTile, tile);
            string formatted = "[" + kindTagZh + "] " + titleZh + "（" + dir + " " + Mathf.CeilToInt(dist) + " 格）";
            output.Add(new RpgWorld500xReconPing(kind, id, titleZh, tile, dist, dir, formatted));
        }

        private static bool TryGetPlayableMapSpec(RpgWorldMapId mapId, out RpgExpandedMapSpec spec)
        {
            IReadOnlyList<RpgExpandedMapSpec> specs = RpgExpandedWorldScale.AllSpecs;
            for (int i = 0; i < specs.Count; i++)
            {
                if (specs[i].MapId == mapId)
                {
                    spec = specs[i];
                    return true;
                }
            }

            spec = default;
            return false;
        }

        private static bool IsInsideMap(Vector2 tile, RpgExpandedMapSpec spec) =>
            tile.x >= 0f && tile.x < spec.ExpandedWidth &&
            tile.y >= 0f && tile.y < spec.ExpandedHeight;

        private static bool IsFinite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
        private static bool IsFinite(Vector2 v) => IsFinite(v.x) && IsFinite(v.y);
    }
}
