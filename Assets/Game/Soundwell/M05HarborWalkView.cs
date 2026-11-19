using System;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Soundwell
{
    public enum M05HarborBeat
    {
        RepairStall,
        FisherRumor,
        MarketAccusation,
        Finished
    }

    /// <summary>
    /// The M05 harbor's observed conversations, staged after a real arrival.
    /// This is not evidence of a vessel arriving or of letters being delivered.
    /// The host controls navigation; this view owns no Makinom facts.
    /// </summary>
    public sealed class M05HarborWalkView : IDisposable
    {
        private readonly GameObject canvasObject;
        private readonly RectTransform panel;
        private readonly Font latinFont;
        private readonly Text heading;
        private readonly Text dialogue;
        private readonly Text advanceLabel;
        private readonly Button advance;
        private Action onFinished;
        private M05HarborBeat beat;
        private bool delivering;
        private bool disposed;

        public M05HarborBeat Beat => beat;
        public Text Dialogue => dialogue;
        public Button AdvanceButton => advance;
        public RectTransform Panel => panel;

        public M05HarborWalkView(Transform parent, Action onFinished)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            this.onFinished = onFinished ?? throw new ArgumentNullException(nameof(onFinished));
            latinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            canvasObject = new GameObject("M05 harbor walk canvas",
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

            var holder = new GameObject("M05 harbor bottom dialogue", typeof(RectTransform));
            holder.transform.SetParent(canvasObject.transform, false);
            panel = (RectTransform)holder.transform;
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0f);
            panel.sizeDelta = new Vector2(904f, 202f);
            panel.anchoredPosition = new Vector2(0f, 112f);

            // A compact bottom panel rather than a full-screen map cover.
            SoundwellUiTheme.CreatePanel(panel, "M05 fine border",
                new Vector2(904f, 202f), new Color(0.64f, 0.56f, 0.42f, 1f));
            SoundwellUiTheme.CreatePanel(panel, "M05 dark backing",
                new Vector2(900f, 198f), new Color(0.06f, 0.09f, 0.11f, 0.97f));
            Color ink = new Color(0.93f, 0.89f, 0.81f, 1f);
            heading = SoundwellUiTheme.CreateLabel(panel, 19,
                new Vector2(0f, 76f), new Vector2(842f, 30f), ink);
            heading.alignment = TextAnchor.MiddleLeft;
            dialogue = SoundwellUiTheme.CreateLabel(panel, 17,
                new Vector2(0f, 6f), new Vector2(842f, 110f), ink);
            dialogue.alignment = TextAnchor.MiddleLeft;
            dialogue.horizontalOverflow = HorizontalWrapMode.Wrap;
            advance = SoundwellUiTheme.CreateButton(panel, "M05 harbor advance",
                new Vector2(312f, -79f), new Vector2(202f, 35f), 16,
                out advanceLabel);
            advance.onClick.AddListener(Advance);
            LayoutForViewportAspect((float)Mathf.Max(1, Screen.width) /
                Mathf.Max(1, Screen.height));
            Refresh();
            SoundwellUiLocalization.Changed += Refresh;
        }

        public void LayoutForViewportAspect(float aspect)
        {
            if (disposed) return;
            if (canvasObject == null || panel == null) { Dispose(); return; }
            SoundwellUiTheme.ApplyViewportAspectScale(panel, aspect);
        }

        public void Refresh()
        {
            if (disposed) return;
            if (canvasObject == null || panel == null || heading == null ||
                dialogue == null || advance == null || advanceLabel == null)
            {
                Dispose();
                return;
            }
            bool zh = SoundwellUiLocalization.Language == SoundwellUiLocalization.Chinese;
            Font font = SoundwellUiLocalization.PresentationFont(latinFont);
            heading.font = dialogue.font = advanceLabel.font = font;
            heading.text = M05HarborWalkCopy.Title(beat, zh);
            dialogue.text = M05HarborWalkCopy.Describe(beat, zh);
            advanceLabel.text = beat == M05HarborBeat.MarketAccusation
                ? (zh ? "离开盐市" : "Leave the market")
                : (zh ? "继续听" : "Keep listening");
            advance.gameObject.SetActive(beat != M05HarborBeat.Finished);
            advance.interactable = !delivering && beat != M05HarborBeat.Finished;
            if (advance.interactable)
                SoundwellInputAdapter.KeepDialogFocus(canvasObject.transform, advance);
        }

        private void Advance()
        {
            // Button.onClick.Invoke bypasses interactable: guard reentrancy.
            if (disposed || delivering || canvasObject == null ||
                beat == M05HarborBeat.Finished)
                return;
            if (beat < M05HarborBeat.MarketAccusation)
            {
                beat++;
                Refresh();
                return;
            }
            delivering = true;
            Refresh();
            try
            {
                onFinished?.Invoke();
                onFinished = null;
                beat = M05HarborBeat.Finished;
            }
            catch
            {
                delivering = false;
                if (!disposed) Refresh();
                throw;
            }
            if (!disposed) Refresh();
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            onFinished = null;
            SoundwellUiLocalization.Changed -= Refresh;
            if (advance != null) advance.onClick.RemoveAllListeners();
            SoundwellUiTheme.SafeDestroyCanvas(canvasObject);
        }
    }

    public static class M05HarborWalkCopy
    {
        public static string Title(M05HarborBeat beat, bool zh)
        {
            switch (beat)
            {
                case M05HarborBeat.RepairStall:
                    return zh ? "赤盐外港 · 修船摊" : "RED SALT · REPAIR STALL";
                case M05HarborBeat.FisherRumor:
                    return zh ? "赤盐外港 · 雾的传闻" : "RED SALT · FOG RUMOR";
                case M05HarborBeat.MarketAccusation:
                    return zh ? "赤盐盐市 · 摊位前" : "RED SALT · MARKET STALL";
                case M05HarborBeat.Finished:
                    return string.Empty;
                default:
                    throw new ArgumentOutOfRangeException(nameof(beat));
            }
        }

        public static string Describe(M05HarborBeat beat, bool zh)
        {
            switch (beat)
            {
                case M05HarborBeat.RepairStall:
                    return zh
                        ? "矿工甲：『把“税务官”递我！这螺母锈死了！』\n矿工乙：『那把是“监察员”，漏油的！』\n矿工乙：『沈工今早点过这一班的工具。她让咱们收工把漏油的这把送到泵房去。』\n你认出那两个外号。沈砾今天还在带修理班。"
                        : "Miner: “Pass the Tax Collector! This nut is rusted shut!”\nAnother: “That's the Inspector. It leaks oil!”\n“Shen Li checked our tools this morning. She wants the leaky one back in the pump room after our shift.”\nYou recognize both nicknames. Shen Li is still running the repair crew.";
                case M05HarborBeat.FisherRumor:
                    return zh
                        ? "渔民在堤边补网，避开东南方的雾。\n『雾里有人叫名字，应了就回不来。这三个月里已有三条船撞礁。』\n这只是传闻，还没有得到查证。"
                        : "A fisherman avoids the fog southeast of the quay.\n“Someone calls your name in there. Answer and you won't return. Three boats struck reef in three months.”\nIt's a rumor, not a verified fact.";
                case M05HarborBeat.MarketAccusation:
                    return zh
                        ? "盐市里，一位矿工家属认出了你的脸，秤杆停在半空。\n『你就是那个逃出去的？我爹那个月只吃半碗。』\n你正想追问，她却转身照看摊子，不再回答。"
                        : "At the salt market, a miner's relative recognizes you and stops weighing goods.\n“You're the one who escaped? My father ate half a bowl that month.”\nYou try to ask what happened. She turns away to tend the stall.";
                case M05HarborBeat.Finished:
                    return string.Empty;
                default:
                    throw new ArgumentOutOfRangeException(nameof(beat));
            }
        }
    }
}
