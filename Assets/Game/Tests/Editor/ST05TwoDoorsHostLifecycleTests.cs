using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class ST05TwoDoorsHostLifecycleTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        private static ST05TwoDoorsHost CreateHost(
            Transform parent,
            Dictionary<string, int> values)
        {
            return new ST05TwoDoorsHost(
                parent,
                () => true,
                () => true,
                () => true,
                () => true,
                () => true,
                () => false,
                () => true,
                Reader(values),
                (k, v) => values[k] = v);
        }

        [Test]
        public void RetiredHomeAgreementCannotWriteOrReplaceNewSession()
        {
            var root = new GameObject("ST05 retired household settlement");
            var values = new Dictionary<string, int>();
            ST05TwoDoorsHost host = null;
            ST05TwoDoorsView replacement = null;
            bool armed = false;
            int writes = 0;
            try
            {
                host = new ST05TwoDoorsHost(root.transform,
                    () => true, () => true, () => true, () => true, () => true, () => false, () => true,
                    key =>
                    {
                        if (armed)
                        {
                            armed = false;
                            host.ActiveView.Dispose();
                            Assert.That(host.TryOpen(), Is.True);
                            replacement = host.ActiveView;
                        }
                        return values.TryGetValue(key, out int value) ? value : 0;
                    }, (key, value) => { writes++; values[key] = value; });

                Assert.That(host.TryOpen(), Is.True);
                var original = host.ActiveView;
                AdvanceToHomeAgreement(original);
                armed = true;
                original.KeepTwoDoorsButton.onClick.Invoke();

                Assert.That(replacement, Is.Not.Null);
                Assert.That(host.ActiveView, Is.SameAs(replacement));
                Assert.That(host.Session.Phase, Is.EqualTo(ST05TwoDoorsPhase.UnblockStaircase));
                Assert.That(writes, Is.Zero);
                Assert.That(values, Is.Empty);
                Assert.That(root.transform.childCount, Is.EqualTo(1));

                AdvanceToHomeAgreement(replacement);
                replacement.KeepTwoDoorsButton.onClick.Invoke();
                Assert.That(host.Session.Phase, Is.EqualTo(ST05TwoDoorsPhase.AwaitChengOwnJobDecision));
                replacement.ChengStayLocalButton.onClick.Invoke();
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
        public void RetiredAssignmentCannotWriteAfterHouseholdConfirmation()
        {
            var root = new GameObject("ST05 retired job settlement");
            var values = new Dictionary<string, int>();
            ST05TwoDoorsHost host = null;
            bool armed = false;
            int writes = 0;
            try
            {
                host = new ST05TwoDoorsHost(root.transform,
                    () => true, () => true, () => true, () => true, () => true, () => false, () => true,
                    key =>
                    {
                        if (armed)
                        {
                            armed = false;
                            host.Dispose();
                        }
                        return values.TryGetValue(key, out int value) ? value : 0;
                    }, (key, value) => { writes++; values[key] = value; });

                Assert.That(host.TryOpen(), Is.True);
                var view = host.ActiveView;
                AdvanceToHomeAgreement(view);
                view.KeepTwoDoorsButton.onClick.Invoke();
                Assert.That(host.Session.Phase, Is.EqualTo(ST05TwoDoorsPhase.AwaitChengOwnJobDecision));
                int householdWrites = writes;
                Assert.That(householdWrites, Is.GreaterThan(0));
                armed = true;
                view.ChengStayLocalButton.onClick.Invoke();

                Assert.That(host.IsDisposed, Is.True);
                Assert.That(host.ActiveView, Is.Null);
                Assert.That(writes, Is.EqualTo(householdWrites));
                Assert.That(ST05TwoDoorsFacts.TryRead(Reader(values),
                    out ST05TwoDoorsOutcome? saved), Is.True);
                Assert.That(saved.HasValue, Is.True);
                Assert.That(saved.Value.Assignment.HasValue, Is.False);
                Assert.That(root.transform.childCount, Is.Zero);
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void AdvanceToHomeAgreement(ST05TwoDoorsView view)
        {
            view.RemoveDoorFrameButton.onClick.Invoke();
            view.TestMarketGiftsButton.onClick.Invoke();
            view.ResolveCourtyardButton.onClick.Invoke();
            view.HearBothPartnersButton.onClick.Invoke();
            Assert.That(view.Session.Phase, Is.EqualTo(ST05TwoDoorsPhase.AgreeHomeAndKeys));
        }

        [Test]
        public void ExternalViewDisposalClearsActiveViewAndAllowsCleanReopen()
        {
            var root = new GameObject("ST05 lifecycle external dispose");
            var values = new Dictionary<string, int>();
            ST05TwoDoorsHost host = null;
            try
            {
                host = CreateHost(root.transform, values);
                Assert.That(host.TryOpen(), Is.True);
                var firstView = host.ActiveView;
                Assert.That(firstView, Is.Not.Null);
                Assert.That(firstView.IsDisposed, Is.False);

                firstView.Dispose();
                Assert.That(firstView.IsDisposed, Is.True);
                Assert.That(host.ActiveView, Is.Null);

                Assert.That(host.TryOpen(), Is.True);
                Assert.That(host.ActiveView, Is.Not.Null);
                Assert.That(host.ActiveView, Is.Not.SameAs(firstView));
                Assert.That(host.ActiveView.IsDisposed, Is.False);
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DestroyedCanvasMarksViewDisposedAndAllowsHostReopen()
        {
            var root = new GameObject("ST05 lifecycle destroyed canvas");
            var values = new Dictionary<string, int>();
            ST05TwoDoorsHost host = null;
            try
            {
                host = CreateHost(root.transform, values);
                Assert.That(host.TryOpen(), Is.True);
                var firstView = host.ActiveView;
                Assert.That(firstView, Is.Not.Null);

                UnityEngine.Object.DestroyImmediate(firstView.Panel.parent.gameObject);
                Assert.That(firstView.IsDisposed, Is.True);
                Assert.That(host.ActiveView, Is.Null);

                Assert.That(host.TryOpen(), Is.True);
                Assert.That(host.ActiveView, Is.Not.Null);
                Assert.That(host.ActiveView, Is.Not.SameAs(firstView));
                Assert.That(host.ActiveView.IsDisposed, Is.False);
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void CourtyardCallbackRetiringHostCannotPublishSessionOrCanvas()
        {
            var root = new GameObject("ST05 retired courtyard opening");
            ST05TwoDoorsHost host = null;
            try
            {
                host = new ST05TwoDoorsHost(root.transform, () =>
                {
                    host.Dispose();
                    return true;
                }, () => true, () => true, () => true, () => true, () => false, () => true,
                    _ => 0, (_, __) => Assert.Fail("Opening must not write facts"));

                Assert.That(host.TryOpen(), Is.False);
                Assert.That(host.IsDisposed, Is.True);
                Assert.That(host.Session, Is.Null);
                Assert.That(host.ActiveView, Is.Null);
                Assert.That(root.transform.childCount, Is.Zero);
                Assert.That(host.TryOpen(), Is.False);
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ParentDestroyedDuringFactReadCannotPublishTwoDoorsSession()
        {
            var root = new GameObject("ST05 parent retired during fact read");
            ST05TwoDoorsHost host = null;
            try
            {
                host = new ST05TwoDoorsHost(root.transform,
                    () => true, () => true, () => true, () => true, () => true, () => false, () => true,
                    _ =>
                    {
                        if (root != null) UnityEngine.Object.DestroyImmediate(root);
                        return 0;
                    }, (_, __) => Assert.Fail("Opening must not write facts"));

                Assert.That(host.TryOpen(), Is.False);
                Assert.That(host.Session, Is.Null);
                Assert.That(host.ActiveView, Is.Null);
                Assert.That(host.TryOpen(), Is.False);
            }
            finally
            {
                host?.Dispose();
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void NestedFactReadCannotPublishSecondTwoDoorsCanvas()
        {
            var root = new GameObject("ST05 nested fact read");
            ST05TwoDoorsHost host = null;
            bool attempted = false;
            try
            {
                host = new ST05TwoDoorsHost(root.transform,
                    () => true, () => true, () => true, () => true, () => true, () => false, () => true,
                    _ =>
                    {
                        if (!attempted)
                        {
                            attempted = true;
                            Assert.That(host.TryOpen(), Is.False);
                        }
                        return 0;
                    }, (_, __) => Assert.Fail("Opening must not write facts"));

                Assert.That(host.TryOpen(), Is.True);
                Assert.That(attempted, Is.True);
                Assert.That(host.ActiveView.Session, Is.SameAs(host.Session));
                Assert.That(root.transform.childCount, Is.EqualTo(1));
                var first = host.ActiveView;
                first.Dispose();
                Assert.That(host.TryOpen(), Is.True);
                Assert.That(host.ActiveView, Is.Not.SameAs(first));
                Assert.That(root.transform.childCount, Is.EqualTo(1));
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DestroyedParentFailsClosedWithoutThrowingOnTryOpen()
        {
            var root = new GameObject("ST05 lifecycle destroyed parent");
            var values = new Dictionary<string, int>();
            ST05TwoDoorsHost host = null;
            try
            {
                host = CreateHost(root.transform, values);
                UnityEngine.Object.DestroyImmediate(root);
                root = null;

                bool opened = false;
                Assert.DoesNotThrow(() => opened = host.TryOpen());
                Assert.That(opened, Is.False);
                Assert.That(host.ActiveView, Is.Null);
            }
            finally
            {
                host?.Dispose();
                if (root != null)
                    UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
