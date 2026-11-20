using System;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Soundwell
{
    public enum ST06HomecomingViewStep
    {
        ResolveChair = 0,
        PostResolution = 1,
        Completed = 2
    }

    /// <summary>
    /// Bottom-anchored safe-region UGUI panel for ST-06 Tao Ji homecoming.
    /// Drives the broken-chair joint choice (RefundAndRebuild / TeachJoint /
    /// TaoJiExplains / CoverUp), surfaces explicit cover-up remediation only
    /// when CoverUp was chosen, records welcome dinner occurrence, and
    /// supports retry-safe Makinom settlement with ZH/EN localization.
    /// </summary>
    public sealed class ST06TaoJiHomecomingView : IDisposable
    {
        private readonly GameObject canvasObject;
        private readonly RectTransform panel;
        private readonly Font latinFont;
        private readonly Text heading;
        private readonly Text body;
        private readonly Button[] choiceButtons = new Button[4];
        private readonly Text[] choiceLabels = new Text[4];
        private readonly ST06TaoJiHomecomingSession session;
        private Action<ST06TaoJiHomecomingSession> onSettleRequested;
        private ST06HomecomingViewStep step;
        private bool settling;
        private bool disposed;

        public ST06TaoJiHomecomingSession Session => session;
        public ST06HomecomingViewStep Step => step;
        public RectTransform Panel => panel;
        public Text Heading => heading;
        public Text Body => body;
        public Button RefundRebuildButton => choiceButtons[0];
        public Button TeachJointButton => choiceButtons[1];
        public Button TaoJiExplainsButton => choiceButtons[2];
        public Button CoverUpButton => choiceButtons[3];
        public Button RemediateCoverUpButton => choiceButtons[0];
        public Button HoldWelcomeDinnerButton => choiceButtons[1];
        public Button SettleButton => choiceButtons[2];

        public ST06TaoJiHomecomingView(
            Transform parent,
            ST06TaoJiHomecomingSession session,
            Action<ST06TaoJiHomecomingSession> onSettleRequested)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.onSettleRequested = onSettleRequested ??
                throw new ArgumentNullException(nameof(onSettleRequested));

            if (session.IsSettled)
                step = ST06HomecomingViewStep.Completed;
            else if (session.ChairResolution.HasValue)
                step = ST06HomecomingViewStep.PostResolution;
            else
                step = ST06HomecomingViewStep.ResolveChair;

            latinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            canvasObject = new GameObject("ST06 taoji homecoming canvas",
                typeof(RectTransform), typeof(Canvas));
            canvasObject.transform.SetParent(parent, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 144;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(
                SoundwellUiTheme.ReferenceWidth, SoundwellUiTheme.ReferenceHeight);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();

            var holder = new GameObject("ST06 homecoming bottom panel", typeof(RectTransform));
            holder.transform.SetParent(canvasObject.transform, false);
            panel = (RectTransform)holder.transform;
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0f);
            panel.sizeDelta = new Vector2(904f, 256f);
            panel.anchoredPosition = new Vector2(0f, 138f);

            SoundwellUiTheme.CreatePanel(panel, "ST06 homecoming border",
                new Vector2(904f, 256f), new Color(0.64f, 0.56f, 0.42f, 1f));
            SoundwellUiTheme.CreatePanel(panel, "ST06 homecoming backing",
                new Vector2(900f, 252f), new Color(0.06f, 0.09f, 0.11f, 0.97f));
            Color ink = new Color(0.93f, 0.89f, 0.81f, 1f);

            heading = SoundwellUiTheme.CreateLabel(panel, 19,
                new Vector2(0f, 102f), new Vector2(842f, 28f), ink);
            heading.alignment = TextAnchor.MiddleLeft;

            body = SoundwellUiTheme.CreateLabel(panel, 15,
                new Vector2(0f, 40f), new Vector2(842f, 84f), ink);
            body.alignment = TextAnchor.UpperLeft;
            body.horizontalOverflow = HorizontalWrapMode.Wrap;

            var positions = new[]
            {
                new Vector2(-216f, -44f),
                new Vector2(216f, -44f),
                new Vector2(-216f, -92f),
                new Vector2(216f, -92f)
            };
            for (int i = 0; i < 4; i++)
            {
                int index = i;
                choiceButtons[i] = SoundwellUiTheme.CreateButton(
                    panel, "ST06 homecoming choice " + i, positions[i],
                    new Vector2(414f, 36f), 14, out choiceLabels[i]);
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

            switch (step)
            {
                case ST06HomecomingViewStep.ResolveChair:
                    heading.text = zh ? "桥西木铺 · 修错的椅子" : "QIAOXI WOODSHOP · BROKEN CHAIR JOINT";
                    body.text = zh
                        ? "陶霁：「十二年了，现在的椅子都变了。」座高接口修错了，该如何向老顾客和小满交代？"
                        : "Tao Ji: \"Twelve years on, even the chairs have changed.\" How should you handle the mis-fitted seat-height joint?";
                    ShowChoice(0, zh ? "先向顾客退款，再一起拆修" : "Refund customer and rebuild together");
                    ShowChoice(1, zh ? "请小满当面教新接口并向顾客说明" : "Have Xiaoman teach joint & explain");
                    ShowChoice(2, zh ? "让陶霁本人去向顾客说清楚" : "Let Tao Ji explain to customer herself");
                    ShowChoice(3, zh ? "先替她遮掩，只换新椅不解释" : "Cover up the mistake (swap chair only)");
                    SoundwellInputAdapter.KeepDialogFocus(canvasObject.transform, choiceButtons[0]);
                    break;

                case ST06HomecomingViewStep.PostResolution:
                    heading.text = zh ? "桥西镇 · 返乡收尾" : "QIAOXI TOWN · HOMECOMING WRAP-UP";
                    string trustNote = session.HasUnremediatedCoverUpTrustLoss
                        ? (zh
                            ? "提示：选择遮掩留下的信誉损失尚未弥补，挂新牌不会自动消除。"
                            : "Note: Unremediated cover-up trust loss remains until explicitly compensated.")
                        : (zh
                            ? "铺子信誉账已说清，不留未交代的遮掩旧账。"
                            : "Workshop trust is clear with no unremediated cover-up.");
                    string dinnerNote = session.WelcomeDinnerHeld
                        ? (zh ? "  ｜  已举办返乡欢迎饭" : "  |  Welcome dinner held")
                        : string.Empty;
                    body.text = trustNote + dinnerNote;

                    bool active = !settling;
                    if (session.HasUnremediatedCoverUpTrustLoss)
                        ShowChoice(0, zh ? "向两位顾客正式道歉并赔付弥补" : "Apologize & compensate both customers", active);
                    ShowChoice(1, zh ? "参加陶家返乡欢迎饭" : "Hold Tao family welcome dinner", active && !session.WelcomeDinnerHeld);
                    ShowChoice(2, zh ? "落盘记录 ST-06 返乡状态" : "Record ST-06 homecoming outcome", active);
                    if (active)
                        SoundwellInputAdapter.KeepDialogFocus(canvasObject.transform, choiceButtons[2]);
                    break;

                case ST06HomecomingViewStep.Completed:
                    heading.text = zh ? "桥西镇 · 陶霁返乡已记录" : "QIAOXI TOWN · HOMECOMING RECORDED";
                    body.text = zh
                        ? "ST-06 修椅处置与返乡状态已保存，不预设未寄达的近况信。"
                        : "ST-06 chair resolution and homecoming status recorded without fabricating undelivered letters.";
                    break;
            }
        }

        private void ShowChoice(int index, string text, bool interactable = true)
        {
            choiceLabels[index].text = text;
            choiceButtons[index].gameObject.SetActive(true);
            choiceButtons[index].interactable = interactable;
        }

        private void HandleChoiceClick(int index)
        {
            if (disposed || CanvasDestroyed()) return;

            if (step == ST06HomecomingViewStep.ResolveChair && index >= 0 && index <= 3)
            {
                session.ResolveBrokenChair((ST06ChairResolution)(index + 1));
                step = ST06HomecomingViewStep.PostResolution;
                Refresh();
                return;
            }

            if (step == ST06HomecomingViewStep.PostResolution && !settling)
            {
                if (index == 0 && session.TryRemediateCoverUp())
                {
                    Refresh();
                    return;
                }
                if (index == 1 && !session.WelcomeDinnerHeld)
                {
                    session.RecordWelcomeDinner();
                    Refresh();
                    return;
                }
                if (index == 2)
                {
                    settling = true;
                    Refresh();
                    try
                    {
                        onSettleRequested?.Invoke(session);
                        if (session.IsSettled)
                        {
                            step = ST06HomecomingViewStep.Completed;
                            onSettleRequested = null;
                        }
                    }
                    finally
                    {
                        settling = false;
                        if (!disposed) Refresh();
                    }
                }
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
            onSettleRequested = null;
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
