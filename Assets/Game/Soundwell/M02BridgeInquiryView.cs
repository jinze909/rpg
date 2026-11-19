using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Scene-owned UGUI for M02's optional investigation. It only reports
    /// observed clues; the host may persist each new discovery immediately.
    /// </summary>
    public sealed class M02BridgeInquiryView : IDisposable
    {
        private static readonly Color Ink = new Color(0.14f, 0.17f, 0.20f);
        private static readonly Color ButtonFill = new Color(0.81f, 0.82f, 0.76f);
        private static readonly M02BridgeEvidence[] WarehouseClues =
        {
            M02BridgeEvidence.CleanCableCut,
            M02BridgeEvidence.FerrymanLog,
            M02BridgeEvidence.ThirdBellSignal
        };

        private readonly M02BridgeInquiry inquiry;
        private readonly GameObject canvasObject;
        private readonly RectTransform authoredView;
        private readonly Text heading;
        private readonly Text narrative;
        private readonly Text[] clueLabels = new Text[3];
        private readonly Button[] clueButtons = new Button[3];
        private readonly Button continueButton;
        private readonly Text continueLabel;
        private readonly Font latinFont;
        private Action<M02BridgeEvidence> onCompleted;
        private Action<M02BridgeEvidence> onObserved;
        private bool disposed;
        private bool completed;

        public Button ContinueButton => continueButton;
        public Text Narrative => narrative;
        public M02BridgeInquiry Inquiry => inquiry;

        // Prefer undiscovered evidence, not an already-read clue or a hidden
        // archive choice. A player can still deliberately skip via Continue.
        public Button PreferredFocusTarget
        {
            get
            {
                if (disposed || inquiry.Location == M02EvidenceLocation.Finished)
                    return null;
                if (inquiry.Location == M02EvidenceLocation.FerryWarehouse)
                {
                    for (int i = 0; i < WarehouseClues.Length; i++)
                        if (clueButtons[i].gameObject.activeInHierarchy &&
                            !inquiry.Has(WarehouseClues[i]))
                            return clueButtons[i];
                }
                else if (inquiry.Location == M02EvidenceLocation.HarborArchive &&
                    clueButtons[0].gameObject.activeInHierarchy &&
                    !inquiry.Has(M02BridgeEvidence.WarReportDraft))
                    return clueButtons[0];

                return continueButton.gameObject.activeInHierarchy ? continueButton : null;
            }
        }

        public M02BridgeInquiryView(Transform parent,
            M02BridgeInquiry inquiry, Action<M02BridgeEvidence> onCompleted,
            Action<M02BridgeEvidence> onObserved = null)
        {
            if (parent == null)
                throw new ArgumentNullException(nameof(parent));
            if (inquiry == null)
                throw new ArgumentNullException(nameof(inquiry));
            if (onCompleted == null)
                throw new ArgumentNullException(nameof(onCompleted));
            if (inquiry.Location != M02EvidenceLocation.FerryWarehouse)
                throw new InvalidOperationException("The host must start M02's inquiry first.");

            this.inquiry = inquiry;
            this.onCompleted = onCompleted;
            this.onObserved = onObserved;
            latinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            canvasObject = SoundwellUiTheme.CreateOverlayCanvas(
                parent, "M02 bridge evidence canvas", 122, out authoredView,
                new Color(0.05f, 0.08f, 0.09f, 0.98f));

            heading = SoundwellUiTheme.CreateLabel(authoredView, 24,
                new Vector2(0f, 215f), new Vector2(840f, 50f), Ink);
            narrative = SoundwellUiTheme.CreateLabel(authoredView, 19,
                new Vector2(0f, 58f), new Vector2(835f, 205f), Ink);
            narrative.alignment = TextAnchor.MiddleLeft;
            for (int i = 0; i < clueButtons.Length; i++)
            {
                int captured = i;
                clueButtons[i] = SoundwellUiTheme.CreateButton(authoredView,
                    "M02 evidence " + i, new Vector2(0f, -69f - 53f * i),
                    new Vector2(820f, 46f), 17, out clueLabels[i],
                    ButtonFill, Ink);
                clueButtons[i].onClick.AddListener(() => Examine(captured));
            }
            continueButton = SoundwellUiTheme.CreateButton(authoredView,
                "M02 continue", new Vector2(0f, -225f),
                new Vector2(450f, 46f), 17, out continueLabel,
                ButtonFill, Ink);
            continueButton.onClick.AddListener(Advance);

            LayoutForViewportAspect((float)Mathf.Max(1, Screen.width) /
                Mathf.Max(1, Screen.height));
            Refresh();
            SoundwellUiLocalization.Changed += Refresh;
        }

        public Button GetEvidenceButton(int index)
        {
            if (index < 0 || index >= clueButtons.Length)
                throw new ArgumentOutOfRangeException(nameof(index));
            return clueButtons[index];
        }

        public void LayoutForViewportAspect(float aspect)
        {
            if (disposed)
                return;
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
            heading.font = narrative.font = continueLabel.font = font;
            heading.text = chinese ? "白水渡 · 断桥旧证" : "WHITEWATER · THE BROKEN BRIDGE";
            M02EvidenceLocation location = inquiry.Location;
            string intro = M02BridgeInquiryCopy.Intro(location, chinese);
            narrative.text = intro + (inquiry.LastExamined.HasValue
                ? "\n\n" + M02BridgeInquiryCopy.ClueText(inquiry.LastExamined.Value, chinese)
                : string.Empty);

            string[] en = { "Examine the severed cable", "Read the keeper's log", "Ask about the three bells" };
            string[] zh = { "检查断缆", "翻看守桥旧记录", "询问三声钟" };
            for (int i = 0; i < clueButtons.Length; i++)
            {
                bool warehouse = location == M02EvidenceLocation.FerryWarehouse;
                bool archive = location == M02EvidenceLocation.HarborArchive && i == 0;
                clueButtons[i].gameObject.SetActive(warehouse || archive);
                clueLabels[i].font = font;
                if (warehouse)
                    clueLabels[i].text = (chinese ? zh[i] : en[i]) +
                        (inquiry.Has(WarehouseClues[i]) ? (chinese ? "（已查）" : " (seen)") : string.Empty);
                else if (archive)
                    clueLabels[i].text = (chinese ? "调阅战报底稿" : "Read the draft battle report") +
                        (inquiry.Has(M02BridgeEvidence.WarReportDraft) ? (chinese ? "（已查）" : " (seen)") : string.Empty);
            }
            continueLabel.text = location == M02EvidenceLocation.FerryWarehouse
                ? (chinese ? "继续到港务署旧档案室" : "Continue to the harbor archive")
                : location == M02EvidenceLocation.HarborArchive
                ? (chinese ? "结束调查" : "Finish investigating")
                : (chinese ? "调查结束" : "Inquiry complete");
            continueButton.gameObject.SetActive(location != M02EvidenceLocation.Finished);
            SoundwellInputAdapter.KeepDialogFocus(canvasObject.transform, PreferredFocusTarget);
        }

        private void Examine(int index)
        {
            if (disposed || completed)
                return;
            bool newlyObserved = false;
            if (inquiry.Location == M02EvidenceLocation.FerryWarehouse)
            {
                M02BridgeEvidence clue = WarehouseClues[index];
                newlyObserved = inquiry.Examine(clue);
                if (newlyObserved)
                    onObserved?.Invoke(clue);
            }
            else if (inquiry.Location == M02EvidenceLocation.HarborArchive && index == 0)
            {
                newlyObserved = inquiry.Examine(M02BridgeEvidence.WarReportDraft);
                if (newlyObserved)
                    onObserved?.Invoke(M02BridgeEvidence.WarReportDraft);
            }

            Refresh();
            // KeepDialogFocus intentionally respects an existing selected
            // button. After a fresh discovery, advance keyboard/gamepad
            // selection to the next unread clue (or Continue) instead.
            if (newlyObserved && !disposed && Application.isPlaying &&
                EventSystem.current != null && !EventSystem.current.alreadySelecting &&
                EventSystem.current.currentSelectedGameObject == clueButtons[index].gameObject)
            {
                Button next = PreferredFocusTarget;
                if (next != null && next != clueButtons[index])
                    EventSystem.current.SetSelectedGameObject(next.gameObject);
            }
        }

        private void Advance()
        {
            if (disposed || completed)
                return;
            bool enteringArchive = inquiry.Location == M02EvidenceLocation.FerryWarehouse;
            if (enteringArchive)
                inquiry.LeaveWarehouse();
            else if (inquiry.Location == M02EvidenceLocation.HarborArchive)
            {
                inquiry.LeaveArchive();
                completed = true;
                Action<M02BridgeEvidence> listener = onCompleted;
                onCompleted = null;
                listener?.Invoke(inquiry.Found);
            }
            Refresh();

            // Continue stays visible in both rooms. KeepDialogFocus preserves
            // the current selection, but a second gamepad Submit would
            // immediately leave the archive without showing its unread draft.
            // Direct focus to the new evidence, without reading it for free.
            if (enteringArchive && !disposed && Application.isPlaying)
            {
                EventSystem system = EventSystem.current;
                if (system != null && !system.alreadySelecting &&
                    system.currentSelectedGameObject == continueButton.gameObject)
                {
                    Button first = PreferredFocusTarget;
                    if (first != null && first != continueButton)
                        system.SetSelectedGameObject(first.gameObject);
                }
            }
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            onCompleted = null;
            onObserved = null;
            SoundwellUiLocalization.Changed -= Refresh;
            foreach (Button button in clueButtons)
                if (button != null)
                    button.onClick.RemoveAllListeners();
            if (continueButton != null)
                continueButton.onClick.RemoveAllListeners();
            SoundwellUiTheme.SafeDestroyCanvas(canvasObject);
        }
    }
}
