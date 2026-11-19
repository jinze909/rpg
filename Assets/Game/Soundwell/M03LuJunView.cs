using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Optional playable M03 camp panel. Rendering reflects the exact
    /// state-machine gates. Host persists the completed choices through Makinom.
    /// </summary>
    public sealed class M03LuJunView : IDisposable
    {
        private readonly M03LuJunEncounter encounter;
        private readonly GameObject canvasObject;
        private readonly RectTransform authoredView;
        private readonly Font latinFont;
        private readonly Text heading;
        private readonly Text dialogue;
        private readonly Button[] choices = new Button[5];
        private readonly Text[] choiceLabels = new Text[5];
        private readonly Button advance;
        private readonly Text advanceLabel;
        private Action<M03LuJunEncounter> onFinished;
        private bool disposed;
        private bool delivered;
        private int rollCallBeat;

        public M03LuJunEncounter Encounter => encounter;
        public Text Dialogue => dialogue;
        public Button AdvanceButton => advance;
        public int RollCallBeat => rollCallBeat;

        private Button PreferredFocusTarget
        {
            get
            {
                if (disposed || delivered)
                    return null;
                if (advance.gameObject.activeInHierarchy)
                    return advance;
                foreach (Button option in choices)
                    if (option != null && option.gameObject.activeInHierarchy)
                        return option;
                return null;
            }
        }

        public M03LuJunView(Transform parent, M02BridgeEvidence evidence,
            bool officialRegistrationEffective, Action<M03LuJunEncounter> onFinished)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (onFinished == null) throw new ArgumentNullException(nameof(onFinished));
            encounter = new M03LuJunEncounter(evidence, officialRegistrationEffective);
            encounter.Begin();
            this.onFinished = onFinished;
            latinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            canvasObject = SoundwellUiTheme.CreateOverlayCanvas(
                parent, "M03 Lu Jun camp canvas", 126, out authoredView);
            heading = SoundwellUiTheme.CreateLabel(authoredView, 23,
                new Vector2(0f, 218f), new Vector2(835f, 48f));
            dialogue = SoundwellUiTheme.CreateLabel(authoredView, 19,
                new Vector2(0f, 106f), new Vector2(835f, 151f));
            dialogue.alignment = TextAnchor.MiddleLeft;
            for (int i = 0; i < choices.Length; i++)
            {
                int index = i;
                choices[i] = SoundwellUiTheme.CreateButton(authoredView,
                    "M03 Lu Jun option " + i, new Vector2(0f, -29f - i * 42f),
                    new Vector2(825f, 39f), 17, out choiceLabels[i]);
                choices[i].onClick.AddListener(() => Choose(index));
            }
            advance = SoundwellUiTheme.CreateButton(authoredView,
                "M03 Lu Jun advance", new Vector2(0f, -238f),
                new Vector2(405f, 38f), 17, out advanceLabel);
            advance.onClick.AddListener(Advance);
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

        public void LayoutForViewportAspect(float aspect)
        {
            if (disposed)
                return;
            SoundwellUiTheme.ApplyViewportAspectScale(authoredView, aspect);
        }

        public void Refresh()
        {
            if (disposed) return;
            if (canvasObject == null || heading == null)
            {
                Dispose();
                return;
            }
            bool chinese = SoundwellUiLocalization.Language == SoundwellUiLocalization.Chinese;
            Font font = SoundwellUiLocalization.PresentationFont(latinFont);
            heading.font = dialogue.font = advanceLabel.font = font;
            heading.text = chinese ? "海边旧营 · 陆钧的名册" : "OLD GUARD CAMP · LU JUN'S ROLL";
            dialogue.text = encounter.Stage == M03LuJunStage.VisitingVeterans
                ? M03LuJunCopy.RollCallMoment(rollCallBeat, chinese)
                : M03LuJunCopy.Narration(encounter, chinese);
            string[] options = M03LuJunCopy.Choices(encounter, chinese);
            for (int i = 0; i < choices.Length; i++)
            {
                choiceLabels[i].font = font;
                choiceLabels[i].text = i < options.Length ? options[i] : string.Empty;
                bool eligible = i < options.Length && !delivered;
                if (encounter.Stage == M03LuJunStage.ChoosingVoyage)
                    eligible &= encounter.CanChoose((M03VoyageChoice)i);
                choices[i].gameObject.SetActive(eligible);
            }
            advanceLabel.text = rollCallBeat < 3
                ? (chinese ? "继续听点名" : "Listen to the next name")
                : (chinese ? "听完点名，向陆钧走去" : "After the roll, approach Lu Jun");
            advance.gameObject.SetActive(!delivered &&
                encounter.Stage == M03LuJunStage.VisitingVeterans);
            if (!delivered)
                SoundwellInputAdapter.KeepDialogFocus(canvasObject.transform, PreferredFocusTarget);
        }

        private void Choose(int index)
        {
            if (disposed || delivered || index < 0 || index >= choices.Length)
                return;
            switch (encounter.Stage)
            {
                case M03LuJunStage.ChoosingOpening:
                    if (index >= 3) return;
                    encounter.Ask((M03LuJunOpening)index);
                    break;
                case M03LuJunStage.RespondingToConfession:
                    if (index >= 3) return;
                    encounter.Respond((M03ConfessionResponse)index);
                    break;
                case M03LuJunStage.ReplyingToSecrecyRequest:
                    if (index >= 3) return;
                    encounter.AnswerSecrecy((M03SecrecyAnswer)(index + 1));
                    break;
                case M03LuJunStage.ChoosingVoyage:
                    if (!encounter.CanChoose((M03VoyageChoice)index)) return;
                    // Lock UGUI before host persistence to reject repeated
                    // or reentrant Submit. Retain the callback on failure.
                    delivered = true;
                    Refresh();
                    try
                    {
                        encounter.ChooseVoyage((M03VoyageChoice)index, onFinished);
                        onFinished = null;
                    }
                    catch
                    {
                        // The encounter has restored ChoosingVoyage and
                        // cleared its provisional choice for a safe retry.
                        delivered = false;
                        Refresh();
                        throw;
                    }
                    return;
                default:
                    return;
            }
            Refresh();
            FocusFirstActionAfterStageChange();
        }

        private void Advance()
        {
            if (disposed || delivered || encounter.Stage != M03LuJunStage.VisitingVeterans)
                return;
            // The third unanswered name must actually be heard before the
            // opening questions become available.
            if (rollCallBeat < 3)
                rollCallBeat++;
            else
                encounter.ObserveRollCall();
            Refresh();
            FocusFirstActionAfterStageChange();
        }

        // The same UGUI button index carries a different answer after each
        // dialogue stage. Retaining its focus can turn a second pad Submit
        // into an unintended ship booking rather than a deliberate choice.
        private void FocusFirstActionAfterStageChange()
        {
            EventSystem system = EventSystem.current;
            if (system == null || system.alreadySelecting)
                return;
            GameObject selected = system.currentSelectedGameObject;
            if (selected == null || !selected.transform.IsChildOf(canvasObject.transform))
                return;
            Button first = PreferredFocusTarget;
            if (first != null && first.gameObject.activeInHierarchy &&
                selected != first.gameObject)
                system.SetSelectedGameObject(first.gameObject);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            onFinished = null;
            SoundwellUiLocalization.Changed -= Refresh;
            foreach (Button button in choices)
                if (button != null)
                    button.onClick.RemoveAllListeners();
            if (advance != null)
                advance.onClick.RemoveAllListeners();
            SoundwellUiTheme.SafeDestroyCanvas(canvasObject);
        }
    }
}
