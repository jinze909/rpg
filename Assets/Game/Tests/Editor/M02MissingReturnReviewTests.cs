using System;
using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    public sealed class M02MissingReturnReviewTests
    {
        [Test]
        public void M01RequestAndWarehouseCluesCannotAutomaticallyApprove()
        {
            var review = new M02MissingReturnReview(
                M01PensionChoice.RequestMissingReturnReview,
                M02BridgeEvidence.CleanCableCut | M02BridgeEvidence.FerrymanLog |
                M02BridgeEvidence.ThirdBellSignal, false);
            Assert.That(review.Requested, Is.False);
            Assert.That(review.Approved, Is.False);
            review.Begin();
            Assert.That(review.CanStamp, Is.False);
            review.Request();
            Assert.That(review.CanStamp, Is.False);
            Assert.Throws<InvalidOperationException>(() => review.Stamp());
            var result = review.Leave();
            Assert.That(result.Requested, Is.True);
            Assert.That(result.Approved, Is.False);
            Assert.That(result.EffectivePensionPath(
                M01PensionChoice.RequestMissingReturnReview), Is.EqualTo("未处理"));
        }

        [Test]
        public void ReportCopyQualifiesOnlyAfterExplicitRequestAndStamp()
        {
            var review = new M02MissingReturnReview(
                M01PensionChoice.RequestMissingReturnReview,
                M02BridgeEvidence.WarReportDraft, false);
            review.Begin();
            Assert.Throws<InvalidOperationException>(() => review.Stamp());
            review.Request();
            Assert.That(review.CanStamp, Is.True);
            review.Stamp();
            Assert.That(review.Approved, Is.True);
            Assert.That(review.CanStamp, Is.False);
            Assert.That(M02MissingReturnCopy.Description(review, true),
                Does.Contain("给你备船").And.Contain("旧账仍挂在港务署"));
            Assert.That(M02MissingReturnCopy.Description(review, false),
                Does.Contain("official dock").And.Contain("stays on the harbor ledger"));
            var result = review.Leave();
            var camp = new M03LuJunEncounter(M02BridgeEvidence.WarReportDraft,
                M02OfficialDockFacts.IsEffective(false, result.Approved));
            camp.Begin();
            camp.ObserveRollCall();
            camp.Ask(M03LuJunOpening.NeedAShip);
            Assert.That(camp.CanChoose(M03VoyageChoice.HanQiaoOfficial), Is.True);
            Assert.That(camp.CanChoose(M03VoyageChoice.MixedWithOfficial), Is.True);
            Assert.That(camp.Voyage, Is.Null);
            Assert.That(result.EffectivePensionPath(
                M01PensionChoice.RequestMissingReturnReview),
                Is.EqualTo("改登记为失踪归还"));
        }

        [Test]
        public void TransferAssistanceIsIndependentAlternateQualification()
        {
            var review = new M02MissingReturnReview(
                M01PensionChoice.LeaveWithoutDecision, M02BridgeEvidence.None, true);
            review.Begin();
            review.Request();
            review.Stamp();
            Assert.That(review.Leave().Approved, Is.True);
        }

        [Test]
        public void ConcealmentOrPublicRegistrationIsNotSilentlyRetconned()
        {
            foreach (M01PensionChoice choice in new[]
            {
                M01PensionChoice.RegisterLivingIdentity,
                M01PensionChoice.KeepIdentityUnregistered
            })
            {
                var review = new M02MissingReturnReview(choice,
                    M02BridgeEvidence.WarReportDraft, true);
                review.Begin();
                Assert.That(review.CanRequest, Is.False);
                Assert.Throws<InvalidOperationException>(() => review.Request());
                Assert.Throws<InvalidOperationException>(() => review.Stamp());
                Assert.That(review.Leave().EffectivePensionPath(choice),
                    Is.EqualTo(M01HomecomingResponse.PensionPathFor(choice)));
            }
        }

        [Test]
        public void UnseenEvidenceAndInvalidPhasesCannotGenerateAnApproval()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new M02MissingReturnReview(M01PensionChoice.LeaveWithoutDecision,
                    (M02BridgeEvidence)128, false));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new M02MissingReturnReview((M01PensionChoice)99,
                    M02BridgeEvidence.None, false));
            var review = new M02MissingReturnReview(
                M01PensionChoice.LeaveWithoutDecision, M02BridgeEvidence.None, false);
            Assert.Throws<InvalidOperationException>(() => review.Request());
            Assert.Throws<InvalidOperationException>(() => review.Leave());
            review.Begin();
            Assert.Throws<InvalidOperationException>(() => review.Begin());
            review.Leave();
            Assert.Throws<InvalidOperationException>(() => review.Request());
            Assert.Throws<InvalidOperationException>(() => review.Stamp());
        }

        [Test]
        public void ApprovedFactWriterRejectsUnstampedOutcome()
        {
            var review = new M02MissingReturnReview(
                M01PensionChoice.RequestMissingReturnReview,
                M02BridgeEvidence.None, false);
            review.Begin();
            var outcome = review.Leave();
            Assert.Throws<InvalidOperationException>(() =>
                M02MissingReturnFacts.TryRecordApproved(outcome));
            Assert.Throws<ArgumentNullException>(() =>
                M02MissingReturnFacts.TryRecordApproved(null));
        }
    }
}
