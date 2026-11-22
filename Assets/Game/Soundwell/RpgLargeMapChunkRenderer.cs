using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Renders 4x-scale RPG tilemap layouts (Soundwell 96x60, Red Salt Harbor
    /// 140x84, Whitewater Crossing 80x44) into crisp 16-PPU, Point-filtered
    /// 32x32-tile SpriteRenderer chunks plus warm beacon halos at every
    /// RpgLight2DAnchor. Automatically loads authored pixel-art slices from
    /// RpgPixelArtManifest when tileSlices is omitted, keeping draw calls low
    /// (~6-15 chunks) and guaranteeing 1:1 world-to-tile alignment.
    /// </summary>
    public sealed class RpgLargeMapChunkRenderer : IDisposable
    {
        public const int ChunkSizeTiles = 32;
        public const int TilePixels = RpgWorldMapScale.PixelsPerUnit;

        private readonly GameObject rootObject;
        private readonly List<Texture2D> ownedTextures = new List<Texture2D>();
        private readonly List<Sprite> ownedSprites = new List<Sprite>();
        private readonly List<SpriteRenderer> chunkRenderers = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> lightHalos = new List<SpriteRenderer>();
        private bool disposed;

        public GameObject RootObject => rootObject;
        public int ChunkCount => chunkRenderers.Count;
        public int LightHaloCount => lightHalos.Count;
        public bool UsedRealArtSlices { get; }
        public bool IsDisposed => disposed || rootObject == null;

        public static RpgLargeMapChunkRenderer BuildForSoundwell(Transform parent,
            SoundwellLargeMapLayout layout, Sprite[] tileSlices = null)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (layout == null) throw new ArgumentNullException(nameof(layout));

            Sprite[] resolvedSlices = tileSlices ?? RpgPixelArtManifest.LoadDefault()?.SoundwellTiles;
            return new RpgLargeMapChunkRenderer(parent, "Soundwell large map chunks",
                layout.Width, layout.Height,
                (x, y) => (int)layout.Tiles[x, y],
                SoundwellFallbackColor,
                layout.PointLights,
                resolvedSlices);
        }

        public static RpgLargeMapChunkRenderer BuildForRedSaltHarbor(Transform parent,
            RedSaltHarborMapLayout layout, Sprite[] tileSlices = null)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (layout == null) throw new ArgumentNullException(nameof(layout));

            Sprite[] resolvedSlices = tileSlices ?? RpgPixelArtManifest.LoadDefault()?.HarborTiles;
            return new RpgLargeMapChunkRenderer(parent, "Red Salt Harbor large map chunks",
                layout.Width, layout.Height,
                (x, y) => (int)layout.Tiles[x, y],
                HarborFallbackColor,
                layout.PointLights,
                resolvedSlices);
        }

        /// <summary>
        /// Materializes the existing M06 Red Salt Mine Camp floorplan as actual
        /// 16-PPU SpriteRenderer chunks and authored point-light anchors.
        /// The camp uses SoundwellTileKind, not HarborTileKind.
        /// </summary>
        public static RpgLargeMapChunkRenderer BuildForMineCamp(Transform parent,
            M06MineCampMapLayout layout, Sprite[] tileSlices = null)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (layout == null) throw new ArgumentNullException(nameof(layout));

            Sprite[] resolvedSlices = tileSlices ?? RpgPixelArtManifest.LoadDefault()?.SoundwellTiles;
            return new RpgLargeMapChunkRenderer(parent, "M06 Red Salt Mine Camp chunks",
                layout.Width, layout.Height,
                (x, y) => (int)layout.Tiles[x, y],
                SoundwellFallbackColor,
                layout.PointLights,
                resolvedSlices);
        }

        public static RpgLargeMapChunkRenderer BuildForWhitewaterCrossing(Transform parent,
            WhitewaterCrossingMapLayout layout, Sprite[] tileSlices = null)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (layout == null) throw new ArgumentNullException(nameof(layout));

            Sprite[] resolvedSlices = tileSlices ?? RpgPixelArtManifest.LoadDefault()?.HarborTiles;
            return new RpgLargeMapChunkRenderer(parent, "Whitewater Crossing large map chunks",
                layout.Width, layout.Height,
                (x, y) => (int)layout.Tiles[x, y],
                HarborFallbackColor,
                layout.PointLights,
                resolvedSlices);
        }

        private RpgLargeMapChunkRenderer(Transform parent, string rootName,
            int mapWidth, int mapHeight, Func<int, int, int> tileIndexAt,
            Func<int, Color32> fallbackPalette,
            IReadOnlyList<RpgLight2DAnchor> lights,
            Sprite[] tileSlices)
        {
            rootObject = new GameObject(rootName);
            rootObject.transform.SetParent(parent, false);
            try
            {
                var cachedSlicePixels = ExtractReadableSlices(tileSlices, out int readableCount);
                UsedRealArtSlices = readableCount > 0;

                for (int cy = 0; cy < mapHeight; cy += ChunkSizeTiles)
                {
                    int chunkTilesH = Mathf.Min(ChunkSizeTiles, mapHeight - cy);
                    for (int cx = 0; cx < mapWidth; cx += ChunkSizeTiles)
                    {
                        int chunkTilesW = Mathf.Min(ChunkSizeTiles, mapWidth - cx);
                        BuildChunk(cx, cy, chunkTilesW, chunkTilesH,
                            tileIndexAt, fallbackPalette, cachedSlicePixels);
                    }
                }

                if (lights != null && lights.Count > 0)
                    BuildLightHalos(lights);
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        private void BuildChunk(int originX, int originY, int tilesW, int tilesH,
            Func<int, int, int> tileIndexAt, Func<int, Color32> fallbackPalette,
            Color32[][] slicePixels)
        {
            int texW = tilesW * TilePixels;
            int texH = tilesH * TilePixels;
            var pixels = new Color32[texW * texH];

            for (int ty = 0; ty < tilesH; ty++)
            {
                for (int tx = 0; tx < tilesW; tx++)
                {
                    int tileId = tileIndexAt(originX + tx, originY + ty);
                    Color32[] src = (slicePixels != null && tileId >= 0 && tileId < slicePixels.Length)
                        ? slicePixels[tileId]
                        : null;
                    Color32 baseColor = fallbackPalette(tileId);
                    BlitTile(pixels, texW, tx * TilePixels, ty * TilePixels, src, baseColor);
                }
            }

            var tex = new Texture2D(texW, texH, TextureFormat.RGBA32, mipChain: false);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.SetPixels32(pixels);
            tex.Apply(updateMipmaps: false, makeNoLongerReadable: true);
            ownedTextures.Add(tex);

            var sprite = Sprite.Create(tex, new Rect(0, 0, texW, texH),
                Vector2.zero, TilePixels);
            ownedSprites.Add(sprite);

            var chunkObj = new GameObject("Chunk_" + originX + "_" + originY);
            chunkObj.transform.SetParent(rootObject.transform, false);
            chunkObj.transform.localPosition = new Vector3(originX, originY, 0f);
            var sr = chunkObj.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = 0;
            chunkRenderers.Add(sr);
        }

        private void BuildLightHalos(IReadOnlyList<RpgLight2DAnchor> lights)
        {
            const int haloSize = 32;
            var haloTex = new Texture2D(haloSize, haloSize, TextureFormat.RGBA32, mipChain: false);
            haloTex.filterMode = FilterMode.Point;
            haloTex.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color32[haloSize * haloSize];
            float half = haloSize * 0.5f;
            for (int y = 0; y < haloSize; y++)
            {
                for (int x = 0; x < haloSize; x++)
                {
                    float dx = (x + 0.5f - half) / half;
                    float dy = (y + 0.5f - half) / half;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01(1f - dist);
                    // Quantize alpha into 4 crisp retro pixel bands.
                    float stepped = Mathf.Floor(alpha * alpha * 4f) / 4f;
                    byte a = (byte)Mathf.RoundToInt(stepped * 96f);
                    pixels[y * haloSize + x] = new Color32(255, 255, 255, a);
                }
            }
            haloTex.SetPixels32(pixels);
            haloTex.Apply(updateMipmaps: false, makeNoLongerReadable: true);
            ownedTextures.Add(haloTex);

            var haloSprite = Sprite.Create(haloTex, new Rect(0, 0, haloSize, haloSize),
                new Vector2(0.5f, 0.5f), TilePixels);
            ownedSprites.Add(haloSprite);

            for (int i = 0; i < lights.Count; i++)
            {
                RpgLight2DAnchor anchor = lights[i];
                var obj = new GameObject("Halo_" + anchor.Id);
                obj.transform.SetParent(rootObject.transform, false);
                obj.transform.localPosition = new Vector3(
                    anchor.Tile.x + 0.5f, anchor.Tile.y + 0.5f, 0f);
                float scale = (!float.IsNaN(anchor.OuterRadiusTiles) && !float.IsInfinity(anchor.OuterRadiusTiles))
                    ? Mathf.Max(1f, anchor.OuterRadiusTiles)
                    : 1f;
                obj.transform.localScale = new Vector3(scale, scale, 1f);
                var sr = obj.AddComponent<SpriteRenderer>();
                sr.sprite = haloSprite;
                sr.color = anchor.Color;
                sr.sortingOrder = 12;
                lightHalos.Add(sr);
            }
        }

        private static Color32[][] ExtractReadableSlices(Sprite[] slices, out int readableCount)
        {
            readableCount = 0;
            if (slices == null || slices.Length == 0)
                return null;

            var result = new Color32[slices.Length][];
            for (int i = 0; i < slices.Length; i++)
            {
                Sprite s = slices[i];
                if (s == null || s.texture == null || !s.texture.isReadable)
                    continue;
                Rect r = s.textureRect;
                int rx = Mathf.RoundToInt(r.x);
                int ry = Mathf.RoundToInt(r.y);
                int w = Mathf.RoundToInt(r.width);
                int h = Mathf.RoundToInt(r.height);
                if (w != TilePixels || h != TilePixels ||
                    rx < 0 || ry < 0 || rx + w > s.texture.width || ry + h > s.texture.height)
                    continue;
                Color[] raw = s.texture.GetPixels(rx, ry, w, h);
                var buf = new Color32[raw.Length];
                for (int p = 0; p < raw.Length; p++)
                    buf[p] = raw[p];
                result[i] = buf;
                readableCount++;
            }
            return result;
        }

        private static void BlitTile(Color32[] dest, int destWidth, int ox, int oy,
            Color32[] src, Color32 fallback)
        {
            if (src != null && src.Length == TilePixels * TilePixels)
            {
                for (int y = 0; y < TilePixels; y++)
                {
                    int dstRow = (oy + y) * destWidth + ox;
                    int srcRow = y * TilePixels;
                    for (int x = 0; x < TilePixels; x++)
                        dest[dstRow + x] = src[srcRow + x];
                }
                return;
            }

            // Crisp low-saturation bordered pixel brick fallback matching Return's palette.
            var edge = new Color32(
                (byte)Mathf.Max(0, fallback.r - 14),
                (byte)Mathf.Max(0, fallback.g - 14),
                (byte)Mathf.Max(0, fallback.b - 12),
                255);
            for (int y = 0; y < TilePixels; y++)
            {
                int dstRow = (oy + y) * destWidth + ox;
                for (int x = 0; x < TilePixels; x++)
                {
                    bool border = x == 0 || y == 0;
                    dest[dstRow + x] = border ? edge : fallback;
                }
            }
        }

        private static Color32 SoundwellFallbackColor(int tileId)
        {
            switch ((SoundwellTileKind)tileId)
            {
                case SoundwellTileKind.FloorPlain: return new Color32(54, 62, 72, 255);
                case SoundwellTileKind.FloorCracked: return new Color32(48, 55, 64, 255);
                case SoundwellTileKind.FloorPuddle: return new Color32(40, 58, 76, 255);
                case SoundwellTileKind.FloorMoss: return new Color32(48, 66, 62, 255);
                case SoundwellTileKind.WallTop: return new Color32(26, 30, 38, 255);
                case SoundwellTileKind.WallGrating: return new Color32(36, 42, 52, 255);
                case SoundwellTileKind.WallMoss: return new Color32(34, 48, 46, 255);
                case SoundwellTileKind.GateClosed: return new Color32(94, 72, 52, 255);
                case SoundwellTileKind.BellPedestal: return new Color32(168, 134, 72, 255);
                case SoundwellTileKind.WetClothSpot: return new Color32(84, 118, 136, 255);
                case SoundwellTileKind.ExtractionHatch: return new Color32(186, 148, 86, 255);
                default: return new Color32(44, 50, 58, 255);
            }
        }

        private static Color32 HarborFallbackColor(int tileId)
        {
            switch ((HarborTileKind)tileId)
            {
                case HarborTileKind.WaterDeep: return new Color32(22, 34, 52, 255);
                case HarborTileKind.WaterCalm: return new Color32(28, 44, 66, 255);
                case HarborTileKind.WaterFoam: return new Color32(62, 82, 102, 255);
                case HarborTileKind.WaterShallow: return new Color32(36, 54, 74, 255);
                case HarborTileKind.BridgePlank: return new Color32(102, 80, 58, 255);
                case HarborTileKind.BridgeCable: return new Color32(138, 112, 74, 255);
                case HarborTileKind.QuayStone: return new Color32(68, 74, 82, 255);
                case HarborTileKind.Brazier: return new Color32(198, 132, 58, 255);
                case HarborTileKind.TenderBoat: return new Color32(92, 68, 48, 255);
                default: return new Color32(58, 62, 70, 255);
            }
        }

        private static void DestroySafe(UnityEngine.Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(obj);
            else UnityEngine.Object.DestroyImmediate(obj);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            chunkRenderers.Clear();
            lightHalos.Clear();
            DestroySafe(rootObject);
            for (int i = 0; i < ownedSprites.Count; i++)
                DestroySafe(ownedSprites[i]);
            ownedSprites.Clear();
            for (int i = 0; i < ownedTextures.Count; i++)
                DestroySafe(ownedTextures[i]);
            ownedTextures.Clear();
        }
    }
}
