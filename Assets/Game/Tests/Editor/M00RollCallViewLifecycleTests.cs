using System;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class M00RollCallViewLifecycleTests
    {
        private GameObject root;
        private M00RollCallView view;
        private string previousLanguage;

        [SetUp]
        public void SetUp()
        {
            previousLanguage = SoundwellUiLocalization.Language;
            SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
            root = new GameObject("M00 roll call lifecycle root");
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
            M00RollCallSequence sequence = Begun();
            view = new M00RollCallView(root.transform, sequence, _ => { });
            Assert.That(view.IsDisposed, Is.False);

            UnityEngine.Object.DestroyImmediate(root);
            root = null;
            Assert.That(view.IsDisposed, Is.True);

            Assert.DoesNotThrow(() => view.Dispose());
            Assert.That(view.IsDisposed, Is.True);
        }

        [Test]
        public void DestroyedCanvasBlocksChooseAndUnansweredAdvanceWithoutMutatingSequence()
        {
            M00RollCallSequence sequence = Begun();
            view = new M00RollCallView(root.transform, sequence, _ => { });
            view.GetChoiceButton(1).onClick.Invoke();
            Assert.That(view.IsWaitingForNextCall, Is.True);
            Assert.That(sequence.CallsMade, Is.EqualTo(1));

            UnityEngine.Object.DestroyImmediate(root);
            root = null;

            Assert.DoesNotThrow(() => view.GetChoiceButton(0).onClick.Invoke());
            Assert.That(view.IsDisposed, Is.True);
            Assert.That(sequence.CallsMade, Is.EqualTo(1));
            Assert.That(sequence.Outcome, Is.EqualTo(M00RollCallOutcome.Pending));

            // Fresh view destroyed while waiting for next call: AdvanceAfterUnansweredCall must dispose safely.
            root = new GameObject("M00 roll call advance root");
            M00RollCallSequence second = Begun();
            view = new M00RollCallView(root.transform, second, _ => { });
            view.GetChoiceButton(1).onClick.Invoke();
            UnityEngine.Object.DestroyImmediate(root);
            root = null;

            Assert.That(view.AdvanceAfterUnansweredCall(), Is.False);
            Assert.That(view.IsDisposed, Is.True);
            Assert.That(second.CallsMade, Is.EqualTo(1));
        }

        [Test]
        public void DestroyedCanvasBlocksContinueWithoutFiringHostCallback()
        {
            M00RollCallSequence sequence = Begun();
            int continuedCalls = 0;
            view = new M00RollCallView(root.transform, sequence, _ => continuedCalls++);
            view.GetChoiceButton(0).onClick.Invoke();
            Assert.That(sequence.IsComplete, Is.True);

            UnityEngine.Object.DestroyImmediate(root);
            root = null;

            Assert.DoesNotThrow(() => view.ContinueButton.onClick.Invoke());
            Assert.That(view.IsDisposed, Is.True);
            Assert.That(continuedCalls, Is.Zero);
        }

        [Test]
        public void FailedHostCallbackThatDisposesViewDoesNotReopenOrThrowSecondaryError()
        {
            M00RollCallSequence sequence = Begun();
            view = new M00RollCallView(root.transform, sequence, _ =>
            {
                view.Dispose();
                throw new InvalidOperationException("Host tore down roll call view and failed");
            });
            view.GetChoiceButton(0).onClick.Invoke();

            Assert.Throws<InvalidOperationException>(() => view.ContinueButton.onClick.Invoke());
            Assert.That(view.IsDisposed, Is.True);
        }

        [Test]
        public void LayoutForViewportAspectIgnoresNonPositiveAndNonFiniteAspects()
        {
            M00RollCallSequence sequence = Begun();
            view = new M00RollCallView(root.transform, sequence, _ => { });

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

        private static M00RollCallSequence Begun()
        {
            var sequence = new M00RollCallSequence();
            sequence.Begin();
            return sequence;
        }
    }
}
