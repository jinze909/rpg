using System;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Soundwell
{
    /// <summary>
    /// The player reads the real M08 dawn notice and sees the five now
    /// investigable roster cases. This does not auto-complete a case or
    /// move a survivor; routes and travel remain owned by scene hosts.
    /// </summary>
    public sealed class M08FirstWorldNoticeView : IDisposable
    {
        private readonly GameObject canvasObject;
        private readonly RectTransform panel;
        private readonly Text heading;
        private readonly Text notice;
        private readonly Text caseList;
        private readonly Button acknowledgeButton;
        private readonly Text acknowledgeLabel;
        private readonly Font latinFont;
        private Func<bool> acknowledge;
        private bool acknowledging;
        private bool delivered;
        private bool disposed;

        public RectTransform Panel => panel;
        public Text Heading => heading;
        public Text Notice => notice;
        public Text CaseList => caseList;
        public Button AcknowledgeButton => acknowledgeButton;
        public bool IsDelivered => delivered;
        public bool IsDisposed => disposed || CanvasDestroyed();

        public M08FirstWorldNoticeView(Transform parent, Func<bool> acknowledge)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            this.acknowledge = acknowledge ?? throw new ArgumentNullException(nameof(acknowledge));
            latinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            canvasObject = new GameObject("M08 first world notice canvas",
                typeof(RectTransform), typeof(Canvas));
            canvasObject.transform.SetParent(parent, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 140;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(
                SoundwellUiTheme.ReferenceWidth, SoundwellUiTheme.ReferenceHeight);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();

            var holder = new GameObject("M08 chapter notice bottom panel", typeof(RectTransform));
            holder.transform.SetParent(canvasObject.transform, false);
            panel = (RectTransform)holder.transform;
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0f);
            panel.sizeDelta = new Vector2(904f, 270f);
            panel.anchoredPosition = new Vector2(0f, 146f);
            SoundwellUiTheme.CreatePanel(panel, "M08 notice border",
                new Vector2(904f, 270f), new Color(0.64f, 0.56f, 0.42f, 1f));
            SoundwellUiTheme.CreatePanel(panel, "M08 notice backing",
                new Vector2(900f, 266f), new Color(0.06f, 0.09f, 0.11f, 0.97f));
            var ink = new Color(0.93f, 0.89f, 0.81f, 1f);

            heading = SoundwellUiTheme.CreateLabel(panel, 19,
                new Vector2(0f, 109f), new Vector2(838f, 28f), ink);
            heading.alignment = TextAnchor.MiddleLeft;
            notice = SoundwellUiTheme.CreateLabel(panel, 16,
                new Vector2(0f, 64f), new Vector2(838f, 58f), ink);
            notice.alignment = TextAnchor.UpperLeft;
            notice.horizontalOverflow = HorizontalWrapMode.Wrap;
            caseList = SoundwellUiTheme.CreateLabel(panel, 14,
                new Vector2(0f, -12f), new Vector2(838f, 105f), ink);
            caseList.alignment = TextAnchor.UpperLeft;
            caseList.horizontalOverflow = HorizontalWrapMode.Wrap;
            acknowledgeButton = SoundwellUiTheme.CreateButton(panel,
                "M08 read toll notice", new Vector2(0f, -110f),
                new Vector2(474f, 38f), 14, out acknowledgeLabel);
            acknowledgeButton.onClick.AddListener(TryAcknowledge);
            LayoutForViewportAspect((float)Mathf.Max(1, Screen.width) /
                Mathf.Max(1, Screen.height));
            SoundwellUiLocalization.Changed += Refresh;
            Refresh();
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
            heading.font = notice.font = caseList.font =
                acknowledgeLabel.font = font;

            heading.text = zh ? "M08 · 第一则预兆／归途名册"
                              : "M08 · FIRST NOTICE / THE RETURN ROSTER";
            notice.text = zh
                ? "第二天早晨，港口贴出告示：回澜关加收通行税。祁雀的货价随之上涨。这是已见到的第一张告示；后续借船与征召均尚未发生。"
                : "At dawn a new notice appears: Huilan Pass raises its transit toll, increasing Qi Que's cargo prices. This is only the first notice; no requisition or conscription has yet occurred.";
            caseList.text = zh
                ? "已核实营救名册中，五行仍待查：\nRC-01 陈砚（炊事兵）   RC-02 郑沅（斥候）\nRC-03 白矶矿（二十七人）   RC-04 丁岳（渡引）\nRC-05 第三班（郝烈等十二人）\n可自由选择调查顺序；开放≠获救、上船、和解或完成。"
                : "Five unresolved roster lines are now available:\nRC-01 Chen Yan (cook)   RC-02 Zheng Yuan (scout)\nRC-03 Baiji Mine (27 people)   RC-04 Ding Yue (pilot)\nRC-05 Third Squad (Hao Lie and eleven others)\nChoose any investigation order; available does not mean rescued, boarded or resolved.";
            acknowledgeLabel.text = zh ? "读完告示 · 开放调查"
                                       : "Read notice · Allow investigation";
            bool canAcknowledge = !delivered && !acknowledging;
            acknowledgeButton.gameObject.SetActive(!delivered);
            acknowledgeButton.interactable = canAcknowledge;
            if (canAcknowledge)
                SoundwellInputAdapter.KeepDialogFocus(
                    canvasObject.transform, acknowledgeButton);
        }

        private void TryAcknowledge()
        {
            if (disposed || delivered || acknowledging || IsDestroyedExternally() ||
                !acknowledgeButton.interactable)
                return;

            // Guard against reentrant submits while keeping declined or
            // interrupted authoritative writes retryable.
            acknowledging = true;
            Refresh();
            var callback = acknowledge;
            try
            {
                if (callback != null && callback())
                {
                    delivered = true;
                    acknowledge = null;
                }
            }
            finally
            {
                acknowledging = false;
                if (!disposed) Refresh();
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
            return canvasObject == null || panel == null ||
                heading == null || notice == null || caseList == null ||
                acknowledgeButton == null || acknowledgeLabel == null;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            acknowledge = null;
            SoundwellUiLocalization.Changed -= Refresh;
            if (acknowledgeButton != null)
                acknowledgeButton.onClick.RemoveAllListeners();
            SoundwellUiTheme.SafeDestroyCanvas(canvasObject);
        }
    }
}
