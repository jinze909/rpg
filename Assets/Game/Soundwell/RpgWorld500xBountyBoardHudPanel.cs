using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Soundwell
{
    public enum RpgWorld500xBountyBoardScope : byte
    {
        ActiveMap = 0,
        AllRegions = 1
    }

    /// <summary>
    /// Interactive UGUI Field Bounty Board &amp; Contract Tracker panel bound to
    /// RpgWorld500xSceneBootstrapper and RpgWorld500xFieldBountyAndSupplyService.
    /// Lists active-region (3) or cross-region (12) exploration bounties with live
    /// progress counts, Salt-Silver rewards, read-only active-map bounty summary,
    /// idempotent claim action, parent CanvasGroup interactability guards, and
    /// zero-leak IDisposable cleanup.
    /// </summary>
    public sealed class RpgWorld500xBountyBoardHudPanel : IDisposable
    {
        private readonly RpgWorld500xSceneBootstrapper _bootstrapper;
        private readonly GameObject _rootObject;
        private readonly Canvas _canvas;
        private readonly Transform _rowContainer;
        private readonly Text _headerText;
        private readonly Text _feedbackText;
        private readonly Button _activeMapTabButton;
        private readonly Button _allRegionsTabButton;
        private readonly Button _claimButton;
        private readonly Text _claimButtonLabel;
        private readonly Button _closeButton;
        private readonly Font _font;
        private readonly List<GameObject> _spawnedRows = new List<GameObject>();
        private RpgWorld500xBountyBoardScope _activeScope = RpgWorld500xBountyBoardScope.ActiveMap;
        private RpgWorld500xBountyBoardSummary _activeMapSummary;
        private int _claimedCount;
        private int _claimableCount;
        private int _totalBountyCount;
        private bool _visible;
        private bool _disposed;

        public bool IsVisible => !IsDisposed && _visible;
        public bool IsDisposed => _disposed || _rootObject == null;
        public RpgWorld500xBountyBoardScope ActiveScope => _activeScope;
        public RpgWorld500xBountyBoardSummary ActiveMapSummary => _activeMapSummary;
        public int DisplayedRowCount => _spawnedRows.Count;
        public int ClaimedCount => _claimedCount;
        public int ClaimableCount => _claimableCount;
        public int ClaimableSaltSilver => _activeMapSummary.ClaimableSaltSilver;
        public int RemainingSaltSilver => _activeMapSummary.RemainingSaltSilver;
        public int TotalBountyCount => _totalBountyCount;
        public string ActiveMapSummaryZh => _activeMapSummary.SummaryZh;
        public string ClaimButtonLabelText => _claimButtonLabel != null ? _claimButtonLabel.text : string.Empty;
        public string HeaderText => _headerText != null ? _headerText.text : string.Empty;
        public string FeedbackText => _feedbackText != null ? _feedbackText.text : string.Empty;

        public RpgWorld500xBountyBoardHudPanel(
            Transform parent,
            RpgWorld500xSceneBootstrapper bootstrapper)
        {
            _bootstrapper = bootstrapper ?? throw new ArgumentNullException(nameof(bootstrapper));
            _font = ResolveUiFont();

            _rootObject = new GameObject("World500xBountyBoardHudPanel", typeof(RectTransform), typeof(Canvas));
            if (parent != null)
            {
                _rootObject.transform.SetParent(parent, false);
            }

            _canvas = _rootObject.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 71;

            var scaler = _rootObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);

            GameObject modal = CreateBox(
                _rootObject.transform,
                "BountyBoardModalBox",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(760f, 500f),
                new Color(0.08f, 0.09f, 0.12f, 0.96f));

            _headerText = CreateText(
                modal.transform,
                "HeaderLabel",
                new Vector2(0.04f, 0.89f), new Vector2(0.96f, 0.96f),
                _font, 15, TextAnchor.MiddleLeft,
                new Color(0.97f, 0.88f, 0.58f, 1f));

            _activeMapTabButton = CreateActionButton(
                modal.transform, "Tab_ActiveMap",
                new Vector2(0.04f, 0.80f), new Vector2(0.49f, 0.88f),
                "本境悬赏（3）", _font,
                () =>
                {
                    if (!CanClick(_activeMapTabButton))
                    {
                        return;
                    }

                    _activeScope = RpgWorld500xBountyBoardScope.ActiveMap;
                    Refresh();
                });

            _allRegionsTabButton = CreateActionButton(
                modal.transform, "Tab_AllRegions",
                new Vector2(0.51f, 0.80f), new Vector2(0.96f, 0.88f),
                "四境总榜（12）", _font,
                () =>
                {
                    if (!CanClick(_allRegionsTabButton))
                    {
                        return;
                    }

                    _activeScope = RpgWorld500xBountyBoardScope.AllRegions;
                    Refresh();
                });

            _feedbackText = CreateText(
                modal.transform,
                "FeedbackLabel",
                new Vector2(0.04f, 0.72f), new Vector2(0.96f, 0.79f),
                _font, 12, TextAnchor.MiddleLeft,
                new Color(0.84f, 0.92f, 0.96f, 1f));
            _feedbackText.text = "完成片区踏勘、补给回收或捷径贯通后，可在此一键结算盐银悬赏。";

            var rowsGo = new GameObject("BountyRows", typeof(RectTransform));
            rowsGo.transform.SetParent(modal.transform, false);
            var rowsRect = rowsGo.GetComponent<RectTransform>();
            rowsRect.anchorMin = new Vector2(0.04f, 0.16f);
            rowsRect.anchorMax = new Vector2(0.96f, 0.71f);
            rowsRect.offsetMin = Vector2.zero;
            rowsRect.offsetMax = Vector2.zero;
            _rowContainer = rowsGo.transform;

            _claimButton = CreateActionButton(
                modal.transform, "ClaimEligibleBountiesButton",
                new Vector2(0.04f, 0.05f), new Vector2(0.58f, 0.13f),
                "领取本境已达标悬赏", _font,
                HandleClaimClicked);
            _claimButtonLabel = _claimButton.GetComponentInChildren<Text>();

            _closeButton = CreateActionButton(
                modal.transform, "CloseBountyBoardButton",
                new Vector2(0.62f, 0.05f), new Vector2(0.96f, 0.13f),
                "关闭悬赏榜", _font,
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

        public bool Show(RpgWorld500xBountyBoardScope? scope = null)
        {
            if (IsDisposed)
            {
                return false;
            }

            if (scope.HasValue)
            {
                _activeScope = scope.Value;
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

        public bool TrySelectScope(RpgWorld500xBountyBoardScope scope)
        {
            Button btn = scope == RpgWorld500xBountyBoardScope.ActiveMap
                ? _activeMapTabButton
                : _allRegionsTabButton;
            if (!CanClick(btn))
            {
                return false;
            }

            btn.onClick.Invoke();
            return true;
        }

        public bool TryClickClaimEligible()
        {
            if (!CanClick(_claimButton))
            {
                return false;
            }

            _claimButton.onClick.Invoke();
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
            string activeMapNameZh = facade.Session.Host.Layout.Spec.DisplayNameZh;
            RpgWorld500xMapCompletionSummary activeSummary = facade.GetCompletionSummary();
            RpgWorld500xFieldBountyAndSupplyService bountySvc = facade.BountyAndSupply;

            _activeMapSummary = bountySvc.BuildBountyBoardSummary(activeSummary);
            _claimableCount = _activeMapSummary.ClaimableCount;

            IReadOnlyList<RpgExpandedMapSpec> specs = RpgExpandedWorldScale.AllSpecs;
            _claimedCount = 0;
            _totalBountyCount = 0;

            for (int s = 0; s < specs.Count; s++)
            {
                IReadOnlyList<RpgWorld500xFieldBountyDef> regionBounties =
                    RpgWorld500xBountyAndSupplyCatalog.GetBounties(specs[s].MapId);
                for (int i = 0; i < regionBounties.Count; i++)
                {
                    RpgWorld500xFieldBountyDef b = regionBounties[i];
                    _totalBountyCount++;
                    if (bountySvc.IsBountyClaimed(b))
                    {
                        _claimedCount++;
                    }
                }
            }

            _headerText.text = "【四境悬赏榜】" + activeMapNameZh +
                               "（总进度 " + _claimedCount + "/" + _totalBountyCount +
                               "｜本境可领 " + _claimableCount +
                               "｜剩余总赏 " + _activeMapSummary.RemainingSaltSilver +
                               "｜盐银 " + bountySvc.SaltSilverBalance + "）";

            UpdateTabHighlight();
            _claimButton.interactable = _claimableCount > 0;
            SetButtonColor(_claimButton, _claimableCount > 0);
            if (_claimButtonLabel != null)
            {
                _claimButtonLabel.text = _claimableCount > 0
                    ? "领取本境已达标悬赏（+" + _activeMapSummary.ClaimableSaltSilver + " 盐银）"
                    : "领取本境已达标悬赏";
            }

            if (_activeScope == RpgWorld500xBountyBoardScope.ActiveMap)
            {
                IReadOnlyList<RpgWorld500xFieldBountyDef> activeBounties =
                    RpgWorld500xBountyAndSupplyCatalog.GetBounties(activeMapId);
                const float rowHeight = 76f;
                const float spacing = 10f;

                for (int i = 0; i < activeBounties.Count; i++)
                {
                    RpgWorld500xFieldBountyDef b = activeBounties[i];
                    bool claimed = bountySvc.IsBountyClaimed(b);
                    int progress = RpgWorld500xFieldBountyAndSupplyService.EvaluateProgressCount(b, activeSummary);
                    bool ready = !claimed && progress >= b.TargetCount;

                    string tag = claimed ? "[已领取] " : (ready ? "[可领取] " : "[进行中] ");
                    string line = tag + b.TitleZh + "｜赏金：盐银 +" + b.SaltSilverReward +
                                  "｜进度：" + Math.Min(progress, b.TargetCount) + "/" + b.TargetCount + "\n" +
                                  b.DescriptionZh;

                    Color rowColor = claimed
                        ? new Color(0.15f, 0.22f, 0.16f, 0.90f)
                        : (ready
                            ? new Color(0.24f, 0.30f, 0.14f, 0.94f)
                            : new Color(0.12f, 0.14f, 0.20f, 0.90f));

                    SpawnInfoRow("BountyRow_" + b.Id, i, rowHeight, spacing, line, rowColor);
                }
            }
            else
            {
                const float rowHeight = 20f;
                const float spacing = 2f;
                int rowIndex = 0;

                for (int s = 0; s < specs.Count; s++)
                {
                    RpgExpandedMapSpec spec = specs[s];
                    IReadOnlyList<RpgWorld500xFieldBountyDef> regionBounties =
                        RpgWorld500xBountyAndSupplyCatalog.GetBounties(spec.MapId);

                    for (int i = 0; i < regionBounties.Count; i++)
                    {
                        RpgWorld500xFieldBountyDef b = regionBounties[i];
                        bool claimed = bountySvc.IsBountyClaimed(b);
                        string statusTag;
                        if (claimed)
                        {
                            statusTag = "[已领]";
                        }
                        else if (spec.MapId == activeMapId)
                        {
                            int progress = RpgWorld500xFieldBountyAndSupplyService.EvaluateProgressCount(b, activeSummary);
                            statusTag = progress >= b.TargetCount
                                ? "[可领]"
                                : "[" + progress + "/" + b.TargetCount + "]";
                        }
                        else
                        {
                            statusTag = "[未领]";
                        }

                        string line = statusTag + " " + spec.DisplayNameZh + " · " + b.TitleZh +
                                      "（目标 " + b.TargetCount + "｜盐银 +" + b.SaltSilverReward + "）";

                        Color rowColor = claimed
                            ? new Color(0.15f, 0.21f, 0.16f, 0.88f)
                            : new Color(0.12f, 0.14f, 0.19f, 0.88f);

                        SpawnInfoRow("AllBountyRow_" + b.Id, rowIndex++, rowHeight, spacing, line, rowColor);
                    }
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
            return IsVisible && _canvas != null && _canvas.isActiveAndEnabled &&
                !_bootstrapper.IsDisposed && button != null &&
                button.isActiveAndEnabled && button.IsInteractable();
        }

        private void HandleClaimClicked()
        {
            if (!CanClick(_claimButton))
            {
                return;
            }

            RpgWorld500xGrandRuntimeFacade facade = _bootstrapper.Driver.Facade;
            // The visible claim belongs to the region displayed when the board
            // was refreshed. Do not settle a different region on a stale click.
            if (facade.CurrentMapId != _activeMapSummary.MapId)
            {
                _feedbackText.text = "本境悬赏已更新，请确认后重试";
                Refresh();
                return;
            }

            RpgWorld500xMapCompletionSummary summary = facade.GetCompletionSummary();
            List<RpgWorld500xFieldBountyDef> claimed = facade.BountyAndSupply.ClaimCompletedBounties(summary);

            if (claimed.Count > 0)
            {
                int totalSilver = 0;
                for (int i = 0; i < claimed.Count; i++)
                {
                    totalSilver += claimed[i].SaltSilverReward;
                }

                _feedbackText.text = "已结算 " + claimed.Count + " 项悬赏，入账盐银 +" + totalSilver + "！";
                _bootstrapper.StatusHud.SyncFromDriver(_bootstrapper.Driver);
            }
            else
            {
                _feedbackText.text = "当前暂无达到结算条件的本境悬赏。";
            }

            Refresh();
        }

        private void UpdateTabHighlight()
        {
            SetButtonColor(_activeMapTabButton, _activeScope == RpgWorld500xBountyBoardScope.ActiveMap);
            SetButtonColor(_allRegionsTabButton, _activeScope == RpgWorld500xBountyBoardScope.AllRegions);
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