using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class M16EpilogueHostLifecycleTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        private static void SeedM15(Dictionary<string, int> values)
        {
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            Assert.That(M15FinalRollCallFacts.TryRecord(new M15FinalRollCallRecord(
                M15RollCallReader.Protagonist,
                M15FinalIdentityResponse.PublicTrueName,
                M15PensionClosure.StoppedAndReconciled,
                M15SteleInscription.AddReturnedAliveLine,
                consentedVerifiedNamesCount: 0,
                xiaoqiCalledByNumberSeventyTwo: false), read, write), Is.True);
        }

        [Test]
        public void ExternalViewDisposalAllowsCleanReopenAndEpilogueSettlement()
        {
            var root = new GameObject("M16 host external dispose");
            var values = new Dictionary<string, int>();
            SeedM15(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            M16EpilogueHost host = null;
            try
            {
                host = new M16EpilogueHost(
                    root.transform, () => true, () => true,
                    () => M16RespectAgencyTone.RespectedOthersChoices, read, write);
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

                reopenedView.ProceedToDeskButton.onClick.Invoke();
                reopenedView.StepOutsideButton.onClick.Invoke();
                reopenedView.PierButton.onClick.Invoke();
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
            var root = new GameObject("M16 host canvas destroy");
            var values = new Dictionary<string, int>();
            SeedM15(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            M16EpilogueHost host = null;
            try
            {
                host = new M16EpilogueHost(
                    root.transform, () => true, () => true,
                    () => M16RespectAgencyTone.NeutralMixedExperience, read, write);
                Assert.That(host.TryOpen(), Is.True);
                var firstView = host.ActiveView;
                Assert.That(firstView, Is.Not.Null);

                UnityEngine.Object.DestroyImmediate(firstView.Panel.parent.gameObject);
                Assert.That(firstView.IsDisposed, Is.True);
                Assert.That(host.ActiveView, Is.Null);

                Assert.That(host.TryOpen(), Is.True);
                var reopenedView = host.ActiveView;
                Assert.That(reopenedView, Is.Not.Null);

                reopenedView.ProceedToDeskButton.onClick.Invoke();
                reopenedView.StepOutsideButton.onClick.Invoke();
                reopenedView.SteleButton.onClick.Invoke();
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
        public void AfternoonCallbackClosingHostCannotOpenEpilogue()
        {
            var root = new GameObject("M16 afternoon closes opening");
            var values = new Dictionary<string, int>();
            SeedM15(values);
            M16EpilogueHost host = null;
            try
            {
                host = new M16EpilogueHost(root.transform, () =>
                {
                    host.Dispose();
                    return true;
                }, () => true, () => M16RespectAgencyTone.RespectedOthersChoices,
                    Reader(values), (k, v) => values[k] = v);
                Assert.That(host.TryOpen(), Is.False);
                Assert.That(host.ActiveView, Is.Null);
                Assert.That(host.IsCompleted, Is.False);
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void AfternoonCallbackClosingEpilogueCannotPersistStandingSpot()
        {
            AssertSceneCallbackClosingEpilogueCannotPersistStandingSpot(0);
        }

        [Test]
        public void WillingnessCallbackClosingEpilogueCannotPersistStandingSpot()
        {
            AssertSceneCallbackClosingEpilogueCannotPersistStandingSpot(1);
        }

        [Test]
        public void ClosingToneCallbackClosingEpilogueCannotPersistStandingSpot()
        {
            AssertSceneCallbackClosingEpilogueCannotPersistStandingSpot(2);
        }

        private static void AssertSceneCallbackClosingEpilogueCannotPersistStandingSpot(int closeCallback)
        {
            var root = new GameObject("M16 callback closes settlement");
            var values = new Dictionary<string, int>();
            SeedM15(values);
            M16EpilogueHost host = null;
            bool closeOnRead = false;
            int writes = 0;
            try
            {
                host = new M16EpilogueHost(root.transform, () =>
                {
                    if (closeOnRead && closeCallback == 0) host.Dispose();
                    return true;
                }, () =>
                {
                    if (closeOnRead && closeCallback == 1) host.Dispose();
                    return true;
                }, () =>
                {
                    if (closeOnRead && closeCallback == 2) host.Dispose();
                    return M16RespectAgencyTone.RespectedOthersChoices;
                }, Reader(values), (k, v) =>
                {
                    writes++;
                    values[k] = v;
                });
                Assert.That(host.TryOpen(), Is.True);
                var view = host.ActiveView;
                view.ProceedToDeskButton.onClick.Invoke();
                view.StepOutsideButton.onClick.Invoke();
                var button = view.PierButton;
                closeOnRead = true;
                Assert.DoesNotThrow(() => button.onClick.Invoke());
                Assert.That(host.IsDisposed, Is.True);
                Assert.That(host.ActiveView, Is.Null);
                Assert.That(host.IsCompleted, Is.False);
                Assert.That(writes, Is.Zero);
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RetiredEpilogueCannotWriteRemainingFactsOrConfirm(bool retireAfterFirstWrite)
        {
            var root = new GameObject("M16 retirement during fact commit");
            var values = new Dictionary<string, int>();
            SeedM15(values);
            Func<string, int> read = Reader(values);
            M16EpilogueHost host = null;
            bool submitting = false;
            int writes = 0;
            int retirements = 0;
            try
            {
                host = new M16EpilogueHost(root.transform, () => true, () => true,
                    () => M16RespectAgencyTone.RespectedOthersChoices, key =>
                    {
                        if (submitting && !retireAfterFirstWrite &&
                            retirements == 0 && key == M16EpilogueFacts.ConfirmedKey)
                        {
                            retirements++;
                            host.Dispose();
                        }
                        return read(key);
                    }, (key, value) =>
                    {
                        writes++;
                        values[key] = value;
                        if (submitting && retireAfterFirstWrite && writes == 1)
                        {
                            retirements++;
                            host.Dispose();
                        }
                    });
                Assert.That(host.TryOpen(), Is.True);
                var view = host.ActiveView;
                view.ProceedToDeskButton.onClick.Invoke();
                view.StepOutsideButton.onClick.Invoke();
                submitting = true;
                Assert.DoesNotThrow(() => view.PierButton.onClick.Invoke());
                Assert.That(retirements, Is.EqualTo(1));
                Assert.That(writes, Is.EqualTo(retireAfterFirstWrite ? 1 : 0));
                Assert.That(read(M16EpilogueFacts.ConfirmedKey), Is.Zero);
                Assert.That(read(M16EpilogueFacts.ClosingToneKey), Is.Zero);
                Assert.That(host.IsCompleted, Is.False);
                Assert.That(host.IsDisposed, Is.True);
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
            var root = new GameObject("M16 host parent destroy");
            var values = new Dictionary<string, int>();
            SeedM15(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            M16EpilogueHost host = null;
            try
            {
                host = new M16EpilogueHost(
                    root.transform, () => true, () => true,
                    () => M16RespectAgencyTone.RespectedOthersChoices, read, write);
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
