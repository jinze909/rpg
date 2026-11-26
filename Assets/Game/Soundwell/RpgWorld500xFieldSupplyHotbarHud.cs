using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Compact non-modal UGUI Field Supply Hotbar bound to RpgWorld500xSceneBootstrapper
    /// and RpgWorld500xFieldSupplyUseService. Renders all 6 camp supplies with live
    /// stock counts, one-click field consumption, active timed buff countdown strip,
    /// read-only inventory snapshot readouts, parent CanvasGroup interactability guards,
    /// and zero-leak IDisposable cleanup.
    /// </summary>
    public sealed class RpgWorld500xFieldSupplyHotbarHud : IDisposable
    {
        private readonly RpgWorld500xSceneBootstrapper _bootstrapper;
        private readonly RpgWorld500xFieldSupplyUseService _supplyUseService;
        private readonly Func<float> _clockSecondsProvider;
        private readonly GameObject _rootObject;
        private readonly Text _feedbackText;
        private readonly Text _activeBuffsText;
        private readonly Font _font;
        private readonly Dictionary<string, Button> _slotButtons =
            new Dictionary<string, Button>(StringComparer.Ordinal);
        private readonly Dictionary<string, Text> _slotLabels =
            new Dictionary<string, Text>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _shortNameById =
            new Dictionary<string, string>(StringComparer.Ordinal);
        private RpgWorld500xFieldSupplyInventorySummary _lastInventorySnapshot;
        private bool _disposed;

        public bool IsDisposed => _disposed || _rootObject == null;
        public RpgWorld500xFieldSupplyUseService SupplyUseService => _supplyUseService;
        public RpgWorld500xFieldSupplyInventorySummary LastInventorySnapshot => _lastInventorySnapshot;
        public int TotalOwnedSupplyCount => _lastInventorySnapshot.TotalOwnedItems;
        public int DistinctOwnedSupplyKinds => _lastInventorySnapshot.DistinctOwnedKinds;
        public int ActiveBuffCount => _lastInventorySnapshot.ActiveBuffCount;
        public string InventorySummaryText => _lastInventorySnapshot.SummaryZh;
        public int DisplayedSlotCount => _slotButtons.Count;
        public string FeedbackText => _feedbackText != null ? _feedbackText.text : string.Empty;
        public string ActiveBuffsText => _activeBuffsText != null ? _activeBuffsText.text : string.Empty;

        public RpgWorld500xFieldSupplyHotbarHud(
            Transform parent,
            RpgWorld500xSceneBootstrapper bootstrapper,
            RpgWorld500xFieldSupplyUseService supplyUseService = null,
            Func<float> clockSecondsProvider = null)
        {
            _bootstrapper = bootstrapper ?? throw new ArgumentNullException(nameof(bootstrapper));
            // A standalone hotbar must consume from the same authoritative
            // inventory that the merchant, world HUD and saved game use.
            _supplyUseService = supplyUseService ?? _bootstrapper.CreateFieldSupplyUseService();
            _clockSecondsProvider = clockSecondsProvider ?? (() => Time.unscaledTime);
            _font = ResolveUiFont();

            _rootObject = new GameObject("World500xFieldSupplyHotbarHud", typeof(RectTransform), typeof(Canvas));
            if (parent != null)
            {
                _rootObject.transform.SetParent(parent, false);
            }

            var canvas = _rootObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 59;

            var scaler = _rootObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);

            GameObject bar = CreateBox(
                _rootObject.transform,
                "SupplyHotbarPanel",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 16f), new Vector2(540f, 94f),
                new Color(0.07f, 0.09f, 0.13f, 0.88f));

            _activeBuffsText = CreateText(
                bar.transform,
                "ActiveBuffsLabel",
                new Vector2(0.03f, 0.72f), new Vector2(0.97f, 0.96f),
                _font, 11, TextAnchor.MiddleLeft,
                new Color(0.96f, 0.88f, 0.56f, 1f));

            _feedbackText = CreateText(
                bar.transform,
                "FeedbackLabel",
                new Vector2(0.03f, 0.50f), new Vector2(0.97f, 0.72f),
                _font, 11, TextAnchor.MiddleLeft,
                new Color(0.84f, 0.92f, 0.96f, 1f));
            _feedbackText.text = "野外补给快捷栏：点击已持有补给品立即生效";

            BuildSupplySlots(bar.transform);
            Refresh(0f);
        }

        public bool HasUsableSupply(string itemId)
        {
            return !IsDisposed && _supplyUseService.HasUsableSupply(itemId);
        }

        public bool TryClickUseSupply(string itemId, float nowSeconds)
        {
            if (IsDisposed || _bootstrapper.IsDisposed || string.IsNullOrEmpty(itemId))
            {
                return false;
            }

            Refresh(nowSeconds);
            return TryUseSupply(itemId, nowSeconds);
        }

        public void Refresh(float nowSeconds)
        {
            if (IsDisposed || _bootstrapper.IsDisposed)
            {
                return;
            }

            _lastInventorySnapshot = _supplyUseService.BuildInventorySnapshot(nowSeconds);

            IReadOnlyList<RpgWorld500xCampSupplyItemDef> supplies =
                RpgWorld500xBountyAndSupplyCatalog.GetCampSupplies();

            for (int i = 0; i < supplies.Count; i++)
            {
                RpgWorld500xCampSupplyItemDef item = supplies[i];
                int owned = _supplyUseService.GetOwnedCount(item.Id);
                bool hasStock = owned > 0;

                if (_slotButtons.TryGetValue(item.Id, out Button btn) && btn != null)
                {
                    btn.interactable = hasStock;
                    var img = btn.GetComponent<Image>();
                    if (img != null)
                    {
                        img.color = hasStock
                            ? new Color(0.18f, 0.30f, 0.40f, 0.95f)
                            : new Color(0.14f, 0.15f, 0.19f, 0.70f);
                    }
                }

                if (_slotLabels.TryGetValue(item.Id, out Text lbl) && lbl != null)
                {
                    string shortName = _shortNameById.TryGetValue(item.Id, out string sn) ? sn : item.DisplayNameZh;
                    lbl.text = (i + 1) + ":" + shortName + " ×" + owned;
                    lbl.color = hasStock ? Color.white : new Color(0.55f, 0.57f, 0.62f, 1f);
                }
            }

            _activeBuffsText.text = _lastInventorySnapshot.ActiveBuffCount > 0
                ? "生效增益：" + _lastInventorySnapshot.ActiveBuffSummaryZh
                : "生效增益：无";
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _slotButtons.Clear();
            _slotLabels.Clear();
            DestroySafe(_rootObject);
        }

        private void BuildSupplySlots(Transform parent)
        {
            IReadOnlyList<RpgWorld500xCampSupplyItemDef> supplies =
                RpgWorld500xBountyAndSupplyCatalog.GetCampSupplies();
            float slotWidth = supplies.Count > 0 ? 0.94f / supplies.Count : 0.94f;

            for (int i = 0; i < supplies.Count; i++)
            {
                RpgWorld500xCampSupplyItemDef item = supplies[i];
                _shortNameById[item.Id] = ResolveShortNameZh(item.Id, item.DisplayNameZh);

                float xMin = 0.03f + (i * slotWidth) + 0.004f;
                float xMax = 0.03f + ((i + 1) * slotWidth) - 0.004f;

                var go = new GameObject("SupplySlot_" + item.Id, typeof(RectTransform), typeof(Image), typeof(Button));
                go.transform.SetParent(parent, false);
                var rect = go.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(xMin, 0.08f);
                rect.anchorMax = new Vector2(xMax, 0.46f);
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;

                var btn = go.GetComponent<Button>();
                string capturedId = item.Id;
                btn.onClick.AddListener(() => TryUseSupply(capturedId, _clockSecondsProvider()));

                Text lbl = CreateText(
                    go.transform,
                    "Label",
                    Vector2.zero, Vector2.one,
                    _font, 11, TextAnchor.MiddleCenter,
                    Color.white);

                _slotButtons[item.Id] = btn;
                _slotLabels[item.Id] = lbl;
            }
        }

        private bool TryUseSupply(string itemId, float now)
        {
            if (IsDisposed || _bootstrapper.IsDisposed ||
                !_slotButtons.TryGetValue(itemId, out Button btn) || btn == null ||
                !btn.isActiveAndEnabled || !btn.gameObject.activeInHierarchy || !btn.IsInteractable())
            {
                return false;
            }

            RpgWorld500xFieldSupplyUseResult res = _supplyUseService.TryConsumeSupply(itemId, now);
            _feedbackText.text = res.FeedbackBannerZh;
            if (res.Succeeded)
            {
                _bootstrapper.StatusHud.SyncFromDriver(_bootstrapper.Driver);
            }

            Refresh(now);
            return res.Succeeded;
        }

        private static string ResolveShortNameZh(string itemId, string fallback)
        {
            switch (itemId)
            {
                case "gauze": return "纱布";
                case "rations": return "干粮";
                case "lamp_oil": return "灯油";
                case "damp_cloth": return "湿布";
                case "flare": return "信号弹";
                case "iron_clamps": return "铁件";
                default: return fallback;
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
