using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class M14FinalPassageHostLifecycleTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        private static void SeedM13Departure(
            Dictionary<string, int> values,
            M13ApproachRoute route)
        {
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            Assert.That(M13DepartureRiskFacts.TryRecordDeparture(new M13DepartureRecord(
                route, safetyPreparationComplete: true, qiQueWarningGiven: false,
                explicitHighRiskConsent: false, threeBellReverseSignalAgreed: false),
                read, write), Is.True);
        }

        [Test]
        public void ExternalViewDisposalAllowsCleanReopenAndSettlement()
        {
            var root = new GameObject("M14 host external dispose");
            var values = new Dictionary<string, int>();
            SeedM13Departure(values, M13ApproachRoute.RouteASupervisedCivilian);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            M14FinalPassageHost host = null;
            try
            {
                host = new M14FinalPassageHost(
                    root.transform, () => true, () => true, () => true,
                    () => false, () => false, () => false,
                    () => new M14CivilianCounts(16, 4, 2), read, write);
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

                reopenedView.DetainLuJunButton.onClick.Invoke();
                reopenedView.VerifyCivilianExitButton.onClick.Invoke();
                reopenedView.SettleAyuanSafeButton.onClick.Invoke();
                Assert.That(host.IsCompleted, Is.True);
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
            var root = new GameObject("M14 host canvas destroy");
            var values = new Dictionary<string, int>();
            SeedM13Departure(values, M13ApproachRoute.RouteCOldCanalNightRun);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            M14FinalPassageHost host = null;
            try
            {
                host = new M14FinalPassageHost(
                    root.transform, () => true, () => false, () => false,
                    () => false, () => true, () => true,
                    () => new M14CivilianCounts(20, 0, 1), read, write);
                Assert.That(host.TryOpen(), Is.True);
                var firstView = host.ActiveView;
                Assert.That(firstView, Is.Not.Null);

                UnityEngine.Object.DestroyImmediate(firstView.Panel.parent.gameObject);
                Assert.That(firstView.IsDisposed, Is.True);
                Assert.That(host.ActiveView, Is.Null);

                Assert.That(host.TryOpen(), Is.True);
                var reopenedView = host.ActiveView;
                Assert.That(reopenedView, Is.Not.Null);

                reopenedView.CompleteFogRelayButton.onClick.Invoke();
                reopenedView.VerifyCivilianExitButton.onClick.Invoke();
                reopenedView.SettleAyuanSafeButton.onClick.Invoke();
                Assert.That(host.IsCompleted, Is.True);
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
            var root = new GameObject("M14 host parent destroy");
            var values = new Dictionary<string, int>();
            SeedM13Departure(values, M13ApproachRoute.RouteASupervisedCivilian);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            M14FinalPassageHost host = null;
            try
            {
                host = new M14FinalPassageHost(
                    root.transform, () => true, () => true, () => true,
                    () => false, () => false, () => false,
                    () => new M14CivilianCounts(16, 4, 2), read, write);
                UnityEngine.Object.DestroyImmediate(root);
                root = null;

                Assert.That(host.TryOpen(), Is.False);
                Assert.That(host.ActiveView, Is.Null);
                Assert.That(host.IsCompleted, Is.False);
            }
            finally
            {
                host?.Dispose();
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
            }
        }
        [Test]
        public void OpeningReservesSlotAcrossSceneCallbackAndCanRetryAfterException()
        {
            var root = new GameObject("M14 reentrant opening");
            var values = new Dictionary<string, int>();
            SeedM13Departure(values, M13ApproachRoute.RouteASupervisedCivilian);
            M14FinalPassageHost host = null;
            bool throwOnce = true;
            bool nestedOpened = true;
            int reads = 0;
            try
            {
                host = new M14FinalPassageHost(root.transform, () =>
                {
                    reads++;
                    if (throwOnce)
                    {
                        throwOnce = false;
                        throw new InvalidOperationException("scene read failed");
                    }
                    nestedOpened = host.TryOpen();
                    return true;
                }, () => true, () => true, () => false, () => false,
                    () => false, () => new M14CivilianCounts(16, 4, 2),
                    Reader(values), (k, v) => values[k] = v);
                Assert.Throws<InvalidOperationException>(() => host.TryOpen());
                Assert.That(host.ActiveView, Is.Null);
                Assert.That(host.TryOpen(), Is.True);
                Assert.That(nestedOpened, Is.False);
                Assert.That(reads, Is.EqualTo(2));
                Assert.That(root.transform.childCount, Is.EqualTo(1));
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void RetirementDuringOpeningDoesNotPublishView()
        {
            var root = new GameObject("M14 retired opening");
            var values = new Dictionary<string, int>();
            SeedM13Departure(values, M13ApproachRoute.RouteASupervisedCivilian);
            M14FinalPassageHost host = null;
            try
            {
                host = new M14FinalPassageHost(root.transform, () =>
                {
                    host.Dispose();
                    return true;
                }, () => true, () => true, () => false, () => false,
                    () => false, () => new M14CivilianCounts(16, 4, 2),
                    Reader(values), (k, v) => values[k] = v);
                Assert.That(host.TryOpen(), Is.False);
                Assert.That(host.IsDisposed, Is.True);
                Assert.That(host.ActiveView, Is.Null);
                Assert.That(root.transform.childCount, Is.Zero);
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ArrivalChangedDuringOpeningRejectsStaleSession()
        {
            var root = new GameObject("M14 changed arrival during opening");
            var values = new Dictionary<string, int>();
            SeedM13Departure(values, M13ApproachRoute.RouteASupervisedCivilian);
            bool changeArrival = true;
            try
            {
                using (var host = new M14FinalPassageHost(root.transform, () =>
                {
                    if (changeArrival)
                    {
                        changeArrival = false;
                        values[M05LandingFacts.ArrivalSequenceKey] = 1;
                    }
                    return true;
                }, () => true, () => true, () => false, () => false,
                    () => false, () => new M14CivilianCounts(16, 4, 2),
                    Reader(values), (k, v) => values[k] = v))
                {
                    Assert.That(host.TryOpen(), Is.False);
                    Assert.That(host.ActiveView, Is.Null);
                    Assert.That(root.transform.childCount, Is.Zero);
                    Assert.That(host.TryOpen(), Is.True);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ReplacedViewDuringFinalProofCannotSettleOrCloseReplacement()
        {
            var root = new GameObject("M14 replaced proof owner");
            var values = new Dictionary<string, int>();
            SeedM13Departure(values, M13ApproachRoute.RouteASupervisedCivilian);
            M14FinalPassageHost host = null;
            bool replaceOnProof = false;
            int writes = 0;
            try
            {
                host = new M14FinalPassageHost(root.transform, () => true, () =>
                {
                    if (replaceOnProof)
                    {
                        replaceOnProof = false;
                        host.ActiveView.Dispose();
                        Assert.That(host.TryOpen(), Is.True);
                    }
                    return true;
                }, () => true, () => false, () => false, () => false,
                    () => new M14CivilianCounts(16, 4, 2), Reader(values),
                    (k, v) => { writes++; values[k] = v; });
                Assert.That(host.TryOpen(), Is.True);
                var oldView = host.ActiveView;
                oldView.DetainLuJunButton.onClick.Invoke();
                oldView.VerifyCivilianExitButton.onClick.Invoke();

                replaceOnProof = true;
                oldView.SettleAyuanSafeButton.onClick.Invoke();
                Assert.That(writes, Is.Zero);
                Assert.That(host.IsCompleted, Is.False);
                var replacement = host.ActiveView;
                Assert.That(replacement, Is.Not.Null);
                Assert.That(replacement, Is.Not.SameAs(oldView));
                Assert.That(host.Session.Phase, Is.EqualTo(M14Phase.RouteExecution));

                replacement.DetainLuJunButton.onClick.Invoke();
                replacement.VerifyCivilianExitButton.onClick.Invoke();
                replacement.SettleAyuanSafeButton.onClick.Invoke();
                Assert.That(host.IsCompleted, Is.True);
                Assert.That(host.ActiveView, Is.Null);
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ArrivalChangedDuringFinalRouteProofCannotWriteSettlement()
        {
            var root = new GameObject("M14 changed arrival during proof");
            var values = new Dictionary<string, int>();
            SeedM13Departure(values, M13ApproachRoute.RouteCOldCanalNightRun);
            bool changeArrival = false;
            int writes = 0;
            try
            {
                using (var host = new M14FinalPassageHost(root.transform,
                    () => true, () => false, () => false, () => false, () =>
                    {
                        if (changeArrival)
                            values[M05LandingFacts.ArrivalSequenceKey] = 1;
                        return true;
                    }, () => true, () => new M14CivilianCounts(20, 0, 1),
                    Reader(values), (k, v) => { writes++; values[k] = v; }))
                {
                    Assert.That(host.TryOpen(), Is.True);
                    var view = host.ActiveView;
                    view.CompleteFogRelayButton.onClick.Invoke();
                    view.VerifyCivilianExitButton.onClick.Invoke();
                    changeArrival = true;
                    view.SettleAyuanSafeButton.onClick.Invoke();
                    Assert.That(writes, Is.Zero);
                    Assert.That(host.IsCompleted, Is.False);
                    Assert.That(host.ActiveView, Is.SameAs(view));
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

    }
}
