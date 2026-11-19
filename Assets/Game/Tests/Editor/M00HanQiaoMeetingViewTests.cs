using System;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Tests.Editor
{
    public sealed class M00HanQiaoMeetingViewTests
    {
        private GameObject root;
        private M00HanQiaoMeetingView view;
        private string priorLanguage;

        [SetUp]
        public void SetUp()
        {
            priorLanguage = SoundwellUiLocalization.Language;
            SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
            root = new GameObject("M00 Han Qiao meeting view test");
        }

        [TearDown]
        public void TearDown()
        {
            view?.Dispose();
            if (root != null)
                UnityEngine.Object.DestroyImmediate(root);
            SoundwellUiLocalization.SetLanguage(priorLanguage);
        }

        [Test]
        public void QuestionsCountShenLiAndLedgerClosingFollowTheAuthoredStages()
        {
            M00HanQiaoMeetingSequence sequence = Begun();
            int completed = 0;
            view = new M00HanQiaoMeetingView(root.transform, sequence, _ => completed++);

            Assert.That(view.CurrentBeat.Choices, Has.Length.EqualTo(3));
            Assert.That(view.ContinueButton.gameObject.activeSelf, Is.False);
            view.GetChoiceButton(2).onClick.Invoke();
            Assert.That(sequence.Stage, Is.EqualTo(M00HanQiaoMeetingStage.RansomCount));
            Assert.That(view.DialogueText.text, Does.Contain("Forty-one"));
            Assert.That(view.ContinueButton.gameObject.activeSelf, Is.True);

            view.ContinueButton.onClick.Invoke();
            Assert.That(sequence.AskedAboutShenLi, Is.True);
            Assert.That(view.CurrentBeat.Choices, Has.Length.EqualTo(2));
            Assert.That(view.DialogueText.text, Does.Contain("among those forty-one"));

            view.GetChoiceButton(0).onClick.Invoke();
            Assert.That(sequence.NextStepChoice,
                Is.EqualTo(M00HanQiaoNextStepChoice.AskAboutTheBrokenBridge));
            Assert.That(view.DialogueText.text, Does.Contain("ledger"));
            view.ContinueButton.onClick.Invoke();
            Assert.That(sequence.IsComplete, Is.True);
            Assert.That(completed, Is.EqualTo(1));
            view.ContinueButton.onClick.Invoke();
            Assert.That(completed, Is.EqualTo(1));
        }

        [Test]
        public void SwitchingLanguagesPreservesStageAndUpdatesVisibleChoices()
        {
            M00HanQiaoMeetingSequence sequence = Begun();
            view = new M00HanQiaoMeetingView(root.transform, sequence, null);

            SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
            Assert.That(view.HeadingText.text, Is.EqualTo("石碑后的小屋"));
            Assert.That(view.GetChoiceButton(2).GetComponentInChildren<Text>(true).text,
                Is.EqualTo("沈砾呢？"));
            Assert.That(sequence.Stage, Is.EqualTo(M00HanQiaoMeetingStage.AwaitingOpeningChoice));

            view.GetChoiceButton(0).onClick.Invoke();
            Assert.That(view.DialogueText.text, Does.Contain("四十一个"));
            SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
            Assert.That(view.DialogueText.text, Does.Contain("Forty-one"));
            Assert.That(sequence.OpeningChoice,
                Is.EqualTo(M00HanQiaoOpeningChoice.QuestionTheReport));
        }

        [Test]
        public void DestroyedViewCannotReceiveStaleLocaleChanges()
        {
            view = new M00HanQiaoMeetingView(root.transform, Begun(), null);
            UnityEngine.Object.DestroyImmediate(root);
            root = null;
            Assert.DoesNotThrow(() =>
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese));
            view.Dispose();
            Assert.DoesNotThrow(() =>
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English));
        }

        [Test]
        public void PortraitAndUltrawideKeepOneSharedMeetingState()
        {
            M00HanQiaoMeetingSequence sequence = Begun();
            view = new M00HanQiaoMeetingView(root.transform, sequence, null);
            view.LayoutForViewportAspect(9f / 16f);
            float portrait = view.DialogueText.rectTransform.parent.localScale.x;
            view.LayoutForViewportAspect(5f);
            float ultrawide = view.DialogueText.rectTransform.parent.localScale.x;
            Assert.That(portrait, Is.GreaterThan(0f).And.LessThanOrEqualTo(1f));
            Assert.That(ultrawide, Is.GreaterThan(0f).And.LessThanOrEqualTo(1f));
            Assert.That(sequence.Stage, Is.EqualTo(M00HanQiaoMeetingStage.AwaitingOpeningChoice));
        }

        [Test]
        public void CannotConstructBeforeTheMeetingBegins()
        {
            Assert.Throws<InvalidOperationException>(() =>
                new M00HanQiaoMeetingView(root.transform,
                    new M00HanQiaoMeetingSequence(), null));
        }

        [Test]
        public void HostFailureOnClosingRollsBackSoPlayerCanRetryAndFinish()
        {
            M00HanQiaoMeetingSequence sequence = Begun();
            int attempts = 0;
            int completed = 0;
            bool failOnce = true;

            view = new M00HanQiaoMeetingView(root.transform, sequence, finished =>
            {
                attempts++;
                Assert.That(finished.IsComplete, Is.True);
                Assert.That(view.ContinueButton.gameObject.activeSelf, Is.False);
                view.ContinueButton.onClick.Invoke();
                if (failOnce)
                {
                    failOnce = false;
                    throw new InvalidOperationException("save failed");
                }

                completed++;
            });

            view.GetChoiceButton(2).onClick.Invoke();
            view.ContinueButton.onClick.Invoke();
            view.GetChoiceButton(0).onClick.Invoke();
            Assert.That(sequence.Stage, Is.EqualTo(M00HanQiaoMeetingStage.Closing));

            Assert.Throws<InvalidOperationException>(() =>
                view.ContinueButton.onClick.Invoke());
            Assert.That(attempts, Is.EqualTo(1));
            Assert.That(completed, Is.EqualTo(0));
            Assert.That(sequence.Stage, Is.EqualTo(M00HanQiaoMeetingStage.Closing));
            Assert.That(sequence.IsComplete, Is.False);
            Assert.That(view.ContinueButton.gameObject.activeSelf, Is.True);

            view.ContinueButton.onClick.Invoke();
            Assert.That(attempts, Is.EqualTo(2));
            Assert.That(completed, Is.EqualTo(1));
            Assert.That(sequence.IsComplete, Is.True);
            Assert.That(view.ContinueButton.gameObject.activeSelf, Is.False);

            view.ContinueButton.onClick.Invoke();
            Assert.That(completed, Is.EqualTo(1));
        }

        private static M00HanQiaoMeetingSequence Begun()
        {
            var sequence = new M00HanQiaoMeetingSequence();
            sequence.Begin();
            return sequence;
        }
    }
}
