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

        private static void AssertLockedAndHiddenClicksIgnored(
            ST02RiverLanternsView view, CanvasGroup group, Dictionary<string, int> values)
        {
            var session = view.Session;
            var stage = session.Stage;
            var inspections = session.Inspections;
            bool relayed = session.OriginalWordsRelayed;
            bool feedback = session.DockFeedbackGathered;
            var disclosure = session.TrialDisclosure;
            var bid = session.BidMode;
            var rescue = session.LockRescue;
            Action clickBoth = () =>
            {
                view.InspectXieZhiBoatButton.onClick.Invoke();
                view.InspectWenShengBoatButton.onClick.Invoke();
            };
            Action assertUnchanged = () =>
            {
                Assert.That(session.Stage, Is.EqualTo(stage));
                Assert.That(session.Inspections, Is.EqualTo(inspections));
                Assert.That(session.OriginalWordsRelayed, Is.EqualTo(relayed));
                Assert.That(session.DockFeedbackGathered, Is.EqualTo(feedback));
                Assert.That(session.TrialDisclosure, Is.EqualTo(disclosure));
                Assert.That(session.BidMode, Is.EqualTo(bid));
                Assert.That(session.LockRescue, Is.EqualTo(rescue));
                Assert.That(values, Is.Empty);
            };
            group.interactable = false;
            clickBoth();
            assertUnchanged();
            group.interactable = true;
            view.Panel.gameObject.SetActive(false);
            clickBoth();
            assertUnchanged();
            view.Panel.gameObject.SetActive(true);
        }

        [Test]
        public void EffectiveInputLockPreservesPrivateTrialIndependentBidAndExplicitRescue()
        {
            var root = new GameObject("ST02 effective input lock");
            var values = new Dictionary<string, int>();
            ST02RiverLanternsView view = null;
            try
            {
                var session = new ST02RiverLanternsSession();
                int submissions = 0;
                view = new ST02RiverLanternsView(root.transform, session, method =>
                {
                    submissions++;
                    Assert.That(session.TryResolveOldLockAndCommit(
                        method, Reader(values), (k, v) => values[k] = v), Is.True);
                });
                var group = root.AddComponent<CanvasGroup>();

                AssertLockedAndHiddenClicksIgnored(view, group, values);
                view.InspectXieZhiBoatButton.onClick.Invoke();
                AssertLockedAndHiddenClicksIgnored(view, group, values);
                view.InspectWenShengBoatButton.onClick.Invoke();
                Assert.That(session.Stage, Is.EqualTo(ST02Stage.DockTrialAndFeedback));

                AssertLockedAndHiddenClicksIgnored(view, group, values);
                view.RelayOriginalWordsButton.onClick.Invoke();
                AssertLockedAndHiddenClicksIgnored(view, group, values);
                view.GatherDockFeedbackButton.onClick.Invoke();
                Assert.That(session.Stage, Is.EqualTo(ST02Stage.TrialDisclosureChoice));

                AssertLockedAndHiddenClicksIgnored(view, group, values);
                view.ReturnPrivatelyButton.onClick.Invoke();
                Assert.That(session.Stage, Is.EqualTo(ST02Stage.BiddingDecision));

                AssertLockedAndHiddenClicksIgnored(view, group, values);
                view.IndependentBidButton.onClick.Invoke();
                Assert.That(session.Stage, Is.EqualTo(ST02Stage.OldLockRescue));

                AssertLockedAndHiddenClicksIgnored(view, group, values);
                Assert.That(submissions, Is.Zero);
                Assert.That(view.DismantleLanternButton.IsInteractable(), Is.True);
                view.DismantleLanternButton.onClick.Invoke();
                view.BraceHullStressButton.onClick.Invoke();

                Assert.That(submissions, Is.EqualTo(1));
                Assert.That(session.IsCompleted, Is.True);
                Assert.That(ST02RiverLanternsFacts.TryRead(Reader(values),
                    out ST02RiverLanternsRecord? saved), Is.True);
                Assert.That(saved.HasValue, Is.True);
                Assert.That(saved.Value.Inspections, Is.EqualTo(ST02InspectionFlags.All));
                Assert.That(saved.Value.OriginalWordsRelayed, Is.True);
                Assert.That(saved.Value.DockFeedbackGathered, Is.True);
                Assert.That(saved.Value.TrialDisclosure, Is.EqualTo(ST02TrialDisclosure.ReturnPrivatelyToEach));
                Assert.That(saved.Value.BidMode, Is.EqualTo(ST02BidMode.IndependentWithSubcontract));
                Assert.That(saved.Value.LockRescue, Is.EqualTo(ST02LockRescueMethod.DismantleLanternFrame));
            }
            finally
            {
                view?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
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
