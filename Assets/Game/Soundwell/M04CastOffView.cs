using System;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Soundwell
{
    /// <summary>
    /// The player's explicit cast-off action after a verified M04 manifest.
    /// Reads and writes the existing Makinom variable authority supplied by
    /// its host; a route choice, cup gift, or UI opening never sails a ship.
    /// A saved cast-off can retry a failed scene handoff without sailing twice.
    /// The host must open this only at the actual ship departure interaction.
    /// </summary>
    public sealed class M04CastOffView : IDisposable
    {
        private readonly Func<string, int> read;
        private readonly Action<string, int> write;
        private readonly GameObject canvasObject;
        private readonly RectTransform panel;
        private readonly Font latinFont;
        private readonly Text heading;
        private readonly Text details;
        private readonly Text actionLabel;
        private readonly Button castOffButton;
        private Action onSailed;
        private bool transitioning;
        private bool completed;
        private bool disposed;

        public Button CastOffButton => castOffButton;
        public Text Details => details;
        public RectTransform Panel => panel;

        public M04CastOffView(Transform parent,
            Func<string, int> read, Action<string, int> write, Action onSailed)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            this.read = read ?? throw new ArgumentNullException(nameof(read));
            this.write = write ?? throw new ArgumentNullException(nameof(write));
            this.onSailed = onSailed ?? throw new ArgumentNullException(nameof(onSailed));
            latinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            canvasObject = new GameObject("M04 cast off canvas",
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

            var holder = new GameObject("M04 cast off bottom panel", typeof(RectTransform));
            holder.transform.SetParent(canvasObject.transform, false);
            panel = (RectTransform)holder.transform;
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0f);
            panel.sizeDelta = new Vector2(912f, 196f);
            panel.anchoredPosition = new Vector2(0f, 107f);
            SoundwellUiTheme.CreatePanel(panel, "M04 cast off border",
                new Vector2(912f, 196f),
                new Color(0.66f, 0.59f, 0.43f, 1f));
            SoundwellUiTheme.CreatePanel(panel, "M04 cast off dark backing",
                new Vector2(908f, 192f),
                new Color(0.06f, 0.09f, 0.12f, 0.97f));

            Color paper = new Color(0.94f, 0.90f, 0.81f, 1f);
            heading = SoundwellUiTheme.CreateLabel(panel, 21,
                new Vector2(0f, 71f), new Vector2(835f, 35f), paper);
            heading.alignment = TextAnchor.MiddleLeft;
            details = SoundwellUiTheme.CreateLabel(panel, 17,
                new Vector2(0f, 8f), new Vector2(838f, 95f), paper);
            details.alignment = TextAnchor.MiddleLeft;
            details.horizontalOverflow = HorizontalWrapMode.Wrap;

            castOffButton = SoundwellUiTheme.CreateButton(panel,
                "M04 cast off confirm", new Vector2(310f, -74f),
                new Vector2(220f, 36f), 16, out actionLabel);
            castOffButton.onClick.AddListener(CastOff);
            LayoutForViewportAspect((float)Mathf.Max(1, Screen.width) /
                Mathf.Max(1, Screen.height));
            SoundwellUiLocalization.Changed += Refresh;
            Refresh();
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
            if (canvasObject == null || heading == null || details == null ||
                castOffButton == null || actionLabel == null)
            {
                Dispose();
                return;
            }

            bool zh = SoundwellUiLocalization.Language == SoundwellUiLocalization.Chinese;
            Font font = SoundwellUiLocalization.PresentationFont(latinFont);
            heading.font = details.font = actionLabel.font = font;
            heading.text = zh ? "临潮港 · 松缆拔锚" : "LINCHAO · CAST OFF";

            bool ready = TryReadStatus(out M04DepartureRecord? manifest,
                out bool sailed);
            details.text = M04CastOffCopy.Describe(
                ready ? manifest : null, sailed, zh);
            actionLabel.text = sailed
                ? (zh ? "继续听雾航程" : "Continue into the fog")
                : (zh ? "松缆，正式出航" : "Cast off now");
            castOffButton.gameObject.SetActive(!completed);
            castOffButton.interactable = !completed && !transitioning && ready;
            if (castOffButton.interactable)
                SoundwellInputAdapter.KeepDialogFocus(canvasObject.transform, castOffButton);
        }

        private bool TryReadStatus(out M04DepartureRecord? manifest, out bool sailed)
        {
            manifest = null;
            sailed = false;
            if (!M04DepartureFacts.TryRead(read, out M04DepartureRecord? record) ||
                !record.HasValue ||
                !M04DepartureFacts.TryReadSailed(read, out sailed))
                return false;
            // A corrupt, noncanonical cast-off bit must never enable the
            // launch control, even if a valid manifest remains in the save.
            int sailedBit = read(M04DepartureFacts.SailedKey);
            if (sailedBit != 0 && sailedBit != 1)
                return false;
            manifest = record;
            return true;
        }

        private void CastOff()
        {
            // Direct onClick.Invoke bypasses Button.interactable.
            if (disposed || completed || transitioning || canvasObject == null ||
                !TryReadStatus(out M04DepartureRecord? manifest, out bool sailed))
                return;

            transitioning = true;
            Refresh();
            try
            {
                // A first real player cast-off records the event; a subsequent
                // invocation only retries the host's next scene if the event
                // was already saved in the authoritative Makinom store.
                if (!sailed && !M04DepartureFacts.TryMarkSailed(read, write))
                {
                    transitioning = false;
                    Refresh();
                    return;
                }
                onSailed?.Invoke();
                onSailed = null;
                completed = true;
            }
            catch
            {
                transitioning = false;
                if (!disposed) Refresh();
                throw;
            }
            transitioning = false;
            if (!disposed) Refresh();
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            onSailed = null;
            SoundwellUiLocalization.Changed -= Refresh;
            if (castOffButton != null) castOffButton.onClick.RemoveAllListeners();
            SoundwellUiTheme.SafeDestroyCanvas(canvasObject);
        }
    }

    public static class M04CastOffCopy
    {
        public static string Describe(M04DepartureRecord? manifest,
            bool sailed, bool zh)
        {
            if (!manifest.HasValue)
                return zh
                    ? "本航次还没有经过真实的船只与接应核验。航路选择和丑杯赠别都不能代替准备确认；现在不能拔锚。"
                    : "This voyage has no verified hull and landing plan yet. A route choice or Qi Que's cup is not departure readiness. You cannot cast off.";

            M04DepartureRecord m = manifest.Value;
            if (sailed)
                return zh
                    ? "缆绳已经松开，船已离岸。本航次只记一次真实拔锚；下一段听雾仍需亲自完成。"
                    : "The lines are free and the ship has left the dock. This cast-off is recorded only once; the fog passage still has to be navigated.";
            if (zh)
                return "已核实领航船准备：船员 " + m.AssignedCrew +
                    " 人，可用铺位 " + m.AvailableBerths +
                    " 个，为营救预留 " + m.ReturneesToBook +
                    " 个。\n船仍停在码头。由你松开缆绳才会正式出航，不能提前算成抵港。";
            return "Verified departure plan: " + m.AssignedCrew +
                " crew, " + m.AvailableBerths +
                " berths, " + m.ReturneesToBook +
                " reserved for the rescue.\nThe ship is still at the dock. Cast off to depart; that does not mean you've arrived.";
        }
    }
}
