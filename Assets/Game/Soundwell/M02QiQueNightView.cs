using System;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Scene-owned bilingual UGUI for M02's optional Qi Que night conversation.
    /// The host supplies the debt discovery already stored by Makinom.
    /// </summary>
    public sealed class M02QiQueNightView : IDisposable
    {
        private readonly M02QiQueNight night;
        private readonly GameObject canvasObject;
        private readonly RectTransform authoredView;
        private readonly Font latinFont;
        private readonly Text heading;
        private readonly Text dialogue;
        private readonly Button progressButton;
        private readonly Text progressLabel;
        private Action onFinished;
        private bool delivered;
        private bool disposed;

        public M02QiQueNight Night => night;
        public Text Dialogue => dialogue;
        public Button ProgressButton => progressButton;

        public M02QiQueNightView(Transform parent, bool debtAlreadyKnown, Action onFinished)
        {
            if (parent == null)
                throw new ArgumentNullException(nameof(parent));
            if (onFinished == null)
                throw new ArgumentNullException(nameof(onFinished));

            night = new M02QiQueNight(debtAlreadyKnown);
            night.Begin();
            this.onFinished = onFinished;
            latinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            canvasObject = SoundwellUiTheme.CreateOverlayCanvas(
                parent, "M02 Qi Que warehouse canvas", 125, out authoredView,
                new Color(0.04f, 0.06f, 0.08f, 0.98f));
            heading = SoundwellUiTheme.CreateLabel(authoredView, 25,
                new Vector2(0f, 211f), new Vector2(835f, 50f));
            dialogue = SoundwellUiTheme.CreateLabel(authoredView, 21,
                new Vector2(0f, 39f), new Vector2(825f, 265f));
            dialogue.alignment = TextAnchor.MiddleLeft;
            progressButton = SoundwellUiTheme.CreateButton(authoredView,
                "M02 Qi Que continue", new Vector2(0f, -213f),
                new Vector2(540f, 55f), 18, out progressLabel,
                new Color(0.81f, 0.82f, 0.76f));
            progressButton.onClick.AddListener(Advance);

            LayoutForViewportAspect((float)Mathf.Max(1, Screen.width) /
                Mathf.Max(1, Screen.height));
            Refresh();
            SoundwellUiLocalization.Changed += Refresh;
        }

        public void LayoutForViewportAspect(float aspect)
        {
            if (disposed)
                return;
            SoundwellUiTheme.ApplyViewportAspectScale(authoredView, aspect);
        }

        public void Refresh()
        {
            if (disposed)
                return;
            if (canvasObject == null || heading == null)
            {
                Dispose();
                return;
            }

            bool chinese = SoundwellUiLocalization.Language == SoundwellUiLocalization.Chinese;
            Font font = SoundwellUiLocalization.PresentationFont(latinFont);
            heading.font = dialogue.font = progressLabel.font = font;
            heading.text = chinese ? "码头货栈 · 祁雀的实话" :
                "DOCK WAREHOUSE · QI QUE'S PROMISE";
            dialogue.text = M02QiQueNightDialogue.Describe(night, chinese);
            switch (night.Stage)
            {
                case M02QiQueNightStage.AtWarehouse:
                    progressLabel.text = chinese ? "走近祁雀" : "Approach Qi Que";
                    break;
                case M02QiQueNightStage.ConfrontingDebt:
                    progressLabel.text = chinese ? "听她说完" : "Hear her out";
                    break;
                case M02QiQueNightStage.OfferingCake:
                    progressLabel.text = chinese ? "接下桂花糕，看海图" : "Take the cake and see the chart";
                    break;
                case M02QiQueNightStage.ReviewingChart:
                    progressLabel.text = chinese ? "记下提议，去见陆钧" : "Note her offer and visit Lu Jun";
                    break;
                default:
                    progressLabel.text = string.Empty;
                    break;
            }
            progressButton.gameObject.SetActive(!delivered &&
                night.Stage != M02QiQueNightStage.Finished);
            if (!delivered)
                SoundwellInputAdapter.KeepDialogFocus(canvasObject.transform,
                    progressButton);
        }

        private void Advance()
        {
            if (disposed || delivered)
                return;
            switch (night.Stage)
            {
                case M02QiQueNightStage.AtWarehouse:
                    night.Greet();
                    break;
                case M02QiQueNightStage.ConfrontingDebt:
                    night.DiscussDebt();
                    break;
                case M02QiQueNightStage.OfferingCake:
                    night.AcceptCake();
                    break;
                case M02QiQueNightStage.ReviewingChart:
                    night.Leave();
                    delivered = true;
                    Refresh();
                    Action callback = onFinished;
                    onFinished = null;
                    callback?.Invoke();
                    return;
                default:
                    return;
            }
            Refresh();
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            onFinished = null;
            SoundwellUiLocalization.Changed -= Refresh;
            if (progressButton != null)
                progressButton.onClick.RemoveAllListeners();
            SoundwellUiTheme.SafeDestroyCanvas(canvasObject);
        }
    }
}
