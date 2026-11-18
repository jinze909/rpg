using System;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Encodes the M00 three-call rule without depending on scene timing or UI:
    /// an answer stops the call immediately; silence continues; leaving the
    /// crowd does not create an answer and the name is still called three times.
    /// </summary>
    public enum M00RollCallChoice
    {
        Answer,
        RemainSilent,
        LeaveCrowd
    }

    public enum M00RollCallOutcome
    {
        Pending,
        AnsweredFirst,
        AnsweredSecond,
        AnsweredThird,
        Silent,
        LeftCrowd
    }

    public sealed class M00RollCallSequence
    {
        public const int MaximumCalls = 3;

        public int CallsMade { get; private set; }
        public bool IsStarted { get; private set; }
        public bool IsComplete { get; private set; }
        public bool IsAwaitingResponse { get; private set; }
        public bool HasLeftCrowd { get; private set; }
        public M00RollCallOutcome Outcome { get; private set; } = M00RollCallOutcome.Pending;

        public bool CanRespond => IsAwaitingResponse;

        public bool CanAdvanceAfterUnansweredCall =>
            IsStarted && !IsComplete && !IsAwaitingResponse &&
            CallsMade < MaximumCalls;

        /// <summary>
        /// Exact v1.2 narrative fact value. Null until the sequence settles.
        /// </summary>
        public string IdentityRevealValue
        {
            get
            {
                switch (Outcome)
                {
                    case M00RollCallOutcome.AnsweredFirst: return "应声第1遍";
                    case M00RollCallOutcome.AnsweredSecond: return "应声第2遍";
                    case M00RollCallOutcome.AnsweredThird: return "应声第3遍";
                    case M00RollCallOutcome.Silent: return "沉默";
                    case M00RollCallOutcome.LeftCrowd: return "离开人群";
                    default: return null;
                }
            }
        }

        public void Begin()
        {
            if (IsStarted)
                throw new InvalidOperationException("M00 roll call can only begin once.");

            IsStarted = true;
            CallsMade = 1;
            IsAwaitingResponse = true;
        }

        public void Respond(M00RollCallChoice choice)
        {
            if (!IsStarted)
                throw new InvalidOperationException("M00 roll call has not begun.");
            if (IsComplete || !IsAwaitingResponse)
                throw new InvalidOperationException("M00 roll call is not waiting for a response.");

            switch (choice)
            {
                case M00RollCallChoice.Answer:
                    Complete(AnsweredOutcomeFor(CallsMade));
                    return;

                case M00RollCallChoice.RemainSilent:
                    IsAwaitingResponse = false;
                    if (CallsMade == MaximumCalls)
                        Complete(M00RollCallOutcome.Silent);
                    return;

                case M00RollCallChoice.LeaveCrowd:
                    HasLeftCrowd = true;
                    IsAwaitingResponse = false;
                    if (CallsMade == MaximumCalls)
                        Complete(M00RollCallOutcome.LeftCrowd);
                    return;

                default:
                    throw new ArgumentOutOfRangeException(nameof(choice), choice,
                        "Unknown M00 roll-call choice.");
            }
        }

        /// <summary>
        /// Advances after an unanswered call. Returns true only when the next
        /// call opens another player response window. After leaving the crowd,
        /// calls continue in the background with no further response windows.
        /// </summary>
        public bool AdvanceAfterUnansweredCall()
        {
            if (!IsStarted)
                throw new InvalidOperationException("M00 roll call has not begun.");
            if (IsComplete)
                throw new InvalidOperationException("M00 roll call is already complete.");
            if (IsAwaitingResponse)
                throw new InvalidOperationException("Resolve the current response before advancing.");
            if (CallsMade >= MaximumCalls)
                throw new InvalidOperationException("No further roll-call attempts remain.");

            CallsMade++;
            if (HasLeftCrowd)
            {
                if (CallsMade == MaximumCalls)
                    Complete(M00RollCallOutcome.LeftCrowd);
                return false;
            }

            IsAwaitingResponse = true;
            return true;
        }

        private static M00RollCallOutcome AnsweredOutcomeFor(int callNumber)
        {
            switch (callNumber)
            {
                case 1: return M00RollCallOutcome.AnsweredFirst;
                case 2: return M00RollCallOutcome.AnsweredSecond;
                case 3: return M00RollCallOutcome.AnsweredThird;
                default:
                    throw new ArgumentOutOfRangeException(nameof(callNumber), callNumber,
                        "M00 roll call has exactly three attempts.");
            }
        }

        private void Complete(M00RollCallOutcome outcome)
        {
            Outcome = outcome;
            IsComplete = true;
            IsAwaitingResponse = false;
        }
    }
}
