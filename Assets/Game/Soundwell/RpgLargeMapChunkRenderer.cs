using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Renders RPG tilemap layouts into crisp 16-PPU, Point-filtered 32x32-tile
    /// SpriteRenderer chunks plus warm beacon halos at every RpgLight2DAnchor.
    /// Supports both eager full-map builds (for compact maps) and camera-windowed
    /// chunk streaming (for 500x-area world maps, bounding active GPU textures to
    /// (2R+1)^2 chunks around the focus tile with immediate native cleanup).
    /// </summary>
    public sealed class RpgLargeMapChunkRenderer : IDisposable
    {
        public const int ChunkSizeTiles = 32;
        public const int TilePixels = RpgWorldMapScale.PixelsPerUnit;

        private readonly struct ChunkHandle
        {
            public ChunkHandle(GameObject obj, SpriteRenderer renderer, Sprite sprite, Texture2D texture)
            {
                Obj = obj;
                Renderer = renderer;
                Sprite = sprite;
                Texture = texture;
            }

            public GameObject Obj { get; }
            public SpriteRenderer Renderer { get; }
            public Sprite Sprite { get; }
            public Texture2D Texture { get; }
        }

        private readonly GameObject rootObject;
        private readonly int mapWidth;
        private readonly int mapHeight;
        private readonly int chunkGridWidth;
        private readonly int chunkGridHeight;
        private readonly Func<int, int, int> tileIndexAt;
        private readonly Func<int, Color32> fallbackPalette;
        private readonly Color32[][] cachedSlicePixels;
        private readonly bool isStreaming;
        private int streamingRadiusChunks;
        private Vector2Int lastFocusChunk = new Vector2Int(int.MinValue, int.MinValue);

        private readonly Dictionary<Vector2Int, ChunkHandle> activeChunks = new Dictionary<Vector2Int, ChunkHandle>();
        private readonly List<Texture2D> haloTextures = new List<Texture2D>();
        private readonly List<Sprite> haloSprites = new List<Sprite>();
        private readonly List<SpriteRenderer> lightHalos = new List<SpriteRenderer>();
        private readonly List<RpgLight2DAnchor> lightAnchors = new List<RpgLight2DAnchor>();
        private readonly List<Vector2Int> unloadScratch = new List<Vector2Int>();
        private bool disposed;

        public GameObject RootObject => rootObject;
        public int MapWidth => mapWidth;
        public int MapHeight => mapHeight;
        public int ChunkCount => activeChunks.Count;
        public int TotalPossibleChunks => chunkGridWidth * chunkGridHeight;
        public int LightHaloCount => lightHalos.Count;
        public bool IsStreaming => isStreaming;
        public int StreamingRadiusChunks => streamingRadiusChunks;
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
                resolvedSlices,
                streaming: false,
                initialFocusTile: Vector2.zero,
                streamingRadius: 0);
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
                resolvedSlices,
                streaming: false,
                initialFocusTile: Vector2.zero,
                streamingRadius: 0);
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
                resolvedSlices,
                streaming: false,
                initialFocusTile: Vector2.zero,
                streamingRadius: 0);
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
                resolvedSlices,
                streaming: false,
                initialFocusTile: Vector2.zero,
                streamingRadius: 0);
        }

        /// <summary>
        /// Builds a camera-windowed streaming chunk renderer for 500x-area world
        /// maps. Only chunks within streamingRadiusChunks of focusTile are
        /// materialized; distant chunks are unloaded and destroyed immediately.
        /// </summary>
        public static RpgLargeMapChunkRenderer BuildStreaming(
            Transform parent,
            string rootName,
            int mapWidth,
            int mapHeight,
            Func<int, int, int> tileIndexAt,
            bool useHarborPalette,
            IReadOnlyList<RpgLight2DAnchor> lights,
            Vector2 initialFocusTile,
            int streamingRadiusChunks = 3,
            Sprite[] tileSlices = null)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (tileIndexAt == null) throw new ArgumentNullException(nameof(tileIndexAt));
            if (mapWidth <= 0) throw new ArgumentOutOfRangeException(nameof(mapWidth));
            if (mapHeight <= 0) throw new ArgumentOutOfRangeException(nameof(mapHeight));
            if (streamingRadiusChunks < 0 ||
                streamingRadiusChunks > MaxUsefulStreamingRadius(mapWidth, mapHeight))
                throw new ArgumentOutOfRangeException(nameof(streamingRadiusChunks));

            RpgPixelArtManifest manifest = tileSlices == null ? RpgPixelArtManifest.LoadDefault() : null;
            Sprite[] resolvedSlices = tileSlices ??
                (useHarborPalette ? manifest?.HarborTiles : manifest?.SoundwellTiles);
            Func<int, Color32> palette = useHarborPalette
                ? (Func<int, Color32>)HarborFallbackColor
                : SoundwellFallbackColor;
            string resolvedName = string.IsNullOrEmpty(rootName) ? "Streaming large map chunks" : rootName;
            Vector2 safeFocus = IsFiniteVector(initialFocusTile) ? initialFocusTile : Vector2.zero;

            return new RpgLargeMapChunkRenderer(parent, resolvedName,
                mapWidth, mapHeight, tileIndexAt, palette, lights, resolvedSlices,
                streaming: true,
                initialFocusTile: safeFocus,
                streamingRadius: streamingRadiusChunks);
        }

        private RpgLargeMapChunkRenderer(Transform parent, string rootName,
            int mapWidth, int mapHeight, Func<int, int, int> tileIndexAt,
            Func<int, Color32> fallbackPalette,
            IReadOnlyList<RpgLight2DAnchor> lights,
            Sprite[] tileSlices,
            bool streaming,
            Vector2 initialFocusTile,
            int streamingRadius)
        {
            this.mapWidth = mapWidth;
            this.mapHeight = mapHeight;
            chunkGridWidth = 1 + (mapWidth - 1) / ChunkSizeTiles;
            chunkGridHeight = 1 + (mapHeight - 1) / ChunkSizeTiles;
            this.tileIndexAt = tileIndexAt;
            this.fallbackPalette = fallbackPalette;
            isStreaming = streaming;
            streamingRadiusChunks = streamingRadius;

            rootObject = new GameObject(rootName);
            rootObject.transform.SetParent(parent, false);
            try
            {
                cachedSlicePixels = ExtractReadableSlices(tileSlices, out int readableCount);
                UsedRealArtSlices = readableCount > 0;

                if (lights != null && lights.Count > 0)
                    BuildLightHalos(lights);

                if (isStreaming)
                {
                    UpdateStreamingFocus(initialFocusTile);
                }
                else
                {
                    for (int cy = 0; cy < chunkGridHeight; cy++)
                    {
                        for (int cx = 0; cx < chunkGridWidth; cx++)
                            LoadChunk(new Vector2Int(cx, cy));
                    }
                }
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public bool UpdateStreamingFocus(Vector2 focusTile)
        {
            return UpdateStreamingFocus(focusTile, streamingRadiusChunks);
        }

        public bool UpdateStreamingFocus(Vector2 focusTile, int radiusChunks)
        {
            if (IsDisposed || !isStreaming || radiusChunks < 0 ||
                radiusChunks > MaxUsefulStreamingRadius(mapWidth, mapHeight) ||
                !IsFiniteVector(focusTile))
                return false;

            // Clamp in floating point before conversion; even finite coordinates
            // can exceed the integer range and otherwise select the wrong edge.
            int centerCx = Mathf.FloorToInt(Mathf.Clamp(
                focusTile.x / ChunkSizeTiles, 0f, chunkGridWidth - 1));
            int centerCy = Mathf.FloorToInt(Mathf.Clamp(
                focusTile.y / ChunkSizeTiles, 0f, chunkGridHeight - 1));
            var centerChunk = new Vector2Int(centerCx, centerCy);

            if (centerChunk == lastFocusChunk && radiusChunks == streamingRadiusChunks && activeChunks.Count > 0)
                return true;

            streamingRadiusChunks = radiusChunks;
            lastFocusChunk = centerChunk;

            int minCx = Mathf.Max(0, centerCx - radiusChunks);
            int maxCx = Mathf.Min(chunkGridWidth - 1, centerCx + radiusChunks);
            int minCy = Mathf.Max(0, centerCy - radiusChunks);
            int maxCy = Mathf.Min(chunkGridHeight - 1, centerCy + radiusChunks);

            unloadScratch.Clear();
            foreach (KeyValuePair<Vector2Int, ChunkHandle> kvp in activeChunks)
            {
                Vector2Int coord = kvp.Key;
                if (coord.x < minCx || coord.x > maxCx || coord.y < minCy || coord.y > maxCy)
                    unloadScratch.Add(coord);
            }
            for (int i = 0; i < unloadScratch.Count; i++)
                UnloadChunk(unloadScratch[i]);
            unloadScratch.Clear();

            for (int cy = minCy; cy <= maxCy; cy++)
            {
                for (int cx = minCx; cx <= maxCx; cx++)
                {
                    var coord = new Vector2Int(cx, cy);
                    if (!activeChunks.ContainsKey(coord))
                        LoadChunk(coord);
                }
            }

            UpdateLightHaloVisibility(minCx * ChunkSizeTiles, (maxCx + 1) * ChunkSizeTiles,
                minCy * ChunkSizeTiles, (maxCy + 1) * ChunkSizeTiles);
            return true;
        }

        private static int MaxUsefulStreamingRadius(int width, int height)
        {
            // No radius larger than the entire map is useful. Keep the
            // documented default of three valid even on tiny preview maps.
            return Math.Max(3, Math.Max(
                1 + (width - 1) / ChunkSizeTiles,
                1 + (height - 1) / ChunkSizeTiles));
        }

        private void LoadChunk(Vector2Int chunkCoord)
        {
            int originX = chunkCoord.x * ChunkSizeTiles;
            int originY = chunkCoord.y * ChunkSizeTiles;
            int tilesW = Mathf.Min(ChunkSizeTiles, mapWidth - originX);
            int tilesH = Mathf.Min(ChunkSizeTiles, mapHeight - originY);
            if (tilesW <= 0 || tilesH <= 0)
                return;

            int texW = tilesW * TilePixels;
            int texH = tilesH * TilePixels;
            var pixels = new Color32[texW * texH];

            for (int ty = 0; ty < tilesH; ty++)
            {
                for (int tx = 0; tx < tilesW; tx++)
                {
                    int tileId = tileIndexAt(originX + tx, originY + ty);
                    Color32[] src = (cachedSlicePixels != null && tileId >= 0 && tileId < cachedSlicePixels.Length)
                        ? cachedSlicePixels[tileId]
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

            var sprite = Sprite.Create(tex, new Rect(0, 0, texW, texH),
                Vector2.zero, TilePixels);

            var chunkObj = new GameObject("Chunk_" + originX + "_" + originY);
            chunkObj.transform.SetParent(rootObject.transform, false);
            chunkObj.transform.localPosition = new Vector3(originX, originY, 0f);
            var sr = chunkObj.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = 0;

            activeChunks[chunkCoord] = new ChunkHandle(chunkObj, sr, sprite, tex);
        }

        private void UnloadChunk(Vector2Int chunkCoord)
        {
            if (!activeChunks.TryGetValue(chunkCoord, out ChunkHandle handle))
                return;
            activeChunks.Remove(chunkCoord);
            DestroySafe(handle.Obj);
            DestroySafe(handle.Sprite);
            DestroySafe(handle.Texture);
        }

        private void UpdateLightHaloVisibility(int minTileX, int maxTileX, int minTileY, int maxTileY)
        {
            for (int i = 0; i < lightHalos.Count && i < lightAnchors.Count; i++)
            {
                SpriteRenderer sr = lightHalos[i];
                if (sr == null) continue;
                RpgLight2DAnchor anchor = lightAnchors[i];
                float pad = (!float.IsNaN(anchor.OuterRadiusTiles) && !float.IsInfinity(anchor.OuterRadiusTiles))
                    ? Mathf.Max(1f, anchor.OuterRadiusTiles)
                    : 1f;
                bool visible = anchor.Tile.x + pad >= minTileX && anchor.Tile.x - pad <= maxTileX &&
                    anchor.Tile.y + pad >= minTileY && anchor.Tile.y - pad <= maxTileY;
                if (sr.gameObject.activeSelf != visible)
                    sr.gameObject.SetActive(visible);
            }
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
            haloTextures.Add(haloTex);

            var haloSprite = Sprite.Create(haloTex, new Rect(0, 0, haloSize, haloSize),
                new Vector2(0.5f, 0.5f), TilePixels);
            haloSprites.Add(haloSprite);

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
                lightAnchors.Add(anchor);
            }
        }

        private static bool IsFiniteVector(Vector2 v)
        {
            return !float.IsNaN(v.x) && !float.IsInfinity(v.x) &&
                !float.IsNaN(v.y) && !float.IsInfinity(v.y);
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
            foreach (KeyValuePair<Vector2Int, ChunkHandle> kvp in activeChunks)
            {
                DestroySafe(kvp.Value.Obj);
                DestroySafe(kvp.Value.Sprite);
                DestroySafe(kvp.Value.Texture);
            }
            activeChunks.Clear();
            lightHalos.Clear();
            lightAnchors.Clear();
            DestroySafe(rootObject);
            for (int i = 0; i < haloSprites.Count; i++)
                DestroySafe(haloSprites[i]);
            haloSprites.Clear();
            for (int i = 0; i < haloTextures.Count; i++)
                DestroySafe(haloTextures[i]);
            haloTextures.Clear();
        }
    }
}
