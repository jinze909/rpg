using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Visual categories for zero-art procedural 16x16 (16-PPU) props used across
    /// the 500x-area world expansion (caches, relics, shortcuts, beacons, field NPCs,
    /// story spots, ambient micro-events, and danger zone markers).
    /// </summary>
    public enum RpgWorld500xPropKind
    {
        SupplyCrateClosed = 0,
        SupplyCrateLooted = 1,
        SupplyBarrelClosed = 2,
        SupplyBarrelLooted = 3,
        SalvagePileClosed = 4,
        SalvagePileLooted = 5,
        RelicShrineClosed = 6,
        RelicShrineLooted = 7,
        ShortcutGateClosed = 8,
        ShortcutGateOpen = 9,
        ShortcutBridgeClosed = 10,
        ShortcutBridgeOpen = 11,
        FastTravelBeaconUnlit = 12,
        FastTravelBeaconLit = 13,
        FieldNpc = 14,
        StorySpot = 15,
        AmbientEventMarker = 16,
        DangerZoneMarker = 17
    }

    /// <summary>
    /// Generates and caches crisp 16x16 Point-filtered 16-PPU pixel-art sprites for
    /// 500x world props with region-specific accent palettes and deterministic
    /// native Texture2D/Sprite disposal.
    /// </summary>
    public sealed class RpgWorld500xProceduralPropAtlas : IDisposable
    {
        public const int SpriteSize = 16;
        public const float PixelsPerUnit = 16f;

        private static readonly Color32 Outline = new Color32(24, 22, 28, 255);
        private static readonly Color32 WoodDark = new Color32(102, 66, 38, 255);
        private static readonly Color32 WoodMid = new Color32(150, 102, 58, 255);
        private static readonly Color32 WoodLight = new Color32(194, 142, 88, 255);
        private static readonly Color32 StoneDark = new Color32(72, 78, 86, 255);
        private static readonly Color32 StoneMid = new Color32(118, 126, 136, 255);
        private static readonly Color32 StoneLight = new Color32(168, 176, 184, 255);
        private static readonly Color32 IronDark = new Color32(58, 62, 72, 255);
        private static readonly Color32 IronLight = new Color32(138, 148, 162, 255);
        private static readonly Color32 GoldMid = new Color32(224, 176, 62, 255);
        private static readonly Color32 GoldBright = new Color32(252, 228, 132, 255);
        private static readonly Color32 DangerRed = new Color32(214, 58, 52, 255);
        private static readonly Color32 DangerAmber = new Color32(246, 184, 72, 255);
        private static readonly Color32 SkinTone = new Color32(236, 198, 164, 255);
        private static readonly Color32 CloakDark = new Color32(46, 52, 64, 255);
        private static readonly Color32 Parchment = new Color32(232, 218, 182, 255);

        private readonly Dictionary<PropCacheKey, CachedProp> _cache =
            new Dictionary<PropCacheKey, CachedProp>();
        private bool _disposed;

        private readonly struct PropCacheKey : IEquatable<PropCacheKey>
        {
            public readonly RpgWorld500xPropKind Kind;
            public readonly RpgWorldMapId MapId;

            public PropCacheKey(RpgWorld500xPropKind kind, RpgWorldMapId mapId)
            {
                Kind = kind;
                MapId = mapId;
            }

            public bool Equals(PropCacheKey other)
            {
                return Kind == other.Kind && MapId == other.MapId;
            }

            public override bool Equals(object obj)
            {
                return obj is PropCacheKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return ((int)Kind * 397) ^ (int)MapId;
                }
            }
        }

        private readonly struct CachedProp
        {
            public readonly Texture2D Texture;
            public readonly Sprite Sprite;

            public CachedProp(Texture2D texture, Sprite sprite)
            {
                Texture = texture;
                Sprite = sprite;
            }
        }

        public int CachedTextureCount => _cache.Count;

        public int CachedSpriteCount => _cache.Count;

        public bool IsDisposed => _disposed;

        public static Color32 GetRegionAccentColor(RpgWorldMapId mapId)
        {
            switch (mapId)
            {
                case RpgWorldMapId.RedSaltHarbor:
                    return new Color32(204, 108, 78, 255);
                case RpgWorldMapId.WhitewaterCrossing:
                    return new Color32(94, 162, 212, 255);
                case RpgWorldMapId.MineCamp:
                    return new Color32(216, 156, 72, 255);
                case RpgWorldMapId.Soundwell:
                default:
                    return new Color32(104, 174, 152, 255);
            }
        }

        public Sprite GetSprite(RpgWorld500xPropKind kind, RpgWorldMapId mapId = RpgWorldMapId.Soundwell)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(RpgWorld500xProceduralPropAtlas));
            }

            var key = new PropCacheKey(kind, mapId);
            if (_cache.TryGetValue(key, out CachedProp existing) && existing.Sprite != null)
            {
                return existing.Sprite;
            }

            CachedProp created = BuildProp(kind, mapId);
            _cache[key] = created;
            return created.Sprite;
        }

        public Sprite[] PrewarmAll(RpgWorldMapId mapId)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(RpgWorld500xProceduralPropAtlas));
            }

            var kinds = (RpgWorld500xPropKind[])Enum.GetValues(typeof(RpgWorld500xPropKind));
            var sprites = new Sprite[kinds.Length];
            for (int i = 0; i < kinds.Length; i++)
            {
                sprites[i] = GetSprite(kinds[i], mapId);
            }

            return sprites;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            foreach (KeyValuePair<PropCacheKey, CachedProp> entry in _cache)
            {
                DestroyUnityObject(entry.Value.Sprite);
                DestroyUnityObject(entry.Value.Texture);
            }

            _cache.Clear();
            _disposed = true;
        }

        private static CachedProp BuildProp(RpgWorld500xPropKind kind, RpgWorldMapId mapId)
        {
            var pixels = new Color32[SpriteSize * SpriteSize];
            Color32 accent = GetRegionAccentColor(mapId);

            switch (kind)
            {
                case RpgWorld500xPropKind.SupplyCrateClosed:
                    DrawCrate(pixels, false, accent);
                    break;
                case RpgWorld500xPropKind.SupplyCrateLooted:
                    DrawCrate(pixels, true, accent);
                    break;
                case RpgWorld500xPropKind.SupplyBarrelClosed:
                    DrawBarrel(pixels, false, accent);
                    break;
                case RpgWorld500xPropKind.SupplyBarrelLooted:
                    DrawBarrel(pixels, true, accent);
                    break;
                case RpgWorld500xPropKind.SalvagePileClosed:
                    DrawSalvagePile(pixels, false, accent);
                    break;
                case RpgWorld500xPropKind.SalvagePileLooted:
                    DrawSalvagePile(pixels, true, accent);
                    break;
                case RpgWorld500xPropKind.RelicShrineClosed:
                    DrawRelicShrine(pixels, false, accent);
                    break;
                case RpgWorld500xPropKind.RelicShrineLooted:
                    DrawRelicShrine(pixels, true, accent);
                    break;
                case RpgWorld500xPropKind.ShortcutGateClosed:
                    DrawShortcutGate(pixels, false, accent);
                    break;
                case RpgWorld500xPropKind.ShortcutGateOpen:
                    DrawShortcutGate(pixels, true, accent);
                    break;
                case RpgWorld500xPropKind.ShortcutBridgeClosed:
                    DrawShortcutBridge(pixels, false, accent);
                    break;
                case RpgWorld500xPropKind.ShortcutBridgeOpen:
                    DrawShortcutBridge(pixels, true, accent);
                    break;
                case RpgWorld500xPropKind.FastTravelBeaconUnlit:
                    DrawFastTravelBeacon(pixels, false, accent);
                    break;
                case RpgWorld500xPropKind.FastTravelBeaconLit:
                    DrawFastTravelBeacon(pixels, true, accent);
                    break;
                case RpgWorld500xPropKind.FieldNpc:
                    DrawFieldNpc(pixels, accent);
                    break;
                case RpgWorld500xPropKind.StorySpot:
                    DrawStorySpot(pixels, accent);
                    break;
                case RpgWorld500xPropKind.AmbientEventMarker:
                    DrawAmbientEventMarker(pixels, accent);
                    break;
                case RpgWorld500xPropKind.DangerZoneMarker:
                default:
                    DrawDangerZoneMarker(pixels);
                    break;
            }

            var texture = new Texture2D(SpriteSize, SpriteSize, TextureFormat.RGBA32, false)
            {
                name = $"World500xProp_{mapId}_{kind}",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, false);

            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, SpriteSize, SpriteSize),
                new Vector2(0.5f, 0.125f),
                PixelsPerUnit,
                0u,
                SpriteMeshType.FullRect);
            sprite.name = texture.name;

            return new CachedProp(texture, sprite);
        }

        private static void DrawCrate(Color32[] pixels, bool looted, Color32 accent)
        {
            int top = looted ? 9 : 13;
            FillRect(pixels, 2, 2, 12, top - 1, Outline);
            FillRect(pixels, 3, 3, 10, top - 3, WoodMid);
            FillRect(pixels, 3, top - 1, 10, 1, WoodLight);
            FillRect(pixels, 3, 3, 10, 1, WoodDark);

            if (!looted)
            {
                for (int i = 0; i < 8; i++)
                {
                    SetPixel(pixels, 4 + i, 4 + i, WoodDark);
                    SetPixel(pixels, 11 - i, 4 + i, WoodDark);
                }
                FillRect(pixels, 6, 11, 4, 2, GoldMid);
                SetPixel(pixels, 7, 12, accent);
                SetPixel(pixels, 8, 12, accent);
            }
            else
            {
                FillRect(pixels, 4, 6, 8, 3, Outline);
                FillRect(pixels, 5, 7, 6, 1, WoodDark);
            }
        }

        private static void DrawBarrel(Color32[] pixels, bool looted, Color32 accent)
        {
            int top = looted ? 10 : 13;
            FillRect(pixels, 4, 2, 8, top - 1, Outline);
            FillRect(pixels, 3, 4, 10, top - 5, Outline);
            FillRect(pixels, 5, 3, 6, top - 3, WoodMid);
            FillRect(pixels, 4, 5, 8, top - 7, WoodMid);
            FillRect(pixels, 5, 4, 2, top - 5, WoodLight);
            FillRect(pixels, 4, 5, 8, 1, IronDark);
            FillRect(pixels, 4, top - 3, 8, 1, IronDark);

            if (!looted)
            {
                FillRect(pixels, 6, 12, 4, 1, accent);
            }
            else
            {
                FillRect(pixels, 5, 8, 6, 2, Outline);
            }
        }

        private static void DrawSalvagePile(Color32[] pixels, bool looted, Color32 accent)
        {
            FillRect(pixels, 2, 2, 12, 4, Outline);
            FillRect(pixels, 3, 3, 10, 2, StoneMid);
            if (!looted)
            {
                FillRect(pixels, 4, 6, 8, 4, Outline);
                FillRect(pixels, 5, 7, 6, 2, IronLight);
                FillRect(pixels, 6, 10, 4, 2, GoldMid);
                SetPixel(pixels, 7, 8, accent);
                SetPixel(pixels, 8, 9, GoldBright);
            }
            else
            {
                FillRect(pixels, 5, 5, 6, 2, StoneDark);
            }
        }

        private static void DrawRelicShrine(Color32[] pixels, bool looted, Color32 accent)
        {
            FillRect(pixels, 3, 2, 10, 4, Outline);
            FillRect(pixels, 4, 3, 8, 2, StoneMid);
            FillRect(pixels, 5, 6, 6, 6, Outline);
            FillRect(pixels, 6, 7, 4, 4, StoneLight);

            if (!looted)
            {
                FillRect(pixels, 6, 11, 4, 4, Outline);
                FillRect(pixels, 7, 12, 2, 2, GoldBright);
                SetPixel(pixels, 7, 9, accent);
                SetPixel(pixels, 8, 9, accent);
            }
            else
            {
                FillRect(pixels, 6, 8, 4, 2, StoneDark);
            }
        }

        private static void DrawShortcutGate(Color32[] pixels, bool open, Color32 accent)
        {
            FillRect(pixels, 1, 2, 4, 12, Outline);
            FillRect(pixels, 2, 3, 2, 10, StoneMid);
            FillRect(pixels, 11, 2, 4, 12, Outline);
            FillRect(pixels, 12, 3, 2, 10, StoneMid);
            FillRect(pixels, 2, 12, 12, 3, Outline);
            FillRect(pixels, 3, 13, 10, 1, accent);

            if (!open)
            {
                for (int x = 5; x <= 10; x += 2)
                {
                    FillRect(pixels, x, 3, 1, 9, IronLight);
                }
                FillRect(pixels, 5, 7, 6, 1, IronDark);
            }
            else
            {
                FillRect(pixels, 5, 3, 6, 2, StoneDark);
            }
        }

        private static void DrawShortcutBridge(Color32[] pixels, bool open, Color32 accent)
        {
            FillRect(pixels, 1, 4, 14, 8, Outline);
            if (open)
            {
                FillRect(pixels, 2, 5, 12, 6, WoodMid);
                for (int x = 3; x <= 12; x += 3)
                {
                    FillRect(pixels, x, 5, 1, 6, WoodDark);
                }
                FillRect(pixels, 2, 10, 12, 1, WoodLight);
                SetPixel(pixels, 2, 5, accent);
                SetPixel(pixels, 13, 5, accent);
            }
            else
            {
                FillRect(pixels, 2, 5, 4, 6, WoodDark);
                FillRect(pixels, 10, 5, 4, 6, WoodDark);
                FillRect(pixels, 6, 6, 4, 4, IronDark);
            }
        }

        private static void DrawFastTravelBeacon(Color32[] pixels, bool lit, Color32 accent)
        {
            FillRect(pixels, 4, 2, 8, 3, Outline);
            FillRect(pixels, 5, 3, 6, 1, StoneMid);
            FillRect(pixels, 6, 5, 4, 6, Outline);
            FillRect(pixels, 7, 6, 2, 4, StoneLight);

            if (lit)
            {
                FillRect(pixels, 5, 11, 6, 4, Outline);
                FillRect(pixels, 6, 12, 4, 2, accent);
                FillRect(pixels, 7, 13, 2, 2, GoldBright);
            }
            else
            {
                FillRect(pixels, 6, 11, 4, 2, IronDark);
            }
        }

        private static void DrawFieldNpc(Color32[] pixels, Color32 accent)
        {
            FillRect(pixels, 5, 2, 6, 6, Outline);
            FillRect(pixels, 6, 3, 4, 4, CloakDark);
            FillRect(pixels, 5, 7, 6, 2, accent);
            FillRect(pixels, 5, 9, 6, 5, Outline);
            FillRect(pixels, 6, 10, 4, 3, SkinTone);
            SetPixel(pixels, 7, 11, Outline);
            SetPixel(pixels, 9, 11, Outline);
        }

        private static void DrawStorySpot(Color32[] pixels, Color32 accent)
        {
            FillRect(pixels, 3, 3, 10, 10, Outline);
            FillRect(pixels, 4, 4, 8, 8, Parchment);
            FillRect(pixels, 5, 10, 6, 1, accent);
            FillRect(pixels, 5, 8, 6, 1, WoodDark);
            FillRect(pixels, 5, 6, 4, 1, WoodDark);
            FillRect(pixels, 10, 4, 2, 2, GoldMid);
        }

        private static void DrawAmbientEventMarker(Color32[] pixels, Color32 accent)
        {
            FillRect(pixels, 4, 4, 8, 8, Outline);
            FillRect(pixels, 5, 5, 6, 6, accent);
            FillRect(pixels, 7, 7, 2, 2, GoldBright);
            SetPixel(pixels, 7, 13, GoldBright);
            SetPixel(pixels, 8, 2, GoldBright);
            SetPixel(pixels, 2, 8, GoldBright);
            SetPixel(pixels, 13, 7, GoldBright);
        }

        private static void DrawDangerZoneMarker(Color32[] pixels)
        {
            for (int y = 2; y <= 13; y++)
            {
                int dist = Math.Abs(y - 7);
                int halfWidth = Math.Max(0, 6 - dist);
                for (int x = 7 - halfWidth; x <= 8 + halfWidth; x++)
                {
                    bool border = x == 7 - halfWidth || x == 8 + halfWidth || y == 2 || y == 13;
                    SetPixel(pixels, x, y, border ? Outline : DangerRed);
                }
            }

            FillRect(pixels, 7, 7, 2, 4, DangerAmber);
            FillRect(pixels, 7, 4, 2, 2, DangerAmber);
        }

        private static void FillRect(Color32[] pixels, int x, int y, int width, int height, Color32 color)
        {
            for (int py = y; py < y + height; py++)
            {
                for (int px = x; px < x + width; px++)
                {
                    SetPixel(pixels, px, py, color);
                }
            }
        }

        private static void SetPixel(Color32[] pixels, int x, int y, Color32 color)
        {
            if (x < 0 || x >= SpriteSize || y < 0 || y >= SpriteSize)
            {
                return;
            }

            pixels[(y * SpriteSize) + x] = color;
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
