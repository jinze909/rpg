using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class ST03BathhouseViewHostTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        [Test]
        public void HostCannotOpenWithoutPresenceOrCleanFactsState()
        {
            var root = new GameObject("ST03 host prerequisites");
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            bool atBathhouse = false;
            ST03BathhouseHost host = null;
            try
            {
                host = new ST03BathhouseHost(
                    root.transform, () => atBathhouse, () => true, () => true,
                    () => true, () => true, read, write);
                Assert.That(host.TryOpen(), Is.False);

                atBathhouse = true;
                values[ST03BathhouseFacts.ChoiceKey] = 1;
                Assert.That(host.TryOpen(), Is.False);

                values[ST03BathhouseFacts.ChoiceKey] = 0;
                Assert.That(host.TryOpen(), Is.True);
                Assert.That(host.TryOpen(), Is.False);
                Assert.That(host.ActiveView, Is.Not.Null);
                Assert.That(host.IsCompleted, Is.False);
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void FullFlowEnforcesAllCluesInterviewsRouteTrialBudgetAndPersistsOnce()
        {
            var root = new GameObject("ST03 host full flow");
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            bool interviewReady = false;
            bool budgetReady = false;
            ST03BathhouseHost host = null;
            ST03BathhouseHost duplicate = null;
            try
            {
                host = new ST03BathhouseHost(
                    root.transform, () => true, () => interviewReady, () => true,
                    () => true, () => budgetReady, read, write);
                Assert.That(host.TryOpen(), Is.True);
                var view = host.ActiveView;

                view.BoilerClueButton.onClick.Invoke();
                view.LockersClueButton.onClick.Invoke();
                view.LettersClueButton.onClick.Invoke();
                Assert.That(host.Session.Phase, Is.EqualTo(ST03BathhousePhase.MeetResidentsAtCareHome));

                view.InterviewWuSuButton.onClick.Invoke();
                Assert.That(host.Session.Phase, Is.EqualTo(ST03BathhousePhase.MeetResidentsAtCareHome));

                interviewReady = true;
                view.InterviewWuSuButton.onClick.Invoke();
                view.MakeSafeRouteButton.onClick.Invoke();
                view.CompleteTrialNightButton.onClick.Invoke();

                view.ReconcileBudgetButton.onClick.Invoke();
                Assert.That(host.Session.Phase, Is.EqualTo(ST03BathhousePhase.TrialReopening));

                budgetReady = true;
                view.ReconcileBudgetButton.onClick.Invoke();
                Assert.That(host.Session.Phase, Is.EqualTo(ST03BathhousePhase.CompareOperatingPlans));

                view.SellAndRelocateButton.onClick.Invoke();
                Assert.That(host.IsCompleted, Is.True);
                Assert.That(host.ActiveView, Is.Null);
                Assert.That(ST03BathhouseFacts.TryRead(read, out ST03BathhouseRecord? saved), Is.True);
                Assert.That(saved.Value.M15Sign, Is.EqualTo(ST03M15BathhouseSign.NoNewSignAtOldSite));
                Assert.That(saved.Value.WusuWorkplace, Is.EqualTo(ST03WusuWorkplace.CareHome));

                duplicate = new ST03BathhouseHost(
                    root.transform, () => true, () => true, () => true,
                    () => true, () => true, read, write);
                Assert.That(duplicate.TryOpen(), Is.False);
            }
            finally
            {
                duplicate?.Dispose();
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void InterruptedSaveAndLostAckLockChoiceAndRecoverWithoutDuplicateWrites()
        {
            var root = new GameObject("ST03 host retry");
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            int preCommitFails = 0;
            int confirmCommits = 0;
            Action<string, int> unstable = (k, v) =>
            {
                if (k == ST03BathhouseFacts.TrialNightKey && preCommitFails++ == 0)
                    throw new InvalidOperationException("Interrupted before confirm");
                values[k] = v;
                if (k == ST03BathhouseFacts.ConfirmedKey && v == 1 && ++confirmCommits == 1)
                    throw new InvalidOperationException("Lost ack after confirm");
            };
            ST03BathhouseHost host = null;
            try
            {
                host = new ST03BathhouseHost(
                    root.transform, () => true, () => true, () => true,
                    () => true, () => true, read, unstable);
                Assert.That(host.TryOpen(), Is.True);
                var view = host.ActiveView;
                view.BoilerClueButton.onClick.Invoke();
                view.LockersClueButton.onClick.Invoke();
                view.LettersClueButton.onClick.Invoke();
                view.InterviewWuSuButton.onClick.Invoke();
                view.MakeSafeRouteButton.onClick.Invoke();
                view.CompleteTrialNightButton.onClick.Invoke();
                view.ReconcileBudgetButton.onClick.Invoke();

                Assert.Throws<InvalidOperationException>(() =>
                    view.ReopenSubsidizedButton.onClick.Invoke());
                Assert.That(host.IsCompleted, Is.False);
                Assert.That(view.ElderBackroomButton.interactable, Is.False);

                Assert.Throws<InvalidOperationException>(() =>
                    view.ReopenSubsidizedButton.onClick.Invoke());
                Assert.That(host.IsCompleted, Is.True);
                Assert.That(confirmCommits, Is.EqualTo(1));

                view.ReopenSubsidizedButton.onClick.Invoke();
                Assert.That(confirmCommits, Is.EqualTo(1));
                Assert.That(host.ActiveView, Is.Null);
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void LocaleAspectPresenceWithdrawalAndDisposalGuardsWorkCleanly()
        {
            var root = new GameObject("ST03 host guards");
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            string prevLang = SoundwellUiLocalization.Language;
            bool atBathhouse = true;
            ST03BathhouseHost host = null;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                host = new ST03BathhouseHost(
                    root.transform, () => atBathhouse, () => true, () => true,
                    () => true, () => true, read, write);
                Assert.That(host.TryOpen(), Is.True);
                var view = host.ActiveView;
                Assert.That(view.Heading.text, Does.Contain("INSPECT CLOSED BATHHOUSE"));
                Assert.That(view.Panel.anchorMin.y, Is.EqualTo(0f));
                foreach (float aspect in new[] { 9f / 16f, 16f / 9f, 32f / 9f })
                    Assert.DoesNotThrow(() => view.LayoutForViewportAspect(aspect));

                view.BoilerClueButton.onClick.Invoke();
                view.LockersClueButton.onClick.Invoke();
                view.LettersClueButton.onClick.Invoke();
                view.InterviewWuSuButton.onClick.Invoke();
                view.MakeSafeRouteButton.onClick.Invoke();
                view.CompleteTrialNightButton.onClick.Invoke();
                view.ReconcileBudgetButton.onClick.Invoke();
                var backroomBtn = view.ElderBackroomButton;

                atBathhouse = false;
                backroomBtn.onClick.Invoke();
                Assert.That(host.IsCompleted, Is.False);

                atBathhouse = true;
                host.Dispose();
                backroomBtn.onClick.Invoke();
                Assert.That(host.IsDisposed, Is.True);
                Assert.That(host.IsCompleted, Is.False);
            }
            finally
            {
                host?.Dispose();
                SoundwellUiLocalization.SetLanguage(prevLang);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
