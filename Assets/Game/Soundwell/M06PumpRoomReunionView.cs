using System;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Bottom-anchored UGUI dialogue and decision panel for M06 ("她没有在原地等你").
    /// Steps cleanly through Shen Li's vessel/draft briefing (N77), pump-room
    /// witnesses, optional family-letter delivery (N76), Oulin's retaliation
    /// truth, and the four mutually exclusive rescue plans with save retry.
    /// </summary>
    public sealed class M06PumpRoomReunionView : IDisposable
    {
        private readonly GameObject canvasObject;
        private readonly RectTransform panel;
        private readonly Font latinFont;
        private readonly Text heading;
        private readonly Text body;
        private readonly Button[] choiceButtons = new Button[4];
        private readonly Text[] choiceLabels = new Text[4];
        private readonly Button advanceButton;
        private readonly Text advanceLabel;
        private readonly M06PumpRoomReunionSession session;
        private Action<M06RescuePlan> onPlanConfirmed;
        private bool submittingPlan;
        private bool disposed;

        public M06PumpRoomReunionSession Session => session;
        public RectTransform Panel => panel;
        public bool IsDisposed
        {
            get
            {
                if (!disposed && CanvasDestroyed()) Dispose();
                return disposed;
            }
        }
        public Text Heading => heading;
        public Text Body => body;
        public Button AdvanceButton => advanceButton;
        public Button GreetingBringBackButton => choiceButtons[0];
        public Button GreetingWrenchButton => choiceButtons[1];
        public Button GreetingWithdrawalOrChannelButton => choiceButtons[2];
        public Button OfficialHandoverButton => choiceButtons[0];
        public Button CargoCoverButton => choiceButtons[1];
        public Button MinerShutdownButton => choiceButtons[2];
        public Button ForceBreakoutButton => choiceButtons[3];
        public Button ShenAskButton => choiceButtons[0];
        public Button ShenHelpButton => choiceButtons[1];
        public Button ShenWaitButton => choiceButtons[2];
        public Button TaoJiLetterButton => choiceButtons[0];
        public Button LedgerLetterButton => choiceButtons[1];
        public Button ReturnedLetterButton => choiceButtons[2];

        public M06PumpRoomReunionView(
            Transform parent,
            M06PumpRoomReunionSession session,
            Action<M06RescuePlan> onPlanConfirmed)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.onPlanConfirmed = onPlanConfirmed ?? throw new ArgumentNullException(nameof(onPlanConfirmed));

            latinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            canvasObject = new GameObject("M06 pump room reunion canvas",
                typeof(RectTransform), typeof(Canvas));
            canvasObject.transform.SetParent(parent, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 134;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(
                SoundwellUiTheme.ReferenceWidth, SoundwellUiTheme.ReferenceHeight);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();

            var holder = new GameObject("M06 reunion bottom panel", typeof(RectTransform));
            holder.transform.SetParent(canvasObject.transform, false);
            panel = (RectTransform)holder.transform;
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0f);
            panel.sizeDelta = new Vector2(904f, 244f);
            panel.anchoredPosition = new Vector2(0f, 132f);

            SoundwellUiTheme.CreatePanel(panel, "M06 reunion border",
                new Vector2(904f, 244f), new Color(0.64f, 0.56f, 0.42f, 1f));
            SoundwellUiTheme.CreatePanel(panel, "M06 reunion backing",
                new Vector2(900f, 240f), new Color(0.06f, 0.09f, 0.11f, 0.97f));
            Color ink = new Color(0.93f, 0.89f, 0.81f, 1f);

            heading = SoundwellUiTheme.CreateLabel(panel, 19,
                new Vector2(0f, 96f), new Vector2(842f, 28f), ink);
            heading.alignment = TextAnchor.MiddleLeft;

            body = SoundwellUiTheme.CreateLabel(panel, 15,
                new Vector2(0f, 36f), new Vector2(842f, 84f), ink);
            body.alignment = TextAnchor.UpperLeft;
            body.horizontalOverflow = HorizontalWrapMode.Wrap;

            var positions = new[]
            {
                new Vector2(-216f, -46f),
                new Vector2(216f, -46f),
                new Vector2(-216f, -92f),
                new Vector2(216f, -92f)
            };
            for (int i = 0; i < 4; i++)
            {
                int index = i;
                choiceButtons[i] = SoundwellUiTheme.CreateButton(
                    panel, "M06 reunion choice " + i, positions[i],
                    new Vector2(414f, 36f), 14, out choiceLabels[i]);
                choiceButtons[i].onClick.AddListener(() => HandleChoiceClick(index));
            }

            advanceButton = SoundwellUiTheme.CreateButton(
                panel, "M06 reunion advance", new Vector2(302f, -92f),
                new Vector2(222f, 36f), 15, out advanceLabel);
            advanceButton.onClick.AddListener(HandleAdvanceClick);

            LayoutForViewportAspect((float)Mathf.Max(1, Screen.width) /
                Mathf.Max(1, Screen.height));
            Refresh();
            SoundwellUiLocalization.Changed += Refresh;
        }

        public void LayoutForViewportAspect(float aspect)
        {
            if (disposed) return;
            if (CanvasDestroyed())
            {
                Dispose();
                return;
            }
            if (float.IsNaN(aspect) || float.IsInfinity(aspect) || aspect <= 0f)
                return;
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
            heading.font = body.font = advanceLabel.font = font;
            for (int i = 0; i < choiceLabels.Length; i++)
                choiceLabels[i].font = font;

            for (int i = 0; i < choiceButtons.Length; i++)
            {
                choiceButtons[i].gameObject.SetActive(false);
                choiceButtons[i].interactable = false;
            }
            advanceButton.gameObject.SetActive(false);
            advanceButton.interactable = false;

            switch (session.Stage)
            {
                case M06ReunionStage.VesselBriefing:
                    heading.text = zh ? "地下主泵房 · 重逢核船" : "UNDERGROUND PUMP HALL · REUNION";
                    body.text = (zh ? "沈砾：「几个人？吃水线多深？」\n" : "Shen Li: \"How many crew? What is the draft?\"\n") +
                        session.FormatVesselBriefing(zh);
                    ShowChoice(0, zh ? "沈砾，我来接你了。" : "Shen Li, I came to bring you back.");
                    ShowChoice(1, zh ? "三个月没见，你给扳手起的名字还是那么难听。" : "Three months gone and your wrench names are still ugly.");
                    ShowChoice(2, session.FormatWithdrawalOrChannelChoice(zh));
                    SoundwellInputAdapter.KeepDialogFocus(canvasObject.transform, choiceButtons[0]);
                    break;

                case M06ReunionStage.PumpRoomWitnesses:
                    heading.text = zh ? "地下主泵房 · 泵房里的人" : "UNDERGROUND PUMP HALL · WITNESSES";
                    body.text = zh
                        ? "小七蹲在泵下递工具，梁伯在角落大火煎盐鱼；管事易达巡查时在本子上划了一道，留下一包茶：「鲁斯先生问沈工好。」沈砾没碰，也没扔。"
                        : "Xiaoqi hands tools beneath the pump while Uncle Liang fries salt fish; Steward Yida marks his ledger and leaves a tea packet from Rus. Shen Li neither touches nor discards it.";
                    ShowAdvance(zh ? "继续" : "Continue");
                    break;

                case M06ReunionStage.LetterHandoff:
                    heading.text = zh ? "矿营班组 · 亲手送信" : "MINE CAMP · FAMILY LETTERS";
                    body.text = zh
                        ? $"按封交付目前能找到的三位收件人。已处理 {session.LettersHandledCount}/3；其余五封仍在信包里，不视为已送达。"
                        : $"Hand the three located letters to their recipients. Handled {session.LettersHandledCount}/3; the other five remain in the packet and are not marked delivered.";
                    PositionLetterHandoffControls();
                    ShowChoice(0, FormatLetterChoice(M06FamilyLetterRecipient.TaoJi, zh),
                        !session.IsLetterHandled(M06FamilyLetterRecipient.TaoJi));
                    ShowChoice(1, FormatLetterChoice(M06FamilyLetterRecipient.LedgerMiner, zh),
                        !session.IsLetterHandled(M06FamilyLetterRecipient.LedgerMiner));
                    ShowChoice(2, FormatLetterChoice(M06FamilyLetterRecipient.PendingMiner, zh),
                        !session.IsLetterHandled(M06FamilyLetterRecipient.PendingMiner));
                    if (session.LettersHandled)
                        ShowAdvance(zh ? "去换班廊道找欧林" : "Continue to Oulin in the shift corridor");
                    else if (!session.IsLetterHandled(M06FamilyLetterRecipient.TaoJi))
                        SoundwellInputAdapter.KeepDialogFocus(canvasObject.transform, choiceButtons[0]);
                    else if (!session.IsLetterHandled(M06FamilyLetterRecipient.LedgerMiner))
                        SoundwellInputAdapter.KeepDialogFocus(canvasObject.transform, choiceButtons[1]);
                    else
                        SoundwellInputAdapter.KeepDialogFocus(canvasObject.transform, choiceButtons[2]);
                    break;

                case M06ReunionStage.OulinRetaliationTruth:
                    heading.text = zh ? "换班廊道 · 欧林的实话" : "SHIFT CORRIDOR · OULIN'S TRUTH";
                    body.text = zh
                        ? "欧林低声告诉你：沈砾十年修泵让三百人吃饱，也让鲁斯的账更好看；三个月前你逃走后，鲁斯扣了半数口粮报复，老张等两位老矿工没熬过去。"
                        : "Oulin whispers that Shen Li's ten years of pump repairs fed three hundred miners while padding Rus's ledger; after your escape three months ago, Rus halved rations and two old miners including Old Zhang did not survive.";
                    ShowAdvance(zh ? "回到大泵前商定方案" : "Return to plan rescue");
                    break;

                case M06ReunionStage.ShenResponse:
                    heading.text = zh ? "地下主泵房 · 回应沈砾" : "UNDERGROUND PUMP HALL · ANSWER SHEN LI";
                    body.text = zh
                        ? "欧林说起断粮后没能活下来的两位矿工。你决定怎样回应沈砾？"
                        : "Oulin tells you about the two miners who died after the ration cuts. How do you respond to Shen Li?";
                    ShowChoice(0, zh ? "直接问她那两位老矿工的事" : "Ask her directly about the two miners");
                    ShowChoice(1, zh ? "先帮她把眼前的阀修完再谈" : "Help finish the valve repair before talking");
                    ShowChoice(2, zh ? "等她自己开口" : "Give her time to speak");
                    SoundwellInputAdapter.KeepDialogFocus(canvasObject.transform, choiceButtons[0]);
                    break;

                case M06ReunionStage.RescuePlanDecision:
                    heading.text = zh ? "地下主泵房 · 敲定营救方案" : "UNDERGROUND PUMP HALL · RESCUE PLAN";
                    body.text = (zh
                        ? "沈砾指着主排水泵：「无准备停转，下层五百人三天水漫。不管走哪条路，都不能为了出港淹了后面的人。」"
                        : "Shen Li points at the main drainage pump: \"Stop this unprepared and five hundred below flood in three days. Whichever route we pick, we do not drown those left behind.\"")
                        + "\n" + FormatShenReply(zh);
                    ShowChoice(0, M06PumpRoomReunionSession.FormatPlanLabel(M06RescuePlan.OfficialHandover, zh), !submittingPlan);
                    ShowChoice(1, M06PumpRoomReunionSession.FormatPlanLabel(M06RescuePlan.CargoCover, zh), !submittingPlan);
                    ShowChoice(2, M06PumpRoomReunionSession.FormatPlanLabel(M06RescuePlan.MinerShutdown, zh), !submittingPlan);
                    ShowChoice(3, M06PumpRoomReunionSession.FormatPlanLabel(M06RescuePlan.ForceBreakout, zh), !submittingPlan);
                    if (!submittingPlan)
                        SoundwellInputAdapter.KeepDialogFocus(canvasObject.transform, choiceButtons[0]);
                    break;

                case M06ReunionStage.Completed:
                    heading.text = zh ? "地下主泵房 · 营救方案已定" : "UNDERGROUND PUMP HALL · PLAN SETTLED";
                    body.text = session.ConfirmedPlan.HasValue
                        ? M06PumpRoomReunionSession.FormatPlanLabel(session.ConfirmedPlan.Value, zh)
                        : string.Empty;
                    break;
            }
        }

        private string FormatLetterChoice(M06FamilyLetterRecipient recipient, bool zh)
        {
            M06FamilyLetterStatus status = session.LetterDelivery.StatusFor(recipient);
            switch (recipient)
            {
                case M06FamilyLetterRecipient.TaoJi:
                    if (status == M06FamilyLetterStatus.DeliveredAndReadTwice)
                        return zh ? "陶霁 · 已交付，她读了两遍" : "Tao Ji · delivered and read twice";
                    return zh ? "把「我把门槛修好了」交给女木匠陶霁"
                        : "Give the doorstep-repair letter to carpenter Tao Ji";
                case M06FamilyLetterRecipient.LedgerMiner:
                    if (status == M06FamilyLetterStatus.DeliveredUnread)
                        return zh ? "账目家书 · 已收下，尚未拆读" : "Ledger letter · accepted, still unopened";
                    return zh ? "把整页账目的家书交给矿工"
                        : "Hand the household-ledger letter to the miner";
                case M06FamilyLetterRecipient.PendingMiner:
                    if (status == M06FamilyLetterStatus.ReturnedPending)
                        return zh ? "第三封 · 已退回，暂记「待投」" : "Third letter · returned, marked pending";
                    return zh ? "把第三封信递给那位矿工"
                        : "Offer the third letter to the miner";
                default:
                    throw new ArgumentOutOfRangeException(nameof(recipient));
            }
        }

        private void PositionLetterHandoffControls()
        {
            ((RectTransform)choiceButtons[2].transform).anchoredPosition =
                new Vector2(-216f, -92f);
            ((RectTransform)advanceButton.transform).anchoredPosition =
                new Vector2(216f, -92f);
        }

        private string FormatShenReply(bool zh)
        {
            switch (session.SelectedShenResponse)
            {
                case M06ShenResponse.AskAboutMiners:
                    return zh ? "她说：「顶得住和该顶是两回事。」"
                        : "She admits: enduring something and deserving it are different things.";
                case M06ShenResponse.HelpRepairValve:
                    return zh ? "修好阀后，她低声说起老张走的那天。"
                        : "After repairing the valve, she quietly recounts Old Zhang's last day.";
                case M06ShenResponse.LetHerSpeak:
                    return zh ? "她说：「先把这班人送出去，行吗？」"
                        : "She asks to get this shift safely out first.";
                default:
                    return string.Empty;
            }
        }

        private void ShowChoice(int index, string text, bool interactable = true)
        {
            choiceLabels[index].text = text;
            choiceButtons[index].gameObject.SetActive(true);
            choiceButtons[index].interactable = interactable;
        }

        private void ShowAdvance(string text)
        {
            advanceLabel.text = text;
            advanceButton.gameObject.SetActive(true);
            advanceButton.interactable = true;
            SoundwellInputAdapter.KeepDialogFocus(canvasObject.transform, advanceButton);
        }

        private void HandleChoiceClick(int index)
        {
            if (disposed) return;
            if (CanvasDestroyed())
            {
                Dispose();
                return;
            }

            if (index < 0 || index >= choiceButtons.Length ||
                !choiceButtons[index].IsActive() || !choiceButtons[index].IsInteractable())
                return;

            if (session.Stage == M06ReunionStage.LetterHandoff && index >= 0 && index <= 2)
            {
                if (!session.TryDeliverCarriedLetter((M06FamilyLetterRecipient)index))
                    throw new InvalidOperationException(
                        "Unable to record the current M06 family-letter handoff.");
                Refresh();
                return;
            }

            if (session.Stage == M06ReunionStage.VesselBriefing && index >= 0 && index <= 2)
            {
                session.AnswerVesselBriefing((M06ReunionGreeting)index);
                Refresh();
                return;
            }

            if (session.Stage == M06ReunionStage.ShenResponse && index >= 0 && index <= 2)
            {
                session.RespondToShen((M06ShenResponse)(index + 1));
                Refresh();
                return;
            }

            if (session.Stage == M06ReunionStage.RescuePlanDecision && !submittingPlan && index >= 0 && index <= 3)
            {
                M06RescuePlan plan = (M06RescuePlan)(index + 1);
                submittingPlan = true;
                Refresh();
                try
                {
                    onPlanConfirmed?.Invoke(plan);
                    if (session.Stage == M06ReunionStage.Completed)
                        onPlanConfirmed = null;
                }
                finally
                {
                    submittingPlan = false;
                    if (!disposed) Refresh();
                }
            }
        }

        private void HandleAdvanceClick()
        {
            if (disposed) return;
            if (CanvasDestroyed())
            {
                Dispose();
                return;
            }

            if (!advanceButton.IsActive() || !advanceButton.IsInteractable())
                return;

            switch (session.Stage)
            {
                case M06ReunionStage.PumpRoomWitnesses:
                    session.WitnessPumpRoomAndInspection();
                    Refresh();
                    break;
                case M06ReunionStage.LetterHandoff:
                    if (session.ContinueAfterLetterHandoff())
                        Refresh();
                    break;
                case M06ReunionStage.OulinRetaliationTruth:
                    session.HearOulinRetaliationTruth();
                    Refresh();
                    break;
            }
        }

        private bool CanvasDestroyed()
        {
            if (canvasObject == null || panel == null || heading == null ||
                body == null || advanceButton == null || advanceLabel == null)
                return true;
            for (int i = 0; i < choiceButtons.Length; i++)
            {
                if (choiceButtons[i] == null || choiceLabels[i] == null)
                    return true;
            }
            return false;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            onPlanConfirmed = null;
            SoundwellUiLocalization.Changed -= Refresh;
            if (advanceButton != null) advanceButton.onClick.RemoveAllListeners();
            for (int i = 0; i < choiceButtons.Length; i++)
            {
                if (choiceButtons[i] != null)
                    choiceButtons[i].onClick.RemoveAllListeners();
            }
            SoundwellUiTheme.SafeDestroyCanvas(canvasObject);
        }
    }
}
