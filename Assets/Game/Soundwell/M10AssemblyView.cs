using System;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Bottom-anchored safe-region UGUI panel for M10 ("靖湾会盟").
    /// Steps through the third world notice ("桥在我在"), three-record spelling
    /// reconciliation (re-queueable without soft-lock), battle-report copy
    /// verification before Han Qiao's ledger choice, supply contract signing,
    /// and the Huilan Pass bell-tower seizure turn with ZH/EN localization.
    /// </summary>
    public sealed class M10AssemblyView : IDisposable
    {
        private readonly GameObject canvasObject;
        private readonly RectTransform panel;
        private readonly Font latinFont;
        private readonly Text heading;
        private readonly Text body;
        private readonly Button[] choiceButtons = new Button[3];
        private readonly Text[] choiceLabels = new Text[3];
        private readonly M10AssemblySession session;
        private readonly Func<bool> threeRecordsMatched;
        private readonly Func<bool> carriesM02BattleReport;
        private readonly Func<bool> rc02LeverageWeakened;
        private readonly Func<bool> rc05NeedsSupplyContract;
        private Action onCommitRequested;
        private bool submitting;
        private bool disposed;

        public M10AssemblySession Session => session;
        public RectTransform Panel => panel;
        public Text Heading => heading;
        public Text Body => body;
        public Button ObserveNoticeButton => choiceButtons[0];
        public Button ReconcileRecordsButton => choiceButtons[0];
        public Button PresentM02CopyButton => choiceButtons[0];
        public Button RetrieveArchiveCopyButton => choiceButtons[1];
        public Button PublishDraftButton => choiceButtons[0];
        public Button TradeForConcessionsButton => choiceButtons[1];
        public Button KeepConfidentialButton => choiceButtons[2];
        public Button SignFullCoverageButton => choiceButtons[0];
        public Button RecordPartialGapButton => choiceButtons[1];
        public Button HearBellTowerReportButton => choiceButtons[0];
        public Button ConfirmAssemblyButton => choiceButtons[0];

        public M10AssemblyView(
            Transform parent,
            M10AssemblySession session,
            Func<bool> threeRecordsMatched,
            Func<bool> carriesM02BattleReport,
            Func<bool> rc02LeverageWeakened,
            Func<bool> rc05NeedsSupplyContract,
            Action onCommitRequested)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.threeRecordsMatched = threeRecordsMatched ??
                throw new ArgumentNullException(nameof(threeRecordsMatched));
            this.carriesM02BattleReport = carriesM02BattleReport ??
                throw new ArgumentNullException(nameof(carriesM02BattleReport));
            this.rc02LeverageWeakened = rc02LeverageWeakened ??
                throw new ArgumentNullException(nameof(rc02LeverageWeakened));
            this.rc05NeedsSupplyContract = rc05NeedsSupplyContract ??
                throw new ArgumentNullException(nameof(rc05NeedsSupplyContract));
            this.onCommitRequested = onCommitRequested ??
                throw new ArgumentNullException(nameof(onCommitRequested));

            latinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            canvasObject = new GameObject("M10 assembly canvas",
                typeof(RectTransform), typeof(Canvas));
            canvasObject.transform.SetParent(parent, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 150;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(
                SoundwellUiTheme.ReferenceWidth, SoundwellUiTheme.ReferenceHeight);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();

            var holder = new GameObject("M10 assembly bottom panel", typeof(RectTransform));
            holder.transform.SetParent(canvasObject.transform, false);
            panel = (RectTransform)holder.transform;
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0f);
            panel.sizeDelta = new Vector2(904f, 256f);
            panel.anchoredPosition = new Vector2(0f, 138f);

            SoundwellUiTheme.CreatePanel(panel, "M10 assembly border",
                new Vector2(904f, 256f), new Color(0.66f, 0.58f, 0.43f, 1f));
            SoundwellUiTheme.CreatePanel(panel, "M10 assembly backing",
                new Vector2(900f, 252f), new Color(0.06f, 0.09f, 0.11f, 0.97f));
            Color ink = new Color(0.93f, 0.89f, 0.81f, 1f);

            heading = SoundwellUiTheme.CreateLabel(panel, 19,
                new Vector2(0f, 102f), new Vector2(842f, 28f), ink);
            heading.alignment = TextAnchor.MiddleLeft;

            body = SoundwellUiTheme.CreateLabel(panel, 15,
                new Vector2(0f, 38f), new Vector2(842f, 88f), ink);
            body.alignment = TextAnchor.UpperLeft;
            body.horizontalOverflow = HorizontalWrapMode.Wrap;

            var positions = new[]
            {
                new Vector2(-282f, -72f),
                new Vector2(0f, -72f),
                new Vector2(282f, -72f)
            };
            for (int i = 0; i < 3; i++)
            {
                int index = i;
                choiceButtons[i] = SoundwellUiTheme.CreateButton(
                    panel, "M10 assembly choice " + i, positions[i],
                    new Vector2(272f, 38f), 13, out choiceLabels[i]);
                choiceButtons[i].onClick.AddListener(() => HandleChoiceClick(index));
            }

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
            heading.font = body.font = font;
            for (int i = 0; i < choiceLabels.Length; i++)
                choiceLabels[i].font = font;

            for (int i = 0; i < choiceButtons.Length; i++)
            {
                choiceButtons[i].gameObject.SetActive(false);
                choiceButtons[i].interactable = false;
            }

            switch (session.Phase)
            {
                case M10AssemblyPhase.NoticeAndArrival:
                    heading.text = zh ? "M10 · 晨间告示与二批靠岸" : "M10 · THIRD WORLD NOTICE & ARRIVAL";
                    body.text = zh
                        ? "临潮码头贴出陆钧征兵告示「桥在我在」，第二批渡船正靠向靖湾。先读清告示再赴会盟桌。"
                        : "Lu Jun's conscription notice ('The Bridge Stands While I Stand') is posted at dawn as the second-batch ferry nears Jingwan.";
                    ShowChoice(0, zh ? "读清告示并入靖湾会盟" : "Read notice & enter assembly");
                    FocusFirstInteractable();
                    break;

                case M10AssemblyPhase.ThreeRecordReconciliation:
                    heading.text = zh ? "M10 · 三表核名放行" : "M10 · THREE-RECORD RECONCILIATION";
                    body.text = session.HadReconciliationDelay
                        ? (zh
                            ? "刚才核名有出入，已退回重排复核（无软锁）。请再次核对东岸名单、M09 登船账与本人口供，由易磊盖章放行。"
                            : "The prior check had a mismatch and was re-queued without soft-lock. Re-verify the three records so Yi Lei can stamp clearance.")
                        : (zh
                            ? "靖湾会盟三张桌前，需逐项核对东岸名单、M09 登船账与本人口供，核准后由易磊盖章放行。"
                            : "Reconcile the east-shore list, M09 boarding log, and personal testimony at the table so Yi Lei can stamp clearance.");
                    ShowChoice(0, zh ? "三表核名并请易磊盖章" : "Reconcile 3 records & stamp");
                    FocusFirstInteractable();
                    break;

                case M10AssemblyPhase.HanQiaoConfrontation:
                    heading.text = zh ? "M10 · 韩峤战报底稿对质" : "M10 · HAN QIAO LEDGER CONFRONTATION";
                    if (!session.BattleReportSource.HasValue)
                    {
                        body.text = zh
                            ? "质询白水渡涂改战报必须拿出实件底稿：出示 M02 随身抄本，或凭白水渡断缆与界碑线索令韩峤亲取档案底稿。"
                            : "Confronting Han Qiao requires an actual battle-report copy: present your M02 copy or press him with Whitewater clues to fetch the signed archive copy.";
                        if (carriesM02BattleReport())
                            ShowChoice(0, zh ? "出示 M02 战报底稿抄本" : "Present M02 battle-report copy");
                        ShowChoice(1, zh ? "凭断缆线索令韩峤取回底稿" : "Have Han fetch archive copy");
                    }
                    else
                    {
                        string leverageNote = rc02LeverageWeakened()
                            ? (zh ? "（RC-02 默认放行被矿主抓住把柄，谈桌筹码受削。）"
                                  : " (RC-02 exception weakened your table leverage.)")
                            : string.Empty;
                        body.text = (zh
                            ? "战报底稿已摆上桌面。请决定如何处置韩峤这份涂改底稿："
                            : "The battle-report draft is on the table. Decide how to use Han Qiao's altered ledger:") + leverageNote;
                        ShowChoice(0, zh ? "公开底稿（牵动抚恤与碑文）" : "Publish draft publicly");
                        ShowChoice(1, zh ? "私下换取泊位与公开认活人" : "Trade for early concessions");
                        ShowChoice(2, zh ? "替他保密（欠姜婶一个说法）" : "Keep confidential");
                    }
                    FocusFirstInteractable();
                    break;

                case M10AssemblyPhase.SupplyContractTable:
                    heading.text = zh ? "M10 · 补给合同签字" : "M10 · SUPPLY CONTRACT TABLE";
                    string rc05Note = rc05NeedsSupplyContract()
                        ? (zh ? " 需兼顾 RC-05 第三班民用赦免后的山上家属供粮份额。"
                              : " Must account for RC-05 Third Squad pardoned mountain families.")
                        : string.Empty;
                    body.text = (zh
                        ? "落实东岸与留岛补给的出粮方、航次与接收人；若运力不足，只记已能覆盖份额，缺口带回 M13 筹备桌。"
                        : "Confirm grain suppliers, sailings, and receivers; if capacity falls short, record covered share and carry the gap to M13.") + rc05Note;
                    ShowChoice(0, zh ? "签下全额覆盖补给合同" : "Sign full-coverage contract");
                    ShowChoice(1, zh ? "记已覆盖份额，缺口带回 M13" : "Record partial gap to M13");
                    FocusFirstInteractable();
                    break;

                case M10AssemblyPhase.BellTowerTurn:
                    heading.text = zh ? "M10 · 回澜关传讯钟楼来报" : "M10 · HUILAN PASS BELL TOWER TURN";
                    if (!session.BellTowerSeizureHeard)
                    {
                        body.text = zh
                            ? "会盟刚落笔，急信传来：陆钧借护送已核名红盐转运队名义接管回澜关传讯钟楼，并按旧三旅花名册开始征兵。"
                            : "Urgent word arrives: Lu Jun seized Huilan Pass's signal bell tower under escort pretext and began conscripting by old Third Brigade rosters.";
                        ShowChoice(0, zh ? "听清回澜关来报" : "Receive bell-tower report");
                        FocusFirstInteractable();
                    }
                    else
                    {
                        body.text = zh
                            ? "靖湾会盟各项账目与回澜关急报均已核清，请确认落盘保存。"
                            : "All Jingwan assembly outcomes and the Huilan Pass report are verified. Confirm to save.";
                        ShowChoice(0, zh ? "确认落盘结束 M10 会盟" : "Confirm & save M10 assembly", !submitting);
                        if (!submitting) FocusFirstInteractable();
                    }
                    break;

                case M10AssemblyPhase.Completed:
                    heading.text = zh ? "M10 · 靖湾会盟已落盘" : "M10 · JINGWAN ASSEMBLY SAVED";
                    body.text = zh
                        ? "靖湾会盟结果已写入记录。"
                        : "The Jingwan assembly outcome has been saved.";
                    break;
            }
        }

        private void ShowChoice(int index, string text, bool interactable = true)
        {
            choiceLabels[index].text = text;
            choiceButtons[index].gameObject.SetActive(true);
            choiceButtons[index].interactable = interactable;
        }

        private void FocusFirstInteractable()
        {
            for (int i = 0; i < choiceButtons.Length; i++)
            {
                if (choiceButtons[i].gameObject.activeSelf && choiceButtons[i].interactable)
                {
                    SoundwellInputAdapter.KeepDialogFocus(canvasObject.transform, choiceButtons[i]);
                    return;
                }
            }
        }

        private void HandleChoiceClick(int index)
        {
            if (disposed || CanvasDestroyed() || index < 0 || index >= choiceButtons.Length)
                return;
            if (!choiceButtons[index].gameObject.activeSelf || !choiceButtons[index].interactable)
                return;

            switch (session.Phase)
            {
                case M10AssemblyPhase.NoticeAndArrival:
                    if (index == 0 && session.TryObserveWorldNoticeThree()) Refresh();
                    break;

                case M10AssemblyPhase.ThreeRecordReconciliation:
                    if (index == 0)
                    {
                        session.TryReconcileThreeRecords(threeRecordsMatched());
                        Refresh();
                    }
                    break;

                case M10AssemblyPhase.HanQiaoConfrontation:
                    if (!session.BattleReportSource.HasValue)
                    {
                        if (index == 0 && carriesM02BattleReport() &&
                            session.TrySecureBattleReportCopy(M10BattleReportSource.CarriedFromM02))
                            Refresh();
                        else if (index == 1 &&
                            session.TrySecureBattleReportCopy(M10BattleReportSource.RetrievedFromHanArchive))
                            Refresh();
                    }
                    else
                    {
                        var use = (M10HanLedgerUse)(index + 1);
                        if (session.TryDecideHanLedgerUse(use)) Refresh();
                    }
                    break;

                case M10AssemblyPhase.SupplyContractTable:
                    if (index == 0 && session.TrySignSupplyContract(M10SupplyContractStatus.SignedFullCoverage))
                        Refresh();
                    else if (index == 1 && session.TrySignSupplyContract(M10SupplyContractStatus.PartialGapToM13))
                        Refresh();
                    break;

                case M10AssemblyPhase.BellTowerTurn:
                    if (!session.BellTowerSeizureHeard)
                    {
                        if (index == 0 && session.TryReceiveBellTowerSeizureReport()) Refresh();
                    }
                    else if (index == 0 && !submitting)
                    {
                        submitting = true;
                        Refresh();
                        try
                        {
                            onCommitRequested?.Invoke();
                            if (session.IsCompleted)
                                onCommitRequested = null;
                        }
                        finally
                        {
                            submitting = false;
                            if (!disposed) Refresh();
                        }
                    }
                    break;
            }
        }

        private bool CanvasDestroyed()
        {
            if (canvasObject == null || panel == null || heading == null || body == null)
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
            onCommitRequested = null;
            SoundwellUiLocalization.Changed -= Refresh;
            for (int i = 0; i < choiceButtons.Length; i++)
            {
                if (choiceButtons[i] != null)
                    choiceButtons[i].onClick.RemoveAllListeners();
            }
            SoundwellUiTheme.SafeDestroyCanvas(canvasObject);
        }
    }
}
