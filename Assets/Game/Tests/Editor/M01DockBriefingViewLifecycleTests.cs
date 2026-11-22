using System;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class M01DockBriefingViewLifecycleTests
    {
        private GameObject root;
        private M01DockBriefingView view;
        private string previousLanguage;

        [SetUp]
        public void SetUp()
        {
            previousLanguage = SoundwellUiLocalization.Language;
            SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
            root = new GameObject("M01 dock briefing lifecycle root");
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
            view = new M01DockBriefingView(root.transform, () => { });
            Assert.That(view.IsDisposed, Is.False);

            UnityEngine.Object.DestroyImmediate(root);
            root = null;
            Assert.That(view.IsDisposed, Is.True);
            Assert.That(view.PreferredFocusTarget, Is.Null);

            Assert.DoesNotThrow(() => view.Dispose());
            Assert.That(view.IsDisposed, Is.True);
        }

        [Test]
        public void DestroyedCanvasBlocksChooseAndAdvanceWithoutMutatingBriefingOrThrowing()
        {
            int finishedCalls = 0;
            view = new M01DockBriefingView(root.transform, () => finishedCalls++);
            view.GetChoiceButton(1).onClick.Invoke();
            view.AdvanceButton.onClick.Invoke();
            Assert.That(view.Stage, Is.EqualTo(M01DockStage.ReviewingRoutes));
            Assert.That(view.InspectedLead, Is.Null);

            UnityEngine.Object.DestroyImmediate(root);
            root = null;

            Assert.DoesNotThrow(() => view.GetChoiceButton(0).onClick.Invoke());
            Assert.DoesNotThrow(() => view.AdvanceButton.onClick.Invoke());
            Assert.That(view.IsDisposed, Is.True);
            Assert.That(view.Stage, Is.EqualTo(M01DockStage.ReviewingRoutes));
            Assert.That(view.InspectedLead, Is.Null);
            Assert.That(finishedCalls, Is.Zero);
        }

        [Test]
        public void FailedHostCallbackThatDisposesViewDoesNotReopenOrThrowSecondaryError()
        {
            view = new M01DockBriefingView(root.transform, () =>
            {
                view.Dispose();
                throw new InvalidOperationException("Host tore down dock briefing view and failed");
            });
            view.GetChoiceButton(0).onClick.Invoke();
            view.AdvanceButton.onClick.Invoke();
            Assert.That(view.Stage, Is.EqualTo(M01DockStage.ReviewingRoutes));

            Assert.Throws<InvalidOperationException>(() => view.AdvanceButton.onClick.Invoke());
            Assert.That(view.IsDisposed, Is.True);
            Assert.That(view.Stage, Is.EqualTo(M01DockStage.ReviewingRoutes));
        }

        [Test]
        public void LayoutForViewportAspectIgnoresNonPositiveAndNonFiniteAspects()
        {
            view = new M01DockBriefingView(root.transform, () => { });

            view.LayoutForViewportAspect(16f / 9f);
            Vector3 expectedScale = view.AdvanceButton.transform.parent.localScale;

            view.LayoutForViewportAspect(0f);
            view.LayoutForViewportAspect(-1f);
            view.LayoutForViewportAspect(float.NaN);
            view.LayoutForViewportAspect(float.PositiveInfinity);
            view.LayoutForViewportAspect(float.NegativeInfinity);

            Assert.That(view.AdvanceButton.transform.parent.localScale, Is.EqualTo(expectedScale));
            Assert.That(view.IsDisposed, Is.False);
        }
    }
}
