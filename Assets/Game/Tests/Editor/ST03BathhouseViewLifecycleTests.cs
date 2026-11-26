using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class ST03BathhouseViewLifecycleTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        private static ST03BathhouseSession CreateSession(Dictionary<string, int> values)
        {
            Assert.That(ST03BathhouseSession.TryCreate(
                Reader(values), out ST03BathhouseSession session), Is.True);
            return session;
        }

        private static void AdvanceToComparePlans(ST03BathhouseView view)
        {
            view.BoilerClueButton.onClick.Invoke();
            view.LockersClueButton.onClick.Invoke();
            view.LettersClueButton.onClick.Invoke();
            view.InterviewWuSuButton.onClick.Invoke();
            view.MakeSafeRouteButton.onClick.Invoke();
            view.CompleteTrialNightButton.onClick.Invoke();
            view.ReconcileBudgetButton.onClick.Invoke();
        }

        [Test]
        public void PartialChildTeardownDisposesViewAndBlocksClickLayoutAndLocaleRefresh()
        {
            var root = new GameObject("ST03 view partial teardown");
            var values = new Dictionary<string, int>();
            string previousLanguage = SoundwellUiLocalization.Language;
            ST03BathhouseView view = null;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
                ST03BathhouseSession session = CreateSession(values);
                int calls = 0;
                view = new ST03BathhouseView(
                    root.transform, session, () => true, () => true, () => true, () => true, () => calls++);

                var boilerBtn = view.BoilerClueButton;
                UnityEngine.Object.DestroyImmediate(view.Heading.gameObject);
                Assert.That(view.IsDisposed, Is.True);

                Assert.DoesNotThrow(() => boilerBtn.onClick.Invoke());
                Assert.That(calls, Is.EqualTo(0));
                Assert.That(session.InspectedClueCount, Is.EqualTo(0));
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
            var root = new GameObject("ST03 view invalid aspects");
            var values = new Dictionary<string, int>();
            ST03BathhouseView view = null;
            try
            {
                ST03BathhouseSession session = CreateSession(values);
                view = new ST03BathhouseView(
                    root.transform, session, () => true, () => true, () => true, () => true, () => { });
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

        private static void AssertLockedChoiceDoesNotAdvance(
            ST03BathhouseView view, CanvasGroup group, UnityEngine.UI.Button button,
            Dictionary<string, int> values)
        {
            ST03BathhouseSession session = view.Session;
            ST03BathhousePhase phase = session.Phase;
            int clues = session.InspectedClueCount;
            bool trial = session.TrialCompleted;
            group.interactable = false;
            Assert.That(button.IsInteractable(), Is.False);
            button.onClick.Invoke();
            group.interactable = true;

            view.Panel.gameObject.SetActive(false);
            button.onClick.Invoke();
            view.Panel.gameObject.SetActive(true);
            Assert.That(session.Phase, Is.EqualTo(phase));
            Assert.That(session.InspectedClueCount, Is.EqualTo(clues));
            Assert.That(session.TrialCompleted, Is.EqualTo(trial));
            Assert.That(values, Is.Empty);
        }

        [Test]
        public void EffectiveInputLockPreservesBathhouseEvidenceAndRelocationOutcome()
        {
            var root = new GameObject("ST03 effective input lock");
            var values = new Dictionary<string, int>();
            ST03BathhouseView view = null;
            try
            {
                ST03BathhouseSession session = CreateSession(values);
                int commits = 0;
                view = new ST03BathhouseView(root.transform, session,
                    () => true, () => true, () => true, () => true, () =>
                    {
                        commits++;
                        Assert.That(session.TryCommit(Reader(values), (k, v) => values[k] = v), Is.True);
                    });
                var group = root.AddComponent<CanvasGroup>();
                foreach (var button in new[]
                {
                    view.BoilerClueButton, view.LockersClueButton, view.LettersClueButton
                })
                {
                    AssertLockedChoiceDoesNotAdvance(view, group, button, values);
                    button.onClick.Invoke();
                }

                Assert.That(session.Phase, Is.EqualTo(ST03BathhousePhase.MeetResidentsAtCareHome));
                AssertLockedChoiceDoesNotAdvance(view, group, view.InterviewWuSuButton, values);
                view.InterviewWuSuButton.onClick.Invoke();
                Assert.That(session.Phase, Is.EqualTo(ST03BathhousePhase.RepairRouteAndTransport));
                AssertLockedChoiceDoesNotAdvance(view, group, view.MakeSafeRouteButton, values);
                view.MakeSafeRouteButton.onClick.Invoke();
                Assert.That(session.Phase, Is.EqualTo(ST03BathhousePhase.TrialReopening));
                AssertLockedChoiceDoesNotAdvance(view, group, view.CompleteTrialNightButton, values);
                view.CompleteTrialNightButton.onClick.Invoke();
                Assert.That(session.TrialCompleted, Is.True);
                AssertLockedChoiceDoesNotAdvance(view, group, view.ReconcileBudgetButton, values);
                view.ReconcileBudgetButton.onClick.Invoke();
                Assert.That(session.Phase, Is.EqualTo(ST03BathhousePhase.CompareOperatingPlans));

                AssertLockedChoiceDoesNotAdvance(view, group, view.ReopenSubsidizedButton, values);
                AssertLockedChoiceDoesNotAdvance(view, group, view.ElderBackroomButton, values);
                AssertLockedChoiceDoesNotAdvance(view, group, view.SellAndRelocateButton, values);
                Assert.That(commits, Is.Zero);

                view.SellAndRelocateButton.onClick.Invoke();
                Assert.That(commits, Is.EqualTo(1));
                Assert.That(ST03BathhouseFacts.TryRead(Reader(values), out ST03BathhouseRecord? saved), Is.True);
                Assert.That(saved.HasValue, Is.True);
                Assert.That(saved.Value.M15Sign, Is.EqualTo(ST03M15BathhouseSign.NoNewSignAtOldSite));
                Assert.That(saved.Value.WusuWorkplace, Is.EqualTo(ST03WusuWorkplace.CareHome));
            }
            finally
            {
                view?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ReentrantPlanSubmitClickIsIgnoredDuringSubmissionCallback()
        {
            var root = new GameObject("ST03 view reentrant submit");
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            ST03BathhouseView view = null;
            try
            {
                ST03BathhouseSession session = CreateSession(values);
                int invocations = 0;
                view = new ST03BathhouseView(
                    root.transform, session, () => true, () => true, () => true, () => true, () =>
                    {
                        invocations++;
                        Assert.That(view.ReopenSubsidizedButton.interactable, Is.False);
                        view.ReopenSubsidizedButton.onClick.Invoke();
                        Assert.That(session.TryCommit(read, write), Is.True);
                    });

                AdvanceToComparePlans(view);
                view.ReopenSubsidizedButton.onClick.Invoke();

                Assert.That(invocations, Is.EqualTo(1));
                Assert.That(session.Phase, Is.EqualTo(ST03BathhousePhase.Completed));
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
            var root = new GameObject("ST03 view dispose in callback");
            var values = new Dictionary<string, int>();
            ST03BathhouseView view = null;
            try
            {
                ST03BathhouseSession session = CreateSession(values);
                view = new ST03BathhouseView(
                    root.transform, session, () => true, () => true, () => true, () => true, () =>
                    {
                        view.Dispose();
                        throw new InvalidOperationException("Simulated ST03 teardown during submit");
                    });

                AdvanceToComparePlans(view);
                Assert.Throws<InvalidOperationException>(() => view.ElderBackroomButton.onClick.Invoke());
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
