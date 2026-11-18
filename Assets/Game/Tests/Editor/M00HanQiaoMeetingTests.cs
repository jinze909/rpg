using System;
using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    public sealed class M00HanQiaoMeetingTests
    {
        [Test]
        public void OpeningOffersTheThreeAuthoredQuestionsInBothLanguages()
        {
            var sequence = Begin();
            var chinese = M00HanQiaoMeetingDialogue.Describe(sequence, true);
            var english = M00HanQiaoMeetingDialogue.Describe(sequence, false);

            Assert.That(chinese.Stage, Is.EqualTo(M00HanQiaoMeetingStage.AwaitingOpeningChoice));
            Assert.That(chinese.Text, Does.Contain("你没死"));
            Assert.That(chinese.Choices, Is.EqualTo(new[]
            {
                "战报上写我死了。", "你认出我了。", "沈砾呢？"
            }));
            Assert.That(english.Choices, Is.EqualTo(new[]
            {
                "Your report says I died.", "You recognized me.", "Where is Shen Li?"
            }));
            Assert.That(chinese.CanContinue, Is.False);
        }

        [TestCase(M00HanQiaoOpeningChoice.QuestionTheReport)]
        [TestCase(M00HanQiaoOpeningChoice.AcknowledgeRecognition)]
        [TestCase(M00HanQiaoOpeningChoice.AskAboutShenLi)]
        public void EveryOpeningChoiceLeadsToTheFixedFortyOneCount(
            M00HanQiaoOpeningChoice choice)
        {
            var sequence = Begin();
            sequence.ChooseOpening(choice);
            var beat = M00HanQiaoMeetingDialogue.Describe(sequence, true);

            Assert.That(M00HanQiaoMeetingSequence.HanRansomCount, Is.EqualTo(41));
            Assert.That(beat.Stage, Is.EqualTo(M00HanQiaoMeetingStage.RansomCount));
            Assert.That(beat.Text, Does.Contain("四十一个"));
            Assert.That(beat.CanContinue, Is.True);
            Assert.That(beat.Choices, Is.Empty);
        }

        [Test]
        public void AskingAboutShenLiChangesTheFollowUpToTheFortyOneQuestion()
        {
            var sequence = Begin();
            sequence.ChooseOpening(M00HanQiaoOpeningChoice.AskAboutShenLi);
            sequence.Continue();

            var chinese = M00HanQiaoMeetingDialogue.Describe(sequence, true);
            var english = M00HanQiaoMeetingDialogue.Describe(sequence, false);

            Assert.That(sequence.AskedAboutShenLi, Is.True);
            Assert.That(chinese.Text, Does.Contain("那四十一个人里，有她吗？"));
            Assert.That(english.Text, Does.Contain("Was she among those forty-one?"));
            Assert.That(chinese.Text, Does.Contain("矿主鲁斯"));
            Assert.That(chinese.Choices, Has.Length.EqualTo(2));
        }

        [TestCase(M00HanQiaoOpeningChoice.QuestionTheReport)]
        [TestCase(M00HanQiaoOpeningChoice.AcknowledgeRecognition)]
        public void OtherOpeningQuestionsKeepTheDirectShenLiFollowUp(
            M00HanQiaoOpeningChoice choice)
        {
            var sequence = Begin();
            sequence.ChooseOpening(choice);
            sequence.Continue();

            var beat = M00HanQiaoMeetingDialogue.Describe(sequence, true);

            Assert.That(sequence.AskedAboutShenLi, Is.False);
            Assert.That(beat.Text, Does.Contain("你：沈砾呢？"));
            Assert.That(beat.Text, Does.Not.Contain("那四十一个人里，有她吗？"));
        }

        [Test]
        public void BothNextStepChoicesLeadToTheSameJiangAuntieAndLedgerClosing()
        {
            foreach (M00HanQiaoNextStepChoice choice in new[]
            {
                M00HanQiaoNextStepChoice.AskAboutTheBrokenBridge,
                M00HanQiaoNextStepChoice.ArrangeBoatToRedSalt
            })
            {
                var sequence = Begin();
                sequence.ChooseOpening(M00HanQiaoOpeningChoice.QuestionTheReport);
                sequence.Continue();
                sequence.ChooseNextStep(choice);

                var beat = M00HanQiaoMeetingDialogue.Describe(sequence, true);
                Assert.That(sequence.NextStepChoice, Is.EqualTo(choice));
                Assert.That(beat.Stage, Is.EqualTo(M00HanQiaoMeetingStage.Closing));
                Assert.That(beat.Text, Does.Contain("姜婶"));
                Assert.That(beat.Text, Does.Contain("赎俘账册"));
                Assert.That(beat.CanContinue, Is.True);
            }
        }

        [Test]
        public void MeetingFinishesOnlyAfterTheLedgerClosingHasBeenPresented()
        {
            var sequence = Begin();
            sequence.ChooseOpening(M00HanQiaoOpeningChoice.AcknowledgeRecognition);
            sequence.Continue();
            sequence.ChooseNextStep(M00HanQiaoNextStepChoice.ArrangeBoatToRedSalt);

            Assert.That(sequence.IsComplete, Is.False);
            sequence.Continue();

            var beat = M00HanQiaoMeetingDialogue.Describe(sequence, false);
            Assert.That(sequence.IsComplete, Is.True);
            Assert.That(beat.Finished, Is.True);
            Assert.That(beat.Text, Is.Empty);
        }

        [Test]
        public void InvalidTransitionsAndRepeatedBeginAreRejected()
        {
            var sequence = new M00HanQiaoMeetingSequence();
            Assert.Throws<InvalidOperationException>(() => sequence.Continue());
            Assert.Throws<InvalidOperationException>(
                () => sequence.ChooseOpening(M00HanQiaoOpeningChoice.QuestionTheReport));
            Assert.Throws<InvalidOperationException>(
                () => M00HanQiaoMeetingDialogue.Describe(sequence, true));

            sequence.Begin();
            Assert.Throws<InvalidOperationException>(() => sequence.Begin());
            Assert.Throws<ArgumentOutOfRangeException>(
                () => sequence.ChooseOpening((M00HanQiaoOpeningChoice)99));
            sequence.ChooseOpening(M00HanQiaoOpeningChoice.QuestionTheReport);
            Assert.Throws<InvalidOperationException>(() => sequence.ChooseNextStep(
                M00HanQiaoNextStepChoice.ArrangeBoatToRedSalt));
            sequence.Continue();
            Assert.Throws<ArgumentOutOfRangeException>(
                () => sequence.ChooseNextStep((M00HanQiaoNextStepChoice)99));
        }

        private static M00HanQiaoMeetingSequence Begin()
        {
            var sequence = new M00HanQiaoMeetingSequence();
            sequence.Begin();
            return sequence;
        }
    }
}
