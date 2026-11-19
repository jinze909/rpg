using System;

namespace Rpg.Soundwell
{
    /// <summary>
    /// The localized, scene-independent player-facing copy for one M00 call.
    /// The roll-call sequence remains the only authority for choices and outcome.
    /// A future UGUI scene may render these strings without duplicating rules.
    /// </summary>
    public sealed class M00RollCallBeat
    {
        internal M00RollCallBeat(int callNumber, string speakerLine, string guidance,
            string[] choices, string identityRevealValue, bool finished)
        {
            CallNumber = callNumber;
            SpeakerLine = speakerLine;
            Guidance = guidance;
            Choices = choices ?? Array.Empty<string>();
            IdentityRevealValue = identityRevealValue;
            Finished = finished;
        }

        public int CallNumber { get; }
        public string SpeakerLine { get; }
        public string Guidance { get; }
        public string[] Choices { get; }
        public string IdentityRevealValue { get; }
        public bool Finished { get; }
        public bool CanRespond => Choices != null && Choices.Length == 3;
    }

    public static class M00RollCallDialogue
    {
        private static readonly string[] NoChoices = Array.Empty<string>();

        public static M00RollCallBeat Describe(M00RollCallSequence sequence, bool chinese)
        {
            if (sequence == null)
                throw new ArgumentNullException(nameof(sequence));
            if (!sequence.IsStarted)
                throw new InvalidOperationException("M00 dialogue requires a started roll call.");

            int call = sequence.CallsMade;
            if (sequence.IsComplete)
            {
                string closing;
                switch (sequence.Outcome)
                {
                    case M00RollCallOutcome.AnsweredFirst:
                    case M00RollCallOutcome.AnsweredSecond:
                    case M00RollCallOutcome.AnsweredThird:
                        closing = chinese
                            ? (sequence.Outcome == M00RollCallOutcome.AnsweredThird
                                ? "韩峤立即停下呼名，抬头三秒，手颤着合上名单：“今天的名单念到这里。”"
                                : "韩峤立即停下呼名，抬头三秒，合上名单：“今天的名单念到这里。”")
                            : (sequence.Outcome == M00RollCallOutcome.AnsweredThird
                                ? "Han Qiao stops at your answer, looks up for three seconds, and closes the list with a trembling hand: 'That is all for today.'"
                                : "Han Qiao stops at your answer, looks up for three seconds, and closes the list: 'That is all for today.'");
                        break;
                    case M00RollCallOutcome.LeftCrowd:
                        closing = chinese
                            ? "三遍无人应声。韩峤合上名单，看向你离开的方向，让人去巷口请你到碑后。"
                            : "After three unanswered calls, Han Qiao closes the list and looks where you left. He sends someone to invite you behind the memorial.";
                        break;
                    case M00RollCallOutcome.Silent:
                        closing = chinese
                            ? "三遍无人应声。韩峤合上名单，目光在人群里找到你，让人请你去碑后。"
                            : "After three unanswered calls, Han Qiao closes the list and finds you in the crowd. He asks someone to invite you behind the memorial.";
                        break;
                    default:
                        throw new InvalidOperationException("The completed roll call has no recorded outcome.");
                }

                return new M00RollCallBeat(call, closing,
                    chinese ? "仪式散场；去石碑后的小屋。" : "The ceremony ends. Continue to the room behind the memorial.",
                    NoChoices, sequence.IdentityRevealValue, true);
            }

            string spoken = chinese
                ? "韩峤（第" + ChineseNumber(call) + "遍）：“（你的名字。）”"
                : "Han Qiao (call " + call + "): '(Your name.)'";

            if (sequence.HasLeftCrowd)
                return new M00RollCallBeat(call, spoken,
                    chinese ? "你已经离开人群。韩峤仍须念完三遍，没有新的应声选项。" : "You have left the crowd. Han Qiao still completes all three calls; there is no response window.",
                    NoChoices, null, false);

            if (!sequence.IsAwaitingResponse)
                return new M00RollCallBeat(call, spoken,
                    chinese ? "这一遍无人应声；韩峤停一拍，准备下一遍。" : "No answer this time. Han Qiao pauses before the next call.",
                    NoChoices, null, false);

            return new M00RollCallBeat(call, spoken,
                chinese ? "你可以现在应声、保持沉默，或离开人群。" : "Answer now, remain silent, or leave the crowd.",
                chinese
                    ? new[] { "应声：“在。”", "继续沉默", "离开人群" }
                    : new[] { "Answer: Here.", "Remain silent", "Leave the crowd" },
                null, false);
        }

        private static string ChineseNumber(int call)
        {
            switch (call)
            {
                case 1: return "一";
                case 2: return "二";
                case 3: return "三";
                default: throw new ArgumentOutOfRangeException(nameof(call));
            }
        }
    }
}
