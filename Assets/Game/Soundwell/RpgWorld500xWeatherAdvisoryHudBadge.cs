using System;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Compact non-modal UGUI Weather &amp; Ambient Window Advisory badge bound to
    /// RpgWorld500xSceneBootstrapper and RpgWorld500xWeatherTravelAdvisoryService.
    /// Renders color-coded travel risk status, tactical advisory text, next phase
    /// countdown, 4-region weather digest readout, and nearest ambient micro-event
    /// window under a zero-leak lifecycle.
    /// </summary>
    public sealed class RpgWorld500xWeatherAdvisoryHudBadge : IDisposable
    {
        private readonly RpgWorld500xSceneBootstrapper _bootstrapper;
        private readonly GameObject _rootObject;
        private readonly Image _riskPillImage;
        private readonly Text _riskPillText;
        private readonly Text _advisoryText;
        private readonly Text _ambientForecastText;
        private readonly Font _font;
        private RpgWorld500xTravelRiskLevel _lastRiskLevel;
        private bool _lastRecommendCampfireRest;
        private RpgWorld500xWorldWeatherDigest _lastWorldDigest;
        private bool _disposed;

        public bool IsDisposed => _disposed || _rootObject == null;
        public RpgWorld500xTravelRiskLevel LastRiskLevel => _lastRiskLevel;
        public bool LastRecommendCampfireRest => _lastRecommendCampfireRest;
        public RpgWorld500xWorldWeatherDigest LastWorldDigest => _lastWorldDigest;
        public int HazardousRegionCount => _lastWorldDigest.HazardousRegionCount;
        public RpgWorldMapId SafestRegionMapId => _lastWorldDigest.SafestMapId;
        public string WorldBulletinText => _lastWorldDigest.WorldBulletinZh ?? string.Empty;
        public string RiskLabelText => _riskPillText != null ? _riskPillText.text : string.Empty;
        public string AdvisoryText => _advisoryText != null ? _advisoryText.text : string.Empty;
        public string AmbientForecastText => _ambientForecastText != null ? _ambientForecastText.text : string.Empty;

        public RpgWorld500xWeatherAdvisoryHudBadge(
            Transform parent,
            RpgWorld500xSceneBootstrapper bootstrapper)
        {
            _bootstrapper = bootstrapper ?? throw new ArgumentNullException(nameof(bootstrapper));
            _font = ResolveUiFont();

            _rootObject = new GameObject("World500xWeatherAdvisoryHudBadge", typeof(RectTransform), typeof(Canvas));
            if (parent != null)
            {
                _rootObject.transform.SetParent(parent, false);
            }

            var canvas = _rootObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 57;

            var scaler = _rootObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);

            GameObject card = CreateBox(
                _rootObject.transform,
                "WeatherAdvisoryCard",
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-16f, 16f), new Vector2(440f, 96f),
                new Color(0.07f, 0.09f, 0.13f, 0.88f));

            GameObject pill = CreateBox(
                card.transform,
                "RiskPill",
                new Vector2(0.03f, 0.64f), new Vector2(0.24f, 0.90f), new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero,
                new Color(0.16f, 0.34f, 0.24f, 0.96f));
            var pillRect = pill.GetComponent<RectTransform>();
            pillRect.offsetMin = Vector2.zero;
            pillRect.offsetMax = Vector2.zero;
            _riskPillImage = pill.GetComponent<Image>();

            _riskPillText = CreateText(
                pill.transform,
                "RiskLabel",
                Vector2.zero, Vector2.one,
                _font, 11, TextAnchor.MiddleCenter,
                Color.white);

            _advisoryText = CreateText(
                card.transform,
                "AdvisoryLabel",
                new Vector2(0.26f, 0.54f), new Vector2(0.97f, 0.94f),
                _font, 11, TextAnchor.MiddleLeft,
                new Color(0.92f, 0.95f, 0.98f, 1f));

            _ambientForecastText = CreateText(
                card.transform,
                "AmbientForecastLabel",
                new Vector2(0.03f, 0.08f), new Vector2(0.97f, 0.50f),
                _font, 11, TextAnchor.MiddleLeft,
                new Color(0.78f, 0.88f, 0.96f, 1f));

            Refresh(0f);
        }

        public void Refresh(float elapsedSeconds = 0f)
        {
            if (IsDisposed || _bootstrapper.IsDisposed)
            {
                return;
            }

            RpgWorld500xGrandRuntimeFacade facade = _bootstrapper.Driver.Facade;
            _lastWorldDigest =
                RpgWorld500xWeatherTravelAdvisoryService.BuildWorldWeatherDigest(facade.Atmosphere, elapsedSeconds);

            // Refresh runs every input frame. Reuse the current region's report
            // rather than rebuilding its snapshot, advisory text and ambient windows.
            RpgWorld500xTravelAdvisoryReport report = default;
            bool foundCurrentRegion = false;
            for (int i = 0; i < _lastWorldDigest.RegionReports.Count; i++)
            {
                RpgWorld500xTravelAdvisoryReport candidate = _lastWorldDigest.RegionReports[i];
                if (candidate.MapId != facade.CurrentMapId)
                {
                    continue;
                }

                report = candidate;
                foundCurrentRegion = true;
                break;
            }

            // Preserve the original behavior if a future map is outside the digest.
            if (!foundCurrentRegion)
            {
                report = RpgWorld500xWeatherTravelAdvisoryService.BuildAdvisory(
                    facade.Atmosphere.GetSnapshot(facade.CurrentMapId), elapsedSeconds);
            }

            _lastRiskLevel = report.RiskLevel;
            _lastRecommendCampfireRest = report.RecommendCampfireRest;
            _riskPillText.text = report.RiskLabelZh;
            _riskPillImage.color = ResolveRiskColor(report.RiskLevel);
            _advisoryText.text = report.TacticalAdvisoryZh;

            if (report.AmbientWindows.Count > 0)
            {
                RpgWorld500xAmbientWindowForecast best = report.AmbientWindows[0];
                for (int i = 1; i < report.AmbientWindows.Count; i++)
                {
                    RpgWorld500xAmbientWindowForecast cand = report.AmbientWindows[i];
                    if ((cand.IsCurrentlyActive && !best.IsCurrentlyActive) ||
                        (cand.IsCurrentlyActive == best.IsCurrentlyActive &&
                         cand.SecondsUntilNextChange < best.SecondsUntilNextChange))
                    {
                        best = cand;
                    }
                }

                int sec = Mathf.CeilToInt(best.SecondsUntilNextChange);
                string stateText = best.IsCurrentlyActive
                    ? "异象激活中（剩余 " + sec + "s）："
                    : "下次异象（" + sec + "s 后）：";
                _ambientForecastText.text = stateText + best.CaptionZh;
            }
            else
            {
                _ambientForecastText.text = "当前区域无周期环境异象";
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            DestroySafe(_rootObject);
        }

        private static Color ResolveRiskColor(RpgWorld500xTravelRiskLevel level)
        {
            switch (level)
            {
                case RpgWorld500xTravelRiskLevel.Hazardous:
                    return new Color(0.56f, 0.16f, 0.16f, 0.96f);
                case RpgWorld500xTravelRiskLevel.Caution:
                    return new Color(0.50f, 0.36f, 0.12f, 0.96f);
                case RpgWorld500xTravelRiskLevel.StealthAdvantage:
                    return new Color(0.14f, 0.36f, 0.44f, 0.96f);
                case RpgWorld500xTravelRiskLevel.Safe:
                default:
                    return new Color(0.16f, 0.36f, 0.24f, 0.96f);
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
