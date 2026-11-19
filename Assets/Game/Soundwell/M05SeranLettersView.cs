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
        private readonly Text heading;
        private readonly Text dialogue;
        private readonly Button takeButton;
        private readonly Button leaveButton;
        private readonly Button continueButton;
        private readonly Text takeLabel;
        private readonly Text leaveLabel;
        private readonly Text continueLabel;
        private Action<M05LetterDecision> onFinished;
        private M05LetterDecision? selected;
        private bool delivered;
        private bool disposed;

        public M05LetterDecision? SelectedDecision => selected;
        public Text Dialogue => dialogue;
        public Button TakeButton => takeButton;
        public Button LeaveButton => leaveButton;
        public Button ContinueButton => continueButton;

        public M05SeranLettersView(
            Transform parent, Action<M05LetterDecision> onFinished)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            this.onFinished = onFinished ?? throw new ArgumentNullException(nameof(onFinished));
            latinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            canvasObject = SoundwellUiTheme.CreateOverlayCanvas(
                parent, "M05 Seran letters canvas", 131, out authored);

            heading = SoundwellUiTheme.CreateLabel(authored, 25,
                new Vector2(0f, 209f), new Vector2(840f, 46f));
            dialogue = SoundwellUiTheme.CreateLabel(authored, 19,
                new Vector2(0f, 49f), new Vector2(822f, 248f));
            dialogue.alignment = TextAnchor.MiddleLeft;
            dialogue.horizontalOverflow = HorizontalWrapMode.Wrap;
            dialogue.verticalOverflow = VerticalWrapMode.Overflow;

            takeButton = SoundwellUiTheme.CreateButton(authored,
                "M05 take eight letters", new Vector2(0f, -139f),
                new Vector2(780f, 45f), 18, out takeLabel);
            leaveButton = SoundwellUiTheme.CreateButton(authored,
                "M05 leave letters", new Vector2(0f, -192f),
                new Vector2(780f, 45f), 18, out leaveLabel);
            continueButton = SoundwellUiTheme.CreateButton(authored,
                "M05 letters continue", new Vector2(0f, -210f),
                new Vector2(580f, 45f), 18, out continueLabel);

            takeButton.onClick.AddListener(() => Choose(M05LetterDecision.TakePacket));
            leaveButton.onClick.AddListener(() => Choose(M05LetterDecision.LeaveForNextBoat));
            continueButton.onClick.AddListener(Complete);
            LayoutForViewportAspect((float)Mathf.Max(1, Screen.width) /
                Mathf.Max(1, Screen.height));
            Refresh();
            SoundwellUiLocalization.Changed += Refresh;
        }

        public void LayoutForViewportAspect(float aspect)
        {
            if (disposed) return;
            if (canvasObject == null || authored == null)
            {
                Dispose();
                return;
            }
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

            bool zh = SoundwellUiLocalization.Language == SoundwellUiLocalization.Chinese;
            Font font = SoundwellUiLocalization.PresentationFont(latinFont);
            heading.font = dialogue.font = takeLabel.font = leaveLabel.font =
                continueLabel.font = font;
            heading.text = zh ? "赤盐外港 · 瑟兰的八封信" :
                "OUTER HARBOR · SERAN'S EIGHT LETTERS";
            dialogue.text = M05SeranLettersCopy.Describe(selected, zh);
            takeLabel.text = zh ? "我带进去，按名字交给他们" :
                "Take the eight letters into the camp";
            leaveLabel.text = zh ? "这趟带不了，留给下一班船" :
                "Leave the packet for the next boat";
            continueLabel.text = zh ? "确认本次选择" : "Confirm this choice";

            bool choosing = !selected.HasValue && !delivered;
            takeButton.gameObject.SetActive(choosing);
            leaveButton.gameObject.SetActive(choosing);
            continueButton.gameObject.SetActive(!choosing && !delivered);
            continueButton.interactable = selected.HasValue && !delivered;
            if (!delivered)
                SoundwellInputAdapter.KeepDialogFocus(
                    canvasObject.transform, choosing ? takeButton : continueButton);
        }

        private bool CanvasDestroyed()
        {
            return canvasObject == null || authored == null || heading == null ||
                dialogue == null || takeButton == null || leaveButton == null ||
                continueButton == null || takeLabel == null || leaveLabel == null ||
                continueLabel == null;
        }

        private void Choose(M05LetterDecision choice)
        {
            if (disposed || delivered || CanvasDestroyed() || selected.HasValue)
                return;
            if (choice != M05LetterDecision.TakePacket &&
                choice != M05LetterDecision.LeaveForNextBoat)
                return;
            selected = choice;
            Refresh();
        }

        private void Complete()
        {
            if (disposed || delivered || CanvasDestroyed() || !selected.HasValue)
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
                Refresh();
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
            if (continueButton != null) continueButton.onClick.RemoveAllListeners();
            SoundwellUiTheme.SafeDestroyCanvas(canvasObject);
        }
    }

    public static class M05SeranLettersCopy
    {
        public static string Describe(M05LetterDecision? choice, bool chinese)
        {
            if (!choice.HasValue)
                return chinese
                    ? "瑟兰老人守在码头，替两岸家属传信。他取出八封信，托你带进矿营。\n你可以接过信包，也可以让他留给下一班船。"
                    : "Seran waits at the quay, carrying messages between families on both shores. He offers you eight letters for the mining camp.\nTake the packet or leave it for the next boat.";

            if (choice.Value == M05LetterDecision.LeaveForNextBoat)
                return chinese
                    ? "你说这趟带不了。瑟兰把八封信收好，继续等下一班船。\n你没有带走信件，也没有替任何人签收。"
                    : "You cannot carry the packet this time. Seran keeps all eight letters for the next boat.\nYou have not collected or delivered any of them.";

            if (choice.Value == M05LetterDecision.TakePacket)
                return chinese
                    ? "你接过八封信，收进随身包里。它们还没有送达任何人。\n第一封：『我把门槛修好了。』\n第二封：密密麻麻的一页家用账目。\n第三封：小孩画的一幅画。\n另外五封：写着各自的家常。"
                    : "You carry eight letters in your bag. None has been delivered yet.\nFirst: “I fixed the doorstep.”\nSecond: a page of household accounts.\nThird: a child's drawing.\nThe other five contain everyday family news.";

            throw new ArgumentOutOfRangeException(nameof(choice));
        }
    }
}
