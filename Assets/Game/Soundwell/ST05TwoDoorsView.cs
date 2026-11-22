using System;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Bottom-anchored safe-region UGUI panel for ST-05 ("两扇门的乔迁宴").
    /// Drives staircase bed clearance, flea-market gift testing, courtyard wind
    /// mediation, partner-to-guest talks, mutual living arrangement commit, and
    /// Cheng Wu's own local vs outside-survey decision with ZH/EN localization.
    /// </summary>
    public sealed class ST05TwoDoorsView : IDisposable
    {
        private readonly GameObject canvasObject;
        private readonly RectTransform panel;
        private readonly Font latinFont;
        private readonly Text heading;
        private readonly Text body;
        private readonly Button[] choiceButtons = new Button[3];
        private readonly Text[] choiceLabels = new Text[3];
        private readonly ST05TwoDoorsSession session;
        private readonly Func<bool> returnHaulageFeePaid;
        private readonly Func<bool> marketGiftsTested;
        private readonly Func<bool> courtyardResolved;
        private readonly Func<bool> partnersHeard;
        private readonly Func<bool> mutualTrialConsent;
        private readonly Func<bool> chengJobTermsReady;
        private Action onHomeCommitRequested;
        private Action onAssignmentCommitRequested;
        private ST05HomeArrangement? pendingArrangement;
        private ST05ChengAssignment? pendingAssignment;
        private bool submitting;
        private bool disposed;

        public ST05TwoDoorsSession Session => session;
        public RectTransform Panel => panel;
        public Text Heading => heading;
        public Text Body => body;
        public Button RemoveDoorFrameButton => choiceButtons[0];
        public Button ConvertToBenchesButton => choiceButtons[1];
        public Button ReturnBedButton => choiceButtons[2];
        public Button TestMarketGiftsButton => choiceButtons[0];
        public Button ResolveCourtyardButton => choiceButtons[0];
        public Button HearBothPartnersButton => choiceButtons[0];
        public Button KeepTwoDoorsButton => choiceButtons[0];
        public Button TrialSharedHomeButton => choiceButtons[1];
        public Button ChengStayLocalButton => choiceButtons[0];
        public Button ChengOutsideSurveyButton => choiceButtons[1];
        public bool IsDisposed => disposed || CanvasDestroyed();

        public ST05TwoDoorsView(
            Transform parent,
            ST05TwoDoorsSession session,
            Func<bool> returnHaulageFeePaid,
            Func<bool> marketGiftsTested,
            Func<bool> courtyardResolved,
            Func<bool> partnersHeard,
            Func<bool> mutualTrialConsent,
            Func<bool> chengJobTermsReady,
            Action onHomeCommitRequested,
            Action onAssignmentCommitRequested)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.returnHaulageFeePaid = returnHaulageFeePaid ??
                throw new ArgumentNullException(nameof(returnHaulageFeePaid));
            this.marketGiftsTested = marketGiftsTested ??
                throw new ArgumentNullException(nameof(marketGiftsTested));
            this.courtyardResolved = courtyardResolved ??
                throw new ArgumentNullException(nameof(courtyardResolved));
            this.partnersHeard = partnersHeard ??
                throw new ArgumentNullException(nameof(partnersHeard));
            this.mutualTrialConsent = mutualTrialConsent ??
                throw new ArgumentNullException(nameof(mutualTrialConsent));
            this.chengJobTermsReady = chengJobTermsReady ??
                throw new ArgumentNullException(nameof(chengJobTermsReady));
            this.onHomeCommitRequested = onHomeCommitRequested ??
                throw new ArgumentNullException(nameof(onHomeCommitRequested));
            this.onAssignmentCommitRequested = onAssignmentCommitRequested ??
                throw new ArgumentNullException(nameof(onAssignmentCommitRequested));

            latinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            canvasObject = new GameObject("ST05 two doors canvas",
                typeof(RectTransform), typeof(Canvas));
            canvasObject.transform.SetParent(parent, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 164;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(
                SoundwellUiTheme.ReferenceWidth, SoundwellUiTheme.ReferenceHeight);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();

            var holder = new GameObject("ST05 two doors bottom panel", typeof(RectTransform));
            holder.transform.SetParent(canvasObject.transform, false);
            panel = (RectTransform)holder.transform;
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0f);
            panel.sizeDelta = new Vector2(904f, 256f);
            panel.anchoredPosition = new Vector2(0f, 138f);

            SoundwellUiTheme.CreatePanel(panel, "ST05 two doors border",
                new Vector2(904f, 256f), new Color(0.68f, 0.55f, 0.42f, 1f));
            SoundwellUiTheme.CreatePanel(panel, "ST05 two doors backing",
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
                    panel, "ST05 two doors choice " + i, positions[i],
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
                case ST05TwoDoorsPhase.UnblockStaircase:
                    heading.text = zh ? "ST05 · 卡在楼梯上的双人床" : "ST05 · STUCK STAIRCASE BED";
                    body.text = zh
                        ? "十七号与十八号公用楼梯被亲友硬送的大双人床卡死。选择处理那张床的办法："
                        : "A double bed sent by relatives jams the shared stairs between Doors 17 and 18. Choose how to clear it:";
                    ShowChoice(0, zh ? "拆下门框把床搬开" : "Remove doorframe & move bed");
                    ShowChoice(1, zh ? "请木匠改成两张院中长椅" : "Convert into two yard benches");
                    ShowChoice(2, zh ? "联系店家退货并付搬运费" : "Return to shop & pay haulage");
                    FocusFirstInteractable();
                    break;

                case ST05TwoDoorsPhase.TestMarketGifts:
                    heading.text = zh ? "ST05 · 旧货市场挑实用礼物" : "ST05 · FLEA MARKET PRACTICAL GIFTS";
                    body.text = zh
                        ? "在旧货市场试夜画防风灯、看面包散热石板裂纹，并向摊主打听街区门道（最贵的不一定最好）。"
                        : "Test the night drafting lamp, check the bread-cooling stone slab for cracks, and listen to stallholder advice.";
                    ShowChoice(0, zh ? "确认灯具、石板与街区消息" : "Confirm tested lamp & slab");
                    FocusFirstInteractable();
                    break;

                case ST05TwoDoorsPhase.ClearCourtyardConflict:
                    heading.text = zh ? "ST05 · 小院晚宴海风与烤炉" : "ST05 · COURTYARD WIND & OVEN";
                    body.text = zh
                        ? "海风把测绘图吹向烤炉。追回散落图页、移开晾图架并请客人让路，把场地先稳住。"
                        : "Sea wind blows drafting sheets toward the oven. Recover the maps, move the rack, and clear guest traffic.";
                    ShowChoice(0, zh ? "追回图页、移晾架并请客让路" : "Recover maps & clear courtyard");
                    FocusFirstInteractable();
                    break;

                case ST05TwoDoorsPhase.HearBothPartners:
                    heading.text = zh ? "ST05 · 钱、钥匙与时间怎么分" : "ST05 · HEAR BOTH PARTNERS";
                    body.text = zh
                        ? "郁禾向姨妈说明、程午向朋友说清，两人面对面谈清店铺进出、钥匙访客与出差成本。"
                        : "Yu He and Cheng Wu speak to relatives and friends themselves, then talk through shop access, keys, and travel costs.";
                    ShowChoice(0, zh ? "听完两人各自表态与实账" : "Finish partner & cost talks");
                    FocusFirstInteractable();
                    break;

                case ST05TwoDoorsPhase.AgreeHomeAndKeys:
                    heading.text = zh ? "ST05 · 确认两扇门居住方案" : "ST05 · LIVING ARRANGEMENT";
                    body.text = zh
                        ? "由郁禾与程午共同确认居住与工作室安排（仅双方明确同意才可选试住合用）："
                        : "Record the living arrangement agreed by Yu He and Cheng Wu (trial cohabitation requires mutual consent):";
                    ShowChoice(0, zh ? "保留两扇门、设院中长桌" : "Keep two doors & yard table",
                        !submitting && (!pendingArrangement.HasValue ||
                            pendingArrangement.Value == ST05HomeArrangement.KeepTwoDoorsAndCourtyardTable));
                    ShowChoice(1, zh ? "双方同意试住合用、另间改工作室" : "Mutual trial shared home & studio",
                        !submitting && mutualTrialConsent() && (!pendingArrangement.HasValue ||
                            pendingArrangement.Value == ST05HomeArrangement.MutuallyAgreedTrialSharedHomeAndStudio));
                    if (!submitting) FocusFirstInteractable();
                    break;

                case ST05TwoDoorsPhase.AwaitChengOwnJobDecision:
                    heading.text = zh ? "ST05 · 程午本人的外派抉择" : "ST05 · CHENG WU'S OWN DECISION";
                    body.text = zh
                        ? "核对完店铺照看、钥匙与归期后，由程午本人决定留本地或接下下一季外地测绘："
                        : "After reviewing shop cover, keys, and return dates, record Cheng Wu's own decision:";
                    ShowChoice(0, zh ? "程午决定留在本地" : "Cheng decides to stay local",
                        !submitting && (!pendingAssignment.HasValue ||
                            pendingAssignment.Value == ST05ChengAssignment.StayLocal));
                    ShowChoice(1, zh ? "程午本人接下外地测绘" : "Cheng accepts outside survey",
                        !submitting && (!pendingAssignment.HasValue ||
                            pendingAssignment.Value == ST05ChengAssignment.AcceptOutsideSurvey));
                    if (!submitting) FocusFirstInteractable();
                    break;

                case ST05TwoDoorsPhase.Completed:
                    heading.text = zh ? "ST05 · 两扇门的乔迁宴已落盘" : "ST05 · TWO DOORS SAVED";
                    body.text = zh
                        ? "十七号与十八号居住安排及程午决定已写入记录。"
                        : "The Two Doors household agreement and Cheng Wu's decision have been saved.";
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
                case ST05TwoDoorsPhase.UnblockStaircase:
                    var bed = (ST05BedResolution)(index + 1);
                    if (session.TryClearStaircase(bed, returnHaulageFeePaid())) Refresh();
                    break;

                case ST05TwoDoorsPhase.TestMarketGifts:
                    bool giftsOk = marketGiftsTested();
                    if (index == 0 && session.TryChoosePracticalGifts(giftsOk, giftsOk, giftsOk))
                        Refresh();
                    break;

                case ST05TwoDoorsPhase.ClearCourtyardConflict:
                    bool yardOk = courtyardResolved();
                    if (index == 0 && session.TryResolveSharedCourtyard(yardOk, yardOk, yardOk))
                        Refresh();
                    break;

                case ST05TwoDoorsPhase.HearBothPartners:
                    bool heardOk = partnersHeard();
                    if (index == 0 && session.TryHearOwnWords(heardOk, heardOk, heardOk, heardOk, heardOk))
                        Refresh();
                    break;

                case ST05TwoDoorsPhase.AgreeHomeAndKeys:
                    if (submitting) return;
                    var arr = index == 0
                        ? ST05HomeArrangement.KeepTwoDoorsAndCourtyardTable
                        : ST05HomeArrangement.MutuallyAgreedTrialSharedHomeAndStudio;
                    if (pendingArrangement.HasValue && pendingArrangement.Value != arr)
                        return;
                    if (!pendingArrangement.HasValue)
                    {
                        bool consent = arr == ST05HomeArrangement.MutuallyAgreedTrialSharedHomeAndStudio &&
                            mutualTrialConsent();
                        if (!session.TryAgreeLivingArrangement(arr, consent)) return;
                        pendingArrangement = arr;
                    }

                    submitting = true;
                    Refresh();
                    try
                    {
                        onHomeCommitRequested?.Invoke();
                    }
                    finally
                    {
                        submitting = false;
                        if (!disposed) Refresh();
                    }
                    break;

                case ST05TwoDoorsPhase.AwaitChengOwnJobDecision:
                    if (submitting) return;
                    var job = index == 0
                        ? ST05ChengAssignment.StayLocal
                        : ST05ChengAssignment.AcceptOutsideSurvey;
                    if (pendingAssignment.HasValue && pendingAssignment.Value != job)
                        return;
                    if (!pendingAssignment.HasValue)
                    {
                        bool termsOk = chengJobTermsReady();
                        if (!session.TryHearChengAssignment(job, termsOk, termsOk, termsOk, termsOk))
                            return;
                        pendingAssignment = job;
                    }

                    submitting = true;
                    Refresh();
                    try
                    {
                        onAssignmentCommitRequested?.Invoke();
                        if (session.Phase == ST05TwoDoorsPhase.Completed)
                        {
                            onHomeCommitRequested = null;
                            onAssignmentCommitRequested = null;
                        }
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
            onHomeCommitRequested = null;
            onAssignmentCommitRequested = null;
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