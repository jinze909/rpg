using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Rpg.Soundwell
{
    /// <summary>
    /// UGUI conversation over M02's harbor-desk choices. It emits one
    /// observed result to its host, which is responsible for Makinom saves.
    /// </summary>
    public sealed class M02HarborDeskView : IDisposable
    {
        private readonly M02HarborDesk desk;
        private readonly GameObject canvasObject;
        private readonly RectTransform authoredView;
        private readonly Text title;
        private readonly Text dialogue;
        private readonly Text[] actionLabels = new Text[4];
        private readonly Button[] actionButtons = new Button[4];
        private readonly Button leaveButton;
        private readonly Text leaveLabel;
        private readonly Font latinFont;
        private Action<M02HarborDeskOutcome> onFinished;
        private bool disposed;
        private bool delivered;

        public M02HarborDesk Desk => desk;
        public Text Dialogue => dialogue;
        public Button LeaveButton => leaveButton;

        // Prefer subjects the player has not heard yet. Everything remains
        // independently selectable for revisits, but default focus should
        // not make keyboard/gamepad users repeat one already-heard response.
        public Button PreferredFocusTarget
        {
            get
            {
                if (disposed || delivered)
                    return null;
                for (int i = 0; i < actionButtons.Length; i++)
                    if (actionButtons[i] != null &&
                        actionButtons[i].gameObject.activeInHierarchy &&
                        !ActionAlreadyDiscussed((M02HarborDeskAction)i))
                        return actionButtons[i];
                return leaveButton != null && leaveButton.gameObject.activeInHierarchy
                    ? leaveButton : null;
            }
        }

        private bool ActionAlreadyDiscussed(M02HarborDeskAction action)
        {
            switch (action)
            {
                case M02HarborDeskAction.PresentLivingRegistration:
                    return desk.LivingRegistryStamped;
                case M02HarborDeskAction.AskAboutQiQueShip:
                    return desk.QiQueDebtDiscovered;
                case M02HarborDeskAction.AskAboutLuJunEscort:
                    return desk.LuJunEscortDiscussed;
                case M02HarborDeskAction.AssistTransferList:
                    return desk.TransferAssistanceAccepted;
                default:
                    return false;
            }
        }

        public M02HarborDeskView(Transform parent, M01PensionChoice pensionChoice,
            Action<M02HarborDeskOutcome> onFinished)
        {
            if (parent == null)
                throw new ArgumentNullException(nameof(parent));
            if (onFinished == null)
                throw new ArgumentNullException(nameof(onFinished));
            desk = new M02HarborDesk(pensionChoice);
            desk.Begin();
            this.onFinished = onFinished;
            latinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            canvasObject = SoundwellUiTheme.CreateOverlayCanvas(
                parent, "M02 harbor desk canvas", 123, out authoredView,
                new Color(0.05f, 0.07f, 0.09f, 0.98f));

            title = SoundwellUiTheme.CreateLabel(authoredView, 24,
                new Vector2(0f, 216f), new Vector2(840f, 45f));
            dialogue = SoundwellUiTheme.CreateLabel(authoredView, 18,
                new Vector2(0f, 74f), new Vector2(840f, 206f));
            dialogue.alignment = TextAnchor.MiddleLeft;
            for (int i = 0; i < actionButtons.Length; i++)
            {
                int selected = i;
                actionButtons[i] = SoundwellUiTheme.CreateButton(authoredView,
                    "M02 office action " + i, new Vector2(0f, -58f - i * 43f),
                    new Vector2(825f, 39f), 16, out actionLabels[i]);
                actionButtons[i].onClick.AddListener(() => Choose((M02HarborDeskAction)selected));
            }
            leaveButton = SoundwellUiTheme.CreateButton(authoredView,
                "M02 harbor leave", new Vector2(0f, -230f),
                new Vector2(400f, 40f), 16, out leaveLabel);
            leaveButton.onClick.AddListener(Leave);

            LayoutForViewportAspect((float)Mathf.Max(Screen.width, 1) /
                Mathf.Max(Screen.height, 1));
            Refresh();
            SoundwellUiLocalization.Changed += Refresh;
        }

        public Button GetActionButton(int index)
        {
            if (index < 0 || index >= actionButtons.Length)
                throw new ArgumentOutOfRangeException(nameof(index));
            return actionButtons[index];
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
            if (canvasObject == null || title == null)
            {
                Dispose();
                return;
            }
            bool chinese = SoundwellUiLocalization.Language == SoundwellUiLocalization.Chinese;
            Font font = SoundwellUiLocalization.PresentationFont(latinFont);
            title.font = dialogue.font = leaveLabel.font = font;
            title.text = chinese ? "临潮港务署 · 船坞与旧账" : "HARBOR OFFICE · SHIPS AND DEBTS";
            string intro = M02HarborDeskDialogue.Introduction(desk, chinese);
            dialogue.text = intro + (desk.LastAction.HasValue
                ? "\n\n" + M02HarborDeskDialogue.Result(desk.LastAction.Value, chinese)
                : string.Empty);
            string[] en = { "Present living-registration papers", "Ask about Qi Que's ship", "Ask about Lu Jun's escort", "Help check the transfer list" };
            string[] zh = { "递交活人户籍呈文", "询问祁雀的船贷", "询问陆钧的护卫", "答应协理移交名册" };
            for (int i = 0; i < actionButtons.Length; i++)
            {
                actionLabels[i].font = font;
                actionLabels[i].text = (chinese ? zh[i] : en[i]) +
                    (ActionAlreadyDiscussed((M02HarborDeskAction)i)
                        ? (chinese ? "（已谈）" : " (discussed)") : string.Empty);
                actionButtons[i].gameObject.SetActive(!delivered &&
                    (i != 0 || desk.CanPresentLivingPapers));
            }
            leaveLabel.text = chinese ? "离开港务署" : "Leave the harbor office";
            leaveButton.gameObject.SetActive(!delivered);
            if (!delivered)
                SoundwellInputAdapter.KeepDialogFocus(canvasObject.transform,
                    PreferredFocusTarget);
        }

        private void Choose(M02HarborDeskAction action)
        {
            if (disposed || delivered)
                return;
            // UnityEvent.Invoke can still call an inactive button. Re-check
            // the domain gate to prevent a second or unauthorized stamp.
            if (action == M02HarborDeskAction.PresentLivingRegistration &&
                !desk.CanPresentLivingPapers)
                return;
            bool newlyDiscussed = desk.Act(action);
            Refresh();
            // The chosen button remains available to reread its dialogue, so
            // KeepDialogFocus intentionally keeps it selected. After a new
            // answer, shift a pad/keyboard user to the next unheard subject
            // without silently activating it or changing any story facts.
            if (newlyDiscussed && Application.isPlaying &&
                EventSystem.current != null && !EventSystem.current.alreadySelecting &&
                EventSystem.current.currentSelectedGameObject ==
                    actionButtons[(int)action].gameObject)
            {
                Button next = PreferredFocusTarget;
                if (next != null && next.gameObject.activeInHierarchy)
                    EventSystem.current.SetSelectedGameObject(next.gameObject);
            }
        }

        private void Leave()
        {
            if (disposed || delivered)
                return;
            // Lock against reentrant UGUI submits before persisting through the
            // host. A failed write must keep the harbor desk retryable instead
            // of discarding the player's stamped papers and discoveries.
            delivered = true;
            Refresh();
            try
            {
                desk.Finish(onFinished);
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
            foreach (Button b in actionButtons)
                if (b != null)
                    b.onClick.RemoveAllListeners();
            if (leaveButton != null)
                leaveButton.onClick.RemoveAllListeners();
            SoundwellUiTheme.SafeDestroyCanvas(canvasObject);
        }
    }
}
