using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class M09EastShoreNightCrossingHostTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        private static void ReachJetty(M09EastShoreNightCrossingView view)
        {
            view.ListenOrProceedButton.onClick.Invoke();
            view.ListenOrProceedButton.onClick.Invoke();
            view.ListenOrProceedButton.onClick.Invoke();
        }

        private static void SeedActualLanding(Dictionary<string, int> values)
        {
            Func<string, int> read = Reader(values);
            Action<string, int> write = (key, value) => values[key] = value;
            var manifest = new M04DepartureManifest(
                M03VoyageChoice.QiQueMerchant, "Confirmed ferry hull", "A Yuan",
                4, "Shallow tender", true, 16, 8, "Named receiver", false, false);
            Assert.That(manifest.ConfirmDeparture(), Is.True);
            Assert.That(M04DepartureFacts.TryRecord(manifest, write), Is.True);
            Assert.That(M04DepartureFacts.TryMarkSailed(read, write), Is.True);
            var fog = new FogListeningSession(
                new[] { new FogListeningCue(FogBearing.Ahead, FogCueKind.ClearWater, 80) },
                maxListenFocus: 1, maxHullIntegrity: 2);
            fog.ApplyHelmAction(FogHelmAction.HoldCourse);
            Assert.That(FogListeningFacts.TryRecord(fog, write), Is.True);
            Assert.That(M05LandingFacts.TryRecord(
                new M05LandingReport(3, 8, true, true), read, write), Is.True);
        }

        [Test]
        public void PreviousVoyageCannotVerifyBoardingOrCommitIntoIdenticalNewLanding()
        {
            var root = new GameObject("M09 stale night ferry replay");
            var values = new Dictionary<string, int>();
            SeedActualLanding(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (key, value) => values[key] = value;
            int boardChecks = 0, committed = 0;
            M09EastShoreNightCrossingHost oldHost = null;
            M09EastShoreNightCrossingHost newHost = null;
            try
            {
                int oldArrival = read(M05LandingFacts.ArrivalSequenceKey);
                oldHost = new M09EastShoreNightCrossingHost(
                    root.transform, () => true, () => 4, () => 2,
                    _ => { boardChecks++; return true; },
                    _ => { committed++; return true; }, read);
                Assert.That(oldHost.TryOpen(), Is.True);
                var oldView = oldHost.ActiveView;
                ReachJetty(oldView);
                oldView.ListenOrProceedButton.onClick.Invoke();
                Assert.That(oldHost.Session.Stage,
                    Is.EqualTo(M09NightCrossingStage.AwaitingActualBoarding));

                Assert.That(ReturnOpeningContinuation.TryResetPreviousOutboundFacts(write),
                    Is.True);
                oldView.ListenOrProceedButton.onClick.Invoke();
                Assert.That(boardChecks, Is.Zero);

                SeedActualLanding(values);
                Assert.That(read(M05LandingFacts.ArrivalSequenceKey),
                    Is.GreaterThan(oldArrival));
                oldView.ListenOrProceedButton.onClick.Invoke();
                Assert.That(boardChecks, Is.Zero);
                Assert.That(oldHost.Session.Stage,
                    Is.EqualTo(M09NightCrossingStage.AwaitingActualBoarding));

                oldView.RiskOrRefuseButton.onClick.Invoke();
                Assert.That(oldHost.Session.IsComplete, Is.True);
                Assert.Throws<InvalidOperationException>(() =>
                    oldView.FinishButton.onClick.Invoke());
                Assert.That(committed, Is.Zero);
                Assert.That(oldHost.IsCommitted, Is.False);
                Assert.That(oldView.FinishButton.interactable, Is.True);

                oldHost.Dispose();
                oldHost = null;
                newHost = new M09EastShoreNightCrossingHost(
                    root.transform, () => true, () => 4, () => 2,
                    _ => { boardChecks++; return true; },
                    _ => { committed++; return true; }, read);
                Assert.That(newHost.TryOpen(), Is.True);
                ReachJetty(newHost.ActiveView);
                newHost.ActiveView.ListenOrProceedButton.onClick.Invoke();
                newHost.ActiveView.ListenOrProceedButton.onClick.Invoke();
                Assert.That(boardChecks, Is.EqualTo(1));
                newHost.ActiveView.FinishButton.onClick.Invoke();
                Assert.That(committed, Is.EqualTo(1));
                Assert.That(newHost.CommittedOutcome, Is.EqualTo(M09RuiyaOutcome.Rescued));
                Assert.That(newHost.CommittedBoardedCount, Is.EqualTo(2));
            }
            finally
            {
                oldHost?.Dispose();
                newHost?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void HostCannotOpenBeforeReadyWithInvalidCountsOrAlreadyConfirmedBerthTable()
        {
            var root = new GameObject("M09 night host prerequisites");
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            bool ready = false;
            int berths = 4;
            int household = 2;
            M09EastShoreNightCrossingHost host = null;
            try
            {
                host = new M09EastShoreNightCrossingHost(
                    root.transform, () => ready, () => berths, () => household,
                    _ => true, _ => true, read);
                Assert.That(host.TryOpen(), Is.False);

                ready = true;
                berths = -1;
                Assert.That(host.TryOpen(), Is.False);

                berths = 4;
                household = 0;
                Assert.That(host.TryOpen(), Is.False);

                household = 2;
                values[M09BerthTableFacts.ConfirmedKey] = 1;
                Assert.That(host.TryOpen(), Is.False);

                values[M09BerthTableFacts.ConfirmedKey] = 0;
                Assert.That(host.TryOpen(), Is.True);
                Assert.That(host.TryOpen(), Is.False);
                Assert.That(host.ActiveView, Is.Not.Null);
                Assert.That(host.IsCommitted, Is.False);
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void VerifiedBoardingCommitsRescuedOutcomeOnceAndClosesView()
        {
            var root = new GameObject("M09 night host rescued");
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            int commits = 0;
            M09EastShoreNightCrossingHost host = null;
            try
            {
                host = new M09EastShoreNightCrossingHost(
                    root.transform, () => true, () => 3, () => 2,
                    count => count == 2,
                    completed =>
                    {
                        commits++;
                        return completed.Outcome == M09RuiyaOutcome.Rescued;
                    },
                    read);
                Assert.That(host.TryOpen(), Is.True);
                var view = host.ActiveView;
                ReachJetty(view);
                view.ListenOrProceedButton.onClick.Invoke();
                view.ListenOrProceedButton.onClick.Invoke();
                view.FinishButton.onClick.Invoke();

                Assert.That(commits, Is.EqualTo(1));
                Assert.That(host.IsCommitted, Is.True);
                Assert.That(host.CommittedOutcome, Is.EqualTo(M09RuiyaOutcome.Rescued));
                Assert.That(host.CommittedBoardedCount, Is.EqualTo(2));
                Assert.That(host.ActiveView, Is.Null);
                Assert.That(host.TryOpen(), Is.False);
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ChangedLiveBerthsOrHouseholdCannotBoardOrCommitFromStaleDialog()
        {
            var root = new GameObject("M09 live roll call and berth drift");
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            int berths = 4, household = 2;
            int boardChecks = 0, commits = 0;
            M09EastShoreNightCrossingHost host = null;
            try
            {
                host = new M09EastShoreNightCrossingHost(
                    root.transform, () => true, () => berths, () => household,
                    count => { boardChecks++; return count == 2; },
                    _ => { commits++; return true; }, read);
                Assert.That(host.TryOpen(), Is.True);
                var view = host.ActiveView;
                ReachJetty(view);
                view.ListenOrProceedButton.onClick.Invoke();
                Assert.That(host.Session.Stage,
                    Is.EqualTo(M09NightCrossingStage.AwaitingActualBoarding));

                // A changed ferry capacity or observed household invalidates
                // the original roll call before any real boarding callback.
                berths = 1;
                view.ListenOrProceedButton.onClick.Invoke();
                Assert.That(boardChecks, Is.Zero);
                Assert.That(host.Session.IsComplete, Is.False);
                berths = 4;
                household = 3;
                view.ListenOrProceedButton.onClick.Invoke();
                Assert.That(boardChecks, Is.Zero);
                Assert.That(host.Session.IsComplete, Is.False);

                household = 2;
                view.ListenOrProceedButton.onClick.Invoke();
                Assert.That(boardChecks, Is.EqualTo(1));
                Assert.That(host.Session.IsComplete, Is.True);

                // Even after an actual boarding was verified, an intervening
                // capacity or roll-call change cannot publish a stale result.
                berths = 0;
                Assert.Throws<InvalidOperationException>(() =>
                    view.FinishButton.onClick.Invoke());
                Assert.That(commits, Is.Zero);
                Assert.That(host.IsCommitted, Is.False);
                berths = 4;
                household = 3;
                Assert.Throws<InvalidOperationException>(() =>
                    view.FinishButton.onClick.Invoke());
                Assert.That(commits, Is.Zero);
                Assert.That(view.FinishButton.interactable, Is.True);

                household = 2;
                view.FinishButton.onClick.Invoke();
                Assert.That(commits, Is.EqualTo(1));
                Assert.That(host.CommittedOutcome, Is.EqualTo(M09RuiyaOutcome.Rescued));
                Assert.That(host.CommittedBoardedCount, Is.EqualTo(2));
                Assert.That(host.ActiveView, Is.Null);
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void VerifiedBoardingMayDeductExactlyItsOccupiedBerthsBeforeFinalCommit()
        {
            var root = new GameObject("M09 consumed ferry berth evidence");
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            int freeBerths = 4, boardingChecks = 0, commits = 0;
            M09EastShoreNightCrossingHost host = null;
            try
            {
                host = new M09EastShoreNightCrossingHost(
                    root.transform, () => true, () => freeBerths, () => 2,
                    count =>
                    {
                        boardingChecks++;
                        // An actual manifest update can consume capacity as
                        // part of the independent boarding verification.
                        freeBerths -= count;
                        return true;
                    },
                    completed =>
                    {
                        commits++;
                        return completed.Outcome == M09RuiyaOutcome.Rescued;
                    }, read);
                Assert.That(host.TryOpen(), Is.True);
                var view = host.ActiveView;
                ReachJetty(view);
                view.ListenOrProceedButton.onClick.Invoke(); // Household consents.
                view.ListenOrProceedButton.onClick.Invoke(); // Actually boarded.
                Assert.That(boardingChecks, Is.EqualTo(1));
                Assert.That(freeBerths, Is.EqualTo(2));
                Assert.That(host.Session.ActuallyBoardedCount, Is.EqualTo(2));

                // A second unexplained loss of a berth is not the same as
                // the two witnessed boardings and must not be committed.
                freeBerths = 1;
                Assert.Throws<InvalidOperationException>(() =>
                    view.FinishButton.onClick.Invoke());
                Assert.That(commits, Is.Zero);
                Assert.That(host.IsCommitted, Is.False);
                freeBerths = 2;
                view.FinishButton.onClick.Invoke();
                Assert.That(commits, Is.EqualTo(1));
                Assert.That(host.CommittedBoardedCount, Is.EqualTo(2));
                Assert.That(host.ActiveView, Is.Null);
                Assert.That(boardingChecks, Is.EqualTo(1),
                    "Retrying final save must not record the household twice.");
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DeclinedOrInterruptedCommitLeavesFinishRetryable()
        {
            var root = new GameObject("M09 night host retry");
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            bool ready = true;
            int attempts = 0;
            M09EastShoreNightCrossingHost host = null;
            try
            {
                host = new M09EastShoreNightCrossingHost(
                    root.transform, () => ready, () => 2, () => 2,
                    _ => true,
                    _ =>
                    {
                        attempts++;
                        if (attempts == 1) return false;
                        if (attempts == 2) throw new InvalidOperationException("Interrupted write");
                        return true;
                    },
                    read);
                Assert.That(host.TryOpen(), Is.True);
                var view = host.ActiveView;
                ReachJetty(view);
                view.ListenOrProceedButton.onClick.Invoke();
                view.ListenOrProceedButton.onClick.Invoke();

                ready = false;
                Assert.Throws<InvalidOperationException>(() => view.FinishButton.onClick.Invoke());
                Assert.That(attempts, Is.Zero);
                Assert.That(host.IsCommitted, Is.False);

                ready = true;
                Assert.Throws<InvalidOperationException>(() => view.FinishButton.onClick.Invoke());
                Assert.Throws<InvalidOperationException>(() => view.FinishButton.onClick.Invoke());
                Assert.That(host.IsCommitted, Is.False);
                Assert.That(view.FinishButton.interactable, Is.True);

                view.FinishButton.onClick.Invoke();
                Assert.That(attempts, Is.EqualTo(3));
                Assert.That(host.IsCommitted, Is.True);
                Assert.That(host.ActiveView, Is.Null);
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void RefusedConsentCommitsNotRescuedWithZeroBoarded()
        {
            var root = new GameObject("M09 night host refused");
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            M09EastShoreNightCrossingHost host = null;
            try
            {
                host = new M09EastShoreNightCrossingHost(
                    root.transform, () => true, () => 4, () => 2,
                    _ => false, _ => true, read);
                Assert.That(host.TryOpen(), Is.True);
                var view = host.ActiveView;
                ReachJetty(view);
                view.RiskOrRefuseButton.onClick.Invoke();
                view.FinishButton.onClick.Invoke();

                Assert.That(host.IsCommitted, Is.True);
                Assert.That(host.CommittedOutcome, Is.EqualTo(M09RuiyaOutcome.NotRescued));
                Assert.That(host.CommittedBoardedCount, Is.Zero);
                Assert.That(host.ActiveView, Is.Null);
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DisposalCleansUpViewAndIgnoresStaleFinishClicks()
        {
            var root = new GameObject("M09 night host dispose");
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            int commits = 0;
            M09EastShoreNightCrossingHost host = null;
            try
            {
                host = new M09EastShoreNightCrossingHost(
                    root.transform, () => true, () => 3, () => 2,
                    _ => true, _ => { commits++; return true; }, read);
                Assert.That(host.TryOpen(), Is.True);
                var view = host.ActiveView;
                ReachJetty(view);
                view.DeferButton.onClick.Invoke();
                var finishBtn = view.FinishButton;

                host.Dispose();
                finishBtn.onClick.Invoke();
                Assert.That(host.IsDisposed, Is.True);
                Assert.That(host.ActiveView, Is.Null);
                Assert.That(host.IsCommitted, Is.False);
                Assert.That(commits, Is.Zero);
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
