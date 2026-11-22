using System;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Bottom-safe UGUI interaction for the authored M09 night approach.
    /// The host supplies the household size observed at roll call and an
    /// independent actual-boarding check; this view cannot mint a rescue.
    /// </summary>
    public sealed class M09EastShoreNightCrossingView : IDisposable
    {
        private readonly GameObject canvasObject;
        private readonly RectTransform panel;
        private readonly Font latinFont;
        private readonly Text heading;
        private readonly Text body;
        private readonly Button[] choices = new Button[3];
        private readonly Text[] labels = new Text[3];
        private readonly Button finishButton;
        private readonly Text finishLabel;
        private readonly M09EastShoreNightCrossingSession session;
        private readonly int observedHouseholdSize;
        private readonly Func<int, bool> confirmActuallyBoarded;
        private Action<M09EastShoreNightCrossingSession> onCompleted;
        private bool delivered;
        private bool disposed;

        public M09EastShoreNightCrossingSession Session => session;
        public RectTransform Panel => panel;
        public Text Body => body;
        public Button ListenOrProceedButton => choices[0];
        public Button RiskOrRefuseButton => choices[1];
        public Button DeferButton => choices[2];
        public Button FinishButton => finishButton;
        public bool IsDisposed => disposed || CanvasDestroyed();

        public M09EastShoreNightCrossingView(
            Transform parent,
            M09EastShoreNightCrossingSession session,
            int observedHouseholdSize,
            Func<int, bool> confirmActuallyBoarded,
            Action<M09EastShoreNightCrossingSession> onCompleted)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            if (observedHouseholdSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(observedHouseholdSize));
            this.confirmActuallyBoarded = confirmActuallyBoarded ??
                throw new ArgumentNullException(nameof(confirmActuallyBoarded));
            this.onCompleted = onCompleted ?? throw new ArgumentNullException(nameof(onCompleted));
            this.observedHouseholdSize = observedHouseholdSize;

            latinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            canvasObject = new GameObject("M09 east shore night ferry canvas",
                typeof(RectTransform), typeof(Canvas));
            canvasObject.transform.SetParent(parent, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 139;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(
                SoundwellUiTheme.ReferenceWidth, SoundwellUiTheme.ReferenceHeight);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();

            var holder = new GameObject("M09 night ferry bottom panel", typeof(RectTransform));
            holder.transform.SetParent(canvasObject.transform, false);
            panel = (RectTransform)holder.transform;
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0f);
            panel.anchoredPosition = new Vector2(0f, 130f);
            panel.sizeDelta = new Vector2(904f, 248f);
            SoundwellUiTheme.CreatePanel(panel, "M09 night ferry border",
                new Vector2(904f, 248f), new Color(0.64f, 0.56f, 0.42f, 1f));
            SoundwellUiTheme.CreatePanel(panel, "M09 night ferry backing",
                new Vector2(900f, 244f), new Color(0.06f, 0.09f, 0.11f, 0.97f));
            var ink = new Color(0.93f, 0.89f, 0.81f, 1f);
            heading = SoundwellUiTheme.CreateLabel(panel, 19,
                new Vector2(0f, 94f), new Vector2(842f, 28f), ink);
            heading.alignment = TextAnchor.MiddleLeft;
            body = SoundwellUiTheme.CreateLabel(panel, 15,
                new Vector2(0f, 23f), new Vector2(842f, 106f), ink);
            body.alignment = TextAnchor.UpperLeft;
            body.horizontalOverflow = HorizontalWrapMode.Wrap;

            for (int i = 0; i < choices.Length; i++)
            {
                int index = i;
                choices[i] = SoundwellUiTheme.CreateButton(
                    panel, "M09 night ferry choice " + i,
                    new Vector2(-280f + i * 280f, -55f),
                    new Vector2(270f, 36f), 14, out labels[i]);
                choices[i].onClick.AddListener(() => Choose(index));
            }
            finishButton = SoundwellUiTheme.CreateButton(
                panel, "M09 night ferry commit outcome",
                new Vector2(0f, -102f), new Vector2(320f, 36f),
                15, out finishLabel);
            finishButton.onClick.AddListener(Finish);

            LayoutForViewportAspect((float)Mathf.Max(1, Screen.width) /
                Mathf.Max(1, Screen.height));
            SoundwellUiLocalization.Changed += Refresh;
            Refresh();
        }

        public void LayoutForViewportAspect(float aspect)
        {
            if (disposed) return;
            if (CanvasDestroyed())
            {
                Dispose();
                return;
            }
            SoundwellUiTheme.ApplyViewportAspectScale(panel, aspect);
        }

        public void Refresh()
        {
            if (disposed) return;
            if (CanvasDestroyed())
            {
                Dispose();
                return;
            }
            bool zh = SoundwellUiLocalization.Language == SoundwellUiLocalization.Chinese;
            Font font = SoundwellUiLocalization.PresentationFont(latinFont);
            heading.font = body.font = finishLabel.font = font;
            for (int i = 0; i < labels.Length; i++)
                labels[i].font = font;
            for (int i = 0; i < choices.Length; i++)
            {
                choices[i].gameObject.SetActive(false);
                choices[i].interactable = false;
            }
            finishButton.gameObject.SetActive(false);
            finishButton.interactable = false;

            heading.text = zh ? "M09 · 东岸渔村夜航" : "M09 · EAST SHORE NIGHT FERRY";
            string berths = zh ? "本艇实际空铺：" : "Verified free berths: ";
            switch (session.Stage)
            {
                case M09NightCrossingStage.FogApproach:
                    body.text = zh
                        ? "夜雾中有巡逻船。先辨桨声，别把未知航道当成安全航道。"
                        : "Patrol craft moves through the fog. Listen for oars before committing the ferry.";
                    Show(0, zh ? "辨认雾中桨声" : "Listen for oars");
                    Show(1, zh ? "冒险直接靠岸" : "Risk rowing in blind");
                    break;
                case M09NightCrossingStage.PatrolPassing:
                    body.text = zh
                        ? "巡逻艇就在附近。全船静音，等它驶远再划桨。"
                        : "Patrol oars are close. Keep the boat silent until they pass.";
                    Show(0, zh ? "保持静音等待" : "Wait in silence");
                    Show(1, zh ? "立刻划向岸边" : "Row before patrol passes");
                    break;
                case M09NightCrossingStage.PatrolAlerted:
                    body.text = zh
                        ? "过早划桨引起了巡逻艇注意。先隐蔽，不能直接算靠岸成功。"
                        : "Early oar strokes alerted the patrol. Hide before trying the approach again.";
                    Show(0, zh ? "隐蔽，重新听桨声" : "Hide and listen again");
                    break;
                case M09NightCrossingStage.ClearApproach:
                    body.text = zh
                        ? "桨声渐远，通向小码头的水道暂时安全。"
                        : "The oars fade. The channel to the small jetty is clear for now.";
                    Show(0, zh ? "划向东岸小码头" : "Approach east-shore jetty");
                    break;
                case M09NightCrossingStage.JettyRollCall:
                    body.text = (zh
                        ? "到了小码头，先问蕊亚一家本人愿不愿走。"
                        : "At the jetty, ask Ruiya's household whether they agree to board.") +
                        "\n" + berths + session.AvailableBerths;
                    Show(0, zh ? "本人同意本班接应" : "Household consents");
                    Show(1, zh ? "本人不愿同行" : "Household declines");
                    Show(2, zh ? "接驳条件不具备，延后" : "Transfer unavailable: defer");
                    break;
                case M09NightCrossingStage.AwaitingActualBoarding:
                    body.text = (zh
                        ? "已获本人同意，但同意不是登船。船工逐人核对，确实登船才勾名。"
                        : "Consent is not boarding. Match each person at the gangplank before marking rescued.") +
                        "\n" + berths + session.AvailableBerths;
                    Show(0, zh ? "核实全家实际登船" : "Verify actual boarding");
                    Show(1, zh ? "未完成登船，继续待接" : "Unable to board: defer");
                    break;
                case M09NightCrossingStage.Completed:
                    body.text = session.Outcome == M09RuiyaOutcome.Rescued
                        ? (zh ? "已核实蕊亚一家实际登船，共 " : "Actual household boardings verified: ") +
                            session.ActuallyBoardedCount
                        : session.Outcome == M09RuiyaOutcome.Deferred
                            ? (zh ? "当前无法接出，保留待接名单；无人被记作已登船。"
                                  : "Transfer deferred. The household remains pending, not boarded.")
                            : (zh ? "本人拒绝这班接应；不得记作已接出。"
                                  : "The household declined; nobody may be marked rescued.");
                    finishLabel.text = zh ? "确认实况并交给存档宿主" : "Submit verified outcome to host";
                    finishButton.gameObject.SetActive(!delivered);
                    finishButton.interactable = !delivered;
                    break;
            }
            if (!delivered)
            {
                Button preferred = finishButton.interactable ? finishButton : choices[0];
                SoundwellInputAdapter.KeepDialogFocus(canvasObject.transform, preferred);
            }
        }

        private void Show(int i, string label)
        {
            labels[i].text = label;
            choices[i].gameObject.SetActive(true);
            choices[i].interactable = true;
        }

        private void Choose(int index)
        {
            if (disposed || delivered || CanvasDestroyed() || index < 0 || index >= 3)
                return;
            // Hidden button callbacks are not an alternate gameplay route.
            if (!choices[index].gameObject.activeSelf || !choices[index].interactable)
                return;
            switch (session.Stage)
            {
                case M09NightCrossingStage.FogApproach:
                    if (index == 0) session.TryListenForOars();
                    else if (index == 1) session.TryRowToJetty();
                    break;
                case M09NightCrossingStage.PatrolPassing:
                    if (index == 0) session.TryHoldSilent();
                    else if (index == 1) session.TryRowToJetty();
                    break;
                case M09NightCrossingStage.PatrolAlerted:
                    if (index == 0) session.TryHideAndRelisten();
                    break;
                case M09NightCrossingStage.ClearApproach:
                    if (index == 0) session.TryRowToJetty();
                    break;
                case M09NightCrossingStage.JettyRollCall:
                    if (index == 0)
                        session.TryRecordRuiyaConsent(true, observedHouseholdSize);
                    else if (index == 1)
                        session.TryRecordRuiyaConsent(false, observedHouseholdSize);
                    else if (index == 2)
                        session.TryDeferForUnmetTransfer();
                    break;
                case M09NightCrossingStage.AwaitingActualBoarding:
                    if (index == 0)
                    {
                        // Only the host knows whether each person actually
                        // crossed the gangplank; a UI click alone proves nothing.
                        if (confirmActuallyBoarded(observedHouseholdSize))
                            session.TryConfirmActualBoarding(observedHouseholdSize);
                    }
                    else if (index == 1)
                        session.TryDeferForUnmetTransfer();
                    break;
            }
            Refresh();
        }

        private void Finish()
        {
            if (disposed || delivered || CanvasDestroyed() ||
                !session.IsComplete || !finishButton.interactable)
                return;
            // A failed Makinom write or interrupted host callback must leave
            // exactly this same completed outcome retryable.
            onCompleted(session);
            delivered = true;
            onCompleted = null;
            Refresh();
        }

        private bool CanvasDestroyed()
        {
            if (canvasObject == null || panel == null || heading == null ||
                body == null || finishButton == null || finishLabel == null)
                return true;
            for (int i = 0; i < choices.Length; i++)
                if (choices[i] == null || labels[i] == null) return true;
            return false;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            onCompleted = null;
            SoundwellUiLocalization.Changed -= Refresh;
            for (int i = 0; i < choices.Length; i++)
                if (choices[i] != null) choices[i].onClick.RemoveAllListeners();
            if (finishButton != null) finishButton.onClick.RemoveAllListeners();
            SoundwellUiTheme.SafeDestroyCanvas(canvasObject);
        }
    }
}
