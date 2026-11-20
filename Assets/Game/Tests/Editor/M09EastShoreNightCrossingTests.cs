using System;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class M09EastShoreNightCrossingTests
    {
        [Test]
        public void PatrolMustBeHeardAndPassedBeforeJettyApproach()
        {
            var session = new M09EastShoreNightCrossingSession(5);
            Assert.That(session.TryHoldSilent(), Is.False);
            Assert.That(session.TryRowToJetty(), Is.False);
            Assert.That(session.PatrolAlerts, Is.EqualTo(1));
            Assert.That(session.Stage, Is.EqualTo(M09NightCrossingStage.PatrolAlerted));
            Assert.That(session.TryRecordRuiyaConsent(true, 2), Is.False);
            Assert.That(session.TryHideAndRelisten(), Is.True);
            Assert.That(session.TryListenForOars(), Is.True);
            Assert.That(session.TryRowToJetty(), Is.False);
            Assert.That(session.PatrolAlerts, Is.EqualTo(2));
            Assert.That(session.TryHideAndRelisten(), Is.True);
            Assert.That(session.TryListenForOars(), Is.True);
            Assert.That(session.TryHoldSilent(), Is.True);
            Assert.That(session.TryRowToJetty(), Is.True);
            Assert.That(session.Stage, Is.EqualTo(M09NightCrossingStage.JettyRollCall));
            Assert.That(session.Outcome, Is.Null);
        }

        [Test]
        public void ConsentAloneNeverRecordsBoardingAndExactObservedCountIsRequired()
        {
            var session = AtJetty(3);
            Assert.That(session.TryRecordRuiyaConsent(true, 2), Is.True);
            Assert.That(session.Stage, Is.EqualTo(M09NightCrossingStage.AwaitingActualBoarding));
            Assert.That(session.Outcome, Is.Null);
            Assert.That(session.ActuallyBoardedCount, Is.Zero);
            Assert.That(session.TryConfirmActualBoarding(1), Is.False);
            Assert.That(session.TryConfirmActualBoarding(4), Is.False);
            Assert.That(session.TryConfirmActualBoarding(2), Is.True);
            Assert.That(session.ActuallyBoardedCount, Is.EqualTo(2));
            Assert.That(session.Outcome, Is.EqualTo(M09RuiyaOutcome.Rescued));
            Assert.That(session.IsComplete, Is.True);
            Assert.That(session.TryConfirmActualBoarding(2), Is.False);
            Assert.That(session.TryDeferForUnmetTransfer(), Is.False);
        }

        [Test]
        public void NoBerthsDeclinedConsentAndInterruptedTransferCannotRescue()
        {
            var insufficient = AtJetty(1);
            Assert.That(insufficient.TryRecordRuiyaConsent(true, 3), Is.True);
            Assert.That(insufficient.Outcome, Is.EqualTo(M09RuiyaOutcome.Deferred));
            Assert.That(insufficient.ActuallyBoardedCount, Is.Zero);

            var noBoat = AtJetty(0);
            Assert.That(noBoat.TryRecordRuiyaConsent(true, 1), Is.True);
            Assert.That(noBoat.Outcome, Is.EqualTo(M09RuiyaOutcome.Deferred));

            var refused = AtJetty(10);
            Assert.That(refused.TryRecordRuiyaConsent(false, 2), Is.True);
            Assert.That(refused.Outcome, Is.EqualTo(M09RuiyaOutcome.NotRescued));
            Assert.That(refused.ActuallyBoardedCount, Is.Zero);

            var noEvidence = AtJetty(10);
            Assert.That(noEvidence.TryRecordRuiyaConsent(true, 0), Is.False);
            Assert.That(noEvidence.TryRecordRuiyaConsent(true, -2), Is.False);
            Assert.That(noEvidence.TryRecordRuiyaConsent(true, 2), Is.True);
            Assert.That(noEvidence.TryDeferForUnmetTransfer(), Is.True);
            Assert.That(noEvidence.Outcome, Is.EqualTo(M09RuiyaOutcome.Deferred));
            Assert.That(noEvidence.ActuallyBoardedCount, Is.Zero);
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new M09EastShoreNightCrossingSession(-1));
        }

        [Test]
        public void UiCannotInventBoardingAndRetriesFailedOutcomeCallback()
        {
            var root = new GameObject("M09 night ferry view test");
            var session = new M09EastShoreNightCrossingSession(2);
            M09EastShoreNightCrossingView view = null;
            int checks = 0, delivered = 0;
            bool boardingObserved = false, failOnce = true;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                view = new M09EastShoreNightCrossingView(root.transform, session, 2,
                    count => { checks++; Assert.That(count, Is.EqualTo(2)); return boardingObserved; },
                    final =>
                    {
                        delivered++;
                        Assert.That(final.Outcome, Is.EqualTo(M09RuiyaOutcome.Rescued));
                        if (failOnce)
                        {
                            failOnce = false;
                            throw new InvalidOperationException("Interrupted authoritative save");
                        }
                    });
                Assert.That(view.FinishButton.interactable, Is.False);
                Assert.That(root.transform.childCount, Is.EqualTo(1));
                view.ListenOrProceedButton.onClick.Invoke();
                view.ListenOrProceedButton.onClick.Invoke();
                view.ListenOrProceedButton.onClick.Invoke();
                Assert.That(session.Stage, Is.EqualTo(M09NightCrossingStage.JettyRollCall));
                view.ListenOrProceedButton.onClick.Invoke();
                Assert.That(session.Stage, Is.EqualTo(M09NightCrossingStage.AwaitingActualBoarding));
                view.ListenOrProceedButton.onClick.Invoke();
                Assert.That(checks, Is.EqualTo(1));
                Assert.That(session.Outcome, Is.Null);
                boardingObserved = true;
                view.ListenOrProceedButton.onClick.Invoke();
                Assert.That(session.Outcome, Is.EqualTo(M09RuiyaOutcome.Rescued));
                Assert.Throws<InvalidOperationException>(() =>
                    view.FinishButton.onClick.Invoke());
                Assert.That(view.FinishButton.interactable, Is.True);
                view.FinishButton.onClick.Invoke();
                Assert.That(delivered, Is.EqualTo(2));
                Assert.That(view.FinishButton.interactable, Is.False);
                view.FinishButton.onClick.Invoke();
                Assert.That(delivered, Is.EqualTo(2));
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
                view.Refresh();
                Assert.That(session.ActuallyBoardedCount, Is.EqualTo(2));
            }
            finally
            {
                view?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
            }
        }

        [Test]
        public void HiddenUiControlsCannotSkipPatrolOrOverrideRefusal()
        {
            var root = new GameObject("M09 hidden button tests");
            var session = new M09EastShoreNightCrossingSession(4);
            M09EastShoreNightCrossingView view = null;
            try
            {
                view = new M09EastShoreNightCrossingView(root.transform, session, 2,
                    _ => { Assert.Fail("No verified boarding can occur"); return false; },
                    _ => { });
                view.DeferButton.onClick.Invoke();
                Assert.That(session.Stage, Is.EqualTo(M09NightCrossingStage.FogApproach));
                view.ListenOrProceedButton.onClick.Invoke();
                view.ListenOrProceedButton.onClick.Invoke();
                view.ListenOrProceedButton.onClick.Invoke();
                Assert.That(session.Stage, Is.EqualTo(M09NightCrossingStage.JettyRollCall));
                view.RiskOrRefuseButton.onClick.Invoke();
                Assert.That(session.Outcome, Is.EqualTo(M09RuiyaOutcome.NotRescued));
                view.ListenOrProceedButton.onClick.Invoke();
                Assert.That(session.ActuallyBoardedCount, Is.Zero);
            }
            finally
            {
                view?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static M09EastShoreNightCrossingSession AtJetty(int berths)
        {
            var session = new M09EastShoreNightCrossingSession(berths);
            Assert.That(session.TryListenForOars(), Is.True);
            Assert.That(session.TryHoldSilent(), Is.True);
            Assert.That(session.TryRowToJetty(), Is.True);
            return session;
        }
    }
}
