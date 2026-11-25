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
        private readonly RpgPixelArtManifest artManifest;
        private readonly Image speakerPortrait;
        private readonly Text heading;
        private readonly Text dialogue;
        private readonly Text advanceLabel;
        private readonly Button advance;
        private Action onFinished;
        private Action<M05HarborBeat> onBeatObserved;
        private M05HarborBeat beat;
        private readonly bool singleBeat;
        private bool delivering;
        private bool disposed;

        public M05HarborBeat Beat => beat;
        public Text Dialogue => dialogue;
        public Button AdvanceButton => advance;
        public RectTransform Panel => panel;
        public Image SpeakerPortrait => speakerPortrait;
        public bool IsDisposed => disposed || CanvasDestroyed();

        // The original sequential staging remains available to existing M05 hosts.
        public M05HarborWalkView(Transform parent, Action onFinished)
            : this(parent, onFinished, M05HarborBeat.RepairStall, false, null) { }

        // A sequential host can record a completed conversation before the
        // view advances. The view itself owns no new save authority.
        public M05HarborWalkView(Transform parent, Action onFinished,
            Action<M05HarborBeat> onBeatObserved)
            : this(parent, onFinished, M05HarborBeat.RepairStall, false, onBeatObserved) { }

        // The exploration map presents only the scene tied to its physical location.
        public M05HarborWalkView(Transform parent, Action onFinished, M05HarborBeat onlyBeat)
            : this(parent, onFinished, onlyBeat, true, null) { }

        private M05HarborWalkView(Transform parent, Action onFinished,
            M05HarborBeat startBeat, bool singleBeat,
            Action<M05HarborBeat> onBeatObserved)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (startBeat < M05HarborBeat.RepairStall ||
                startBeat >= M05HarborBeat.Finished)
                throw new ArgumentOutOfRangeException(nameof(startBeat));
            this.onFinished = onFinished ?? throw new ArgumentNullException(nameof(onFinished));
            this.onBeatObserved = onBeatObserved;
            this.singleBeat = singleBeat;
            beat = startBeat;
            latinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            artManifest = RpgPixelArtManifest.LoadDefault();
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
            speakerPortrait = RpgStoryArtResolver.CreateOrUpdatePixelIcon(
                panel, "M05 speaker portrait",
                new Vector2(-372f, 20f), new Vector2(96f, 96f),
                ResolveSpeakerSprite(beat));
            Color ink = new Color(0.93f, 0.89f, 0.81f, 1f);
            heading = SoundwellUiTheme.CreateLabel(panel, 19,
                new Vector2(64f, 76f), new Vector2(714f, 30f), ink);
            heading.alignment = TextAnchor.MiddleLeft;
            dialogue = SoundwellUiTheme.CreateLabel(panel, 17,
                new Vector2(64f, 6f), new Vector2(714f, 110f), ink);
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
            if (speakerPortrait != null)
                RpgStoryArtResolver.ApplyPixelSprite(speakerPortrait, ResolveSpeakerSprite(beat));
            bool zh = SoundwellUiLocalization.Language == SoundwellUiLocalization.Chinese;
            Font font = SoundwellUiLocalization.PresentationFont(latinFont);
            heading.font = dialogue.font = advanceLabel.font = font;
            heading.text = M05HarborWalkCopy.Title(beat, zh);
            dialogue.text = M05HarborWalkCopy.Describe(beat, zh);
            advanceLabel.text = beat == M05HarborBeat.MarketAccusation
                ? (zh ? "离开盐市" : "Leave the market")
                : singleBeat
                    ? (zh ? "返回外港" : "Return to harbor")
                    : (zh ? "继续听" : "Keep listening");
            advance.gameObject.SetActive(beat != M05HarborBeat.Finished);
            advance.interactable = !delivering && beat != M05HarborBeat.Finished;
            if (advance.interactable)
                SoundwellInputAdapter.KeepDialogFocus(canvasObject.transform, advance);
        }

        private bool CanvasDestroyed()
        {
            return canvasObject == null || panel == null || heading == null ||
                dialogue == null || advance == null || advanceLabel == null;
        }

        private Sprite ResolveSpeakerSprite(M05HarborBeat currentBeat)
        {
            if (artManifest == null) return null;
            switch (currentBeat)
            {
                case M05HarborBeat.RepairStall:
                    return artManifest.HarborRepairer;
                case M05HarborBeat.FisherRumor:
                    return artManifest.OuterHarborFisher;
                case M05HarborBeat.MarketAccusation:
                    return artManifest.MiningFamilySaltVendor;
                default:
                    return null;
            }
        }

        private void Advance()
        {
            // Button.onClick.Invoke bypasses interactable: guard reentrancy.
            if (disposed || delivering || beat == M05HarborBeat.Finished)
                return;
            if (CanvasDestroyed())
            {
                Dispose();
                return;
            }
            // Direct or stale UI callbacks must respect the effective parent
            // interaction lock before witnessing a conversation.
            if (!advance.IsActive() || !advance.IsInteractable())
                return;

            // A failed save must leave this same witnessed beat actionable;
            // do not advance to the next conversation until it is committed.
            delivering = true;
            Refresh();
            try
            {
                onBeatObserved?.Invoke(beat);
                if (!singleBeat && beat < M05HarborBeat.MarketAccusation)
                {
                    beat++;
                    delivering = false;
                    if (!disposed) Refresh();
                    return;
                }
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
            onBeatObserved = null;
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
