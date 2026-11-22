using System;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class M00HanQiaoMeetingViewLifecycleTests
    {
        private GameObject root;
        private M00HanQiaoMeetingView view;
        private string previousLanguage;

        [SetUp]
        public void SetUp()
        {
            previousLanguage = SoundwellUiLocalization.Language;
            SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
            root = new GameObject("M00 Han Qiao meeting lifecycle root");
        }

        [TearDown]
        public void TearDown()
        {
            view?.Dispose();
            if (root != null)
                UnityEngine.Object.DestroyImmediate(root);
            SoundwellUiLocalization.SetLanguage(previousLanguage);
        }

        [Test]
        public void IsDisposedTracksExplicitDisposeAndExternalCanvasDestruction()
        {
            M00HanQiaoMeetingSequence sequence = Begun();
            view = new M00HanQiaoMeetingView(root.transform, sequence, _ => { });
            Assert.That(view.IsDisposed, Is.False);

            UnityEngine.Object.DestroyImmediate(root);
            root = null;
            Assert.That(view.IsDisposed, Is.True);

            Assert.DoesNotThrow(() => view.Dispose());
            Assert.That(view.IsDisposed, Is.True);
        }

        [Test]
        public void DestroyedCanvasBlocksChooseWithoutMutatingSequenceOrFiringRansomCallback()
        {
            M00HanQiaoMeetingSequence sequence = Begun();
            int ransomCalls = 0;
            view = new M00HanQiaoMeetingView(root.transform, sequence, _ => { }, () => ransomCalls++);

            UnityEngine.Object.DestroyImmediate(root);
            root = null;

            Assert.DoesNotThrow(() => view.GetChoiceButton(0).onClick.Invoke());
            Assert.That(view.IsDisposed, Is.True);
            Assert.That(sequence.Stage, Is.EqualTo(M00HanQiaoMeetingStage.AwaitingOpeningChoice));
            Assert.That(sequence.OpeningChoice, Is.Null);
            Assert.That(ransomCalls, Is.Zero);
        }

        [Test]
        public void DestroyedCanvasBlocksClosingContinueWithoutCompletingOrFiringHostCallback()
        {
            M00HanQiaoMeetingSequence sequence = Begun();
            int finishedCalls = 0;
            view = new M00HanQiaoMeetingView(root.transform, sequence, _ => finishedCalls++);
            view.GetChoiceButton(2).onClick.Invoke();
            view.ContinueButton.onClick.Invoke();
            view.GetChoiceButton(0).onClick.Invoke();
            Assert.That(sequence.Stage, Is.EqualTo(M00HanQiaoMeetingStage.Closing));

            UnityEngine.Object.DestroyImmediate(root);
            root = null;

            Assert.DoesNotThrow(() => view.ContinueButton.onClick.Invoke());
            Assert.That(view.IsDisposed, Is.True);
            Assert.That(sequence.Stage, Is.EqualTo(M00HanQiaoMeetingStage.Closing));
            Assert.That(sequence.IsComplete, Is.False);
            Assert.That(finishedCalls, Is.Zero);
        }

        [Test]
        public void FailedHostCallbackThatDisposesViewDoesNotReopenOrThrowSecondaryError()
        {
            M00HanQiaoMeetingSequence sequence = Begun();
            view = new M00HanQiaoMeetingView(root.transform, sequence, _ =>
            {
                view.Dispose();
                throw new InvalidOperationException("Host tore down meeting view and failed");
            });
            view.GetChoiceButton(2).onClick.Invoke();
            view.ContinueButton.onClick.Invoke();
            view.GetChoiceButton(0).onClick.Invoke();
            Assert.That(sequence.Stage, Is.EqualTo(M00HanQiaoMeetingStage.Closing));

            Assert.Throws<InvalidOperationException>(() => view.ContinueButton.onClick.Invoke());
            Assert.That(view.IsDisposed, Is.True);
            Assert.That(sequence.Stage, Is.EqualTo(M00HanQiaoMeetingStage.Closing));
        }

        [Test]
        public void LayoutForViewportAspectIgnoresNonPositiveAndNonFiniteAspects()
        {
            M00HanQiaoMeetingSequence sequence = Begun();
            view = new M00HanQiaoMeetingView(root.transform, sequence, _ => { });

            view.LayoutForViewportAspect(16f / 9f);
            Vector3 expectedScale = view.ContinueButton.transform.parent.localScale;

            view.LayoutForViewportAspect(0f);
            view.LayoutForViewportAspect(-1f);
            view.LayoutForViewportAspect(float.NaN);
            view.LayoutForViewportAspect(float.PositiveInfinity);
            view.LayoutForViewportAspect(float.NegativeInfinity);

            Assert.That(view.ContinueButton.transform.parent.localScale, Is.EqualTo(expectedScale));
            Assert.That(view.IsDisposed, Is.False);
        }

        private static M00HanQiaoMeetingSequence Begun()
        {
            var sequence = new M00HanQiaoMeetingSequence();
            sequence.Begin();
            return sequence;
        }
    }
}
