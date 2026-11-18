using System;

namespace Rpg.Soundwell
{
    public enum M01PensionChoice
    {
        RegisterLivingIdentity,
        KeepIdentityUnregistered,
        RequestMissingReturnReview,
        LeaveWithoutDecision
    }

    /// <summary>
    /// The first M01 homecoming consequences from the v1.2 scene script.
    /// Pure dialogue/decision logic: M00 remains the identity-fact writer,
    /// Makinom owns scene facts and ORK remains the save authority.
    /// </summary>
    public static class M01HomecomingResponse
    {
        public static bool AnsweredAtMemorial(M00RollCallOutcome outcome)
        {
            switch (outcome)
            {
                case M00RollCallOutcome.AnsweredFirst:
                case M00RollCallOutcome.AnsweredSecond:
                case M00RollCallOutcome.AnsweredThird:
                    return true;

                case M00RollCallOutcome.Pending:
                case M00RollCallOutcome.Silent:
                case M00RollCallOutcome.LeftCrowd:
                    return false;

                default:
                    throw new ArgumentOutOfRangeException(nameof(outcome), outcome, null);
            }
        }

        /// <summary>
        /// Answering the roll call takes precedence over helping Xiaodou with
        /// his stage line. Prompting alone never fabricates a public answer.
        /// Pending/missing identity follows the authored silent default.
        /// </summary>
        public static string XiaodouGreeting(
            M00RollCallOutcome identity, bool xiaodouPrompted, bool chinese)
        {
            if (AnsweredAtMemorial(identity))
                return chinese
                    ? "奶奶……这就是今天在广场上应声的那个人？"
                    : "Grandma... is this the person who answered the roll call in the square today?";

            if (xiaodouPrompted)
                return chinese
                    ? "奶奶……这就是今天在台下帮我提词的那个人？"
                    : "Grandma... is this the person who prompted my lines from below the stage today?";

            return chinese
                ? "奶奶……这人是真的吗？"
                : "Grandma... is this person really here?";
        }

        /// <summary>
        /// Asking about missing-return registration in M01 is not approval.
        /// Only M02 can change the historical path after formal adjudication.
        /// </summary>
        public static string PensionPathFor(M01PensionChoice choice)
        {
            switch (choice)
            {
                case M01PensionChoice.RegisterLivingIdentity:
                    return "公开";
                case M01PensionChoice.KeepIdentityUnregistered:
                    return "隐瞒";
                case M01PensionChoice.RequestMissingReturnReview:
                case M01PensionChoice.LeaveWithoutDecision:
                    return "未处理";
                default:
                    throw new ArgumentOutOfRangeException(nameof(choice), choice, null);
            }
        }

        public static bool ConcealmentHasPublicWitnesses(
            M00RollCallOutcome identity, M01PensionChoice choice)
        {
            // A later decision to hide the registry entry cannot erase
            // witnesses to an earlier public memorial response.
            string path = PensionPathFor(choice);
            return path == "隐瞒" && AnsweredAtMemorial(identity);
        }
    }
}
