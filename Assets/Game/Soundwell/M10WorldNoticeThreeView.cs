using System;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Soundwell
{
    /// <summary>
    /// The player reads M10's third omen at Linchao Ferry. Acknowledging the
    /// notice records only that it was seen; it does not complete the Jingwan
    /// assembly or advance any roster, contract, or rescue outcome.
    /// </summary>
    public sealed class M10WorldNoticeThreeView : IDisposable
    {
        private readonly GameObject canvasObject;
        private readonly RectTransform panel;
        private readonly Text heading;
        private readonly Text notice;
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
        public Button AcknowledgeButton => acknowledgeButton;
        public bool IsDelivered => delivered;
        public bool IsDisposed => disposed || CanvasDestroyed();

        public M10WorldNoticeThreeView(Transform parent, Func<bool> acknowledge)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            this.acknowledge = acknowledge ??
                throw new ArgumentNullException(nameof(acknowledge));
            latinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            canvasObject = new GameObject("M10 third world notice canvas",
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

            var holder = new GameObject("M10 third omen bottom panel", typeof(RectTransform));
            holder.transform.SetParent(canvasObject.transform, false);
            panel = (RectTransform)holder.transform;
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0f);
            panel.sizeDelta = new Vector2(904f, 230f);
            panel.anchoredPosition = new Vector2(0f, 126f);
            SoundwellUiTheme.CreatePanel(panel, "M10 notice border",
                new Vector2(904f, 230f), new Color(0.64f, 0.56f, 0.42f, 1f));
            SoundwellUiTheme.CreatePanel(panel, "M10 notice backing",
                new Vector2(900f, 226f), new Color(0.06f, 0.09f, 0.11f, 0.97f));
            var ink = new Color(0.93f, 0.89f, 0.81f, 1f);

            heading = SoundwellUiTheme.CreateLabel(panel, 20,
                new Vector2(0f, 84f), new Vector2(838f, 28f), ink);
            heading.alignment = TextAnchor.MiddleLeft;
            notice = SoundwellUiTheme.CreateLabel(panel, 18,
                new Vector2(0f, 6f), new Vector2(838f, 118f), ink);
            notice.alignment = TextAnchor.UpperLeft;
            notice.horizontalOverflow = HorizontalWrapMode.Wrap;
            // Keep the notice legible rather than shrinking a dense paragraph
            // into one tiny line in the actual 960x540 story GameView.
            notice.resizeTextForBestFit = false;
            acknowledgeButton = SoundwellUiTheme.CreateButton(panel,
                "M10 read third omen", new Vector2(0f, -82f),
                new Vector2(474f, 42f), 16, out acknowledgeLabel);
            acknowledgeButton.onClick.AddListener(TryAcknowledge);

            LayoutForViewportAspect((float)Mathf.Max(1, Screen.width) /
                Mathf.Max(1, Screen.height));
            SoundwellUiLocalization.Changed += Refresh;
            Refresh();
        }

        public void LayoutForViewportAspect(float aspect)
        {
            if (disposed || IsDestroyedExternally())
                return;
            if (float.IsNaN(aspect) || float.IsInfinity(aspect) || aspect <= 0f)
                return;
            SoundwellUiTheme.ApplyViewportAspectScale(panel, aspect);
        }

        public void Refresh()
        {
            if (disposed || IsDestroyedExternally())
                return;

            bool zh = SoundwellUiLocalization.Language == SoundwellUiLocalization.Chinese;
            Font font = SoundwellUiLocalization.PresentationFont(latinFont);
            heading.font = notice.font = acknowledgeLabel.font = font;
            heading.text = zh
                ? "M10 · 第三则预兆／临潮渡口"
                : "M10 · THIRD OMEN / LINCHAO FERRY";
            notice.text = zh
                ? "临潮渡口贴出第三则预兆告示。\n纸上只有一句：“桥在我在。”落款：陆钧。\n有人念了一遍，又把纸角压平。\n十二年前，你在这座桥上喊的是借绳子。"
                : "A third omen is posted at Linchao Ferry.\nThe notice carries one line: “I am the bridge; I am here.” Signed Lu Jun.\nSomeone reads it aloud and presses the paper flat.\nTwelve years ago, what you called across this bridge was a request for rope.";
            acknowledgeLabel.text = zh
                ? "记下告示 · 前往镜湾"
                : "Record notice · Continue to Mirror Bay";
            acknowledgeButton.gameObject.SetActive(!delivered);
            acknowledgeButton.interactable = !delivered && !acknowledging;
            if (!delivered && !acknowledging)
                SoundwellInputAdapter.KeepDialogFocus(
                    canvasObject.transform, acknowledgeButton);
        }

        private void TryAcknowledge()
        {
            if (disposed || delivered || acknowledging || IsDestroyedExternally() ||
                !acknowledgeButton.interactable)
                return;

            acknowledging = true;
            Refresh();
            var callback = acknowledge;
            try
            {
                if (callback != null && callback())
                {
                    delivered = true;
                    acknowledge = null;
                    Dispose();
                }
            }
            finally
            {
                acknowledging = false;
                if (!disposed)
                    Refresh();
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
                heading == null || notice == null ||
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