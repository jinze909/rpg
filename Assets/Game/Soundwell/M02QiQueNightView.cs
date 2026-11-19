using System;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Scene-owned bilingual UGUI for M02's optional Qi Que night conversation.
    /// The host supplies the debt discovery already stored by Makinom.
    /// </summary>
    public sealed class M02QiQueNightView : IDisposable
    {
        private readonly M02QiQueNight night;
        private readonly GameObject canvasObject;
        private readonly RectTransform authoredView;
        private readonly Font latinFont;
        private readonly Text heading;
        private readonly Text dialogue;
        private readonly Button progressButton;
        private readonly Text progressLabel;
        private Action onFinished;
        private bool delivered;
        private bool disposed;

        public M02QiQueNight Night => night;
        public Text Dialogue => dialogue;
        public Button ProgressButton => progressButton;

        public M02QiQueNightView(Transform parent, bool debtAlreadyKnown, Action onFinished)
        {
            if (parent == null)
                throw new ArgumentNullException(nameof(parent));
            if (onFinished == null)
                throw new ArgumentNullException(nameof(onFinished));

            night = new M02QiQueNight(debtAlreadyKnown);
            night.Begin();
            this.onFinished = onFinished;
            latinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            canvasObject = new GameObject("M02 Qi Que warehouse canvas",
                typeof(RectTransform), typeof(Canvas));
            canvasObject.transform.SetParent(parent, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 125;
            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(960f, 540f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();

            Panel(canvas.transform, "M02 night backdrop",
                new Vector2(950f, 530f), new Color(0.04f, 0.06f, 0.08f, 0.98f));
            var authored = new GameObject("M02 Qi Que authored view",
                typeof(RectTransform));
            authored.transform.SetParent(canvas.transform, false);
            authoredView = (RectTransform)authored.transform;
            authoredView.anchorMin = authoredView.anchorMax = new Vector2(0.5f, 0.5f);
            authoredView.sizeDelta = new Vector2(960f, 540f);
            Panel(authoredView, "M02 Qi Que paper", new Vector2(920f, 515f),
                new Color(0.92f, 0.89f, 0.81f));
            heading = Label(authoredView, 25, new Vector2(0f, 211f),
                new Vector2(835f, 50f));
            dialogue = Label(authoredView, 21, new Vector2(0f, 39f),
                new Vector2(825f, 265f));
            dialogue.alignment = TextAnchor.MiddleLeft;
            progressButton = NewButton(authoredView, "M02 Qi Que continue",
                new Vector2(0f, -213f), new Vector2(540f, 55f),
                out progressLabel);
            progressButton.onClick.AddListener(Advance);

            LayoutForViewportAspect((float)Mathf.Max(1, Screen.width) /
                Mathf.Max(1, Screen.height));
            Refresh();
            SoundwellUiLocalization.Changed += Refresh;
        }

        public void LayoutForViewportAspect(float aspect)
        {
            if (disposed || !(aspect > 0f) || float.IsNaN(aspect) ||
                float.IsInfinity(aspect))
                return;
            float width = Mathf.Sqrt(960f * 540f * aspect);
            float height = Mathf.Sqrt(960f * 540f / aspect);
            float scale = Mathf.Min(1f, Mathf.Min(width / 960f, height / 540f));
            if (scale < 1f)
                scale *= 0.95f;
            authoredView.localScale = new Vector3(scale, scale, 1f);
        }

        public void Refresh()
        {
            if (disposed)
                return;
            if (canvasObject == null || heading == null)
            {
                Dispose();
                return;
            }

            bool chinese = SoundwellUiLocalization.Language == SoundwellUiLocalization.Chinese;
            Font font = SoundwellUiLocalization.PresentationFont(latinFont);
            heading.font = dialogue.font = progressLabel.font = font;
            heading.text = chinese ? "码头货栈 · 祁雀的实话" :
                "DOCK WAREHOUSE · QI QUE'S PROMISE";
            dialogue.text = M02QiQueNightDialogue.Describe(night, chinese);
            switch (night.Stage)
            {
                case M02QiQueNightStage.AtWarehouse:
                    progressLabel.text = chinese ? "走近祁雀" : "Approach Qi Que";
                    break;
                case M02QiQueNightStage.ConfrontingDebt:
                    progressLabel.text = chinese ? "听她说完" : "Hear her out";
                    break;
                case M02QiQueNightStage.OfferingCake:
                    progressLabel.text = chinese ? "接下桂花糕，看海图" : "Take the cake and see the chart";
                    break;
                case M02QiQueNightStage.ReviewingChart:
                    progressLabel.text = chinese ? "记下提议，去见陆钧" : "Note her offer and visit Lu Jun";
                    break;
                default:
                    progressLabel.text = string.Empty;
                    break;
            }
            progressButton.gameObject.SetActive(!delivered &&
                night.Stage != M02QiQueNightStage.Finished);
            if (!delivered)
                SoundwellInputAdapter.KeepDialogFocus(canvasObject.transform,
                    progressButton);
        }

        private void Advance()
        {
            if (disposed || delivered)
                return;
            switch (night.Stage)
            {
                case M02QiQueNightStage.AtWarehouse:
                    night.Greet();
                    break;
                case M02QiQueNightStage.ConfrontingDebt:
                    night.DiscussDebt();
                    break;
                case M02QiQueNightStage.OfferingCake:
                    night.AcceptCake();
                    break;
                case M02QiQueNightStage.ReviewingChart:
                    night.Leave();
                    delivered = true;
                    Refresh();
                    Action callback = onFinished;
                    onFinished = null;
                    callback?.Invoke();
                    return;
                default:
                    return;
            }
            Refresh();
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            onFinished = null;
            SoundwellUiLocalization.Changed -= Refresh;
            progressButton.onClick.RemoveAllListeners();
            if (canvasObject != null)
            {
                canvasObject.SetActive(false);
                if (Application.isPlaying)
                    UnityEngine.Object.Destroy(canvasObject);
                else
                    UnityEngine.Object.DestroyImmediate(canvasObject);
            }
        }

        private static void Panel(Transform parent, string name,
            Vector2 size, Color color)
        {
            var obj = new GameObject(name, typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Image));
            obj.transform.SetParent(parent, false);
            Image img = obj.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            RectTransform rect = img.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
        }

        private static Text Label(Transform parent, int fontSize,
            Vector2 pos, Vector2 bounds)
        {
            Text t = SoundwellView.AddLabel(parent, string.Empty, fontSize,
                new Color(0.12f, 0.17f, 0.20f), pos, bounds);
            t.raycastTarget = false;
            t.resizeTextForBestFit = true;
            t.resizeTextMinSize = 10;
            t.resizeTextMaxSize = fontSize;
            return t;
        }

        private static Button NewButton(Transform parent, string name,
            Vector2 pos, Vector2 size, out Text label)
        {
            var go = new GameObject(name, typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            Image img = go.GetComponent<Image>();
            img.color = new Color(0.81f, 0.82f, 0.76f);
            RectTransform rect = img.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
            Button button = go.GetComponent<Button>();
            button.targetGraphic = img;
            label = Label(go.transform, 18, Vector2.zero, size);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(12f, 2f);
            label.rectTransform.offsetMax = new Vector2(-12f, -2f);
            return button;
        }
    }
}
