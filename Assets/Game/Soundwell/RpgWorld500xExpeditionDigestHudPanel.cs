using System;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Interactive UGUI Campfire Journal &amp; Expedition Digest modal panel bound to
    /// RpgWorld500xSceneBootstrapper and RpgWorld500xCampfireJournalDigestService.
    /// Displays live map/district context, weather risk severity banner, 4-region
    /// weather/reputation/milestone summary accessors, faction standing, Salt-Silver
    /// &amp; supply summary, map/global completion, active supply buffs, and prioritized
    /// next-objective guidance with zero-leak lifecycle.
    /// </summary>
    public sealed class RpgWorld500xExpeditionDigestHudPanel : IDisposable
    {
        private readonly RpgWorld500xSceneBootstrapper _bootstrapper;
        private readonly RpgWorld500xFieldSupplyUseService _supplyUseService;
        private readonly GameObject _rootObject;
        private readonly Text _headerText;
        private readonly Text _statusRibbonText;
        private readonly Image _riskBannerImage;
        private readonly Text _riskBannerText;
        private readonly Text _digestBodyText;
        private readonly Button _refreshButton;
        private readonly Button _closeButton;
        private readonly Font _font;
        private RpgWorld500xExpeditionDigest _lastDigest;
        private float _lastElapsedSeconds;
        private int _refreshCount;
        private bool _visible;
        private bool _disposed;

        public bool IsVisible => !IsDisposed && _visible;
        public bool IsDisposed => _disposed || _rootObject == null;
        public RpgWorld500xExpeditionDigest LastDigest => _lastDigest;
        public int HazardousRegionCount => _lastDigest.HazardousRegionCount;
        public string WorldWeatherBulletinText => _lastDigest.WorldWeatherBulletinZh ?? string.Empty;
        public string WorldReputationSummaryText => _lastDigest.WorldReputationSummaryZh ?? string.Empty;
        public string MilestoneSummaryText => _lastDigest.MilestoneSummaryZh ?? string.Empty;
        public float LastElapsedSeconds => _lastElapsedSeconds;
        public int RefreshCount => _refreshCount;
        public string HeaderText => _headerText != null ? _headerText.text : string.Empty;
        public string StatusRibbonText => _statusRibbonText != null ? _statusRibbonText.text : string.Empty;
        public string RiskBannerText => _riskBannerText != null ? _riskBannerText.text : string.Empty;
        public string DigestBodyText => _digestBodyText != null ? _digestBodyText.text : string.Empty;

        public RpgWorld500xExpeditionDigestHudPanel(
            Transform parent,
            RpgWorld500xSceneBootstrapper bootstrapper,
            RpgWorld500xFieldSupplyUseService supplyUseService = null)
        {
            _bootstrapper = bootstrapper ?? throw new ArgumentNullException(nameof(bootstrapper));
            _supplyUseService = supplyUseService;
            _font = ResolveUiFont();

            _rootObject = new GameObject("World500xExpeditionDigestHudPanel", typeof(RectTransform), typeof(Canvas));
            if (parent != null)
            {
                _rootObject.transform.SetParent(parent, false);
            }

            var canvas = _rootObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 72;
            canvas.pixelPerfect = true;

            var scaler = _rootObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            // Use the native 960x540 acceptance frame as the design size; avoid
            // rendering CJK text at fractional sub-12-pixel glyph sizes there.
            scaler.referenceResolution = new Vector2(960f, 540f);

            GameObject modal = CreateBox(
                _rootObject.transform,
                "DigestModalBox",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(900f, 510f),
                new Color(0.08f, 0.09f, 0.12f, 0.96f));

            _headerText = CreateText(
                modal.transform,
                "HeaderLabel",
                new Vector2(0.04f, 0.90f), new Vector2(0.96f, 0.97f),
                _font, 20, TextAnchor.MiddleLeft,
                new Color(0.97f, 0.89f, 0.62f, 1f));

            _statusRibbonText = CreateText(
                modal.transform,
                "StatusRibbonLabel",
                new Vector2(0.04f, 0.82f), new Vector2(0.96f, 0.89f),
                _font, 16, TextAnchor.MiddleLeft,
                new Color(0.84f, 0.92f, 0.96f, 1f));

            GameObject riskBox = CreateBox(
                modal.transform,
                "RiskBannerBox",
                new Vector2(0.04f, 0.72f), new Vector2(0.96f, 0.81f), new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero,
                new Color(0.14f, 0.28f, 0.20f, 0.92f));
            var riskRect = riskBox.GetComponent<RectTransform>();
            riskRect.offsetMin = Vector2.zero;
            riskRect.offsetMax = Vector2.zero;
            _riskBannerImage = riskBox.GetComponent<Image>();

            _riskBannerText = CreateText(
                riskBox.transform,
                "RiskBannerLabel",
                new Vector2(0.03f, 0.08f), new Vector2(0.97f, 0.92f),
                _font, 16, TextAnchor.MiddleLeft,
                Color.white);

            GameObject bodyBox = CreateBox(
                modal.transform,
                "DigestBodyBox",
                new Vector2(0.04f, 0.145f), new Vector2(0.96f, 0.71f), new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero,
                new Color(0.11f, 0.13f, 0.17f, 0.92f));
            var bodyRect = bodyBox.GetComponent<RectTransform>();
            bodyRect.offsetMin = Vector2.zero;
            bodyRect.offsetMax = Vector2.zero;

            _digestBodyText = CreateText(
                bodyBox.transform,
                "DigestBodyLabel",
                new Vector2(0.025f, 0.035f), new Vector2(0.975f, 0.965f),
                _font, 16, TextAnchor.UpperLeft,
                new Color(0.94f, 0.95f, 0.97f, 1f));

            _refreshButton = CreateActionButton(
                modal.transform,
                "RefreshDigestButton",
                new Vector2(0.04f, 0.025f), new Vector2(0.48f, 0.125f),
                "刷新营火手记",
                _font,
                () =>
                {
                    if (!CanClick(_refreshButton))
                    {
                        return;
                    }

                    Refresh(_lastElapsedSeconds);
                });

            _closeButton = CreateActionButton(
                modal.transform,
                "CloseDigestButton",
                new Vector2(0.52f, 0.025f), new Vector2(0.96f, 0.125f),
                "合上手记",
                _font,
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

            _lastElapsedSeconds = elapsedSeconds;
            _lastDigest = RpgWorld500xCampfireJournalDigestService.BuildDigest(
                _bootstrapper,
                elapsedSeconds,
                _supplyUseService);
            _refreshCount++;

            _headerText.text = "【远征营火手记】" + _lastDigest.MapDisplayNameZh + " · " + _lastDigest.DistrictDisplayNameZh;
            _statusRibbonText.text = "第 " + _lastDigest.DayCount + " 日 " + _lastDigest.FormattedClock +
                                     "｜" + _lastDigest.WeatherDisplayNameZh +
                                     "｜" + _lastDigest.FactionNameZh + "「" + _lastDigest.ReputationRankZh + "」" +
                                     "｜盐银 " + _lastDigest.SaltSilverBalance +
                                     "｜随身补给 " + _lastDigest.TotalOwnedSupplyItems + " 件";

            _riskBannerImage.color = ResolveRiskColor(_lastDigest.TravelRiskLevel);
            _riskBannerText.text = ResolveRiskLabelZh(_lastDigest.TravelRiskLevel) +
                                   "｜本图堪舆 " + _lastDigest.ActiveMapCompletionPercent + "%" +
                                   "｜四境总进度 " + _lastDigest.GlobalWorldCompletionPercent + "%" +
                                   "｜待领里程碑 " + _lastDigest.ClaimableMilestoneCount + " 项" +
                                   "｜高危区域 " + _lastDigest.HazardousRegionCount + " 境";

            _digestBodyText.text = _lastDigest.FormattedDigestZh;
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

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _visible = false;
            DestroySafe(_rootObject);
        }

        private bool CanClick(Button button)
        {
            return IsVisible && !_bootstrapper.IsDisposed && button != null &&
                button.isActiveAndEnabled && button.IsInteractable();
        }

        private static Color ResolveRiskColor(RpgWorld500xTravelRiskLevel level)
        {
            switch (level)
            {
                case RpgWorld500xTravelRiskLevel.Hazardous:
                    return new Color(0.50f, 0.16f, 0.16f, 0.94f);
                case RpgWorld500xTravelRiskLevel.Caution:
                    return new Color(0.46f, 0.34f, 0.14f, 0.94f);
                default:
                    return new Color(0.14f, 0.32f, 0.22f, 0.94f);
            }
        }

        private static string ResolveRiskLabelZh(RpgWorld500xTravelRiskLevel level)
        {
            switch (level)
            {
                case RpgWorld500xTravelRiskLevel.Hazardous:
                    return "行旅风险：高危";
                case RpgWorld500xTravelRiskLevel.Caution:
                    return "行旅风险：谨慎";
                default:
                    return "行旅风险：平稳";
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
            text.raycastTarget = false;
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
                font, 16, TextAnchor.MiddleCenter,
                Color.white);
            txt.text = label;
            return btn;
        }

        private static Font ResolveUiFont()
        {
            // This panel presents Chinese text regardless of the story UI language.
            // Use the same pinned OFL font as Soundwell localization rather than
            // the Latin-only builtin font (blank CJK glyphs in CI GameView).
            Font font = Resources.Load<Font>("Soundwell/Fonts/SoundwellChinese");
            if (font == null || !font.HasCharacter('远') ||
                !font.HasCharacter('险') || !font.HasCharacter('境'))
            {
                throw new InvalidOperationException("The pinned Chinese font is missing or lacks expedition digest glyphs.");
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
