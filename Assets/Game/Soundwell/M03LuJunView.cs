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
        private readonly RpgPixelArtManifest artManifest;
        private readonly Image stageIcon;
        private readonly Text heading;
        private readonly Text dialogue;
        private readonly Button[] choices = new Button[5];
        private readonly Text[] choiceLabels = new Text[5];
        private readonly Button advance;
        private readonly Text advanceLabel;
        private readonly Button informantButton;
        private readonly Text informantLabel;
        private Action<M03LuJunEncounter> onFinished;
        private bool disposed;
        private bool delivered;
        private int rollCallBeat;
        private bool informantVisited;
        private bool showingInformant;

        public M03LuJunEncounter Encounter => encounter;
        public Text Dialogue => dialogue;
        public Image StageIcon => stageIcon;
        public Button AdvanceButton => advance;
        public Button InformantButton => informantButton;
        public bool InformantVisited => informantVisited;
        public int RollCallBeat => rollCallBeat;
        public bool IsDisposed => disposed || CanvasDestroyed();

        private Button PreferredFocusTarget
        {
            get
            {
                if (disposed || delivered || CanvasDestroyed())
                    return null;
                if (advance.gameObject.activeInHierarchy)
                    return advance;
                foreach (Button option in choices)
                    if (option != null && option.gameObject.activeInHierarchy &&
                        option.IsActive() && option.IsInteractable())
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
            artManifest = RpgPixelArtManifest.LoadDefault();
            canvasObject = SoundwellUiTheme.CreateOverlayCanvas(
                parent, "M03 Lu Jun camp canvas", 126, out authoredView);
            stageIcon = RpgStoryArtResolver.CreateOrUpdatePixelIcon(
                authoredView, "M03 Lu Jun stage icon",
                new Vector2(-346f, 106f), new Vector2(112f, 112f),
                ResolveStageSprite());
            heading = SoundwellUiTheme.CreateLabel(authoredView, 23,
                new Vector2(0f, 218f), new Vector2(835f, 48f));
            dialogue = SoundwellUiTheme.CreateLabel(authoredView, 19,
                new Vector2(72f, 106f), new Vector2(690f, 151f));
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
            informantButton = SoundwellUiTheme.CreateButton(authoredView,
                "M03 miner informant", new Vector2(-210f, -238f),
                new Vector2(400f, 38f), 17, out informantLabel);
            informantButton.onClick.AddListener(AskInformant);
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
            // The scene owner can destroy the canvas before calling Dispose.
            // Detach the locale listener instead of laying out dead Unity UI.
            if (CanvasDestroyed())
            {
                Dispose();
                return;
            }
            if (float.IsNaN(aspect) || float.IsInfinity(aspect) || aspect <= 0f)
                return;
            SoundwellUiTheme.ApplyViewportAspectScale(authoredView, aspect);
        }

        public void Refresh()
        {
            if (disposed) return;
            if (CanvasDestroyed())
            {
                Dispose();
                return;
            }
            if (stageIcon != null)
                RpgStoryArtResolver.ApplyPixelSprite(stageIcon, ResolveStageSprite());
            bool chinese = SoundwellUiLocalization.Language == SoundwellUiLocalization.Chinese;
            Font font = SoundwellUiLocalization.PresentationFont(latinFont);
            heading.font = dialogue.font = advanceLabel.font = informantLabel.font = font;
            heading.text = chinese ? "海边旧营 · 陆钧的名册" : "OLD GUARD CAMP · LU JUN'S ROLL";
            dialogue.text = encounter.Stage == M03LuJunStage.VisitingVeterans
                ? (showingInformant ? M03LuJunCopy.MineInformantReport(chinese)
                    : M03LuJunCopy.RollCallMoment(rollCallBeat, chinese))
                : M03LuJunCopy.Narration(encounter, chinese);
            string[] options = M03LuJunCopy.Choices(encounter, chinese);
            for (int i = 0; i < choices.Length; i++)
            {
                choiceLabels[i].font = font;
                choiceLabels[i].text = i < options.Length ? options[i] : string.Empty;
                bool visible = i < options.Length && !delivered;
                bool eligible = visible &&
                    (encounter.Stage != M03LuJunStage.ChoosingVoyage ||
                     encounter.CanChoose((M03VoyageChoice)i));
                // Keep the stamped official routes visible as locked choices.
                // Players can discover the prerequisite without being granted
                // a voyage or bypassing M02's authoritative registration.
                choices[i].gameObject.SetActive(visible);
                choices[i].interactable = eligible;
            }
            advanceLabel.text = showingInformant
                ? (chinese ? "回到点名" : "Return to the roll call")
                : rollCallBeat < 3
                    ? (chinese ? "继续听点名" : "Listen to the next name")
                    : (chinese ? "听完点名，向陆钧走去" : "After the roll, approach Lu Jun");
            bool atRollCall = !delivered &&
                encounter.Stage == M03LuJunStage.VisitingVeterans;
            // The roll call has no choice rows: use that space for readable\n            // fixed-size dialogue instead of shrinking long CJK copy to 10 pt.\n            dialogue.resizeTextForBestFit = !atRollCall;\n            dialogue.rectTransform.sizeDelta = new Vector2(690f, atRollCall ? 240f : 151f);\n            dialogue.rectTransform.anchoredPosition = new Vector2(72f, atRollCall ? 61.5f : 106f);\n            dialogue.alignment = atRollCall ? TextAnchor.UpperLeft : TextAnchor.MiddleLeft;\n            advance.gameObject.SetActive(atRollCall);
            informantLabel.font = font;
            informantLabel.text = chinese ? "询问矿区线人" : "Ask the miner informant";
            informantButton.gameObject.SetActive(atRollCall && !informantVisited);
            // Share the existing bottom action row; never cover the story text.
            advance.GetComponent<RectTransform>().anchoredPosition =
                informantButton.gameObject.activeSelf
                    ? new Vector2(210f, -238f) : new Vector2(0f, -238f);
            if (!delivered)
                SoundwellInputAdapter.KeepDialogFocus(canvasObject.transform, PreferredFocusTarget);
        }

        private Sprite ResolveStageSprite()
        {
            if (delivered || encounter.Stage == M03LuJunStage.Complete)
                return null;
            switch (encounter.Stage)
            {
                case M03LuJunStage.VisitingVeterans:
                    return rollCallBeat == 0
                        ? RpgStoryArtResolver.ResolveM03CampCoalsSprite(artManifest)
                        : RpgStoryArtResolver.ResolveM03LuJunSprite(true, artManifest);
                case M03LuJunStage.ChoosingOpening:
                    return RpgStoryArtResolver.ResolveM03LuJunSprite(false, artManifest);
                case M03LuJunStage.RespondingToConfession:
                    return RpgStoryArtResolver.ResolveM02EvidenceSprite(
                        M02BridgeEvidence.WarReportDraft, artManifest);
                case M03LuJunStage.ReplyingToSecrecyRequest:
                    return RpgStoryArtResolver.ResolveM03StandardSprite(false, 0, artManifest);
                case M03LuJunStage.ChoosingVoyage:
                    return RpgStoryArtResolver.ResolveM03TidalChartSprite(true, artManifest);
                default:
                    return null;
            }
        }

        private void Choose(int index)
        {
            if (disposed || delivered)
                return;
            if (CanvasDestroyed())
            {
                Dispose();
                return;
            }
            if (index < 0 || index >= choices.Length)
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
                        // A successful host write can keep this camp view alive.
                        // Show the completed passage plan rather than leaving
                        // the pre-selection dialogue on screen.
                        Refresh();
                    }
                    catch
                    {
                        // The encounter has restored ChoosingVoyage and
                        // cleared its provisional choice for a safe retry.
                        if (!disposed)
                        {
                            delivered = false;
                            Refresh();
                        }
                        throw;
                    }
                    return;
                default:
                    return;
            }
            Refresh();
            FocusFirstActionAfterStageChange();
        }

        private void AskInformant()
        {
            if (disposed || delivered)
                return;
            if (CanvasDestroyed())
            {
                Dispose();
                return;
            }
            if (encounter.Stage != M03LuJunStage.VisitingVeterans || informantVisited)
                return;
            // The camp contact provides optional route context, not a stamped
            // registration, booked ship or shortcut past the three missing names.
            informantVisited = true;
            showingInformant = true;
            Refresh();
            FocusFirstActionAfterStageChange();
        }

        private void Advance()
        {
            if (disposed || delivered)
                return;
            if (CanvasDestroyed())
            {
                Dispose();
                return;
            }
            if (encounter.Stage != M03LuJunStage.VisitingVeterans)
                return;
            if (showingInformant)
            {
                showingInformant = false;
                Refresh();
                FocusFirstActionAfterStageChange();
                return;
            }
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
            if (disposed || CanvasDestroyed())
                return;
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
            if (informantButton != null)
                informantButton.onClick.RemoveAllListeners();
            SoundwellUiTheme.SafeDestroyCanvas(canvasObject);
        }

        private bool CanvasDestroyed()
        {
            if (canvasObject == null || authoredView == null || heading == null ||
                dialogue == null || advance == null || advanceLabel == null ||
                informantButton == null || informantLabel == null)
                return true;
            for (int i = 0; i < choices.Length; i++)
            {
                if (choices[i] == null || choiceLabels[i] == null)
                    return true;
            }
            return false;
        }
    }
}
