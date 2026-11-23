using System;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Lightweight, zero-leak UGUI status overlay HUD bound to RpgWorld500xRuntimeDriver.
    /// Displays active district, 24h clock & regional weather, nearest 8-direction waypoint
    /// guidance, Salt-Silver wallet balance, map completion percentage, and latest toast banner.
    /// </summary>
    public sealed class RpgWorld500xStatusOverlayHud : IDisposable
    {
        private readonly GameObject _rootObject;
        private readonly Text _environmentText;
        private readonly Text _waypointAndEconomyText;
        private readonly Text _toastText;
        private readonly GameObject _toastPanel;
        private bool _disposed;

        public bool IsDisposed => _disposed || _rootObject == null;
        public string EnvironmentReadout => _environmentText != null ? _environmentText.text : string.Empty;
        public string WaypointAndEconomyReadout => _waypointAndEconomyText != null ? _waypointAndEconomyText.text : string.Empty;
        public string ToastReadout => _toastText != null ? _toastText.text : string.Empty;
        public bool IsToastVisible => _toastPanel != null && _toastPanel.activeSelf;

        public RpgWorld500xStatusOverlayHud(Transform parent)
        {
            _rootObject = new GameObject("World500xStatusOverlayHud");
            if (parent != null)
            {
                _rootObject.transform.SetParent(parent, false);
            }

            var canvas = _rootObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 25;

            var scaler = _rootObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);

            Font font = ResolveUiFont();

            GameObject leftPanel = CreatePanel(
                _rootObject.transform,
                "EnvironmentStatusPanel",
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(16f, -16f), new Vector2(440f, 56f),
                new Color(0.08f, 0.10f, 0.13f, 0.82f));
            _environmentText = CreateLabel(leftPanel.transform, "EnvironmentLabel", font, 14, TextAnchor.MiddleLeft);

            GameObject rightPanel = CreatePanel(
                _rootObject.transform,
                "WaypointEconomyPanel",
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-16f, -16f), new Vector2(460f, 56f),
                new Color(0.08f, 0.10f, 0.13f, 0.82f));
            _waypointAndEconomyText = CreateLabel(rightPanel.transform, "WaypointEconomyLabel", font, 14, TextAnchor.MiddleRight);

            _toastPanel = CreatePanel(
                _rootObject.transform,
                "ToastBannerPanel",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 20f), new Vector2(540f, 42f),
                new Color(0.14f, 0.11f, 0.08f, 0.90f));
            _toastText = CreateLabel(_toastPanel.transform, "ToastLabel", font, 14, TextAnchor.MiddleCenter);
            _toastPanel.SetActive(false);
        }

        public bool SyncFromDriver(RpgWorld500xRuntimeDriver driver)
        {
            if (IsDisposed || driver == null || !driver.IsInitialized)
            {
                return false;
            }

            RpgWorld500xGrandRuntimeFacade facade = driver.Facade;
            RpgWorld500xMapCompletionSummary summary = facade.GetCompletionSummary();

            string districtName = string.Empty;
            Vector2 pos = facade.Session.Host.PlayerPosition;
            var tile = new Vector2Int(Mathf.FloorToInt(pos.x), Mathf.FloorToInt(pos.y));
            if (RpgWorld500xDistrictCatalog.TryResolveDistrictAtTile(facade.CurrentMapId, tile, out RpgWorld500xDistrictDef dist))
            {
                districtName = dist.DisplayNameZh;
            }

            string envCaption = driver.LastEnvironmentCaptionZh;
            if (string.IsNullOrEmpty(envCaption))
            {
                RpgWorld500xAtmosphereSnapshot snap = facade.Atmosphere.GetSnapshot(facade.CurrentMapId);
                envCaption = snap.FormattedClock + " " + snap.PhaseDisplayName + " · " + snap.WeatherDisplayName;
            }

            _environmentText.text = string.IsNullOrEmpty(districtName)
                ? envCaption
                : districtName + "\n" + envCaption;

            int silver = facade.BountyAndSupply.SaltSilverBalance;
            string wp = string.IsNullOrEmpty(driver.LastNearestWaypointReadoutZh)
                ? "暂无附近航点"
                : driver.LastNearestWaypointReadoutZh;
            _waypointAndEconomyText.text = wp + "\n盐银 " + silver + " · 探索度 " + summary.OverallCompletionPercent + "%";

            if (driver.RecentToasts.Count > 0)
            {
                _toastText.text = driver.RecentToasts[driver.RecentToasts.Count - 1];
                _toastPanel.SetActive(true);
            }
            else
            {
                _toastText.text = string.Empty;
                _toastPanel.SetActive(false);
            }

            return true;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (_rootObject != null)
            {
                if (Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(_rootObject);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(_rootObject);
                }
            }
        }

        private static GameObject CreatePanel(
            Transform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPos,
            Vector2 size,
            Color backgroundColor)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;

            var img = go.AddComponent<Image>();
            img.color = backgroundColor;
            return go;
        }

        private static Text CreateLabel(Transform parent, string name, Font font, int fontSize, TextAnchor alignment)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(10f, 4f);
            rect.offsetMax = new Vector2(-10f, -4f);

            var text = go.AddComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = new Color(0.95f, 0.93f, 0.87f, 1f);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
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
    }
}
