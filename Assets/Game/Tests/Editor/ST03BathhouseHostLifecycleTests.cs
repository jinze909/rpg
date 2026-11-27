using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class ST03BathhouseHostLifecycleTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        private static ST03BathhouseHost CreateHost(
            Transform parent,
            Dictionary<string, int> values)
        {
            return new ST03BathhouseHost(
                parent,
                () => true,
                () => true,
                () => true,
                () => true,
                () => true,
                Reader(values),
                (k, v) => values[k] = v);
        }

        [Test]
        public void ExternalViewDisposalClearsActiveViewAndAllowsCleanReopen()
        {
            var root = new GameObject("ST03 lifecycle external dispose");
            var values = new Dictionary<string, int>();
            ST03BathhouseHost host = null;
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
            var root = new GameObject("ST03 lifecycle destroyed canvas");
            var values = new Dictionary<string, int>();
            ST03BathhouseHost host = null;
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
        public void DisposingHostDuringLiveOpeningDoesNotPublishCanvas()
        {
            var root = new GameObject("ST03 retired during opening");
            ST03BathhouseHost host = null;
            try
            {
                host = new ST03BathhouseHost(root.transform, () =>
                {
                    host.Dispose();
                    return true;
                }, () => true, () => true, () => true, () => true,
                    _ => 0, (_, __) => Assert.Fail("Opening must not write facts"));

                Assert.That(host.TryOpen(), Is.False);
                Assert.That(host.IsDisposed, Is.True);
                Assert.That(host.Session, Is.Null);
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
        public void NestedOpeningCannotCreateTwoBathhouseCanvases()
        {
            var root = new GameObject("ST03 nested opening");
            ST03BathhouseHost host = null;
            bool attempted = false;
            try
            {
                host = new ST03BathhouseHost(root.transform, () =>
                {
                    if (!attempted)
                    {
                        attempted = true;
                        Assert.That(host.TryOpen(), Is.False);
                    }
                    return true;
                }, () => true, () => true, () => true, () => true,
                    _ => 0, (_, __) => Assert.Fail("Opening must not write facts"));

                Assert.That(host.TryOpen(), Is.True);
                Assert.That(attempted, Is.True);
                Assert.That(root.transform.childCount, Is.EqualTo(1));
                host.ActiveView.Dispose();
                Assert.That(host.TryOpen(), Is.True);
                Assert.That(root.transform.childCount, Is.EqualTo(1));
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void SceneProofRetiredPermanentChoiceCannotWriteOrCloseReplacement()
        {
            AssertRetiredPermanentChoiceCannotWriteOrCloseReplacement(false);
        }

        [Test]
        public void FactReadRetiredPermanentChoiceCannotWriteOrCloseReplacement()
        {
            AssertRetiredPermanentChoiceCannotWriteOrCloseReplacement(true);
        }

        private static void AssertRetiredPermanentChoiceCannotWriteOrCloseReplacement(bool retireDuringFactRead)
        {
            var root = new GameObject("ST03 retired permanent choice");
            var values = new Dictionary<string, int>();
            ST03BathhouseHost host = null;
            ST03BathhouseView replacement = null;
            bool armed = false;
            int writes = 0;
            try
            {
                void ReplaceView()
                {
                    armed = false;
                    host.ActiveView.Dispose();
                    Assert.That(host.TryOpen(), Is.True);
                    replacement = host.ActiveView;
                }

                host = new ST03BathhouseHost(root.transform, () =>
                {
                    if (armed && !retireDuringFactRead) ReplaceView();
                    return true;
                }, () => true, () => true, () => true, () => true, key =>
                {
                    if (armed && retireDuringFactRead) ReplaceView();
                    return values.TryGetValue(key, out int value) ? value : 0;
                }, (key, value) => { writes++; values[key] = value; });

                Assert.That(host.TryOpen(), Is.True);
                var original = host.ActiveView;
                original.BoilerClueButton.onClick.Invoke();
                original.LockersClueButton.onClick.Invoke();
                original.LettersClueButton.onClick.Invoke();
                original.InterviewWuSuButton.onClick.Invoke();
                original.MakeSafeRouteButton.onClick.Invoke();
                original.CompleteTrialNightButton.onClick.Invoke();
                original.ReconcileBudgetButton.onClick.Invoke();
                Assert.That(host.Session.Phase, Is.EqualTo(ST03BathhousePhase.CompareOperatingPlans));

                armed = true;
                original.SellAndRelocateButton.onClick.Invoke();

                Assert.That(replacement, Is.Not.Null);
                Assert.That(host.ActiveView, Is.SameAs(replacement));
                Assert.That(host.Session.Phase, Is.EqualTo(ST03BathhousePhase.InspectClosedBathhouse));
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
            var root = new GameObject("ST03 lifecycle destroyed parent");
            var values = new Dictionary<string, int>();
            ST03BathhouseHost host = null;
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
