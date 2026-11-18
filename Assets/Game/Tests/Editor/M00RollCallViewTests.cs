using System;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Tests.Editor
{
    public sealed class M00RollCallViewTests
    {
        private GameObject root;
        private M00RollCallView view;

        [TearDown]
        public void TearDown()
        {
            if (view != null)
                view.Dispose();
            if (root != null)
                UnityEngine.Object.DestroyImmediate(root);
        }

        [Test]
        public void AnsweringFirstCallHidesFurtherChoicesAndContinuesWithFirstCallFact()
        {
            M00RollCallSequence sequence = Begin();
            M00RollCallOutcome? continued = null;
            view = new M00RollCallView(root.transform, sequence, outcome => continued = outcome);

            Assert.That(view.CurrentBeat.CallNumber, Is.EqualTo(1));
            Assert.That(view.CurrentBeat.CanRespond, Is.True);
            Assert.That(ActiveChoices(), Is.EqualTo(3));
            Assert.That(Label(view.GetChoiceButton(0)), Is.EqualTo(view.CurrentBeat.Choices[0]));

            view.GetChoiceButton(0).onClick.Invoke();

            Assert.That(sequence.IsComplete, Is.True);
            Assert.That(sequence.CallsMade, Is.EqualTo(1));
            Assert.That(view.CurrentBeat.IdentityRevealValue, Is.EqualTo("应声第1遍"));
            Assert.That(ActiveChoices(), Is.Zero);
            Assert.That(view.ContinueButton.gameObject.activeSelf, Is.True);

            view.ContinueButton.onClick.Invoke();
            Assert.That(continued, Is.EqualTo(M00RollCallOutcome.AnsweredFirst));
        }

        [Test]
        public void SilenceRequiresAnExplicitPauseAdvanceBeforeSecondResponseWindow()
        {
            M00RollCallSequence sequence = Begin();
            view = new M00RollCallView(root.transform, sequence, null);

            view.GetChoiceButton(1).onClick.Invoke();

            Assert.That(view.IsWaitingForNextCall, Is.True);
            Assert.That(ActiveChoices(), Is.Zero);
            Assert.That(view.CurrentBeat.Finished, Is.False);
            Assert.That(view.AdvanceAfterUnansweredCall(), Is.True);
            Assert.That(view.CallsMade, Is.EqualTo(2));
            Assert.That(view.CurrentBeat.SpeakerLine,
                Does.Contain(SoundwellUiLocalization.Language == SoundwellUiLocalization.Chinese
                    ? "第二遍" : "call 2"));
            Assert.That(ActiveChoices(), Is.EqualTo(3));

            view.GetChoiceButton(0).onClick.Invoke();
            Assert.That(sequence.Outcome, Is.EqualTo(M00RollCallOutcome.AnsweredSecond));
            Assert.That(view.CurrentBeat.IdentityRevealValue, Is.EqualTo("应声第2遍"));
        }

        [Test]
        public void LeavingCrowdKeepsThreeSpokenCallsWithoutFurtherChoiceWindows()
        {
            M00RollCallSequence sequence = Begin();
            view = new M00RollCallView(root.transform, sequence, null);

            view.GetChoiceButton(2).onClick.Invoke();
            Assert.That(view.IsWaitingForNextCall, Is.True);
            Assert.That(view.AdvanceAfterUnansweredCall(), Is.False);
            Assert.That(view.CallsMade, Is.EqualTo(2));
            Assert.That(ActiveChoices(), Is.Zero);
            Assert.That(view.CurrentBeat.SpeakerLine, Does.Contain("call 2"));

            Assert.That(view.AdvanceAfterUnansweredCall(), Is.False);
            Assert.That(view.CallsMade, Is.EqualTo(3));
            Assert.That(view.CurrentBeat.Finished, Is.True);
            Assert.That(view.CurrentBeat.IdentityRevealValue, Is.EqualTo("离开人群"));
            Assert.That(view.CurrentBeat.SpeakerLine,
                Does.Contain(SoundwellUiLocalization.Language == SoundwellUiLocalization.Chinese
                    ? "离开的方向" : "where you left"));
            Assert.That(view.ContinueButton.gameObject.activeSelf, Is.True);
        }

        [Test]
        public void ThreeSilentCallsFindThePlayerStillInTheCrowd()
        {
            M00RollCallSequence sequence = Begin();
            view = new M00RollCallView(root.transform, sequence, null);

            view.GetChoiceButton(1).onClick.Invoke();
            Assert.That(view.AdvanceAfterUnansweredCall(), Is.True);
            view.GetChoiceButton(1).onClick.Invoke();
            Assert.That(view.AdvanceAfterUnansweredCall(), Is.True);
            view.GetChoiceButton(1).onClick.Invoke();

            Assert.That(sequence.Outcome, Is.EqualTo(M00RollCallOutcome.Silent));
            Assert.That(view.CurrentBeat.IdentityRevealValue, Is.EqualTo("沉默"));
            Assert.That(view.CurrentBeat.SpeakerLine,
                Does.Contain(SoundwellUiLocalization.Language == SoundwellUiLocalization.Chinese
                    ? "人群里找到你" : "finds you in the crowd"));
            Assert.That(ActiveChoices(), Is.Zero);
        }

        [Test]
        public void LiveLocaleChangeUpdatesEveryRollCallLabelWithoutResettingTheResponse()
        {
            string originalLanguage = SoundwellUiLocalization.Language;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                M00RollCallSequence sequence = Begin();
                view = new M00RollCallView(root.transform, sequence, null);
                Text heading = Array.Find(root.GetComponentsInChildren<Text>(true),
                    label => label.text == "THE TWELFTH-YEAR ROLL CALL");
                Assert.That(heading, Is.Not.Null);
                Font englishFont = heading.font;
                Assert.That(Label(view.ContinueButton), Is.EqualTo("Continue"));

                view.GetChoiceButton(1).onClick.Invoke();
                Assert.That(view.IsWaitingForNextCall, Is.True);
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);

                // No explicit Refresh call: the real locale-change event must
                // update labels even while the name is between attempts.
                Assert.That(heading.text, Is.EqualTo("第十二年的点名"));
                Assert.That(Label(view.ContinueButton), Is.EqualTo("继续"));
                Assert.That(view.SpeakerText.text, Is.EqualTo(view.CurrentBeat.SpeakerLine));
                Assert.That(view.GuidanceText.text, Is.EqualTo(view.CurrentBeat.Guidance));
                Assert.That(heading.font, Is.SameAs(view.SpeakerText.font));
                Assert.That(sequence.CallsMade, Is.EqualTo(1));
                Assert.That(sequence.Outcome, Is.EqualTo(M00RollCallOutcome.Pending));

                Assert.That(view.AdvanceAfterUnansweredCall(), Is.True);
                Assert.That(Label(view.GetChoiceButton(0)),
                    Is.EqualTo(view.CurrentBeat.Choices[0]));
                Assert.That(view.GetChoiceButton(0).GetComponentInChildren<Text>(true).font,
                    Is.SameAs(view.SpeakerText.font));
                view.GetChoiceButton(0).onClick.Invoke();
                Assert.That(view.ContinueButton.gameObject.activeSelf, Is.True);

                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                Assert.That(heading.text, Is.EqualTo("THE TWELFTH-YEAR ROLL CALL"));
                Assert.That(Label(view.ContinueButton), Is.EqualTo("Continue"));
                Assert.That(heading.font, Is.SameAs(englishFont),
                    "Switching back to EN must restore the Latin face, not keep the CJK font.");
                Assert.That(sequence.Outcome, Is.EqualTo(M00RollCallOutcome.AnsweredSecond));
                Assert.That(sequence.CallsMade, Is.EqualTo(2));
            }
            finally
            {
                SoundwellUiLocalization.SetLanguage(originalLanguage);
            }
        }

        [Test]
        public void ViewFitsPortraitAndUltrawideWithoutChangingRollCallState()
        {
            M00RollCallSequence sequence = Begin();
            view = new M00RollCallView(root.transform, sequence, null);

            view.LayoutForViewportAspect(9f / 16f);
            float portraitScale = view.SpeakerText.rectTransform.parent.localScale.x;
            view.LayoutForViewportAspect(32f / 9f);
            float ultrawideScale = view.SpeakerText.rectTransform.parent.localScale.x;

            Assert.That(portraitScale, Is.GreaterThan(0f).And.LessThanOrEqualTo(1f));
            Assert.That(ultrawideScale, Is.GreaterThan(0f).And.LessThanOrEqualTo(1f));
            Assert.That(sequence.CallsMade, Is.EqualTo(1));
            Assert.That(sequence.IsAwaitingResponse, Is.True);
        }

        private M00RollCallSequence Begin()
        {
            root = new GameObject("M00 roll call view regression root");
            var sequence = new M00RollCallSequence();
            sequence.Begin();
            return sequence;
        }

        private int ActiveChoices()
        {
            int count = 0;
            for (int i = 0; i < 3; i++)
                if (view.GetChoiceButton(i).gameObject.activeSelf)
                    count++;
            return count;
        }

        private static string Label(Button button) =>
            button.GetComponentInChildren<Text>(true).text;
    }
}
