using System;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Soundwell
{
    public enum M01WestLaneStage
    {
        AtDoor,
        AtIronBox,
        ChoosingPension,
        Concluded
    }

    /// <summary>
    /// Scene-owned UGUI presentation for M01's West Lane door and iron box.
    /// M00 facts control Xiaodou's greeting; the scene host persists the
    /// selected pension path through Makinom. The view never approves a
    /// missing-return application or overwrites the M00 roll-call answer.
    /// </summary>
    public sealed class M01WestLaneView : IDisposable
    {
        private static readonly Color Ink = new Color(0.12f, 0.17f, 0.20f);
        private static readonly Color Paper = new Color(0.91f, 0.88f, 0.79f);
        private static readonly Color Brass = new Color(0.57f, 0.37f, 0.18f);

        private readonly M00RollCallOutcome identity;
        private readonly bool xiaodouPrompted;
        private readonly GameObject canvasObject;
        private readonly RectTransform authoredView;
        private readonly Font latinFont;
        private readonly Text heading;
        private readonly Text dialogue;
        private readonly Button progressButton;
        private readonly Text progressLabel;
        private readonly Button[] choices = new Button[4];
        private readonly Text[] choiceLabels = new Text[4];
        private Action<M01PensionChoice> onChosen;
        private bool delivered;
        private bool disposed;

        public M01WestLaneStage Stage { get; private set; }
        public M01PensionChoice? Decision { get; private set; }
        public Text DialogueText => dialogue;
        public Text HeadingText => heading;
        public Button ProgressButton => progressButton;

        public M01WestLaneView(
            Transform parent, M00RollCallOutcome identity, bool xiaodouPrompted,
            Action<M01PensionChoice> onChosen)
        {
            if (parent == null)
                throw new ArgumentNullException(nameof(parent));
            if (onChosen == null)
                throw new ArgumentNullException(nameof(onChosen));

            // Reject corrupted identity values instead of fabricating a
            // response from the crowd or silently recording a pension choice.
            M01HomecomingResponse.AnsweredAtMemorial(identity);
            this.identity = identity;
            this.xiaodouPrompted = xiaodouPrompted;
            this.onChosen = onChosen;
            Stage = M01WestLaneStage.AtDoor;
            latinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            canvasObject = SoundwellUiTheme.CreateOverlayCanvas(
                parent, "M01 West Lane canvas", 121, out authoredView,
                paperColor: Paper);
            heading = SoundwellUiTheme.CreateLabel(authoredView, 24,
                new Vector2(0f, 216f), new Vector2(845f, 43f), Brass);
            dialogue = SoundwellUiTheme.CreateLabel(authoredView, 19,
                new Vector2(0f, 69f), new Vector2(835f, 232f), Ink);
            dialogue.alignment = TextAnchor.MiddleLeft;

            for (int i = 0; i < choices.Length; i++)
            {
                int selected = i;
                choices[i] = SoundwellUiTheme.CreateButton(authoredView,
                    "M01 pension choice " + i,
                    new Vector2(0f, -72f - 57f * i),
                    new Vector2(830f, 48f), 17, out choiceLabels[i]);
                choices[i].onClick.AddListener(() => Choose((M01PensionChoice)selected));
            }

            progressButton = SoundwellUiTheme.CreateButton(authoredView,
                "M01 West Lane continue", new Vector2(0f, -199f),
                new Vector2(360f, 50f), 17, out progressLabel);
            progressButton.onClick.AddListener(Advance);

            LayoutForViewportAspect((float)Mathf.Max(Screen.width, 1) /
                Mathf.Max(Screen.height, 1));
            Refresh();
            SoundwellUiLocalization.Changed += Refresh;
        }

        public Button GetChoiceButton(int index)
        {
            if (index < 0 || index >= choices.Length)
                throw new ArgumentOutOfRangeException(nameof(index));
            return choices[index];
        }

        public void LayoutForViewportAspect(float aspect)
        {
            if (disposed)
                return;
            SoundwellUiTheme.ApplyViewportAspectScale(authoredView, aspect);
        }

        public void Refresh()
        {
            if (disposed)
                return;
            if (canvasObject == null || heading == null)
            {
                Dispose();
                return;
            }

            bool chinese = SoundwellUiLocalization.Language == SoundwellUiLocalization.Chinese;
            Font font = SoundwellUiLocalization.PresentationFont(latinFont);
            heading.font = font;
            dialogue.font = font;
            progressLabel.font = font;
            heading.text = chinese ? "西巷 · 铁盒" : "WEST LANE · THE IRON BOX";

            foreach (Button choice in choices)
                choice.gameObject.SetActive(Stage == M01WestLaneStage.ChoosingPension);
            for (int i = 0; i < choiceLabels.Length; i++)
                choiceLabels[i].font = font;

            progressButton.gameObject.SetActive(!delivered &&
                Stage != M01WestLaneStage.ChoosingPension);
            if (!delivered)
                SoundwellInputAdapter.KeepDialogFocus(canvasObject.transform,
                    Stage == M01WestLaneStage.ChoosingPension ? choices[0] : progressButton);
            switch (Stage)
            {
                case M01WestLaneStage.AtDoor:
                    dialogue.text = chinese
                        ? "傍晚，姜婶家的门还关着。院里晒着粗布衣裳，屋里传来热面汤的香味。"
                        : "At dusk, Aunt Jiang's door is closed. Clean laundry hangs in the yard; hot noodle soup scents the air.";
                    progressLabel.text = chinese ? "上前敲门" : "Knock on the door";
                    break;
                case M01WestLaneStage.AtIronBox:
                    dialogue.text = (chinese
                        ? "姜婶认出了你，放下擀面杖，抱出一只铁盒。小豆从门后探头。\n小豆："
                        : "Aunt Jiang recognizes you, puts down her rolling pin, and brings out an iron box. Xiaodou peers around the door.\nXiaodou: ")
                        + M01HomecomingResponse.XiaodouGreeting(identity, xiaodouPrompted, chinese);
                    progressLabel.text = chinese ? "打开铁盒" : "Open the iron box";
                    break;
                case M01WestLaneStage.ChoosingPension:
                    dialogue.text = chinese
                        ? "盒里叠着十二年的抚恤单据，旁边的布包压着字条：“万一人回来要还”。姜婶用大部分钱养大了小豆，供她上学；若恢复活人户籍，抚恤将停发，旧账还得追缴。你怎么处理？"
                        : "The box holds twelve years of receipts for your death benefits and a note: 'If they return, repay it.' Aunt Jiang used most of the money to raise Xiaodou and pay for school. Restoring your living identity would stop the benefits and require repayment. What will you do?";
                    choiceLabels[0].text = chinese
                        ? "决定以真名登记，承担追缴"
                        : "Decide to register and face repayment";
                    choiceLabels[1].text = chinese
                        ? "暂时隐瞒身份，让抚恤继续"
                        : "Wait to register and keep the benefits for now";
                    choiceLabels[2].text = chinese
                        ? "明天询问“失踪归还”申请（尚未获准）"
                        : "Ask tomorrow about missing-return review (not approved)";
                    choiceLabels[3].text = chinese
                        ? "先离开西巷，不作决定"
                        : "Leave West Lane without deciding";
                    break;
                case M01WestLaneStage.Concluded:
                    dialogue.text = DecisionSummary(chinese);
                    progressLabel.text = chinese ? "去码头货栈" : "Go to the warehouse";
                    break;
                default:
                    throw new InvalidOperationException("Unknown West Lane scene stage.");
            }
        }

        private string DecisionSummary(bool chinese)
        {
            if (!Decision.HasValue)
                throw new InvalidOperationException("The conclusion requires a pension choice.");
            switch (Decision.Value)
            {
                case M01PensionChoice.RegisterLivingIdentity:
                    return chinese
                        ? "你决定以真名去官署登记，并承担追缴的账。姜婶把铁盒递给你：“人活着回来，比刻在石头上强。”"
                        : "You will register under your real name and shoulder the debt. Aunt Jiang pushes the iron box to you: 'Coming home alive matters more than a name on stone.'";
                case M01PensionChoice.KeepIdentityUnregistered:
                    string warning = M01HomecomingResponse.ConcealmentHasPublicWitnesses(identity, Decision.Value)
                        ? (chinese ? "但广场上已经有人听见你应声，目击者和流言不会消失。" : "But people already witnessed your public roll-call answer; hiding now cannot erase it.")
                        : string.Empty;
                    return (chinese
                        ? "你把铁盒推回去，暂不登记。姜婶让你往后在街上把帽檐压低些。官府一旦核实，账仍须追缴。"
                        : "You push the box back and remain unregistered. Aunt Jiang warns you to keep a low profile; official discovery would still bring repayment.")
                        + (warning.Length == 0 ? string.Empty : "\n" + warning);
                case M01PensionChoice.RequestMissingReturnReview:
                    return chinese
                        ? "你打算明天向韩峤问“失踪归还”的可能。姜婶提醒你：官署尚未批准，铁盒与抚恤都不能当作已经结清。"
                        : "You will ask Han Qiao tomorrow about missing-return review. Aunt Jiang warns that nothing has been approved; the benefits remain unsettled.";
                case M01PensionChoice.LeaveWithoutDecision:
                    return chinese
                        ? "你暂时离开西巷，没有向官署登记，也没有提出申请。姜婶的抚恤问题仍未处理。"
                        : "You leave West Lane without registration or application. Aunt Jiang's benefits remain unsettled.";
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void Choose(M01PensionChoice choice)
        {
            if (disposed || Stage != M01WestLaneStage.ChoosingPension)
                return;
            M01HomecomingResponse.PensionPathFor(choice);
            Decision = choice;
            Stage = M01WestLaneStage.Concluded;
            Refresh();
        }

        private void Advance()
        {
            if (disposed)
                return;
            switch (Stage)
            {
                case M01WestLaneStage.AtDoor:
                    Stage = M01WestLaneStage.AtIronBox;
                    Refresh();
                    break;
                case M01WestLaneStage.AtIronBox:
                    Stage = M01WestLaneStage.ChoosingPension;
                    Refresh();
                    break;
                case M01WestLaneStage.Concluded:
                    if (delivered || !Decision.HasValue)
                        return;
                    // A failed Makinom handoff must not permanently strand the
                    // chosen pension path. Lock UGUI while the host writes so
                    // a reentrant pad submit cannot dispatch it twice.
                    delivered = true;
                    Refresh();
                    try
                    {
                        onChosen?.Invoke(Decision.Value);
                        onChosen = null;
                    }
                    catch
                    {
                        delivered = false;
                        Refresh();
                        throw;
                    }
                    break;
            }
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            SoundwellUiLocalization.Changed -= Refresh;
            onChosen = null;
            foreach (Button button in choices)
                if (button != null)
                    button.onClick.RemoveAllListeners();
            if (progressButton != null)
                progressButton.onClick.RemoveAllListeners();
            SoundwellUiTheme.SafeDestroyCanvas(canvasObject);
        }
    }
}
