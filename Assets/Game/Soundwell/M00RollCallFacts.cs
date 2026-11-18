using System;
using GamingIsLove.Makinom;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Persists the M00 roll-call identity fact in Makinom's global game variables.
    /// ORK remains the save owner; this adapter only writes the story fact that
    /// later chapters read. A missing or unknown value follows v1.2's silent default.
    /// </summary>
    public static class M00RollCallFacts
    {
        public const string IdentityRevealKey = "rpg.return.m00.identity-reveal";

        private const int AnsweredFirstCode = 1;
        private const int AnsweredSecondCode = 2;
        private const int AnsweredThirdCode = 3;
        private const int SilentCode = 4;
        private const int LeftCrowdCode = 5;

        /// <summary>
        /// Records a settled roll call. Returns false only when Makinom's global
        /// variable store is not available yet; incomplete sequences are rejected.
        /// </summary>
        public static bool TryRecord(M00RollCallSequence sequence)
        {
            if (sequence == null)
                throw new ArgumentNullException(nameof(sequence));
            if (!sequence.IsComplete || sequence.Outcome == M00RollCallOutcome.Pending)
                throw new InvalidOperationException("Only a completed M00 roll call can be persisted.");

            if (!Maki.Initialized || Maki.Game?.Variables == null)
                return false;

            Maki.Game.Variables.Set(IdentityRevealKey, Encode(sequence.Outcome));
            return true;
        }

        /// <summary>
        /// Reads the saved fact. A missing key is code 0 and defaults to Silent,
        /// as required by the v1.2 narrative facts register.
        /// </summary>
        public static bool TryRead(out M00RollCallOutcome outcome)
        {
            outcome = M00RollCallOutcome.Silent;
            if (!Maki.Initialized || Maki.Game?.Variables == null)
                return false;

            outcome = Decode(Maki.Game.Variables.GetInt(IdentityRevealKey));
            return true;
        }

        public static int Encode(M00RollCallOutcome outcome)
        {
            switch (outcome)
            {
                case M00RollCallOutcome.AnsweredFirst: return AnsweredFirstCode;
                case M00RollCallOutcome.AnsweredSecond: return AnsweredSecondCode;
                case M00RollCallOutcome.AnsweredThird: return AnsweredThirdCode;
                case M00RollCallOutcome.Silent: return SilentCode;
                case M00RollCallOutcome.LeftCrowd: return LeftCrowdCode;
                default:
                    throw new ArgumentOutOfRangeException(nameof(outcome), outcome,
                        "A pending roll call has no identity fact to persist.");
            }
        }

        public static M00RollCallOutcome Decode(int storedValue)
        {
            switch (storedValue)
            {
                case AnsweredFirstCode: return M00RollCallOutcome.AnsweredFirst;
                case AnsweredSecondCode: return M00RollCallOutcome.AnsweredSecond;
                case AnsweredThirdCode: return M00RollCallOutcome.AnsweredThird;
                case SilentCode: return M00RollCallOutcome.Silent;
                case LeftCrowdCode: return M00RollCallOutcome.LeftCrowd;
                default: return M00RollCallOutcome.Silent;
            }
        }

        public static string IdentityRevealValueFor(M00RollCallOutcome outcome)
        {
            switch (outcome)
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
}
