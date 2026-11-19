using System;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Shows only resources actually verified by the voyage host. The screen
    /// cannot add a ship, a pilot, a tender, a landing contact or new berths.
    /// The host remains responsible for saving any successfully accepted plan.
    /// </summary>
    public sealed class M04DepartureReadinessView : IDisposable
    {
        private readonly GameObject canvasObject;
        private readonly RectTransform authored;
        private readonly Font latinFont;
        private readonly Text heading;
        private readonly Text details;
        private readonly Text confirmLabel;
        private readonly Button confirm;
        private readonly Button back;
        private readonly Text backLabel;
        private readonly Action<M04DepartureManifest> onConfirmed;
        private readonly Action onBack;
        private M04DepartureManifest manifest;
        private bool disposed;
        private bool delivered;

        public M04DepartureManifest Manifest => manifest;
        public Text Details => details;
        public Button ConfirmButton => confirm;
        public Button BackButton => back;

        public M04DepartureReadinessView(Transform parent, M04DepartureManifest initial,
            Action<M04DepartureManifest> onConfirmed, Action onBack)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (initial == null) throw new ArgumentNullException(nameof(initial));
            this.onConfirmed = onConfirmed ?? throw new ArgumentNullException(nameof(onConfirmed));
            this.onBack = onBack ?? throw new ArgumentNullException(nameof(onBack));
            manifest = initial;
            latinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            canvasObject = new GameObject("M04 departure readiness canvas",
                typeof(RectTransform), typeof(Canvas));
            canvasObject.transform.SetParent(parent, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 129;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(960f, 540f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();
            Panel(canvas.transform, "M04 readiness backdrop", new Vector2(950f, 530f),
                new Color(0.04f, 0.07f, 0.08f, 0.98f));
            var page = new GameObject("M04 readiness panel", typeof(RectTransform));
            page.transform.SetParent(canvas.transform, false);
            authored = (RectTransform)page.transform;
            authored.anchorMin = authored.anchorMax = new Vector2(0.5f, 0.5f);
            authored.sizeDelta = new Vector2(960f, 540f);
            Panel(authored, "M04 readiness paper", new Vector2(920f, 515f),
                new Color(0.92f, 0.89f, 0.81f));
            heading = Label(authored, 24, new Vector2(0f, 209f), new Vector2(820f, 46f));
            details = Label(authored, 18, new Vector2(0f, 40f), new Vector2(830f, 277f));
            details.alignment = TextAnchor.UpperLeft;
            confirm = NewButton(authored, "M04 confirm departure",
                new Vector2(-210f, -207f), new Vector2(340f, 46f), out confirmLabel);
            back = NewButton(authored, "M04 return to preparations",
                new Vector2(210f, -207f), new Vector2(340f, 46f), out backLabel);
            confirm.onClick.AddListener(Confirm);
            back.onClick.AddListener(GoBack);
            LayoutForViewportAspect((float)Mathf.Max(1, Screen.width) /
                Mathf.Max(1, Screen.height));
            SoundwellUiLocalization.Changed += Refresh;
            Refresh();
        }

        public void SetConfirmedManifest(M04DepartureManifest updated)
        {
            if (disposed) throw new ObjectDisposedException(nameof(M04DepartureReadinessView));
            if (delivered) throw new InvalidOperationException("The departure has been confirmed.");
            manifest = updated ?? throw new ArgumentNullException(nameof(updated));
            Refresh();
        }

        public void LayoutForViewportAspect(float aspect)
        {
            if (disposed || !(aspect > 0f) || float.IsNaN(aspect) ||
                float.IsInfinity(aspect)) return;
            float width = Mathf.Sqrt(960f * 540f * aspect);
            float height = Mathf.Sqrt(960f * 540f / aspect);
            float scale = Mathf.Min(1f, Mathf.Min(width / 960f, height / 540f));
            if (scale < 1f) scale *= 0.95f;
            authored.localScale = new Vector3(scale, scale, 1f);
        }

        public void Refresh()
        {
            if (disposed) return;
            bool zh = SoundwellUiLocalization.Language == SoundwellUiLocalization.Chinese;
            Font font = SoundwellUiLocalization.PresentationFont(latinFont);
            heading.font = details.font = confirmLabel.font = backLabel.font = font;
            heading.text = zh ? "启航前 · 船位与接应核验" : "BEFORE DEPARTURE · VOYAGE CHECK";
            details.text = M04DepartureReadinessCopy.Describe(manifest, zh);
            confirmLabel.text = zh ? "确认准备齐全" : "Confirm readiness";
            backLabel.text = zh ? "返回准备" : "Return to preparations";
            confirm.interactable = !delivered && manifest.Check().Length == 0;
            back.interactable = !delivered;
            if (!delivered)
                SoundwellInputAdapter.KeepDialogFocus(canvasObject.transform,
                    confirm.interactable ? confirm : back);
        }

        private void Confirm()
        {
            // Button.onClick.Invoke in tests bypasses UGUI's interactable
            // property. Check the domain rule again at the action boundary.
            if (disposed || delivered || !manifest.ConfirmDeparture())
                return;
            delivered = true;
            Refresh();
            onConfirmed(manifest);
        }

        private void GoBack()
        {
            if (disposed || delivered) return;
            onBack();
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            SoundwellUiLocalization.Changed -= Refresh;
            confirm.onClick.RemoveAllListeners();
            back.onClick.RemoveAllListeners();
            if (canvasObject != null)
            {
                canvasObject.SetActive(false);
                if (Application.isPlaying) UnityEngine.Object.Destroy(canvasObject);
                else UnityEngine.Object.DestroyImmediate(canvasObject);
            }
        }

        private static void Panel(Transform parent, string name, Vector2 size, Color color)
        {
            var item = new GameObject(name, typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Image));
            item.transform.SetParent(parent, false);
            var graphic = item.GetComponent<Image>();
            graphic.color = color;
            graphic.raycastTarget = false;
            var rect = graphic.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
        }

        private static Text Label(Transform parent, int fontSize, Vector2 position,
            Vector2 dimensions)
        {
            var result = SoundwellView.AddLabel(parent, "", fontSize,
                new Color(0.12f, 0.17f, 0.20f), position, dimensions);
            result.resizeTextForBestFit = true;
            result.resizeTextMinSize = 10;
            result.resizeTextMaxSize = fontSize;
            result.raycastTarget = false;
            return result;
        }

        private static Button NewButton(Transform parent, string name,
            Vector2 position, Vector2 size, out Text label)
        {
            var obj = new GameObject(name, typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Image), typeof(Button));
            obj.transform.SetParent(parent, false);
            var image = obj.GetComponent<Image>();
            image.color = new Color(0.82f, 0.83f, 0.77f);
            var rect = (RectTransform)obj.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var button = obj.GetComponent<Button>();
            button.targetGraphic = image;
            label = Label(obj.transform, 18, Vector2.zero, size);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(10f, 2f);
            label.rectTransform.offsetMax = new Vector2(-10f, -2f);
            return button;
        }
    }

    public static class M04DepartureReadinessCopy
    {
        public static string Describe(M04DepartureManifest manifest, bool chinese)
        {
            if (manifest == null) throw new ArgumentNullException(nameof(manifest));
            var blockers = manifest.Check();
            var copy = new StringBuilder();
            copy.Append(chinese ? "实际核对的铺位：" : "Verified berths: ");
            copy.Append(manifest.AvailableBerths);
            copy.Append(chinese ? "；本趟登记的返乡者：" : "; returnees on this trip: ");
            copy.Append(manifest.ReturneesToBook);
            copy.AppendLine();
            if (blockers.Length == 0)
            {
                copy.Append(chinese ? "船只与登岸接应均已核实，可以申请正式确认。" :
                    "The ship and landing arrangements are verified. You may confirm this plan.");
                return copy.ToString();
            }
            copy.AppendLine(chinese ? "尚不能启航；必须先解决：" :
                "Departure is blocked. Resolve these items first:");
            foreach (M04DepartureBlocker issue in blockers)
            {
                copy.Append("• ");
                copy.AppendLine(BlockerText(issue, chinese));
            }
            return copy.ToString();
        }

        public static string BlockerText(M04DepartureBlocker issue, bool chinese)
        {
            switch (issue)
            {
                case M04DepartureBlocker.NoConfirmedHull:
                    return chinese ? "尚无核实的船只" : "No confirmed vessel";
                case M04DepartureBlocker.NoConfirmedPilot:
                    return chinese ? "缺少确认的领航人" : "No confirmed pilot";
                case M04DepartureBlocker.NoCrewAssigned:
                    return chinese ? "尚未安排船员" : "No assigned crew";
                case M04DepartureBlocker.UncheckedDraft:
                    return chinese ? "尚未检查船只吃水" : "Draft has not been checked";
                case M04DepartureBlocker.NoShallowTender:
                    return chinese ? "缺少浅滩接驳艇" : "No shallow-water tender";
                case M04DepartureBlocker.NoLandingReceiver:
                    return chinese ? "尚无指定登岸接应" : "No named landing receiver";
                case M04DepartureBlocker.NoReturneeDemand:
                    return chinese ? "尚未登记本趟返乡者" : "No returnees have been booked";
                case M04DepartureBlocker.InsufficientBerths:
                    return chinese ? "已核实铺位不足" : "Not enough verified berths";
                case M04DepartureBlocker.MissingOfficialStamp:
                    return chinese ? "官方航路缺少有效的登记批准" : "Official passage lacks a valid stamp";
                case M04DepartureBlocker.UnacceptedEscortFlag:
                    return chinese ? "陆钧的公开护航旗尚未确认" : "Lu Jun's public escort flag is not accepted";
                default:
                    throw new ArgumentOutOfRangeException(nameof(issue));
            }
        }
    }
}
