using System;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Soundwell
{
    /// <summary>
    /// The authored M05 outer-harbor choice about Seran's eight letters.
    /// The host owns persistence and must open this scene only after actual
    /// arrival. This view never invents arrival, recipients, or delivery.
    /// </summary>
    public sealed class M05SeranLettersView : IDisposable
    {
        private readonly GameObject canvasObject;
        private readonly RectTransform authored;
        private readonly Font latinFont;
        private readonly RpgPixelArtManifest artManifest;
        private readonly Image speakerPortrait;
        private readonly Text heading;
        private readonly Text dialogue;
        private readonly Button takeButton;
        private readonly Button leaveButton;
        private readonly Button readPacketButton;
        private readonly Button continueButton;
        private readonly Text takeLabel;
        private readonly Text leaveLabel;
        private readonly Text readPacketLabel;
        private readonly Text continueLabel;
        private Action<M05LetterDecision> onFinished;
        private M05LetterDecision? selected;
        private bool packetOpened;
        private bool delivered;
        private bool disposed;

        public M05LetterDecision? SelectedDecision => selected;
        public Text Dialogue => dialogue;
        public Button TakeButton => takeButton;
        public Button LeaveButton => leaveButton;
        public Button ReadPacketButton => readPacketButton;
        public Button ContinueButton => continueButton;
        public Image SpeakerPortrait => speakerPortrait;
        public bool IsDisposed => disposed || CanvasDestroyed();

        public M05SeranLettersView(
            Transform parent, Action<M05LetterDecision> onFinished)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            this.onFinished = onFinished ?? throw new ArgumentNullException(nameof(onFinished));
            latinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            artManifest = RpgPixelArtManifest.LoadDefault();
            canvasObject = SoundwellUiTheme.CreateOverlayCanvas(
                parent, "M05 Seran letters canvas", 131, out authored);

            speakerPortrait = RpgStoryArtResolver.CreateOrUpdatePixelIcon(
                authored, "M05 Seran portrait",
                new Vector2(-346f, 64f), new Vector2(112f, 112f),
                ResolveSpeakerSprite());
            heading = SoundwellUiTheme.CreateLabel(authored, 25,
                new Vector2(0f, 209f), new Vector2(840f, 46f));
            dialogue = SoundwellUiTheme.CreateLabel(authored, 19,
                new Vector2(72f, 49f), new Vector2(678f, 248f));
            dialogue.alignment = TextAnchor.MiddleLeft;
            dialogue.horizontalOverflow = HorizontalWrapMode.Wrap;
            dialogue.verticalOverflow = VerticalWrapMode.Overflow;

            takeButton = SoundwellUiTheme.CreateButton(authored,
                "M05 take eight letters", new Vector2(0f, -139f),
                new Vector2(780f, 45f), 18, out takeLabel);
            leaveButton = SoundwellUiTheme.CreateButton(authored,
                "M05 leave letters", new Vector2(0f, -192f),
                new Vector2(780f, 45f), 18, out leaveLabel);
            readPacketButton = SoundwellUiTheme.CreateButton(authored,
                "M05 read letter packet", new Vector2(0f, -155f),
                new Vector2(580f, 45f), 18, out readPacketLabel);
            continueButton = SoundwellUiTheme.CreateButton(authored,
                "M05 letters continue", new Vector2(0f, -210f),
                new Vector2(580f, 45f), 18, out continueLabel);

            takeButton.onClick.AddListener(() => Choose(M05LetterDecision.TakePacket));
            leaveButton.onClick.AddListener(() => Choose(M05LetterDecision.LeaveForNextBoat));
            readPacketButton.onClick.AddListener(ReadPacket);
            continueButton.onClick.AddListener(Complete);
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

            if (speakerPortrait != null)
                RpgStoryArtResolver.ApplyPixelSprite(speakerPortrait, ResolveSpeakerSprite());

            bool zh = SoundwellUiLocalization.Language == SoundwellUiLocalization.Chinese;
            Font font = SoundwellUiLocalization.PresentationFont(latinFont);
            heading.font = dialogue.font = takeLabel.font = leaveLabel.font =
                readPacketLabel.font = continueLabel.font = font;
            heading.text = zh ? "赤盐外港 · 八封家书" :
                "RED SALT OUTER HARBOR · LETTERS FROM HOME";
            dialogue.text = M05SeranLettersCopy.Describe(selected, zh, packetOpened);
            takeLabel.text = zh ? "我带进去，按名字交给他们" :
                "Take the eight letters into the camp";
            leaveLabel.text = zh ? "这趟带不了，留给下一班船" :
                "Leave the packet for the next boat";
            readPacketLabel.text = zh ? "打开信包看看" : "Open the letter packet";
            continueLabel.text = zh ? "确认本次选择" : "Confirm this choice";

            bool choosing = !selected.HasValue && !delivered;
            takeButton.gameObject.SetActive(choosing);
            leaveButton.gameObject.SetActive(choosing);
            readPacketButton.gameObject.SetActive(!delivered && !packetOpened &&
                selected == M05LetterDecision.TakePacket);
            continueButton.gameObject.SetActive(!choosing && !delivered);
            continueButton.interactable = selected.HasValue && !delivered;
            if (!delivered)
                SoundwellInputAdapter.KeepDialogFocus(
                    canvasObject.transform, choosing ? takeButton : continueButton);
        }

        private Sprite ResolveSpeakerSprite()
        {
            return artManifest != null ? artManifest.SeranLetterCarrier : null;
        }

        private bool CanvasDestroyed()
        {
            return canvasObject == null || authored == null || heading == null ||
                dialogue == null || takeButton == null || leaveButton == null ||
                readPacketButton == null || continueButton == null || takeLabel == null ||
                leaveLabel == null || readPacketLabel == null || continueLabel == null;
        }

        private static bool CanInvoke(Button button)
        {
            return button != null && button.IsActive() && button.IsInteractable();
        }

        private void Choose(M05LetterDecision choice)
        {
            if (disposed || delivered || selected.HasValue)
                return;
            if (CanvasDestroyed())
            {
                Dispose();
                return;
            }
            if (choice != M05LetterDecision.TakePacket &&
                choice != M05LetterDecision.LeaveForNextBoat)
                return;
            Button source = choice == M05LetterDecision.TakePacket ? takeButton : leaveButton;
            if (!CanInvoke(source))
                return;
            selected = choice;
            Refresh();
        }

        private void ReadPacket()
        {
            if (disposed || delivered || packetOpened ||
                selected != M05LetterDecision.TakePacket)
                return;
            if (CanvasDestroyed())
            {
                Dispose();
                return;
            }

            if (!CanInvoke(readPacketButton))
                return;
            packetOpened = true;
            Refresh();
        }

        private void Complete()
        {
            if (disposed || delivered || !selected.HasValue)
                return;
            if (CanvasDestroyed())
            {
                Dispose();
                return;
            }

            if (!CanInvoke(continueButton))
                return;

            // Disable all UGUI callbacks before the single Makinom host write.
            // An interrupted write preserves this exact choice for retry.
            delivered = true;
            Refresh();
            try
            {
                onFinished(selected.Value);
                onFinished = null;
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
            onFinished = null;
            SoundwellUiLocalization.Changed -= Refresh;
            if (takeButton != null) takeButton.onClick.RemoveAllListeners();
            if (leaveButton != null) leaveButton.onClick.RemoveAllListeners();
            if (readPacketButton != null) readPacketButton.onClick.RemoveAllListeners();
            if (continueButton != null) continueButton.onClick.RemoveAllListeners();
            SoundwellUiTheme.SafeDestroyCanvas(canvasObject);
        }
    }

    public static class M05SeranLettersCopy
    {
        public static string Describe(M05LetterDecision? choice, bool chinese,
            bool packetOpened = false)
        {
            if (!choice.HasValue)
                return chinese
                    ? "瑟兰老人守在码头，替两岸家属传信。他取出八封信，托你带进矿营。\n你可以接过信包，也可以让他留给下一班船。"
                    : "An old Seran courier carries messages between families on both shores. At the quay, he offers you eight letters for the mining camp.\nTake the packet or leave it for the next boat.";

            if (choice.Value == M05LetterDecision.LeaveForNextBoat)
                return chinese
                    ? "你说这趟带不了。老人把八封信收好，继续等下一班船。\n你没有带走信件，也没有替任何人签收。"
                    : "You cannot carry the packet this time. The courier keeps all eight letters for the next boat.\nYou have not collected or delivered any of them.";

            if (choice.Value == M05LetterDecision.TakePacket)
            {
                string carrying = chinese
                    ? "你接过八封信，收进随身包里。它们还没有送达任何人。"
                    : "You carry eight letters in your bag. None has been delivered yet.";
                if (!packetOpened)
                    return carrying;

                return carrying + (chinese
                    ? "\n第一封只有一句『我把门槛修好了』。\n第二封是一页家用账。\n第三封是一幅孩子画的画。\n另外五封写着各家的近况。"
                    : "\nThe first says, “I fixed the doorstep.”\nThe second is a page of household accounts.\nThe third is a child's drawing.\nThe other five contain everyday family news.");
            }

            throw new ArgumentOutOfRangeException(nameof(choice));
        }
    }
}
