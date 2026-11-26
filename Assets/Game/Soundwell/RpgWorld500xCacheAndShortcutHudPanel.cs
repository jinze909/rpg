using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Soundwell
{
    public enum RpgWorld500xCacheShortcutTab : byte
    {
        HiddenCaches = 0,
        Shortcuts = 1
    }

    /// <summary>
    /// Interactive UGUI Hidden Caches &amp; Unlockable Shortcuts tracker panel bound to
    /// RpgWorld500xSceneBootstrapper. Lists all or remaining incomplete hidden supply
    /// caches and unlockable shortcuts on the active 500x map with looted/unlocked state,
    /// controller-backed progress summary, remaining Salt-Silver value, nearest incomplete
    /// target guidance, and live 8-direction Chinese compass bearing and distance from the player.
    /// </summary>
    public sealed class RpgWorld500xCacheAndShortcutHudPanel : IDisposable
    {
        private readonly RpgWorld500xSceneBootstrapper _bootstrapper;
        private readonly GameObject _rootObject;
        private readonly Transform _rowContainer;
        private readonly Text _headerText;
        private readonly Text _summaryText;
        private readonly Button _cachesTabButton;
        private readonly Button _shortcutsTabButton;
        private readonly Font _font;
        private readonly List<GameObject> _spawnedRows = new List<GameObject>();
        private RpgWorld500xCacheShortcutTab _activeTab = RpgWorld500xCacheShortcutTab.HiddenCaches;
        private RpgWorld500xCacheShortcutProgressSummary _lastProgressSummary;
        private bool _incompleteOnly;
        private int _lootedCacheCount;
        private int _unlockedShortcutCount;
        private int _remainingCacheSaltSilver;
        private string _nearestIncompleteId = string.Empty;
        private float _nearestIncompleteDistanceTiles;
        private string _nearestIncompleteLabelZh = string.Empty;
        private bool _visible;
        private bool _disposed;

        public bool IsVisible => !IsDisposed && _visible;
        public bool IsDisposed => _disposed || _rootObject == null;
        public RpgWorld500xCacheShortcutTab ActiveTab => _activeTab;
        public RpgWorld500xCacheShortcutProgressSummary LastProgressSummary => _lastProgressSummary;
        public string ProgressSummaryText => _lastProgressSummary.SummaryZh;
        public bool IncompleteOnly => _incompleteOnly;
        public int DisplayedRowCount => _spawnedRows.Count;
        public int LootedCacheCount => _lootedCacheCount;
        public int UnlockedShortcutCount => _unlockedShortcutCount;
        public int RemainingCacheSaltSilver => _remainingCacheSaltSilver;
        public string NearestIncompleteId => _nearestIncompleteId;
        public float NearestIncompleteDistanceTiles => _nearestIncompleteDistanceTiles;
        public string NearestIncompleteLabelZh => _nearestIncompleteLabelZh;
        public string HeaderText => _headerText != null ? _headerText.text : string.Empty;
        public string SummaryText => _summaryText != null ? _summaryText.text : string.Empty;

        public RpgWorld500xCacheAndShortcutHudPanel(
            Transform parent,
            RpgWorld500xSceneBootstrapper bootstrapper)
        {
            _bootstrapper = bootstrapper ?? throw new ArgumentNullException(nameof(bootstrapper));
            _font = ResolveUiFont();

            _rootObject = new GameObject("World500xCacheAndShortcutHudPanel", typeof(RectTransform), typeof(Canvas));
            if (parent != null)
            {
                _rootObject.transform.SetParent(parent, false);
            }

            var canvas = _rootObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 70;

            var scaler = _rootObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);

            GameObject modal = CreateBox(
                _rootObject.transform,
                "CacheShortcutModalBox",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(740f, 490f),
                new Color(0.08f, 0.09f, 0.12f, 0.95f));

            _headerText = CreateText(
                modal.transform,
                "HeaderLabel",
                new Vector2(0.04f, 0.89f), new Vector2(0.96f, 0.96f),
                _font, 15, TextAnchor.MiddleLeft,
                new Color(0.96f, 0.88f, 0.60f, 1f));

            _cachesTabButton = CreateActionButton(
                modal.transform, "Tab_HiddenCaches",
                new Vector2(0.04f, 0.80f), new Vector2(0.49f, 0.88f),
                "暗藏补给箱", _font,
                () =>
                {
                    if (!CanClick(_cachesTabButton))
                    {
                        return;
                    }

                    _activeTab = RpgWorld500xCacheShortcutTab.HiddenCaches;
                    Refresh();
                });

            _shortcutsTabButton = CreateActionButton(
                modal.transform, "Tab_Shortcuts",
                new Vector2(0.51f, 0.80f), new Vector2(0.96f, 0.88f),
                "可解锁捷径", _font,
                () =>
                {
                    if (!CanClick(_shortcutsTabButton))
                    {
                        return;
                    }

                    _activeTab = RpgWorld500xCacheShortcutTab.Shortcuts;
                    Refresh();
                });

            _summaryText = CreateText(
                modal.transform,
                "SummaryLabel",
                new Vector2(0.04f, 0.71f), new Vector2(0.96f, 0.79f),
                _font, 12, TextAnchor.MiddleLeft,
                new Color(0.84f, 0.92f, 0.96f, 1f));

            var rowsGo = new GameObject("CacheShortcutRows", typeof(RectTransform));
            rowsGo.transform.SetParent(modal.transform, false);
            var rowsRect = rowsGo.GetComponent<RectTransform>();
            rowsRect.anchorMin = new Vector2(0.04f, 0.04f);
            rowsRect.anchorMax = new Vector2(0.96f, 0.69f);
            rowsRect.offsetMin = Vector2.zero;
            rowsRect.offsetMax = Vector2.zero;
            _rowContainer = rowsGo.transform;

            _visible = false;
            _rootObject.SetActive(false);
            Refresh();
        }

        public bool Show(RpgWorld500xCacheShortcutTab? tab = null)
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

        public bool TrySelectTab(RpgWorld500xCacheShortcutTab tab)
        {
            Button btn = tab == RpgWorld500xCacheShortcutTab.HiddenCaches
                ? _cachesTabButton
                : _shortcutsTabButton;
            if (!CanClick(btn))
            {
                return false;
            }

            btn.onClick.Invoke();
            return true;
        }

        public bool TrySetIncompleteOnly(bool incompleteOnly)
        {
            Button guardBtn = _activeTab == RpgWorld500xCacheShortcutTab.HiddenCaches
                ? _cachesTabButton
                : _shortcutsTabButton;
            if (!CanClick(guardBtn))
            {
                return false;
            }

            _incompleteOnly = incompleteOnly;
            Refresh();
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
            RpgWorld500xCachesAndShortcutsController cacheCtrl = facade.ActivityDirector.CachesAndShortcuts;

            IReadOnlyList<RpgSupplyCacheDef> caches = RpgWorld500xContentCatalog.GetCaches(mapId);
            IReadOnlyList<RpgWorldShortcutDef> shortcuts = RpgWorld500xContentCatalog.GetShortcuts(mapId);

            _lastProgressSummary = cacheCtrl.BuildProgressSummary();
            _lootedCacheCount = _lastProgressSummary.LootedCacheCount;
            _unlockedShortcutCount = _lastProgressSummary.UnlockedShortcutCount;
            _remainingCacheSaltSilver = _lastProgressSummary.RemainingCacheSaltSilver;

            _headerText.text = mapNameZh + " · 秘藏补给与捷径图谱（补给 " +
                               _lootedCacheCount + "/" + caches.Count +
                               "｜捷径 " + _unlockedShortcutCount + "/" + shortcuts.Count + "）";

            int clampsOwned = facade.BountyAndSupply.GetOwnedSupplyCount("iron_clamps");
            _summaryText.text = "当前坐标：" + Mathf.FloorToInt(playerPos.x) + "," + Mathf.FloorToInt(playerPos.y) +
                                "｜随身便携修桥铁件：" + clampsOwned +
                                "｜未领补给盐银：" + _remainingCacheSaltSilver;

            UpdateTabHighlight();

            if (_activeTab == RpgWorld500xCacheShortcutTab.HiddenCaches)
            {
                const float rowHeight = 24f;
                const float spacing = 3f;
                int rowIndex = 0;

                if (cacheCtrl.TryFindNearestUnlootedCache(playerPos, out RpgSupplyCacheDef nearestCache, out float nearestDist))
                {
                    _nearestIncompleteId = nearestCache.Id;
                    _nearestIncompleteDistanceTiles = nearestDist;
                    string nearestDir = RpgWorld500xFieldReconScannerService.ResolveDirection8Zh(playerPos, nearestCache.Tile);
                    _nearestIncompleteLabelZh = nearestCache.TitleZh + "（" + nearestDir + " " +
                                                Mathf.CeilToInt(nearestDist) + " 格｜盐银 +" + nearestCache.SaltSilverReward + "）";
                }
                else
                {
                    _nearestIncompleteId = string.Empty;
                    _nearestIncompleteDistanceTiles = 0f;
                    _nearestIncompleteLabelZh = "本境补给箱已全部开启";
                }

                for (int i = 0; i < caches.Count; i++)
                {
                    RpgSupplyCacheDef c = caches[i];
                    bool looted = cacheCtrl.IsCacheLooted(c.Id);
                    if (_incompleteOnly && looted)
                    {
                        continue;
                    }

                    string dir = RpgWorld500xFieldReconScannerService.ResolveDirection8Zh(playerPos, c.Tile);
                    int dist = Mathf.CeilToInt(Vector2.Distance(playerPos, new Vector2(c.Tile.x + 0.5f, c.Tile.y + 0.5f)));
                    string tag = looted ? "[已开启] " : "[未开启] ";
                    string line = tag + c.TitleZh + "｜盐银 +" + c.SaltSilverReward +
                                  "｜方位：" + dir + " " + dist + " 格（" + c.Tile.x + "," + c.Tile.y + "）";

                    SpawnInfoRow(
                        "CacheRow_" + c.Id,
                        rowIndex++,
                        rowHeight,
                        spacing,
                        line,
                        looted ? new Color(0.15f, 0.21f, 0.16f, 0.88f) : new Color(0.12f, 0.14f, 0.19f, 0.88f));
                }
            }
            else
            {
                const float rowHeight = 52f;
                const float spacing = 8f;
                int rowIndex = 0;

                if (cacheCtrl.TryFindNearestLockedShortcut(playerPos, out RpgWorldShortcutDef nearestSc, out float nearestDist))
                {
                    _nearestIncompleteId = nearestSc.Id;
                    _nearestIncompleteDistanceTiles = nearestDist;
                    string nearestDir = RpgWorld500xFieldReconScannerService.ResolveDirection8Zh(playerPos, nearestSc.TriggerTile);
                    _nearestIncompleteLabelZh = nearestSc.TitleZh + "（" + nearestDir + " " +
                                                Mathf.CeilToInt(nearestDist) + " 格）";
                }
                else
                {
                    _nearestIncompleteId = string.Empty;
                    _nearestIncompleteDistanceTiles = 0f;
                    _nearestIncompleteLabelZh = "本境捷径已全部打通";
                }

                for (int i = 0; i < shortcuts.Count; i++)
                {
                    RpgWorldShortcutDef sc = shortcuts[i];
                    bool unlocked = cacheCtrl.IsShortcutUnlocked(sc.Id);
                    if (_incompleteOnly && unlocked)
                    {
                        continue;
                    }

                    string dir = RpgWorld500xFieldReconScannerService.ResolveDirection8Zh(playerPos, sc.TriggerTile);
                    int dist = Mathf.CeilToInt(Vector2.Distance(playerPos, new Vector2(sc.TriggerTile.x + 0.5f, sc.TriggerTile.y + 0.5f)));
                    string tag = unlocked ? "[已打通] " : "[未解锁] ";
                    string line = tag + sc.TitleZh + "｜方位：" + dir + " " + dist +
                                  " 格（触发点 " + sc.TriggerTile.x + "," + sc.TriggerTile.y + "）\n" +
                                  (unlocked ? sc.UnlockedToastZh : "抵达触发点按交互键打通捷径通道");

                    SpawnInfoRow(
                        "ShortcutRow_" + sc.Id,
                        rowIndex++,
                        rowHeight,
                        spacing,
                        line,
                        unlocked ? new Color(0.16f, 0.24f, 0.18f, 0.90f) : new Color(0.13f, 0.15f, 0.21f, 0.88f));
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
            SetButtonColor(_cachesTabButton, _activeTab == RpgWorld500xCacheShortcutTab.HiddenCaches);
            SetButtonColor(_shortcutsTabButton, _activeTab == RpgWorld500xCacheShortcutTab.Shortcuts);
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
