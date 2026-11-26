using System;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class M01WestLaneViewLifecycleTests
    {
        private GameObject root;
        private M01WestLaneView view;
        private string previousLanguage;

        [SetUp]
        public void SetUp()
        {
            previousLanguage = SoundwellUiLocalization.Language;
            SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
            root = new GameObject("M01 West Lane lifecycle root");
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
            view = new M01WestLaneView(root.transform, M00RollCallOutcome.Silent, false, _ => { });
            Assert.That(view.IsDisposed, Is.False);

            UnityEngine.Object.DestroyImmediate(root);
            root = null;
            Assert.That(view.IsDisposed, Is.True);

            Assert.DoesNotThrow(() => view.Dispose());
            Assert.That(view.IsDisposed, Is.True);
        }

        [Test]
        public void DestroyedCanvasBlocksAdvanceAndChooseWithoutMutatingStageOrDecision()
        {
            int chosenCalls = 0;
            view = new M01WestLaneView(root.transform, M00RollCallOutcome.AnsweredSecond, true, _ => chosenCalls++);
            view.ProgressButton.onClick.Invoke();
            view.ProgressButton.onClick.Invoke();
            Assert.That(view.Stage, Is.EqualTo(M01WestLaneStage.ChoosingPension));

            UnityEngine.Object.DestroyImmediate(root);
            root = null;

            Assert.DoesNotThrow(() => view.GetChoiceButton(1).onClick.Invoke());
            Assert.DoesNotThrow(() => view.ProgressButton.onClick.Invoke());
            Assert.That(view.IsDisposed, Is.True);
            Assert.That(view.Stage, Is.EqualTo(M01WestLaneStage.ChoosingPension));
            Assert.That(view.Decision, Is.Null);
            Assert.That(chosenCalls, Is.Zero);
        }

        [Test]
        public void DestroyedCanvasBlocksConcludedHandoffWithoutFiringHostCallback()
        {
            int chosenCalls = 0;
            view = new M01WestLaneView(root.transform, M00RollCallOutcome.Silent, false, _ => chosenCalls++);
            view.ProgressButton.onClick.Invoke();
            view.ProgressButton.onClick.Invoke();
            view.GetChoiceButton(2).onClick.Invoke();
            Assert.That(view.Stage, Is.EqualTo(M01WestLaneStage.Concluded));

            UnityEngine.Object.DestroyImmediate(root);
            root = null;

            Assert.DoesNotThrow(() => view.ProgressButton.onClick.Invoke());
            Assert.That(view.IsDisposed, Is.True);
            Assert.That(chosenCalls, Is.Zero);
        }

        [Test]
        public void FailedHostCallbackThatDisposesViewDoesNotReopenOrThrowSecondaryError()
        {
            view = new M01WestLaneView(root.transform, M00RollCallOutcome.AnsweredFirst, false, _ =>
            {
                view.Dispose();
                throw new InvalidOperationException("Host tore down West Lane view and failed");
            });
            view.ProgressButton.onClick.Invoke();
            view.ProgressButton.onClick.Invoke();
            view.GetChoiceButton(0).onClick.Invoke();
            Assert.That(view.Stage, Is.EqualTo(M01WestLaneStage.Concluded));

            Assert.Throws<InvalidOperationException>(() => view.ProgressButton.onClick.Invoke());
            Assert.That(view.IsDisposed, Is.True);
            Assert.That(view.Stage, Is.EqualTo(M01WestLaneStage.Concluded));
        }

        [Test]
        public void LayoutForViewportAspectIgnoresNonPositiveAndNonFiniteAspects()
        {
            view = new M01WestLaneView(root.transform, M00RollCallOutcome.Silent, false, _ => { });

            view.LayoutForViewportAspect(16f / 9f);
            Vector3 expectedScale = view.ProgressButton.transform.parent.localScale;

            view.LayoutForViewportAspect(0f);
            view.LayoutForViewportAspect(-1f);
            view.LayoutForViewportAspect(float.NaN);
            view.LayoutForViewportAspect(float.PositiveInfinity);
            view.LayoutForViewportAspect(float.NegativeInfinity);

            Assert.That(view.ProgressButton.transform.parent.localScale, Is.EqualTo(expectedScale));
            Assert.That(view.IsDisposed, Is.False);
        }
    }
}
