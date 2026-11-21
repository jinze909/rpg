using System;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Soundwell
{
    public readonly struct M14CivilianCounts
    {
        public M14CivilianCounts(int departing, int staying, int undecided)
        {
            Departing = departing;
            Staying = staying;
            Undecided = undecided;
        }

        public int Departing { get; }
        public int Staying { get; }
        public int Undecided { get; }
    }

    /// <summary>
    /// Bottom-anchored safe-region UGUI panel for M14 ("同一个海峡，不同的进场").
    /// Drives the M13-confirmed route execution (A/B/C), voluntary civilian exit
    /// verification, and A Yuan position settlement (exposing high-risk outcomes
    /// only when M13 recorded incomplete prep + warning + explicit consent) with
    /// ZH/EN localization.
    /// </summary>
    public sealed class M14FinalPassageView : IDisposable
    {
        private readonly GameObject canvasObject;
        private readonly RectTransform panel;
        private readonly Font latinFont;
        private readonly Text heading;
        private readonly Text body;
        private readonly Button[] choiceButtons = new Button[3];
        private readonly Text[] choiceLabels = new Text[3];
        private readonly M14FinalPassageSession session;
        private readonly Func<bool> validSupervisionCredentials;
        private readonly Func<bool> validPortGuarantee;
        private readonly Func<bool> threeOpenDoorBellsRung;
        private readonly Func<bool> fogRelayCompleted;
        private readonly Func<bool> fogMasteryBonusAvailable;
        private readonly Func<M14CivilianCounts> readCivilianCounts;
        private Action<M14AyuanOutcome> onAyuanSubmitted;
        private bool submitting;
        private bool disposed;

        public M14FinalPassageSession Session => session;
        public RectTransform Panel => panel;
        public Text Heading => heading;
        public Text Body => body;
        public Button RelieveLuJunButton => choiceButtons[0];
        public Button DetainLuJunButton => choiceButtons[1];
        public Button WithdrawLuJunButton => choiceButtons[2];
        public Button StopAtEscortButton => choiceButtons[0];
        public Button PursueWarehousesButton => choiceButtons[1];
        public Button CompleteFogRelayButton => choiceButtons[0];
        public Button VerifyCivilianExitButton => choiceButtons[0];
        public Button SettleAyuanSafeButton => choiceButtons[0];
        public Button SettleAyuanInjuredButton => choiceButtons[1];
        public Button SettleAyuanLostButton => choiceButtons[2];

        public M14FinalPassageView(
            Transform parent,
            M14FinalPassageSession session,
            Func<bool> validSupervisionCredentials,
            Func<bool> validPortGuarantee,
            Func<bool> threeOpenDoorBellsRung,
            Func<bool> fogRelayCompleted,
            Func<bool> fogMasteryBonusAvailable,
            Func<M14CivilianCounts> readCivilianCounts,
            Action<M14AyuanOutcome> onAyuanSubmitted)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.validSupervisionCredentials = validSupervisionCredentials ??
                throw new ArgumentNullException(nameof(validSupervisionCredentials));
            this.validPortGuarantee = validPortGuarantee ??
                throw new ArgumentNullException(nameof(validPortGuarantee));
            this.threeOpenDoorBellsRung = threeOpenDoorBellsRung ??
                throw new ArgumentNullException(nameof(threeOpenDoorBellsRung));
            this.fogRelayCompleted = fogRelayCompleted ??
                throw new ArgumentNullException(nameof(fogRelayCompleted));
            this.fogMasteryBonusAvailable = fogMasteryBonusAvailable ??
                throw new ArgumentNullException(nameof(fogMasteryBonusAvailable));
            this.readCivilianCounts = readCivilianCounts ??
                throw new ArgumentNullException(nameof(readCivilianCounts));
            this.onAyuanSubmitted = onAyuanSubmitted ??
                throw new ArgumentNullException(nameof(onAyuanSubmitted));

            latinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            canvasObject = new GameObject("M14 final passage canvas",
                typeof(RectTransform), typeof(Canvas));
            canvasObject.transform.SetParent(parent, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 156;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(
                SoundwellUiTheme.ReferenceWidth, SoundwellUiTheme.ReferenceHeight);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();

            var holder = new GameObject("M14 final passage bottom panel", typeof(RectTransform));
            holder.transform.SetParent(canvasObject.transform, false);
            panel = (RectTransform)holder.transform;
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0f);
            panel.sizeDelta = new Vector2(904f, 256f);
            panel.anchoredPosition = new Vector2(0f, 138f);

            SoundwellUiTheme.CreatePanel(panel, "M14 final passage border",
                new Vector2(904f, 256f), new Color(0.56f, 0.62f, 0.50f, 1f));
            SoundwellUiTheme.CreatePanel(panel, "M14 final passage backing",
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
                    panel, "M14 final passage choice " + i, positions[i],
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
                case M14Phase.RouteExecution:
                    if (session.Departure.Route == M13ApproachRoute.RouteASupervisedCivilian)
                    {
                        heading.text = zh ? "M14 · 路线 A 监督移交广场" : "M14 · ROUTE A SUPERVISED SQUARE";
                        body.text = zh
                            ? "广场摆开还票桌与意愿登记桌，凭有效监察凭证与港务担保落实放行，并按证据与责任确定陆钧唯一处置结果："
                            : "Return tickets and record voluntary choices with valid supervision and port guarantees, then settle Lu Jun's single disposition:";
                        ShowChoice(0, zh ? "解除陆钧指挥并完成放行" : "Relieve Lu Jun of command");
                        ShowChoice(1, zh ? "依法拘押陆钧并完成放行" : "Detain Lu Jun");
                        ShowChoice(2, zh ? "陆钧带自愿留部退出关城" : "Lu Jun withdraws with volunteers");
                    }
                    else if (session.Departure.Route == M13ApproachRoute.RouteBBellTowerAssault)
                    {
                        heading.text = zh ? "M14 · 路线 B 钟楼三声开门" : "M14 · ROUTE B BELL TOWER ASSAULT";
                        body.text = zh
                            ? "钟楼顶层三声铜钟响起「开门」信号，封港铁栅已开。请决定关门打开后的军事行动边界："
                            : "Three open-door bells ring from the tower as the gate lifts. Decide the boundary of armed action:";
                        ShowChoice(0, zh ? "止于护送（不扩大战事）" : "Stop at escort");
                        ShowChoice(1, zh ? "追击夺占沿岸仓区" : "Pursue & seize warehouses");
                    }
                    else
                    {
                        heading.text = zh ? "M14 · 路线 C 旧运渠大雾绕行" : "M14 · ROUTE C OLD CANAL NIGHT RUN";
                        string bonusNote = fogMasteryBonusAvailable()
                            ? (zh ? "（RC-04 听雾加成生效，雾中接应更稳。）" : " (RC-04 fog mastery bonus active.)")
                            : string.Empty;
                        body.text = (zh
                            ? "浅底船队挂上识别灯滑入旧运渠，一条船接一条船以名应「在」完成雾中接力，绕行前往中立城镜湾。"
                            : "Shallow boats slip into the fogged canal, relaying names one by one toward neutral Jingwan.") + bonusNote;
                        ShowChoice(0, zh ? "完成雾中名册接力并靠岸" : "Complete fog relay & land");
                    }
                    FocusFirstInteractable();
                    break;

                case M14Phase.CivilianExitVerification:
                    heading.text = zh ? "M14 · 核实平民自愿进退人数" : "M14 · VERIFY VOLUNTARY CIVILIAN EXIT";
                    M14CivilianCounts counts = readCivilianCounts();
                    body.text = zh
                        ? "开门或挂旗不等于全员解放。当前核点：自愿登船 " + counts.Departing +
                          " 人，自愿留驻 " + counts.Staying + " 人，留票待定 " + counts.Undecided + " 人。"
                        : "Opening a gate does not auto-count liberation. Counts: " + counts.Departing +
                          " departing, " + counts.Staying + " staying, " + counts.Undecided + " holding tickets.";
                    ShowChoice(0, zh ? "核实登记进退人数并推进" : "Verify counts & continue");
                    FocusFirstInteractable();
                    break;

                case M14Phase.AyuanPositionResolution:
                    heading.text = zh ? "M14 · 舵手阿鸢岗位结算" : "M14 · A YUAN POSITION SETTLEMENT";
                    if (session.Departure.CanAyuanSufferHighRiskOutcome)
                    {
                        body.text = zh
                            ? "此前在 M13 准备不足且明确接受高风险警示。请按本场固定剧情节点结算阿鸢去向并落盘："
                            : "M13 departed unprepared with explicit risk consent. Settle A Yuan's outcome and save M14:";
                        ShowChoice(0, zh ? "阿鸢安全撤回船上" : "A Yuan returns safe", !submitting);
                        ShowChoice(1, zh ? "派人救回但重伤退出" : "Rescued, severely injured", !submitting);
                        ShowChoice(2, zh ? "坚持留守没能回来" : "Held post and lost", !submitting);
                    }
                    else
                    {
                        body.text = zh
                            ? "路线 A 或 M13 安全准备已补齐，阿鸢在接应下顺利撤回船上（必安全）。确认落盘保存 M14。"
                            : "Route A or full M13 prep guarantees A Yuan returns safe. Confirm to save M14.";
                        ShowChoice(0, zh ? "确认阿鸢安全并落盘 M14" : "Confirm A Yuan safe & save M14", !submitting);
                    }
                    if (!submitting) FocusFirstInteractable();
                    break;

                case M14Phase.Completed:
                    heading.text = zh ? "M14 · 终局通路已落盘" : "M14 · FINAL PASSAGE SAVED";
                    body.text = zh
                        ? "M14 终局通路与阿鸢结果已写入记录。"
                        : "The M14 final passage and A Yuan outcome have been saved.";
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
                case M14Phase.RouteExecution:
                    if (session.Departure.Route == M13ApproachRoute.RouteASupervisedCivilian)
                    {
                        var disp = (M14LuJunDisposition)(index + 1);
                        if (session.TryResolveRouteASupervisedSquare(
                            validSupervisionCredentials(), validPortGuarantee(), disp))
                            Refresh();
                    }
                    else if (session.Departure.Route == M13ApproachRoute.RouteBBellTowerAssault)
                    {
                        var action = index == 0
                            ? M14BorderForceAction.StopAtEscort
                            : M14BorderForceAction.PursueAndSeizeWarehouses;
                        if (session.TryResolveRouteBBellTowerAssault(
                            threeOpenDoorBellsRung(), action))
                            Refresh();
                    }
                    else if (index == 0 && session.TryResolveRouteCOldCanalRun(
                        fogRelayCompleted(), fogMasteryBonusAvailable()))
                    {
                        Refresh();
                    }
                    break;

                case M14Phase.CivilianExitVerification:
                    if (index == 0)
                    {
                        M14CivilianCounts counts = readCivilianCounts();
                        if (session.TryVerifyCivilianExit(
                            counts.Departing, counts.Staying, counts.Undecided))
                            Refresh();
                    }
                    break;

                case M14Phase.AyuanPositionResolution:
                    if (submitting) return;
                    var outcome = (M14AyuanOutcome)(index + 1);
                    submitting = true;
                    Refresh();
                    try
                    {
                        onAyuanSubmitted?.Invoke(outcome);
                        if (session.IsCompleted)
                            onAyuanSubmitted = null;
                    }
                    finally
                    {
                        submitting = false;
                        if (!disposed) Refresh();
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
            onAyuanSubmitted = null;
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
