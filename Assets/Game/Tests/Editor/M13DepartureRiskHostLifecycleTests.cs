using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class M13DepartureRiskHostLifecycleTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }


        [Test]
        public void NestedBriefingOpenPreservesOriginalDeparturePanel()
        {
            var root = new GameObject("M13 nested briefing");
            var values = new Dictionary<string, int>();
            M13DepartureRiskHost host = null;
            bool attempted = false, nestedOpened = true;
            try
            {
                host = new M13DepartureRiskHost(root.transform, () =>
                {
                    if (!attempted)
                    {
                        attempted = true;
                        nestedOpened = host.TryOpen();
                    }
                    return true;
                }, () => true, () => true, Reader(values), (k, v) => values[k] = v);
                Assert.That(host.TryOpen(), Is.True);
                Assert.That(attempted, Is.True);
                Assert.That(nestedOpened, Is.False);
                Assert.That(host.TryOpen(), Is.False);
                Assert.That(host.ActiveView, Is.Not.Null);
                host.ActiveView.RouteAButton.onClick.Invoke();
                Assert.That(host.IsDeparted, Is.True);
                Assert.That(host.ActiveView, Is.Null);
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void OpeningExceptionReleasesDepartureReservation()
        {
            var root = new GameObject("M13 interrupted briefing");
            var values = new Dictionary<string, int>();
            M13DepartureRiskHost host = null;
            bool fail = true;
            try
            {
                host = new M13DepartureRiskHost(root.transform, () => true, () =>
                {
                    if (fail) throw new InvalidOperationException("Safety prep unavailable");
                    return true;
                }, () => true, Reader(values), (k, v) => values[k] = v);
                Assert.Throws<InvalidOperationException>(() => host.TryOpen());
                Assert.That(host.ActiveView, Is.Null);
                fail = false;
                Assert.That(host.TryOpen(), Is.True);
                host.ActiveView.RouteAButton.onClick.Invoke();
                Assert.That(host.IsDeparted, Is.True);
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void RetirementDuringSafetyReadCannotPublishDeparturePanel()
        {
            var root = new GameObject("M13 retired opening");
            var values = new Dictionary<string, int>();
            M13DepartureRiskHost host = null;
            try
            {
                host = new M13DepartureRiskHost(root.transform, () => true,
                    () => { host.Dispose(); return true; }, () => true,
                    Reader(values), (k, v) => values[k] = v);
                Assert.That(host.TryOpen(), Is.False);
                Assert.That(host.IsDisposed, Is.True);
                Assert.That(host.ActiveView, Is.Null);
                Assert.That(host.Session, Is.Null);
                Assert.That(host.IsDeparted, Is.False);
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void RetirementDuringWarningProofCannotDeliverStaleWarning()
        {
            var root = new GameObject("M13 retired warning proof");
            var values = new Dictionary<string, int>();
            M13DepartureRiskHost host = null;
            bool retire = false;
            Func<string, int> read = key =>
            {
                if (retire && key == M05LandingFacts.ArrivalSequenceKey)
                    host.Dispose();
                return Reader(values)(key);
            };
            try
            {
                host = new M13DepartureRiskHost(root.transform,
                    () => true, () => false, () => true, read, (k, v) => values[k] = v);
                Assert.That(host.TryOpen(), Is.True);
                retire = true;
                bool delivered = true;
                Assert.DoesNotThrow(() => delivered = host.TryDeliverQiQueWarning());
                Assert.That(delivered, Is.False);
                Assert.That(host.ActiveView, Is.Null);
                Assert.That(host.Session.QiQueWarningGiven, Is.False);
                Assert.That(host.IsDeparted, Is.False);
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void RetirementDuringFinalDepartureProofCannotPersistRoute()
        {
            var root = new GameObject("M13 retired final proof");
            var values = new Dictionary<string, int>();
            M13DepartureRiskHost host = null;
            int confirmWrites = 0;
            bool retire = false;
            try
            {
                host = new M13DepartureRiskHost(root.transform, () => true,
                    () => true, () =>
                    {
                        if (retire) host.Dispose();
                        return true;
                    }, Reader(values), (k, v) =>
                    {
                        values[k] = v;
                        if (k == M13DepartureRiskFacts.DepartureConfirmedKey && v == 1)
                            confirmWrites++;
                    });
                Assert.That(host.TryOpen(), Is.True);
                var view = host.ActiveView;
                retire = true;
                Assert.DoesNotThrow(() => view.RouteAButton.onClick.Invoke());
                Assert.That(host.IsDisposed, Is.True);
                Assert.That(host.ActiveView, Is.Null);
                Assert.That(host.IsDeparted, Is.False);
                Assert.That(confirmWrites, Is.Zero);
                Assert.That(host.Session.IsDeparted, Is.False);
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ExternalViewDisposalAllowsCleanReopenAndBlocksStaleQiQueWarning()
        {
            var root = new GameObject("M13 host external dispose");
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            M13DepartureRiskHost host = null;
            try
            {
                host = new M13DepartureRiskHost(
                    root.transform, () => true, () => false, () => true, read, write);
                Assert.That(host.TryOpen(), Is.True);
                var firstView = host.ActiveView;
                Assert.That(firstView, Is.Not.Null);

                firstView.Dispose();
                Assert.That(firstView.IsDisposed, Is.True);
                Assert.That(host.ActiveView, Is.Null);
                Assert.That(host.TryDeliverQiQueWarning(), Is.False);

                Assert.That(host.TryOpen(), Is.True);
                var reopenedView = host.ActiveView;
                Assert.That(reopenedView, Is.Not.Null);
                Assert.That(reopenedView, Is.Not.SameAs(firstView));

                Assert.That(host.TryDeliverQiQueWarning(), Is.True);
                reopenedView.AcceptRiskConsentButton.onClick.Invoke();
                reopenedView.RouteCButton.onClick.Invoke();
                Assert.That(host.IsDeparted, Is.True);
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
            var root = new GameObject("M13 host canvas destroy");
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            M13DepartureRiskHost host = null;
            try
            {
                host = new M13DepartureRiskHost(
                    root.transform, () => true, () => true, () => true, read, write);
                Assert.That(host.TryOpen(), Is.True);
                var firstView = host.ActiveView;
                Assert.That(firstView, Is.Not.Null);

                UnityEngine.Object.DestroyImmediate(firstView.Panel.parent.gameObject);
                Assert.That(firstView.IsDisposed, Is.True);
                Assert.That(host.ActiveView, Is.Null);
                Assert.That(host.TryDeliverQiQueWarning(), Is.False);

                Assert.That(host.TryOpen(), Is.True);
                var reopenedView = host.ActiveView;
                Assert.That(reopenedView, Is.Not.Null);
                reopenedView.RouteAButton.onClick.Invoke();
                Assert.That(host.IsDeparted, Is.True);
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
            var root = new GameObject("M13 host parent destroy");
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            M13DepartureRiskHost host = null;
            try
            {
                host = new M13DepartureRiskHost(
                    root.transform, () => true, () => true, () => true, read, write);
                UnityEngine.Object.DestroyImmediate(root);
                root = null;

                Assert.That(host.TryOpen(), Is.False);
                Assert.That(host.ActiveView, Is.Null);
                Assert.That(host.IsDeparted, Is.False);
            }
            finally
            {
                host?.Dispose();
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
