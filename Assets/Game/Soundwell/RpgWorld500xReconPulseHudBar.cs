using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Compact non-modal UGUI Field Recon Pulse HUD bar bound to
    /// RpgWorld500xSceneBootstrapper and RpgWorld500xFieldReconScannerService.
    /// Triggers weather-modulated recon scans around the player, supports optional
    /// category filtering and cooldown-free nearest-target preview, displays live
    /// cooldown readiness, and renders the top 3 nearest undiscovered POI pings
    /// under a zero-leak IDisposable lifecycle.
    /// </summary>
    public sealed class RpgWorld500xReconPulseHudBar : IDisposable
    {
        private readonly RpgWorld500xSceneBootstrapper _bootstrapper;
        private readonly RpgWorld500xFieldReconScannerService _scanner;
        private readonly GameObject _rootObject;
        private readonly Button _scanButton;
        private readonly Text _scanButtonLabel;
        private readonly Text _bannerText;
        private readonly Text _pingsText;
        private readonly Font _font;
        private readonly List<RpgWorld500xReconPing> _lastPings = new List<RpgWorld500xReconPing>();
        // The controller supplies its session clock each frame. Mouse and hotkey
        // clicks must share it rather than mixing it with Unity's global clock.
        private float _currentClockSeconds;
        private RpgWorld500xReconPingKind? _activeFilterKind;
        private bool _disposed;

        public bool IsDisposed => _disposed || _rootObject == null;
        public bool IsScanButtonInteractable => !IsDisposed && _scanButton != null &&
            _scanButton.isActiveAndEnabled && _scanButton.gameObject.activeInHierarchy && _scanButton.IsInteractable();
        public RpgWorld500xReconPingKind? ActiveFilterKind => _activeFilterKind;
        public string ActiveFilterLabelZh => ResolveFilterLabelZh(_activeFilterKind);
        public int LastPingCount => _lastPings.Count;
        public IReadOnlyList<RpgWorld500xReconPing> LastPings => _lastPings;
        public string BannerText => _bannerText != null ? _bannerText.text : string.Empty;
        public string PingsText => _pingsText != null ? _pingsText.text : string.Empty;
        public string ButtonLabelText => _scanButtonLabel != null ? _scanButtonLabel.text : string.Empty;

        public RpgWorld500xReconPulseHudBar(
            Transform parent,
            RpgWorld500xSceneBootstrapper bootstrapper,
            float cooldownSeconds = RpgWorld500xFieldReconScannerService.DefaultCooldownSeconds)
        {
            _bootstrapper = bootstrapper ?? throw new ArgumentNullException(nameof(bootstrapper));
            _font = ResolveUiFont();
            _scanner = new RpgWorld500xFieldReconScannerService(
                getInt: k => (_bootstrapper.Store != null && _bootstrapper.Store.TryGetValue(k, out int v)) ? v : 0,
                cooldownSeconds: cooldownSeconds);

            _rootObject = new GameObject("World500xReconPulseHudBar", typeof(RectTransform), typeof(Canvas));
            if (parent != null)
            {
                _rootObject.transform.SetParent(parent, false);
            }

            var canvas = _rootObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 58;

            var scaler = _rootObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);

            GameObject bar = CreateBox(
                _rootObject.transform,
                "ReconPulsePanel",
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(16f, 16f), new Vector2(460f, 118f),
                new Color(0.07f, 0.10f, 0.14f, 0.88f));

            _scanButton = CreateActionButton(
                bar.transform,
                "PulseScanButton",
                new Vector2(0.03f, 0.66f), new Vector2(0.32f, 0.92f),
                "野外勘测 [就绪]",
                _font,
                HandleScanButtonClicked,
                out _scanButtonLabel);

            _bannerText = CreateText(
                bar.transform,
                "BannerLabel",
                new Vector2(0.34f, 0.66f), new Vector2(0.97f, 0.92f),
                _font, 12, TextAnchor.MiddleLeft,
                new Color(0.86f, 0.96f, 0.88f, 1f));
            _bannerText.text = "点击「野外勘测」扫描附近暗藏补给、捷径与传闻";

            _pingsText = CreateText(
                bar.transform,
                "PingsLabel",
                new Vector2(0.03f, 0.06f), new Vector2(0.97f, 0.62f),
                _font, 11, TextAnchor.UpperLeft,
                new Color(0.84f, 0.90f, 0.96f, 1f));
            _pingsText.text = string.Empty;
        }

        public static string ResolveFilterLabelZh(RpgWorld500xReconPingKind? filterKind)
        {
            if (!filterKind.HasValue)
            {
                return "全部分类";
            }

            switch (filterKind.Value)
            {
                case RpgWorld500xReconPingKind.HiddenCache:
                    return "暗藏补给";
                case RpgWorld500xReconPingKind.LockedShortcut:
                    return "未通捷径";
                case RpgWorld500xReconPingKind.UnheardRumorNpc:
                    return "未闻传闻";
                case RpgWorld500xReconPingKind.UninspectedStorySpot:
                    return "待查旧迹";
                case RpgWorld500xReconPingKind.AmbientEventZone:
                    return "环境事件";
                default:
                    return "全部分类";
            }
        }

        public bool TrySetFilterKind(RpgWorld500xReconPingKind? filterKind)
        {
            if (IsDisposed || _bootstrapper.IsDisposed)
            {
                return false;
            }

            _activeFilterKind = filterKind;
            return true;
        }

        /// <summary>
        /// Cooldown-free lookup for the nearest undiscovered target matching the active
        /// category filter. Returns false when disposed or when no filter is selected.
        /// </summary>
        public bool TryPreviewNearestForActiveFilter(
            out RpgWorld500xReconPing nearest,
            float maxRadiusTiles = 2000f)
        {
            nearest = default;
            if (IsDisposed || _bootstrapper.IsDisposed || !_activeFilterKind.HasValue)
            {
                return false;
            }

            RpgWorld500xGrandRuntimeFacade facade = _bootstrapper.Driver.Facade;
            return _scanner.TryFindNearestByKind(
                facade.CurrentMapId,
                facade.Session.Host.PlayerPosition,
                _activeFilterKind.Value,
                out nearest,
                maxRadiusTiles);
        }

        public void UpdateCooldownDisplay(float nowSeconds)
        {
            if (IsDisposed || float.IsNaN(nowSeconds) || float.IsInfinity(nowSeconds))
            {
                return;
            }

            _currentClockSeconds = nowSeconds;
            float remaining = _scanner.GetRemainingCooldown(nowSeconds);
            bool ready = remaining <= 0.0001f;
            _scanButton.interactable = ready;
            _scanButtonLabel.text = ready
                ? "野外勘测 [就绪]"
                : "冷却中 (" + Mathf.CeilToInt(remaining) + "s)";

            var img = _scanButton.GetComponent<Image>();
            if (img != null)
            {
                img.color = ready
                    ? new Color(0.18f, 0.36f, 0.44f, 0.95f)
                    : new Color(0.16f, 0.18f, 0.22f, 0.72f);
            }
        }

        public bool TryClickPulseScan(float nowSeconds)
        {
            if (IsDisposed || _bootstrapper.IsDisposed || float.IsNaN(nowSeconds) || float.IsInfinity(nowSeconds))
            {
                return false;
            }

            UpdateCooldownDisplay(nowSeconds);
            if (!IsScanButtonInteractable)
            {
                return false;
            }

            _scanButton.onClick.Invoke();
            return true;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _lastPings.Clear();
            DestroySafe(_rootObject);
        }

        private void HandleScanButtonClicked()
        {
            if (_bootstrapper.IsDisposed || !IsScanButtonInteractable)
            {
                return;
            }

            float scanTime = _currentClockSeconds;

            RpgWorld500xGrandRuntimeFacade facade = _bootstrapper.Driver.Facade;
            RpgWorldMapId mapId = facade.CurrentMapId;
            Vector2 playerPos = facade.Session.Host.PlayerPosition;
            RpgWorld500xAtmosphereSnapshot atmo = facade.Atmosphere.GetSnapshot(mapId);

            if (_scanner.TryPulseScan(
                mapId,
                playerPos,
                scanTime,
                out List<RpgWorld500xReconPing> pings,
                out string banner,
                visibilityMultiplier: atmo.PatrolDetectionMultiplier,
                maxResults: 3,
                filterKind: _activeFilterKind))
            {
                _lastPings.Clear();
                _lastPings.AddRange(pings);
                _bannerText.text = _activeFilterKind.HasValue
                    ? "[" + ResolveFilterLabelZh(_activeFilterKind) + "] " + banner
                    : banner;

                var sb = new StringBuilder(160);
                for (int i = 0; i < pings.Count; i++)
                {
                    if (i > 0)
                    {
                        sb.Append('\n');
                    }

                    sb.Append(i + 1).Append(". ").Append(pings[i].FormattedPingZh);
                }

                _pingsText.text = sb.ToString();
            }
            else
            {
                _bannerText.text = banner;
            }

            UpdateCooldownDisplay(scanTime);
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
            Action onClick,
            out Text labelText)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            go.GetComponent<Image>().color = new Color(0.18f, 0.36f, 0.44f, 0.95f);

            var btn = go.GetComponent<Button>();
            btn.interactable = true;
            if (onClick != null)
            {
                btn.onClick.AddListener(() => onClick());
            }

            labelText = CreateText(
                go.transform,
                "Text",
                Vector2.zero, Vector2.one,
                font, 11, TextAnchor.MiddleCenter,
                Color.white);
            labelText.text = label;
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