using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class M02MissingReturnViewTests
    {
        private GameObject root;
        private M02MissingReturnView view;
        private string previousLanguage;

        [SetUp]
        public void SetUp()
        {
            previousLanguage = SoundwellUiLocalization.Language;
            SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
            root = new GameObject("M02 review test root");
        }

        [TearDown]
        public void TearDown()
        {
            view?.Dispose();
            if (root != null)
                Object.DestroyImmediate(root);
            SoundwellUiLocalization.SetLanguage(previousLanguage);
        }

        [Test]
        public void StampRequiresActualPlayerClicksAndOutcomeIsDeliveredOnce()
        {
            int count = 0;
            M02MissingReturnOutcome delivered = null;
            view = new M02MissingReturnView(root.transform,
                M01PensionChoice.RequestMissingReturnReview,
                M02BridgeEvidence.WarReportDraft, false, outcome =>
                {
                    count++;
                    delivered = outcome;
                });
            Assert.That(view.Review.Approved, Is.False);
            Assert.That(view.StampButton.gameObject.activeSelf, Is.False);
            view.StampButton.onClick.Invoke();
            Assert.That(view.Review.Approved, Is.False);
            view.RequestButton.onClick.Invoke();
            Assert.That(view.StampButton.gameObject.activeSelf, Is.True);
            Assert.That(view.Review.Approved, Is.False);
            view.StampButton.onClick.Invoke();
            Assert.That(view.Review.Approved, Is.True);
            view.LeaveButton.onClick.Invoke();
            view.LeaveButton.onClick.Invoke();
            Assert.That(count, Is.EqualTo(1));
            Assert.That(delivered.Approved, Is.True);
        }

        [Test]
        public void WithoutEvidencePlayerCanRequestButNeverGetStamped()
        {
            M02MissingReturnOutcome received = null;
            view = new M02MissingReturnView(root.transform,
                M01PensionChoice.LeaveWithoutDecision,
                M02BridgeEvidence.ThirdBellSignal, false, result => received = result);
            view.RequestButton.onClick.Invoke();
            SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
            Assert.That(view.Narrative.text, Does.Contain("仍未获准"));
            Assert.That(view.StampButton.gameObject.activeSelf, Is.False);
            view.LayoutForViewportAspect(9f / 16f);
            view.LayoutForViewportAspect(32f / 9f);
            view.LeaveButton.onClick.Invoke();
            Assert.That(received.Requested, Is.True);
            Assert.That(received.Approved, Is.False);
        }

        [Test]
        public void DisposalDropsUnfinishedReviewAndDetachesLocale()
        {
            int calls = 0;
            view = new M02MissingReturnView(root.transform,
                M01PensionChoice.KeepIdentityUnregistered,
                M02BridgeEvidence.WarReportDraft, true, _ => calls++);
            Assert.That(view.RequestButton.gameObject.activeSelf, Is.False);
            view.Dispose();
            SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
            Assert.That(calls, Is.Zero);
        }
    }
}
