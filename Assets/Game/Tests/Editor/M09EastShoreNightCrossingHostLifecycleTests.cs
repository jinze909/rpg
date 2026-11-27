using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class M09EastShoreNightCrossingHostLifecycleTests
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


        [TestCase(false)]
        [TestCase(true)]
        public void NestedOpeningDoesNotReplaceOriginalNightCrossing(bool duringCountRead)
        {
            var root = new GameObject("M09 nested night crossing");
            var values = new Dictionary<string, int>();
            int commits = 0, boardingChecks = 0;
            bool attempted = false, nestedOpened = true;
            M09EastShoreNightCrossingHost host = null;
            void TryNested()
            {
                if (attempted) return;
                attempted = true;
                nestedOpened = host.TryOpen();
            }
            try
            {
                host = new M09EastShoreNightCrossingHost(
                    root.transform,
                    () => { if (!duringCountRead) TryNested(); return true; },
                    () => { if (duringCountRead) TryNested(); return 3; },
                    () => 2,
                    count => { boardingChecks++; return count == 2; },
                    completed => { commits++; return completed.Outcome == M09RuiyaOutcome.Rescued; },
                    Reader(values));

                Assert.That(host.TryOpen(), Is.True);
                Assert.That(attempted, Is.True);
                Assert.That(nestedOpened, Is.False);
                var opened = host.ActiveView;
                Assert.That(opened, Is.Not.Null);
                Assert.That(host.TryOpen(), Is.False);
                Assert.That(host.ActiveView, Is.SameAs(opened));
                ReachJetty(opened);
                opened.ListenOrProceedButton.onClick.Invoke();
                opened.ListenOrProceedButton.onClick.Invoke();
                opened.FinishButton.onClick.Invoke();
                Assert.That(boardingChecks, Is.EqualTo(1));
                Assert.That(commits, Is.EqualTo(1));
                Assert.That(host.CommittedOutcome, Is.EqualTo(M09RuiyaOutcome.Rescued));
                Assert.That(host.ActiveView, Is.Null);
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void OpeningExceptionReleasesNightCrossingReservation()
        {
            var root = new GameObject("M09 opening exception");
            var values = new Dictionary<string, int>();
            bool fail = true;
            M09EastShoreNightCrossingHost host = null;
            try
            {
                host = new M09EastShoreNightCrossingHost(
                    root.transform, () => true,
                    () =>
                    {
                        if (fail) throw new InvalidOperationException("Berth count unavailable");
                        return 3;
                    },
                    () => 2, _ => true, _ => true, Reader(values));
                Assert.Throws<InvalidOperationException>(() => host.TryOpen());
                Assert.That(host.ActiveView, Is.Null);
                fail = false;
                Assert.That(host.TryOpen(), Is.True);
                Assert.That(host.ActiveView, Is.Not.Null);
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void RetirementDuringCountReadDoesNotPublishNightCrossing()
        {
            var root = new GameObject("M09 retired during count read");
            var values = new Dictionary<string, int>();
            M09EastShoreNightCrossingHost host = null;
            try
            {
                host = new M09EastShoreNightCrossingHost(
                    root.transform, () => true, () => 3,
                    () => { host.Dispose(); return 2; },
                    _ => true, _ => true, Reader(values));
                Assert.That(host.TryOpen(), Is.False);
                Assert.That(host.IsDisposed, Is.True);
                Assert.That(host.ActiveView, Is.Null);
                Assert.That(host.Session, Is.Null);
                Assert.That(host.IsCommitted, Is.False);
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ArrivalChangeDuringCountReadRequiresFreshNightCrossing()
        {
            var root = new GameObject("M09 arrival changed during count read");
            var values = new Dictionary<string, int>();
            bool changeArrival = true;
            M09EastShoreNightCrossingHost host = null;
            try
            {
                host = new M09EastShoreNightCrossingHost(
                    root.transform, () => true, () => 3,
                    () =>
                    {
                        if (changeArrival)
                        {
                            changeArrival = false;
                            values[M05LandingFacts.ArrivalSequenceKey] = 1;
                        }
                        return 2;
                    },
                    _ => true, _ => true, Reader(values));
                Assert.That(host.TryOpen(), Is.False);
                Assert.That(host.ActiveView, Is.Null);
                Assert.That(host.Session, Is.Null);
                Assert.That(host.IsCommitted, Is.False);
                Assert.That(host.TryOpen(), Is.True);
                Assert.That(host.ActiveView, Is.Not.Null);
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ExternalViewDisposalAllowsCleanReopenAndOutcomeCommit()
        {
            var root = new GameObject("M09 night host external dispose");
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
                var firstView = host.ActiveView;
                Assert.That(firstView, Is.Not.Null);

                firstView.Dispose();
                Assert.That(firstView.IsDisposed, Is.True);
                Assert.That(host.ActiveView, Is.Null);

                Assert.That(host.TryOpen(), Is.True);
                var reopenedView = host.ActiveView;
                Assert.That(reopenedView, Is.Not.Null);
                Assert.That(reopenedView, Is.Not.SameAs(firstView));

                ReachJetty(reopenedView);
                reopenedView.ListenOrProceedButton.onClick.Invoke();
                reopenedView.ListenOrProceedButton.onClick.Invoke();
                reopenedView.FinishButton.onClick.Invoke();
                Assert.That(commits, Is.EqualTo(1));
                Assert.That(host.IsCommitted, Is.True);
                Assert.That(host.CommittedOutcome, Is.EqualTo(M09RuiyaOutcome.Rescued));
                Assert.That(host.ActiveView, Is.Null);
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DestroyedViewCanvasTreatsViewAsDisposedAndAllowsCleanReopen()
        {
            var root = new GameObject("M09 night host canvas destroy");
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            int commits = 0;
            M09EastShoreNightCrossingHost host = null;
            try
            {
                host = new M09EastShoreNightCrossingHost(
                    root.transform, () => true, () => 4, () => 2,
                    _ => false,
                    _ =>
                    {
                        commits++;
                        return true;
                    },
                    read);
                Assert.That(host.TryOpen(), Is.True);
                var firstView = host.ActiveView;
                Assert.That(firstView, Is.Not.Null);

                UnityEngine.Object.DestroyImmediate(firstView.Panel.parent.gameObject);
                Assert.That(firstView.IsDisposed, Is.True);
                Assert.That(host.ActiveView, Is.Null);

                Assert.That(host.TryOpen(), Is.True);
                var reopenedView = host.ActiveView;
                Assert.That(reopenedView, Is.Not.Null);

                ReachJetty(reopenedView);
                reopenedView.RiskOrRefuseButton.onClick.Invoke();
                reopenedView.FinishButton.onClick.Invoke();
                Assert.That(commits, Is.EqualTo(1));
                Assert.That(host.IsCommitted, Is.True);
                Assert.That(host.CommittedOutcome, Is.EqualTo(M09RuiyaOutcome.NotRescued));
                Assert.That(host.ActiveView, Is.Null);
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DestroyedParentFailsClosedOnTryOpenWithoutThrowing()
        {
            var root = new GameObject("M09 night host parent destroy");
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            M09EastShoreNightCrossingHost host = null;
            try
            {
                host = new M09EastShoreNightCrossingHost(
                    root.transform, () => true, () => 3, () => 2,
                    _ => true, _ => true, read);
                UnityEngine.Object.DestroyImmediate(root);
                root = null;

                Assert.That(host.TryOpen(), Is.False);
                Assert.That(host.ActiveView, Is.Null);
                Assert.That(host.IsCommitted, Is.False);
            }
            finally
            {
                host?.Dispose();
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
