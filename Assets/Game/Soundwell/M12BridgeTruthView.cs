using System;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Bottom-anchored safe-region UGUI panel for M12 ("一面旗帜不等于每个人").
    /// Drives Lu Jun's bell-tower confrontation (gating "You said that last time"
    /// on M03 prior confession), order-independent stakeholder statements (never
    /// summoning an absent Shen Li while keeping PublishTruth open), optional
    /// RC-01 Lin Su echo, and truth disclosure confirmation with ZH/EN support.
    /// </summary>
    public sealed class M12BridgeTruthView : IDisposable
    {
        private readonly GameObject canvasObject;
        private readonly RectTransform panel;
        private readonly Font latinFont;
        private readonly Text heading;
        private readonly Text body;
        private readonly Button[] choiceButtons = new Button[3];
        private readonly Text[] choiceLabels = new Text[3];
        private readonly Button linSuEchoButton;
        private readonly Text linSuEchoLabel;
        private readonly M12BridgeTruthSession session;
        private readonly Func<bool> m10HanExposedPublicly;
        private Action<M12TruthDisclosure> onDisclosureSubmitted;
        private bool submitting;
        private bool disposed;

        public M12BridgeTruthSession Session => session;
        public RectTransform Panel => panel;
        public Text Heading => heading;
        public Text Body => body;
        public Button SomeWantNoMoreWarButton => choiceButtons[0];
        public Button YouTookTheirRoadButton => choiceButtons[1];
        public Button YouSaidThatLastTimeButton => choiceButtons[2];
        public Button HearJiangShenButton => choiceButtons[0];
        public Button HearHanAndYiLeiButton => choiceButtons[1];
        public Button HearShenLiButton => choiceButtons[2];
        public Button LinSuEchoButton => linSuEchoButton;
        public Button PublishTruthButton => choiceButtons[0];
        public Button WillingKnowersButton => choiceButtons[1];
        public Button SealTruthButton => choiceButtons[2];
        public bool IsDisposed => disposed || CanvasDestroyed();

        public M12BridgeTruthView(
            Transform parent,
            M12BridgeTruthSession session,
            Func<bool> m10HanExposedPublicly,
            Action<M12TruthDisclosure> onDisclosureSubmitted)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.m10HanExposedPublicly = m10HanExposedPublicly ??
                throw new ArgumentNullException(nameof(m10HanExposedPublicly));
            this.onDisclosureSubmitted = onDisclosureSubmitted ??
                throw new ArgumentNullException(nameof(onDisclosureSubmitted));

            latinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            canvasObject = new GameObject("M12 bridge truth canvas",
                typeof(RectTransform), typeof(Canvas));
            canvasObject.transform.SetParent(parent, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 154;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(
                SoundwellUiTheme.ReferenceWidth, SoundwellUiTheme.ReferenceHeight);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();

            var holder = new GameObject("M12 bridge truth bottom panel", typeof(RectTransform));
            holder.transform.SetParent(canvasObject.transform, false);
            panel = (RectTransform)holder.transform;
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0f);
            panel.sizeDelta = new Vector2(904f, 256f);
            panel.anchoredPosition = new Vector2(0f, 138f);

            SoundwellUiTheme.CreatePanel(panel, "M12 bridge truth border",
                new Vector2(904f, 256f), new Color(0.64f, 0.52f, 0.40f, 1f));
            SoundwellUiTheme.CreatePanel(panel, "M12 bridge truth backing",
                new Vector2(900f, 252f), new Color(0.06f, 0.09f, 0.11f, 0.97f));
            Color ink = new Color(0.93f, 0.89f, 0.81f, 1f);

            heading = SoundwellUiTheme.CreateLabel(panel, 19,
                new Vector2(-110f, 102f), new Vector2(620f, 28f), ink);
            heading.alignment = TextAnchor.MiddleLeft;

            linSuEchoButton = SoundwellUiTheme.CreateButton(
                panel, "M12 lin su echo button", new Vector2(312f, 102f),
                new Vector2(218f, 28f), 12, out linSuEchoLabel);
            linSuEchoButton.onClick.AddListener(HandleLinSuEchoClick);

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
                    panel, "M12 bridge truth choice " + i, positions[i],
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
            if (float.IsNaN(aspect) || float.IsInfinity(aspect) || aspect <= 0f)
                return;
            SoundwellUiTheme.ApplyViewportAspectScale(panel, aspect);
        }

        public void Refresh()
        {
            if (disposed || IsDestroyedExternally()) return;

            bool zh = SoundwellUiLocalization.Language == SoundwellUiLocalization.Chinese;
            Font font = SoundwellUiLocalization.PresentationFont(latinFont);
            heading.font = body.font = linSuEchoLabel.font = font;
            for (int i = 0; i < choiceLabels.Length; i++)
                choiceLabels[i].font = font;

            for (int i = 0; i < choiceButtons.Length; i++)
            {
                choiceButtons[i].gameObject.SetActive(false);
                choiceButtons[i].interactable = false;
            }
            linSuEchoButton.gameObject.SetActive(false);
            linSuEchoButton.interactable = false;

            switch (session.Phase)
            {
                case M12Phase.BellTowerConfrontation:
                    heading.text = zh ? "M12 · 钟楼下对质陆钧" : "M12 · BELL TOWER CONFRONTATION";
                    body.text = session.M03PriorConfession
                        ? (zh
                            ? "陆钧摆出救回的四百一十二人名册与借据，你也放下他副手没收的船票名单。他承认那是自己的命令：桥这头一万四千，桥那头一千一百。"
                            : "Lu Jun lays out his 412-name rescue ledger beside the confiscated tickets and admits the order was his: 14,000 on one side, 1,100 on the other.")
                        : (zh
                            ? "陆钧摆出四百一十二人名册与借据，承认扣船票是他的命令；你此前在 M03 未问过断桥旧事，他正看着钟楼准备亲口交代三更总帅令。"
                            : "Lu Jun admits the ticket confiscation was his order; since you did not ask about the bridge in M03, he prepares to confess the third-watch order here.");
                    ShowChoice(0, zh ? "「有人不想再打。」" : "'Some want no more war.'");
                    ShowChoice(1, zh ? "「你把别人的路收走了。」" : "'You took their road away.'");
                    if (session.M03PriorConfession)
                        ShowChoice(2, zh ? "「你上次也是这么说的。」" : "'You said that last time.'");
                    FocusFirstInteractable();
                    break;

                case M12Phase.StakeholderStatements:
                    heading.text = zh ? "M12 · 听取各方陈述" : "M12 · STAKEHOLDER STATEMENTS";
                    string hanNote = m10HanExposedPublicly()
                        ? (zh ? "（韩峤已在 M10 去职，以签字当事人身份作证。）"
                              : " (Han Qiao left office in M10 and testifies as signatory.)")
                        : string.Empty;
                    string linNote = session.LinSuEchoHeard
                        ? (zh ? " 林素提醒：「你让他们知道，还是让他们安心，我是过来人。」"
                              : " Lin Su: 'Whether they know or stay at peace—I've lived through it.'")
                        : string.Empty;
                    body.text = (zh
                        ? "回到关外据点，渡口之夜的真相说与不说各有沉重代价。请逐一听完在场当事人的陈述："
                        : "Back at camp, each choice over the bridge truth carries real cost. Hear the present stakeholders in any order:") + hanNote + linNote;

                    ShowChoice(0, zh ? "听姜婶陈述（主张封存）" : "Hear Aunt Jiang (seal)",
                        (session.StatementsHeard & M12StatementFlags.JiangShenHeard) == 0);
                    ShowChoice(1, zh ? "听韩峤与伊蕾（愿知者名册）" : "Hear Han & Yi Lei (ledger)",
                        (session.StatementsHeard & M12StatementFlags.HanAndYiLeiHeard) == 0);
                    if (session.ShenLiParticipating)
                        ShowChoice(2, zh ? "听沈砾陈述（主张公开）" : "Hear Shen Li (publish)",
                            (session.StatementsHeard & M12StatementFlags.ShenLiHeard) == 0);

                    if (session.LinSuEligible && !session.LinSuEchoHeard)
                    {
                        linSuEchoLabel.text = zh ? "听林素回响（RC-01）" : "Hear Lin Su (RC-01)";
                        linSuEchoButton.gameObject.SetActive(true);
                        linSuEchoButton.interactable = true;
                    }
                    FocusFirstInteractable();
                    break;

                case M12Phase.TruthDisclosureChoice:
                    heading.text = zh ? "M12 · 渡口真相的抉择" : "M12 · BRIDGE TRUTH DISCLOSURE";
                    string m10Warning = m10HanExposedPublicly()
                        ? (zh ? " M10 已公开的战报底稿不可撤回。" : " M10's published war-report draft cannot be revoked.")
                        : string.Empty;
                    string secrecyNote = session.M03SecrecyPromised
                        ? (zh ? " 你曾在 M03 答应替陆钧保密。" : " You promised Lu Jun secrecy in M03.")
                        : string.Empty;
                    body.text = (zh
                        ? "陈述已听完。渡口三更断桥的真相，由你落笔定调："
                        : "All statements are heard. Decide how to handle the truth of the third-watch bridge cut:") + m10Warning + secrecyNote;
                    ShowChoice(0, zh ? "公开真相（重估抚恤与碑文）" : "Publish truth publicly", !submitting);
                    ShowChoice(1, zh ? "设「愿知者名册」（交底稿）" : "Willing Knowers Ledger", !submitting);
                    ShowChoice(2, zh ? "封存不公开（护住眼下念想）" : "Seal the truth", !submitting);
                    if (!submitting) FocusFirstInteractable();
                    break;

                case M12Phase.Completed:
                    heading.text = zh ? "M12 · 渡口真相已落笔" : "M12 · BRIDGE TRUTH RECORDED";
                    body.text = zh
                        ? "M12 渡口真相处置结果已落盘保存。"
                        : "The M12 bridge-truth disclosure decision has been saved.";
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

        private void HandleLinSuEchoClick()
        {
            if (disposed || IsDestroyedExternally() || !linSuEchoButton.gameObject.activeSelf ||
                !linSuEchoButton.interactable)
                return;
            if (session.TryHearLinSuEcho()) Refresh();
        }

        private void HandleChoiceClick(int index)
        {
            if (disposed || IsDestroyedExternally() || index < 0 || index >= choiceButtons.Length)
                return;
            if (!choiceButtons[index].gameObject.activeSelf || !choiceButtons[index].interactable)
                return;

            switch (session.Phase)
            {
                case M12Phase.BellTowerConfrontation:
                    var reply = (M12LuConfrontationReply)(index + 1);
                    if (session.TryConfrontLuJun(reply)) Refresh();
                    break;

                case M12Phase.StakeholderStatements:
                    M12StatementFlags flag = index == 0
                        ? M12StatementFlags.JiangShenHeard
                        : index == 1
                            ? M12StatementFlags.HanAndYiLeiHeard
                            : M12StatementFlags.ShenLiHeard;
                    if (session.TryHearStatement(flag)) Refresh();
                    break;

                case M12Phase.TruthDisclosureChoice:
                    if (submitting) return;
                    var disclosure = (M12TruthDisclosure)(index + 1);
                    submitting = true;
                    Refresh();
                    try
                    {
                        onDisclosureSubmitted?.Invoke(disclosure);
                        if (session.IsCompleted)
                            onDisclosureSubmitted = null;
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
            if (!CanvasDestroyed())
                return false;
            Dispose();
            return true;
        }

        private bool CanvasDestroyed()
        {
            if (canvasObject == null || panel == null || heading == null || body == null ||
                linSuEchoButton == null || linSuEchoLabel == null)
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
            onDisclosureSubmitted = null;
            SoundwellUiLocalization.Changed -= Refresh;
            if (linSuEchoButton != null)
                linSuEchoButton.onClick.RemoveAllListeners();
            for (int i = 0; i < choiceButtons.Length; i++)
            {
                if (choiceButtons[i] != null)
                    choiceButtons[i].onClick.RemoveAllListeners();
            }
            SoundwellUiTheme.SafeDestroyCanvas(canvasObject);
        }
    }
}
