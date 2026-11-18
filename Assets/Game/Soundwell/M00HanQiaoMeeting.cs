using System;

namespace Rpg.Soundwell
{
    public enum M00HanQiaoMeetingStage
    {
        NotStarted,
        AwaitingOpeningChoice,
        RansomCount,
        AwaitingNextStepChoice,
        Closing,
        Complete
    }

    public enum M00HanQiaoOpeningChoice
    {
        QuestionTheReport,
        AcknowledgeRecognition,
        AskAboutShenLi
    }

    public enum M00HanQiaoNextStepChoice
    {
        AskAboutTheBrokenBridge,
        ArrangeBoatToRedSalt
    }

    /// <summary>
    /// Scene-independent state for the M00 meeting behind the memorial.
    /// The authored count is fixed at 41; the player's questions and next-step
    /// choice remain transient until a later chapter defines a persistent fact.
    /// </summary>
    public sealed class M00HanQiaoMeetingSequence
    {
        public const int HanRansomCount = 41;

        public M00HanQiaoMeetingStage Stage { get; private set; }
        public bool IsStarted { get; private set; }
        public bool IsComplete => Stage == M00HanQiaoMeetingStage.Complete;
        public M00HanQiaoOpeningChoice? OpeningChoice { get; private set; }
        public M00HanQiaoNextStepChoice? NextStepChoice { get; private set; }
        public bool AskedAboutShenLi =>
            OpeningChoice == M00HanQiaoOpeningChoice.AskAboutShenLi;

        public void Begin()
        {
            if (IsStarted)
                throw new InvalidOperationException("The M00 Han Qiao meeting can only begin once.");

            IsStarted = true;
            Stage = M00HanQiaoMeetingStage.AwaitingOpeningChoice;
        }

        public void ChooseOpening(M00HanQiaoOpeningChoice choice)
        {
            RequireStage(M00HanQiaoMeetingStage.AwaitingOpeningChoice);
            if (!Enum.IsDefined(typeof(M00HanQiaoOpeningChoice), choice))
                throw new ArgumentOutOfRangeException(nameof(choice), choice, null);

            OpeningChoice = choice;
            Stage = M00HanQiaoMeetingStage.RansomCount;
        }

        /// <summary>
        /// Advances the authored fixed-count exchange, or closes the meeting
        /// after the shared final invitation and ledger image.
        /// </summary>
        public void Continue()
        {
            if (Stage == M00HanQiaoMeetingStage.RansomCount)
            {
                Stage = M00HanQiaoMeetingStage.AwaitingNextStepChoice;
                return;
            }

            if (Stage == M00HanQiaoMeetingStage.Closing)
            {
                Stage = M00HanQiaoMeetingStage.Complete;
                return;
            }

            throw new InvalidOperationException(
                "The M00 Han Qiao meeting has no continuation at its current stage.");
        }

        public void ChooseNextStep(M00HanQiaoNextStepChoice choice)
        {
            RequireStage(M00HanQiaoMeetingStage.AwaitingNextStepChoice);
            if (!Enum.IsDefined(typeof(M00HanQiaoNextStepChoice), choice))
                throw new ArgumentOutOfRangeException(nameof(choice), choice, null);

            NextStepChoice = choice;
            Stage = M00HanQiaoMeetingStage.Closing;
        }

        private void RequireStage(M00HanQiaoMeetingStage expected)
        {
            if (!IsStarted)
                throw new InvalidOperationException("The M00 Han Qiao meeting has not begun.");
            if (Stage != expected)
                throw new InvalidOperationException(
                    "The M00 Han Qiao meeting is not at " + expected + ".");
        }
    }

    public sealed class M00HanQiaoMeetingBeat
    {
        internal M00HanQiaoMeetingBeat(M00HanQiaoMeetingStage stage, string text,
            string[] choices, bool canContinue, bool finished)
        {
            Stage = stage;
            Text = text;
            Choices = choices ?? Array.Empty<string>();
            CanContinue = canContinue;
            Finished = finished;
        }

        public M00HanQiaoMeetingStage Stage { get; }
        public string Text { get; }
        public string[] Choices { get; }
        public bool CanContinue { get; }
        public bool Finished { get; }
    }

    /// <summary>
    /// Bilingual copy for the M00 post-roll-call meeting. It deliberately
    /// does not own persistence or duplicate the three-call identity fact.
    /// </summary>
    public static class M00HanQiaoMeetingDialogue
    {
        private static readonly string[] NoChoices = Array.Empty<string>();

        public static M00HanQiaoMeetingBeat Describe(
            M00HanQiaoMeetingSequence sequence, bool chinese)
        {
            if (sequence == null)
                throw new ArgumentNullException(nameof(sequence));
            if (!sequence.IsStarted)
                throw new InvalidOperationException("Begin the M00 Han Qiao meeting before rendering it.");

            switch (sequence.Stage)
            {
                case M00HanQiaoMeetingStage.AwaitingOpeningChoice:
                    return new M00HanQiaoMeetingBeat(sequence.Stage,
                        chinese
                            ? "韩峤（指了指桌对面的座位）：坐下吃口热饭吧。……你没死。"
                            : "Han Qiao points to the seat across the table: 'Sit down and have something warm. ...You're not dead.'",
                        chinese
                            ? new[] { "战报上写我死了。", "你认出我了。", "沈砾呢？" }
                            : new[] { "The battle report says I died.", "You recognized me.", "Where is Shen Li?" },
                        false, false);

                case M00HanQiaoMeetingStage.RansomCount:
                    return new M00HanQiaoMeetingBeat(sequence.Stage,
                        chinese
                            ? "韩峤：我知道还有人活着。我接回过一些。\n你：多少？\n韩峤：四十一个。从停战到现在。"
                            : "Han Qiao: 'I knew some people were still alive. I've brought a few back.'\nYou: 'How many?'\nHan Qiao: 'Forty-one, since the ceasefire.'",
                        NoChoices, true, false);

                case M00HanQiaoMeetingStage.AwaitingNextStepChoice:
                    string followUp = sequence.AskedAboutShenLi
                        ? (chinese ? "你：那四十一个人里，有她吗？"
                                   : "You: 'Was she among those forty-one?'")
                        : (chinese ? "你：沈砾呢？"
                                   : "You: 'Where is Shen Li?'");
                    string response = chinese
                        ? "韩峤：不在我手里的名单里。（停）前些天赤盐传回消息，矿主鲁斯在镜湾会盟前调动劳力，要把人拆散转运。你想让我去查，还是先想办法弄一条船？"
                        : "Han Qiao: 'She isn't on the list I have.' (A pause.) Word came from Red Salt a few days ago: before the Mirror Bay summit, mine owner Russ is moving laborers to split them up and transport them. Do you want me to investigate, or should we find a ship first?";
                    return new M00HanQiaoMeetingBeat(sequence.Stage,
                        followUp + "\n" + response,
                        chinese
                            ? new[] { "战报说桥被敌军烧了。可我记得，桥是先断的。", "先弄船。我要去赤盐。" }
                            : new[] { "The report says the enemy burned the bridge. But I remember the bridge broke first.", "Find a ship first. I want to go to Red Salt." },
                        false, false);

                case M00HanQiaoMeetingStage.Closing:
                    return new M00HanQiaoMeetingBeat(sequence.Stage,
                        chinese
                            ? "韩峤（停了一拍）：船的事，明天来港务署谈。你今晚先去西巷看一眼姜婶——十二年前你留的紧急联系人是她，抚恤一直发在她手里。\n\n（韩峤起身去倒茶。桌上，他的赎俘账册摊开着。最后一页，你的名字被墨涂过，涂了又写，写了又涂。）\n\n镜头停在那一页。"
                            : "Han Qiao pauses. 'We'll discuss the ship at the harbor office tomorrow. Tonight, go see Auntie Jiang in West Lane. You left her as your emergency contact twelve years ago; she's been receiving the death benefits in your name ever since.'\n\n(Han Qiao gets up to pour tea. His ransom ledger lies open on the table. On the last page, your name has been inked out, written back in, and inked out again.)\n\nThe camera holds on that page.",
                        NoChoices, true, false);

                case M00HanQiaoMeetingStage.Complete:
                    return new M00HanQiaoMeetingBeat(sequence.Stage, string.Empty,
                        NoChoices, false, true);

                default:
                    throw new InvalidOperationException(
                        "The M00 Han Qiao meeting has not entered a renderable stage.");
            }
        }
    }
}
