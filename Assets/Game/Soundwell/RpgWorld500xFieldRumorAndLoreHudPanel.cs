using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Soundwell
{
    public enum RpgWorld500xRumorLoreTab : byte
    {
        FieldRumors = 0,
        StorySpots = 1
    }

    /// <summary>
    /// Interactive UGUI Field Rumor &amp; Environmental Lore Codex panel bound to
    /// RpgWorld500xSceneBootstrapper. Displays all or undiscovered field NPC rumors
    /// (with one-click landmark tracking once heard) and environmental story spots on
    /// the active 500x map with LivingWorldService-backed progress summary and nearest
    /// undiscovered bearing guidance under a zero-leak IDisposable lifecycle.
    /// </summary>
    public sealed class RpgWorld500xFieldRumorAndLoreHudPanel : IDisposable
    {
        private readonly RpgWorld500xSceneBootstrapper _bootstrapper;
        private readonly GameObject _rootObject;
        private readonly Canvas _canvas;
        private readonly Transform _rowContainer;
        private readonly Text _headerText;
        private readonly Text _statusText;
        private readonly Font _font;
        private readonly Button _rumorsTabButton;
        private readonly Button _loreTabButton;
        private readonly List<GameObject> _spawnedRows = new List<GameObject>();
        private readonly Dictionary<string, Button> _trackButtons =
            new Dictionary<string, Button>(StringComparer.Ordinal);
        private RpgWorld500xRumorLoreTab _activeTab = RpgWorld500xRumorLoreTab.FieldRumors;
        private RpgWorld500xLivingWorldProgressSummary _lastProgressSummary;
        private bool _undiscoveredOnly;
        private int _heardRumorCount;
        private int _totalRumorCount;
        private int _inspectedStorySpotCount;
        private int _totalStorySpotCount;
        private string _nearestUndiscoveredId = string.Empty;
        private float _nearestUndiscoveredDistanceTiles;
        private string _nearestUndiscoveredLabelZh = string.Empty;
        private bool _visible;
        private bool _disposed;

        public bool IsVisible => !IsDisposed && _visible;
        public bool IsDisposed => _disposed || _rootObject == null;
        public RpgWorld500xRumorLoreTab ActiveTab => _activeTab;
        public RpgWorld500xLivingWorldProgressSummary LastProgressSummary => _lastProgressSummary;
        public string ProgressSummaryText => _lastProgressSummary.SummaryZh;
        public bool UndiscoveredOnly => _undiscoveredOnly;
        public int HeardRumorCount => _heardRumorCount;
        public int TotalRumorCount => _totalRumorCount;
        public int InspectedStorySpotCount => _inspectedStorySpotCount;
        public int TotalStorySpotCount => _totalStorySpotCount;
        public string NearestUndiscoveredId => _nearestUndiscoveredId;
        public float NearestUndiscoveredDistanceTiles => _nearestUndiscoveredDistanceTiles;
        public string NearestUndiscoveredLabelZh => _nearestUndiscoveredLabelZh;
        public int DisplayedRowCount => _spawnedRows.Count;
        public string HeaderText => _headerText != null ? _headerText.text : string.Empty;
        public string StatusText => _statusText != null ? _statusText.text : string.Empty;

        public RpgWorld500xFieldRumorAndLoreHudPanel(
            Transform parent,
            RpgWorld500xSceneBootstrapper bootstrapper)
        {
            _bootstrapper = bootstrapper ?? throw new ArgumentNullException(nameof(bootstrapper));
            _font = ResolveUiFont();

            _rootObject = new GameObject("World500xFieldRumorAndLoreHudPanel", typeof(RectTransform), typeof(Canvas));
            if (parent != null)
            {
                _rootObject.transform.SetParent(parent, false);
            }

            _canvas = _rootObject.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 68;

            var scaler = _rootObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);

            GameObject modal = CreateBox(
                _rootObject.transform,
                "RumorLoreModalBox",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(740f, 470f),
                new Color(0.08f, 0.08f, 0.11f, 0.95f));

            _headerText = CreateText(
                modal.transform,
                "HeaderLabel",
                new Vector2(0.04f, 0.89f), new Vector2(0.96f, 0.96f),
                _font, 15, TextAnchor.MiddleLeft,
                new Color(0.96f, 0.86f, 0.60f, 1f));

            _rumorsTabButton = CreateActionButton(
                modal.transform, "Tab_FieldRumors",
                new Vector2(0.04f, 0.80f), new Vector2(0.49f, 0.88f),
                "野外传闻线索", _font, interactable: true,
                () =>
                {
                    _activeTab = RpgWorld500xRumorLoreTab.FieldRumors;
                    Refresh();
                });

            _loreTabButton = CreateActionButton(
                modal.transform, "Tab_StorySpots",
                new Vector2(0.51f, 0.80f), new Vector2(0.96f, 0.88f),
                "风物轶事志", _font, interactable: true,
                () =>
                {
                    _activeTab = RpgWorld500xRumorLoreTab.StorySpots;
                    Refresh();
                });

            _statusText = CreateText(
                modal.transform,
                "StatusLabel",
                new Vector2(0.04f, 0.70f), new Vector2(0.96f, 0.79f),
                _font, 12, TextAnchor.MiddleLeft,
                new Color(0.84f, 0.92f, 0.96f, 1f));

            var rowsGo = new GameObject("RumorLoreRows", typeof(RectTransform));
            rowsGo.transform.SetParent(modal.transform, false);
            var rowsRect = rowsGo.GetComponent<RectTransform>();
            rowsRect.anchorMin = new Vector2(0.04f, 0.05f);
            rowsRect.anchorMax = new Vector2(0.96f, 0.68f);
            rowsRect.offsetMin = Vector2.zero;
            rowsRect.offsetMax = Vector2.zero;
            _rowContainer = rowsGo.transform;

            _visible = false;
            _rootObject.SetActive(false);
            Refresh();
        }

        public bool Show(RpgWorld500xRumorLoreTab? tab = null)
        {
            if (IsDisposed)
            {
                return false;
            }

            if (tab.HasValue)
            {
                _activeTab = tab.Value;
            }

            _visible = true;
            _rootObject.SetActive(true);
            Refresh();
            return true;
        }

        public bool Hide()
        {
            if (IsDisposed)
            {
                return false;
            }

            _visible = false;
            _rootObject.SetActive(false);
            return true;
        }

        public bool TrySelectTab(RpgWorld500xRumorLoreTab tab)
        {
            if (!Enum.IsDefined(typeof(RpgWorld500xRumorLoreTab), tab))
            {
                return false;
            }

            Button btn = tab == RpgWorld500xRumorLoreTab.FieldRumors ? _rumorsTabButton : _loreTabButton;
            if (!CanInvoke(btn))
            {
                return false;
            }

            btn.onClick.Invoke();
            return true;
        }

        public bool TrySetUndiscoveredOnly(bool undiscoveredOnly)
        {
            Button guardBtn = _activeTab == RpgWorld500xRumorLoreTab.FieldRumors
                ? _rumorsTabButton
                : _loreTabButton;
            if (!CanInvoke(guardBtn))
            {
                return false;
            }

            _undiscoveredOnly = undiscoveredOnly;
            Refresh();
            return true;
        }

        public bool TryClickTrackRumorTarget(string npcId)
        {
            if (string.IsNullOrEmpty(npcId) ||
                !_trackButtons.TryGetValue(npcId, out Button btn) ||
                !CanInvoke(btn))
            {
                return false;
            }

            btn.onClick.Invoke();
            return true;
        }

        public void Refresh()
        {
            if (IsDisposed || _bootstrapper.IsDisposed)
            {
                return;
            }

            ClearRows();

            RpgWorld500xGrandRuntimeFacade facade = _bootstrapper.Driver.Facade;
            RpgWorldMapId mapId = facade.CurrentMapId;
            string mapNameZh = facade.Session.Host.Layout.Spec.DisplayNameZh;
            Vector2 playerPos = facade.Session.Host.PlayerPosition;
            IReadOnlyDictionary<string, int> store = _bootstrapper.Store;
            Func<string, int> read = k => (store != null && store.TryGetValue(k, out int v)) ? v : 0;
            RpgWorld500xLivingWorldService livingWorld = facade.ActivityDirector.LivingWorld;

            IReadOnlyList<RpgFieldNpcDef> npcs = RpgWorld500xLivingWorldCatalog.GetFieldNpcs(mapId);
            IReadOnlyList<RpgStorySpotDef> spots = RpgWorld500xLivingWorldCatalog.GetStorySpots(mapId);

            _lastProgressSummary = livingWorld.BuildLivingWorldProgressSummary();
            _heardRumorCount = _lastProgressSummary.HeardRumorCount;
            _totalRumorCount = _lastProgressSummary.TotalRumorCount;
            _inspectedStorySpotCount = _lastProgressSummary.InspectedStorySpotCount;
            _totalStorySpotCount = _lastProgressSummary.TotalStorySpotCount;

            _headerText.text = mapNameZh + " · 野外见闻与风物志（传闻 " + _heardRumorCount + "/" + _totalRumorCount +
                               "｜轶事 " + _inspectedStorySpotCount + "/" + _totalStorySpotCount + "）";

            UpdateTabHighlight();

            if (_activeTab == RpgWorld500xRumorLoreTab.FieldRumors)
            {
                const float rowHeight = 42f;
                const float spacing = 5f;
                int rowIndex = 0;

                if (livingWorld.TryFindNearestUnheardRumorNpc(playerPos, out RpgFieldNpcDef nearestNpc, out float nearestDist))
                {
                    _nearestUndiscoveredId = nearestNpc.Id;
                    _nearestUndiscoveredDistanceTiles = nearestDist;
                    string nearestDir = RpgWorld500xFieldReconScannerService.ResolveDirection8Zh(playerPos, nearestNpc.Tile);
                    _nearestUndiscoveredLabelZh = nearestNpc.DisplayNameZh + "（" + nearestDir + " " +
                                                  Mathf.CeilToInt(nearestDist) + " 格）";
                }
                else
                {
                    _nearestUndiscoveredId = string.Empty;
                    _nearestUndiscoveredDistanceTiles = 0f;
                    _nearestUndiscoveredLabelZh = "本境野外传闻已全部打听";
                }

                for (int i = 0; i < npcs.Count; i++)
                {
                    RpgFieldNpcDef npc = npcs[i];
                    bool heard = read(npc.RumorMakinomKey) == 1;
                    if (_undiscoveredOnly && heard)
                    {
                        continue;
                    }

                    bool hasTarget = !string.IsNullOrEmpty(npc.RumorTargetMarkerId);
                    RpgMapLandmarkMarker targetMarker = default;
                    bool foundMarker = hasTarget &&
                        RpgWorldLandmarkCatalog.TryGetMarker(mapId, npc.RumorTargetMarkerId, out targetMarker);

                    string line;
                    if (heard)
                    {
                        string targetLabel = foundMarker ? " -> " + targetMarker.TitleZh : string.Empty;
                        line = "[已打听] " + npc.DisplayNameZh + targetLabel + "：" + npc.RumorHintZh;
                    }
                    else
                    {
                        line = "[未打听] " + npc.DisplayNameZh + "（坐标 " + npc.Tile.x + "," + npc.Tile.y +
                               "｜前往交谈获取线索）";
                    }

                    bool canTrack = heard && foundMarker;
                    string capturedMarkerId = npc.RumorTargetMarkerId;
                    string capturedMarkerTitle = foundMarker ? targetMarker.TitleZh : string.Empty;
                    Button btn = SpawnActionRow(
                        "RumorRow_" + npc.Id,
                        rowIndex++,
                        rowHeight,
                        spacing,
                        line,
                        canTrack ? "追踪地标" : "未获取",
                        canTrack,
                        () => HandleTrackClicked(capturedMarkerId, capturedMarkerTitle));

                    _trackButtons[npc.Id] = btn;
                }
            }
            else
            {
                const float rowHeight = 58f;
                const float spacing = 8f;
                int rowIndex = 0;

                if (livingWorld.TryFindNearestUninspectedStorySpot(playerPos, out RpgStorySpotDef nearestSpot, out float nearestDist))
                {
                    _nearestUndiscoveredId = nearestSpot.Id;
                    _nearestUndiscoveredDistanceTiles = nearestDist;
                    string nearestDir = RpgWorld500xFieldReconScannerService.ResolveDirection8Zh(playerPos, nearestSpot.Tile);
                    _nearestUndiscoveredLabelZh = "未知风物轶事点（" + nearestDir + " " +
                                                  Mathf.CeilToInt(nearestDist) + " 格）";
                }
                else
                {
                    _nearestUndiscoveredId = string.Empty;
                    _nearestUndiscoveredDistanceTiles = 0f;
                    _nearestUndiscoveredLabelZh = "本境风物轶事已全部勘察";
                }

                for (int i = 0; i < spots.Count; i++)
                {
                    RpgStorySpotDef spot = spots[i];
                    bool inspected = read(spot.MakinomKey) == 1;
                    if (_undiscoveredOnly && inspected)
                    {
                        continue;
                    }

                    string line = inspected
                        ? "[已勘察] " + spot.TitleZh + "\n" + spot.DescriptionZh
                        : "[未勘察] 未知风物轶事点（坐标 " + spot.Tile.x + "," + spot.Tile.y + "）";

                    SpawnInfoRow(
                        "LoreRow_" + spot.Id,
                        rowIndex++,
                        rowHeight,
                        spacing,
                        line,
                        inspected ? new Color(0.16f, 0.22f, 0.18f, 0.90f) : new Color(0.12f, 0.13f, 0.17f, 0.86f));
                }
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _visible = false;
            ClearRows();
            DestroySafe(_rootObject);
        }

        private void HandleTrackClicked(string markerId, string markerTitleZh)
        {
            if (IsDisposed || _bootstrapper.IsDisposed || string.IsNullOrEmpty(markerId))
            {
                return;
            }

            RpgWorld500xGrandRuntimeFacade facade = _bootstrapper.Driver.Facade;
            if (facade.Session.Host.Tracker.TrySetTrackedMarker(markerId))
            {
                facade.Session.SyncHud();
                _bootstrapper.StatusHud.SyncFromDriver(_bootstrapper.Driver);
                _statusText.text = "已将传闻目标【" + markerTitleZh + "】设为罗盘追踪";
            }
        }

        private void UpdateTabHighlight()
        {
            SetButtonColor(_rumorsTabButton, _activeTab == RpgWorld500xRumorLoreTab.FieldRumors);
            SetButtonColor(_loreTabButton, _activeTab == RpgWorld500xRumorLoreTab.StorySpots);
        }

        private static void SetButtonColor(Button btn, bool active)
        {
            if (btn == null)
            {
                return;
            }

            var img = btn.GetComponent<Image>();
            if (img != null)
            {
                img.color = active
                    ? new Color(0.24f, 0.36f, 0.48f, 0.96f)
                    : new Color(0.14f, 0.16f, 0.21f, 0.86f);
            }
        }

        private Button SpawnActionRow(
            string name,
            int index,
            float height,
            float spacing,
            string label,
            string buttonLabel,
            bool interactable,
            Action onClick)
        {
            var rowGo = new GameObject(name, typeof(RectTransform), typeof(Image));
            rowGo.transform.SetParent(_rowContainer, false);
            var rect = rowGo.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -(index * (height + spacing)));
            rect.sizeDelta = new Vector2(0f, height);
            rowGo.GetComponent<Image>().color = interactable
                ? new Color(0.14f, 0.17f, 0.22f, 0.90f)
                : new Color(0.12f, 0.12f, 0.16f, 0.82f);

            Text text = CreateText(
                rowGo.transform,
                "Label",
                new Vector2(0.02f, 0.06f), new Vector2(0.78f, 0.94f),
                _font, 12, TextAnchor.MiddleLeft,
                Color.white);
            text.text = label;

            Button btn = CreateActionButton(
                rowGo.transform,
                "TrackButton",
                new Vector2(0.80f, 0.14f), new Vector2(0.98f, 0.86f),
                buttonLabel,
                _font,
                interactable,
                onClick);

            _spawnedRows.Add(rowGo);
            return btn;
        }

        private void SpawnInfoRow(
            string name,
            int index,
            float height,
            float spacing,
            string label,
            Color background)
        {
            var rowGo = new GameObject(name, typeof(RectTransform), typeof(Image));
            rowGo.transform.SetParent(_rowContainer, false);
            var rect = rowGo.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -(index * (height + spacing)));
            rect.sizeDelta = new Vector2(0f, height);
            rowGo.GetComponent<Image>().color = background;

            Text text = CreateText(
                rowGo.transform,
                "Label",
                new Vector2(0.02f, 0.06f), new Vector2(0.98f, 0.94f),
                _font, 12, TextAnchor.MiddleLeft,
                Color.white);
            text.text = label;

            _spawnedRows.Add(rowGo);
        }

        private bool CanInvoke(Button button)
        {
            return IsVisible && _canvas != null && _canvas.isActiveAndEnabled &&
                !_bootstrapper.IsDisposed && button != null &&
                button.isActiveAndEnabled && button.gameObject.activeInHierarchy && button.IsInteractable();
        }

        private void ClearRows()
        {
            _trackButtons.Clear();
            for (int i = 0; i < _spawnedRows.Count; i++)
            {
                GameObject row = _spawnedRows[i];
                if (row != null)
                {
                    row.SetActive(false);
                    DestroySafe(row);
                }
            }

            _spawnedRows.Clear();
        }

        private static GameObject CreateBox(
            Transform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPos,
            Vector2 size,
            Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;
            go.GetComponent<Image>().color = color;
            return go;
        }

        private static Text CreateText(
            Transform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Font font,
            int fontSize,
            TextAnchor alignment,
            Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var text = go.GetComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color;
            return text;
        }

        private Button CreateActionButton(
            Transform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            string label,
            Font font,
            bool interactable,
            Action onClick)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            go.GetComponent<Image>().color = interactable
                ? new Color(0.22f, 0.30f, 0.42f, 0.95f)
                : new Color(0.16f, 0.17f, 0.20f, 0.68f);

            var btn = go.GetComponent<Button>();
            btn.interactable = interactable;
            if (onClick != null)
            {
                btn.onClick.AddListener(() =>
                {
                    // Ignore input from hidden tabs, unheard rumors and retired rows.
                    if (CanInvoke(btn))
                        onClick();
                });
            }

            Text txt = CreateText(
                go.transform,
                "Text",
                Vector2.zero, Vector2.one,
                font, 12, TextAnchor.MiddleCenter,
                interactable ? Color.white : new Color(0.55f, 0.57f, 0.62f, 1f));
            txt.text = label;
            return btn;
        }

        private static Font ResolveUiFont()
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null)
            {
                font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            return font;
        }

        private static void DestroySafe(UnityEngine.Object obj)
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
