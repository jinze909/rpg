using System;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Bilingual UGUI progress and decision panel for ST-05. It presents
    /// verified scene facts but does not create world actions or choose for
    /// Yu He or Cheng Wu.
    /// </summary>
    public sealed class ST05TwoDoorsView : IDisposable
    {
        private readonly GameObject canvasObject;
        private readonly RectTransform panel;
        private readonly Font latinFont;
        private readonly Text heading;
        private readonly Text summary;
        private readonly Text status;
        private readonly Button advanceButton;
        private readonly Text advanceLabel;
        private Action onAdvance;
        private ST05TwoDoorsPhase phase;
        private bool canAdvance;
        private bool completed;
        private string statusCopy = string.Empty;
        private bool disposed;

        public RectTransform Panel => panel;
        public Text Heading => heading;
        public Text Summary => summary;
        public Text Status => status;
        public Button AdvanceButton => advanceButton;
        public bool IsDisposed => disposed;

        public ST05TwoDoorsView(Transform parent, Action onAdvance)
        {
            if (parent == null)
                throw new ArgumentNullException(nameof(parent));
            this.onAdvance = onAdvance ?? throw new ArgumentNullException(nameof(onAdvance));

            latinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            canvasObject = SoundwellUiTheme.CreateOverlayCanvas(
                parent, "ST05 two doors canvas", 145, out panel);

            Color ink = new Color(0.12f, 0.17f, 0.20f, 1f);
            heading = SoundwellUiTheme.CreateLabel(panel, 23,
                new Vector2(0f, 188f), new Vector2(840f, 38f), ink);
            heading.alignment = TextAnchor.MiddleCenter;

            summary = SoundwellUiTheme.CreateLabel(panel, 18,
                new Vector2(0f, 66f), new Vector2(830f, 168f), ink);
            summary.alignment = TextAnchor.UpperLeft;
            summary.horizontalOverflow = HorizontalWrapMode.Wrap;
            summary.verticalOverflow = VerticalWrapMode.Overflow;

            status = SoundwellUiTheme.CreateLabel(panel, 16,
                new Vector2(0f, -82f), new Vector2(830f, 66f), ink);
            status.alignment = TextAnchor.MiddleLeft;
            status.horizontalOverflow = HorizontalWrapMode.Wrap;
            status.verticalOverflow = VerticalWrapMode.Overflow;

            advanceButton = SoundwellUiTheme.CreateButton(
                panel, "ST05 record partner decision", new Vector2(0f, -177f),
                new Vector2(780f, 48f), 17, out advanceLabel);
            advanceButton.onClick.AddListener(HandleAdvance);

            SoundwellUiLocalization.Changed += RefreshLocalizedCopy;
            RefreshLocalizedCopy();
        }

        public void Refresh(ST05TwoDoorsPhase nextPhase, bool ready,
            string nextStatus, bool isCompleted)
        {
            if (disposed)
                return;
            if (CanvasDestroyed())
            {
                Dispose();
                return;
            }

            phase = nextPhase;
            canAdvance = ready;
            completed = isCompleted;
            statusCopy = nextStatus ?? string.Empty;
            RefreshLocalizedCopy();
        }

        public void LayoutForViewportAspect(float aspect)
        {
            if (disposed)
                return;
            if (CanvasDestroyed())
            {
                Dispose();
                return;
            }
            SoundwellUiTheme.ApplyViewportAspectScale(panel, aspect);
        }

        private void RefreshLocalizedCopy()
        {
            if (disposed || CanvasDestroyed())
                return;

            bool zh = SoundwellUiLocalization.Language == SoundwellUiLocalization.Chinese;
            Font font = SoundwellUiLocalization.PresentationFont(latinFont);
            heading.font = summary.font = status.font = advanceLabel.font = font;
            heading.text = zh ? "两扇门的乔迁宴" : "TWO DOORS, ONE COURTYARD";
            summary.text = StageSummary(phase, zh);
            status.text = statusCopy;
            advanceLabel.text = zh
                ? phase == ST05TwoDoorsPhase.AgreeHomeAndKeys
                    ? "记录双方共同决定的居住安排"
                    : "记录程午本人作出的工作决定"
                : phase == ST05TwoDoorsPhase.AgreeHomeAndKeys
                    ? "Record the partners' agreed home arrangement"
                    : "Record Cheng Wu's own work decision";

            bool decisionStage = phase == ST05TwoDoorsPhase.AgreeHomeAndKeys ||
                phase == ST05TwoDoorsPhase.AwaitChengOwnJobDecision;
            advanceButton.gameObject.SetActive(decisionStage && !completed);
            advanceButton.interactable = decisionStage && canAdvance && !completed;
            if (advanceButton.gameObject.activeInHierarchy && advanceButton.interactable)
                SoundwellInputAdapter.KeepDialogFocus(canvasObject.transform, advanceButton);
        }

        private static string StageSummary(ST05TwoDoorsPhase phase, bool zh)
        {
            switch (phase)
            {
                case ST05TwoDoorsPhase.UnblockStaircase:
                    return zh
                        ? "楼梯上卡着一张双人床。郁禾和程午各自说明了工作与生活的需要；这里只记录现场真正完成的搬运方式。"
                        : "A double bed is stuck on the staircase. Yu He and Cheng Wu have their own needs; this panel records only the solution actually carried out in the world.";
                case ST05TwoDoorsPhase.TestMarketGifts:
                    return zh
                        ? "在旧货市场试亮夜画灯、检查散热石板的裂纹，并听取摊主建议。最贵的不一定最好。"
                        : "At the flea market, test the drawing lamp, inspect the cooling stone for cracks, and hear the stallholder's advice. The most expensive item is not always best.";
                case ST05TwoDoorsPhase.ClearCourtyardConflict:
                    return zh
                        ? "海风把地图吹向烤炉。追回散页、移开晾架，并为来客让出通道；不要替两人决定谁该让步。"
                        : "The sea wind carries maps toward the oven. Recover the pages, move the rack, and redirect guests without deciding which partner must give way.";
                case ST05TwoDoorsPhase.HearBothPartners:
                    return zh
                        ? "郁禾亲自向姨妈说明，程午亲自向朋友说明。两人还要谈清店铺出入、钥匙、访客和远行成本。"
                        : "Yu He speaks to her aunt and Cheng Wu to his friends. They also discuss shop access, keys, visitors, and the cost of travel.";
                case ST05TwoDoorsPhase.AgreeHomeAndKeys:
                    return zh
                        ? "记录两人共同作出的安排：保留两扇门和院中长桌，或在双方同意后试住合用一间、另一间作工作室。玩家不能替他们投票。"
                        : "Record their shared decision: keep two doors and a courtyard table, or try one shared home and one studio only with both partners' consent. The player never votes for them.";
                case ST05TwoDoorsPhase.AwaitChengOwnJobDecision:
                    return zh
                        ? "程午本人决定留在当地或接受外地测绘。只有谈妥店铺照看、钥匙和归期后，才记录他的决定。"
                        : "Cheng Wu decides whether to stay local or accept the outside survey. Record his choice only after shop cover, keys, and a return date are discussed.";
                case ST05TwoDoorsPhase.Completed:
                    return zh
                        ? "居住安排与程午自己的工作决定已经记录。远距工作、信件送达和 M15 到场仍是独立事件。"
                        : "The home agreement and Cheng's own work decision are recorded. Outside work, delivered letters, and M15 arrivals remain separate events.";
                default:
                    return string.Empty;
            }
        }

        private void HandleAdvance()
        {
            if (disposed || completed || !canAdvance || CanvasDestroyed())
                return;
            onAdvance?.Invoke();
        }

        private bool CanvasDestroyed()
        {
            return canvasObject == null || panel == null || heading == null ||
                summary == null || status == null || advanceButton == null ||
                advanceLabel == null;
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            onAdvance = null;
            SoundwellUiLocalization.Changed -= RefreshLocalizedCopy;
            if (advanceButton != null)
                advanceButton.onClick.RemoveAllListeners();
            SoundwellUiTheme.SafeDestroyCanvas(canvasObject);
        }
    }
}