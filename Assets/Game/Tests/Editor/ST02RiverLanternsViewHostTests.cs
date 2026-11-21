using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class ST02RiverLanternsViewHostTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        [Test]
        public void HostCannotOpenWithoutDockPresenceOrCleanStateKeys()
        {
            var root = new GameObject("ST02 host prerequisites");
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            bool atDock = false;
            ST02RiverLanternsHost host = null;
            try
            {
                host = new ST02RiverLanternsHost(root.transform, () => atDock, read, write);
                Assert.That(host.TryOpen(), Is.False);

                atDock = true;
                values[ST02RiverLanternsFacts.ConfirmedKey] = 2;
                Assert.That(host.TryOpen(), Is.False);

                values[ST02RiverLanternsFacts.ConfirmedKey] = 0;
                values[ST02RiverLanternsFacts.PendingSignatureKey] = 2121;
                Assert.That(host.TryOpen(), Is.False);

                values[ST02RiverLanternsFacts.PendingSignatureKey] = 0;
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
        public void HostCannotStartWithOrphanedOutcomeFactEvenWhenIntentIsClear()
        {
            foreach (string orphanKey in new[]
            {
                ST02RiverLanternsFacts.InspectionsMaskKey,
                ST02RiverLanternsFacts.OriginalWordsRelayedKey,
                ST02RiverLanternsFacts.DockFeedbackKey,
                ST02RiverLanternsFacts.TrialDisclosureKey,
                ST02RiverLanternsFacts.BidModeKey,
                ST02RiverLanternsFacts.LockRescueKey
            })
                CheckOrphanedOutcomeFact(orphanKey);
        }

        private static void CheckOrphanedOutcomeFact(string orphanKey)
        {
            var root = new GameObject("ST02 host orphaned payload guard");
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            ST02RiverLanternsHost host = null;
            try
            {
                host = new ST02RiverLanternsHost(root.transform, () => true,
                    read, (key, value) => values[key] = value);
                values[orphanKey] = 1;
                Assert.That(ST02RiverLanternsFacts.TryRead(read, out _), Is.False);
                Assert.That(host.TryOpen(), Is.False,
                    "Orphaned facts from an incomplete reset must not reopen a doomed encounter.");
                Assert.That(host.ActiveView, Is.Null);

                values[orphanKey] = 0;
                Assert.That(host.TryOpen(), Is.True,
                    "A genuinely clean new-game reset still allows entry.");
                Assert.That(host.ActiveView, Is.Not.Null);
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void FullJointBidFlowInspectsBothBoatsRelaysWordsAndPersistsOnce()
        {
            var root = new GameObject("ST02 host joint bid");
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            ST02RiverLanternsHost host = null;
            ST02RiverLanternsHost duplicate = null;
            try
            {
                host = new ST02RiverLanternsHost(root.transform, () => true, read, write);
                Assert.That(host.TryOpen(), Is.True);
                var view = host.ActiveView;

                view.InspectXieZhiBoatButton.onClick.Invoke();
                Assert.That(host.Session.Stage, Is.EqualTo(ST02Stage.WorkshopInspection));
                view.InspectWenShengBoatButton.onClick.Invoke();
                Assert.That(host.Session.Stage, Is.EqualTo(ST02Stage.DockTrialAndFeedback));

                view.RelayOriginalWordsButton.onClick.Invoke();
                Assert.That(host.Session.OriginalWordsRelayed, Is.True);
                view.GatherDockFeedbackButton.onClick.Invoke();
                view.PublishToBothButton.onClick.Invoke();
                view.JointBidButton.onClick.Invoke();
                view.BraceHullStressButton.onClick.Invoke();

                Assert.That(host.IsCompleted, Is.True);
                Assert.That(host.ActiveView, Is.Null);
                Assert.That(ST02RiverLanternsFacts.TryRead(read, out ST02RiverLanternsRecord? saved), Is.True);
                Assert.That(saved.Value.OriginalWordsRelayed, Is.True);
                Assert.That(saved.Value.JointFreeRepairDisputeEligible, Is.True);
                Assert.That(saved.Value.LanternPreservedIntact, Is.True);

                duplicate = new ST02RiverLanternsHost(root.transform, () => true, read, write);
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
        public void InterruptedSaveAndLostAckRecoverSameChoiceOnlyWithoutDuplicateWrites()
        {
            var root = new GameObject("ST02 host retry");
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            int preCommitFails = 0;
            int confirmCommits = 0;
            Action<string, int> unstable = (k, v) =>
            {
                if (k == ST02RiverLanternsFacts.BidModeKey && preCommitFails++ == 0)
                    throw new InvalidOperationException("Interrupted before confirm");
                values[k] = v;
                if (k == ST02RiverLanternsFacts.ConfirmedKey && v == 1 && ++confirmCommits == 1)
                    throw new InvalidOperationException("Lost ack after confirm");
            };
            ST02RiverLanternsHost host = null;
            try
            {
                host = new ST02RiverLanternsHost(root.transform, () => true, read, unstable);
                Assert.That(host.TryOpen(), Is.True);
                var view = host.ActiveView;
                view.InspectXieZhiBoatButton.onClick.Invoke();
                view.InspectWenShengBoatButton.onClick.Invoke();
                view.GatherDockFeedbackButton.onClick.Invoke();
                view.ReturnPrivatelyButton.onClick.Invoke();
                view.IndependentBidButton.onClick.Invoke();

                Assert.Throws<InvalidOperationException>(() =>
                    view.DismantleLanternButton.onClick.Invoke());
                Assert.That(host.IsCompleted, Is.False);

                // Switching rescue method on torn write must fail closed.
                view.BraceHullStressButton.onClick.Invoke();
                Assert.That(host.IsCompleted, Is.False);

                Assert.Throws<InvalidOperationException>(() =>
                    view.DismantleLanternButton.onClick.Invoke());
                Assert.That(host.IsCompleted, Is.True);
                Assert.That(confirmCommits, Is.EqualTo(1));

                view.DismantleLanternButton.onClick.Invoke();
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
            var root = new GameObject("ST02 host guards");
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            string prevLang = SoundwellUiLocalization.Language;
            bool atDock = true;
            ST02RiverLanternsHost host = null;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                host = new ST02RiverLanternsHost(root.transform, () => atDock, read, write);
                Assert.That(host.TryOpen(), Is.True);
                var view = host.ActiveView;
                Assert.That(view.Heading.text, Does.Contain("TWO WORKSHOPS"));
                Assert.That(view.Panel.anchorMin.y, Is.EqualTo(0f));
                foreach (float aspect in new[] { 9f / 16f, 16f / 9f, 32f / 9f })
                    Assert.DoesNotThrow(() => view.LayoutForViewportAspect(aspect));

                view.InspectXieZhiBoatButton.onClick.Invoke();
                view.InspectWenShengBoatButton.onClick.Invoke();
                view.GatherDockFeedbackButton.onClick.Invoke();
                view.PublishToBothButton.onClick.Invoke();
                view.JointBidButton.onClick.Invoke();
                var braceBtn = view.BraceHullStressButton;

                atDock = false;
                braceBtn.onClick.Invoke();
                Assert.That(host.IsCompleted, Is.False);

                atDock = true;
                host.Dispose();
                braceBtn.onClick.Invoke();
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
