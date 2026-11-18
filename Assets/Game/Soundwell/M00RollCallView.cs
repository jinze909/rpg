using System;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Soundwell
{
    /// <summary>
    /// UGUI presentation for M00's authored three-call ritual. The sequence
    /// remains the sole authority for when another call is allowed; the host
    /// advances the pause between unanswered calls through AdvanceAfterUnansweredCall.
    /// </summary>
    public sealed class M00RollCallView : IDisposable
    {
        private static readonly Color Ink = new Color(0.10f, 0.15f, 0.18f);
        private static readonly Color Paper = new Color(0.91f, 0.89f, 0.80f);
        private static readonly Color Gold = new Color(0.60f, 0.39f, 0.17f);
        private static readonly Color ButtonInk = new Color(0.16f, 0.23f, 0.26f);

        private readonly M00RollCallSequence sequence;
        private readonly GameObject canvasObject;
        private readonly RectTransform authoredView;
        private readonly Font englishFont;
        private readonly Text title;
        private readonly Text speaker;
        private readonly Text guidance;
        private readonly Button[] choiceButtons = new Button[3];
        private readonly Text[] choiceLabels = new Text[3];
        private readonly Button continueButton;
        private readonly Text continueLabel;
        private Action<M00RollCallOutcome> onContinue;
        private bool disposed;
        private float lastViewAspect = -1f;

        public M00RollCallBeat CurrentBeat { get; private set; }
        public bool IsComplete => sequence.IsComplete;
        public bool IsWaitingForNextCall =>
            CurrentBeat != null && !CurrentBeat.Finished && !CurrentBeat.CanRespond;
        public int CallsMade => sequence.CallsMade;
        public Button ContinueButton => continueButton;
        public Text SpeakerText => speaker;
        public Text GuidanceText => guidance;

        public M00RollCallView(Transform parent, M00RollCallSequence sequence,
            Action<M00RollCallOutcome> onContinue)
        {
            if (parent == null)
                throw new ArgumentNullException(nameof(parent));
            if (sequence == null)
                throw new ArgumentNullException(nameof(sequence));
            if (!sequence.IsStarted)
                throw new InvalidOperationException("Begin the M00 roll call before creating its view.");

            this.sequence = sequence;
            this.onContinue = onContinue;
            englishFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            canvasObject = new GameObject("M00 twelfth-year roll call canvas",
                typeof(RectTransform), typeof(Canvas));
            canvasObject.transform.SetParent(parent, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 120;
            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(960f, 540f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();

            CreateStretchPanel(canvas.transform, "M00 roll call dimmed square",
                new Color(0.035f, 0.055f, 0.065f, 0.94f));

            var viewObject = new GameObject("M00 roll call authored view",
                typeof(RectTransform));
            viewObject.transform.SetParent(canvas.transform, false);
            authoredView = (RectTransform)viewObject.transform;
            authoredView.anchorMin = authoredView.anchorMax = new Vector2(0.5f, 0.5f);
            authoredView.anchoredPosition = Vector2.zero;
            authoredView.sizeDelta = new Vector2(960f, 540f);

            CreatePanel(authoredView, "M00 roll call paper panel",
                Vector2.zero, new Vector2(900f, 470f), Paper);

            title = CreateLabel(authoredView,
                Copy("THE TWELFTH-YEAR ROLL CALL", "第十二年的点名"),
                25, Gold, new Vector2(0f, 184f), new Vector2(820f, 42f));

            speaker = CreateLabel(authoredView, string.Empty, 23, Ink,
                new Vector2(0f, 91f), new Vector2(820f, 110f));
            speaker.alignment = TextAnchor.MiddleCenter;

            guidance = CreateLabel(authoredView, string.Empty, 16, ButtonInk,
                new Vector2(0f, 14f), new Vector2(820f, 54f));
            guidance.alignment = TextAnchor.MiddleCenter;

            for (int i = 0; i < choiceButtons.Length; i++)
            {
                int choiceIndex = i;
                Vector2 position = new Vector2(0f, -66f - i * 61f);
                choiceButtons[i] = CreateButton(authoredView,
                    "M00 roll call choice " + (i + 1), position,
                    new Vector2(720f, 46f), out choiceLabels[i]);
                choiceButtons[i].onClick.AddListener(() => Choose(choiceIndex));
                choiceButtons[i].gameObject.SetActive(false);
            }

            continueButton = CreateButton(authoredView, "M00 roll call continue",
                new Vector2(0f, -188f), new Vector2(340f, 48f), out continueLabel);
            continueLabel.text = Copy("Continue", "继续");
            continueButton.onClick.AddListener(Continue);
            continueButton.gameObject.SetActive(false);

            LayoutForViewportAspect((float)Mathf.Max(1, Screen.width) /
                Mathf.Max(1, Screen.height));
            Refresh();
            // The active roll call must follow the existing global UI language
            // immediately, including its persistent heading and finish button.
            SoundwellUiLocalization.Changed += Refresh;
        }

        public Button GetChoiceButton(int index)
        {
            if (index < 0 || index >= choiceButtons.Length)
                throw new ArgumentOutOfRangeException(nameof(index));
            return choiceButtons[index];
        }

        /// <summary>
        /// Called by the owning scene after its authored pause between names.
        /// Returns true only when the next call opens another response window.
        /// </summary>
        public bool AdvanceAfterUnansweredCall()
        {
            ThrowIfDisposed();
            if (!IsWaitingForNextCall)
                throw new InvalidOperationException("The roll call is not waiting between unanswered calls.");

            bool canRespond = sequence.AdvanceAfterUnansweredCall();
            Refresh();
            return canRespond;
        }

        public void Refresh()
        {
            ThrowIfDisposed();
            // A Unity GameObject may be destroyed outside this view's Dispose
            // method (notably by a scene unload or an EditMode test fixture).
            // Detach the global locale listener before touching dead UGUI Text.
            if (canvasObject == null || title == null)
            {
                Dispose();
                return;
            }
            bool chinese = SoundwellUiLocalization.Language == SoundwellUiLocalization.Chinese;
            CurrentBeat = M00RollCallDialogue.Describe(sequence, chinese);
            title.text = Copy("THE TWELFTH-YEAR ROLL CALL", "第十二年的点名");
            continueLabel.text = Copy("Continue", "继续");
            // Use the pinned CJK font for Chinese and restore the real Latin
            // font on EN, rather than passing a previously selected CJK font
            // back as the English fallback on the next locale change.
            Font font = SoundwellUiLocalization.PresentationFont(englishFont);
            title.font = font;
            speaker.font = font;
            guidance.font = font;
            continueLabel.font = font;
            speaker.text = CurrentBeat.SpeakerLine;
            guidance.text = CurrentBeat.Guidance;

            for (int i = 0; i < choiceButtons.Length; i++)
            {
                bool visible = i < CurrentBeat.Choices.Length;
                choiceLabels[i].font = font;
                choiceButtons[i].gameObject.SetActive(visible);
                if (visible)
                {
                    choiceLabels[i].text = CurrentBeat.Choices[i];
                    choiceButtons[i].interactable = true;
                }
            }

            continueButton.gameObject.SetActive(CurrentBeat.Finished);
        }

        /// <summary>
        /// Fits the authored 960x540 composition inside portrait and ultrawide
        /// viewports without moving the logical scene or changing the call state.
        /// </summary>
        public void LayoutForViewportAspect(float aspect)
        {
            ThrowIfDisposed();
            if (!(aspect > 0f) || float.IsNaN(aspect) || float.IsInfinity(aspect))
                return;

            float width = Mathf.Sqrt(960f * 540f * aspect);
            float height = Mathf.Sqrt(960f * 540f / aspect);
            float scale = Mathf.Min(1f, Mathf.Min(width / 960f, height / 540f));
            if (scale < 1f)
                scale *= 0.95f;
            authoredView.localScale = new Vector3(scale, scale, 1f);
            lastViewAspect = aspect;
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            SoundwellUiLocalization.Changed -= Refresh;
            onContinue = null;
            foreach (Button button in choiceButtons)
                if (button != null)
                    button.onClick.RemoveAllListeners();
            if (continueButton != null)
                continueButton.onClick.RemoveAllListeners();
            if (canvasObject != null)
            {
                if (Application.isPlaying)
                    UnityEngine.Object.Destroy(canvasObject);
                else
                    UnityEngine.Object.DestroyImmediate(canvasObject);
            }
        }

        private void Choose(int index)
        {
            ThrowIfDisposed();
            if (CurrentBeat == null || !CurrentBeat.CanRespond ||
                index < 0 || index >= CurrentBeat.Choices.Length)
                return;

            switch (index)
            {
                case 0: sequence.Respond(M00RollCallChoice.Answer); break;
                case 1: sequence.Respond(M00RollCallChoice.RemainSilent); break;
                case 2: sequence.Respond(M00RollCallChoice.LeaveCrowd); break;
                default: throw new ArgumentOutOfRangeException(nameof(index));
            }
            Refresh();
        }

        private void Continue()
        {
            ThrowIfDisposed();
            if (!sequence.IsComplete)
                return;
            Action<M00RollCallOutcome> callback = onContinue;
            onContinue = null;
            callback?.Invoke(sequence.Outcome);
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(M00RollCallView));
        }

        private static string Copy(string english, string chinese) =>
            SoundwellUiLocalization.Language == SoundwellUiLocalization.Chinese
                ? chinese : english;

        private static Image CreateStretchPanel(Transform parent, string name, Color color)
        {
            var obj = new GameObject(name,
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            obj.transform.SetParent(parent, false);
            var image = obj.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            image.rectTransform.anchorMin = Vector2.zero;
            image.rectTransform.anchorMax = Vector2.one;
            image.rectTransform.offsetMin = Vector2.zero;
            image.rectTransform.offsetMax = Vector2.zero;
            return image;
        }

        private static Image CreatePanel(Transform parent, string name,
            Vector2 position, Vector2 size, Color color)
        {
            var obj = new GameObject(name,
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            obj.transform.SetParent(parent, false);
            var image = obj.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            image.rectTransform.anchorMin = image.rectTransform.anchorMax =
                new Vector2(0.5f, 0.5f);
            image.rectTransform.anchoredPosition = position;
            image.rectTransform.sizeDelta = size;
            return image;
        }

        private static Text CreateLabel(Transform parent, string content, int fontSize,
            Color color, Vector2 position, Vector2 size)
        {
            Text label = SoundwellView.AddLabel(parent, content, fontSize,
                color, position, size);
            label.font = SoundwellUiLocalization.PresentationFont(label.font);
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = Mathf.Max(10, fontSize - 4);
            label.resizeTextMaxSize = fontSize;
            return label;
        }

        private static Button CreateButton(Transform parent, string name,
            Vector2 position, Vector2 size, out Text label)
        {
            var obj = new GameObject(name,
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            obj.transform.SetParent(parent, false);
            var image = obj.GetComponent<Image>();
            image.color = new Color(0.82f, 0.84f, 0.80f);
            var button = obj.GetComponent<Button>();
            button.targetGraphic = image;
            var rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            label = CreateLabel(obj.transform, string.Empty, 17, Ink,
                Vector2.zero, size);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(12f, 2f);
            label.rectTransform.offsetMax = new Vector2(-12f, -2f);
            return button;
        }
    }
}
