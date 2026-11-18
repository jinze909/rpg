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
            Assert.That(view.CurrentBeat.SpeakerLine, Does.Contain("第二遍"));
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
            Assert.That(view.CurrentBeat.SpeakerLine, Does.Contain("离开的方向"));
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
            Assert.That(view.CurrentBeat.SpeakerLine, Does.Contain("人群"));
            Assert.That(ActiveChoices(), Is.Zero);
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
