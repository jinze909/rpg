using System;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Playable M01 dock meeting after West Lane. The player replies to Qi
    /// Que, accepts a cake, and freely compares three passage leads. This
    /// view does not award a vessel, register a pension or set voyage_path.
    /// </summary>
    public sealed class M01DockBriefingView : IDisposable
    {
        private readonly M01DockBriefing briefing;
        private readonly GameObject canvasObject;
        private readonly RectTransform authoredView;
        private readonly Font latinFont;
        private readonly Text heading;
        private readonly Text dialogue;
        private readonly Button[] choices = new Button[3];
        private readonly Text[] choiceLabels = new Text[3];
        private readonly Button advanceButton;
        private readonly Text advanceLabel;
        private Action onFinished;
        private bool delivered;
        private bool disposed;

        public M01DockStage Stage => briefing.Stage;
        public M01VoyageLead? InspectedLead => briefing.InspectedLead;
        public Text Dialogue => dialogue;
        public Button AdvanceButton => advanceButton;

        public M01DockBriefingView(Transform parent, Action onFinished)
        {
            if (parent == null)
                throw new ArgumentNullException(nameof(parent));
            if (onFinished == null)
                throw new ArgumentNullException(nameof(onFinished));
            this.onFinished = onFinished;
            briefing = new M01DockBriefing();
            briefing.Begin();
            latinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            canvasObject = new GameObject("M01 dock briefing canvas",
                typeof(RectTransform), typeof(Canvas));
            canvasObject.transform.SetParent(parent, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 122;
            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(960f, 540f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();
            Panel(canvas.transform, "M01 dock backdrop",
                new Vector2(950f, 530f), new Color(0.05f, 0.07f, 0.08f, 0.98f));
            var authored = new GameObject("M01 dock authored view", typeof(RectTransform));
            authored.transform.SetParent(canvas.transform, false);
            authoredView = (RectTransform)authored.transform;
            authoredView.anchorMin = authoredView.anchorMax = new Vector2(0.5f, 0.5f);
            authoredView.sizeDelta = new Vector2(960f, 540f);
            Panel(authoredView, "M01 dock paper",
                new Vector2(920f, 515f), new Color(0.92f, 0.89f, 0.81f));

            heading = Label(authoredView, 25,
                new Vector2(0f, 216f), new Vector2(830f, 50f));
            dialogue = Label(authoredView, 20,
                new Vector2(0f, 76f), new Vector2(830f, 235f));
            dialogue.alignment = TextAnchor.MiddleLeft;
            for (int i = 0; i < choices.Length; i++)
            {
                int chosen = i;
                choices[i] = NewButton(authoredView, "M01 dock option " + i,
                    new Vector2(0f, -72f - 59f * i),
                    new Vector2(820f, 48f), out choiceLabels[i]);
                choices[i].onClick.AddListener(() => Choose(chosen));
            }
            advanceButton = NewButton(authoredView, "M01 dock continue",
                new Vector2(0f, -227f), new Vector2(440f, 45f), out advanceLabel);
            advanceButton.onClick.AddListener(Advance);

            LayoutForViewportAspect((float)Mathf.Max(Screen.width, 1) /
                Mathf.Max(Screen.height, 1));
            Refresh();
            SoundwellUiLocalization.Changed += Refresh;
        }

        public Button GetChoiceButton(int index)
        {
            if (index < 0 || index >= choices.Length)
                throw new ArgumentOutOfRangeException(nameof(index));
            return choices[index];
        }

        public void LayoutForViewportAspect(float aspect)
        {
            if (disposed || !(aspect > 0f) ||
                float.IsNaN(aspect) || float.IsInfinity(aspect))
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
            M01DockBeat beat = M01DockBriefingDialogue.Describe(briefing, chinese);
            heading.font = dialogue.font = advanceLabel.font = font;
            heading.text = chinese ? "临潮 · 祁雀的货栈" : "HARBOR · QI QUE'S WAREHOUSE";
            dialogue.text = beat.Text;
            for (int i = 0; i < choices.Length; i++)
            {
                bool active = !delivered && i < beat.Choices.Length;
                choices[i].gameObject.SetActive(active);
                choiceLabels[i].font = font;
                choiceLabels[i].text = active ? beat.Choices[i] : string.Empty;
            }
            advanceLabel.text = briefing.Stage == M01DockStage.OfferingCake
                ? (chinese ? "接过桂花糕，看看海图" : "Accept the cake and study the chart")
                : (chinese ? "记下三条路，告别祁雀" : "Remember the three routes and leave");
            advanceButton.gameObject.SetActive(!delivered && beat.CanContinue);
            if (!delivered)
            {
                SoundwellInputAdapter.KeepDialogFocus(canvasObject.transform,
                    briefing.Stage == M01DockStage.AwaitingOpeningReply ||
                    briefing.Stage == M01DockStage.ReviewingRoutes
                        ? choices[0] : advanceButton);
            }
        }

        private void Choose(int index)
        {
            if (disposed || delivered)
                return;
            if (index < 0 || index >= choices.Length)
                return;
            if (briefing.Stage == M01DockStage.AwaitingOpeningReply)
                briefing.Reply((M01DockReply)index);
            else if (briefing.Stage == M01DockStage.ReviewingRoutes)
                briefing.Inspect((M01VoyageLead)index);
            else
                return;
            Refresh();
        }

        private void Advance()
        {
            if (disposed || delivered)
                return;
            if (briefing.Stage == M01DockStage.OfferingCake)
            {
                briefing.ContinueAfterCake();
                Refresh();
                return;
            }
            if (briefing.Stage != M01DockStage.ReviewingRoutes)
                return;
            briefing.EndConversation();
            delivered = true;
            Refresh();
            Action listener = onFinished;
            onFinished = null;
            listener?.Invoke();
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            onFinished = null;
            SoundwellUiLocalization.Changed -= Refresh;
            foreach (Button button in choices)
                if (button != null)
                    button.onClick.RemoveAllListeners();
            if (advanceButton != null)
                advanceButton.onClick.RemoveAllListeners();
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
            var go = new GameObject(name, typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            RectTransform rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
        }

        private static Text Label(Transform parent, int fontSize,
            Vector2 position, Vector2 bounds)
        {
            Text label = SoundwellView.AddLabel(parent, string.Empty, fontSize,
                new Color(0.12f, 0.17f, 0.2f), position, bounds);
            label.raycastTarget = false;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 10;
            label.resizeTextMaxSize = fontSize;
            return label;
        }

        private static Button NewButton(Transform parent, string name,
            Vector2 pos, Vector2 size, out Text label)
        {
            var go = new GameObject(name, typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            image.color = new Color(0.81f, 0.82f, 0.76f);
            RectTransform rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
            Button button = go.GetComponent<Button>();
            button.targetGraphic = image;
            label = Label(go.transform, 17, Vector2.zero, size);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(10f, 2f);
            label.rectTransform.offsetMax = new Vector2(-10f, -2f);
            return button;
        }
    }
}
