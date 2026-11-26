using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Fixed-resolution downsampled minimap, expandable world-overview map,
    /// 6-category landmark pin overlay, and live 8-direction quest wayfinding
    /// compass HUD for 500x-area RPG maps. Bounds minimap texture memory to a
    /// crisp ~192x112 RGBA32 texture regardless of multi-million-tile map scale.
    /// </summary>
    public sealed class RpgWorld500xMinimapHud : IDisposable
    {
        public const int DefaultTextureWidth = 192;
        public const int DefaultTextureHeight = 112;

        private readonly RpgExpandedMapSpec spec;
        private readonly RpgMapExplorationTracker tracker;
        private readonly GameObject rootObject;
        private readonly RectTransform mapPanelRect;
        private readonly RawImage mapImage;
        private readonly Texture2D minimapTexture;
        private readonly RectTransform playerPin;
        private readonly RectTransform threatPin;
        private readonly Text compassText;
        private readonly Dictionary<string, Image> landmarkPins = new Dictionary<string, Image>(StringComparer.Ordinal);
        private readonly IReadOnlyList<RpgMapLandmarkMarker> markers;
        private Vector2 lastValidPlayerTile;
        private bool hasValidPlayerTile;
        private bool overviewMode;
        private bool disposed;

        public GameObject RootObject => rootObject;
        public Texture2D MinimapTexture => minimapTexture;
        public int TextureWidth => minimapTexture != null ? minimapTexture.width : 0;
        public int TextureHeight => minimapTexture != null ? minimapTexture.height : 0;
        public int PinCount => landmarkPins.Count;
        public bool IsOverviewMode => overviewMode;
        public string LastCompassText => compassText != null ? compassText.text : string.Empty;
        public bool IsDisposed => disposed || rootObject == null;

        public RpgWorld500xMinimapHud(
            Transform parent,
            RpgExpandedMapSpec spec,
            Func<int, int, int> tileSampler,
            bool useHarborPalette,
            RpgMapExplorationTracker tracker,
            int textureWidth = DefaultTextureWidth,
            int textureHeight = DefaultTextureHeight)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (tileSampler == null) throw new ArgumentNullException(nameof(tileSampler));
            if (tracker == null) throw new ArgumentNullException(nameof(tracker));
            if (textureWidth <= 0) throw new ArgumentOutOfRangeException(nameof(textureWidth));
            if (textureHeight <= 0) throw new ArgumentOutOfRangeException(nameof(textureHeight));

            this.spec = spec;
            this.tracker = tracker;
            markers = RpgWorldLandmarkCatalog.GetMarkers(spec.MapId);

            rootObject = new GameObject("RpgWorld500xMinimapHud_" + spec.MapId,
                typeof(RectTransform), typeof(Canvas));
            rootObject.transform.SetParent(parent, false);
            try
            {
                var canvas = rootObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 40;

                minimapTexture = BuildDownsampledTexture(
                    spec.ExpandedWidth, spec.ExpandedHeight,
                    textureWidth, textureHeight, tileSampler, useHarborPalette);

                var panelObj = new GameObject("MapPanel", typeof(RectTransform), typeof(Image));
                panelObj.transform.SetParent(rootObject.transform, false);
                mapPanelRect = panelObj.GetComponent<RectTransform>();
                mapPanelRect.anchorMin = new Vector2(1f, 1f);
                mapPanelRect.anchorMax = new Vector2(1f, 1f);
                mapPanelRect.pivot = new Vector2(1f, 1f);
                mapPanelRect.anchoredPosition = new Vector2(-16f, -16f);
                mapPanelRect.sizeDelta = new Vector2(224f, 136f);
                panelObj.GetComponent<Image>().color = new Color(0.06f, 0.08f, 0.11f, 0.88f);

                var rawObj = new GameObject("MinimapImage", typeof(RectTransform), typeof(RawImage));
                rawObj.transform.SetParent(panelObj.transform, false);
                var rawRect = rawObj.GetComponent<RectTransform>();
                rawRect.anchorMin = new Vector2(0.03f, 0.05f);
                rawRect.anchorMax = new Vector2(0.97f, 0.95f);
                rawRect.offsetMin = Vector2.zero;
                rawRect.offsetMax = Vector2.zero;
                mapImage = rawObj.GetComponent<RawImage>();
                mapImage.texture = minimapTexture;

                for (int i = 0; i < markers.Count; i++)
                {
                    RpgMapLandmarkMarker m = markers[i];
                    var pinObj = new GameObject("Pin_" + m.Id, typeof(RectTransform), typeof(Image));
                    pinObj.transform.SetParent(rawObj.transform, false);
                    var pinRect = pinObj.GetComponent<RectTransform>();
                    float nx = Mathf.Clamp01((m.Tile.x + 0.5f) / spec.ExpandedWidth);
                    float ny = Mathf.Clamp01((m.Tile.y + 0.5f) / spec.ExpandedHeight);
                    pinRect.anchorMin = new Vector2(nx, ny);
                    pinRect.anchorMax = new Vector2(nx, ny);
                    pinRect.sizeDelta = m.Category == RpgMapMarkerCategory.MainStory
                        ? new Vector2(7f, 7f)
                        : new Vector2(5f, 5f);
                    var img = pinObj.GetComponent<Image>();
                    img.color = CategoryColor(m.Category);
                    landmarkPins[m.Id] = img;
                }

                var threatObj = new GameObject("ThreatPin", typeof(RectTransform), typeof(Image));
                threatObj.transform.SetParent(rawObj.transform, false);
                threatPin = threatObj.GetComponent<RectTransform>();
                threatPin.sizeDelta = new Vector2(6f, 6f);
                threatObj.GetComponent<Image>().color = new Color(1.0f, 0.20f, 0.20f, 1f);
                threatObj.SetActive(false);

                var playerObj = new GameObject("PlayerPin", typeof(RectTransform), typeof(Image));
                playerObj.transform.SetParent(rawObj.transform, false);
                playerPin = playerObj.GetComponent<RectTransform>();
                playerPin.sizeDelta = new Vector2(7f, 7f);
                playerObj.GetComponent<Image>().color = Color.white;

                var compassObj = new GameObject("CompassBanner", typeof(RectTransform), typeof(Text));
                compassObj.transform.SetParent(rootObject.transform, false);
                var compassRect = compassObj.GetComponent<RectTransform>();
                compassRect.anchorMin = new Vector2(1f, 1f);
                compassRect.anchorMax = new Vector2(1f, 1f);
                compassRect.pivot = new Vector2(1f, 1f);
                compassRect.anchoredPosition = new Vector2(-16f, -158f);
                compassRect.sizeDelta = new Vector2(360f, 44f);
                compassText = compassObj.GetComponent<Text>();
                compassText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                compassText.fontSize = 13;
                compassText.alignment = TextAnchor.UpperRight;
                compassText.color = new Color(0.95f, 0.93f, 0.86f, 1f);

                RefreshLandmarkPins();
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public static Color CategoryColor(RpgMapMarkerCategory category)
        {
            switch (category)
            {
                case RpgMapMarkerCategory.MainStory: return new Color(1.0f, 0.85f, 0.25f, 1f);
                case RpgMapMarkerCategory.LandmarkQuest: return new Color(0.98f, 0.62f, 0.22f, 1f);
                case RpgMapMarkerCategory.FastTravelBeacon: return new Color(0.26f, 0.88f, 0.96f, 1f);
                case RpgMapMarkerCategory.CampAndSupply: return new Color(0.34f, 0.86f, 0.48f, 1f);
                case RpgMapMarkerCategory.LoreArchive: return new Color(0.76f, 0.62f, 0.96f, 1f);
                case RpgMapMarkerCategory.DangerZone: return new Color(0.94f, 0.30f, 0.28f, 1f);
                default: return Color.white;
            }
        }

        public bool SetOverviewMode(bool expandedOverview)
        {
            if (IsDisposed)
                return false;
            overviewMode = expandedOverview;
            mapPanelRect.sizeDelta = overviewMode
                ? new Vector2(480f, 280f)
                : new Vector2(224f, 136f);
            RefreshLandmarkPins();
            return true;
        }

        public int VisiblePinCount
        {
            get
            {
                if (IsDisposed) return 0;
                int count = 0;
                foreach (KeyValuePair<string, Image> kvp in landmarkPins)
                {
                    if (kvp.Value != null && kvp.Value.gameObject.activeSelf)
                        count++;
                }
                return count;
            }
        }

        public bool UpdatePlayerAndCompass(Vector2 playerTile, string currentDistrictZh = null)
        {
            if (IsDisposed || !IsFinite(playerTile))
                return false;

            lastValidPlayerTile = playerTile;
            hasValidPlayerTile = true;

            float nx = Mathf.Clamp01(playerTile.x / spec.ExpandedWidth);
            float ny = Mathf.Clamp01(playerTile.y / spec.ExpandedHeight);
            playerPin.anchorMin = new Vector2(nx, ny);
            playerPin.anchorMax = new Vector2(nx, ny);

            RefreshLandmarkPins();

            string district = string.IsNullOrEmpty(currentDistrictZh) ? spec.DisplayNameZh : currentDistrictZh;
            string header = district + " | 地标 " + tracker.DiscoveredMarkerCount + "/" + tracker.TotalMarkerCount +
                " | 任务 " + tracker.CompletedQuestCount + "/" + tracker.TotalQuestCount;
            if (tracker.TryGetWayfindingReading(playerTile, out RpgWayfindingReading reading))
            {
                int distTiles = Mathf.RoundToInt(reading.DistanceTiles);
                compassText.text = header + "\n追踪：" + reading.TitleZh + " · " +
                    reading.CompassDirectionZh + " " + distTiles + "格";
            }
            else
            {
                compassText.text = header;
            }
            return true;
        }

        public bool UpdateThreatMarker(Vector2 threatTile, bool visible)
        {
            if (IsDisposed)
                return false;
            if (!visible)
            {
                threatPin.gameObject.SetActive(false);
                return true;
            }
            if (!IsFinite(threatTile))
                return false;

            float nx = Mathf.Clamp01(threatTile.x / spec.ExpandedWidth);
            float ny = Mathf.Clamp01(threatTile.y / spec.ExpandedHeight);
            threatPin.anchorMin = new Vector2(nx, ny);
            threatPin.anchorMax = new Vector2(nx, ny);
            threatPin.gameObject.SetActive(true);
            return true;
        }

        public void RefreshLandmarkPins()
        {
            if (IsDisposed)
                return;
            for (int i = 0; i < markers.Count; i++)
            {
                RpgMapLandmarkMarker m = markers[i];
                if (!landmarkPins.TryGetValue(m.Id, out Image img) || img == null)
                    continue;
                bool discovered = tracker.IsMarkerDiscovered(m.Id);
                bool isTracked = string.Equals(tracker.TrackedMarkerId, m.Id, StringComparison.Ordinal);
                bool isMain = m.Category == RpgMapMarkerCategory.MainStory;
                bool show = overviewMode || discovered || isMain || isTracked;
                img.gameObject.SetActive(show);
                if (show)
                {
                    Color c = CategoryColor(m.Category);
                    c.a = (discovered || isMain || isTracked) ? 1f : 0.45f;
                    img.color = c;
                }
            }
        }

        private static Texture2D BuildDownsampledTexture(
            int mapWidth, int mapHeight, int texWidth, int texHeight,
            Func<int, int, int> tileSampler, bool useHarborPalette)
        {
            var pixels = new Color32[texWidth * texHeight];
            for (int y = 0; y < texHeight; y++)
            {
                int ty = Mathf.Clamp((int)((long)y * mapHeight / texHeight), 0, mapHeight - 1);
                for (int x = 0; x < texWidth; x++)
                {
                    int tx = Mathf.Clamp((int)((long)x * mapWidth / texWidth), 0, mapWidth - 1);
                    int tileId = tileSampler(tx, ty);
                    pixels[y * texWidth + x] = SampleMinimapColor(tileId, useHarborPalette);
                }
            }

            var tex = new Texture2D(texWidth, texHeight, TextureFormat.RGBA32, mipChain: false);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.SetPixels32(pixels);
            tex.Apply(updateMipmaps: false, makeNoLongerReadable: true);
            return tex;
        }

        private static Color32 SampleMinimapColor(int tileId, bool useHarborPalette)
        {
            if (useHarborPalette)
            {
                switch ((HarborTileKind)tileId)
                {
                    case HarborTileKind.WaterDeep: return new Color32(18, 30, 48, 255);
                    case HarborTileKind.WaterCalm:
                    case HarborTileKind.WaterShallow: return new Color32(28, 48, 70, 255);
                    case HarborTileKind.WaterFoam: return new Color32(66, 90, 114, 255);
                    case HarborTileKind.BridgePlank:
                    case HarborTileKind.BridgeCable: return new Color32(142, 110, 74, 255);
                    case HarborTileKind.QuayStone: return new Color32(84, 92, 102, 255);
                    default: return new Color32(52, 58, 66, 255);
                }
            }

            switch ((SoundwellTileKind)tileId)
            {
                case SoundwellTileKind.WallTop:
                case SoundwellTileKind.WallGrating:
                case SoundwellTileKind.WallCorner:
                case SoundwellTileKind.WallMoss:
                    return new Color32(20, 24, 32, 255);
                case SoundwellTileKind.GateClosed:
                    return new Color32(160, 96, 52, 255);
                case SoundwellTileKind.BellPedestal:
                case SoundwellTileKind.ExtractionHatch:
                    return new Color32(198, 158, 80, 255);
                default:
                    return new Color32(76, 88, 102, 255);
            }
        }

        private static bool IsFinite(Vector2 v)
        {
            return !float.IsNaN(v.x) && !float.IsInfinity(v.x) &&
                !float.IsNaN(v.y) && !float.IsInfinity(v.y);
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
            landmarkPins.Clear();
            DestroySafe(rootObject);
            DestroySafe(minimapTexture);
        }
    }
}
