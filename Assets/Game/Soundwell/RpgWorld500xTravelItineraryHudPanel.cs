using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Interactive UGUI Multi-Hop Cross-Region Travel Itinerary modal panel bound to
    /// RpgWorld500xSceneBootstrapper, RpgWorld500xFastTravelItineraryService,
    /// RpgWorld500xCampProvisioningService, and RpgWorld500xWorldAtlasPortalService.
    /// Lets players pick any of the 4 500x maps, inspect the shortest-time multi-hop
    /// route and per-leg weather risks, auto-provision route supplies in one click
    /// (respecting active timed supply buffs), and depart along the first hop with
    /// parent CanvasGroup interactability guards and zero-leak cleanup.
    /// </summary>
    public sealed class RpgWorld500xTravelItineraryHudPanel : IDisposable
    {
        private readonly RpgWorld500xSceneBootstrapper _bootstrapper;
        private readonly RpgWorld500xFieldSupplyUseService _supplyUseService;
        private readonly GameObject _rootObject;
        private readonly Transform _rowContainer;
        private readonly Text _headerText;
        private readonly Image _summaryBannerImage;
        private readonly Text _summaryBannerText;
        private readonly Text _feedbackText;
        private readonly Dictionary<RpgWorldMapId, Button> _destinationButtons =
            new Dictionary<RpgWorldMapId, Button>();
        private readonly Button _autoProvisionButton;
        private readonly Text _autoProvisionButtonLabel;
        private readonly Button _departFirstLegButton;
        private readonly Button _closeButton;
        private readonly Font _font;
        private readonly List<GameObject> _spawnedRows = new List<GameObject>();
        private RpgWorldMapId _selectedDestination;
        private RpgWorld500xTravelItinerary _lastItinerary;
        private RpgWorld500xProvisioningPlan _lastProvisioningPlan;
        private RpgWorld500xProvisioningReceipt _lastProvisioningReceipt;
        private float _lastElapsedSeconds;
        private bool _visible;
        private bool _disposed;

        public bool IsVisible => !IsDisposed && _visible;
        public bool IsDisposed => _disposed || _rootObject == null;
        public RpgWorldMapId SelectedDestination => _selectedDestination;
        public RpgWorld500xTravelItinerary LastItinerary => _lastItinerary;
        public RpgWorld500xProvisioningPlan LastProvisioningPlan => _lastProvisioningPlan;
        public RpgWorld500xProvisioningReceipt LastProvisioningReceipt => _lastProvisioningReceipt;
        public int DisplayedLegRowCount => _spawnedRows.Count;
        public string HeaderText => _headerText != null ? _headerText.text : string.Empty;
        public string SummaryBannerText => _summaryBannerText != null ? _summaryBannerText.text : string.Empty;
        public string FeedbackText => _feedbackText != null ? _feedbackText.text : string.Empty;

        public RpgWorld500xTravelItineraryHudPanel(
            Transform parent,
            RpgWorld500xSceneBootstrapper bootstrapper,
            RpgWorld500xFieldSupplyUseService supplyUseService = null)
        {
            _bootstrapper = bootstrapper ?? throw new ArgumentNullException(nameof(bootstrapper));
            _supplyUseService = supplyUseService;
            _selectedDestination = _bootstrapper.Driver.Facade.CurrentMapId == RpgWorldMapId.MineCamp
                ? RpgWorldMapId.Soundwell
                : RpgWorldMapId.MineCamp;
            _font = ResolveUiFont();

            _rootObject = new GameObject("World500xTravelItineraryHudPanel", typeof(RectTransform), typeof(Canvas));
            if (parent != null)
            {
                _rootObject.transform.SetParent(parent, false);
            }

            var canvas = _rootObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 76;

            var scaler = _rootObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);

            GameObject modal = CreateBox(
                _rootObject.transform,
                "ItineraryModalBox",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(760f, 500f),
                new Color(0.08f, 0.09f, 0.12f, 0.96f));

            _headerText = CreateText(
                modal.transform,
                "HeaderLabel",
                new Vector2(0.04f, 0.89f), new Vector2(0.96f, 0.96f),
                _font, 15, TextAnchor.MiddleLeft,
                new Color(0.97f, 0.89f, 0.60f, 1f));

            BuildDestinationButtons(modal.transform);

            GameObject bannerBox = CreateBox(
                modal.transform,
                "SummaryBannerBox",
                new Vector2(0.04f, 0.69f), new Vector2(0.96f, 0.78f), new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero,
                new Color(0.14f, 0.28f, 0.22f, 0.94f));
            var bannerRect = bannerBox.GetComponent<RectTransform>();
            bannerRect.offsetMin = Vector2.zero;
            bannerRect.offsetMax = Vector2.zero;
            _summaryBannerImage = bannerBox.GetComponent<Image>();

            _summaryBannerText = CreateText(
                bannerBox.transform,
                "SummaryBannerLabel",
                new Vector2(0.03f, 0.06f), new Vector2(0.97f, 0.94f),
                _font, 12, TextAnchor.MiddleLeft,
                Color.white);

            _feedbackText = CreateText(
                modal.transform,
                "FeedbackLabel",
                new Vector2(0.04f, 0.61f), new Vector2(0.96f, 0.68f),
                _font, 12, TextAnchor.MiddleLeft,
                new Color(0.84f, 0.92f, 0.96f, 1f));
            _feedbackText.text = "选择目标区域查看最优多跳路线、耗时与沿途天时风险。";

            var rowsGo = new GameObject("ItineraryLegRows", typeof(RectTransform));
            rowsGo.transform.SetParent(modal.transform, false);
            var rowsRect = rowsGo.GetComponent<RectTransform>();
            rowsRect.anchorMin = new Vector2(0.04f, 0.16f);
            rowsRect.anchorMax = new Vector2(0.96f, 0.60f);
            rowsRect.offsetMin = Vector2.zero;
            rowsRect.offsetMax = Vector2.zero;
            _rowContainer = rowsGo.transform;

            _autoProvisionButton = CreateActionButton(
                modal.transform, "AutoProvisionItineraryButton",
                new Vector2(0.04f, 0.05f), new Vector2(0.38f, 0.13f),
                "按行程一键整备", _font,
                HandleAutoProvisionClicked);
            _autoProvisionButtonLabel = _autoProvisionButton.GetComponentInChildren<Text>();

            _departFirstLegButton = CreateActionButton(
                modal.transform, "DepartFirstLegButton",
                new Vector2(0.40f, 0.05f), new Vector2(0.72f, 0.13f),
                "沿首段路线启程", _font,
                HandleDepartFirstLegClicked);

            _closeButton = CreateActionButton(
                modal.transform, "CloseItineraryButton",
                new Vector2(0.74f, 0.05f), new Vector2(0.96f, 0.13f),
                "关闭行程规划", _font,
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

        public bool Show(RpgWorldMapId? destination = null, float elapsedSeconds = 0f)
        {
            if (IsDisposed)
            {
                return false;
            }

            if (destination.HasValue)
            {
                _selectedDestination = destination.Value;
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

        public bool TrySelectDestination(RpgWorldMapId destination)
        {
            if (!_destinationButtons.TryGetValue(destination, out Button btn) || !CanClick(btn))
            {
                return false;
            }

            btn.onClick.Invoke();
            return true;
        }

        public bool TryClickAutoProvision()
        {
            if (!CanClick(_autoProvisionButton))
            {
                return false;
            }

            _autoProvisionButton.onClick.Invoke();
            return true;
        }

        public bool TryClickDepartFirstLeg()
        {
            if (!CanClick(_departFirstLegButton))
            {
                return false;
            }

            _departFirstLegButton.onClick.Invoke();
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

            RpgWorld500xGrandRuntimeFacade facade = _bootstrapper.Driver.Facade;
            RpgWorldMapId origin = facade.CurrentMapId;
            _lastItinerary = RpgWorld500xFastTravelItineraryService.PlanShortestItinerary(
                origin,
                _selectedDestination,
                facade.Atmosphere);
            _lastProvisioningPlan = RpgWorld500xCampProvisioningService.BuildProvisioningPlan(
                _bootstrapper,
                _selectedDestination,
                _lastElapsedSeconds,
                _supplyUseService);

            _headerText.text = "【四境多跳行程规划】当前位置：" + facade.Session.Host.Layout.Spec.DisplayNameZh;
            _summaryBannerImage.color = ResolveRiskColor(_lastItinerary.HighestRiskLevel);
            _summaryBannerText.text = _lastItinerary.SummaryZh;

            UpdateDestinationHighlights();

            bool needsProvisioning = _lastProvisioningPlan.Lines.Count > 0;
            _autoProvisionButton.interactable = needsProvisioning;
            SetButtonColor(_autoProvisionButton, needsProvisioning);
            if (_autoProvisionButtonLabel != null)
            {
                _autoProvisionButtonLabel.text = needsProvisioning
                    ? "一键整备(" + _lastProvisioningPlan.TotalRequiredSaltSilver + "盐银)"
                    : "行囊已齐备";
            }

            bool canDepart = _lastItinerary.IsReachable && _lastItinerary.HopCount > 0;
            _departFirstLegButton.interactable = canDepart;
            SetButtonColor(_departFirstLegButton, canDepart);

            const float rowHeight = 52f;
            const float spacing = 8f;
            for (int i = 0; i < _lastItinerary.Legs.Count; i++)
            {
                RpgWorld500xItineraryLeg leg = _lastItinerary.Legs[i];
                string line = "第 " + (i + 1) + " 段：" + leg.Route.RouteNameZh +
                              "（耗时 " + leg.Route.TravelMinutesInGame + " 分钟｜落点风险：" + leg.DestinationRiskLabelZh + "）\n" +
                              leg.Route.FlavorNoteZh;
                Color rowColor = leg.DestinationRiskLevel == RpgWorld500xTravelRiskLevel.Hazardous
                    ? new Color(0.32f, 0.14f, 0.16f, 0.92f)
                    : new Color(0.13f, 0.15f, 0.21f, 0.90f);

                SpawnInfoRow("LegRow_" + i + "_" + leg.Route.RouteId, i, rowHeight, spacing, line, rowColor);
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
            _destinationButtons.Clear();
            DestroySafe(_rootObject);
        }

        private bool CanClick(Button button)
        {
            return IsVisible && !_bootstrapper.IsDisposed && button != null &&
                button.isActiveAndEnabled && button.IsInteractable();
        }

        private void BuildDestinationButtons(Transform parent)
        {
            IReadOnlyList<RpgExpandedMapSpec> specs = RpgExpandedWorldScale.AllSpecs;
            float width = specs.Count > 0 ? 0.92f / specs.Count : 0.92f;
            for (int i = 0; i < specs.Count; i++)
            {
                RpgExpandedMapSpec spec = specs[i];
                float xMin = 0.04f + (i * width) + 0.004f;
                float xMax = 0.04f + ((i + 1) * width) - 0.004f;
                RpgWorldMapId captured = spec.MapId;
                Button btn = null;
                btn = CreateActionButton(
                    parent,
                    "Dest_" + spec.MapId,
                    new Vector2(xMin, 0.80f), new Vector2(xMax, 0.88f),
                    spec.DisplayNameZh,
                    _font,
                    () =>
                    {
                        if (!CanClick(btn))
                        {
                            return;
                        }

                        _selectedDestination = captured;
                        Refresh(_lastElapsedSeconds);
                    });
                _destinationButtons[spec.MapId] = btn;
            }
        }

        private void HandleAutoProvisionClicked()
        {
            if (!CanClick(_autoProvisionButton))
            {
                return;
            }

            _lastProvisioningReceipt = RpgWorld500xCampProvisioningService.ExecuteAutoProvisioning(
                _bootstrapper,
                _selectedDestination,
                allowPartialIfUnderBudget: true,
                elapsedSeconds: _lastElapsedSeconds,
                supplyUseService: _supplyUseService);

            _feedbackText.text = _lastProvisioningReceipt.SummaryZh;
            if (_lastProvisioningReceipt.TotalSpentSaltSilver > 0)
            {
                _bootstrapper.StatusHud.SyncFromDriver(_bootstrapper.Driver);
            }

            Refresh(_lastElapsedSeconds);
        }

        private void HandleDepartFirstLegClicked()
        {
            if (!CanClick(_departFirstLegButton) || !_lastItinerary.IsReachable || _lastItinerary.Legs.Count == 0)
            {
                return;
            }

            RpgWorld500xItineraryLeg firstLeg = _lastItinerary.Legs[0];
            if (RpgWorld500xWorldAtlasPortalService.TryTravelToConnectedRegion(
                _bootstrapper,
                firstLeg.Route.ToMap,
                out RpgWorld500xRegionTravelResult result))
            {
                _feedbackText.text = result.ArrivalBannerZh;
            }
            else
            {
                _feedbackText.text = "当前无法启程，请确认未处于危险遭遇战中。";
            }

            Refresh(_lastElapsedSeconds);
        }

        private void UpdateDestinationHighlights()
        {
            foreach (KeyValuePair<RpgWorldMapId, Button> kv in _destinationButtons)
            {
                SetButtonColor(kv.Value, kv.Key == _selectedDestination);
            }
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
