using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Compact non-modal UGUI Quick-Launch Menu Dock &amp; Hotkey Strip bound directly
    /// to RpgWorld500xMenuRouter. Exposes all 16 500x world modals with their
    /// corresponding hardware hotkey hints (J/M/B/K/T/N/O/L/U/C/H/V/P/X/G/Y),
    /// active-modal highlight syncing, parent CanvasGroup interactability guards,
    /// and zero-leak IDisposable cleanup.
    /// </summary>
    public sealed class RpgWorld500xQuickMenuDockHud : IDisposable
    {
        private readonly struct DockEntry
        {
            public readonly RpgWorld500xModalKind Kind;
            public readonly string ShortLabelZh;
            public readonly string HotkeyTag;

            public DockEntry(RpgWorld500xModalKind kind, string shortLabelZh, string hotkeyTag)
            {
                Kind = kind;
                ShortLabelZh = shortLabelZh;
                HotkeyTag = hotkeyTag;
            }
        }

        private static readonly DockEntry[] Entries =
        {
            new DockEntry(RpgWorld500xModalKind.QuestJournal, "日志", "J"),
            new DockEntry(RpgWorld500xModalKind.WorldAtlas, "图志", "M"),
            new DockEntry(RpgWorld500xModalKind.CampMerchant, "营商", "B"),
            new DockEntry(RpgWorld500xModalKind.Bestiary, "图鉴", "K"),
            new DockEntry(RpgWorld500xModalKind.FastTravelBeacons, "信标", "T"),
            new DockEntry(RpgWorld500xModalKind.CartographyReputation, "堪舆", "N"),
            new DockEntry(RpgWorld500xModalKind.SaveSlots, "存档", "O"),
            new DockEntry(RpgWorld500xModalKind.FieldRumorAndLore, "传闻", "L"),
            new DockEntry(RpgWorld500xModalKind.ExpeditionMilestones, "远征", "U"),
            new DockEntry(RpgWorld500xModalKind.CacheAndShortcut, "秘藏", "C"),
            new DockEntry(RpgWorld500xModalKind.ExpeditionDigest, "手记", "H"),
            new DockEntry(RpgWorld500xModalKind.BountyBoard, "悬赏", "V"),
            new DockEntry(RpgWorld500xModalKind.ExpeditionReadiness, "整备", "P"),
            new DockEntry(RpgWorld500xModalKind.DangerTacticalBrief, "战备", "X"),
            new DockEntry(RpgWorld500xModalKind.WorldGazetteer, "总录", "G"),
            new DockEntry(RpgWorld500xModalKind.TravelItinerary, "行程", "Y")
        };

        private readonly RpgWorld500xMenuRouter _menuRouter;
        private readonly GameObject _rootObject;
        private readonly Dictionary<RpgWorld500xModalKind, Button> _buttons =
            new Dictionary<RpgWorld500xModalKind, Button>();
        private readonly Font _font;
        private RpgWorld500xModalKind _activeHighlightedModal = RpgWorld500xModalKind.None;
        private bool _disposed;

        public bool IsDisposed => _disposed || _rootObject == null;
        public int DisplayedButtonCount => _buttons.Count;
        public RpgWorld500xModalKind ActiveHighlightedModal => _activeHighlightedModal;

        public RpgWorld500xQuickMenuDockHud(
            Transform parent,
            RpgWorld500xMenuRouter menuRouter)
        {
            _menuRouter = menuRouter ?? throw new ArgumentNullException(nameof(menuRouter));
            _font = ResolveUiFont();

            _rootObject = new GameObject("World500xQuickMenuDockHud", typeof(RectTransform), typeof(Canvas));
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

            GameObject dock = CreateBox(
                _rootObject.transform,
                "QuickMenuDockPanel",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -10f), new Vector2(880f, 54f),
                new Color(0.07f, 0.09f, 0.13f, 0.88f));

            const int cols = 8;
            const int rows = 2;
            float cellW = 0.98f / cols;
            float cellH = 0.88f / rows;

            for (int i = 0; i < Entries.Length; i++)
            {
                DockEntry entry = Entries[i];
                int row = i / cols;
                int col = i % cols;

                float xMin = 0.01f + (col * cellW) + 0.003f;
                float xMax = 0.01f + ((col + 1) * cellW) - 0.003f;
                float yMax = 0.94f - (row * cellH) - 0.02f;
                float yMin = 0.94f - ((row + 1) * cellH) + 0.02f;

                RpgWorld500xModalKind captured = entry.Kind;
                Button btn = CreateActionButton(
                    dock.transform,
                    "DockBtn_" + entry.Kind,
                    new Vector2(xMin, yMin), new Vector2(xMax, yMax),
                    entry.ShortLabelZh + "[" + entry.HotkeyTag + "]",
                    _font,
                    () => HandleDockButtonClicked(captured));
                _buttons[entry.Kind] = btn;
            }

            RefreshActiveHighlight();
        }

        public bool TryClickModalButton(RpgWorld500xModalKind kind)
        {
            if (IsDisposed || _menuRouter.IsDisposed)
            {
                return false;
            }

            if (!_buttons.TryGetValue(kind, out Button btn) ||
                btn == null || !btn.interactable || !btn.IsInteractable())
            {
                return false;
            }

            btn.onClick.Invoke();
            return true;
        }

        public void RefreshActiveHighlight()
        {
            if (IsDisposed || _menuRouter.IsDisposed)
            {
                return;
            }

            _activeHighlightedModal = _menuRouter.ActiveModal;
            foreach (KeyValuePair<RpgWorld500xModalKind, Button> kv in _buttons)
            {
                Button btn = kv.Value;
                if (btn == null)
                {
                    continue;
                }

                var img = btn.GetComponent<Image>();
                if (img != null)
                {
                    img.color = kv.Key == _activeHighlightedModal
                        ? new Color(0.28f, 0.44f, 0.58f, 0.96f)
                        : new Color(0.14f, 0.17f, 0.23f, 0.88f);
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
            _buttons.Clear();
            DestroySafe(_rootObject);
        }

        private void HandleDockButtonClicked(RpgWorld500xModalKind kind)
        {
            if (IsDisposed || _menuRouter.IsDisposed)
            {
                return;
            }

            _menuRouter.ToggleModal(kind);
            RefreshActiveHighlight();
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
            go.GetComponent<Image>().color = new Color(0.14f, 0.17f, 0.23f, 0.88f);

            var btn = go.GetComponent<Button>();
            btn.interactable = true;
            if (onClick != null)
            {
                btn.onClick.AddListener(() => onClick());
            }

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(go.transform, false);
            var textRect = textGo.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var txt = textGo.GetComponent<Text>();
            txt.font = font;
            txt.fontSize = 11;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;
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
