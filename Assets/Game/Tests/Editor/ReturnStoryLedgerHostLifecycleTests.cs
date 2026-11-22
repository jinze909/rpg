using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class ReturnStoryLedgerHostLifecycleTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        [Test]
        public void DisposingViewExternallyClearsHostOpenStateAndAllowsCleanReopen()
        {
            var root = new GameObject("LedgerLifecycleReopenRoot");
            var values = new Dictionary<string, int>();
            try
            {
                using (var host = new ReturnStoryLedgerHost(root.transform, Reader(values)))
                {
                    Assert.That(host.TryOpenOrRefresh(), Is.True);
                    var firstView = host.ActiveView;
                    Assert.That(firstView, Is.Not.Null);
                    Assert.That(host.IsOpen, Is.True);

                    firstView.Dispose();
                    Assert.That(firstView.IsDisposed, Is.True);
                    Assert.That(host.IsOpen, Is.False);
                    Assert.That(host.ActiveView, Is.Null);

                    Assert.That(host.TryOpenOrRefresh(), Is.True);
                    Assert.That(host.IsOpen, Is.True);
                    Assert.That(host.ActiveView, Is.Not.Null);
                    Assert.That(host.ActiveView, Is.Not.SameAs(firstView));
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DestroyingParentClearsHostOpenStateAndBlocksReopenWithoutThrowing()
        {
            var root = new GameObject("LedgerLifecycleDestroyedParentRoot");
            var values = new Dictionary<string, int>();
            ReturnStoryLedgerHost host = null;
            try
            {
                host = new ReturnStoryLedgerHost(root.transform, Reader(values));
                Assert.That(host.TryOpenOrRefresh(), Is.True);
                Assert.That(host.IsOpen, Is.True);

                UnityEngine.Object.DestroyImmediate(root);
                root = null;

                Assert.That(host.IsOpen, Is.False);
                Assert.That(host.ActiveView, Is.Null);
                Assert.That(host.TryOpenOrRefresh(), Is.False);
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
