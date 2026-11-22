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
