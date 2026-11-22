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
    }
}
