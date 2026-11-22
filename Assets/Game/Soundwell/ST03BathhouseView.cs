using System;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Bottom-anchored safe-region UGUI panel for ST-03 ("第三声钟").
    /// Drives inspecting the three closed-bathhouse clues, interviewing Wu Su
    /// and care-home residents, clearing the safe route &amp; return timetable,
    /// completing trial reopening &amp; transport budget reconciliation, and
    /// committing one permanent operating plan with ZH/EN localization.
    /// </summary>
    public sealed class ST03BathhouseView : IDisposable
    {
        private readonly GameObject canvasObject;
        private readonly RectTransform panel;
        private readonly Font latinFont;
        private readonly Text heading;
        private readonly Text body;
        private readonly Button[] choiceButtons = new Button[3];
        private readonly Text[] choiceLabels = new Text[3];
        private readonly ST03BathhouseSession session;
        private readonly Func<bool> careHomeInterviewReady;
        private readonly Func<bool> safeRouteReady;
        private readonly Func<bool> trialNightReady;
        private readonly Func<bool> transportBudgetReady;
        private Action onCommitRequested;
        private int clickedClueMask;
        private ST03BathhouseChoice? pendingChoice;
        private bool submitting;
        private bool disposed;

        public ST03BathhouseSession Session => session;
        public RectTransform Panel => panel;
        public Text Heading => heading;
        public Text Body => body;
        public Button BoilerClueButton => choiceButtons[0];
        public Button LockersClueButton => choiceButtons[1];
        public Button LettersClueButton => choiceButtons[2];
        public Button InterviewWuSuButton => choiceButtons[0];
        public Button MakeSafeRouteButton => choiceButtons[0];
        public Button CompleteTrialNightButton => choiceButtons[0];
        public Button ReconcileBudgetButton => choiceButtons[0];
        public Button ReopenSubsidizedButton => choiceButtons[0];
        public Button ElderBackroomButton => choiceButtons[1];
        public Button SellAndRelocateButton => choiceButtons[2];
        public bool IsDisposed => disposed || CanvasDestroyed();

        public ST03BathhouseView(
            Transform parent,
            ST03BathhouseSession session,
            Func<bool> careHomeInterviewReady,
            Func<bool> safeRouteReady,
            Func<bool> trialNightReady,
            Func<bool> transportBudgetReady,
            Action onCommitRequested)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.careHomeInterviewReady = careHomeInterviewReady ??
                throw new ArgumentNullException(nameof(careHomeInterviewReady));
            this.safeRouteReady = safeRouteReady ??
                throw new ArgumentNullException(nameof(safeRouteReady));
            this.trialNightReady = trialNightReady ??
                throw new ArgumentNullException(nameof(trialNightReady));
            this.transportBudgetReady = transportBudgetReady ??
                throw new ArgumentNullException(nameof(transportBudgetReady));
            this.onCommitRequested = onCommitRequested ??
                throw new ArgumentNullException(nameof(onCommitRequested));

            latinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            canvasObject = new GameObject("ST03 bathhouse canvas",
                typeof(RectTransform), typeof(Canvas));
            canvasObject.transform.SetParent(parent, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 160;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(
                SoundwellUiTheme.ReferenceWidth, SoundwellUiTheme.ReferenceHeight);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();

            var holder = new GameObject("ST03 bathhouse bottom panel", typeof(RectTransform));
            holder.transform.SetParent(canvasObject.transform, false);
            panel = (RectTransform)holder.transform;
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0f);
            panel.sizeDelta = new Vector2(904f, 256f);
            panel.anchoredPosition = new Vector2(0f, 138f);

            SoundwellUiTheme.CreatePanel(panel, "ST03 bathhouse border",
                new Vector2(904f, 256f), new Color(0.54f, 0.60f, 0.58f, 1f));
            SoundwellUiTheme.CreatePanel(panel, "ST03 bathhouse backing",
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
                    panel, "ST03 bathhouse choice " + i, positions[i],
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
            if (disposed || IsDestroyedExternally()) return;
            if (float.IsNaN(aspect) || float.IsInfinity(aspect) || aspect <= 0f) return;
            SoundwellUiTheme.ApplyViewportAspectScale(panel, aspect);
        }

        public void Refresh()
        {
            if (disposed || IsDestroyedExternally()) return;

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
                case ST03BathhousePhase.InspectClosedBathhouse:
                    heading.text = zh ? "ST03 · 查验停业旧浴堂" : "ST03 · INSPECT CLOSED BATHHOUSE";
                    body.text = zh
                        ? "钟岭老浴堂停业后仍有人寄来三封落款不同的信。先逐一查验锅炉欠薪、旧柜修补与三封信笔迹（已查 " +
                          session.InspectedClueCount + "/3）。"
                        : "Inspect the leaking boiler, old lockers, and Wu Su's handwriting on the three letters (" +
                          session.InspectedClueCount + "/3 checked).";
                    ShowChoice(0, zh ? "查漏水锅炉与欠薪账" : "Inspect boiler & unpaid ledger",
                        (clickedClueMask & 1) == 0);
                    ShowChoice(1, zh ? "查旧更衣柜与修补痕" : "Inspect old lockers & repairs",
                        (clickedClueMask & 2) == 0);
                    ShowChoice(2, zh ? "核对三封信与伍肃笔迹" : "Compare letters & Wu Su's hand",
                        (clickedClueMask & 4) == 0);
                    FocusFirstInteractable();
                    break;

                case ST03BathhousePhase.MeetResidentsAtCareHome:
                    heading.text = zh ? "ST03 · 疗养院访伍肃与住户" : "ST03 · CARE HOME INTERVIEW";
                    body.text = zh
                        ? "上山拜访伍肃与疗养院老人们，核实寄信人身份，并同时听取愿下山洗浴者与愿留在院内者的意见。"
                        : "Visit Wu Su and care-home residents to confirm letter authorship and hear both willing visitors and those preferring to stay.";
                    ShowChoice(0, zh ? "完成伍肃与两派住户访谈" : "Complete Wu Su & resident talks");
                    FocusFirstInteractable();
                    break;

                case ST03BathhousePhase.RepairRouteAndTransport:
                    heading.text = zh ? "ST03 · 清路、避风歇脚与改车次" : "ST03 · SAFE ROUTE & TIMETABLE";
                    body.text = zh
                        ? "清理倒塌货架、落实避风歇脚点并调整返程车次时刻，确保下山洗浴往返安全。"
                        : "Clear the fallen shelf, secure a sheltered rest stop, and retime the return vehicle schedule.";
                    ShowChoice(0, zh ? "确认安全通道与返程时刻" : "Confirm safe route & timetable");
                    FocusFirstInteractable();
                    break;

                case ST03BathhousePhase.TrialReopening:
                    heading.text = zh ? "ST03 · 试营业与车费对账" : "ST03 · TRIAL NIGHT & BUDGET";
                    if (!session.TrialCompleted)
                    {
                        body.text = zh
                            ? "疏通排水口、理清毛巾并核销旧浴票，完成第三声钟下的试营业。"
                            : "Clear the drain, sort towels, and honor old bath tickets to complete the trial reopening.";
                        ShowChoice(0, zh ? "完成试营业全部工序" : "Complete trial reopening");
                    }
                    else
                    {
                        body.text = zh
                            ? "试营业顺利结束，但新改车次带来了额外车费缺口。先核平交通账目再定长期方案。"
                            : "Trial reopening succeeded, leaving an overlooked carriage fare to reconcile before choosing a permanent plan.";
                        ShowChoice(0, zh ? "核平新增车费账目" : "Reconcile carriage fare budget");
                    }
                    FocusFirstInteractable();
                    break;

                case ST03BathhousePhase.CompareOperatingPlans:
                    heading.text = zh ? "ST03 · 选定浴堂长期方案" : "ST03 · PERMANENT BATHHOUSE PLAN";
                    body.text = zh
                        ? "前期试运营与车费账目已核清。请选择老浴堂与伍肃的永久安排并落盘保存："
                        : "Trial reopening and transport costs are balanced. Choose the permanent arrangement and save:";
                    ShowChoice(0, zh ? "重开旧浴堂并设补贴时段" : "Reopen with subsidized hours",
                        !submitting && (!pendingChoice.HasValue ||
                            pendingChoice.Value == ST03BathhouseChoice.ReopenWithSubsidizedHours));
                    ShowChoice(1, zh ? "改由老人合办后间洗衣浴舍" : "Elder-run laundry backroom",
                        !submitting && (!pendingChoice.HasValue ||
                            pendingChoice.Value == ST03BathhouseChoice.ElderRunLaundryBackroom));
                    ShowChoice(2, zh ? "售出旧址并改建疗养院洗浴" : "Sell old site & relocate to care home",
                        !submitting && (!pendingChoice.HasValue ||
                            pendingChoice.Value == ST03BathhouseChoice.SellAndRelocateToCareHome));
                    if (!submitting) FocusFirstInteractable();
                    break;

                case ST03BathhousePhase.Completed:
                    heading.text = zh ? "ST03 · 第三声钟已落盘" : "ST03 · THIRD BELL SAVED";
                    body.text = zh
                        ? "钟岭老浴堂永久方案已写入记录。"
                        : "The Third Bell bathhouse outcome has been saved.";
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
            if (disposed || IsDestroyedExternally() || index < 0 || index >= choiceButtons.Length)
                return;
            if (!choiceButtons[index].gameObject.activeSelf || !choiceButtons[index].interactable)
                return;

            switch (session.Phase)
            {
                case ST03BathhousePhase.InspectClosedBathhouse:
                    var clue = (ST03BathhouseClue)(index + 1);
                    if (session.TryInspectClue(clue))
                    {
                        clickedClueMask |= (1 << index);
                        Refresh();
                    }
                    break;

                case ST03BathhousePhase.MeetResidentsAtCareHome:
                    bool interviewOk = careHomeInterviewReady();
                    if (index == 0 && session.TryInterviewWuSu(interviewOk, interviewOk, interviewOk))
                        Refresh();
                    break;

                case ST03BathhousePhase.RepairRouteAndTransport:
                    bool routeOk = safeRouteReady();
                    if (index == 0 && session.TryMakeSafeRoute(routeOk, routeOk, routeOk))
                        Refresh();
                    break;

                case ST03BathhousePhase.TrialReopening:
                    if (!session.TrialCompleted)
                    {
                        bool trialOk = trialNightReady();
                        if (index == 0 && session.TryCompleteTrialNight(trialOk, trialOk, trialOk))
                            Refresh();
                    }
                    else if (index == 0 && session.TryReconcileTransportBudget(transportBudgetReady()))
                    {
                        Refresh();
                    }
                    break;

                case ST03BathhousePhase.CompareOperatingPlans:
                    if (submitting) return;
                    var choice = (ST03BathhouseChoice)(index + 1);
                    if (pendingChoice.HasValue && pendingChoice.Value != choice)
                        return;
                    if (!pendingChoice.HasValue)
                    {
                        if (!session.TryChoose(choice)) return;
                        pendingChoice = choice;
                    }

                    submitting = true;
                    Refresh();
                    try
                    {
                        onCommitRequested?.Invoke();
                        if (session.Phase == ST03BathhousePhase.Completed)
                            onCommitRequested = null;
                    }
                    finally
                    {
                        submitting = false;
                        if (!disposed) Refresh();
                    }
                    break;
            }
        }

        private bool IsDestroyedExternally()
        {
            if (!CanvasDestroyed()) return false;
            Dispose();
            return true;
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