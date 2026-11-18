using System;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Soundwell
{
    /// <summary>
    /// UGUI conversation for the authored M00 meeting behind the memorial.
    /// The sequence owns branch decisions. This view only presents the current
    /// dialogue beat and forwards player choices; later scene logic owns saves.
    /// </summary>
    public sealed class M00HanQiaoMeetingView : IDisposable
    {
        private static readonly Color Ink = new Color(0.13f, 0.17f, 0.19f);
        private static readonly Color Paper = new Color(0.92f, 0.89f, 0.80f);
        private static readonly Color Brass = new Color(0.59f, 0.39f, 0.18f);

        private readonly M00HanQiaoMeetingSequence sequence;
        private readonly GameObject canvasObject;
        private readonly RectTransform authoredView;
        private readonly Font latinFont;
        private readonly Text heading;
        private readonly Text dialogue;
        private readonly Button[] choices = new Button[3];
        private readonly Text[] choiceLabels = new Text[3];
        private readonly Button continueButton;
        private readonly Text continueLabel;
        private Action<M00HanQiaoMeetingSequence> onFinished;
        private Action onRansomCountShown;
        private bool disposed;

        public M00HanQiaoMeetingBeat CurrentBeat { get; private set; }
        public Text DialogueText => dialogue;
        public Text HeadingText => heading;
        public Button ContinueButton => continueButton;

        public M00HanQiaoMeetingView(Transform parent,
            M00HanQiaoMeetingSequence sequence,
            Action<M00HanQiaoMeetingSequence> onFinished, Action onRansomCountShown = null)
        {
            if (parent == null)
                throw new ArgumentNullException(nameof(parent));
            if (sequence == null)
                throw new ArgumentNullException(nameof(sequence));
            if (!sequence.IsStarted)
                throw new InvalidOperationException("Begin Han Qiao's meeting first.");

            this.sequence = sequence;
            this.onFinished = onFinished;
            this.onRansomCountShown = onRansomCountShown;
            latinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            canvasObject = new GameObject("M00 Han Qiao meeting canvas",
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

            AddPanel(canvas.transform, "M00 Han Qiao backdrop", Vector2.zero,
                new Vector2(960f, 540f), new Color(0.03f, 0.06f, 0.07f, 0.96f));

            var authored = new GameObject("M00 Han Qiao authored view",
                typeof(RectTransform));
            authored.transform.SetParent(canvas.transform, false);
            authoredView = (RectTransform)authored.transform;
            authoredView.anchorMin = authoredView.anchorMax = new Vector2(0.5f, 0.5f);
            authoredView.sizeDelta = new Vector2(960f, 540f);
            authoredView.anchoredPosition = Vector2.zero;

            AddPanel(authoredView, "M00 Han Qiao table room", Vector2.zero,
                new Vector2(910f, 510f), Paper);

            heading = AddText(authoredView, string.Empty, 25, Brass,
                new Vector2(0f, 225f), new Vector2(840f, 46f));
            dialogue = AddText(authoredView, string.Empty, 19, Ink,
                new Vector2(0f, 65f), new Vector2(832f, 275f));
            dialogue.alignment = TextAnchor.MiddleLeft;
            dialogue.resizeTextForBestFit = true;
            dialogue.resizeTextMinSize = 11;
            dialogue.resizeTextMaxSize = 19;

            for (int i = 0; i < choices.Length; i++)
            {
                int clicked = i;
                choices[i] = AddButton(authoredView,
                    "M00 Han Qiao choice " + i,
                    new Vector2(0f, -132f - 53f * i),
                    new Vector2(830f, 46f), out choiceLabels[i]);
                choices[i].onClick.AddListener(() => Choose(clicked));
            }

            continueButton = AddButton(authoredView,
                "M00 Han Qiao continue", new Vector2(0f, -213f),
                new Vector2(300f, 46f), out continueLabel);
            continueButton.onClick.AddListener(Continue);

            LayoutForViewportAspect((float)Mathf.Max(1, Screen.width) /
                Mathf.Max(1, Screen.height));
            Refresh();
            SoundwellUiLocalization.Changed += Refresh;
        }

        public Button GetChoiceButton(int index)
        {
            if (index < 0 || index >= choices.Length)
                throw new ArgumentOutOfRangeException(nameof(index));
            return choices[index];
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
            CurrentBeat = M00HanQiaoMeetingDialogue.Describe(sequence, chinese);
            Font font = SoundwellUiLocalization.PresentationFont(latinFont);
            heading.font = font;
            dialogue.font = font;
            continueLabel.font = font;
            heading.text = chinese ? "石碑后的小屋" : "BEHIND THE MEMORIAL";
            dialogue.text = CurrentBeat.Text;
            continueLabel.text = chinese ? "继续" : "Continue";

            for (int i = 0; i < choices.Length; i++)
            {
                bool visible = i < CurrentBeat.Choices.Length;
                choiceLabels[i].font = font;
                choiceLabels[i].text = visible ? CurrentBeat.Choices[i] : string.Empty;
                choices[i].gameObject.SetActive(visible);
            }
            continueButton.gameObject.SetActive(CurrentBeat.CanContinue);
            Button firstVisible = CurrentBeat.CanContinue ? continueButton : null;
            if (CurrentBeat.Choices.Length != 0)
                firstVisible = choices[0];
            SoundwellInputAdapter.KeepDialogFocus(canvasObject.transform, firstVisible);
        }

        public void LayoutForViewportAspect(float aspect)
        {
            if (disposed || !(aspect > 0f) || float.IsNaN(aspect) || float.IsInfinity(aspect))
                return;
            float width = Mathf.Sqrt(960f * 540f * aspect);
            float height = Mathf.Sqrt(960f * 540f / aspect);
            float scale = Mathf.Min(1f, Mathf.Min(width / 960f, height / 540f));
            if (scale < 1f)
                scale *= 0.95f;
            authoredView.localScale = new Vector3(scale, scale, 1f);
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            SoundwellUiLocalization.Changed -= Refresh;
            onFinished = null;
            onRansomCountShown = null;
            foreach (Button button in choices)
                if (button != null)
                    button.onClick.RemoveAllListeners();
            if (continueButton != null)
                continueButton.onClick.RemoveAllListeners();
            if (canvasObject != null)
            {
                canvasObject.SetActive(false);
                if (Application.isPlaying)
                    UnityEngine.Object.Destroy(canvasObject);
                else
                    UnityEngine.Object.DestroyImmediate(canvasObject);
            }
        }

        private void Choose(int index)
        {
            if (disposed || CurrentBeat == null ||
                index < 0 || index >= CurrentBeat.Choices.Length)
                return;
            switch (sequence.Stage)
            {
                case M00HanQiaoMeetingStage.AwaitingOpeningChoice:
                    sequence.ChooseOpening((M00HanQiaoOpeningChoice)index);
                    break;
                case M00HanQiaoMeetingStage.AwaitingNextStepChoice:
                    sequence.ChooseNextStep((M00HanQiaoNextStepChoice)index);
                    break;
                default:
                    return;
            }
            Refresh();
            if (sequence.Stage == M00HanQiaoMeetingStage.RansomCount)
            {
                Action shown = onRansomCountShown;
                onRansomCountShown = null;
                shown?.Invoke();
            }
        }

        private void Continue()
        {
            if (disposed || CurrentBeat == null || !CurrentBeat.CanContinue)
                return;
            sequence.Continue();
            Refresh();
            if (!sequence.IsComplete)
                return;
            Action<M00HanQiaoMeetingSequence> finished = onFinished;
            onFinished = null;
            finished?.Invoke(sequence);
        }

        private static Image AddPanel(Transform parent, string name,
            Vector2 pos, Vector2 size, Color color)
        {
            var obj = new GameObject(name,
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            obj.transform.SetParent(parent, false);
            Image image = obj.GetComponent<Image>();
            image.raycastTarget = false;
            image.color = color;
            image.rectTransform.anchorMin = image.rectTransform.anchorMax =
                new Vector2(0.5f, 0.5f);
            image.rectTransform.anchoredPosition = pos;
            image.rectTransform.sizeDelta = size;
            return image;
        }

        private static Text AddText(Transform parent, string text,
            int size, Color color, Vector2 pos, Vector2 bounds)
        {
            Text result = SoundwellView.AddLabel(parent, text, size, color, pos, bounds);
            result.font = SoundwellUiLocalization.PresentationFont(result.font);
            result.resizeTextForBestFit = true;
            result.resizeTextMinSize = 12;
            result.resizeTextMaxSize = size;
            result.raycastTarget = false;
            return result;
        }

        private static Button AddButton(Transform parent, string name,
            Vector2 pos, Vector2 size, out Text label)
        {
            var obj = new GameObject(name,
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            obj.transform.SetParent(parent, false);
            var image = obj.GetComponent<Image>();
            image.color = new Color(0.81f, 0.84f, 0.79f);
            RectTransform rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
            Button button = obj.GetComponent<Button>();
            button.targetGraphic = image;
            label = AddText(obj.transform, string.Empty, 17, Ink,
                Vector2.zero, size);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(12f, 2f);
            label.rectTransform.offsetMax = new Vector2(-12f, -2f);
            return button;
        }
    }
}
