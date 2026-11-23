using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Camera-windowed, chunk-culled 2D SpriteRenderer spawner for 500x world props
    /// (supply caches/relics, unlockable shortcuts, field NPCs, story spots, ambient
    /// zones, and danger zone markers). Uses RpgWorld500xProceduralPropAtlas and
    /// dynamically swaps Closed/Looted and Closed/Open sprites with zero native leaks.
    /// </summary>
    public sealed class RpgWorld500xPropVisualSpawner : IDisposable
    {
        private enum PropCategory : byte
        {
            Cache = 1,
            Shortcut = 2,
            FieldNpc = 3,
            StorySpot = 4,
            AmbientZone = 5,
            DangerZone = 6
        }

        private readonly struct PropEntry
        {
            public readonly string Id;
            public readonly Vector2Int Tile;
            public readonly Vector2Int Chunk;
            public readonly PropCategory Category;
            public readonly RpgWorld500xPropKind DefaultKind;
            public readonly RpgWorld500xPropKind CompletedKind;

            public PropEntry(
                string id,
                Vector2Int tile,
                PropCategory category,
                RpgWorld500xPropKind defaultKind,
                RpgWorld500xPropKind completedKind)
            {
                Id = id;
                Tile = tile;
                Chunk = ToChunk(tile);
                Category = category;
                DefaultKind = defaultKind;
                CompletedKind = completedKind;
            }
        }

        private readonly struct ActiveVisual
        {
            public readonly PropEntry Entry;
            public readonly GameObject GameObject;
            public readonly SpriteRenderer Renderer;

            public ActiveVisual(PropEntry entry, GameObject gameObject, SpriteRenderer renderer)
            {
                Entry = entry;
                GameObject = gameObject;
                Renderer = renderer;
            }
        }

        private readonly RpgWorldMapId _mapId;
        private readonly Transform _parent;
        private readonly RpgWorld500xCachesAndShortcutsController _cachesAndShortcuts;
        private readonly RpgWorld500xProceduralPropAtlas _atlas;
        private readonly bool _ownsAtlas;
        private readonly Dictionary<Vector2Int, List<PropEntry>> _entriesByChunk =
            new Dictionary<Vector2Int, List<PropEntry>>();
        private readonly Dictionary<string, ActiveVisual> _activeVisuals =
            new Dictionary<string, ActiveVisual>(StringComparer.Ordinal);
        private readonly List<string> _removalBuffer = new List<string>();
        private bool _disposed;

        public int TotalIndexedPropCount { get; }

        public int ActiveVisualCount => _activeVisuals.Count;

        public bool IsDisposed => _disposed;

        public RpgWorld500xPropVisualSpawner(
            RpgWorld500xMapLayout layout,
            Transform parent,
            RpgWorld500xCachesAndShortcutsController cachesAndShortcuts = null,
            RpgWorld500xProceduralPropAtlas sharedAtlas = null)
        {
            if (layout == null)
            {
                throw new ArgumentNullException(nameof(layout));
            }

            _mapId = layout.MapId;
            _parent = parent;
            _cachesAndShortcuts = cachesAndShortcuts;
            _ownsAtlas = sharedAtlas == null;
            _atlas = sharedAtlas ?? new RpgWorld500xProceduralPropAtlas();

            int indexed = 0;

            IReadOnlyList<RpgSupplyCacheDef> caches = RpgWorld500xContentCatalog.GetCaches(_mapId);
            for (int i = 0; i < caches.Count; i++)
            {
                RpgSupplyCacheDef c = caches[i];
                MapCachePropKinds(c.Kind, out RpgWorld500xPropKind closedKind, out RpgWorld500xPropKind lootedKind);
                AddEntry(new PropEntry(c.Id, c.Tile, PropCategory.Cache, closedKind, lootedKind));
                indexed++;
            }

            IReadOnlyList<RpgWorldShortcutDef> shortcuts = RpgWorld500xContentCatalog.GetShortcuts(_mapId);
            for (int i = 0; i < shortcuts.Count; i++)
            {
                RpgWorldShortcutDef sc = shortcuts[i];
                MapShortcutPropKinds(sc.Kind, out RpgWorld500xPropKind closedKind, out RpgWorld500xPropKind openKind);
                AddEntry(new PropEntry(sc.Id, sc.TriggerTile, PropCategory.Shortcut, closedKind, openKind));
                indexed++;
            }

            IReadOnlyList<RpgFieldNpcDef> npcs = RpgWorld500xLivingWorldCatalog.GetFieldNpcs(_mapId);
            for (int i = 0; i < npcs.Count; i++)
            {
                RpgFieldNpcDef npc = npcs[i];
                AddEntry(new PropEntry(npc.Id, npc.Tile, PropCategory.FieldNpc,
                    RpgWorld500xPropKind.FieldNpc, RpgWorld500xPropKind.FieldNpc));
                indexed++;
            }

            IReadOnlyList<RpgStorySpotDef> spots = RpgWorld500xLivingWorldCatalog.GetStorySpots(_mapId);
            for (int i = 0; i < spots.Count; i++)
            {
                RpgStorySpotDef spot = spots[i];
                AddEntry(new PropEntry(spot.Id, spot.Tile, PropCategory.StorySpot,
                    RpgWorld500xPropKind.StorySpot, RpgWorld500xPropKind.StorySpot));
                indexed++;
            }

            IReadOnlyList<RpgAmbientMicroEventZoneDef> zones = RpgWorld500xLivingWorldCatalog.GetAmbientZones(_mapId);
            for (int i = 0; i < zones.Count; i++)
            {
                RpgAmbientMicroEventZoneDef zone = zones[i];
                AddEntry(new PropEntry(zone.Id, zone.CenterTile, PropCategory.AmbientZone,
                    RpgWorld500xPropKind.AmbientEventMarker, RpgWorld500xPropKind.AmbientEventMarker));
                indexed++;
            }

            for (int i = 0; i < layout.Markers.Count; i++)
            {
                RpgMapLandmarkMarker marker = layout.Markers[i];
                if (marker.Category == RpgMapMarkerCategory.DangerZone)
                {
                    AddEntry(new PropEntry(marker.Id, marker.Tile, PropCategory.DangerZone,
                        RpgWorld500xPropKind.DangerZoneMarker, RpgWorld500xPropKind.DangerZoneMarker));
                    indexed++;
                }
            }

            TotalIndexedPropCount = indexed;
        }

        public bool UpdateFocus(Vector2 playerTile, int streamingRadiusChunks = 2)
        {
            if (_disposed ||
                float.IsNaN(playerTile.x) || float.IsInfinity(playerTile.x) ||
                float.IsNaN(playerTile.y) || float.IsInfinity(playerTile.y))
            {
                return false;
            }

            int radius = Math.Max(0, streamingRadiusChunks);
            Vector2Int centerChunk = ToChunk(new Vector2Int(
                Mathf.FloorToInt(playerTile.x),
                Mathf.FloorToInt(playerTile.y)));

            _removalBuffer.Clear();
            foreach (KeyValuePair<string, ActiveVisual> kvp in _activeVisuals)
            {
                Vector2Int c = kvp.Value.Entry.Chunk;
                if (Math.Abs(c.x - centerChunk.x) > radius || Math.Abs(c.y - centerChunk.y) > radius)
                {
                    _removalBuffer.Add(kvp.Key);
                }
            }

            for (int i = 0; i < _removalBuffer.Count; i++)
            {
                string id = _removalBuffer[i];
                if (_activeVisuals.TryGetValue(id, out ActiveVisual visual))
                {
                    DestroyUnityObject(visual.GameObject);
                    _activeVisuals.Remove(id);
                }
            }

            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    Vector2Int chunkCoord = new Vector2Int(centerChunk.x + dx, centerChunk.y + dy);
                    if (!_entriesByChunk.TryGetValue(chunkCoord, out List<PropEntry> bucket))
                    {
                        continue;
                    }

                    for (int i = 0; i < bucket.Count; i++)
                    {
                        PropEntry entry = bucket[i];
                        if (_activeVisuals.ContainsKey(entry.Id))
                        {
                            continue;
                        }

                        var go = new GameObject("World500xProp_" + entry.Id);
                        if (_parent != null)
                        {
                            go.transform.SetParent(_parent, false);
                        }

                        go.transform.localPosition = new Vector3(entry.Tile.x + 0.5f, entry.Tile.y + 0.5f, 0f);
                        var sr = go.AddComponent<SpriteRenderer>();
                        sr.sortingOrder = entry.Category == PropCategory.AmbientZone ? 8 : 14;
                        sr.sprite = _atlas.GetSprite(ResolveCurrentKind(entry), _mapId);
                        _activeVisuals[entry.Id] = new ActiveVisual(entry, go, sr);
                    }
                }
            }

            RefreshActiveSprites();
            return true;
        }

        public void RefreshActiveSprites()
        {
            if (_disposed)
            {
                return;
            }

            foreach (KeyValuePair<string, ActiveVisual> kvp in _activeVisuals)
            {
                ActiveVisual visual = kvp.Value;
                if (visual.Renderer == null)
                {
                    continue;
                }

                Sprite desired = _atlas.GetSprite(ResolveCurrentKind(visual.Entry), _mapId);
                if (!ReferenceEquals(visual.Renderer.sprite, desired))
                {
                    visual.Renderer.sprite = desired;
                }
            }
        }

        public bool TryGetActiveRenderer(string propId, out SpriteRenderer renderer)
        {
            if (!string.IsNullOrEmpty(propId) && _activeVisuals.TryGetValue(propId, out ActiveVisual visual))
            {
                renderer = visual.Renderer;
                return renderer != null;
            }

            renderer = null;
            return false;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            foreach (KeyValuePair<string, ActiveVisual> kvp in _activeVisuals)
            {
                DestroyUnityObject(kvp.Value.GameObject);
            }

            _activeVisuals.Clear();
            _entriesByChunk.Clear();
            if (_ownsAtlas)
            {
                _atlas.Dispose();
            }

            _disposed = true;
        }

        private RpgWorld500xPropKind ResolveCurrentKind(PropEntry entry)
        {
            if (_cachesAndShortcuts != null)
            {
                if (entry.Category == PropCategory.Cache && _cachesAndShortcuts.IsCacheLooted(entry.Id))
                {
                    return entry.CompletedKind;
                }

                if (entry.Category == PropCategory.Shortcut && _cachesAndShortcuts.IsShortcutUnlocked(entry.Id))
                {
                    return entry.CompletedKind;
                }
            }

            return entry.DefaultKind;
        }

        private void AddEntry(PropEntry entry)
        {
            if (!_entriesByChunk.TryGetValue(entry.Chunk, out List<PropEntry> bucket))
            {
                bucket = new List<PropEntry>();
                _entriesByChunk[entry.Chunk] = bucket;
            }

            bucket.Add(entry);
        }

        private static void MapCachePropKinds(
            RpgSupplyCacheKind kind,
            out RpgWorld500xPropKind closedKind,
            out RpgWorld500xPropKind lootedKind)
        {
            switch (kind)
            {
                case RpgSupplyCacheKind.BrineBarrel:
                    closedKind = RpgWorld500xPropKind.SupplyBarrelClosed;
                    lootedKind = RpgWorld500xPropKind.SupplyBarrelLooted;
                    return;
                case RpgSupplyCacheKind.SalvagePile:
                    closedKind = RpgWorld500xPropKind.SalvagePileClosed;
                    lootedKind = RpgWorld500xPropKind.SalvagePileLooted;
                    return;
                case RpgSupplyCacheKind.FieldRelic:
                    closedKind = RpgWorld500xPropKind.RelicShrineClosed;
                    lootedKind = RpgWorld500xPropKind.RelicShrineLooted;
                    return;
                case RpgSupplyCacheKind.SupplyCrate:
                default:
                    closedKind = RpgWorld500xPropKind.SupplyCrateClosed;
                    lootedKind = RpgWorld500xPropKind.SupplyCrateLooted;
                    return;
            }
        }

        private static void MapShortcutPropKinds(
            RpgWorldShortcutKind kind,
            out RpgWorld500xPropKind closedKind,
            out RpgWorld500xPropKind openKind)
        {
            if (kind == RpgWorldShortcutKind.CanalDrawbridge || kind == RpgWorldShortcutKind.FerryFootbridge)
            {
                closedKind = RpgWorld500xPropKind.ShortcutBridgeClosed;
                openKind = RpgWorld500xPropKind.ShortcutBridgeOpen;
                return;
            }

            closedKind = RpgWorld500xPropKind.ShortcutGateClosed;
            openKind = RpgWorld500xPropKind.ShortcutGateOpen;
        }

        private static Vector2Int ToChunk(Vector2Int tile)
        {
            return new Vector2Int(
                tile.x / RpgLargeMapChunkRenderer.ChunkSizeTiles,
                tile.y / RpgLargeMapChunkRenderer.ChunkSizeTiles);
        }

        private static void DestroyUnityObject(UnityEngine.Object obj)
        {
            if (obj == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(obj);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(obj);
            }
        }
    }
}
