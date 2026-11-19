using System;
using UnityEngine;
using UnityEngine.EventSystems;
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

        // When opening the route chart, prefer the first explorable lead.
        // Leaving is still allowed, but a repeated pad Submit must not
        // accidentally skip the offers without showing their choices.
        public Button PreferredFocusTarget
        {
            get
            {
                if (disposed || delivered || canvasObject == null)
                    return null;
                if (briefing.Stage == M01DockStage.AwaitingOpeningReply ||
                    briefing.Stage == M01DockStage.ReviewingRoutes)
                    return choices.Length > 0 && choices[0] != null ? choices[0] : null;
                return briefing.Stage == M01DockStage.OfferingCake && advanceButton != null
                    ? advanceButton : null;
            }
        }

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

            canvasObject = SoundwellUiTheme.CreateOverlayCanvas(
                parent, "M01 dock briefing canvas", 122, out authoredView,
                new Color(0.05f, 0.07f, 0.08f, 0.98f));

            heading = SoundwellUiTheme.CreateLabel(authoredView, 25,
                new Vector2(0f, 216f), new Vector2(830f, 50f));
            dialogue = SoundwellUiTheme.CreateLabel(authoredView, 20,
                new Vector2(0f, 76f), new Vector2(830f, 235f));
            dialogue.alignment = TextAnchor.MiddleLeft;
            Color buttonColor = new Color(0.81f, 0.82f, 0.76f);
            for (int i = 0; i < choices.Length; i++)
            {
                int chosen = i;
                choices[i] = SoundwellUiTheme.CreateButton(authoredView,
                    "M01 dock option " + i, new Vector2(0f, -72f - 59f * i),
                    new Vector2(820f, 48f), 17, out choiceLabels[i], buttonColor);
                choices[i].onClick.AddListener(() => Choose(chosen));
            }
            advanceButton = SoundwellUiTheme.CreateButton(authoredView,
                "M01 dock continue", new Vector2(0f, -227f),
                new Vector2(440f, 45f), 17, out advanceLabel, buttonColor);
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
            if (disposed)
                return;
            if (canvasObject == null || authoredView == null)
            {
                Dispose();
                return;
            }
            SoundwellUiTheme.ApplyViewportAspectScale(authoredView, aspect);
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
                    PreferredFocusTarget);
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
                // KeepDialogFocus preserves an active selected control.
                // Continue remains active in ReviewingRoutes, so specifically
                // move keyboard/gamepad focus to the first route when the
                // player's last Submit came from Continue.
                EventSystem system = EventSystem.current;
                if (Application.isPlaying && system != null &&
                    !system.alreadySelecting &&
                    advanceButton != null &&
                    system.currentSelectedGameObject == advanceButton.gameObject &&
                    choices.Length > 0 && choices[0] != null &&
                    choices[0].gameObject.activeInHierarchy)
                    system.SetSelectedGameObject(choices[0].gameObject);
                return;
            }
            if (briefing.Stage != M01DockStage.ReviewingRoutes)
                return;
            // Lock against reentrant UGUI submits before handing off to the
            // scene host. A failed handoff must keep the route briefing
            // retryable rather than hiding the leave button.
            delivered = true;
            Refresh();
            try
            {
                briefing.EndConversation(onFinished);
                onFinished = null;
            }
            catch
            {
                delivered = false;
                Refresh();
                throw;
            }
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
            SoundwellUiTheme.SafeDestroyCanvas(canvasObject);
        }
    }
}
