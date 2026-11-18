using System;
using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    public sealed class M00RollCallDialogueTests
    {
        [Test]
        public void FirstCallOffersThreeRealChoicesInBothLanguages()
        {
            var sequence = Start();
            var zh = M00RollCallDialogue.Describe(sequence, true);
            var en = M00RollCallDialogue.Describe(sequence, false);

            Assert.That(zh.CallNumber, Is.EqualTo(1));
            Assert.That(zh.SpeakerLine, Does.Contain("第一遍"));
            Assert.That(zh.Choices, Is.EqualTo(new[] { "应声：“在。”", "继续沉默", "离开人群" }));
            Assert.That(en.Choices, Is.EqualTo(new[] { "Answer: Here.", "Remain silent", "Leave the crowd" }));
            Assert.That(en.CanRespond, Is.True);
            Assert.That(en.Finished, Is.False);
            Assert.That(en.IdentityRevealValue, Is.Null);
        }

        [Test]
        public void SilenceOpensASecondAndThenThirdCallNotAnEarlyConclusion()
        {
            var sequence = Start();
            sequence.Respond(M00RollCallChoice.RemainSilent);
            var pause = M00RollCallDialogue.Describe(sequence, true);
            Assert.That(pause.CanRespond, Is.False);
            Assert.That(pause.Finished, Is.False);
            Assert.That(sequence.AdvanceAfterUnansweredCall(), Is.True);
            Assert.That(M00RollCallDialogue.Describe(sequence, true).SpeakerLine, Does.Contain("第二遍"));
            sequence.Respond(M00RollCallChoice.RemainSilent);
            Assert.That(sequence.AdvanceAfterUnansweredCall(), Is.True);
            Assert.That(M00RollCallDialogue.Describe(sequence, true).SpeakerLine, Does.Contain("第三遍"));
        }

        [Test]
        public void AnswerOnThirdCallStopsWithoutExtraCallAndKeepsItsFact()
        {
            var sequence = Start();
            sequence.Respond(M00RollCallChoice.RemainSilent);
            sequence.AdvanceAfterUnansweredCall();
            sequence.Respond(M00RollCallChoice.RemainSilent);
            sequence.AdvanceAfterUnansweredCall();
            sequence.Respond(M00RollCallChoice.Answer);

            var ending = M00RollCallDialogue.Describe(sequence, true);
            Assert.That(ending.Finished, Is.True);
            Assert.That(ending.CanRespond, Is.False);
            Assert.That(ending.CallNumber, Is.EqualTo(3));
            Assert.That(ending.IdentityRevealValue, Is.EqualTo("应声第3遍"));
            Assert.That(ending.SpeakerLine, Does.Contain("手颤"));
        }

        [Test]
        public void FirstCallAnswerEndsImmediatelyInsteadOfOfferingSecondCall()
        {
            var sequence = Start();
            sequence.Respond(M00RollCallChoice.Answer);

            var ending = M00RollCallDialogue.Describe(sequence, false);
            Assert.That(ending.CallNumber, Is.EqualTo(1));
            Assert.That(ending.CanRespond, Is.False);
            Assert.That(ending.Finished, Is.True);
            Assert.That(ending.IdentityRevealValue, Is.EqualTo("应声第1遍"));
        }

        [Test]
        public void LeavingCrowdKeepsTheRemainingSpokenCallsButOffersNoMoreChoices()
        {
            var sequence = Start();
            sequence.Respond(M00RollCallChoice.LeaveCrowd);

            Assert.That(sequence.AdvanceAfterUnansweredCall(), Is.False);
            var second = M00RollCallDialogue.Describe(sequence, false);
            Assert.That(second.CallNumber, Is.EqualTo(2));
            Assert.That(second.CanRespond, Is.False);
            Assert.That(second.SpeakerLine, Does.Contain("call 2"));
            Assert.That(sequence.AdvanceAfterUnansweredCall(), Is.False);

            var ending = M00RollCallDialogue.Describe(sequence, true);
            Assert.That(ending.CallNumber, Is.EqualTo(3));
            Assert.That(ending.Finished, Is.True);
            Assert.That(ending.IdentityRevealValue, Is.EqualTo("离开人群"));
            Assert.That(ending.SpeakerLine, Does.Contain("离开的方向"));
        }

        [Test]
        public void ThreeSilencesFindThePlayerStillInTheCrowd()
        {
            var sequence = Start();
            for (int i = 1; i <= 3; i++)
            {
                sequence.Respond(M00RollCallChoice.RemainSilent);
                if (i < 3)
                    sequence.AdvanceAfterUnansweredCall();
            }

            var ending = M00RollCallDialogue.Describe(sequence, true);
            Assert.That(ending.IdentityRevealValue, Is.EqualTo("沉默"));
            Assert.That(ending.SpeakerLine, Does.Contain("人群"));
            Assert.That(ending.CanRespond, Is.False);
        }

        [Test]
        public void RenderingAnUnstartedSequenceIsRejected()
        {
            Assert.Throws<ArgumentNullException>(() => M00RollCallDialogue.Describe(null, true));
            Assert.Throws<InvalidOperationException>(
                () => M00RollCallDialogue.Describe(new M00RollCallSequence(), true));
        }

        private static M00RollCallSequence Start()
        {
            var sequence = new M00RollCallSequence();
            sequence.Begin();
            return sequence;
        }
    }
}
