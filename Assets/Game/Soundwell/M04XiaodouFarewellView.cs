using System;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Soundwell
{
    // Optional UGUI slice for M04: scene hosts decide when the dock
    // departure is ready, and persist only the finished answer.
    public sealed class M04XiaodouFarewellView : IDisposable
    {
        private readonly M04XiaodouFarewell encounter;
        private readonly GameObject canvasObject;
        private readonly RectTransform authoredView;
        private readonly Font latinFont;
        private readonly RpgPixelArtManifest artManifest;
        private readonly Image stageIcon;
        private readonly Text heading;
        private readonly Text dialogue;
        private readonly Button advance;
        private readonly Text advanceLabel;
        private readonly Button[] choices = new Button[2];
        private readonly Text[] choiceLabels = new Text[2];
        private Action<M04XiaodouFarewell> onFinished;
        private bool disposed;
        private bool delivered;

        public M04XiaodouFarewell Encounter => encounter;
        public Text Dialogue => dialogue;
        public Image StageIcon => stageIcon;
        public Button AdvanceButton => advance;
        public Button GetChoiceButton(int index) => choices[index];
        public bool IsDisposed => disposed || CanvasDestroyed();

        public M04XiaodouFarewellView(Transform parent, bool promptedOnStage,
            Action<M04XiaodouFarewell> onFinished)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (onFinished == null) throw new ArgumentNullException(nameof(onFinished));
            this.onFinished = onFinished;
            encounter = new M04XiaodouFarewell(promptedOnStage);
            encounter.Begin();
            latinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            artManifest = RpgPixelArtManifest.LoadDefault();
            canvasObject = SoundwellUiTheme.CreateOverlayCanvas(
                parent, "M04 Xiaodou farewell canvas", 127, out authoredView);

            stageIcon = RpgStoryArtResolver.CreateOrUpdatePixelIcon(
                authoredView, "M04 Xiaodou farewell stage icon",
                new Vector2(-346f, 53f), new Vector2(112f, 112f),
                ResolveStageSprite());
            heading = SoundwellUiTheme.CreateLabel(authoredView, 24, new Vector2(0f, 208f),
                new Vector2(820f, 48f));
            dialogue = SoundwellUiTheme.CreateLabel(authoredView, 20, new Vector2(72f, 53f),
                new Vector2(686f, 237f));
            dialogue.alignment = TextAnchor.MiddleLeft;
            for (int i = 0; i < choices.Length; i++)
            {
                int choice = i;
                choices[i] = SoundwellUiTheme.CreateButton(authoredView, "M04 Xiaodou choice " + i,
                    new Vector2(0f, -124f - i * 49f), new Vector2(760f, 43f), 18,
                    out choiceLabels[i]);
                choices[i].onClick.AddListener(() => Choose(choice));
            }
            advance = SoundwellUiTheme.CreateButton(authoredView, "M04 Xiaodou advance",
                new Vector2(0f, -216f), new Vector2(460f, 43f), 18,
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
            // Scene unload may destroy the canvas before its owner disposes us.
            if (CanvasDestroyed())
            {
                Dispose();
                return;
            }
            if (float.IsNaN(aspect) || float.IsInfinity(aspect) || aspect <= 0f)
                return;
            SoundwellUiTheme.ApplyViewportAspectScale(authoredView, aspect);
        }

        public void Refresh()
        {
            if (disposed) return;
            if (CanvasDestroyed())
            {
                Dispose();
                return;
            }
            if (stageIcon != null)
                RpgStoryArtResolver.ApplyPixelSprite(stageIcon, ResolveStageSprite());
            bool zh = SoundwellUiLocalization.Language == SoundwellUiLocalization.Chinese;
            Font font = SoundwellUiLocalization.PresentationFont(latinFont);
            heading.font = dialogue.font = advanceLabel.font = font;
            heading.text = zh ? "出航前 · 小豆的糖人" : "BEFORE DEPARTURE · XIAODOU";
            dialogue.text = M04XiaodouFarewellCopy.Describe(encounter, zh);
            string[] options = M04XiaodouFarewellCopy.Choices(encounter, zh);
            for (int i = 0; i < choices.Length; i++)
            {
                choiceLabels[i].font = font;
                choiceLabels[i].text = i < options.Length ? options[i] : string.Empty;
                choices[i].gameObject.SetActive(!delivered && i < options.Length);
            }
            advanceLabel.text = encounter.Stage == M04FarewellStage.Greeting
                ? (zh ? "接过糖人，听小豆说" : "Take the sweet and listen")
                : (zh ? "向小豆道别" : "Say goodbye to Xiaodou");
            advance.gameObject.SetActive(!delivered &&
                (encounter.Stage == M04FarewellStage.Greeting ||
                 encounter.Stage == M04FarewellStage.HearingResponse));
            if (!delivered)
                SoundwellInputAdapter.KeepDialogFocus(canvasObject.transform,
                    advance.gameObject.activeInHierarchy ? advance : choices[0]);
        }

        private bool CanvasDestroyed()
        {
            if (canvasObject == null || authoredView == null || heading == null ||
                dialogue == null || advance == null || advanceLabel == null)
                return true;
            for (int i = 0; i < choices.Length; i++)
                if (choices[i] == null || choiceLabels[i] == null)
                    return true;
            return false;
        }

        private Sprite ResolveStageSprite()
        {
            if (delivered || encounter.Stage == M04FarewellStage.Complete)
                return null;
            switch (encounter.Stage)
            {
                case M04FarewellStage.Greeting:
                    return RpgStoryArtResolver.ResolveM04LanternSprite(true, artManifest);
                case M04FarewellStage.ChoosingTrueLine:
                    return RpgStoryArtResolver.ResolveM02EvidenceSprite(
                        M02BridgeEvidence.CleanCableCut, artManifest);
                case M04FarewellStage.HearingResponse:
                    return encounter.TaughtRealLine
                        ? RpgStoryArtResolver.ResolveM02DocumentSprite(false, artManifest)
                        : RpgStoryArtResolver.ResolveM04LanternSprite(false, artManifest);
                default:
                    return null;
            }
        }

        private void Choose(int index)
        {
            if (disposed || delivered) return;
            if (CanvasDestroyed())
            {
                Dispose();
                return;
            }
            if (encounter.Stage != M04FarewellStage.ChoosingTrueLine ||
                index < 0 || index >= choices.Length) return;
            encounter.AnswerChild((M04XiaodouAnswer)index);
            Refresh();
        }

        private void Advance()
        {
            if (disposed || delivered) return;
            if (CanvasDestroyed())
            {
                Dispose();
                return;
            }
            if (encounter.Stage == M04FarewellStage.Greeting)
                encounter.ReceiveCandy();
            else if (encounter.Stage == M04FarewellStage.HearingResponse)
            {
                // Disable all inputs before the host writes Makinom facts;
                // programmatic onClick can still fire on hidden controls.
                delivered = true;
                Refresh();
                try
                {
                    encounter.SayGoodbye(onFinished);
                    onFinished = null;
                }
                catch
                {
                    // The encounter restored HearingResponse, retaining the
                    // actual choice rather than forcing the player to repeat it.
                    delivered = false;
                    if (!disposed) Refresh();
                    throw;
                }
                return;
            }
            else return;
            Refresh();
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            onFinished = null;
            SoundwellUiLocalization.Changed -= Refresh;
            foreach (Button option in choices)
            {
                if (option != null)
                    option.onClick.RemoveAllListeners();
            }
            if (advance != null)
                advance.onClick.RemoveAllListeners();
            SoundwellUiTheme.SafeDestroyCanvas(canvasObject);
        }
    }
}
