using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class ST02RiverLanternsViewLifecycleTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        private static void AdvanceToOldLockRescue(ST02RiverLanternsView view)
        {
            view.InspectXieZhiBoatButton.onClick.Invoke();
            view.InspectWenShengBoatButton.onClick.Invoke();
            view.GatherDockFeedbackButton.onClick.Invoke();
            view.PublishToBothButton.onClick.Invoke();
            view.JointBidButton.onClick.Invoke();
        }

        [Test]
        public void PartialChildTeardownDisposesViewAndBlocksClickLayoutAndLocaleRefresh()
        {
            var root = new GameObject("ST02 view partial teardown");
            string previousLanguage = SoundwellUiLocalization.Language;
            ST02RiverLanternsView view = null;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
                var session = new ST02RiverLanternsSession();
                int calls = 0;
                view = new ST02RiverLanternsView(root.transform, session, _ => calls++);

                var inspectBtn = view.InspectXieZhiBoatButton;
                UnityEngine.Object.DestroyImmediate(view.Heading.gameObject);
                Assert.That(view.IsDisposed, Is.True);

                Assert.DoesNotThrow(() => inspectBtn.onClick.Invoke());
                Assert.That(calls, Is.EqualTo(0));
                Assert.That(session.Inspections, Is.EqualTo(ST02InspectionFlags.None));
                Assert.That(view.Panel == null, Is.True);

                Assert.DoesNotThrow(() => view.LayoutForViewportAspect(16f / 9f));
                Assert.DoesNotThrow(() => SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English));
            }
            finally
            {
                view?.Dispose();
                SoundwellUiLocalization.SetLanguage(previousLanguage);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void InvalidViewportAspectsAreIgnoredWithoutDistortingPanelScale()
        {
            var root = new GameObject("ST02 view invalid aspects");
            ST02RiverLanternsView view = null;
            try
            {
                var session = new ST02RiverLanternsSession();
                view = new ST02RiverLanternsView(root.transform, session, _ => { });
                view.LayoutForViewportAspect(16f / 9f);
                Vector3 baselineScale = view.Panel.localScale;

                foreach (float invalidAspect in new[]
                    { 0f, -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
                {
                    Assert.DoesNotThrow(() => view.LayoutForViewportAspect(invalidAspect));
                    Assert.That(view.Panel.localScale, Is.EqualTo(baselineScale));
                }
            }
            finally
            {
                view?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ReentrantRescueSubmitClickIsIgnoredDuringSubmissionCallback()
        {
            var root = new GameObject("ST02 view reentrant submit");
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            ST02RiverLanternsView view = null;
            try
            {
                var session = new ST02RiverLanternsSession();
                int invocations = 0;
                view = new ST02RiverLanternsView(root.transform, session, method =>
                {
                    invocations++;
                    Assert.That(view.BraceHullStressButton.interactable, Is.False);
                    view.BraceHullStressButton.onClick.Invoke();
                    Assert.That(session.TryResolveOldLockAndCommit(method, read, write), Is.True);
                });

                AdvanceToOldLockRescue(view);
                view.BraceHullStressButton.onClick.Invoke();

                Assert.That(invocations, Is.EqualTo(1));
                Assert.That(session.IsCompleted, Is.True);
            }
            finally
            {
                view?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DisposingViewInsideThrowingCallbackDoesNotThrowOnFinallyRefresh()
        {
            var root = new GameObject("ST02 view dispose in callback");
            ST02RiverLanternsView view = null;
            try
            {
                var session = new ST02RiverLanternsSession();
                view = new ST02RiverLanternsView(root.transform, session, _ =>
                {
                    view.Dispose();
                    throw new InvalidOperationException("Simulated ST02 teardown during submit");
                });

                AdvanceToOldLockRescue(view);
                Assert.Throws<InvalidOperationException>(() => view.DismantleLanternButton.onClick.Invoke());
                Assert.That(view.IsDisposed, Is.True);
                Assert.DoesNotThrow(() => view.Refresh());
                Assert.DoesNotThrow(() => view.LayoutForViewportAspect(16f / 9f));
            }
            finally
            {
                view?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
