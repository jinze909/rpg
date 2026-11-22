using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class ST01TheaterChoiceHostLifecycleTests
    {
        private static Func<string, int> Read(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        private static ST01TheaterChoiceHost CreateHost(
            Transform parent,
            Dictionary<string, int> values)
        {
            return new ST01TheaterChoiceHost(
                parent,
                () => true,
                () => true,
                () => true,
                () => true,
                () => true,
                () => true,
                () => false,
                () => false,
                () => true,
                Read(values),
                (key, value) => values[key] = value);
        }

        [Test]
        public void ExternalViewDisposalClearsActiveViewAndAllowsCleanReopen()
        {
            var root = new GameObject("ST01 lifecycle external dispose");
            var values = new Dictionary<string, int>();
            ST01TheaterChoiceHost host = null;
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
                Assert.DoesNotThrow(() => host.RefreshView());
                Assert.That(host.ActiveView, Is.Null);

                Assert.That(host.TryOpen(), Is.True);
                Assert.That(host.ActiveView, Is.Not.Null);
                Assert.That(host.ActiveView, Is.Not.SameAs(firstView));
                Assert.That(host.ActiveView.IsDisposed, Is.False);

                host.ActiveView.BoycottButton.onClick.Invoke();
                Assert.That(host.ActiveView, Is.Null);
                Assert.That(ST01RegionalStoryFacts.TryRead(Read(values), out ST01RegionalOutcome saved), Is.True);
                Assert.That(saved.Theater, Is.EqualTo(ST01TheaterChoice.PublicBoycott));
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
            var root = new GameObject("ST01 lifecycle destroyed canvas");
            var values = new Dictionary<string, int>();
            ST01TheaterChoiceHost host = null;
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
        public void DestroyedParentFailsClosedWithoutThrowingOnTryOpen()
        {
            var root = new GameObject("ST01 lifecycle destroyed parent");
            var values = new Dictionary<string, int>();
            ST01TheaterChoiceHost host = null;
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
