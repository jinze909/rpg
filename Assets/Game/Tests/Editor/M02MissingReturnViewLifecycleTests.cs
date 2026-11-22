using System;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class M02MissingReturnViewLifecycleTests
    {
        private GameObject root;
        private M02MissingReturnView view;
        private string previousLanguage;

        [SetUp]
        public void SetUp()
        {
            previousLanguage = SoundwellUiLocalization.Language;
            SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
            root = new GameObject("M02 missing-return lifecycle root");
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
            view = new M02MissingReturnView(root.transform,
                M01PensionChoice.RequestMissingReturnReview,
                M02BridgeEvidence.WarReportDraft, false, _ => { });
            Assert.That(view.IsDisposed, Is.False);

            UnityEngine.Object.DestroyImmediate(root);
            root = null;
            Assert.That(view.IsDisposed, Is.True);

            Assert.DoesNotThrow(() => view.Dispose());
            Assert.That(view.IsDisposed, Is.True);
        }

        [Test]
        public void DestroyedCanvasBlocksAskStampAndLeaveWithoutMutatingReviewOrThrowing()
        {
            int finishedCalls = 0;
            view = new M02MissingReturnView(root.transform,
                M01PensionChoice.RequestMissingReturnReview,
                M02BridgeEvidence.WarReportDraft, false, _ => finishedCalls++);

            UnityEngine.Object.DestroyImmediate(root);
            root = null;

            Assert.DoesNotThrow(() => view.RequestButton.onClick.Invoke());
            Assert.DoesNotThrow(() => view.StampButton.onClick.Invoke());
            Assert.DoesNotThrow(() => view.LeaveButton.onClick.Invoke());
            Assert.That(view.IsDisposed, Is.True);
            Assert.That(view.Review.Requested, Is.False);
            Assert.That(view.Review.Approved, Is.False);
            Assert.That(view.Review.Stage, Is.EqualTo(M02ReviewStage.AtOffice));
            Assert.That(finishedCalls, Is.Zero);
        }

        [Test]
        public void FailedHostCallbackThatDisposesViewDoesNotReopenOrThrowSecondaryError()
        {
            view = new M02MissingReturnView(root.transform,
                M01PensionChoice.RequestMissingReturnReview,
                M02BridgeEvidence.WarReportDraft, false, _ =>
                {
                    view.Dispose();
                    throw new InvalidOperationException("Host tore down view and failed");
                });

            Assert.Throws<InvalidOperationException>(() => view.LeaveButton.onClick.Invoke());
            Assert.That(view.IsDisposed, Is.True);
            Assert.That(view.Review.Stage, Is.EqualTo(M02ReviewStage.AtOffice));
        }

        [Test]
        public void LayoutForViewportAspectIgnoresNonPositiveAndNonFiniteAspects()
        {
            view = new M02MissingReturnView(root.transform,
                M01PensionChoice.RequestMissingReturnReview,
                M02BridgeEvidence.WarReportDraft, false, _ => { });

            view.LayoutForViewportAspect(16f / 9f);
            Vector3 expectedScale = view.RequestButton.transform.parent.localScale;

            view.LayoutForViewportAspect(0f);
            view.LayoutForViewportAspect(-1f);
            view.LayoutForViewportAspect(float.NaN);
            view.LayoutForViewportAspect(float.PositiveInfinity);
            view.LayoutForViewportAspect(float.NegativeInfinity);

            Assert.That(view.RequestButton.transform.parent.localScale, Is.EqualTo(expectedScale));
            Assert.That(view.IsDisposed, Is.False);
        }
    }
}
