using System;
using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    public sealed class M01HomecomingResponseTests
    {
        [Test]
        public void AnyMemorialAnswerTakesPriorityOverTheSeparateStagePrompt()
        {
            foreach (M00RollCallOutcome answer in new[]
            {
                M00RollCallOutcome.AnsweredFirst,
                M00RollCallOutcome.AnsweredSecond,
                M00RollCallOutcome.AnsweredThird
            })
            {
                Assert.That(M01HomecomingResponse.AnsweredAtMemorial(answer), Is.True);
                Assert.That(M01HomecomingResponse.XiaodouGreeting(answer, true, true),
                    Is.EqualTo("奶奶……这就是今天在广场上应声的那个人？"));
                Assert.That(M01HomecomingResponse.XiaodouGreeting(answer, false, false),
                    Does.Contain("answered the roll call"));
            }
        }

        [Test]
        public void PromptWithoutAnAnswerRecognizesOnlyTheStageHelp()
        {
            foreach (M00RollCallOutcome noAnswer in new[]
            {
                M00RollCallOutcome.Silent,
                M00RollCallOutcome.LeftCrowd,
                M00RollCallOutcome.Pending
            })
            {
                Assert.That(M01HomecomingResponse.AnsweredAtMemorial(noAnswer), Is.False);
                Assert.That(M01HomecomingResponse.XiaodouGreeting(noAnswer, true, true),
                    Is.EqualTo("奶奶……这就是今天在台下帮我提词的那个人？"));
                Assert.That(M01HomecomingResponse.XiaodouGreeting(noAnswer, true, false),
                    Does.Contain("prompted my lines"));
            }
        }

        [Test]
        public void NeitherAnswerNorStageHelpDoesNotCreateRecognition()
        {
            foreach (M00RollCallOutcome noAnswer in new[]
            {
                M00RollCallOutcome.Silent,
                M00RollCallOutcome.LeftCrowd,
                M00RollCallOutcome.Pending
            })
            {
                Assert.That(M01HomecomingResponse.XiaodouGreeting(noAnswer, false, true),
                    Is.EqualTo("奶奶……这人是真的吗？"));
                Assert.That(M01HomecomingResponse.XiaodouGreeting(noAnswer, false, false),
                    Does.Contain("really here"));
            }
        }

        [Test]
        public void MissingReturnRequestDoesNotPrematurelyApprovePensionReclassification()
        {
            Assert.That(M01HomecomingResponse.PensionPathFor(
                M01PensionChoice.RegisterLivingIdentity), Is.EqualTo("公开"));
            Assert.That(M01HomecomingResponse.PensionPathFor(
                M01PensionChoice.KeepIdentityUnregistered), Is.EqualTo("隐瞒"));
            Assert.That(M01HomecomingResponse.PensionPathFor(
                M01PensionChoice.RequestMissingReturnReview), Is.EqualTo("未处理"));
            Assert.That(M01HomecomingResponse.PensionPathFor(
                M01PensionChoice.LeaveWithoutDecision), Is.EqualTo("未处理"));
        }

        [Test]
        public void ConcealingAfterAVisibleRollCallCannotEraseWitnesses()
        {
            Assert.That(M01HomecomingResponse.ConcealmentHasPublicWitnesses(
                M00RollCallOutcome.AnsweredThird, M01PensionChoice.KeepIdentityUnregistered),
                Is.True);
            Assert.That(M01HomecomingResponse.ConcealmentHasPublicWitnesses(
                M00RollCallOutcome.Silent, M01PensionChoice.KeepIdentityUnregistered),
                Is.False);
            Assert.That(M01HomecomingResponse.ConcealmentHasPublicWitnesses(
                M00RollCallOutcome.AnsweredFirst, M01PensionChoice.RegisterLivingIdentity),
                Is.False);
            Assert.That(M01HomecomingResponse.ConcealmentHasPublicWitnesses(
                M00RollCallOutcome.AnsweredSecond, M01PensionChoice.RequestMissingReturnReview),
                Is.False);
        }

        [Test]
        public void InvalidOutcomesAndDecisionsAreNotSilentlyCommitted()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                M01HomecomingResponse.AnsweredAtMemorial((M00RollCallOutcome)99));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                M01HomecomingResponse.PensionPathFor((M01PensionChoice)99));
        }
    }
}
