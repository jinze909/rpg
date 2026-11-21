using System;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Bottom-anchored safe-region UGUI panel for RC-02 ("借来的名字").
    /// Steps through the 3 intake checks, Shi Leng's roof confession, the
    /// three-way intake decision, and the companionship-driven adoption naming
    /// moment with retry-safe Makinom persistence and ZH/EN localization.
    /// </summary>
    public sealed class RC02ZhengYuanView : IDisposable
    {
        private readonly GameObject canvasObject;
        private readonly RectTransform panel;
        private readonly Font latinFont;
        private readonly Text heading;
        private readonly Text body;
        private readonly Button[] choiceButtons = new Button[3];
        private readonly Text[] choiceLabels = new Text[3];
        private readonly RC02ZhengYuanSession session;
        private readonly Func<bool> shiLengWillingForAdoption;
        private Action<RC02Resolution> onResolutionSubmitted;
        private bool submitting;
        private bool disposed;

        public RC02ZhengYuanSession Session => session;
        public RectTransform Panel => panel;
        public Text Heading => heading;
        public Text Body => body;
        public Button ToothCheckButton => choiceButtons[0];
        public Button FlankQuestionButton => choiceButtons[1];
        public Button HandQuestionButton => choiceButtons[2];
        public Button HearConfessionButton => choiceButtons[0];
        public Button ExposeButton => choiceButtons[0];
        public Button ExceptionButton => choiceButtons[1];
        public Button BeginAdoptionButton => choiceButtons[2];
        public Button WaitQuietlyButton => choiceButtons[0];
        public Button SpeakForHerButton => choiceButtons[1];
        public Button ConfirmAdoptionButton => choiceButtons[0];

        public RC02ZhengYuanView(
            Transform parent,
            RC02ZhengYuanSession session,
            Func<bool> shiLengWillingForAdoption,
            Action<RC02Resolution> onResolutionSubmitted)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.shiLengWillingForAdoption = shiLengWillingForAdoption ??
                throw new ArgumentNullException(nameof(shiLengWillingForAdoption));
            this.onResolutionSubmitted = onResolutionSubmitted ??
                throw new ArgumentNullException(nameof(onResolutionSubmitted));

            latinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            canvasObject = new GameObject("RC02 zheng yuan canvas",
                typeof(RectTransform), typeof(Canvas));
            canvasObject.transform.SetParent(parent, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 146;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(
                SoundwellUiTheme.ReferenceWidth, SoundwellUiTheme.ReferenceHeight);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();

            var holder = new GameObject("RC02 zheng yuan bottom panel", typeof(RectTransform));
            holder.transform.SetParent(canvasObject.transform, false);
            panel = (RectTransform)holder.transform;
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0f);
            panel.sizeDelta = new Vector2(904f, 256f);
            panel.anchoredPosition = new Vector2(0f, 138f);

            SoundwellUiTheme.CreatePanel(panel, "RC02 zheng yuan border",
                new Vector2(904f, 256f), new Color(0.64f, 0.56f, 0.42f, 1f));
            SoundwellUiTheme.CreatePanel(panel, "RC02 zheng yuan backing",
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
                    panel, "RC02 zheng yuan choice " + i, positions[i],
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

            switch (session.Stage)
            {
                case RC02Stage.IntakeVerification:
                    heading.text = zh ? "RC02 · 镜湾接纳处核验" : "RC02 · JINGWAN INTAKE CHECK";
                    body.text = zh
                        ? "伊蕾放下笔低声说：「牙对不上，口音也偏。我不能盖，也不想拒。你陪我核清三处。」"
                        : "Yi Lei sets down her pen: \"The tooth and accent do not match. Help me verify three details.\"";
                    ShowChoice(0, zh ? "核验左上第二颗牙" : "Check upper-left tooth",
                        (session.ChecksCompleted & RC02IntakeChecks.UpperLeftToothCheck) == 0);
                    ShowChoice(1, zh ? "问斥候那一仗进路" : "Ask scout battle flank",
                        (session.ChecksCompleted & RC02IntakeChecks.BattleFlankQuestion) == 0);
                    ShowChoice(2, zh ? "问老妇人儿子惯用手" : "Ask mother dominant hand",
                        (session.ChecksCompleted & RC02IntakeChecks.DominantHandQuestion) == 0);
                    FocusFirstInteractable();
                    break;

                case RC02Stage.RoofConfession:
                    heading.text = zh ? "RC02 · 修了三次的屋顶" : "RC02 · REPAIRED ROOF";
                    body.text = zh
                        ? "晚上棚屋屋檐下。瓦片颜色不一，补过三次。男人没有看你：「您是来拆我的吧。」"
                        : "Evening under the patched eaves. The man does not look at you: \"You came to expose me.\"";
                    ShowChoice(0, zh ? "听石棱说清来由与老妇人的话" : "Hear Shi Leng's confession");
                    FocusFirstInteractable();
                    break;

                case RC02Stage.DeskDecision:
                    heading.text = zh ? "RC02 · 接纳处落笔" : "RC02 · INTAKE DESK DECISION";
                    body.text = zh
                        ? "石棱说她第一眼就知道，只说「我儿子没有这么温柔」。伊蕾：「你的选择，我只负责做。」"
                        : "Shi Leng says she knew at first sight. Yi Lei: \"Your choice; I will execute it.\"";
                    ShowChoice(0, zh ? "揭穿（退回债籍，守住制度）" : "Expose (return to debt)", !submitting);
                    ShowChoice(1, zh ? "默认放行（伊蕾签例外条款）" : "Allow by exception (Yi Lei signs)", !submitting);
                    ShowChoice(2, zh ? "商谈养子收养（须叫「石棱」）" : "Negotiate adoption as Shi Leng", !submitting);
                    if (!submitting) FocusFirstInteractable();
                    break;

                case RC02Stage.AdoptionNameMoment:
                    heading.text = zh ? "RC02 · 当面叫他的名字" : "RC02 · CALLING HIS NAME";
                    if (session.MotherCalledShiLeng && session.ShiLengConsented)
                    {
                        body.text = zh
                            ? "老妇人：「……石棱。」石棱把脸转向一边，点了头：「娘。」伊蕾提笔准备分开登记两个名字。"
                            : "The mother speaks: \"...Shi Leng.\" Shi Leng turns aside and nods: \"Mother.\" Yi Lei lifts her pen.";
                        ShowChoice(0, zh ? "落笔登记养子「石棱」" : "Record adoption as Shi Leng", !submitting);
                    }
                    else
                    {
                        body.text = zh
                            ? "老妇人看着石棱，张了张口：「沅……」她停住，嘴唇抿紧了。窗外的海风吹动了纸。"
                            : "The mother looks at Shi Leng: \"Yuan...\" She stops, lips pressed tight. Sea wind rustles the papers.";
                        ShowChoice(0, zh ? "安静等她" : "Wait quietly");
                        ShowChoice(1, zh ? "替她答：「慢慢来。」" : "Speak for her: \"Take your time.\"");
                    }
                    if (!submitting) FocusFirstInteractable();
                    break;

                case RC02Stage.Completed:
                    heading.text = zh ? "RC02 · 郑沅案已落笔" : "RC02 · CASE RECORDED";
                    body.text = zh
                        ? "接纳处登记结果已落盘保存。"
                        : "The intake desk registration outcome has been recorded.";
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

            switch (session.Stage)
            {
                case RC02Stage.IntakeVerification:
                    RC02IntakeChecks check = index == 0
                        ? RC02IntakeChecks.UpperLeftToothCheck
                        : index == 1
                            ? RC02IntakeChecks.BattleFlankQuestion
                            : RC02IntakeChecks.DominantHandQuestion;
                    if (session.TryCompleteIntakeCheck(check)) Refresh();
                    break;

                case RC02Stage.RoofConfession:
                    if (index == 0 && session.TryHearRoofConfession()) Refresh();
                    break;

                case RC02Stage.DeskDecision:
                    if (submitting) return;
                    if (index == 2)
                    {
                        if (session.TryBeginAdoptionNegotiation()) Refresh();
                        return;
                    }
                    SubmitResolution(index == 0
                        ? RC02Resolution.ExposeImpostor
                        : RC02Resolution.AllowByException);
                    break;

                case RC02Stage.AdoptionNameMoment:
                    if (submitting) return;
                    if (session.MotherCalledShiLeng && session.ShiLengConsented)
                    {
                        if (index == 0)
                            SubmitResolution(RC02Resolution.AdoptAsShiLeng);
                        return;
                    }
                    var companionship = index == 0
                        ? RC02AdoptionCompanionship.WaitQuietly
                        : RC02AdoptionCompanionship.SpeakForHer;
                    session.TryResolveAdoptionMoment(companionship, shiLengWillingForAdoption());
                    Refresh();
                    break;
            }
        }

        private void SubmitResolution(RC02Resolution resolution)
        {
            submitting = true;
            Refresh();
            try
            {
                onResolutionSubmitted?.Invoke(resolution);
                if (session.IsResolved)
                    onResolutionSubmitted = null;
            }
            finally
            {
                submitting = false;
                if (!disposed) Refresh();
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
            onResolutionSubmitted = null;
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
