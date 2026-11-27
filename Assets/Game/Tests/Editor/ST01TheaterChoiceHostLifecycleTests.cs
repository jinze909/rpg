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
        public void RetiredTheaterSettlementCannotWriteOrCloseReplacement()
        {
            var root = new GameObject("ST01 retired theater settlement");
            var values = new Dictionary<string, int>();
            ST01TheaterChoiceHost host = null;
            ST01TheaterChoiceView replacement = null;
            bool armOnBoycottProof = false;
            bool retireOnRead = false;
            int writes = 0;
            try
            {
                host = new ST01TheaterChoiceHost(root.transform,
                    () => true, () => true, () => true, () => true, () => true, () => true,
                    () => false, () => false, () =>
                    {
                        if (armOnBoycottProof)
                        {
                            armOnBoycottProof = false;
                            retireOnRead = true;
                        }
                        return true;
                    }, key =>
                    {
                        if (retireOnRead)
                        {
                            retireOnRead = false;
                            host.ActiveView.Dispose();
                            Assert.That(host.TryOpen(), Is.True);
                            replacement = host.ActiveView;
                        }
                        return values.TryGetValue(key, out int value) ? value : 0;
                    }, (key, value) => { writes++; values[key] = value; });

                Assert.That(host.TryOpen(), Is.True);
                var original = host.ActiveView;
                armOnBoycottProof = true;
                original.BoycottButton.onClick.Invoke();

                Assert.That(replacement, Is.Not.Null);
                Assert.That(host.ActiveView, Is.SameAs(replacement));
                Assert.That(writes, Is.Zero);
                Assert.That(values, Is.Empty);
                Assert.That(root.transform.childCount, Is.EqualTo(1));

                replacement.BoycottButton.onClick.Invoke();
                Assert.That(host.ActiveView, Is.Null);
                Assert.That(ST01RegionalStoryFacts.TryRead(Read(values),
                    out ST01RegionalOutcome saved), Is.True);
                Assert.That(saved.Theater, Is.EqualTo(ST01TheaterChoice.PublicBoycott));
                Assert.That(writes, Is.EqualTo(2));
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
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
        public void SceneCallbackRetiringHostCannotPublishTheaterView()
        {
            var root = new GameObject("ST01 scene callback retirement");
            ST01TheaterChoiceHost host = null;
            try
            {
                host = new ST01TheaterChoiceHost(root.transform, () =>
                {
                    host.Dispose();
                    return true;
                }, () => true, () => true, () => true, () => true, () => true,
                    () => false, () => false, () => true,
                    _ => 0, (_, __) => Assert.Fail("Opening must not write facts"));

                Assert.That(host.TryOpen(), Is.False);
                Assert.That(host.IsDisposed, Is.True);
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
        public void DestroyedParentDuringFactReadCannotPublishTheaterView()
        {
            var root = new GameObject("ST01 fact callback retirement");
            ST01TheaterChoiceHost host = null;
            try
            {
                host = new ST01TheaterChoiceHost(root.transform,
                    () => true, () => true, () => true, () => true, () => true, () => true,
                    () => false, () => false, () => true, key =>
                    {
                        if (key == ST01RegionalStoryFacts.DepartureChoiceKey && root != null)
                            UnityEngine.Object.DestroyImmediate(root);
                        return 0;
                    }, (_, __) => Assert.Fail("Opening must not write facts"));

                Assert.That(host.TryOpen(), Is.False);
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
        public void NestedFactReadCannotPublishSecondTheaterCanvas()
        {
            var root = new GameObject("ST01 nested fact read");
            ST01TheaterChoiceHost host = null;
            bool attempted = false;
            try
            {
                host = new ST01TheaterChoiceHost(root.transform,
                    () => true, () => true, () => true, () => true, () => true, () => true,
                    () => false, () => false, () => true, key =>
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
                Assert.That(host.ActiveView, Is.Not.Null);
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
