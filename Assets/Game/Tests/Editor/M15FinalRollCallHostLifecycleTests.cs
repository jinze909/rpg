using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class M15FinalRollCallHostLifecycleTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        [Test]
        public void ExternalViewDisposalAllowsCleanReopenAndRollCallSettlement()
        {
            var root = new GameObject("M15 host external dispose");
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            M15FinalRollCallHost host = null;
            try
            {
                host = new M15FinalRollCallHost(
                    root.transform, () => true, () => false, () => 4, null, read, write);
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

                reopenedView.HanQiaoReaderButton.onClick.Invoke();
                reopenedView.PublicTrueNameButton.onClick.Invoke();
                reopenedView.CarveNamesSteleButton.onClick.Invoke();
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
            var root = new GameObject("M15 host canvas destroy");
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            M15FinalRollCallHost host = null;
            try
            {
                host = new M15FinalRollCallHost(
                    root.transform, () => true, () => false, () => 2, () => "陈安", read, write);
                Assert.That(host.TryOpen(), Is.True);
                var firstView = host.ActiveView;
                Assert.That(firstView, Is.Not.Null);

                UnityEngine.Object.DestroyImmediate(firstView.Panel.parent.gameObject);
                Assert.That(firstView.IsDisposed, Is.True);
                Assert.That(host.ActiveView, Is.Null);

                Assert.That(host.TryOpen(), Is.True);
                var reopenedView = host.ActiveView;
                Assert.That(reopenedView, Is.Not.Null);

                reopenedView.ProtagonistReaderButton.onClick.Invoke();
                reopenedView.PrivateCustodianButton.onClick.Invoke();
                reopenedView.KeepOriginalSteleButton.onClick.Invoke();
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
            var root = new GameObject("M15 host parent destroy");
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            M15FinalRollCallHost host = null;
            try
            {
                host = new M15FinalRollCallHost(
                    root.transform, () => true, () => false, () => 2, null, read, write);
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
        public void OpeningReservesSlotAndExceptionAllowsRetry()
        {
            var root = new GameObject("M15 reentrant opening");
            var values = new Dictionary<string, int>();
            M15FinalRollCallHost host = null;
            bool throwOnce = true;
            bool nestedOpened = true;
            try
            {
                host = new M15FinalRollCallHost(root.transform, () =>
                {
                    if (throwOnce)
                    {
                        throwOnce = false;
                        throw new InvalidOperationException("ceremony proof failed");
                    }
                    nestedOpened = host.TryOpen();
                    return true;
                }, () => false, () => 4, null, Reader(values), (k, v) => values[k] = v);
                Assert.Throws<InvalidOperationException>(() => host.TryOpen());
                Assert.That(host.ActiveView, Is.Null);
                Assert.That(host.TryOpen(), Is.True);
                Assert.That(nestedOpened, Is.False);
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
            var root = new GameObject("M15 retired opening");
            var values = new Dictionary<string, int>();
            M15FinalRollCallHost host = null;
            try
            {
                host = new M15FinalRollCallHost(root.transform, () => true,
                    () => false, () => 4, () =>
                    {
                        host.Dispose();
                        return "Chen";
                    }, Reader(values), (k, v) => values[k] = v);
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
        public void ChangedArrivalDuringOpeningCannotPublishStaleCeremony()
        {
            var root = new GameObject("M15 changed arrival during opening");
            var values = new Dictionary<string, int>();
            bool changeArrival = true;
            try
            {
                using (var host = new M15FinalRollCallHost(root.transform,
                    () => true, () => false, () =>
                    {
                        if (changeArrival)
                        {
                            changeArrival = false;
                            values[M05LandingFacts.ArrivalSequenceKey] = 1;
                        }
                        return 4;
                    }, null, Reader(values), (k, v) => values[k] = v))
                {
                    Assert.That(host.TryOpen(), Is.False);
                    Assert.That(host.ActiveView, Is.Null);
                    Assert.That(host.TryOpen(), Is.True);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ChangedArrivalDuringFirstSettlementWriteStopsRemainingCeremonyFacts()
        {
            var root = new GameObject("M15 arrival changed during settlement");
            var values = new Dictionary<string, int>();
            var written = new List<string>();
            M15FinalRollCallHost host = null;
            try
            {
                host = new M15FinalRollCallHost(root.transform, () => true,
                    () => false, () => 4, null, Reader(values), (k, v) =>
                    {
                        written.Add(k);
                        values[k] = v;
                        if (k == M15FinalRollCallFacts.IdentityResponseKey)
                            values[M05LandingFacts.ArrivalSequenceKey] = 1;
                    });
                Assert.That(host.TryOpen(), Is.True);
                var view = host.ActiveView;
                var session = host.Session;
                view.HanQiaoReaderButton.onClick.Invoke();
                view.PublicTrueNameButton.onClick.Invoke();
                Assert.DoesNotThrow(() => view.CarveNamesSteleButton.onClick.Invoke());

                CollectionAssert.AreEqual(
                    new[] { M15FinalRollCallFacts.IdentityResponseKey }, written);
                Assert.That(Reader(values)(M15FinalRollCallFacts.ConfirmedKey), Is.Zero);
                Assert.That(host.IsCompleted, Is.False);
                Assert.That(session.IsCompleted, Is.False);
                Assert.That(session.CompletedRecord.HasValue, Is.False);
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ReplacedCeremonyDuringLiveConsentCannotWriteOrCloseReplacement()
        {
            var root = new GameObject("M15 replaced ceremony");
            var values = new Dictionary<string, int>();
            M15FinalRollCallHost host = null;
            bool replaceOnConsent = false;
            int writes = 0;
            try
            {
                host = new M15FinalRollCallHost(root.transform, () => true,
                    () => false, () =>
                    {
                        if (replaceOnConsent)
                        {
                            replaceOnConsent = false;
                            host.ActiveView.Dispose();
                            Assert.That(host.TryOpen(), Is.True);
                        }
                        return 4;
                    }, null, Reader(values), (k, v) => { writes++; values[k] = v; });
                Assert.That(host.TryOpen(), Is.True);
                var oldView = host.ActiveView;
                oldView.HanQiaoReaderButton.onClick.Invoke();
                oldView.PublicTrueNameButton.onClick.Invoke();
                replaceOnConsent = true;
                oldView.CarveNamesSteleButton.onClick.Invoke();

                Assert.That(writes, Is.Zero);
                Assert.That(host.IsCompleted, Is.False);
                var replacement = host.ActiveView;
                Assert.That(replacement, Is.Not.Null);
                Assert.That(replacement, Is.Not.SameAs(oldView));
                replacement.ProtagonistReaderButton.onClick.Invoke();
                replacement.PrivateCustodianButton.onClick.Invoke();
                replacement.KeepOriginalSteleButton.onClick.Invoke();
                Assert.That(host.IsCompleted, Is.True);
                Assert.That(host.ActiveView, Is.Null);
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
