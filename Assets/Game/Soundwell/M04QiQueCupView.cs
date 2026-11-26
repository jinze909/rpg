using System;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Optional bilingual UGUI for Qi Que's tangible pre-departure gift.
    /// The scene host decides whether Qi Que actually met the player.
    /// </summary>
    public sealed class M04QiQueCupView : IDisposable
    {
        private readonly M04QiQueCupEncounter cup;
        private readonly GameObject canvasObject;
        private readonly RectTransform authored;
        private readonly Font latinFont;
        private readonly RpgPixelArtManifest artManifest;
        private readonly Image giftIcon;
        private readonly Text heading;
        private readonly Text dialogue;
        private readonly Button[] choices = new Button[3];
        private readonly Text[] choiceLabels = new Text[3];
        private readonly Button advance;
        private readonly Text advanceLabel;
        private Action<M04QiQueCupEncounter> onFinished;
        private bool delivered;
        private bool disposed;

        public M04QiQueCupEncounter Encounter => cup;
        public Text Dialogue => dialogue;
        public Image GiftIcon => giftIcon;
        public Button AdvanceButton => advance;
        public bool IsDisposed => disposed || CanvasDestroyed();
        public Button GetChoiceButton(int index)
        {
            if (index < 0 || index >= choices.Length)
                throw new ArgumentOutOfRangeException(nameof(index));
            return choices[index];
        }

        public M04QiQueCupView(Transform parent, bool qiQuePresent, bool debtKnown,
            Action<M04QiQueCupEncounter> onFinished)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (onFinished == null) throw new ArgumentNullException(nameof(onFinished));
            this.onFinished = onFinished;
            cup = new M04QiQueCupEncounter(qiQuePresent, debtKnown);
            cup.Begin();
            latinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            artManifest = RpgPixelArtManifest.LoadDefault();
            canvasObject = SoundwellUiTheme.CreateOverlayCanvas(
                parent, "M04 Qi Que cup canvas", 128, out authored);
            giftIcon = RpgStoryArtResolver.CreateOrUpdatePixelIcon(
                authored, "M04 Qi cup gift icon",
                new Vector2(-346f, 75f), new Vector2(112f, 112f),
                ResolveGiftSprite());
            heading = SoundwellUiTheme.CreateLabel(authored, 24, new Vector2(0f, 210f), new Vector2(840f, 48f));
            dialogue = SoundwellUiTheme.CreateLabel(authored, 20, new Vector2(72f, 75f), new Vector2(678f, 175f));
            dialogue.alignment = TextAnchor.MiddleLeft;
            for (int i = 0; i < choices.Length; i++)
            {
                int index = i;
                choices[i] = SoundwellUiTheme.CreateButton(authored, "M04 Qi cup choice " + i,
                    new Vector2(0f, -93f - i * 45f), new Vector2(810f, 40f), 17,
                    out choiceLabels[i]);
                choices[i].onClick.AddListener(() => Choose(index));
            }
            advance = SoundwellUiTheme.CreateButton(authored, "M04 Qi cup continue",
                new Vector2(0f, -239f), new Vector2(460f, 38f), 17, out advanceLabel);
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
            SoundwellUiTheme.ApplyViewportAspectScale(authored, aspect);
        }

        public void Refresh()
        {
            if (disposed) return;
            if (CanvasDestroyed())
            {
                Dispose();
                return;
            }
            if (giftIcon != null)
                RpgStoryArtResolver.ApplyPixelSprite(giftIcon, ResolveGiftSprite());
            bool zh = SoundwellUiLocalization.Language == SoundwellUiLocalization.Chinese;
            var font = SoundwellUiLocalization.PresentationFont(latinFont);
            heading.font = dialogue.font = advanceLabel.font = font;
            heading.text = zh ? "出航前 · 祁雀的丑杯子" : "BEFORE DEPARTURE · QI QUE'S CUP";
            dialogue.text = M04QiQueCupCopy.Narration(cup, zh);
            string[] options = M04QiQueCupCopy.Choices(cup, zh);
            for (int i = 0; i < choices.Length; i++)
            {
                choiceLabels[i].font = font;
                choiceLabels[i].text = i < options.Length ? options[i] : "";
                choices[i].gameObject.SetActive(!delivered && i < options.Length);
            }
            advanceLabel.text = cup.Stage == M04QiQueCupStage.OfferingCup
                ? (zh ? "看看那只杯子" : "Look at the cup")
                : (zh ? "继续出航准备" : "Continue preparations");
            advance.gameObject.SetActive(!delivered &&
                (cup.Stage == M04QiQueCupStage.OfferingCup ||
                 cup.Stage == M04QiQueCupStage.Responding ||
                 cup.Stage == M04QiQueCupStage.WithoutCup));
            if (!delivered)
                SoundwellInputAdapter.KeepDialogFocus(canvasObject.transform,
                    advance.gameObject.activeInHierarchy ? advance : choices[0]);
        }

        private bool CanvasDestroyed()
        {
            if (canvasObject == null || authored == null || heading == null ||
                dialogue == null || advance == null || advanceLabel == null)
                return true;
            for (int i = 0; i < choices.Length; i++)
                if (choices[i] == null || choiceLabels[i] == null)
                    return true;
            return false;
        }

        private Sprite ResolveGiftSprite()
        {
            switch (cup.Stage)
            {
                case M04QiQueCupStage.OfferingCup:
                    return RpgStoryArtResolver.ResolveM07CargoCrateSprite(false, artManifest);
                case M04QiQueCupStage.ChoosingReply:
                case M04QiQueCupStage.Responding:
                    return RpgStoryArtResolver.ResolveM07CargoCrateSprite(true, artManifest);
                case M04QiQueCupStage.WithoutCup:
                    return RpgStoryArtResolver.ResolveM04LanternSprite(false, artManifest);
                default:
                    return null;
            }
        }

        private void Choose(int index)
        {
            if (disposed || delivered)
                return;
            if (CanvasDestroyed())
            {
                Dispose();
                return;
            }
            if (cup.Stage != M04QiQueCupStage.ChoosingReply)
                return;
            if (index < 0 || index >= M04QiQueCupCopy.Choices(cup, false).Length)
                return;
            cup.Choose((M04QiQueCupReply)index);
            Refresh();
        }

        private void Advance()
        {
            if (disposed || delivered)
                return;
            if (CanvasDestroyed())
            {
                Dispose();
                return;
            }
            if (cup.Stage == M04QiQueCupStage.OfferingCup)
                cup.ExamineGift();
            else if (cup.Stage == M04QiQueCupStage.Responding ||
                     cup.Stage == M04QiQueCupStage.WithoutCup)
            {
                // Lock against reentrant UGUI submits before persisting
                // through the Makinom host. A failed write must not consume
                // the player's chosen reply or irrevocably finish this view.
                delivered = true;
                Refresh();
                try
                {
                    cup.Finish(onFinished);
                    onFinished = null;
                }
                catch
                {
                    delivered = false;
                    if (!disposed) Refresh();
                    throw;
                }
                return;
            }
            else
                return;
            Refresh();
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            onFinished = null;
            SoundwellUiLocalization.Changed -= Refresh;
            foreach (var option in choices)
                if (option != null)
                    option.onClick.RemoveAllListeners();
            if (advance != null)
                advance.onClick.RemoveAllListeners();
            SoundwellUiTheme.SafeDestroyCanvas(canvasObject);
        }
    }
}
