using System;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Bottom-anchored safe-region UGUI panel for M16 ("一个普通的下午").
    /// Drives the pressure-free afternoon harbor walk, desk register &amp; eligible
    /// delivered letters (never fabricating letters for uncompleted cases), and
    /// final evening standing spot with respect_agency closing line (or silent
    /// harbor fade-out if Shen Li declined to meet), supporting ZH/EN.
    /// </summary>
    public sealed class M16EpilogueView : IDisposable
    {
        private static readonly M16DeliveredLettersFlags[] LetterOrder =
        {
            M16DeliveredLettersFlags.RC01ChenYan,
            M16DeliveredLettersFlags.RC02ZhengYuan,
            M16DeliveredLettersFlags.RC05ThirdSquad,
            M16DeliveredLettersFlags.ST02RiverLanterns
        };

        private readonly GameObject canvasObject;
        private readonly Canvas canvas;
        private readonly RectTransform panel;
        private readonly Font latinFont;
        private readonly Text heading;
        private readonly Text body;
        private readonly Button[] choiceButtons = new Button[3];
        private readonly Text[] choiceLabels = new Text[3];
        private readonly Button advanceButton;
        private readonly Text advanceLabel;
        private readonly M16EpilogueSession session;
        private Action<M16StandingSpot> onStandingSpotSubmitted;
        private M16DeliveredLettersFlags lastReadLetter;
        private bool submitting;
        private bool disposed;

        public M16EpilogueSession Session => session;
        public M16DeliveredLettersFlags LastReadLetter => lastReadLetter;
        public RectTransform Panel => panel;
        public Text Heading => heading;
        public Text Body => body;
        public Button VisitWorkshopButton => choiceButtons[0];
        public Button VisitQiQueButton => choiceButtons[1];
        public Button VisitOulinAndWestLaneButton => choiceButtons[2];
        public Button ProceedToDeskButton => advanceButton;
        public Button ReadNextLetterButton => choiceButtons[0];
        public Button ReviewRegisterButton => choiceButtons[1];
        public Button StepOutsideButton => advanceButton;
        public Button PierButton => choiceButtons[0];
        public Button SteleButton => choiceButtons[1];
        public Button WorkshopDoorButton => choiceButtons[2];
        public bool IsDisposed => disposed || CanvasDestroyed();

        public M16EpilogueView(
            Transform parent,
            M16EpilogueSession session,
            Action<M16StandingSpot> onStandingSpotSubmitted)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.onStandingSpotSubmitted = onStandingSpotSubmitted ??
                throw new ArgumentNullException(nameof(onStandingSpotSubmitted));

            latinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            canvasObject = new GameObject("M16 epilogue canvas",
                typeof(RectTransform), typeof(Canvas));
            canvasObject.transform.SetParent(parent, false);
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 162;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(
                SoundwellUiTheme.ReferenceWidth, SoundwellUiTheme.ReferenceHeight);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();

            var holder = new GameObject("M16 epilogue bottom panel", typeof(RectTransform));
            holder.transform.SetParent(canvasObject.transform, false);
            panel = (RectTransform)holder.transform;
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0f);
            panel.sizeDelta = new Vector2(904f, 256f);
            panel.anchoredPosition = new Vector2(0f, 138f);

            SoundwellUiTheme.CreatePanel(panel, "M16 epilogue border",
                new Vector2(904f, 256f), new Color(0.66f, 0.59f, 0.46f, 1f));
            SoundwellUiTheme.CreatePanel(panel, "M16 epilogue backing",
                new Vector2(900f, 252f), new Color(0.06f, 0.09f, 0.11f, 0.97f));
            Color ink = new Color(0.93f, 0.89f, 0.81f, 1f);

            heading = SoundwellUiTheme.CreateLabel(panel, 19,
                new Vector2(-110f, 102f), new Vector2(620f, 28f), ink);
            heading.alignment = TextAnchor.MiddleLeft;

            advanceButton = SoundwellUiTheme.CreateButton(
                panel, "M16 epilogue advance button", new Vector2(312f, 102f),
                new Vector2(218f, 28f), 12, out advanceLabel);
            advanceButton.onClick.AddListener(HandleAdvanceClick);

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
                    panel, "M16 epilogue choice " + i, positions[i],
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

            switch (session.Phase)
            {
                case M16Phase.AfternoonWalk:
                    heading.text = zh ? "M16 · 临潮的一个普通下午" : "M16 · AN ORDINARY AFTERNOON";
                    body.text = zh
                        ? "没有任务标记，也没有倒计时。你可以随心走走工坊、祁雀货栈、欧林饭桌与西巷，或随时回屋看桌上的来信。"
                        : "No quest markers and no timer. Stroll past the workshop, Qi Que's warehouse, Oulin's table, and West Lane, or head to your desk.";
                    ShowChoice(0, zh ? "去沈砾工坊看「债主」与小七" : "Visit workshop & Xiaoqi",
                        (session.StopsVisited & M16WalkStops.WorkshopStop) == 0);
                    ShowChoice(1, zh ? "去货栈看祁雀的紫红印章" : "Visit Qi Que's warehouse",
                        (session.StopsVisited & M16WalkStops.QiQueWarehouseStop) == 0);
                    bool oulinDone = (session.StopsVisited & M16WalkStops.OulinDinnerTableStop) != 0;
                    bool westDone = (session.StopsVisited & M16WalkStops.WestLaneAndHarborOfficeStop) != 0;
                    ShowChoice(2,
                        !oulinDone
                            ? (zh ? "去欧林与蕊亚的小院饭桌" : "Visit Oulin & Ruiya's table")
                            : (zh ? "去西巷姜婶院与港务署" : "Visit West Lane & harbor office"),
                        !oulinDone || !westDone);
                    ShowAdvance(zh ? "回屋看桌上名册与来信" : "Head inside to desk");
                    FocusFirstInteractable();
                    break;

                case M16Phase.DeskLettersAndRegister:
                    heading.text = zh ? "M16 · 桌上的来信与名册笔迹" : "M16 · DESK LETTERS & REGISTER";
                    M16DeliveredLettersFlags nextLetter = FindNextUnreadLetter();
                    body.text = FormatDeskBody(nextLetter, zh);
                    ShowChoice(0, FormatNextLetterButtonLabel(nextLetter, zh),
                        nextLetter != M16DeliveredLettersFlags.None);
                    ShowChoice(1, zh ? "翻看名册页边笔迹" : "Review register notes",
                        !session.DeskRegisterReviewed);
                    ShowAdvance(zh ? "走出屋门迎傍晚海风" : "Step outside for evening");
                    FocusFirstInteractable();
                    break;

                case M16Phase.FinalStandingAndFadeOut:
                    heading.text = zh ? "M16 · 最后一个下午" : "M16 · ONE LAST AFTERNOON";
                    body.text = FormatClosingBody(zh);
                    ShowChoice(0, zh ? "在码头边吹吹海风" : "Stand by the pier", !submitting);
                    ShowChoice(1, zh ? "在石碑前看一会儿夕阳" : "Watch sunset by the stele", !submitting);
                    ShowChoice(2, zh ? "在工坊门口靠一会儿" : "Lean by the workshop door", !submitting);
                    if (!submitting) FocusFirstInteractable();
                    break;

                case M16Phase.Completed:
                    heading.text = zh ? "M16 · 归来终场已落盘" : "M16 · EPILOGUE COMPLETED";
                    body.text = zh
                        ? "海风吹过港口，远处渡船归港。全剧终。"
                        : "Sea breeze crosses the harbor as the distant ferry returns. Fin.";
                    break;
            }
        }

        private string FormatDeskBody(M16DeliveredLettersFlags nextLetter, bool zh)
        {
            if (lastReadLetter != M16DeliveredLettersFlags.None)
            {
                string letterText = FormatDeliveredLetterBody(lastReadLetter, zh);
                string tail = nextLetter != M16DeliveredLettersFlags.None
                    ? (zh ? "桌上还有未拆的来信。" : "Another delivered letter waits on the desk.")
                    : (zh ? "桌上已送达的来信均已看清，随时可走出屋门看傍晚海风。"
                          : "All delivered letters have been read. Step outside whenever ready.");
                return letterText + "\n" + tail;
            }

            return nextLetter != M16DeliveredLettersFlags.None
                ? (zh ? "桌上只放着确实寄出并送达的朋友来信，可逐封拆读或翻看名册上的各方笔迹。"
                      : "Only actually delivered letters from completed tales rest on the desk. Read them or review the register notes.")
                : (zh ? "桌上已送达的来信均已看清，未开支线不生成虚构回信。随时可走出屋门看傍晚海风。"
                      : "All delivered letters have been read; unstarted tales stay unfabricated. Step outside whenever ready.");
        }

        private static string FormatNextLetterButtonLabel(M16DeliveredLettersFlags nextLetter, bool zh)
        {
            switch (nextLetter)
            {
                case M16DeliveredLettersFlags.RC01ChenYan:
                    return zh ? "拆读砚叔来信" : "Read Chen Yan's letter";
                case M16DeliveredLettersFlags.RC02ZhengYuan:
                    return zh ? "拆读郑远来信" : "Read Zheng Yuan's letter";
                case M16DeliveredLettersFlags.RC05ThirdSquad:
                    return zh ? "拆读第三班来信" : "Read Third Squad's letter";
                case M16DeliveredLettersFlags.ST02RiverLanterns:
                    return zh ? "拆读两盏河灯来信" : "Read River Lanterns letter";
                default:
                    return zh ? "已读完全部送达来信" : "All delivered letters read";
            }
        }

        private static string FormatDeliveredLetterBody(M16DeliveredLettersFlags letter, bool zh)
        {
            switch (letter)
            {
                case M16DeliveredLettersFlags.RC01ChenYan:
                    return zh
                        ? "【RC-01 砚叔来信】白芦渡水浅平稳，名册账目已按当日约定落定，嘱你保重。"
                        : "[RC-01 Chen Yan] White Reed Ferry runs calm; the roster settlement holds as agreed.";
                case M16DeliveredLettersFlags.RC02ZhengYuan:
                    return zh
                        ? "【RC-02 郑远来信】临潮户籍与名姓已各归其位，家里安稳，特寄信报一声平安。"
                        : "[RC-02 Zheng Yuan] The household and names stand settled in Linchao; all is quiet at home.";
                case M16DeliveredLettersFlags.RC05ThirdSquad:
                    return zh
                        ? "【RC-05 第三班来信】山口旧班组与盐粮接济均已按约落定，托过路船带信致谢。"
                        : "[RC-05 Third Squad] The mountain-pass settlement and supply terms hold; they send word by passing boat.";
                case M16DeliveredLettersFlags.ST02RiverLanterns:
                    return zh
                        ? "【ST-02 两盏河灯来信】河埠水闸夜灯照旧点着，两船联标契约运转如常，邀你得空再访。"
                        : "[ST-02 River Lanterns] The lock lanterns still burn at dusk and the joint shipping terms hold steady.";
                default:
                    return string.Empty;
            }
        }

        private string FormatClosingBody(bool zh)
        {
            switch (session.ClosingTone)
            {
                case M16RespectAgencyTone.RespectedOthersChoices:
                    return zh
                        ? "沈砾：「这一路，你没替他们把日子过完。每个人都自己选了路，挺好。天黑了，回去吃饭吧。」选一处站一会儿："
                        : "Shen Li: 'You let everyone choose their own road. Good. Come eat when dark falls.' Choose where to stand:";
                case M16RespectAgencyTone.DecidedForOthersOften:
                    return zh
                        ? "沈砾：「你这人啊，连别人上哪条船都要替人挑。下回替人拿主意之前，先问一句人家自己想不想。」选一处站一会儿："
                        : "Shen Li: 'Next time before deciding for someone else, ask what they want first.' Choose where to stand:";
                case M16RespectAgencyTone.PartedWaysWithClosure:
                    return zh
                        ? "沈砾把修好的旧物件递还给你：「这件东西修好了，账也清了。往后各走各的路，自己保重。」选一处站一会儿："
                        : "Shen Li hands back the mended keepsake: 'Fixed, and the ledger is clear. Take care out there.' Choose where to stand:";
                case M16RespectAgencyTone.NeutralMixedExperience:
                    return zh
                        ? "傍晚海风渐起，沈砾站在身旁看着归港的渡船，谁也没再多说旧账。选一处站一会儿："
                        : "Evening wind rises over the returning ferries beside Shen Li, with no need to tally old scores. Choose where to stand:";
                default:
                    return zh
                        ? "傍晚海风吹过你所在的港口，远处的渡船缓缓归港。选一处独自站一会儿，为这一程收尾："
                        : "Evening wind crosses the harbor as distant ferries return. Choose a quiet spot to close the voyage:";
            }
        }

        private M16DeliveredLettersFlags FindNextUnreadLetter()
        {
            for (int i = 0; i < LetterOrder.Length; i++)
            {
                M16DeliveredLettersFlags candidate = LetterOrder[i];
                if ((session.EligibleLetters & candidate) != 0 &&
                    (session.LettersRead & candidate) == 0)
                    return candidate;
            }
            return M16DeliveredLettersFlags.None;
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
        }

        private void FocusFirstInteractable()
        {
            for (int i = 0; i < choiceButtons.Length; i++)
            {
                if (choiceButtons[i].IsActive() && choiceButtons[i].IsInteractable())
                {
                    SoundwellInputAdapter.KeepDialogFocus(canvasObject.transform, choiceButtons[i]);
                    return;
                }
            }
            if (advanceButton.IsActive() && advanceButton.IsInteractable())
                SoundwellInputAdapter.KeepDialogFocus(canvasObject.transform, advanceButton);
        }

        private void HandleAdvanceClick()
        {
            if (disposed || IsDestroyedExternally() || !canvas.isActiveAndEnabled || !advanceButton.IsActive() ||
                !advanceButton.IsInteractable())
                return;

            if (session.Phase == M16Phase.AfternoonWalk && session.TryProceedToDesk())
                Refresh();
            else if (session.Phase == M16Phase.DeskLettersAndRegister && session.TryStepOutsideForEvening())
                Refresh();
        }

        private void HandleChoiceClick(int index)
        {
            if (disposed || IsDestroyedExternally() || !canvas.isActiveAndEnabled ||
                index < 0 || index >= choiceButtons.Length)
                return;
            if (!choiceButtons[index].IsActive() || !choiceButtons[index].IsInteractable())
                return;

            switch (session.Phase)
            {
                case M16Phase.AfternoonWalk:
                    if (index == 0 && session.TryVisitStop(M16WalkStops.WorkshopStop))
                        Refresh();
                    else if (index == 1 && session.TryVisitStop(M16WalkStops.QiQueWarehouseStop))
                        Refresh();
                    else if (index == 2)
                    {
                        M16WalkStops stop = (session.StopsVisited & M16WalkStops.OulinDinnerTableStop) == 0
                            ? M16WalkStops.OulinDinnerTableStop
                            : M16WalkStops.WestLaneAndHarborOfficeStop;
                        if (session.TryVisitStop(stop)) Refresh();
                    }
                    break;

                case M16Phase.DeskLettersAndRegister:
                    if (index == 0)
                    {
                        M16DeliveredLettersFlags next = FindNextUnreadLetter();
                        if (next != M16DeliveredLettersFlags.None && session.TryReadLetter(next))
                        {
                            lastReadLetter = next;
                            Refresh();
                        }
                    }
                    else if (index == 1 && session.TryReviewRegisterNotes())
                    {
                        Refresh();
                    }
                    break;

                case M16Phase.FinalStandingAndFadeOut:
                    if (submitting) return;
                    var spot = (M16StandingSpot)(index + 1);
                    submitting = true;
                    Refresh();
                    try
                    {
                        onStandingSpotSubmitted?.Invoke(spot);
                        if (session.IsCompleted)
                            onStandingSpotSubmitted = null;
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
            if (canvasObject == null || canvas == null || panel == null || heading == null || body == null ||
                advanceButton == null || advanceLabel == null)
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
            onStandingSpotSubmitted = null;
            SoundwellUiLocalization.Changed -= Refresh;
            if (advanceButton != null)
                advanceButton.onClick.RemoveAllListeners();
            for (int i = 0; i < choiceButtons.Length; i++)
            {
                if (choiceButtons[i] != null)
                    choiceButtons[i].onClick.RemoveAllListeners();
            }
            SoundwellUiTheme.SafeDestroyCanvas(canvasObject);
        }
    }
}
