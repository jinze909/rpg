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
