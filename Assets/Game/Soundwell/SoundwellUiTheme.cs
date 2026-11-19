using System;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Shared UGUI factory, palette, viewport-aspect scaler, and safe canvas
    /// teardown helper for M00-M04 narrative/preparation views.
    /// Keeps UGUI as the single UI system without introducing any state or save authority.
    /// </summary>
    public static class SoundwellUiTheme
    {
        public const float ReferenceWidth = 960f;
        public const float ReferenceHeight = 540f;

        public static readonly Color DefaultBackdropColor = new Color(0.04f, 0.07f, 0.08f, 0.98f);
        public static readonly Color DefaultPaperColor = new Color(0.92f, 0.89f, 0.81f, 1f);
        public static readonly Color DefaultInkColor = new Color(0.12f, 0.17f, 0.20f, 1f);
        public static readonly Color DefaultButtonColor = new Color(0.82f, 0.83f, 0.77f, 1f);

        public static GameObject CreateOverlayCanvas(
            Transform parent,
            string canvasName,
            int sortingOrder,
            out RectTransform authoredPanel,
            Color? backdropColor = null,
            Color? paperColor = null)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (string.IsNullOrWhiteSpace(canvasName))
                throw new ArgumentException("Canvas name is required.", nameof(canvasName));

            var canvasObject = new GameObject(canvasName,
                typeof(RectTransform), typeof(Canvas));
            canvasObject.transform.SetParent(parent, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();

            CreatePanel(canvas.transform, canvasName + " backdrop",
                new Vector2(ReferenceWidth - 10f, ReferenceHeight - 10f),
                backdropColor ?? DefaultBackdropColor);

            var page = new GameObject(canvasName + " panel", typeof(RectTransform));
            page.transform.SetParent(canvas.transform, false);
            authoredPanel = (RectTransform)page.transform;
            authoredPanel.anchorMin = authoredPanel.anchorMax = new Vector2(0.5f, 0.5f);
            authoredPanel.sizeDelta = new Vector2(ReferenceWidth, ReferenceHeight);

            CreatePanel(authoredPanel, canvasName + " paper",
                new Vector2(ReferenceWidth - 40f, ReferenceHeight - 25f),
                paperColor ?? DefaultPaperColor);

            return canvasObject;
        }

        public static Image CreatePanel(Transform parent, string name, Vector2 size, Color color)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            var item = new GameObject(name, typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Image));
            item.transform.SetParent(parent, false);
            var graphic = item.GetComponent<Image>();
            graphic.color = color;
            graphic.raycastTarget = false;
            var rect = graphic.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            return graphic;
        }

        public static Text CreateLabel(
            Transform parent,
            int fontSize,
            Vector2 position,
            Vector2 dimensions,
            Color? color = null)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            var result = SoundwellView.AddLabel(parent, "", fontSize,
                color ?? DefaultInkColor, position, dimensions);
            result.resizeTextForBestFit = true;
            result.resizeTextMinSize = 10;
            result.resizeTextMaxSize = fontSize;
            result.raycastTarget = false;
            return result;
        }

        public static Button CreateButton(
            Transform parent,
            string name,
            Vector2 position,
            Vector2 size,
            int fontSize,
            out Text label,
            Color? buttonColor = null,
            Color? textColor = null)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            var obj = new GameObject(name, typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Image), typeof(Button));
            obj.transform.SetParent(parent, false);
            var image = obj.GetComponent<Image>();
            image.color = buttonColor ?? DefaultButtonColor;
            var rect = (RectTransform)obj.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var button = obj.GetComponent<Button>();
            button.targetGraphic = image;
            label = CreateLabel(obj.transform, fontSize, Vector2.zero, size, textColor);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(10f, 2f);
            label.rectTransform.offsetMax = new Vector2(-10f, -2f);
            return button;
        }

        public static void ApplyViewportAspectScale(RectTransform authoredPanel, float aspect)
        {
            if (authoredPanel == null || !(aspect > 0f) || float.IsNaN(aspect) ||
                float.IsInfinity(aspect))
                return;
            float width = Mathf.Sqrt(ReferenceWidth * ReferenceHeight * aspect);
            float height = Mathf.Sqrt(ReferenceWidth * ReferenceHeight / aspect);
            float scale = Mathf.Min(1f, Mathf.Min(width / ReferenceWidth, height / ReferenceHeight));
            if (scale < 1f) scale *= 0.95f;
            authoredPanel.localScale = new Vector3(scale, scale, 1f);
        }

        public static void SafeDestroyCanvas(GameObject canvasObject)
        {
            if (canvasObject == null)
                return;
            canvasObject.SetActive(false);
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(canvasObject);
            else
                UnityEngine.Object.DestroyImmediate(canvasObject);
        }
    }
}
