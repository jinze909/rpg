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

        /// <summary>
        /// Closes the meeting and invokes the host completion callback, rolling
        /// back to <see cref="M00HanQiaoMeetingStage.Closing"/> if the host throws.
        /// </summary>
        public void Continue(Action<M00HanQiaoMeetingSequence> recordWithHost)
        {
            if (recordWithHost == null)
                throw new ArgumentNullException(nameof(recordWithHost));

            RequireStage(M00HanQiaoMeetingStage.Closing);
            Stage = M00HanQiaoMeetingStage.Complete;
            try
            {
                recordWithHost(this);
            }
            catch
            {
                Stage = M00HanQiaoMeetingStage.Closing;
                throw;
            }
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
}
