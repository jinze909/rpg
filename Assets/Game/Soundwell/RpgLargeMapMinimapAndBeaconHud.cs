using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Visual-only top-right pixel-art minimap and objective beacon HUD for all
    /// three 4x-scale RPG maps (Soundwell 96x60, Red Salt Harbor 140x84,
    /// Whitewater Crossing 80x44). Bakes a crisp 1-pixel-per-tile Point-filtered
    /// Texture2D from the authored layout, tracks the player's live tile position,
    /// optionally shows a live threat/patrol marker, and highlights active vs.
    /// completed objective beacons without adding any GraphicRaycaster or blocking input.
    /// </summary>
    public sealed class RpgLargeMapMinimapAndBeaconHud : IDisposable
    {
        private sealed class BeaconEntry
        {
            public string Id;
            public Image Dot;
            public Color ActiveColor;
            public Color CompletedColor;
            public bool Completed;
        }

        private const float MapDisplayWidth = 168f;
        private const float MapDisplayHeight = 104f;

        private readonly GameObject canvasObject;
        private readonly RectTransform panel;
        private readonly RectTransform mapFrameRect;
        private readonly RectTransform playerMarkerRect;
        private readonly RectTransform threatMarkerRect;
        private readonly Image threatMarkerImage;
        private readonly Text titleLabel;
        private readonly Font latinFont;
        private readonly Texture2D minimapTexture;
        private readonly Sprite minimapSprite;
        private readonly int mapWidth;
        private readonly int mapHeight;
        private readonly string titleZh;
        private readonly string titleEn;
        private readonly Dictionary<string, BeaconEntry> beacons =
            new Dictionary<string, BeaconEntry>(StringComparer.Ordinal);
        private bool disposed;

        public RectTransform Panel => panel;
        public RectTransform PlayerMarkerRect => playerMarkerRect;
        public RectTransform ThreatMarkerRect => threatMarkerRect;
        public bool IsThreatMarkerVisible =>
            !disposed && threatMarkerRect != null && threatMarkerRect.gameObject.activeSelf;
        public Text TitleLabel => titleLabel;
        public Texture2D MinimapTexture => minimapTexture;
        public int BeaconCount => beacons.Count;
        public int ActiveBeaconCount
        {
            get
            {
                int count = 0;
                foreach (var entry in beacons.Values)
                    if (!entry.Completed) count++;
                return count;
            }
        }
        public bool IsDisposed => disposed;

        public static RpgLargeMapMinimapAndBeaconHud BuildForSoundwell(
            Transform parent, SoundwellLargeMapLayout layout)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (layout == null) throw new ArgumentNullException(nameof(layout));

            Color amber = new Color(0.98f, 0.80f, 0.36f, 1f);
            Color cyan = new Color(0.45f, 0.84f, 0.96f, 1f);
            Color emerald = new Color(0.44f, 0.88f, 0.58f, 1f);

            var specs = new[]
            {
                ("soundwell.wet-cloth", layout.WetClothTile, cyan),
                ("soundwell.bell", layout.BronzeBellTile, amber),
                ("soundwell.gate-lever", layout.GateLeverTile, amber),
                ("soundwell.extraction", layout.ExtractionTile, emerald)
            };

            return new RpgLargeMapMinimapAndBeaconHud(
                parent, "钟岭声井 · 96×60", "Soundwell · 96x60",
                layout.Width, layout.Height,
                (x, y) => SoundwellMinimapColor(layout.Tiles[x, y]),
                new Vector2(layout.PlayerSpawn.x + 0.5f, layout.PlayerSpawn.y + 0.5f),
                specs);
        }

        public static RpgLargeMapMinimapAndBeaconHud BuildForRedSaltHarbor(
            Transform parent, RedSaltHarborMapLayout layout)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (layout == null) throw new ArgumentNullException(nameof(layout));

            Color warmGold = new Color(0.98f, 0.82f, 0.42f, 1f);
            Color gateCoral = new Color(0.94f, 0.52f, 0.38f, 1f);
            var specs = new (string id, Vector2Int tile, Color color)[layout.Triggers.Count];
            for (int i = 0; i < layout.Triggers.Count; i++)
            {
                RpgMapTriggerAnchor t = layout.Triggers[i];
                Color c = t.Id.StartsWith("m06.", StringComparison.Ordinal) ? gateCoral : warmGold;
                specs[i] = (t.Id, t.Tile, c);
            }

            return new RpgLargeMapMinimapAndBeaconHud(
                parent, "赤盐外港 · 140×84", "Red Salt Harbor · 140x84",
                layout.Width, layout.Height,
                (x, y) => HarborMinimapColor(layout.Tiles[x, y]),
                new Vector2(layout.PlayerSpawn.x + 0.5f, layout.PlayerSpawn.y + 0.5f),
                specs);
        }

        public static RpgLargeMapMinimapAndBeaconHud BuildForWhitewaterCrossing(
            Transform parent, WhitewaterCrossingMapLayout layout)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (layout == null) throw new ArgumentNullException(nameof(layout));

            Color ember = new Color(0.98f, 0.64f, 0.32f, 1f);
            Color steel = new Color(0.54f, 0.82f, 0.98f, 1f);
            Color crimson = new Color(0.94f, 0.36f, 0.32f, 1f);

            var specs = new[]
            {
                ("s00.cable-anchor", layout.CableAnchorTile, ember),
                ("s00.bridgehead", layout.BridgeheadDefenseTile, steel),
                ("s00.far-bank", layout.FarBankFrontTile, crimson)
            };

            return new RpgLargeMapMinimapAndBeaconHud(
                parent, "白水渡浮桥 · 80×44", "Whitewater · 80x44",
                layout.Width, layout.Height,
                (x, y) => HarborMinimapColor(layout.Tiles[x, y]),
                new Vector2(layout.PlayerSpawn.x + 0.5f, layout.PlayerSpawn.y + 0.5f),
                specs);
        }

        private RpgLargeMapMinimapAndBeaconHud(
            Transform parent, string titleZh, string titleEn,
            int mapWidth, int mapHeight,
            Func<int, int, Color32> colorAt,
            Vector2 initialPlayerTilePos,
            (string id, Vector2Int tile, Color color)[] beaconSpecs)
        {
            this.mapWidth = Mathf.Max(1, mapWidth);
            this.mapHeight = Mathf.Max(1, mapHeight);
            this.titleZh = titleZh;
            this.titleEn = titleEn;
            latinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            minimapTexture = BakeMinimapTexture(this.mapWidth, this.mapHeight, colorAt);
            minimapSprite = Sprite.Create(
                minimapTexture, new Rect(0, 0, this.mapWidth, this.mapHeight),
                new Vector2(0.5f, 0.5f), 1f);

            // Visual-only overlay (no GraphicRaycaster) so clicks pass straight through.
            canvasObject = new GameObject("RpgLargeMapMinimap HUD canvas",
                typeof(RectTransform), typeof(Canvas));
            canvasObject.transform.SetParent(parent, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 118;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(
                SoundwellUiTheme.ReferenceWidth, SoundwellUiTheme.ReferenceHeight);
            scaler.matchWidthOrHeight = 0.5f;

            var holder = new GameObject("Minimap panel", typeof(RectTransform));
            holder.transform.SetParent(canvasObject.transform, false);
            panel = (RectTransform)holder.transform;
            panel.anchorMin = panel.anchorMax = new Vector2(1f, 1f);
            panel.pivot = new Vector2(1f, 1f);
            panel.sizeDelta = new Vector2(MapDisplayWidth + 16f, MapDisplayHeight + 34f);
            panel.anchoredPosition = new Vector2(-18f, -110f);

            SoundwellUiTheme.CreatePanel(panel, "Minimap border",
                panel.sizeDelta, new Color(0.68f, 0.58f, 0.40f, 0.90f));
            SoundwellUiTheme.CreatePanel(panel, "Minimap backing",
                panel.sizeDelta - new Vector2(4f, 4f), new Color(0.05f, 0.07f, 0.10f, 0.92f));

            titleLabel = SoundwellUiTheme.CreateLabel(panel, 12,
                new Vector2(0f, (MapDisplayHeight + 34f) * 0.5f - 13f),
                new Vector2(MapDisplayWidth, 18f), new Color(0.94f, 0.90f, 0.82f, 1f));
            titleLabel.alignment = TextAnchor.MiddleCenter;

            var mapObj = new GameObject("Minimap image",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            mapObj.transform.SetParent(panel, false);
            mapFrameRect = (RectTransform)mapObj.transform;
            mapFrameRect.anchorMin = mapFrameRect.anchorMax = new Vector2(0.5f, 0.5f);
            mapFrameRect.sizeDelta = new Vector2(MapDisplayWidth, MapDisplayHeight);
            mapFrameRect.anchoredPosition = new Vector2(0f, -9f);
            var mapImage = mapObj.GetComponent<Image>();
            mapImage.sprite = minimapSprite;
            mapImage.preserveAspect = false;
            mapImage.raycastTarget = false;

            if (beaconSpecs != null)
            {
                for (int i = 0; i < beaconSpecs.Length; i++)
                {
                    var spec = beaconSpecs[i];
                    var dotObj = new GameObject("Beacon_" + spec.id,
                        typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                    dotObj.transform.SetParent(mapFrameRect, false);
                    var dotRect = (RectTransform)dotObj.transform;
                    dotRect.anchorMin = dotRect.anchorMax = new Vector2(0f, 0f);
                    dotRect.sizeDelta = new Vector2(6f, 6f);
                    dotRect.anchoredPosition = TileToMinimapLocal(
                        new Vector2(spec.tile.x + 0.5f, spec.tile.y + 0.5f));
                    var dotImg = dotObj.GetComponent<Image>();
                    dotImg.color = spec.color;
                    dotImg.raycastTarget = false;

                    beacons[spec.id] = new BeaconEntry
                    {
                        Id = spec.id,
                        Dot = dotImg,
                        ActiveColor = spec.color,
                        CompletedColor = new Color(0.45f, 0.50f, 0.54f, 0.55f),
                        Completed = false
                    };
                }
            }

            var threatObj = new GameObject("Threat marker",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            threatObj.transform.SetParent(mapFrameRect, false);
            threatMarkerRect = (RectTransform)threatObj.transform;
            threatMarkerRect.anchorMin = threatMarkerRect.anchorMax = new Vector2(0f, 0f);
            threatMarkerRect.sizeDelta = new Vector2(7f, 7f);
            threatMarkerImage = threatObj.GetComponent<Image>();
            threatMarkerImage.color = new Color(0.96f, 0.34f, 0.28f, 1f);
            threatMarkerImage.raycastTarget = false;
            threatObj.SetActive(false);

            var playerObj = new GameObject("Player marker",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            playerObj.transform.SetParent(mapFrameRect, false);
            playerMarkerRect = (RectTransform)playerObj.transform;
            playerMarkerRect.anchorMin = playerMarkerRect.anchorMax = new Vector2(0f, 0f);
            playerMarkerRect.sizeDelta = new Vector2(7f, 7f);
            var playerImg = playerObj.GetComponent<Image>();
            playerImg.color = new Color(1f, 0.98f, 0.92f, 1f);
            playerImg.raycastTarget = false;

            UpdatePlayerPosition(initialPlayerTilePos);
            LayoutForViewportAspect((float)Mathf.Max(1, Screen.width) /
                Mathf.Max(1, Screen.height));
            SoundwellUiLocalization.Changed += Refresh;
            Refresh();
        }

        public void UpdatePlayerPosition(Vector2 tilePosition)
        {
            if (disposed || CanvasDestroyed()) return;
            playerMarkerRect.anchoredPosition = TileToMinimapLocal(tilePosition);
        }

        public void UpdateThreatMarker(Vector2 tilePosition, bool visible, Color? tint = null)
        {
            if (disposed || CanvasDestroyed() || threatMarkerRect == null) return;
            threatMarkerRect.gameObject.SetActive(visible);
            if (!visible) return;
            threatMarkerRect.anchoredPosition = TileToMinimapLocal(tilePosition);
            if (tint.HasValue && threatMarkerImage != null)
                threatMarkerImage.color = tint.Value;
        }

        public bool SetBeaconCompleted(string beaconId, bool completed)
        {
            if (disposed || string.IsNullOrEmpty(beaconId)) return false;
            if (!beacons.TryGetValue(beaconId, out BeaconEntry entry))
                return false;

            entry.Completed = completed;
            if (entry.Dot != null)
                entry.Dot.color = completed ? entry.CompletedColor : entry.ActiveColor;
            return true;
        }

        public void LayoutForViewportAspect(float aspect)
        {
            if (disposed) return;
            if (CanvasDestroyed())
            {
                Dispose();
                return;
            }
            SoundwellUiTheme.ApplyViewportAspectScale(panel, aspect);
        }

        public void Refresh()
        {
            if (disposed) return;
            if (CanvasDestroyed())
            {
                Dispose();
                return;
            }

            bool zh = SoundwellUiLocalization.Language == SoundwellUiLocalization.Chinese;
            titleLabel.font = SoundwellUiLocalization.PresentationFont(latinFont);
            titleLabel.text = zh ? titleZh : titleEn;
        }

        private Vector2 TileToMinimapLocal(Vector2 tilePos)
        {
            float nx = Mathf.Clamp01(tilePos.x / mapWidth);
            float ny = Mathf.Clamp01(tilePos.y / mapHeight);
            return new Vector2(nx * MapDisplayWidth, ny * MapDisplayHeight);
        }

        private static Texture2D BakeMinimapTexture(int width, int height,
            Func<int, int, Color32> colorAt)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, mipChain: false);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                int row = y * width;
                for (int x = 0; x < width; x++)
                    pixels[row + x] = colorAt(x, y);
            }
            tex.SetPixels32(pixels);
            tex.Apply(updateMipmaps: false, makeNoLongerReadable: true);
            return tex;
        }

        private static Color32 SoundwellMinimapColor(SoundwellTileKind tile)
        {
            switch (tile)
            {
                case SoundwellTileKind.WallTop:
                case SoundwellTileKind.WallGrating:
                case SoundwellTileKind.WallCorner:
                case SoundwellTileKind.WallMoss:
                    return new Color32(18, 22, 30, 255);
                case SoundwellTileKind.GateClosed:
                    return new Color32(180, 96, 54, 255);
                case SoundwellTileKind.BellPedestal:
                    return new Color32(220, 178, 84, 255);
                case SoundwellTileKind.WetClothSpot:
                    return new Color32(94, 158, 184, 255);
                case SoundwellTileKind.ExtractionHatch:
                    return new Color32(104, 198, 128, 255);
                default:
                    return new Color32(62, 72, 86, 255);
            }
        }

        private static Color32 HarborMinimapColor(HarborTileKind tile)
        {
            switch (tile)
            {
                case HarborTileKind.WaterDeep:
                case HarborTileKind.WaterCalm:
                case HarborTileKind.WaterShallow:
                case HarborTileKind.WaterFoam:
                    return new Color32(24, 42, 64, 255);
                case HarborTileKind.BridgePlank:
                case HarborTileKind.BridgeCable:
                    return new Color32(146, 112, 76, 255);
                case HarborTileKind.QuayStone:
                    return new Color32(82, 88, 98, 255);
                case HarborTileKind.Brazier:
                    return new Color32(230, 146, 62, 255);
                default:
                    return new Color32(46, 50, 58, 255);
            }
        }

        private bool CanvasDestroyed()
        {
            return canvasObject == null || panel == null ||
                mapFrameRect == null || playerMarkerRect == null || titleLabel == null;
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
            SoundwellUiLocalization.Changed -= Refresh;
            beacons.Clear();
            SoundwellUiTheme.SafeDestroyCanvas(canvasObject);
            DestroySafe(minimapSprite);
            DestroySafe(minimapTexture);
        }
    }
}
