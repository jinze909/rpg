using System;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Bottom-anchored safe-region UGUI panel for ST-02 ("河面上两盏头灯").
    /// Drives inspecting both Xie Zhi's old sample boat and Wen Sheng's new boat,
    /// optionally relaying their original words, gathering dock feedback, choosing
    /// trial-result disclosure and bidding mode, and resolving the old-lock rescue
    /// with ZH/EN localization.
    /// </summary>
    public sealed class ST02RiverLanternsView : IDisposable
    {
        private readonly GameObject canvasObject;
        private readonly RectTransform panel;
        private readonly Font latinFont;
        private readonly Text heading;
        private readonly Text body;
        private readonly Button[] choiceButtons = new Button[3];
        private readonly Text[] choiceLabels = new Text[3];
        private readonly ST02RiverLanternsSession session;
        private Action<ST02LockRescueMethod> onRescueSubmitted;
        private bool submitting;
        private bool disposed;

        public ST02RiverLanternsSession Session => session;
        public RectTransform Panel => panel;
        public Text Heading => heading;
        public Text Body => body;
        public Button InspectXieZhiBoatButton => choiceButtons[0];
        public Button InspectWenShengBoatButton => choiceButtons[1];
        public Button RelayOriginalWordsButton => choiceButtons[0];
        public Button GatherDockFeedbackButton => choiceButtons[1];
        public Button PublishToBothButton => choiceButtons[0];
        public Button ReturnPrivatelyButton => choiceButtons[1];
        public Button IndependentBidButton => choiceButtons[0];
        public Button JointBidButton => choiceButtons[1];
        public Button BraceHullStressButton => choiceButtons[0];
        public Button DismantleLanternButton => choiceButtons[1];
        public bool IsDisposed => disposed || CanvasDestroyed();

        public ST02RiverLanternsView(
            Transform parent,
            ST02RiverLanternsSession session,
            Action<ST02LockRescueMethod> onRescueSubmitted)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.onRescueSubmitted = onRescueSubmitted ??
                throw new ArgumentNullException(nameof(onRescueSubmitted));

            latinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            canvasObject = new GameObject("ST02 river lanterns canvas",
                typeof(RectTransform), typeof(Canvas));
            canvasObject.transform.SetParent(parent, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 158;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(
                SoundwellUiTheme.ReferenceWidth, SoundwellUiTheme.ReferenceHeight);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();

            var holder = new GameObject("ST02 river lanterns bottom panel", typeof(RectTransform));
            holder.transform.SetParent(canvasObject.transform, false);
            panel = (RectTransform)holder.transform;
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0f);
            panel.sizeDelta = new Vector2(904f, 256f);
            panel.anchoredPosition = new Vector2(0f, 138f);

            SoundwellUiTheme.CreatePanel(panel, "ST02 river lanterns border",
                new Vector2(904f, 256f), new Color(0.68f, 0.58f, 0.36f, 1f));
            SoundwellUiTheme.CreatePanel(panel, "ST02 river lanterns backing",
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
                    panel, "ST02 river lanterns choice " + i, positions[i],
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

            switch (session.Stage)
            {
                case ST02Stage.WorkshopInspection:
                    heading.text = zh ? "ST02 · 同一批灯骨与两只船" : "ST02 · TWO WORKSHOPS & ONE BATCH";
                    body.text = zh
                        ? "河灯节前夜，谢枝与闻笙同时雇你买断同一批灯骨。先分别查看谢枝的旧样舟（留着画歪的第一只鹭鸟）与闻笙的新舟（船底暗榫）。"
                        : "On river-lantern eve, Xie Zhi and Wen Sheng both bid for the same frames. Inspect both boats before the dock trial.";
                    ShowChoice(0, zh ? "查看谢枝的旧样舟" : "Inspect Xie Zhi's old boat",
                        (session.Inspections & ST02InspectionFlags.XieZhiOldBoat) == 0);
                    ShowChoice(1, zh ? "查看闻笙的新灯舟" : "Inspect Wen Sheng's new boat",
                        (session.Inspections & ST02InspectionFlags.WenShengNewBoat) == 0);
                    FocusFirstInteractable();
                    break;

                case ST02Stage.DockTrialAndFeedback:
                    heading.text = zh ? "ST02 · 芦汀渡口试航与征询" : "ST02 · DOCK TRIAL & FEEDBACK";
                    string relayNote = session.OriginalWordsRelayed
                        ? (zh ? " 你已把两人的原话各自带到对方耳边。" : " You relayed each artisan's original words.")
                        : string.Empty;
                    body.text = (zh
                        ? "两条舟下水试航：谢枝的舟稳但船帮高，闻笙的舟灵巧却载重易倾。陪他们去问掌灯人、老人和装卸工的意见。"
                        : "Both boats take the water: Xie Zhi's sits steady with high gunwales; Wen Sheng's turns sharply but lists under full lamps.") + relayNote;
                    ShowChoice(0, zh ? "把各自原话带回给对方" : "Relay original words to each",
                        !session.OriginalWordsRelayed);
                    ShowChoice(1, zh ? "向掌灯人、老人与装卸工征询" : "Gather dock voices & feedback");
                    FocusFirstInteractable();
                    break;

                case ST02Stage.TrialDisclosureChoice:
                    heading.text = zh ? "ST02 · 试航结果怎么处理" : "ST02 · TRIAL RESULT DISCLOSURE";
                    body.text = zh
                        ? "试航短板已经摸清。决定如何向两位雇主交代这次试航结果："
                        : "The trial trade-offs are clear. Decide how to share the trial results:";
                    ShowChoice(0, zh ? "公开结果，让两人对照改进" : "Publish to both to improve");
                    ShowChoice(1, zh ? "各交各家，由雇主自行决定公开" : "Return privately to each");
                    FocusFirstInteractable();
                    break;

                case ST02Stage.BiddingDecision:
                    heading.text = zh ? "ST02 · 独立分包或联名投标" : "ST02 · BIDDING ARRANGEMENT";
                    body.text = zh
                        ? "镇公所只给一份主合同，允许列明分包。协助确认两人本次的投标方式："
                        : "The town office grants one main contract with named subcontracts. Confirm their bidding path:";
                    ShowChoice(0, zh ? "一人独立争冠、另一人拿分包" : "Independent bid + subcontract");
                    ShowChoice(1, zh ? "联合投标，写清收益与维修责任" : "Joint bid with written terms");
                    FocusFirstInteractable();
                    break;

                case ST02Stage.OldLockRescue:
                    heading.text = zh ? "ST02 · 旧闸解围与落盘" : "ST02 · OLD LOCK RESCUE";
                    body.text = zh
                        ? "河灯节当夜旧水闸卡船，谢枝抛缆拉船、闻笙爬架拆机。选择你的旧闸解围方式并落盘保存："
                        : "At the narrow old lock, both artisans leap to help each other. Choose how to free the boats and save:";
                    ShowChoice(0, zh ? "按试航受力点精准撑开（保舟完好）" : "Brace hull stress point (intact)", !submitting);
                    ShowChoice(1, zh ? "直接卸掉灯架拖舟靠岸（损伤较大）" : "Dismantle lantern frame (damaged)", !submitting);
                    if (!submitting) FocusFirstInteractable();
                    break;

                case ST02Stage.Completed:
                    heading.text = zh ? "ST02 · 两盏头灯已落盘" : "ST02 · TWO RIVER LANTERNS SAVED";
                    body.text = zh
                        ? "芦汀河灯节结果已写入记录。"
                        : "The Two River Lanterns outcome has been saved.";
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

            switch (session.Stage)
            {
                case ST02Stage.WorkshopInspection:
                    var boat = index == 0
                        ? ST02InspectionFlags.XieZhiOldBoat
                        : ST02InspectionFlags.WenShengNewBoat;
                    if (session.TryInspectBoat(boat)) Refresh();
                    break;

                case ST02Stage.DockTrialAndFeedback:
                    if (index == 0 && session.TryRelayOriginalWords()) Refresh();
                    else if (index == 1 && session.TryGatherDockFeedback()) Refresh();
                    break;

                case ST02Stage.TrialDisclosureChoice:
                    var disclosure = index == 0
                        ? ST02TrialDisclosure.PublishToBoth
                        : ST02TrialDisclosure.ReturnPrivatelyToEach;
                    if (session.TryChooseTrialDisclosure(disclosure)) Refresh();
                    break;

                case ST02Stage.BiddingDecision:
                    var bid = index == 0
                        ? ST02BidMode.IndependentWithSubcontract
                        : ST02BidMode.JointBidWithWrittenTerms;
                    if (session.TryChooseBidMode(bid)) Refresh();
                    break;

                case ST02Stage.OldLockRescue:
                    if (submitting) return;
                    var rescue = index == 0
                        ? ST02LockRescueMethod.BraceHullStressPoint
                        : ST02LockRescueMethod.DismantleLanternFrame;
                    submitting = true;
                    Refresh();
                    try
                    {
                        onRescueSubmitted?.Invoke(rescue);
                        if (session.IsCompleted)
                            onRescueSubmitted = null;
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
            onRescueSubmitted = null;
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