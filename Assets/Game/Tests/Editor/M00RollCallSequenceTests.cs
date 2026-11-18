using System;
using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    public sealed class M00RollCallSequenceTests
    {
        [Test]
        public void FirstCallAnswerStopsImmediately()
        {
            var sequence = BeginSequence();

            sequence.Respond(M00RollCallChoice.Answer);

            Assert.That(sequence.CallsMade, Is.EqualTo(1));
            Assert.That(sequence.IsComplete, Is.True);
            Assert.That(sequence.IsAwaitingResponse, Is.False);
            Assert.That(sequence.Outcome, Is.EqualTo(M00RollCallOutcome.AnsweredFirst));
            Assert.That(sequence.IdentityRevealValue, Is.EqualTo("应声第1遍"));
        }

        [Test]
        public void SecondCallAnswerStopsWithoutMakingThirdCall()
        {
            var sequence = BeginSequence();
            sequence.Respond(M00RollCallChoice.RemainSilent);

            Assert.That(sequence.AdvanceAfterUnansweredCall(), Is.True);
            Assert.That(sequence.CallsMade, Is.EqualTo(2));
            sequence.Respond(M00RollCallChoice.Answer);

            Assert.That(sequence.IsComplete, Is.True);
            Assert.That(sequence.CallsMade, Is.EqualTo(2));
            Assert.That(sequence.Outcome, Is.EqualTo(M00RollCallOutcome.AnsweredSecond));
            Assert.That(sequence.IdentityRevealValue, Is.EqualTo("应声第2遍"));
        }

        [Test]
        public void ThirdCallAnswerIsRecordedOnlyAfterTwoSilentCalls()
        {
            var sequence = BeginSequence();
            sequence.Respond(M00RollCallChoice.RemainSilent);
            sequence.AdvanceAfterUnansweredCall();
            sequence.Respond(M00RollCallChoice.RemainSilent);
            sequence.AdvanceAfterUnansweredCall();

            Assert.That(sequence.CallsMade, Is.EqualTo(3));
            sequence.Respond(M00RollCallChoice.Answer);

            Assert.That(sequence.IsComplete, Is.True);
            Assert.That(sequence.Outcome, Is.EqualTo(M00RollCallOutcome.AnsweredThird));
            Assert.That(sequence.IdentityRevealValue, Is.EqualTo("应声第3遍"));
        }

        [Test]
        public void ThreeSilentCallsFinishAsSilent()
        {
            var sequence = BeginSequence();
            sequence.Respond(M00RollCallChoice.RemainSilent);
            sequence.AdvanceAfterUnansweredCall();
            sequence.Respond(M00RollCallChoice.RemainSilent);
            sequence.AdvanceAfterUnansweredCall();
            sequence.Respond(M00RollCallChoice.RemainSilent);

            Assert.That(sequence.CallsMade, Is.EqualTo(3));
            Assert.That(sequence.IsComplete, Is.True);
            Assert.That(sequence.Outcome, Is.EqualTo(M00RollCallOutcome.Silent));
            Assert.That(sequence.IdentityRevealValue, Is.EqualTo("沉默"));
        }

        [Test]
        public void LeavingOnFirstCallStillCompletesThreeCallsWithoutMoreResponseWindows()
        {
            var sequence = BeginSequence();
            sequence.Respond(M00RollCallChoice.LeaveCrowd);

            Assert.That(sequence.HasLeftCrowd, Is.True);
            Assert.That(sequence.IsComplete, Is.False);
            Assert.That(sequence.CanRespond, Is.False);
            Assert.That(sequence.CanAdvanceAfterUnansweredCall, Is.True);

            Assert.That(sequence.AdvanceAfterUnansweredCall(), Is.False);
            Assert.That(sequence.CallsMade, Is.EqualTo(2));
            Assert.That(sequence.IsComplete, Is.False);
            Assert.That(sequence.AdvanceAfterUnansweredCall(), Is.False);

            Assert.That(sequence.CallsMade, Is.EqualTo(3));
            Assert.That(sequence.IsComplete, Is.True);
            Assert.That(sequence.Outcome, Is.EqualTo(M00RollCallOutcome.LeftCrowd));
            Assert.That(sequence.IdentityRevealValue, Is.EqualTo("离开人群"));
        }

        [Test]
        public void LeavingOnSecondCallAfterSilenceStillFinishesThirdCall()
        {
            var sequence = BeginSequence();
            sequence.Respond(M00RollCallChoice.RemainSilent);
            Assert.That(sequence.AdvanceAfterUnansweredCall(), Is.True);
            sequence.Respond(M00RollCallChoice.LeaveCrowd);

            Assert.That(sequence.AdvanceAfterUnansweredCall(), Is.False);
            Assert.That(sequence.CallsMade, Is.EqualTo(3));
            Assert.That(sequence.IsComplete, Is.True);
            Assert.That(sequence.Outcome, Is.EqualTo(M00RollCallOutcome.LeftCrowd));
        }

        [Test]
        public void LeavingOnThirdCallCompletesImmediately()
        {
            var sequence = BeginSequence();
            sequence.Respond(M00RollCallChoice.RemainSilent);
            sequence.AdvanceAfterUnansweredCall();
            sequence.Respond(M00RollCallChoice.RemainSilent);
            sequence.AdvanceAfterUnansweredCall();
            sequence.Respond(M00RollCallChoice.LeaveCrowd);

            Assert.That(sequence.CallsMade, Is.EqualTo(3));
            Assert.That(sequence.IsComplete, Is.True);
            Assert.That(sequence.Outcome, Is.EqualTo(M00RollCallOutcome.LeftCrowd));
        }

        [Test]
        public void CannotAdvanceWhileWaitingForResponseOrRespondAfterCompletion()
        {
            var sequence = BeginSequence();

            Assert.Throws<InvalidOperationException>(
                () => sequence.AdvanceAfterUnansweredCall());
            sequence.Respond(M00RollCallChoice.Answer);
            Assert.Throws<InvalidOperationException>(
                () => sequence.Respond(M00RollCallChoice.RemainSilent));
            Assert.Throws<InvalidOperationException>(
                () => sequence.AdvanceAfterUnansweredCall());
        }

        [Test]
        public void CannotBeginTheSameSequenceTwice()
        {
            var sequence = BeginSequence();

            Assert.Throws<InvalidOperationException>(() => sequence.Begin());
        }

        private static M00RollCallSequence BeginSequence()
        {
            var sequence = new M00RollCallSequence();
            sequence.Begin();
            return sequence;
        }
    }
}
