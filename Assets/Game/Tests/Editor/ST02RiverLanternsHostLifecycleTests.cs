using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class ST02RiverLanternsHostLifecycleTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        [Test]
        public void ExternalViewDisposalClearsActiveViewAndAllowsCleanReopen()
        {
            var root = new GameObject("ST02 lifecycle external dispose");
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            ST02RiverLanternsHost host = null;
            try
            {
                host = new ST02RiverLanternsHost(root.transform, () => true, read, write);
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
            var root = new GameObject("ST02 lifecycle destroyed canvas");
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            ST02RiverLanternsHost host = null;
            try
            {
                host = new ST02RiverLanternsHost(root.transform, () => true, read, write);
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
        public void DockCallbackDisposingHostPreventsLateOpening()
        {
            var root = new GameObject("ST02 dock callback disposes opening host");
            ST02RiverLanternsHost host = null;
            try
            {
                host = new ST02RiverLanternsHost(root.transform, () =>
                {
                    host.Dispose();
                    return true;
                }, _ => 0, (_, __) => Assert.Fail("Unexpected fact write"));
                Assert.That(host.TryOpen(), Is.False);
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
        public void DockCallbackReplacingDialogCannotSettleReplacement()
        {
            AssertRetiredRescueDoesNotSettle(false);
        }

        [Test]
        public void FactReaderReplacingDialogCannotWriteRetiredRescue()
        {
            AssertRetiredRescueDoesNotSettle(true);
        }

        private static void AssertRetiredRescueDoesNotSettle(bool retireDuringRead)
        {
            var root = new GameObject("ST02 stale rescue callback");
            var values = new Dictionary<string, int>();
            ST02RiverLanternsHost host = null;
            ST02RiverLanternsView replacement = null;
            bool armed = false;
            int writes = 0;
            try
            {
                void RetireAndReopen()
                {
                    armed = false;
                    host.ActiveView.Dispose();
                    Assert.That(host.TryOpen(), Is.True);
                    replacement = host.ActiveView;
                }

                host = new ST02RiverLanternsHost(root.transform, () =>
                {
                    if (armed && !retireDuringRead) RetireAndReopen();
                    return true;
                }, key =>
                {
                    if (armed && retireDuringRead) RetireAndReopen();
                    return values.TryGetValue(key, out int value) ? value : 0;
                }, (key, value) => { writes++; values[key] = value; });

                Assert.That(host.TryOpen(), Is.True);
                var original = host.ActiveView;
                original.InspectXieZhiBoatButton.onClick.Invoke();
                original.InspectWenShengBoatButton.onClick.Invoke();
                original.GatherDockFeedbackButton.onClick.Invoke();
                original.PublishToBothButton.onClick.Invoke();
                original.JointBidButton.onClick.Invoke();
                Assert.That(host.Session.Stage, Is.EqualTo(ST02Stage.OldLockRescue));
                armed = true;
                original.BraceHullStressButton.onClick.Invoke();

                Assert.That(replacement, Is.Not.Null);
                Assert.That(host.ActiveView, Is.SameAs(replacement));
                Assert.That(host.Session.Stage, Is.EqualTo(ST02Stage.WorkshopInspection));
                Assert.That(writes, Is.Zero);
                Assert.That(host.IsCompleted, Is.False);
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
            var root = new GameObject("ST02 lifecycle destroyed parent");
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            ST02RiverLanternsHost host = null;
            try
            {
                host = new ST02RiverLanternsHost(root.transform, () => true, read, write);
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
