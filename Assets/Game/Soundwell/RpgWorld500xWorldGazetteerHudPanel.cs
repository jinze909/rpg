using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Soundwell
{
    public enum RpgWorld500xGazetteerHudTab : byte
    {
        RegionOverview = 0,
        SearchResults = 1
    }

    /// <summary>
    /// Interactive UGUI Cross-Map World Gazetteer &amp; Keyword Search Directory modal panel
    /// bound to RpgWorld500xSceneBootstrapper and RpgWorld500xWorldGazetteerService.
    /// Displays all 4 500x region cards (Soundwell, RedSaltHarbor, WhitewaterCrossing,
    /// MineCamp) with faction standing, live cache/shortcut/bounty completion, live route
    /// risk &amp; camp-provisioning status (with shortest-time vs. safest-route toggle),
    /// plus cross-region or active-map keyword search with parent CanvasGroup
    /// interactability guards and zero-leak IDisposable cleanup.
    /// </summary>
    public sealed class RpgWorld500xWorldGazetteerHudPanel : IDisposable
    {
        private const int MaxDisplayedSearchRows = 12;

        private readonly RpgWorld500xSceneBootstrapper _bootstrapper;
        private readonly GameObject _rootObject;
        private readonly Transform _rowContainer;
        private readonly Text _headerText;
        private readonly Text _statusText;
        private readonly Button _overviewTabButton;
        private readonly Button _searchTabButton;
        private readonly Button _toggleScopeButton;
        private readonly Button _clearSearchButton;
        private readonly Button _closeButton;
        private readonly Font _font;
        private readonly List<GameObject> _spawnedRows = new List<GameObject>();
        private List<RpgWorld500xRegionGazetteerCard> _lastCards = new List<RpgWorld500xRegionGazetteerCard>();
        private RpgWorld500xGazetteerHudTab _activeTab = RpgWorld500xGazetteerHudTab.RegionOverview;
        private string _searchQuery = string.Empty;
        private bool _filterActiveMapOnly;
        private bool _preferSafestRoute;
        private int _lastSearchHitCount;
        private int _averageCompletionPercent;
        private bool _visible;
        private bool _disposed;

        public bool IsVisible => !IsDisposed && _visible;
        public bool IsDisposed => _disposed || _rootObject == null;
        public RpgWorld500xGazetteerHudTab ActiveTab => _activeTab;
        public string SearchQuery => _searchQuery;
        public bool FilterActiveMapOnly => _filterActiveMapOnly;
        public bool PreferSafestRoute => _preferSafestRoute;
        public int DisplayedRowCount => _spawnedRows.Count;
        public int LastSearchHitCount => _lastSearchHitCount;
        public int AverageCompletionPercent => _averageCompletionPercent;
        public IReadOnlyList<RpgWorld500xRegionGazetteerCard> LastCards => _lastCards;
        public string HeaderText => _headerText != null ? _headerText.text : string.Empty;
        public string StatusText => _statusText != null ? _statusText.text : string.Empty;

        public RpgWorld500xWorldGazetteerHudPanel(
            Transform parent,
            RpgWorld500xSceneBootstrapper bootstrapper)
        {
            _bootstrapper = bootstrapper ?? throw new ArgumentNullException(nameof(bootstrapper));
            _font = ResolveUiFont();

            _rootObject = new GameObject("World500xWorldGazetteerHudPanel", typeof(RectTransform), typeof(Canvas));
            if (parent != null)
            {
                _rootObject.transform.SetParent(parent, false);
            }

            var canvas = _rootObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 75;

            var scaler = _rootObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);

            GameObject modal = CreateBox(
                _rootObject.transform,
                "GazetteerModalBox",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(760f, 500f),
                new Color(0.08f, 0.09f, 0.12f, 0.96f));

            _headerText = CreateText(
                modal.transform,
                "HeaderLabel",
                new Vector2(0.04f, 0.89f), new Vector2(0.96f, 0.96f),
                _font, 15, TextAnchor.MiddleLeft,
                new Color(0.97f, 0.89f, 0.62f, 1f));

            _overviewTabButton = CreateActionButton(
                modal.transform, "Tab_RegionOverview",
                new Vector2(0.04f, 0.80f), new Vector2(0.49f, 0.88f),
                "四境总览（4）", _font,
                () =>
                {
                    if (!CanClick(_overviewTabButton))
                    {
                        return;
                    }

                    _activeTab = RpgWorld500xGazetteerHudTab.RegionOverview;
                    Refresh();
                });

            _searchTabButton = CreateActionButton(
                modal.transform, "Tab_SearchResults",
                new Vector2(0.51f, 0.80f), new Vector2(0.96f, 0.88f),
                "风物关键词检索", _font,
                () =>
                {
                    if (!CanClick(_searchTabButton))
                    {
                        return;
                    }

                    _activeTab = RpgWorld500xGazetteerHudTab.SearchResults;
                    Refresh();
                });

            _statusText = CreateText(
                modal.transform,
                "StatusLabel",
                new Vector2(0.04f, 0.72f), new Vector2(0.96f, 0.79f),
                _font, 12, TextAnchor.MiddleLeft,
                new Color(0.84f, 0.92f, 0.96f, 1f));

            var rowsGo = new GameObject("GazetteerRows", typeof(RectTransform));
            rowsGo.transform.SetParent(modal.transform, false);
            var rowsRect = rowsGo.GetComponent<RectTransform>();
            rowsRect.anchorMin = new Vector2(0.04f, 0.16f);
            rowsRect.anchorMax = new Vector2(0.96f, 0.71f);
            rowsRect.offsetMin = Vector2.zero;
            rowsRect.offsetMax = Vector2.zero;
            _rowContainer = rowsGo.transform;

            _toggleScopeButton = CreateActionButton(
                modal.transform, "ToggleSearchScopeButton",
                new Vector2(0.04f, 0.05f), new Vector2(0.42f, 0.13f),
                "检索范围：四境全图", _font,
                () =>
                {
                    if (!CanClick(_toggleScopeButton))
                    {
                        return;
                    }

                    _filterActiveMapOnly = !_filterActiveMapOnly;
                    Refresh();
                });

            _clearSearchButton = CreateActionButton(
                modal.transform, "ClearSearchButton",
                new Vector2(0.45f, 0.05f), new Vector2(0.70f, 0.13f),
                "清空关键词", _font,
                () =>
                {
                    if (!CanClick(_clearSearchButton))
                    {
                        return;
                    }

                    _searchQuery = string.Empty;
                    Refresh();
                });

            _closeButton = CreateActionButton(
                modal.transform, "CloseGazetteerButton",
                new Vector2(0.73f, 0.05f), new Vector2(0.96f, 0.13f),
                "关闭总录", _font,
                () =>
                {
                    if (!CanClick(_closeButton))
                    {
                        return;
                    }

                    Hide();
                });

            _visible = false;
            _rootObject.SetActive(false);
            Refresh();
        }

        public bool Show(RpgWorld500xGazetteerHudTab? tab = null)
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

        public bool TrySelectTab(RpgWorld500xGazetteerHudTab tab)
        {
            Button btn = tab == RpgWorld500xGazetteerHudTab.RegionOverview
                ? _overviewTabButton
                : _searchTabButton;
            if (!CanClick(btn))
            {
                return false;
            }

            btn.onClick.Invoke();
            return true;
        }

        public bool TrySetPreferSafestRoute(bool preferSafestRoute)
        {
            if (!CanClick(_overviewTabButton))
            {
                return false;
            }

            _preferSafestRoute = preferSafestRoute;
            Refresh();
            return true;
        }

        public bool TryToggleSafestRoute()
        {
            return TrySetPreferSafestRoute(!_preferSafestRoute);
        }

        public bool TrySearch(string keywordZh, bool? filterActiveMapOnly = null)
        {
            if (!CanClick(_searchTabButton))
            {
                return false;
            }

            _searchQuery = keywordZh ?? string.Empty;
            if (filterActiveMapOnly.HasValue)
            {
                _filterActiveMapOnly = filterActiveMapOnly.Value;
            }

            _activeTab = RpgWorld500xGazetteerHudTab.SearchResults;
            Refresh();
            return true;
        }

        public bool TryClickToggleScope()
        {
            if (!CanClick(_toggleScopeButton))
            {
                return false;
            }

            _toggleScopeButton.onClick.Invoke();
            return true;
        }

        public bool TryClickClearSearch()
        {
            if (!CanClick(_clearSearchButton))
            {
                return false;
            }

            _clearSearchButton.onClick.Invoke();
            return true;
        }

        public bool TryClickClose()
        {
            if (!CanClick(_closeButton))
            {
                return false;
            }

            _closeButton.onClick.Invoke();
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
            RpgWorldMapId activeMapId = facade.CurrentMapId;
            IReadOnlyDictionary<string, int> store = _bootstrapper.Store;
            Func<string, int> read = k => (store != null && store.TryGetValue(k, out int v)) ? v : 0;

            List<RpgWorld500xRegionGazetteerCard> cards =
                RpgWorld500xWorldGazetteerService.BuildLiveWorldCards(
                    _bootstrapper, read, preferSafestRoute: _preferSafestRoute);
            _lastCards = cards;
            int sumPct = 0;
            int totalCaches = 0;
            int totalShortcuts = 0;
            int totalBounties = 0;

            for (int i = 0; i < cards.Count; i++)
            {
                sumPct += cards[i].CompletionPercent;
                totalCaches += cards[i].TotalCaches;
                totalShortcuts += cards[i].TotalShortcuts;
                totalBounties += cards[i].TotalBounties;
            }

            _averageCompletionPercent = cards.Count > 0 ? Mathf.RoundToInt((float)sumPct / cards.Count) : 0;
            _headerText.text = "【四境风物总录】四境均完成度 " + _averageCompletionPercent +
                               "%｜补给箱 " + totalCaches + " · 捷径 " + totalShortcuts + " · 悬赏 " + totalBounties;

            UpdateButtonLabel(_toggleScopeButton, _filterActiveMapOnly ? "检索范围：仅本境" : "检索范围：四境全图");
            UpdateTabHighlight();

            if (_activeTab == RpgWorld500xGazetteerHudTab.RegionOverview)
            {
                _statusText.text = "当前所在：" + facade.Session.Host.Layout.Spec.DisplayNameZh +
                                   "｜共收录 4 处 500x 巨幅区域" +
                                   (_preferSafestRoute ? "｜路径：避险优先" : "｜路径：最短耗时");
                const float rowHeight = 56f;
                const float spacing = 8f;

                for (int i = 0; i < cards.Count; i++)
                {
                    RpgWorld500xRegionGazetteerCard c = cards[i];
                    bool isCurrent = c.MapId == activeMapId;
                    string prefix = isCurrent ? "[当前] " : string.Empty;
                    string line = prefix + c.FormattedCardZh +
                                  "\n环境微事件 " + c.AmbientZoneCount + " 处｜连通区域 " + c.ConnectedRouteCount +
                                  " 处｜营地折扣 -" + c.CampDiscountPercent + "%";
                    Color rowColor = isCurrent
                        ? new Color(0.16f, 0.25f, 0.34f, 0.94f)
                        : new Color(0.12f, 0.14f, 0.20f, 0.90f);

                    SpawnInfoRow("GazetteerRegion_" + c.MapId, i, rowHeight, spacing, line, rowColor);
                }
            }
            else
            {
                RpgWorldMapId? scopeFilter = _filterActiveMapOnly ? activeMapId : (RpgWorldMapId?)null;
                List<RpgWorld500xGazetteerSearchHit> hits =
                    RpgWorld500xWorldGazetteerService.SearchGazetteer(_searchQuery, read, scopeFilter);
                _lastSearchHitCount = hits.Count;

                if (string.IsNullOrWhiteSpace(_searchQuery))
                {
                    _statusText.text = "输入关键词（如补给箱、捷径、悬赏名）即可在" +
                                       (_filterActiveMapOnly ? "本境" : "四境全图") + "内检索。";
                }
                else
                {
                    _statusText.text = "关键词「" + _searchQuery.Trim() + "」命中 " + hits.Count + " 项" +
                                       (_filterActiveMapOnly ? "（仅本境）" : "（四境全图）");
                }

                const float rowHeight = 20f;
                const float spacing = 2f;
                int shown = Math.Min(hits.Count, MaxDisplayedSearchRows);
                for (int i = 0; i < shown; i++)
                {
                    RpgWorld500xGazetteerSearchHit h = hits[i];
                    string state = h.IsCompleted ? "[已完成] " : string.Empty;
                    string line = state + "[" + h.MapDisplayNameZh + "] " + h.TitleZh + "｜" + h.DetailZh;
                    Color rowColor = h.IsCompleted
                        ? new Color(0.15f, 0.22f, 0.16f, 0.90f)
                        : new Color(0.12f, 0.14f, 0.19f, 0.88f);

                    SpawnInfoRow("GazetteerHit_" + i + "_" + h.Id, i, rowHeight, spacing, line, rowColor);
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

        private bool CanClick(Button button)
        {
            return IsVisible && !_bootstrapper.IsDisposed && button != null &&
                button.isActiveAndEnabled && button.IsInteractable();
        }

        private void UpdateTabHighlight()
        {
            SetButtonColor(_overviewTabButton, _activeTab == RpgWorld500xGazetteerHudTab.RegionOverview);
            SetButtonColor(_searchTabButton, _activeTab == RpgWorld500xGazetteerHudTab.SearchResults);
        }

        private static void UpdateButtonLabel(Button btn, string label)
        {
            if (btn == null)
            {
                return;
            }

            Text txt = btn.GetComponentInChildren<Text>();
            if (txt != null)
            {
                txt.text = label;
            }
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
                new Vector2(0.02f, 0.04f), new Vector2(0.98f, 0.96f),
                _font, 11, TextAnchor.MiddleLeft,
                Color.white);
            text.text = label;

            _spawnedRows.Add(rowGo);
        }

        private void ClearRows()
        {
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
            Action onClick)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            go.GetComponent<Image>().color = new Color(0.20f, 0.28f, 0.38f, 0.95f);

            var btn = go.GetComponent<Button>();
            btn.interactable = true;
            if (onClick != null)
            {
                btn.onClick.AddListener(() => onClick());
            }

            Text txt = CreateText(
                go.transform,
                "Text",
                Vector2.zero, Vector2.one,
                font, 12, TextAnchor.MiddleCenter,
                Color.white);
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
