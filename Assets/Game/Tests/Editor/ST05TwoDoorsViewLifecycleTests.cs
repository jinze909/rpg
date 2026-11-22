using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class ST05TwoDoorsViewLifecycleTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        private static ST05TwoDoorsSession CreateSession(Dictionary<string, int> values)
        {
            Assert.That(ST05TwoDoorsSession.TryCreate(
                Reader(values), out ST05TwoDoorsSession session), Is.True);
            return session;
        }

        private static void AdvanceToAgreeHome(ST05TwoDoorsView view)
        {
            view.ConvertToBenchesButton.onClick.Invoke();
            view.TestMarketGiftsButton.onClick.Invoke();
            view.ResolveCourtyardButton.onClick.Invoke();
            view.HearBothPartnersButton.onClick.Invoke();
        }

        [Test]
        public void PartialChildTeardownDisposesViewAndBlocksClickLayoutAndLocaleRefresh()
        {
            var root = new GameObject("ST05 view partial teardown");
            var values = new Dictionary<string, int>();
            string previousLanguage = SoundwellUiLocalization.Language;
            ST05TwoDoorsView view = null;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
                ST05TwoDoorsSession session = CreateSession(values);
                int homeCalls = 0;
                view = new ST05TwoDoorsView(
                    root.transform, session, () => true, () => true, () => true,
                    () => true, () => true, () => true, () => homeCalls++, () => { });

                var benchBtn = view.ConvertToBenchesButton;
                UnityEngine.Object.DestroyImmediate(view.Heading.gameObject);
                Assert.That(view.IsDisposed, Is.True);

                Assert.DoesNotThrow(() => benchBtn.onClick.Invoke());
                Assert.That(homeCalls, Is.EqualTo(0));
                Assert.That(session.Phase, Is.EqualTo(ST05TwoDoorsPhase.UnblockStaircase));
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
            var root = new GameObject("ST05 view invalid aspects");
            var values = new Dictionary<string, int>();
            ST05TwoDoorsView view = null;
            try
            {
                ST05TwoDoorsSession session = CreateSession(values);
                view = new ST05TwoDoorsView(
                    root.transform, session, () => true, () => true, () => true,
                    () => true, () => true, () => true, () => { }, () => { });
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
        public void ReentrantHomeAndAssignmentSubmitsAreIgnoredDuringCallbacks()
        {
            var root = new GameObject("ST05 view reentrant submits");
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            ST05TwoDoorsView view = null;
            try
            {
                ST05TwoDoorsSession session = CreateSession(values);
                int homeCalls = 0;
                int jobCalls = 0;
                view = new ST05TwoDoorsView(
                    root.transform, session, () => true, () => true, () => true,
                    () => true, () => true, () => true,
                    () =>
                    {
                        homeCalls++;
                        Assert.That(view.KeepTwoDoorsButton.interactable, Is.False);
                        view.KeepTwoDoorsButton.onClick.Invoke();
                        Assert.That(session.TryCommitLivingAgreement(read, write), Is.True);
                    },
                    () =>
                    {
                        jobCalls++;
                        Assert.That(view.ChengStayLocalButton.interactable, Is.False);
                        view.ChengStayLocalButton.onClick.Invoke();
                        Assert.That(session.TryCommitChengAssignment(read, write), Is.True);
                    });

                AdvanceToAgreeHome(view);
                view.KeepTwoDoorsButton.onClick.Invoke();
                Assert.That(homeCalls, Is.EqualTo(1));
                Assert.That(session.Phase, Is.EqualTo(ST05TwoDoorsPhase.AwaitChengOwnJobDecision));

                view.ChengStayLocalButton.onClick.Invoke();
                Assert.That(jobCalls, Is.EqualTo(1));
                Assert.That(session.Phase, Is.EqualTo(ST05TwoDoorsPhase.Completed));
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
            var root = new GameObject("ST05 view dispose in callback");
            var values = new Dictionary<string, int>();
            ST05TwoDoorsView view = null;
            try
            {
                ST05TwoDoorsSession session = CreateSession(values);
                view = new ST05TwoDoorsView(
                    root.transform, session, () => true, () => true, () => true,
                    () => true, () => true, () => true,
                    () =>
                    {
                        view.Dispose();
                        throw new InvalidOperationException("Simulated ST05 teardown during submit");
                    },
                    () => { });

                AdvanceToAgreeHome(view);
                Assert.Throws<InvalidOperationException>(() => view.KeepTwoDoorsButton.onClick.Invoke());
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
