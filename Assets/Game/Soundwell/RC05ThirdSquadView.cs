using System;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Bottom-anchored safe-region UGUI panel for RC-05 ("山上的第三班").
    /// Steps through mountain-pass roll-call tracking, salt-grain boat delivery
    /// trial, abandoned-mill meeting (M00 stele callback & real Zheng Yuan
    /// roster+testimony check), and three-party decision with ZH/EN support.
    /// </summary>
    public sealed class RC05ThirdSquadView : IDisposable
    {
        private readonly GameObject canvasObject;
        private readonly RectTransform panel;
        private readonly Font latinFont;
        private readonly Text heading;
        private readonly Text body;
        private readonly Button[] choiceButtons = new Button[3];
        private readonly Text[] choiceLabels = new Text[3];
        private readonly RC05ThirdSquadSession session;
        private readonly Func<bool> boatmanCountMatched;
        private readonly Func<bool> squadRosterChecked;
        private readonly Func<bool> zhengYuanTestimonyConfirmed;
        private readonly Func<bool> skirmishTriggered;
        private Action<RC05Resolution> onResolutionSubmitted;
        private bool submitting;
        private bool disposed;

        public RC05ThirdSquadSession Session => session;
        public RectTransform Panel => panel;
        public Text Heading => heading;
        public Text Body => body;
        public Button ObserveRollCallButton => choiceButtons[0];
        public Button DeliverSaltGrainButton => choiceButtons[0];
        public Button MentionStoneButton => choiceButtons[0];
        public Button VerifyZhengYuanButton => choiceButtons[1];
        public Button ProceedTalksButton => choiceButtons[2];
        public Button CivilianPardonButton => choiceButtons[0];
        public Button EnlistLuJunButton => choiceButtons[1];
        public Button ContinueSmugglingButton => choiceButtons[2];

        public RC05ThirdSquadView(
            Transform parent,
            RC05ThirdSquadSession session,
            Func<bool> boatmanCountMatched,
            Func<bool> squadRosterChecked,
            Func<bool> zhengYuanTestimonyConfirmed,
            Func<bool> skirmishTriggered,
            Action<RC05Resolution> onResolutionSubmitted)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.boatmanCountMatched = boatmanCountMatched ??
                throw new ArgumentNullException(nameof(boatmanCountMatched));
            this.squadRosterChecked = squadRosterChecked ??
                throw new ArgumentNullException(nameof(squadRosterChecked));
            this.zhengYuanTestimonyConfirmed = zhengYuanTestimonyConfirmed ??
                throw new ArgumentNullException(nameof(zhengYuanTestimonyConfirmed));
            this.skirmishTriggered = skirmishTriggered ??
                throw new ArgumentNullException(nameof(skirmishTriggered));
            this.onResolutionSubmitted = onResolutionSubmitted ??
                throw new ArgumentNullException(nameof(onResolutionSubmitted));

            latinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            canvasObject = new GameObject("RC05 third squad canvas",
                typeof(RectTransform), typeof(Canvas));
            canvasObject.transform.SetParent(parent, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 148;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(
                SoundwellUiTheme.ReferenceWidth, SoundwellUiTheme.ReferenceHeight);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();

            var holder = new GameObject("RC05 third squad bottom panel", typeof(RectTransform));
            holder.transform.SetParent(canvasObject.transform, false);
            panel = (RectTransform)holder.transform;
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0f);
            panel.sizeDelta = new Vector2(904f, 256f);
            panel.anchoredPosition = new Vector2(0f, 138f);

            SoundwellUiTheme.CreatePanel(panel, "RC05 third squad border",
                new Vector2(904f, 256f), new Color(0.64f, 0.56f, 0.42f, 1f));
            SoundwellUiTheme.CreatePanel(panel, "RC05 third squad backing",
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
                    panel, "RC05 third squad choice " + i, positions[i],
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
                case RC05Stage.MountainPassTracking:
                    heading.text = zh ? "RC05 · 山道关口报数" : "RC05 · MOUNTAIN PASS ROLL CALL";
                    body.text = zh
                        ? "白苇与钟岭交界夜雾里，背盐人过关前逐一低声报数「一」到「十二」。伏身核对足迹与暗哨。"
                        : "In the mountain-pass fog, salt carriers count off from one to twelve before crossing. Observe their tracks.";
                    ShowChoice(0, zh ? "听清一至十二报数并跟踪" : "Observe 1-12 roll call & track");
                    FocusFirstInteractable();
                    break;

                case RC05Stage.SaltGrainBoatTrial:
                    heading.text = zh ? "RC05 · 山下交船试探" : "RC05 · SALT-GRAIN BOAT TRIAL";
                    body.text = zh
                        ? "废磨坊的人先托你把一批送往东岸的盐粮运到山下交船，须等接货船工点清数目才肯正式现身。"
                        : "They ask you to deliver a batch of salt and grain to the shore boat first; Hao Lie waits until the boatman confirms the count.";
                    ShowChoice(0, zh ? "交船并由船工点清数目" : "Deliver & verify boatman count");
                    FocusFirstInteractable();
                    break;

                case RC05Stage.MillMeeting:
                    heading.text = zh ? "RC05 · 半山废磨坊" : "RC05 · ABANDONED MILL MEETING";
                    string note = session.OldWomanStoneMentioned
                        ? (zh ? "郝烈攥紧麻袋口：「我娘。她每个月都去。」" : "Hao Lie grips the sack: \"My mother goes every month.\"")
                        : (zh ? "郝烈道出三个月前托船送进赤盐的三车救命盐粮。人群里有一位惯用左手的斥候。" : "Hao Lie reveals the three carts of grain sent to Red Salt. A left-handed scout sits among the twelve.");
                    if (session.RealZhengYuanVerified)
                        note += zh ? " 已核对班册与证词，确认真郑沅。" : " Roster & testimony confirmed: real Zheng Yuan.";
                    body.text = note;

                    if (session.OldWomanStoneTriggered)
                        ShowChoice(0, zh ? "提起碑前擦名的老妇人" : "Mention mother at memorial stele", !session.OldWomanStoneMentioned);
                    ShowChoice(1, zh ? "核对班册与证词辨认郑沅" : "Verify Zheng Yuan via roster", !session.RealZhengYuanVerified);
                    ShowChoice(2, zh ? "进入三方陈述与抉择" : "Proceed to three-party talks");
                    FocusFirstInteractable();
                    break;

                case RC05Stage.ThreePartyDecision:
                    heading.text = zh ? "RC05 · 第三班的出路" : "RC05 · THIRD SQUAD DECISION";
                    body.text = zh
                        ? "郝烈、韩峤税吏与陆钧招募人各自陈述完毕。请选择第三班的出路："
                        : "Hao Lie, Han Qiao's tax officer, and Lu Jun's recruiter have spoken. Choose the squad's path:";
                    ShowChoice(0, zh ? "民用赦免登记（停走私，靠M10供粮）" : "Civilian pardon (stop smuggling)", !submitting);
                    ShowChoice(1, zh ? "引荐陆钧（保留班制入伍）" : "Enlist under Lu Jun (keep squad)", !submitting);
                    ShowChoice(2, zh ? "遮掩继续走私（保粮线，损商路）" : "Continue smuggling (keep supply)", !submitting);
                    if (!submitting) FocusFirstInteractable();
                    break;

                case RC05Stage.Completed:
                    heading.text = zh ? "RC05 · 第三班案已落笔" : "RC05 · THIRD SQUAD RECORDED";
                    body.text = zh
                        ? "第三班处置结果已落盘保存。"
                        : "The Third Squad outcome has been recorded.";
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
                case RC05Stage.MountainPassTracking:
                    if (index == 0 && session.TryObserveMountainPassRollCall()) Refresh();
                    break;

                case RC05Stage.SaltGrainBoatTrial:
                    if (index == 0 && session.TryVerifySaltGrainBoatDelivery(boatmanCountMatched())) Refresh();
                    break;

                case RC05Stage.MillMeeting:
                    if (index == 0 && session.TryMentionOldWomanAtStele()) Refresh();
                    else if (index == 1 && session.TryVerifyRealZhengYuan(
                        squadRosterChecked(), zhengYuanTestimonyConfirmed())) Refresh();
                    else if (index == 2 && session.TryProceedToThreePartyTalks(skirmishTriggered())) Refresh();
                    break;

                case RC05Stage.ThreePartyDecision:
                    if (submitting) return;
                    var resolution = (RC05Resolution)(index + 1);
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
