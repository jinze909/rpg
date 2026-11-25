using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Interactive UGUI Pre-Departure Expedition Readiness &amp; Camp Shopping Checklist
    /// modal panel bound to RpgWorld500xSceneBootstrapper and
    /// RpgWorld500xExpeditionReadinessService. Displays live 0-100 readiness score,
    /// color-coded readiness grade banner, four readiness checks, prioritized missing
    /// camp supplies, and a one-click auto-restock action with parent CanvasGroup
    /// interactability guards and zero-leak IDisposable cleanup.
    /// </summary>
    public sealed class RpgWorld500xExpeditionReadinessHudPanel : IDisposable
    {
        private readonly RpgWorld500xSceneBootstrapper _bootstrapper;
        private readonly RpgWorld500xFieldSupplyUseService _supplyUseService;
        private readonly GameObject _rootObject;
        private readonly Transform _rowContainer;
        private readonly Text _headerText;
        private readonly Image _gradeBannerImage;
        private readonly Text _gradeBannerText;
        private readonly Text _feedbackText;
        private readonly Button _buyRecommendedButton;
        private readonly Button _refreshButton;
        private readonly Button _closeButton;
        private readonly Font _font;
        private readonly List<GameObject> _spawnedRows = new List<GameObject>();
        private RpgWorld500xExpeditionReadinessReport _lastReport;
        private float _lastElapsedSeconds;
        private bool _visible;
        private bool _disposed;

        public bool IsVisible => !IsDisposed && _visible;
        public bool IsDisposed => _disposed || _rootObject == null;
        public RpgWorld500xExpeditionReadinessReport LastReport => _lastReport;
        public int DisplayedRowCount => _spawnedRows.Count;
        public string HeaderText => _headerText != null ? _headerText.text : string.Empty;
        public string GradeBannerText => _gradeBannerText != null ? _gradeBannerText.text : string.Empty;
        public string FeedbackText => _feedbackText != null ? _feedbackText.text : string.Empty;

        public RpgWorld500xExpeditionReadinessHudPanel(
            Transform parent,
            RpgWorld500xSceneBootstrapper bootstrapper,
            RpgWorld500xFieldSupplyUseService supplyUseService = null)
        {
            _bootstrapper = bootstrapper ?? throw new ArgumentNullException(nameof(bootstrapper));
            _supplyUseService = supplyUseService;
            _font = ResolveUiFont();

            _rootObject = new GameObject("World500xExpeditionReadinessHudPanel", typeof(RectTransform), typeof(Canvas));
            if (parent != null)
            {
                _rootObject.transform.SetParent(parent, false);
            }

            var canvas = _rootObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 73;

            var scaler = _rootObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);

            GameObject modal = CreateBox(
                _rootObject.transform,
                "ReadinessModalBox",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(760f, 500f),
                new Color(0.08f, 0.09f, 0.12f, 0.96f));

            _headerText = CreateText(
                modal.transform,
                "HeaderLabel",
                new Vector2(0.04f, 0.89f), new Vector2(0.96f, 0.96f),
                _font, 15, TextAnchor.MiddleLeft,
                new Color(0.97f, 0.89f, 0.60f, 1f));

            GameObject gradeBox = CreateBox(
                modal.transform,
                "GradeBannerBox",
                new Vector2(0.04f, 0.78f), new Vector2(0.96f, 0.87f), new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero,
                new Color(0.14f, 0.30f, 0.22f, 0.94f));
            var gradeRect = gradeBox.GetComponent<RectTransform>();
            gradeRect.offsetMin = Vector2.zero;
            gradeRect.offsetMax = Vector2.zero;
            _gradeBannerImage = gradeBox.GetComponent<Image>();

            _gradeBannerText = CreateText(
                gradeBox.transform,
                "GradeBannerLabel",
                new Vector2(0.03f, 0.08f), new Vector2(0.97f, 0.92f),
                _font, 13, TextAnchor.MiddleLeft,
                Color.white);

            _feedbackText = CreateText(
                modal.transform,
                "FeedbackLabel",
                new Vector2(0.04f, 0.70f), new Vector2(0.96f, 0.77f),
                _font, 12, TextAnchor.MiddleLeft,
                new Color(0.84f, 0.92f, 0.96f, 1f));
            _feedbackText.text = "出发前核对疗伤、照明、战术信号与修桥整备状态。";

            var rowsGo = new GameObject("ReadinessRows", typeof(RectTransform));
            rowsGo.transform.SetParent(modal.transform, false);
            var rowsRect = rowsGo.GetComponent<RectTransform>();
            rowsRect.anchorMin = new Vector2(0.04f, 0.16f);
            rowsRect.anchorMax = new Vector2(0.96f, 0.69f);
            rowsRect.offsetMin = Vector2.zero;
            rowsRect.offsetMax = Vector2.zero;
            _rowContainer = rowsGo.transform;

            _buyRecommendedButton = CreateActionButton(
                modal.transform, "BuyRecommendedSuppliesButton",
                new Vector2(0.04f, 0.05f), new Vector2(0.46f, 0.13f),
                "一键补购欠缺补给", _font,
                HandleBuyRecommendedClicked);

            _refreshButton = CreateActionButton(
                modal.transform, "RefreshReadinessButton",
                new Vector2(0.49f, 0.05f), new Vector2(0.72f, 0.13f),
                "刷新评估", _font,
                () => Refresh(_lastElapsedSeconds));

            _closeButton = CreateActionButton(
                modal.transform, "CloseReadinessButton",
                new Vector2(0.75f, 0.05f), new Vector2(0.96f, 0.13f),
                "关闭整备单", _font,
                () => Hide());

            _visible = false;
            _rootObject.SetActive(false);
            Refresh(0f);
        }

        public bool Show(float elapsedSeconds = 0f)
        {
            if (IsDisposed)
            {
                return false;
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

        public void Refresh(float elapsedSeconds = 0f)
        {
            if (IsDisposed || _bootstrapper.IsDisposed)
            {
                return;
            }

            ClearRows();
            _lastElapsedSeconds = elapsedSeconds;
            _lastReport = RpgWorld500xExpeditionReadinessService.EvaluateReadiness(
                _bootstrapper,
                elapsedSeconds,
                _supplyUseService);

            int balance = _bootstrapper.Driver.Facade.BountyAndSupply.SaltSilverBalance;
            _headerText.text = "【启程整备核验】" + _lastReport.MapDisplayNameZh +
                               "｜盐银余额 " + balance +
                               "｜未通捷径 " + _lastReport.LockedShortcutCount + " 处";

            _gradeBannerImage.color = ResolveGradeColor(_lastReport.Grade);
            _gradeBannerText.text = _lastReport.ChecklistSummaryZh;

            bool hasRecommendations = _lastReport.RecommendedSupplyIds.Count > 0;
            _buyRecommendedButton.interactable = hasRecommendations;
            SetButtonColor(_buyRecommendedButton, hasRecommendations);

            const float rowHeight = 42f;
            const float spacing = 8f;
            SpawnCheckRow("Check_Healing", 0, rowHeight, spacing,
                (_lastReport.HasHealingStock ? "[已备] " : "[欠缺] ") + "疗伤与干粮补给（止血纱布 / 行军干粮）",
                _lastReport.HasHealingStock);
            SpawnCheckRow("Check_Visibility", 1, rowHeight, spacing,
                (_lastReport.HasVisibilityPrep ? "[已备] " : "[欠缺] ") + "夜间与浓雾照明准备（矿灯灯油或生效增益）",
                _lastReport.HasVisibilityPrep);
            SpawnCheckRow("Check_StealthFlare", 2, rowHeight, spacing,
                (_lastReport.HasStealthOrFlarePrep ? "[已备] " : "[欠缺] ") + "潜行消声与盲区信号准备（防潮湿布 / 应急信号弹）",
                _lastReport.HasStealthOrFlarePrep);
            SpawnCheckRow("Check_ShortcutTools", 3, rowHeight, spacing,
                (_lastReport.HasShortcutTools ? "[已备] " : "[欠缺] ") + "捷径与吊桥检修工具（便携修桥铁件或本境已全通）",
                _lastReport.HasShortcutTools);
        }

        public bool TryClickBuyRecommended()
        {
            if (IsDisposed || _buyRecommendedButton == null ||
                !_buyRecommendedButton.interactable || !_buyRecommendedButton.IsInteractable())
            {
                return false;
            }

            _buyRecommendedButton.onClick.Invoke();
            return true;
        }

        public bool TryClickRefresh(float elapsedSeconds = 0f)
        {
            if (IsDisposed || _refreshButton == null ||
                !_refreshButton.interactable || !_refreshButton.IsInteractable())
            {
                return false;
            }

            _lastElapsedSeconds = elapsedSeconds;
            _refreshButton.onClick.Invoke();
            return true;
        }

        public bool TryClickClose()
        {
            if (IsDisposed || _closeButton == null ||
                !_closeButton.interactable || !_closeButton.IsInteractable())
            {
                return false;
            }

            _closeButton.onClick.Invoke();
            return true;
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

        private void HandleBuyRecommendedClicked()
        {
            if (IsDisposed || _bootstrapper.IsDisposed)
            {
                return;
            }

            RpgWorld500xFieldBountyAndSupplyService shop = _bootstrapper.Driver.Facade.BountyAndSupply;
            IReadOnlyList<string> rec = _lastReport.RecommendedSupplyIds;
            int bought = 0;
            int skipped = 0;

            for (int i = 0; i < rec.Count; i++)
            {
                if (shop.TryPurchaseSupply(rec[i], 1) == RpgCampSupplyPurchaseStatus.Succeeded)
                {
                    bought++;
                }
                else
                {
                    skipped++;
                }
            }

            if (bought > 0)
            {
                _feedbackText.text = skipped == 0
                    ? "已补购 " + bought + " 项推荐补给，整备状态已更新。"
                    : "已补购 " + bought + " 项推荐补给，另有 " + skipped + " 项因盐银或库存不足未能购入。";
                _bootstrapper.StatusHud.SyncFromDriver(_bootstrapper.Driver);
            }
            else
            {
                _feedbackText.text = "盐银余额不足，暂无法补购推荐补给。";
            }

            Refresh(_lastElapsedSeconds);
        }

        private void SpawnCheckRow(
            string name,
            int index,
            float height,
            float spacing,
            string label,
            bool passed)
        {
            var rowGo = new GameObject(name, typeof(RectTransform), typeof(Image));
            rowGo.transform.SetParent(_rowContainer, false);
            var rect = rowGo.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -(index * (height + spacing)));
            rect.sizeDelta = new Vector2(0f, height);
            rowGo.GetComponent<Image>().color = passed
                ? new Color(0.15f, 0.25f, 0.18f, 0.92f)
                : new Color(0.26f, 0.16f, 0.16f, 0.90f);

            Text text = CreateText(
                rowGo.transform,
                "Label",
                new Vector2(0.03f, 0.06f), new Vector2(0.97f, 0.94f),
                _font, 12, TextAnchor.MiddleLeft,
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

        private static Color ResolveGradeColor(RpgWorld500xReadinessGrade grade)
        {
            switch (grade)
            {
                case RpgWorld500xReadinessGrade.WellPrepared:
                    return new Color(0.14f, 0.34f, 0.22f, 0.95f);
                case RpgWorld500xReadinessGrade.Ready:
                    return new Color(0.16f, 0.30f, 0.40f, 0.95f);
                case RpgWorld500xReadinessGrade.Underprepared:
                    return new Color(0.46f, 0.34f, 0.14f, 0.95f);
                default:
                    return new Color(0.52f, 0.16f, 0.16f, 0.95f);
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
                    ? new Color(0.22f, 0.36f, 0.48f, 0.96f)
                    : new Color(0.14f, 0.16f, 0.21f, 0.80f);
            }
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
