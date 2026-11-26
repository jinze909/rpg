using System;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Playable RC04 ("盐雾里的应声") three-round echo discrimination at
    /// Ding Yue's reef. This does not declare a boat, a safe landing,
    /// the beast's defeat, or Ding Yue's return; the physical scene host
    /// alone decides when the actual tidal-window approach is available.
    /// </summary>
    public sealed class RC04ReefListeningView : IDisposable
    {
        private readonly RC04DingYueReefSession session;
        private readonly GameObject canvasObject;
        private readonly RectTransform panel;
        private readonly Text heading;
        private readonly Text body;
        private readonly Button slowEchoButton;
        private readonly Text slowEchoLabel;
        private readonly Button fastEchoButton;
        private readonly Text fastEchoLabel;
        private readonly Button approachButton;
        private readonly Text approachLabel;
        private readonly Font latinFont;
        private Func<RC04DingYueReefSession, bool> onApproachRequested;
        private bool lastPickedMimic;
        private bool requesting;
        private bool delivered;
        private bool disposed;

        public RC04DingYueReefSession Session => session;
        public RectTransform Panel => panel;
        public Text Heading => heading;
        public Text Body => body;
        public Button SlowEchoButton => slowEchoButton;
        public Button FastEchoButton => fastEchoButton;
        public Button ApproachButton => approachButton;
        public bool ApproachDelivered => delivered;
        public bool IsDisposed => disposed || CanvasDestroyed();

        public RC04ReefListeningView(Transform parent,
            RC04DingYueReefSession session,
            Func<RC04DingYueReefSession, bool> onApproachRequested)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.onApproachRequested = onApproachRequested ??
                throw new ArgumentNullException(nameof(onApproachRequested));

            latinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            canvasObject = new GameObject("RC04 reef listening canvas",
                typeof(RectTransform), typeof(Canvas));
            canvasObject.transform.SetParent(parent, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 139;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(
                SoundwellUiTheme.ReferenceWidth, SoundwellUiTheme.ReferenceHeight);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();

            var holder = new GameObject("RC04 listening bottom panel",
                typeof(RectTransform));
            holder.transform.SetParent(canvasObject.transform, false);
            panel = (RectTransform)holder.transform;
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0f);
            panel.sizeDelta = new Vector2(904f, 248f);
            panel.anchoredPosition = new Vector2(0f, 134f);
            SoundwellUiTheme.CreatePanel(panel, "RC04 listening border",
                new Vector2(904f, 248f), new Color(0.64f, 0.56f, 0.42f, 1f));
            SoundwellUiTheme.CreatePanel(panel, "RC04 listening backing",
                new Vector2(900f, 244f), new Color(0.06f, 0.09f, 0.11f, 0.97f));
            var ink = new Color(0.93f, 0.89f, 0.81f, 1f);
            heading = SoundwellUiTheme.CreateLabel(panel, 19,
                new Vector2(0f, 97f), new Vector2(842f, 28f), ink);
            heading.alignment = TextAnchor.MiddleLeft;
            body = SoundwellUiTheme.CreateLabel(panel, 15,
                new Vector2(0f, 32f), new Vector2(842f, 91f), ink);
            body.alignment = TextAnchor.UpperLeft;
            body.horizontalOverflow = HorizontalWrapMode.Wrap;

            slowEchoButton = SoundwellUiTheme.CreateButton(panel,
                "RC04 slow human echo", new Vector2(-226f, -83f),
                new Vector2(435f, 38f), 14, out slowEchoLabel);
            fastEchoButton = SoundwellUiTheme.CreateButton(panel,
                "RC04 fast mimic echo", new Vector2(226f, -83f),
                new Vector2(435f, 38f), 14, out fastEchoLabel);
            approachButton = SoundwellUiTheme.CreateButton(panel,
                "RC04 request actual reef approach", new Vector2(0f, -83f),
                new Vector2(492f, 38f), 14, out approachLabel);

            slowEchoButton.onClick.AddListener(() => Choose(RC04EchoChoice.SlowHumanEcho));
            fastEchoButton.onClick.AddListener(() => Choose(RC04EchoChoice.FastMimicReply));
            approachButton.onClick.AddListener(RequestApproach);
            LayoutForViewportAspect((float)Mathf.Max(1, Screen.width) /
                Mathf.Max(1, Screen.height));
            SoundwellUiLocalization.Changed += Refresh;
            Refresh();
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
            heading.font = body.font = slowEchoLabel.font =
                fastEchoLabel.font = approachLabel.font = font;
            slowEchoButton.gameObject.SetActive(false);
            fastEchoButton.gameObject.SetActive(false);
            approachButton.gameObject.SetActive(false);
            slowEchoButton.interactable = false;
            fastEchoButton.interactable = false;
            approachButton.interactable = false;
            heading.text = zh ? "RC04 · 盐雾里的应声" : "RC04 · REPLIES IN THE SALT FOG";

            if (session.IsResolved)
            {
                body.text = zh ? "本案已有经核验的处置结果，不能重新靠岸或重写选择。"
                               : "This case already has a verified resolution. No second landing or choice is available.";
                return;
            }
            if (delivered)
            {
                body.text = zh ? "三次听雾完成，真实潮窗已交给实际航行场景处理。"
                               : "All three echoes verified. The actual scene now handles the tidal-window approach.";
                return;
            }
            if (session.TidalWindowLocated)
            {
                body.text = zh
                    ? "三轮声音已分清：过快的同声是借声兽，慢半拍的咳嗽来自活人。你已识别可接近的短潮窗，但仍须真船与安全返航安排，不能凭此认定丁岳已获救。"
                    : "Three rounds reveal the rushed voice-copying beast and the delayed human cough. You found a short approach window; real passage and safe return still need confirmation. Ding Yue is not yet rescued.";
                approachLabel.text = zh ? "申请实际靠礁（核验船与返航潮窗）"
                                        : "Request real approach (check boat and safe return)";
                approachButton.gameObject.SetActive(true);
                approachButton.interactable = !requesting;
                if (!requesting)
                    SoundwellInputAdapter.KeepDialogFocus(
                        canvasObject.transform, approachButton);
                return;
            }

            int round = session.CompletedListeningRounds + 1;
            string prompt;
            switch (round)
            {
                case 1:
                    prompt = zh
                        ? "第一轮：雾里立刻回喊了与你一模一样的声音。这样的抢答会骗船转舵。要锁定真人方向，该相信哪道回声？"
                        : "Round 1: The fog immediately copies your shout. Such an eager reply can lure boats onto rocks. Which echo should guide you toward a human?";
                    break;
                case 2:
                    prompt = zh
                        ? "第二轮：相隔两拍半，礁石深处才传来苍老的咳嗽与慢回音。真人的方向是哪边？"
                        : "Round 2: After two and a half beats, an old cough comes from the reef. Which delayed echo belongs to the living voice?";
                    break;
                default:
                    prompt = zh
                        ? "第三轮：船上两人互叫家中小名，雾中借声兽连口气都照搬。找出不跟着抢答的真人声，别把模仿当应声。"
                        : "Round 3: Two sailors call each other by family names. The mimic repeats every breath. Find the slow human reply, not the stolen imitation.";
                    break;
            }
            string correction = lastPickedMimic
                ? (zh ? "那道过快的复制声是兽声；它不能增加识别轮次。\n"
                      : "The instant imitation is the beast; it does not count toward the three real listening rounds.\n")
                : "";
            body.text = correction + prompt;
            slowEchoLabel.text = zh ? "辨认真声：迟到的咳嗽 / 回音"
                                    : "Choose delayed human cough / echo";
            fastEchoLabel.text = zh ? "跟随立刻复制的呼喊（危险）"
                                    : "Follow immediate copied shout (danger)";
            slowEchoButton.gameObject.SetActive(true);
            fastEchoButton.gameObject.SetActive(true);
            slowEchoButton.interactable = true;
            fastEchoButton.interactable = true;
            SoundwellInputAdapter.KeepDialogFocus(
                canvasObject.transform, slowEchoButton);
        }

        private void Choose(RC04EchoChoice choice)
        {
            if (disposed || IsDestroyedExternally() || delivered || requesting ||
                session.IsResolved || session.TidalWindowLocated ||
                !slowEchoButton.IsActive() || !slowEchoButton.IsInteractable() ||
                !fastEchoButton.IsActive() || !fastEchoButton.IsInteractable())
                return;
            lastPickedMimic = choice == RC04EchoChoice.FastMimicReply;
            session.SubmitEchoDiscrimination(choice);
            Refresh();
        }

        private void RequestApproach()
        {
            if (disposed || IsDestroyedExternally() || delivered || requesting ||
                session.IsResolved || !session.TidalWindowLocated ||
                !approachButton.IsActive() || !approachButton.IsInteractable())
                return;
            requesting = true;
            approachButton.interactable = false;
            try
            {
                // A declined or interrupted scene callback keeps the approach
                // button available for a later genuine short tide window.
                if (!onApproachRequested(session))
                    return;
                delivered = true;
                onApproachRequested = null;
            }
            finally
            {
                requesting = false;
                if (!disposed) Refresh();
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
            return canvasObject == null || panel == null ||
                heading == null || body == null ||
                slowEchoButton == null || slowEchoLabel == null ||
                fastEchoButton == null || fastEchoLabel == null ||
                approachButton == null || approachLabel == null;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            onApproachRequested = null;
            SoundwellUiLocalization.Changed -= Refresh;
            if (slowEchoButton != null) slowEchoButton.onClick.RemoveAllListeners();
            if (fastEchoButton != null) fastEchoButton.onClick.RemoveAllListeners();
            if (approachButton != null) approachButton.onClick.RemoveAllListeners();
            SoundwellUiTheme.SafeDestroyCanvas(canvasObject);
        }
    }
}