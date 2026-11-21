using System;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Bottom-anchored safe-region UGUI panel for RC-01 ("砚叔的两个家").
    /// Steps through the 3 ferry clues, Chen Yan's identity confrontation,
    /// order-sensitive interviews with Lin Su and Qiuniang, and the three-way
    /// roster decision with retry-safe Makinom persistence and ZH/EN support.
    /// </summary>
    public sealed class RC01ChenYanView : IDisposable
    {
        private readonly GameObject canvasObject;
        private readonly RectTransform panel;
        private readonly Font latinFont;
        private readonly Text heading;
        private readonly Text body;
        private readonly Button[] choiceButtons = new Button[3];
        private readonly Text[] choiceLabels = new Text[3];
        private readonly RC01ChenYanSession session;
        private Action<RC01ChenYanOutcome> onOutcomeSubmitted;
        private bool submitting;
        private bool disposed;

        public RC01ChenYanSession Session => session;
        public RectTransform Panel => panel;
        public Text Heading => heading;
        public Text Body => body;
        public Button WristScarButton => choiceButtons[0];
        public Button DoughTuneButton => choiceButtons[1];
        public Button ShrineTabletButton => choiceButtons[2];
        public Button ConfrontButton => choiceButtons[0];
        public Button TalkLinSuButton => choiceButtons[0];
        public Button TalkQiuniangButton => choiceButtons[1];
        public Button RestoreLivingButton => choiceButtons[0];
        public Button KeepDeadButton => choiceButtons[1];
        public Button NewReturneeNameButton => choiceButtons[2];

        public RC01ChenYanView(
            Transform parent,
            RC01ChenYanSession session,
            Action<RC01ChenYanOutcome> onOutcomeSubmitted)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.onOutcomeSubmitted = onOutcomeSubmitted ??
                throw new ArgumentNullException(nameof(onOutcomeSubmitted));

            latinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            canvasObject = new GameObject("RC01 chen yan canvas",
                typeof(RectTransform), typeof(Canvas));
            canvasObject.transform.SetParent(parent, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 145;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(
                SoundwellUiTheme.ReferenceWidth, SoundwellUiTheme.ReferenceHeight);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();

            var holder = new GameObject("RC01 chen yan bottom panel", typeof(RectTransform));
            holder.transform.SetParent(canvasObject.transform, false);
            panel = (RectTransform)holder.transform;
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0f);
            panel.sizeDelta = new Vector2(904f, 256f);
            panel.anchoredPosition = new Vector2(0f, 138f);

            SoundwellUiTheme.CreatePanel(panel, "RC01 chen yan border",
                new Vector2(904f, 256f), new Color(0.64f, 0.56f, 0.42f, 1f));
            SoundwellUiTheme.CreatePanel(panel, "RC01 chen yan backing",
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
                    panel, "RC01 chen yan choice " + i, positions[i],
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
                case RC01InvestigationStage.FerryClues:
                    heading.text = zh ? "RC01 · 白苇渡口查访" : "RC01 · WHITE REED FERRY CLUES";
                    body.text = zh
                        ? "砚叔矢口否认当过兵。你在渡口住下帮他撑船搬柴，须核实三处细节再与他对质。"
                        : "Uncle Yan denies ever serving. Stay at the ferry and verify three daily details before confronting him.";
                    ShowChoice(0, zh ? "留意左手腕旧烫疤" : "Check wrist burn scar",
                        (session.CluesFound & RC01FerryClues.WristScar) == 0);
                    ShowChoice(1, zh ? "听收摊时的揉面小调" : "Listen to dough-kneading tune",
                        (session.CluesFound & RC01FerryClues.DoughHumTune) == 0);
                    ShowChoice(2, zh ? "查看小祠的无字木牌位" : "Inspect blank shrine tablet",
                        (session.CluesFound & RC01FerryClues.BlankShrineTablet) == 0);
                    FocusFirstInteractable();
                    break;

                case RC01InvestigationStage.IdentityConfrontation:
                    heading.text = zh ? "RC01 · 渡口岸边对质" : "RC01 · FERRY CONFRONTATION";
                    body.text = zh
                        ? "三处细节与临潮旧档全部对上。将手腕烫疤、揉面小调与无字牌位摆在砚叔面前。"
                        : "All three details match the Linchao archives. Confront Uncle Yan by the river.";
                    ShowChoice(0, zh ? "摆出三处细节与旧档" : "Present the three clues & archive");
                    FocusFirstInteractable();
                    break;

                case RC01InvestigationStage.TwoHomesTalk:
                    heading.text = zh ? "RC01 · 两边的话" : "RC01 · BOTH HOUSEHOLDS";
                    string note = session.Atmosphere == RC01TalkAtmosphere.GuardedWithQiuniang
                        ? (zh
                            ? "你带着林素托付的旧布鞋底去渡口，秋娘一眼看见便紧绷防备，将阿河护在身后。"
                            : "Carrying Lin Su's half-stitched shoe sole makes Qiuniang tense and protective of A-He.")
                        : session.Atmosphere == RC01TalkAtmosphere.CalmWithLinSu
                            ? (zh
                                ? "你带着秋娘包的炒盐葱油饼去临潮，林素一闻炒盐香便知他安稳，气氛平静释然。"
                                : "Bringing Qiuniang's roasted-salt flatbread lets Lin Su know he is safe, keeping the talk calm.")
                            : (zh
                                ? "陈砚承认身份，称八年前城门外见林素已改嫁便未敲门。须分别听林素与秋娘陈述。"
                                : "Chen Yan admits who he is. Speak with both Lin Su in Linchao and Qiuniang at the ferry.");
                    body.text = note;
                    ShowChoice(0, zh ? "去临潮城找林素谈" : "Talk to Lin Su in Linchao", !session.LinSuTalked);
                    ShowChoice(1, zh ? "在白苇渡口找秋娘谈" : "Talk to Qiuniang at ferry", !session.QiuniangTalked);
                    FocusFirstInteractable();
                    break;

                case RC01InvestigationStage.RosterDecision:
                    heading.text = zh ? "RC01 · 名册上怎么落笔" : "RC01 · ROSTER ENTRY DECISION";
                    body.text = zh
                        ? "陈砚：「你怎么写，我就怎么认。」林素与秋娘皆已陈述，请选择名册落笔方式（林素将留下一行笔迹）："
                        : "Chen Yan: \"Whatever you write, I will accept.\" Choose how to record him on the roster:";
                    ShowChoice(0, zh ? "恢复「在世」（前婚恢复）" : "Restore living name", !submitting);
                    ShowChoice(1, zh ? "保持「战死」（两家各自完整）" : "Keep marked dead", !submitting);
                    ShowChoice(2, zh ? "办「归还者」新名字" : "Issue new returnee name", !submitting);
                    if (!submitting) FocusFirstInteractable();
                    break;

                case RC01InvestigationStage.Completed:
                    heading.text = zh ? "RC01 · 陈砚案已落笔" : "RC01 · ROSTER ENTRY RECORDED";
                    body.text = zh
                        ? "名册上陈砚一页已落笔，并记入林素亲手写下的一行字。"
                        : "Chen Yan's roster entry and Lin Su's handwritten register note have been recorded.";
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
                case RC01InvestigationStage.FerryClues:
                    RC01FerryClues clue = index == 0
                        ? RC01FerryClues.WristScar
                        : index == 1
                            ? RC01FerryClues.DoughHumTune
                            : RC01FerryClues.BlankShrineTablet;
                    if (session.TryObserveClue(clue)) Refresh();
                    break;

                case RC01InvestigationStage.IdentityConfrontation:
                    if (index == 0 && session.TryConfrontAtFerry()) Refresh();
                    break;

                case RC01InvestigationStage.TwoHomesTalk:
                    if (index == 0 && session.TryTalkToLinSu()) Refresh();
                    else if (index == 1 && session.TryTalkToQiuniang()) Refresh();
                    break;

                case RC01InvestigationStage.RosterDecision:
                    if (submitting) return;
                    var outcome = (RC01ChenYanOutcome)(index + 1);
                    submitting = true;
                    Refresh();
                    try
                    {
                        onOutcomeSubmitted?.Invoke(outcome);
                        if (session.IsResolved)
                            onOutcomeSubmitted = null;
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
            onOutcomeSubmitted = null;
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
