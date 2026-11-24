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
    /// RpgWorld500xSceneBootstrapper. Displays all 6 field NPC rumors (with one-click
    /// landmark tracking once heard) and all 4 environmental story spots on the active
    /// 500x map under a zero-leak IDisposable lifecycle.
    /// </summary>
    public sealed class RpgWorld500xFieldRumorAndLoreHudPanel : IDisposable
    {
        private readonly RpgWorld500xSceneBootstrapper _bootstrapper;
        private readonly GameObject _rootObject;
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
        private bool _visible;
        private bool _disposed;

        public bool IsVisible => !IsDisposed && _visible;
        public bool IsDisposed => _disposed || _rootObject == null;
        public RpgWorld500xRumorLoreTab ActiveTab => _activeTab;
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

            var canvas = _rootObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 68;

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
            if (IsDisposed)
            {
                return false;
            }

            Button btn = tab == RpgWorld500xRumorLoreTab.FieldRumors ? _rumorsTabButton : _loreTabButton;
            if (btn == null)
            {
                return false;
            }

            btn.onClick.Invoke();
            return true;
        }

        public bool TryClickTrackRumorTarget(string npcId)
        {
            if (IsDisposed || string.IsNullOrEmpty(npcId) ||
                !_trackButtons.TryGetValue(npcId, out Button btn) ||
                btn == null || !btn.interactable)
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
            IReadOnlyDictionary<string, int> store = _bootstrapper.Store;
            Func<string, int> read = k => (store != null && store.TryGetValue(k, out int v)) ? v : 0;

            IReadOnlyList<RpgFieldNpcDef> npcs = RpgWorld500xLivingWorldCatalog.GetFieldNpcs(mapId);
            IReadOnlyList<RpgStorySpotDef> spots = RpgWorld500xLivingWorldCatalog.GetStorySpots(mapId);

            int heardCount = 0;
            for (int i = 0; i < npcs.Count; i++)
            {
                if (read(npcs[i].RumorMakinomKey) == 1)
                {
                    heardCount++;
                }
            }

            int inspectedCount = 0;
            for (int i = 0; i < spots.Count; i++)
            {
                if (read(spots[i].MakinomKey) == 1)
                {
                    inspectedCount++;
                }
            }

            _headerText.text = mapNameZh + " · 野外见闻与风物志（传闻 " + heardCount + "/" + npcs.Count +
                               "｜轶事 " + inspectedCount + "/" + spots.Count + "）";

            UpdateTabHighlight();

            if (_activeTab == RpgWorld500xRumorLoreTab.FieldRumors)
            {
                const float rowHeight = 42f;
                const float spacing = 5f;
                for (int i = 0; i < npcs.Count; i++)
                {
                    RpgFieldNpcDef npc = npcs[i];
                    bool heard = read(npc.RumorMakinomKey) == 1;
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
                        i,
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
                for (int i = 0; i < spots.Count; i++)
                {
                    RpgStorySpotDef spot = spots[i];
                    bool inspected = read(spot.MakinomKey) == 1;
                    string line = inspected
                        ? "[已勘察] " + spot.TitleZh + "\n" + spot.DescriptionZh
                        : "[未勘察] 未知风物轶事点（坐标 " + spot.Tile.x + "," + spot.Tile.y + "）";

                    SpawnInfoRow(
                        "LoreRow_" + spot.Id,
                        i,
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

        private static Button CreateActionButton(
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
                btn.onClick.AddListener(() => onClick());
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
