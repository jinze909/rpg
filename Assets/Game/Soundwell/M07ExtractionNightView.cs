using System;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Bottom-anchored safe-region UGUI panel driving M07ExtractionNightSession
    /// through route execution (or blocked fallback), Oulin's reverse lock and
    /// Yida's gate, Rus's ledger confrontation, real return-letter collection,
    /// and pre-dawn dock roll call with retry-safe Makinom persistence.
    /// </summary>
    public sealed class M07ExtractionNightView : IDisposable
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
        private readonly M07ExtractionNightSession session;
        private readonly int defaultBoardedCount;
        private readonly int fallbackBoardedCount;
        private readonly M07YidaChoice defaultYidaChoice;
        private Action<M07LiangChoice, M07YidaChoice> onRollCallConfirmed;
        private bool submittingRollCall;
        private bool disposed;

        public M07ExtractionNightSession Session => session;
        public RectTransform Panel => panel;
        public Text Heading => heading;
        public Text Body => body;
        public Button AdvanceButton => advanceButton;
        public Button ExecutePlanButton => choiceButtons[0];
        public Button PivotFallbackButton => choiceButtons[1];
        public Button ArrestRusButton => choiceButtons[0];
        public Button RusFledButton => choiceButtons[1];
        public Button RusLostContractButton => choiceButtons[2];
        public Button RusShelteredButton => choiceButtons[3];
        public Button LiangStayButton => choiceButtons[0];
        public Button LiangPersuadeButton => choiceButtons[1];
        public Button LiangForceButton => choiceButtons[2];

        public M07ExtractionNightView(
            Transform parent,
            M07ExtractionNightSession session,
            Action<M07LiangChoice, M07YidaChoice> onRollCallConfirmed,
            int defaultBoardedCount = 300,
            int fallbackBoardedCount = 180,
            M07YidaChoice defaultYidaChoice = M07YidaChoice.HandToInspector)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.onRollCallConfirmed = onRollCallConfirmed ??
                throw new ArgumentNullException(nameof(onRollCallConfirmed));
            if (defaultBoardedCount <= 0 || defaultBoardedCount > M07ExtractionFacts.MaxFirstBatchBoarded)
                throw new ArgumentOutOfRangeException(nameof(defaultBoardedCount));
            if (fallbackBoardedCount <= 0 || fallbackBoardedCount > M07ExtractionFacts.MaxFirstBatchBoarded)
                throw new ArgumentOutOfRangeException(nameof(fallbackBoardedCount));

            this.defaultBoardedCount = defaultBoardedCount;
            this.fallbackBoardedCount = fallbackBoardedCount;
            this.defaultYidaChoice = defaultYidaChoice;

            latinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            canvasObject = new GameObject("M07 extraction night canvas",
                typeof(RectTransform), typeof(Canvas));
            canvasObject.transform.SetParent(parent, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 136;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(
                SoundwellUiTheme.ReferenceWidth, SoundwellUiTheme.ReferenceHeight);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();

            var holder = new GameObject("M07 extraction bottom panel", typeof(RectTransform));
            holder.transform.SetParent(canvasObject.transform, false);
            panel = (RectTransform)holder.transform;
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0f);
            panel.sizeDelta = new Vector2(904f, 248f);
            panel.anchoredPosition = new Vector2(0f, 134f);

            SoundwellUiTheme.CreatePanel(panel, "M07 extraction border",
                new Vector2(904f, 248f), new Color(0.64f, 0.56f, 0.42f, 1f));
            SoundwellUiTheme.CreatePanel(panel, "M07 extraction backing",
                new Vector2(900f, 244f), new Color(0.06f, 0.09f, 0.11f, 0.97f));
            Color ink = new Color(0.93f, 0.89f, 0.81f, 1f);

            heading = SoundwellUiTheme.CreateLabel(panel, 19,
                new Vector2(0f, 98f), new Vector2(842f, 28f), ink);
            heading.alignment = TextAnchor.MiddleLeft;

            body = SoundwellUiTheme.CreateLabel(panel, 15,
                new Vector2(0f, 36f), new Vector2(842f, 88f), ink);
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
                    panel, "M07 extraction choice " + i, positions[i],
                    new Vector2(414f, 36f), 14, out choiceLabels[i]);
                choiceButtons[i].onClick.AddListener(() => HandleChoiceClick(index));
            }

            advanceButton = SoundwellUiTheme.CreateButton(
                panel, "M07 extraction advance", new Vector2(302f, -92f),
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
                case M07ExtractionStage.RouteExecution:
                    heading.text = zh ? "赤盐营救之夜 · 启动方案" : "RED SALT EXTRACTION NIGHT · LAUNCH";
                    body.text = (zh ? "既定方案：" : "Selected plan: ") +
                        M06PumpRoomReunionSession.FormatPlanLabel(session.M06Record.Plan, zh);
                    ShowChoice(0, zh ? "按既定方案推进撤离" : "Execute selected rescue plan");
                    ShowChoice(1, zh ? "现场受阻，叠计损耗转分批监察移交" : "Blocked in field: pivot to fallback handover");
                    SoundwellInputAdapter.KeepDialogFocus(canvasObject.transform, choiceButtons[0]);
                    break;

                case M07ExtractionStage.OulinLockAndYidaGate:
                    heading.text = zh ? "矿道与暗闸 · 欧林开锁" : "TUNNEL GATE · OULIN'S LOCK";
                    string pumpLine = session.OldZhangFarewellSpoken
                        ? (zh
                            ? "下层五百人已撤上安全层、手摇备用泵接通。沈砾拍了拍主泵机壳：「对不住了，老张。」主泵安全停机三小时。\n"
                            : "Five hundred moved to the upper gallery with hand pumps active. Shen Li pats the main pump: \"Sorry, Old Zhang.\" Three-hour safe shutdown begins.\n")
                        : (zh
                            ? "主泵由留守工匠维持运转，不拉总闸。\n"
                            : "The main pump remains running under relief crew; the breaker is not pulled.\n");
                    body.text = pumpLine + (zh
                        ? "欧林反手解开最外层暗锁，在水桶边洗了很久的手；尽头暗闸被管事易达从里面悄悄拨开。"
                        : "Oulin picks the outer hidden lock backwards and washes his hands by the bucket; Steward Yida quietly unlatches the end gate from inside.");
                    ShowAdvance(zh ? "前往账房" : "Proceed to ledger office");
                    break;

                case M07ExtractionStage.RusLedgerConfrontation:
                    heading.text = zh ? "账房对峙 · 鲁斯" : "LEDGER OFFICE · RUS";
                    body.text = zh
                        ? "鲁斯倒了一杯茶，翻开口粮与损耗栏：「你们带走这三百人，明年谁给剩下的付粮？」证据已分散在矿工记录、船单与监察副本中。"
                        : "Rus pours tea and opens the ration-and-loss column: \"Take these three hundred, and who pays grain for the rest next year?\" Dispersed records preserve accountability.";
                    ShowChoice(0, zh ? "当场扣押鲁斯并保下账册" : "Arrest Rus & preserve ledger");
                    ShowChoice(1, zh ? "鲁斯趁乱逃亡（账册部分烧毁）" : "Rus flees (ledger partially burned)");
                    ShowChoice(2, zh ? "取证追责令其失去供给合同" : "Strip Rus's supply contract");
                    ShowChoice(3, zh ? "鲁斯退入庇护（保留分散凭证）" : "Rus takes shelter (dispersed proof kept)");
                    SoundwellInputAdapter.KeepDialogFocus(canvasObject.transform, choiceButtons[0]);
                    break;

                case M07ExtractionStage.ReturnLettersCheck:
                    heading.text = zh ? "出营清点 · 家书回信" : "LEAVING CAMP · RETURN LETTERS";
                    body.text = session.HasReturnLetters
                        ? (zh
                            ? "M06 亲手投递的家书带回了真实回信：其中一位收信人已去世、一位不肯回、一位写了一整页。名册上多了几张核验过的脸。"
                            : "Delivered M06 letters yield real replies: one recipient deceased, one declined to write, and one wrote a full page.")
                        : (zh
                            ? "此前未在矿营投递外港家书，本趟不生成回信。"
                            : "No outer-harbor letters were delivered in M06; no replies are fabricated.");
                    ShowAdvance(zh ? "前往黎明前码头点名" : "Proceed to pre-dawn dock roll call");
                    break;

                case M07ExtractionStage.DockRollCall:
                    heading.text = zh ? "黎明前 · 赤盐码头点名" : "BEFORE DAWN · RED SALT PIER ROLL CALL";
                    body.text = (zh ? "沈砾手持" : "Shen Li reads from ") +
                        session.FormatRollCallRosterSource(zh) +
                        (zh
                            ? "。小七听见编号「七十二」答了「在」，陶霁应声登船，易达走上前称自己也在名册里；梁伯答完「在」却说「我不走」："
                            : ". Xiaoqi answers \"Present\" to #72, Tao Ji boards, Yida steps up from the pier edge; Uncle Liang answers \"Present\" then says \"I am staying\":");
                    ShowChoice(0, zh ? "尊重梁伯决定，留他在岛上" : "Respect Uncle Liang's choice to stay", !submittingRollCall);
                    ShowChoice(1, zh ? "耐心劝说梁伯同行" : "Persuade Uncle Liang to board", !submittingRollCall);
                    ShowChoice(2, zh ? "强行带走梁伯（将损伤与沈砾关系）" : "Force Uncle Liang aboard (strains Shen Li)", !submittingRollCall);
                    if (!submittingRollCall)
                        SoundwellInputAdapter.KeepDialogFocus(canvasObject.transform, choiceButtons[0]);
                    break;

                case M07ExtractionStage.Completed:
                    heading.text = zh ? "赤盐码头 · 首批起航" : "RED SALT PIER · FIRST BATCH SAILED";
                    body.text = zh
                        ? $"首批登船离岛 {session.BoardedFirstBatchCount} 人，上层撤离安置 {session.LowerLevelEvacuatedCount} 人。"
                        : $"First batch boarded off-island: {session.BoardedFirstBatchCount}; lower-level moved to upper gallery: {session.LowerLevelEvacuatedCount}.";
                    break;
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
            if (disposed || CanvasDestroyed()) return;

            if (session.Stage == M07ExtractionStage.RouteExecution)
            {
                bool blocked = index == 1;
                session.ExecuteRoute(blocked, blocked ? fallbackBoardedCount : defaultBoardedCount);
                Refresh();
                return;
            }

            if (session.Stage == M07ExtractionStage.RusLedgerConfrontation && index >= 0 && index <= 3)
            {
                M07RusFate fate = (M07RusFate)(index + 1);
                M07LedgerState ledger = index == 1
                    ? M07LedgerState.PartiallyBurned
                    : M07LedgerState.Preserved;
                session.ResolveRusConfrontation(fate, ledger);
                Refresh();
                return;
            }

            if (session.Stage == M07ExtractionStage.DockRollCall && !submittingRollCall && index >= 0 && index <= 2)
            {
                M07LiangChoice liang = (M07LiangChoice)(index + 1);
                submittingRollCall = true;
                Refresh();
                try
                {
                    onRollCallConfirmed?.Invoke(liang, defaultYidaChoice);
                    if (session.Stage == M07ExtractionStage.Completed)
                        onRollCallConfirmed = null;
                }
                finally
                {
                    submittingRollCall = false;
                    if (!disposed) Refresh();
                }
            }
        }

        private void HandleAdvanceClick()
        {
            if (disposed || CanvasDestroyed()) return;

            if (session.Stage == M07ExtractionStage.OulinLockAndYidaGate)
            {
                session.AdvanceTunnelOpening();
                Refresh();
            }
            else if (session.Stage == M07ExtractionStage.ReturnLettersCheck)
            {
                session.AdvanceReturnLettersCheck();
                Refresh();
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
            onRollCallConfirmed = null;
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
