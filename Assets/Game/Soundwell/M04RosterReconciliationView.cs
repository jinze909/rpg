using System;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Bilingual UGUI panel for M04 roster reconciliation backed strictly by
    /// an existing M04RosterLedger and host-verified berth capacity. Cannot
    /// fabricate returnees, extra berths, or a parallel save authority.
    /// </summary>
    public sealed class M04RosterReconciliationView : IDisposable
    {
        private readonly M04RosterLedger ledger;
        private readonly GameObject canvasObject;
        private readonly RectTransform authored;
        private readonly Font latinFont;
        private readonly Text heading;
        private readonly Text summary;
        private readonly Text selectedDetails;
        private readonly Button prevButton;
        private readonly Text prevLabel;
        private readonly Button nextButton;
        private readonly Text nextLabel;
        private readonly Button confirmDockButton;
        private readonly Text confirmDockLabel;
        private readonly Button resolveRollButton;
        private readonly Text resolveRollLabel;
        private readonly Button bookButton;
        private readonly Text bookLabel;
        private readonly Button unbookButton;
        private readonly Text unbookLabel;
        private readonly Button declineButton;
        private readonly Text declineLabel;
        private readonly Button finishButton;
        private readonly Text finishLabel;
        private readonly Button backButton;
        private readonly Text backLabel;
        private readonly Action<M04RosterLedger> onFinished;
        private readonly Action onBack;
        private int verifiedBerths;
        private int selectedIndex;
        private bool delivered;
        private bool disposed;

        public M04RosterLedger Ledger => ledger;
        public int VerifiedBerths => verifiedBerths;
        public int SelectedIndex => selectedIndex;
        public Text Summary => summary;
        public Text SelectedDetails => selectedDetails;
        public Button PrevButton => prevButton;
        public Button NextButton => nextButton;
        public Button ConfirmDockButton => confirmDockButton;
        public Button ResolveRollButton => resolveRollButton;
        public Button BookButton => bookButton;
        public Button UnbookButton => unbookButton;
        public Button DeclineButton => declineButton;
        public Button FinishButton => finishButton;
        public Button BackButton => backButton;

        public M04RosterReconciliationView(
            Transform parent,
            M04RosterLedger ledger,
            int verifiedBerths,
            Action<M04RosterLedger> onFinished,
            Action onBack)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (ledger == null) throw new ArgumentNullException(nameof(ledger));
            if (verifiedBerths < 0) throw new ArgumentOutOfRangeException(nameof(verifiedBerths));
            this.onFinished = onFinished ?? throw new ArgumentNullException(nameof(onFinished));
            this.onBack = onBack ?? throw new ArgumentNullException(nameof(onBack));
            this.ledger = ledger;
            this.verifiedBerths = verifiedBerths;

            latinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            canvasObject = new GameObject("M04 roster reconciliation canvas",
                typeof(RectTransform), typeof(Canvas));
            canvasObject.transform.SetParent(parent, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 130;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(960f, 540f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();

            Panel(canvas.transform, "M04 roster backdrop", new Vector2(950f, 530f),
                new Color(0.04f, 0.07f, 0.08f, 0.98f));
            var page = new GameObject("M04 roster panel", typeof(RectTransform));
            page.transform.SetParent(canvas.transform, false);
            authored = (RectTransform)page.transform;
            authored.anchorMin = authored.anchorMax = new Vector2(0.5f, 0.5f);
            authored.sizeDelta = new Vector2(960f, 540f);
            Panel(authored, "M04 roster paper", new Vector2(920f, 515f),
                new Color(0.92f, 0.89f, 0.81f));

            heading = Label(authored, 23, new Vector2(0f, 215f), new Vector2(840f, 42f));
            summary = Label(authored, 17, new Vector2(0f, 150f), new Vector2(840f, 72f));
            summary.alignment = TextAnchor.UpperLeft;
            selectedDetails = Label(authored, 18, new Vector2(0f, 52f), new Vector2(840f, 108f));
            selectedDetails.alignment = TextAnchor.UpperLeft;

            prevButton = NewButton(authored, "M04 roster prev",
                new Vector2(-290f, -38f), new Vector2(240f, 38f), out prevLabel);
            nextButton = NewButton(authored, "M04 roster next",
                new Vector2(290f, -38f), new Vector2(240f, 38f), out nextLabel);

            confirmDockButton = NewButton(authored, "M04 roster confirm dock",
                new Vector2(-280f, -92f), new Vector2(260f, 40f), out confirmDockLabel);
            resolveRollButton = NewButton(authored, "M04 roster resolve roll",
                new Vector2(0f, -92f), new Vector2(260f, 40f), out resolveRollLabel);
            declineButton = NewButton(authored, "M04 roster decline",
                new Vector2(280f, -92f), new Vector2(260f, 40f), out declineLabel);

            bookButton = NewButton(authored, "M04 roster book",
                new Vector2(-210f, -146f), new Vector2(340f, 40f), out bookLabel);
            unbookButton = NewButton(authored, "M04 roster unbook",
                new Vector2(210f, -146f), new Vector2(340f, 40f), out unbookLabel);

            finishButton = NewButton(authored, "M04 roster finish",
                new Vector2(-210f, -206f), new Vector2(340f, 44f), out finishLabel);
            backButton = NewButton(authored, "M04 roster back",
                new Vector2(210f, -206f), new Vector2(340f, 44f), out backLabel);

            prevButton.onClick.AddListener(() => StepSelection(-1));
            nextButton.onClick.AddListener(() => StepSelection(1));
            confirmDockButton.onClick.AddListener(ConfirmSelectedAtDock);
            resolveRollButton.onClick.AddListener(ResolveSelectedRollCorrection);
            bookButton.onClick.AddListener(BookSelected);
            unbookButton.onClick.AddListener(UnbookSelected);
            declineButton.onClick.AddListener(DeclineSelected);
            finishButton.onClick.AddListener(Finish);
            backButton.onClick.AddListener(GoBack);

            LayoutForViewportAspect((float)Mathf.Max(1, Screen.width) /
                Mathf.Max(1, Screen.height));
            SoundwellUiLocalization.Changed += Refresh;
            Refresh();
        }

        public void SetVerifiedBerths(int updatedBerths)
        {
            if (disposed) throw new ObjectDisposedException(nameof(M04RosterReconciliationView));
            if (delivered) throw new InvalidOperationException("Roster reconciliation has finished.");
            if (updatedBerths < 0) throw new ArgumentOutOfRangeException(nameof(updatedBerths));
            verifiedBerths = updatedBerths;
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
            heading.font = summary.font = selectedDetails.font = font;
            prevLabel.font = nextLabel.font = confirmDockLabel.font = resolveRollLabel.font = font;
            bookLabel.font = unbookLabel.font = declineLabel.font = finishLabel.font = backLabel.font = font;

            M04RosterEntry[] entries = ledger.Snapshot();
            if (entries.Length == 0)
                selectedIndex = 0;
            else if (selectedIndex < 0 || selectedIndex >= entries.Length)
                selectedIndex = Math.Max(0, Math.Min(selectedIndex, entries.Length - 1));

            M04RosterEntry current = entries.Length > 0 ? entries[selectedIndex] : null;

            heading.text = zh ? "名册对账 · 核对返乡人员" : "ROSTER RECONCILIATION · VERIFY RETURNEES";
            summary.text = M04RosterReconciliationCopy.DescribeSummary(ledger, verifiedBerths, zh);
            selectedDetails.text = M04RosterReconciliationCopy.DescribeEntry(
                current, selectedIndex, entries.Length, zh);

            prevLabel.text = zh ? "上一条" : "Previous";
            nextLabel.text = zh ? "下一条" : "Next";
            confirmDockLabel.text = zh ? "确认码头实到" : "Confirm at dock";
            resolveRollLabel.text = zh ? "更正阵亡名册" : "Resolve dead-roll entry";
            bookLabel.text = zh ? "登记占铺位" : "Book berth";
            unbookLabel.text = zh ? "取消占铺位" : "Unbook berth";
            declineLabel.text = zh ? "登记放弃本趟" : "Mark declined";
            finishLabel.text = zh ? "完成本轮对账" : "Complete reconciliation";
            backLabel.text = zh ? "返回准备" : "Return to preparations";

            bool active = !delivered;
            prevButton.interactable = active && entries.Length > 1;
            nextButton.interactable = active && entries.Length > 1;
            confirmDockButton.interactable = active && current != null &&
                (current.Status == M04RosterStatus.ListedDead ||
                 current.Status == M04RosterStatus.RansomedUnconfirmed);
            resolveRollButton.interactable = active && current != null &&
                current.RequiresRollCorrection;
            bookButton.interactable = active && current != null &&
                current.Status == M04RosterStatus.ConfirmedAtDock &&
                ledger.BookedCount < verifiedBerths;
            unbookButton.interactable = active && current != null &&
                current.Status == M04RosterStatus.BookedForVoyage;
            declineButton.interactable = active && current != null &&
                (current.Status == M04RosterStatus.ConfirmedAtDock ||
                 current.Status == M04RosterStatus.BookedForVoyage);
            finishButton.interactable = active && ledger.UnresolvedDiscrepancyCount == 0;
            backButton.interactable = active;

            if (active)
            {
                Button preferred = resolveRollButton.interactable ? resolveRollButton
                    : confirmDockButton.interactable ? confirmDockButton
                    : bookButton.interactable ? bookButton
                    : finishButton.interactable ? finishButton
                    : backButton;
                SoundwellInputAdapter.KeepDialogFocus(canvasObject.transform, preferred);
            }
        }

        private M04RosterEntry CurrentEntry()
        {
            M04RosterEntry[] entries = ledger.Snapshot();
            if (entries.Length == 0 || selectedIndex < 0 || selectedIndex >= entries.Length)
                return null;
            return entries[selectedIndex];
        }

        private void StepSelection(int delta)
        {
            if (disposed || delivered) return;
            M04RosterEntry[] entries = ledger.Snapshot();
            if (entries.Length <= 1) return;
            selectedIndex = (selectedIndex + delta + entries.Length) % entries.Length;
            Refresh();
        }

        private void ConfirmSelectedAtDock()
        {
            if (disposed || delivered) return;
            M04RosterEntry current = CurrentEntry();
            if (current == null ||
                (current.Status != M04RosterStatus.ListedDead &&
                 current.Status != M04RosterStatus.RansomedUnconfirmed))
                return;
            ledger.MarkConfirmedAtDock(current.EntryId);
            Refresh();
        }

        private void ResolveSelectedRollCorrection()
        {
            if (disposed || delivered) return;
            M04RosterEntry current = CurrentEntry();
            if (current == null || !current.RequiresRollCorrection)
                return;
            ledger.ResolveRollCorrection(current.EntryId);
            Refresh();
        }

        private void BookSelected()
        {
            if (disposed || delivered) return;
            M04RosterEntry current = CurrentEntry();
            if (current == null || !ledger.TryBookForVoyage(current.EntryId, verifiedBerths))
                return;
            Refresh();
        }

        private void UnbookSelected()
        {
            if (disposed || delivered) return;
            M04RosterEntry current = CurrentEntry();
            if (current == null || current.Status != M04RosterStatus.BookedForVoyage)
                return;
            ledger.UnbookFromVoyage(current.EntryId);
            Refresh();
        }

        private void DeclineSelected()
        {
            if (disposed || delivered) return;
            M04RosterEntry current = CurrentEntry();
            if (current == null ||
                (current.Status != M04RosterStatus.ConfirmedAtDock &&
                 current.Status != M04RosterStatus.BookedForVoyage))
                return;
            ledger.MarkDeclinedVoyage(current.EntryId);
            Refresh();
        }

        private void Finish()
        {
            // Enforce discrepancy rule at the action boundary so direct
            // Button.onClick.Invoke calls cannot bypass interactable = false.
            if (disposed || delivered || ledger.UnresolvedDiscrepancyCount != 0)
                return;
            delivered = true;
            Refresh();
            onFinished(ledger);
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
            prevButton.onClick.RemoveAllListeners();
            nextButton.onClick.RemoveAllListeners();
            confirmDockButton.onClick.RemoveAllListeners();
            resolveRollButton.onClick.RemoveAllListeners();
            bookButton.onClick.RemoveAllListeners();
            unbookButton.onClick.RemoveAllListeners();
            declineButton.onClick.RemoveAllListeners();
            finishButton.onClick.RemoveAllListeners();
            backButton.onClick.RemoveAllListeners();
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
            label = Label(obj.transform, 16, Vector2.zero, size);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(8f, 2f);
            label.rectTransform.offsetMax = new Vector2(-8f, -2f);
            return button;
        }
    }

    public static class M04RosterReconciliationCopy
    {
        public static string DescribeSummary(
            M04RosterLedger ledger, int verifiedBerths, bool chinese)
        {
            if (ledger == null) throw new ArgumentNullException(nameof(ledger));
            var copy = new StringBuilder();
            copy.Append(chinese ? "名册总计：" : "Total entries: ")
                .Append(ledger.TotalEntries)
                .Append(chinese ? "；码头已核：" : "; confirmed at dock: ")
                .Append(ledger.ConfirmedAtDockCount)
                .Append(chinese ? "；已占铺位：" : "; booked berths: ")
                .Append(ledger.BookedCount)
                .Append('/')
                .Append(verifiedBerths)
                .AppendLine();
            copy.Append(chinese ? "放弃本趟：" : "Declined: ")
                .Append(ledger.DeclinedCount)
                .Append(chinese ? "；待更正阵亡差异：" : "; pending dead-roll discrepancies: ")
                .Append(ledger.UnresolvedDiscrepancyCount);
            return copy.ToString();
        }

        public static string DescribeEntry(
            M04RosterEntry entry, int index, int total, bool chinese)
        {
            if (entry == null || total <= 0)
                return chinese ? "当前名册暂无可核对条目。" : "No roster entries to reconcile.";

            var copy = new StringBuilder();
            copy.Append(chinese ? "当前条目 (" : "Entry (")
                .Append(index + 1)
                .Append('/')
                .Append(total)
                .Append("): ")
                .AppendLine(entry.DisplayName);
            copy.Append(chinese ? "状态：" : "Status: ")
                .AppendLine(StatusText(entry.Status, chinese));
            if (entry.RequiresRollCorrection)
            {
                copy.Append(chinese
                    ? "差异：原列阵亡名册，需先完成名册更正后再结束对账。"
                    : "Discrepancy: listed on dead roll; resolve roll correction before finishing.");
            }
            return copy.ToString();
        }

        public static string StatusText(M04RosterStatus status, bool chinese)
        {
            switch (status)
            {
                case M04RosterStatus.ListedDead:
                    return chinese ? "原列阵亡名册" : "Listed dead";
                case M04RosterStatus.RansomedUnconfirmed:
                    return chinese ? "赎回待核实" : "Ransomed (unconfirmed)";
                case M04RosterStatus.ConfirmedAtDock:
                    return chinese ? "码头已核实" : "Confirmed at dock";
                case M04RosterStatus.DeclinedVoyage:
                    return chinese ? "放弃本趟出航" : "Declined voyage";
                case M04RosterStatus.BookedForVoyage:
                    return chinese ? "已登记铺位" : "Booked for voyage";
                default:
                    throw new ArgumentOutOfRangeException(nameof(status));
            }
        }
    }
}
