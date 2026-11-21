using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class M13DepartureRiskHostTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        [Test]
        public void HostCannotOpenBeforeFleetBriefingReadyOrWhenConfirmedKeyNonZero()
        {
            var root = new GameObject("M13 host prerequisites");
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            bool briefingReady = false;
            M13DepartureRiskHost host = null;
            try
            {
                host = new M13DepartureRiskHost(
                    root.transform, () => briefingReady, () => false, () => true, read, write);
                Assert.That(host.TryOpen(), Is.False);
                Assert.That(host.TryDeliverQiQueWarning(), Is.False);

                briefingReady = true;
                values[M13DepartureRiskFacts.DepartureConfirmedKey] = 2;
                Assert.That(host.TryOpen(), Is.False);

                values[M13DepartureRiskFacts.DepartureConfirmedKey] = 0;
                Assert.That(host.TryOpen(), Is.True);
                Assert.That(host.TryOpen(), Is.False);
                Assert.That(host.ActiveView, Is.Not.Null);
                Assert.That(host.IsDeparted, Is.False);
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void QiQueWarningAndConsentFlowDepartsRouteCOnceAndBlocksReopen()
        {
            var root = new GameObject("M13 host route C");
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            M13DepartureRiskHost host = null;
            M13DepartureRiskHost duplicate = null;
            try
            {
                host = new M13DepartureRiskHost(
                    root.transform, () => true, () => false, () => true, read, write);
                Assert.That(host.TryOpen(), Is.True);
                var view = host.ActiveView;
                Assert.That(view.RouteCButton.interactable, Is.False);

                Assert.That(host.TryDeliverQiQueWarning(), Is.True);
                Assert.That(host.TryDeliverQiQueWarning(), Is.False);
                Assert.That(view.AcceptRiskConsentButton.gameObject.activeSelf, Is.True);

                view.AcceptRiskConsentButton.onClick.Invoke();
                Assert.That(view.RouteCButton.interactable, Is.True);

                view.RouteCButton.onClick.Invoke();
                Assert.That(host.IsDeparted, Is.True);
                Assert.That(host.ActiveView, Is.Null);
                Assert.That(M13DepartureRiskFacts.TryReadDeparture(
                    read, out M13DepartureRecord? saved), Is.True);
                Assert.That(saved.HasValue, Is.True);
                Assert.That(saved.Value.Route, Is.EqualTo(M13ApproachRoute.RouteCOldCanalNightRun));
                Assert.That(saved.Value.CanAyuanSufferHighRiskOutcome, Is.True);

                duplicate = new M13DepartureRiskHost(
                    root.transform, () => true, () => false, () => true, read, write);
                Assert.That(duplicate.TryOpen(), Is.False);
            }
            finally
            {
                duplicate?.Dispose();
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void InterruptedWriteAndCommitThenThrowRecoverWithoutDuplicateWrites()
        {
            var root = new GameObject("M13 host retry");
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            int preCommitFails = 0;
            int confirmCommits = 0;
            Action<string, int> unstable = (k, v) =>
            {
                if (k == M13DepartureRiskFacts.RouteKey && preCommitFails++ == 0)
                    throw new InvalidOperationException("Interrupted before confirm");
                values[k] = v;
                if (k == M13DepartureRiskFacts.DepartureConfirmedKey && v == 1 && ++confirmCommits == 1)
                    throw new InvalidOperationException("Lost ack after confirm");
            };
            M13DepartureRiskHost host = null;
            try
            {
                host = new M13DepartureRiskHost(
                    root.transform, () => true, () => true, () => true, read, unstable);
                Assert.That(host.TryOpen(), Is.True);
                var view = host.ActiveView;
                view.AgreeThreeBellsButton.onClick.Invoke();

                Assert.Throws<InvalidOperationException>(() =>
                    view.RouteBButton.onClick.Invoke());
                Assert.That(host.IsDeparted, Is.False);
                Assert.That(host.ActiveView, Is.Not.Null);

                Assert.Throws<InvalidOperationException>(() =>
                    view.RouteBButton.onClick.Invoke());
                Assert.That(host.IsDeparted, Is.True);
                Assert.That(confirmCommits, Is.EqualTo(1));

                view.RouteBButton.onClick.Invoke();
                Assert.That(confirmCommits, Is.EqualTo(1));
                Assert.That(host.ActiveView, Is.Null);
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DepartureConditionWithdrawalBlocksWriteUntilReadyAgain()
        {
            var root = new GameObject("M13 host gate");
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            bool canDepart = false;
            M13DepartureRiskHost host = null;
            try
            {
                host = new M13DepartureRiskHost(
                    root.transform, () => true, () => true, () => canDepart, read, write);
                Assert.That(host.TryOpen(), Is.True);
                var view = host.ActiveView;

                view.RouteAButton.onClick.Invoke();
                Assert.That(host.IsDeparted, Is.False);
                Assert.That(host.ActiveView, Is.Not.Null);

                canDepart = true;
                view.RouteAButton.onClick.Invoke();
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
        public void DisposalCleansUpViewAndIgnoresStaleRouteButtonClicks()
        {
            var root = new GameObject("M13 host dispose");
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            M13DepartureRiskHost host = null;
            try
            {
                host = new M13DepartureRiskHost(
                    root.transform, () => true, () => true, () => true, read, write);
                Assert.That(host.TryOpen(), Is.True);
                var routeABtn = host.ActiveView.RouteAButton;

                host.Dispose();
                routeABtn.onClick.Invoke();
                Assert.That(host.IsDisposed, Is.True);
                Assert.That(host.ActiveView, Is.Null);
                Assert.That(host.IsDeparted, Is.False);
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
