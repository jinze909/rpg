using System;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Compact bottom-anchored UGUI panel for verifying an actual Red Salt
    /// outer-harbor arrival (crew, tender, receiver and usable rescue berths)
    /// before the host records M05LandingFacts. Never invents arrival or
    /// inflates rescue capacity when the tender or receiver is missing.
    /// </summary>
    public sealed class M05LandingCheckView : IDisposable
    {
        private readonly GameObject canvasObject;
        private readonly RectTransform panel;
        private readonly Font latinFont;
        private readonly RpgPixelArtManifest artManifest;
        private readonly Image statusIcon;
        private readonly Text heading;
        private readonly Text summary;
        private readonly Text confirmLabel;
        private readonly Button confirmButton;
        private readonly M05LandingReport report;
        private Action<M05LandingReport> onConfirmed;
        private bool delivered;
        private bool disposed;

        public M05LandingReport Report => report;
        public Text Summary => summary;
        public Button ConfirmButton => confirmButton;
        public RectTransform Panel => panel;
        public Image StatusIcon => statusIcon;
        public bool IsDisposed => disposed || CanvasDestroyed();

        public M05LandingCheckView(Transform parent, M05LandingReport report,
            Action<M05LandingReport> onConfirmed)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            this.onConfirmed = onConfirmed ?? throw new ArgumentNullException(nameof(onConfirmed));
            if (report.CrewArrived <= 0 || report.ReadyRescueBerths < 0 ||
                (report.ReadyRescueBerths > 0 && (!report.TenderArrived || !report.ReceiverMet)))
                throw new ArgumentOutOfRangeException(nameof(report),
                    "Landing check cannot display unverified or inflated rescue capacity.");

            this.report = report;
            latinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            artManifest = RpgPixelArtManifest.LoadDefault();
            canvasObject = new GameObject("M05 landing check canvas",
                typeof(RectTransform), typeof(Canvas));
            canvasObject.transform.SetParent(parent, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 130;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(
                SoundwellUiTheme.ReferenceWidth, SoundwellUiTheme.ReferenceHeight);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();

            var holder = new GameObject("M05 landing bottom panel", typeof(RectTransform));
            holder.transform.SetParent(canvasObject.transform, false);
            panel = (RectTransform)holder.transform;
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0f);
            panel.sizeDelta = new Vector2(904f, 206f);
            panel.anchoredPosition = new Vector2(0f, 114f);

            // Bottom safe-region panel with fine border and dark backing.
            SoundwellUiTheme.CreatePanel(panel, "M05 landing border",
                new Vector2(904f, 206f), new Color(0.64f, 0.56f, 0.42f, 1f));
            SoundwellUiTheme.CreatePanel(panel, "M05 landing backing",
                new Vector2(900f, 202f), new Color(0.06f, 0.09f, 0.11f, 0.97f));
            statusIcon = RpgStoryArtResolver.CreateOrUpdatePixelIcon(
                panel, "M05 landing status icon",
                new Vector2(-372f, 20f), new Vector2(96f, 96f),
                ResolveStatusSprite());
            Color ink = new Color(0.93f, 0.89f, 0.81f, 1f);
            heading = SoundwellUiTheme.CreateLabel(panel, 19,
                new Vector2(64f, 78f), new Vector2(714f, 30f), ink);
            heading.alignment = TextAnchor.MiddleLeft;
            summary = SoundwellUiTheme.CreateLabel(panel, 16,
                new Vector2(64f, 8f), new Vector2(714f, 112f), ink);
            summary.alignment = TextAnchor.MiddleLeft;
            summary.horizontalOverflow = HorizontalWrapMode.Wrap;
            confirmButton = SoundwellUiTheme.CreateButton(panel, "M05 landing confirm",
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
            if (CanvasDestroyed())
            {
                Dispose();
                return;
            }
            if (float.IsNaN(aspect) || float.IsInfinity(aspect) || aspect <= 0f)
                return;
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

            if (statusIcon != null)
                RpgStoryArtResolver.ApplyPixelSprite(statusIcon, ResolveStatusSprite());

            bool zh = SoundwellUiLocalization.Language == SoundwellUiLocalization.Chinese;
            Font font = SoundwellUiLocalization.PresentationFont(latinFont);
            heading.font = summary.font = confirmLabel.font = font;
            heading.text = M05LandingCheckCopy.Title(zh);
            summary.text = M05LandingCheckCopy.Describe(report, zh);
            confirmLabel.text = zh ? "确认靠岸核查" : "Confirm landing check";

            confirmButton.gameObject.SetActive(!delivered);
            confirmButton.interactable = !delivered;
            if (!delivered)
                SoundwellInputAdapter.KeepDialogFocus(canvasObject.transform, confirmButton);
        }

        private Sprite ResolveStatusSprite()
        {
            return RpgStoryArtResolver.ResolveM07PassSprite(
                report.HasUsableLandingCapacity, artManifest);
        }

        private bool CanvasDestroyed()
        {
            return canvasObject == null || panel == null || heading == null ||
                summary == null || confirmButton == null || confirmLabel == null;
        }

        private void Confirm()
        {
            if (disposed || delivered)
                return;
            if (CanvasDestroyed())
            {
                Dispose();
                return;
            }

            delivered = true;
            Refresh();
            try
            {
                onConfirmed?.Invoke(report);
                onConfirmed = null;
            }
            catch
            {
                delivered = false;
                if (!disposed) Refresh();
                throw;
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            onConfirmed = null;
            SoundwellUiLocalization.Changed -= Refresh;
            if (confirmButton != null) confirmButton.onClick.RemoveAllListeners();
            SoundwellUiTheme.SafeDestroyCanvas(canvasObject);
        }
    }

    public static class M05LandingCheckCopy
    {
        public static string Title(bool zh)
        {
            return zh ? "赤盐外港 · 靠岸核查" : "RED SALT OUTER HARBOR · LANDING CHECK";
        }

        public static string Describe(M05LandingReport report, bool zh)
        {
            if (zh)
            {
                string tender = report.TenderArrived ? "已靠岸" : "尚未到达";
                string receiver = report.ReceiverMet ? "已接头" : "尚未接头";
                string berths = report.HasUsableLandingCapacity
                    ? "当前可用接应舱位：" + report.ReadyRescueBerths
                    : "当前可用接应舱位：0（驳船或接头人未到位前不计为可转移运力）";
                return "实到船员：" + report.CrewArrived +
                    "  ｜  浅水驳船：" + tender +
                    "  ｜  外港接头人：" + receiver +
                    "\n" + berths;
            }

            string tenderEn = report.TenderArrived ? "Arrived" : "Not yet arrived";
            string receiverEn = report.ReceiverMet ? "Met" : "Not yet met";
            string berthsEn = report.HasUsableLandingCapacity
                ? "Usable rescue berths now: " + report.ReadyRescueBerths
                : "Usable rescue berths now: 0 (manifest berths do not count without tender and receiver)";
            return "Crew arrived: " + report.CrewArrived +
                "  |  Shallow tender: " + tenderEn +
                "  |  Receiver: " + receiverEn +
                "\n" + berthsEn;
        }
    }
}
