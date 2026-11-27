using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Soundwell
{
    public enum RpgWorld500xDangerBriefFilter : byte
    {
        UnclearedOnly = 0,
        AllZones = 1
    }

    /// <summary>
    /// Interactive UGUI DangerZone Tactical Briefing modal panel bound to
    /// RpgWorld500xSceneBootstrapper and RpgWorld500xDangerZoneTacticalBriefService.
    /// Lists uncleared or all DangerZones on the active 500x map with weather-scaled
    /// threat levels, 8-direction bearings, standard vs. Lock-Break Salt-Silver
    /// payouts, remaining payout and first-clear reputation summaries, threat/tier
    /// criteria filters, parent CanvasGroup interactability guards, and zero-leak cleanup.
    /// </summary>
    public sealed class RpgWorld500xDangerTacticalBriefHudPanel : IDisposable
    {
        private readonly RpgWorld500xSceneBootstrapper _bootstrapper;
        private readonly Func<string, int> _overrideGetInt;
        private readonly GameObject _rootObject;
        private readonly Canvas _canvas;
        private readonly Transform _rowContainer;
        private readonly Text _headerText;
        private readonly Image _summaryRibbonImage;
        private readonly Text _summaryRibbonText;
        private readonly Button _unclearedTabButton;
        private readonly Button _allZonesTabButton;
        private readonly Button _refreshButton;
        private readonly Button _closeButton;
        private readonly Font _font;
        private readonly List<GameObject> _spawnedRows = new List<GameObject>();
        private RpgWorld500xDangerBriefFilter _activeFilter = RpgWorld500xDangerBriefFilter.UnclearedOnly;
        private int _minEffectiveThreatLevel = 1;
        private RpgWorld500xThreatTier? _tierFilter;
        private RpgWorld500xRegionThreatBrief _lastBrief;
        private RpgWorld500xDangerClearancePayoutSummary _lastPayoutSummary;
        private RpgWorld500xMapFirstClearSummary _lastFirstClearSummary;
        private float _lastElapsedSeconds;
        private bool _visible;
        private bool _disposed;

        public bool IsVisible => !IsDisposed && _visible;
        public bool IsDisposed => _disposed || _rootObject == null;
        public RpgWorld500xDangerBriefFilter ActiveFilter => _activeFilter;
        public int MinEffectiveThreatLevel => _minEffectiveThreatLevel;
        public RpgWorld500xThreatTier? TierFilter => _tierFilter;
        public RpgWorld500xRegionThreatBrief LastBrief => _lastBrief;
        public RpgWorld500xDangerClearancePayoutSummary LastPayoutSummary => _lastPayoutSummary;
        public string PayoutSummaryText => _lastPayoutSummary.SummaryZh;
        public RpgWorld500xMapFirstClearSummary LastFirstClearSummary => _lastFirstClearSummary;
        public string FirstClearSummaryText => _lastFirstClearSummary.SummaryZh;
        public int RemainingFirstClearStandardReputation => _lastFirstClearSummary.RemainingStandardReputation;
        public int RemainingFirstClearLockBreakReputation => _lastFirstClearSummary.RemainingLockBreakReputation;
        public int DisplayedRowCount => _spawnedRows.Count;
        public string HeaderText => _headerText != null ? _headerText.text : string.Empty;
        public string SummaryRibbonText => _summaryRibbonText != null ? _summaryRibbonText.text : string.Empty;

        public RpgWorld500xDangerTacticalBriefHudPanel(
            Transform parent,
            RpgWorld500xSceneBootstrapper bootstrapper,
            Func<string, int> overrideGetInt = null)
        {
            _bootstrapper = bootstrapper ?? throw new ArgumentNullException(nameof(bootstrapper));
            _overrideGetInt = overrideGetInt;
            _font = ResolveUiFont();

            _rootObject = new GameObject("World500xDangerTacticalBriefHudPanel", typeof(RectTransform), typeof(Canvas));
            if (parent != null)
            {
                _rootObject.transform.SetParent(parent, false);
            }

            _canvas = _rootObject.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 74;

            var scaler = _rootObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);

            GameObject modal = CreateBox(
                _rootObject.transform,
                "DangerBriefModalBox",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(760f, 500f),
                new Color(0.08f, 0.09f, 0.12f, 0.96f));

            _headerText = CreateText(
                modal.transform,
                "HeaderLabel",
                new Vector2(0.04f, 0.89f), new Vector2(0.96f, 0.96f),
                _font, 15, TextAnchor.MiddleLeft,
                new Color(0.97f, 0.86f, 0.58f, 1f));

            _unclearedTabButton = CreateActionButton(
                modal.transform, "Tab_UnclearedOnly",
                new Vector2(0.04f, 0.80f), new Vector2(0.49f, 0.88f),
                "未清剿目标", _font,
                () =>
                {
                    if (!CanClick(_unclearedTabButton))
                    {
                        return;
                    }

                    _activeFilter = RpgWorld500xDangerBriefFilter.UnclearedOnly;
                    Refresh(_lastElapsedSeconds);
                });

            _allZonesTabButton = CreateActionButton(
                modal.transform, "Tab_AllZones",
                new Vector2(0.51f, 0.80f), new Vector2(0.96f, 0.88f),
                "全境危险区", _font,
                () =>
                {
                    if (!CanClick(_allZonesTabButton))
                    {
                        return;
                    }

                    _activeFilter = RpgWorld500xDangerBriefFilter.AllZones;
                    Refresh(_lastElapsedSeconds);
                });

            GameObject ribbonBox = CreateBox(
                modal.transform,
                "SummaryRibbonBox",
                new Vector2(0.04f, 0.70f), new Vector2(0.96f, 0.78f), new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero,
                new Color(0.16f, 0.24f, 0.30f, 0.94f));
            var ribbonRect = ribbonBox.GetComponent<RectTransform>();
            ribbonRect.offsetMin = Vector2.zero;
            ribbonRect.offsetMax = Vector2.zero;
            _summaryRibbonImage = ribbonBox.GetComponent<Image>();

            _summaryRibbonText = CreateText(
                ribbonBox.transform,
                "SummaryRibbonLabel",
                new Vector2(0.03f, 0.06f), new Vector2(0.97f, 0.94f),
                _font, 12, TextAnchor.MiddleLeft,
                Color.white);

            var rowsGo = new GameObject("DangerZoneRows", typeof(RectTransform));
            rowsGo.transform.SetParent(modal.transform, false);
            var rowsRect = rowsGo.GetComponent<RectTransform>();
            rowsRect.anchorMin = new Vector2(0.04f, 0.16f);
            rowsRect.anchorMax = new Vector2(0.96f, 0.68f);
            rowsRect.offsetMin = Vector2.zero;
            rowsRect.offsetMax = Vector2.zero;
            _rowContainer = rowsGo.transform;

            _refreshButton = CreateActionButton(
                modal.transform, "RefreshDangerBriefButton",
                new Vector2(0.04f, 0.05f), new Vector2(0.48f, 0.13f),
                "刷新战备简报", _font,
                () =>
                {
                    if (!CanClick(_refreshButton))
                    {
                        return;
                    }

                    Refresh(_lastElapsedSeconds);
                });

            _closeButton = CreateActionButton(
                modal.transform, "CloseDangerBriefButton",
                new Vector2(0.52f, 0.05f), new Vector2(0.96f, 0.13f),
                "关闭简报", _font,
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
            Refresh(0f);
        }

        public bool Show(RpgWorld500xDangerBriefFilter? filter = null, float elapsedSeconds = 0f)
        {
            if (IsDisposed)
            {
                return false;
            }

            if (filter.HasValue)
            {
                _activeFilter = filter.Value;
            }

            _visible = true;
            _rootObject.SetActive(true);
            Refresh(elapsedSeconds);
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

        public bool TrySelectFilter(RpgWorld500xDangerBriefFilter filter)
        {
            Button btn = filter == RpgWorld500xDangerBriefFilter.UnclearedOnly
                ? _unclearedTabButton
                : _allZonesTabButton;
            if (!CanClick(btn))
            {
                return false;
            }

            btn.onClick.Invoke();
            return true;
        }

        public bool TrySetThreatCriteria(
            int minEffectiveThreatLevel = 1,
            RpgWorld500xThreatTier? tierFilter = null)
        {
            if (!CanClick(_refreshButton))
            {
                return false;
            }

            _minEffectiveThreatLevel = Mathf.Clamp(minEffectiveThreatLevel, 1, 5);
            _tierFilter = tierFilter;
            Refresh(_lastElapsedSeconds);
            return true;
        }

        public bool TryClickRefresh(float elapsedSeconds = 0f)
        {
            if (!CanClick(_refreshButton))
            {
                return false;
            }

            _lastElapsedSeconds = elapsedSeconds;
            _refreshButton.onClick.Invoke();
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

        public void Refresh(float elapsedSeconds = 0f)
        {
            if (IsDisposed || _bootstrapper.IsDisposed)
            {
                return;
            }

            ClearRows();
            _lastElapsedSeconds = elapsedSeconds;
            _lastBrief = RpgWorld500xDangerZoneTacticalBriefService.BuildRegionBrief(
                _bootstrapper,
                elapsedSeconds,
                _overrideGetInt);
            _lastPayoutSummary = RpgWorld500xDangerZoneTacticalBriefService.SummarizeRemainingPayouts(_lastBrief);

            IReadOnlyDictionary<string, int> store = _bootstrapper.Store;
            Func<string, int> read = _overrideGetInt ??
                (k => (store != null && store.TryGetValue(k, out int v)) ? v : 0);
            _lastFirstClearSummary = RpgWorld500xDangerVictoryRewardService.SummarizeRemainingFirstClearRewards(
                _bootstrapper.Driver.Facade.Session.Host.Layout,
                read);

            _headerText.text = "【危险区战备简报】" + _lastBrief.MapDisplayNameZh +
                               "（已首破 " + _lastBrief.ClearedDangerZones + "/" + _lastBrief.TotalDangerZones +
                               "｜剩余 " + _lastBrief.RemainingDangerZones +
                               "｜余赏 " + _lastPayoutSummary.RemainingStandardSilver +
                               "/破锁 " + _lastPayoutSummary.RemainingLockBreakSilver +
                               "｜余声望 " + _lastFirstClearSummary.RemainingStandardReputation +
                               "/破锁 " + _lastFirstClearSummary.RemainingLockBreakReputation + "）";

            _summaryRibbonImage.color = ResolveRiskColor(_lastBrief.TravelRiskLevel);
            _summaryRibbonText.text = _lastBrief.HasNearestUncleared
                ? "最近未清剿：" + _lastBrief.NearestUnclearedZone.SummaryLineZh
                : "本境所有危险区均已完成首破清剿。";

            UpdateTabHighlight();

            List<RpgWorld500xDangerZoneTacticalEntry> filtered =
                RpgWorld500xDangerZoneTacticalBriefService.FilterEntries(
                    _lastBrief,
                    unclearedOnly: _activeFilter == RpgWorld500xDangerBriefFilter.UnclearedOnly,
                    minEffectiveThreatLevel: _minEffectiveThreatLevel,
                    tierFilter: _tierFilter);

            const float rowHeight = 36f;
            const float spacing = 6f;

            for (int i = 0; i < filtered.Count; i++)
            {
                RpgWorld500xDangerZoneTacticalEntry entry = filtered[i];
                Color rowColor = entry.IsCleared
                    ? new Color(0.15f, 0.23f, 0.17f, 0.90f)
                    : (entry.EffectiveThreatLevel >= 4
                        ? new Color(0.30f, 0.14f, 0.16f, 0.92f)
                        : new Color(0.13f, 0.15f, 0.22f, 0.90f));

                SpawnInfoRow("DangerRow_" + entry.MarkerId, i, rowHeight, spacing, entry.SummaryLineZh, rowColor);
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
            return IsVisible && _canvas != null && _canvas.isActiveAndEnabled &&
                !_bootstrapper.IsDisposed && button != null &&
                button.isActiveAndEnabled && button.IsInteractable();
        }

        private void UpdateTabHighlight()
        {
            SetButtonColor(_unclearedTabButton, _activeFilter == RpgWorld500xDangerBriefFilter.UnclearedOnly);
            SetButtonColor(_allZonesTabButton, _activeFilter == RpgWorld500xDangerBriefFilter.AllZones);
        }

        private static Color ResolveRiskColor(RpgWorld500xTravelRiskLevel level)
        {
            switch (level)
            {
                case RpgWorld500xTravelRiskLevel.Hazardous:
                    return new Color(0.50f, 0.16f, 0.16f, 0.94f);
                case RpgWorld500xTravelRiskLevel.Caution:
                    return new Color(0.46f, 0.34f, 0.14f, 0.94f);
                case RpgWorld500xTravelRiskLevel.StealthAdvantage:
                    return new Color(0.18f, 0.30f, 0.42f, 0.94f);
                default:
                    return new Color(0.14f, 0.32f, 0.22f, 0.94f);
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
