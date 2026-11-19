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
        private static readonly Color ButtonFill = new Color(0.81f, 0.84f, 0.79f);

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
        private bool delivered;
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
            canvasObject = SoundwellUiTheme.CreateOverlayCanvas(
                parent, "M00 Han Qiao meeting canvas", 122, out authoredView,
                new Color(0.03f, 0.06f, 0.07f, 0.96f), Paper);

            heading = SoundwellUiTheme.CreateLabel(authoredView, 25,
                new Vector2(0f, 225f), new Vector2(840f, 46f), Brass);
            dialogue = SoundwellUiTheme.CreateLabel(authoredView, 19,
                new Vector2(0f, 65f), new Vector2(832f, 275f), Ink);
            dialogue.alignment = TextAnchor.MiddleLeft;
            dialogue.resizeTextMinSize = 11;

            for (int i = 0; i < choices.Length; i++)
            {
                int clicked = i;
                choices[i] = SoundwellUiTheme.CreateButton(authoredView,
                    "M00 Han Qiao choice " + i,
                    new Vector2(0f, -132f - 53f * i),
                    new Vector2(830f, 46f), 17, out choiceLabels[i],
                    ButtonFill, Ink);
                choices[i].onClick.AddListener(() => Choose(clicked));
            }

            continueButton = SoundwellUiTheme.CreateButton(authoredView,
                "M00 Han Qiao continue", new Vector2(0f, -213f),
                new Vector2(300f, 46f), 17, out continueLabel,
                ButtonFill, Ink);
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
                choices[i].gameObject.SetActive(!delivered && visible);
            }
            continueButton.gameObject.SetActive(!delivered && CurrentBeat.CanContinue);
            Button firstVisible = CurrentBeat.CanContinue ? continueButton : null;
            if (CurrentBeat.Choices.Length != 0)
                firstVisible = choices[0];
            if (!delivered)
                SoundwellInputAdapter.KeepDialogFocus(canvasObject.transform, firstVisible);
        }

        public void LayoutForViewportAspect(float aspect)
        {
            if (disposed)
                return;
            if (canvasObject == null || authoredView == null)
            {
                Dispose();
                return;
            }
            SoundwellUiTheme.ApplyViewportAspectScale(authoredView, aspect);
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
            SoundwellUiTheme.SafeDestroyCanvas(canvasObject);
        }

        private void Choose(int index)
        {
            if (disposed || delivered || CurrentBeat == null ||
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
            if (disposed || delivered || CurrentBeat == null || !CurrentBeat.CanContinue)
                return;
            if (sequence.Stage != M00HanQiaoMeetingStage.Closing)
            {
                sequence.Continue();
                Refresh();
                return;
            }

            delivered = true;
            Refresh();
            try
            {
                if (onFinished != null)
                {
                    sequence.Continue(onFinished);
                    onFinished = null;
                }
                else
                {
                    sequence.Continue();
                }
                Refresh();
            }
            catch
            {
                delivered = false;
                Refresh();
                throw;
            }
        }
    }
}
