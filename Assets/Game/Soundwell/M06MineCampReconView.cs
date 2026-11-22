using System;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Compact bottom-anchored UGUI panel displaying one inspected M06 mine-camp
    /// reconnaissance lead (押运、看守、物资账、矿道与主泵) and invoking the host
    /// save callback with clean retry on interrupted Makinom writes.
    /// </summary>
    public sealed class M06MineCampReconView : IDisposable
    {
        private readonly GameObject canvasObject;
        private readonly RectTransform panel;
        private readonly Font latinFont;
        private readonly Text heading;
        private readonly Text summary;
        private readonly Text confirmLabel;
        private readonly Button confirmButton;
        private readonly M06MineCampLead lead;
        private Action<M06MineCampLead> onRecorded;
        private bool recorded;
        private bool disposed;

        public M06MineCampLead Lead => lead;
        public Text Heading => heading;
        public Text Summary => summary;
        public Button ConfirmButton => confirmButton;
        public RectTransform Panel => panel;
        public bool IsDisposed
        {
            get
            {
                if (!disposed && CanvasDestroyed()) Dispose();
                return disposed;
            }
        }

        public M06MineCampReconView(
            Transform parent,
            M06MineCampLead lead,
            Action<M06MineCampLead> onRecorded)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (!Enum.IsDefined(typeof(M06MineCampLead), lead))
                throw new ArgumentOutOfRangeException(nameof(lead));
            this.onRecorded = onRecorded ?? throw new ArgumentNullException(nameof(onRecorded));
            this.lead = lead;

            latinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            canvasObject = new GameObject("M06 mine camp recon canvas",
                typeof(RectTransform), typeof(Canvas));
            canvasObject.transform.SetParent(parent, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 132;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(
                SoundwellUiTheme.ReferenceWidth, SoundwellUiTheme.ReferenceHeight);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();

            var holder = new GameObject("M06 recon bottom panel", typeof(RectTransform));
            holder.transform.SetParent(canvasObject.transform, false);
            panel = (RectTransform)holder.transform;
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0f);
            panel.sizeDelta = new Vector2(904f, 206f);
            panel.anchoredPosition = new Vector2(0f, 114f);

            SoundwellUiTheme.CreatePanel(panel, "M06 recon border",
                new Vector2(904f, 206f), new Color(0.64f, 0.56f, 0.42f, 1f));
            SoundwellUiTheme.CreatePanel(panel, "M06 recon backing",
                new Vector2(900f, 202f), new Color(0.06f, 0.09f, 0.11f, 0.97f));
            Color ink = new Color(0.93f, 0.89f, 0.81f, 1f);
            heading = SoundwellUiTheme.CreateLabel(panel, 19,
                new Vector2(0f, 78f), new Vector2(842f, 30f), ink);
            heading.alignment = TextAnchor.MiddleLeft;
            summary = SoundwellUiTheme.CreateLabel(panel, 16,
                new Vector2(0f, 8f), new Vector2(842f, 112f), ink);
            summary.alignment = TextAnchor.MiddleLeft;
            summary.horizontalOverflow = HorizontalWrapMode.Wrap;
            confirmButton = SoundwellUiTheme.CreateButton(panel, "M06 recon confirm",
                new Vector2(302f, -79f), new Vector2(222f, 35f), 16,
                out confirmLabel);
            confirmButton.onClick.AddListener(Confirm);

            LayoutForViewportAspect((float)Mathf.Max(1, Screen.width) /
                Mathf.Max(1, Screen.height));
            Refresh();
            SoundwellUiLocalization.Changed += Refresh;
        }

        public void LayoutForViewportAspect(float aspect)
        {
            if (disposed) return;
            if (canvasObject == null || panel == null)
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
            heading.font = summary.font = confirmLabel.font = font;
            string prefix = zh ? "赤盐矿营侦察 · " : "RED SALT MINE CAMP · ";
            heading.text = prefix + M06MineCampReconSession.FormatLeadTitle(lead, zh);
            summary.text = M06MineCampReconSession.FormatLeadFindings(lead, zh);
            confirmLabel.text = zh ? "记下这条线索" : "Record recon lead";

            confirmButton.gameObject.SetActive(!recorded);
            confirmButton.interactable = !recorded;
            if (!recorded)
                SoundwellInputAdapter.KeepDialogFocus(canvasObject.transform, confirmButton);
        }

        private bool CanvasDestroyed()
        {
            return canvasObject == null || panel == null || heading == null ||
                summary == null || confirmButton == null || confirmLabel == null;
        }

        private void Confirm()
        {
            if (disposed || recorded || CanvasDestroyed())
                return;

            recorded = true;
            Refresh();
            try
            {
                onRecorded?.Invoke(lead);
                onRecorded = null;
            }
            catch
            {
                recorded = false;
                if (!disposed) Refresh();
                throw;
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            onRecorded = null;
            SoundwellUiLocalization.Changed -= Refresh;
            if (confirmButton != null) confirmButton.onClick.RemoveAllListeners();
            SoundwellUiTheme.SafeDestroyCanvas(canvasObject);
        }
    }
}
