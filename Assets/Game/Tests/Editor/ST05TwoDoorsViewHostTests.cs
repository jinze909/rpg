using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class ST05TwoDoorsViewHostTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        [Test]
        public void HostCannotOpenWithoutCourtyardPresenceOrCleanHomeStateKeys()
        {
            var root = new GameObject("ST05 host prerequisites");
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            bool atYard = false;
            ST05TwoDoorsHost host = null;
            try
            {
                host = new ST05TwoDoorsHost(
                    root.transform, () => atYard, () => true, () => true,
                    () => true, () => true, () => false, () => true, read, write);
                Assert.That(host.TryOpen(), Is.False);

                atYard = true;
                values[ST05TwoDoorsFacts.HomeIntentKey] = 1210;
                Assert.That(host.TryOpen(), Is.False);

                values[ST05TwoDoorsFacts.HomeIntentKey] = 0;
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
        public void FullFlowEnforcesHaulageMutualConsentAndChengOwnAssignmentOnce()
        {
            var root = new GameObject("ST05 host full flow");
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            bool haulagePaid = false;
            bool mutualConsent = false;
            ST05TwoDoorsHost host = null;
            ST05TwoDoorsHost duplicate = null;
            try
            {
                host = new ST05TwoDoorsHost(
                    root.transform, () => true, () => haulagePaid, () => true,
                    () => true, () => true, () => mutualConsent, () => true, read, write);
                Assert.That(host.TryOpen(), Is.True);
                var view = host.ActiveView;

                view.ReturnBedButton.onClick.Invoke();
                Assert.That(host.Session.Phase, Is.EqualTo(ST05TwoDoorsPhase.UnblockStaircase));

                haulagePaid = true;
                view.ReturnBedButton.onClick.Invoke();
                view.TestMarketGiftsButton.onClick.Invoke();
                view.ResolveCourtyardButton.onClick.Invoke();
                view.HearBothPartnersButton.onClick.Invoke();
                Assert.That(host.Session.Phase, Is.EqualTo(ST05TwoDoorsPhase.AgreeHomeAndKeys));
                Assert.That(view.TrialSharedHomeButton.interactable, Is.False);

                mutualConsent = true;
                view.Refresh();
                Assert.That(view.TrialSharedHomeButton.interactable, Is.True);
                view.TrialSharedHomeButton.onClick.Invoke();
                Assert.That(host.Session.Phase, Is.EqualTo(ST05TwoDoorsPhase.AwaitChengOwnJobDecision));

                view.ChengOutsideSurveyButton.onClick.Invoke();
                Assert.That(host.IsCompleted, Is.True);
                Assert.That(host.ActiveView, Is.Null);
                Assert.That(ST05TwoDoorsFacts.TryRead(read, out ST05TwoDoorsOutcome? saved), Is.True);
                Assert.That(saved.Value.Home.MutualTrialConsent, Is.True);
                Assert.That(saved.Value.Assignment, Is.EqualTo(ST05ChengAssignment.AcceptOutsideSurvey));
                Assert.That(saved.Value.OutsideWorkStarted, Is.False);

                duplicate = new ST05TwoDoorsHost(
                    root.transform, () => true, () => true, () => true,
                    () => true, () => true, () => true, () => true, read, write);
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
        public void InterruptedHomeAndAssignmentCommitsLockChoicesAndRecoverCleanly()
        {
            var root = new GameObject("ST05 host retry");
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            int homeFails = 0;
            int assignmentAckFails = 0;
            int assignmentConfirms = 0;
            Action<string, int> unstable = (k, v) =>
            {
                if (k == ST05TwoDoorsFacts.BedKey && homeFails++ == 0)
                    throw new InvalidOperationException("Interrupted home write");
                values[k] = v;
                if (k == ST05TwoDoorsFacts.AssignmentConfirmedKey && v == 1 && ++assignmentConfirms == 1)
                {
                    assignmentAckFails++;
                    throw new InvalidOperationException("Lost assignment ack");
                }
            };
            ST05TwoDoorsHost host = null;
            try
            {
                host = new ST05TwoDoorsHost(
                    root.transform, () => true, () => true, () => true,
                    () => true, () => true, () => true, () => true, read, unstable);
                Assert.That(host.TryOpen(), Is.True);
                var view = host.ActiveView;
                view.ConvertToBenchesButton.onClick.Invoke();
                view.TestMarketGiftsButton.onClick.Invoke();
                view.ResolveCourtyardButton.onClick.Invoke();
                view.HearBothPartnersButton.onClick.Invoke();

                Assert.Throws<InvalidOperationException>(() =>
                    view.KeepTwoDoorsButton.onClick.Invoke());
                Assert.That(view.TrialSharedHomeButton.interactable, Is.False);

                view.KeepTwoDoorsButton.onClick.Invoke();
                Assert.That(host.Session.Phase, Is.EqualTo(ST05TwoDoorsPhase.AwaitChengOwnJobDecision));

                Assert.Throws<InvalidOperationException>(() =>
                    view.ChengStayLocalButton.onClick.Invoke());
                Assert.That(assignmentAckFails, Is.EqualTo(1));
                Assert.That(host.IsCompleted, Is.True);

                view.ChengStayLocalButton.onClick.Invoke();
                Assert.That(assignmentConfirms, Is.EqualTo(1));
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
            var root = new GameObject("ST05 host guards");
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            string prevLang = SoundwellUiLocalization.Language;
            bool atYard = true;
            ST05TwoDoorsHost host = null;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                host = new ST05TwoDoorsHost(
                    root.transform, () => atYard, () => true, () => true,
                    () => true, () => true, () => false, () => true, read, write);
                Assert.That(host.TryOpen(), Is.True);
                var view = host.ActiveView;
                Assert.That(view.Heading.text, Does.Contain("STUCK STAIRCASE BED"));
                Assert.That(view.Panel.anchorMin.y, Is.EqualTo(0f));
                foreach (float aspect in new[] { 9f / 16f, 16f / 9f, 32f / 9f })
                    Assert.DoesNotThrow(() => view.LayoutForViewportAspect(aspect));

                view.RemoveDoorFrameButton.onClick.Invoke();
                view.TestMarketGiftsButton.onClick.Invoke();
                view.ResolveCourtyardButton.onClick.Invoke();
                view.HearBothPartnersButton.onClick.Invoke();
                view.KeepTwoDoorsButton.onClick.Invoke();
                var stayBtn = view.ChengStayLocalButton;

                atYard = false;
                stayBtn.onClick.Invoke();
                Assert.That(host.IsCompleted, Is.False);

                atYard = true;
                host.Dispose();
                stayBtn.onClick.Invoke();
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
