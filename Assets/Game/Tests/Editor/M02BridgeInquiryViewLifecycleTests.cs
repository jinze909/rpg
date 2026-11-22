using System;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class M02BridgeInquiryViewLifecycleTests
    {
        private GameObject root;
        private M02BridgeInquiryView view;
        private string previousLanguage;

        [SetUp]
        public void SetUp()
        {
            previousLanguage = SoundwellUiLocalization.Language;
            SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
            root = new GameObject("M02 bridge inquiry lifecycle root");
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
            var inquiry = new M02BridgeInquiry();
            inquiry.Begin();
            view = new M02BridgeInquiryView(root.transform, inquiry, _ => { });
            Assert.That(view.IsDisposed, Is.False);

            UnityEngine.Object.DestroyImmediate(root);
            root = null;
            Assert.That(view.IsDisposed, Is.True);
            Assert.That(view.PreferredFocusTarget, Is.Null);

            Assert.DoesNotThrow(() => view.Dispose());
            Assert.That(view.IsDisposed, Is.True);
        }

        [Test]
        public void DestroyedCanvasBlocksExamineAndArchiveAdvanceWithoutMutatingInquiryOrThrowing()
        {
            var inquiry = new M02BridgeInquiry();
            inquiry.Begin();
            int observedCalls = 0;
            int completedCalls = 0;
            view = new M02BridgeInquiryView(root.transform, inquiry,
                _ => completedCalls++, _ => observedCalls++);
            view.ContinueButton.onClick.Invoke();
            Assert.That(inquiry.Location, Is.EqualTo(M02EvidenceLocation.HarborArchive));

            UnityEngine.Object.DestroyImmediate(root);
            root = null;

            Assert.DoesNotThrow(() => view.GetEvidenceButton(0).onClick.Invoke());
            Assert.DoesNotThrow(() => view.ContinueButton.onClick.Invoke());
            Assert.That(view.IsDisposed, Is.True);
            Assert.That(inquiry.Location, Is.EqualTo(M02EvidenceLocation.HarborArchive));
            Assert.That(inquiry.Found, Is.EqualTo(M02BridgeEvidence.None));
            Assert.That(observedCalls, Is.Zero);
            Assert.That(completedCalls, Is.Zero);
        }

        [Test]
        public void FailedHostCallbackThatDisposesViewDoesNotReopenOrThrowSecondaryError()
        {
            var inquiry = new M02BridgeInquiry();
            inquiry.Begin();
            view = new M02BridgeInquiryView(root.transform, inquiry, _ =>
            {
                view.Dispose();
                throw new InvalidOperationException("Host tore down bridge inquiry view and failed");
            });
            view.ContinueButton.onClick.Invoke();
            Assert.That(inquiry.Location, Is.EqualTo(M02EvidenceLocation.HarborArchive));

            Assert.Throws<InvalidOperationException>(() => view.ContinueButton.onClick.Invoke());
            Assert.That(view.IsDisposed, Is.True);
            Assert.That(inquiry.Location, Is.EqualTo(M02EvidenceLocation.HarborArchive));
        }

        [Test]
        public void LayoutForViewportAspectIgnoresNonPositiveAndNonFiniteAspects()
        {
            var inquiry = new M02BridgeInquiry();
            inquiry.Begin();
            view = new M02BridgeInquiryView(root.transform, inquiry, _ => { });

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
    }
}
