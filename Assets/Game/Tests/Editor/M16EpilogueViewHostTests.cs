using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class M16EpilogueViewHostTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        private static void SeedM15(
            Dictionary<string, int> values,
            bool seedST02Completed = false)
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

            if (seedST02Completed)
            {
                Assert.That(ST02RiverLanternsFacts.TryRecord(new ST02RiverLanternsRecord(
                    ST02InspectionFlags.All, true, true,
                    ST02TrialDisclosure.PublishToBoth,
                    ST02BidMode.JointBidWithWrittenTerms,
                    ST02LockRescueMethod.BraceHullStressPoint), read, write), Is.True);
            }
        }

        [Test]
        public void HostCannotOpenWithoutM15RollCallPresenceOrCleanConfirmedKey()
        {
            var root = new GameObject("M16 host prerequisites");
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            bool atAfternoon = false;
            M16EpilogueHost host = null;
            try
            {
                host = new M16EpilogueHost(
                    root.transform, () => atAfternoon, () => true,
                    () => M16RespectAgencyTone.RespectedOthersChoices, read, write);
                Assert.That(host.TryOpen(), Is.False);

                SeedM15(values);
                Assert.That(host.TryOpen(), Is.False);

                atAfternoon = true;
                values[M16EpilogueFacts.ConfirmedKey] = 2;
                Assert.That(host.TryOpen(), Is.False);

                values[M16EpilogueFacts.ConfirmedKey] = 0;
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
        public void FullEpilogueFlowVisitsStopsReadsOnlyEligibleLettersAndPersistsOnce()
        {
            var root = new GameObject("M16 host full flow");
            var values = new Dictionary<string, int>();
            SeedM15(values, seedST02Completed: true);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            M16EpilogueHost host = null;
            M16EpilogueHost duplicate = null;
            try
            {
                host = new M16EpilogueHost(
                    root.transform, () => true, () => true,
                    () => M16RespectAgencyTone.RespectedOthersChoices, read, write);
                Assert.That(host.TryOpen(), Is.True);
                var view = host.ActiveView;

                view.VisitWorkshopButton.onClick.Invoke();
                view.VisitQiQueButton.onClick.Invoke();
                view.VisitOulinAndWestLaneButton.onClick.Invoke();
                view.VisitOulinAndWestLaneButton.onClick.Invoke();
                Assert.That(host.Session.StopsVisited, Is.EqualTo(M16WalkStops.All));

                view.ProceedToDeskButton.onClick.Invoke();
                Assert.That(host.Session.Phase, Is.EqualTo(M16Phase.DeskLettersAndRegister));
                Assert.That(view.ReadNextLetterButton.interactable, Is.True);

                view.ReadNextLetterButton.onClick.Invoke();
                Assert.That(host.Session.LettersRead, Is.EqualTo(M16DeliveredLettersFlags.ST02RiverLanterns));
                Assert.That(view.ReadNextLetterButton.interactable, Is.False);

                view.ReviewRegisterButton.onClick.Invoke();
                view.StepOutsideButton.onClick.Invoke();
                Assert.That(host.Session.Phase, Is.EqualTo(M16Phase.FinalStandingAndFadeOut));

                view.PierButton.onClick.Invoke();
                Assert.That(host.IsCompleted, Is.True);
                Assert.That(host.ActiveView, Is.Null);
                Assert.That(M16EpilogueFacts.TryRead(read, out M16EpilogueRecord? saved), Is.True);
                Assert.That(saved.Value.StopsVisited, Is.EqualTo(M16WalkStops.All));
                Assert.That(saved.Value.DeskRegisterReviewed, Is.True);
                Assert.That(saved.Value.ShenLiSpokeClosingLine, Is.True);

                duplicate = new M16EpilogueHost(
                    root.transform, () => true, () => true,
                    () => M16RespectAgencyTone.RespectedOthersChoices, read, write);
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
        public void DeclinedMeetingNeverAttributesClosingLineAndInterruptedSaveRecoversCleanly()
        {
            var root = new GameObject("M16 host declined retry");
            var values = new Dictionary<string, int>();
            SeedM15(values);
            Func<string, int> read = Reader(values);
            int preCommitFails = 0;
            int confirmCommits = 0;
            Action<string, int> unstable = (k, v) =>
            {
                if (k == M16EpilogueFacts.StandingSpotKey && preCommitFails++ == 0)
                    throw new InvalidOperationException("Interrupted before confirm");
                values[k] = v;
                if (k == M16EpilogueFacts.ConfirmedKey && v == 1 && ++confirmCommits == 1)
                    throw new InvalidOperationException("Lost ack after confirm");
            };
            M16EpilogueHost host = null;
            try
            {
                host = new M16EpilogueHost(
                    root.transform, () => true, () => false,
                    () => M16RespectAgencyTone.ShenLiDeclinedMeeting, read, unstable);
                Assert.That(host.TryOpen(), Is.True);
                var view = host.ActiveView;
                view.ProceedToDeskButton.onClick.Invoke();
                Assert.That(view.ReadNextLetterButton.interactable, Is.False);
                view.StepOutsideButton.onClick.Invoke();
                Assert.That(view.Body.text, Does.Not.Contain("沈砾"));

                Assert.Throws<InvalidOperationException>(() =>
                    view.SteleButton.onClick.Invoke());
                Assert.That(host.IsCompleted, Is.False);

                Assert.Throws<InvalidOperationException>(() =>
                    view.SteleButton.onClick.Invoke());
                Assert.That(host.IsCompleted, Is.True);
                Assert.That(confirmCommits, Is.EqualTo(1));

                view.SteleButton.onClick.Invoke();
                Assert.That(confirmCommits, Is.EqualTo(1));
                Assert.That(host.ActiveView, Is.Null);
                Assert.That(M16EpilogueFacts.TryRead(read, out M16EpilogueRecord? saved), Is.True);
                Assert.That(saved.Value.ShenLiSpokeClosingLine, Is.False);
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
            var root = new GameObject("M16 host guards");
            var values = new Dictionary<string, int>();
            SeedM15(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            string prevLang = SoundwellUiLocalization.Language;
            bool atAfternoon = true;
            M16EpilogueHost host = null;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                host = new M16EpilogueHost(
                    root.transform, () => atAfternoon, () => true,
                    () => M16RespectAgencyTone.NeutralMixedExperience, read, write);
                Assert.That(host.TryOpen(), Is.True);
                var view = host.ActiveView;
                Assert.That(view.Heading.text, Does.Contain("ORDINARY AFTERNOON"));
                Assert.That(view.Panel.anchorMin.y, Is.EqualTo(0f));
                foreach (float aspect in new[] { 9f / 16f, 16f / 9f, 32f / 9f })
                    Assert.DoesNotThrow(() => view.LayoutForViewportAspect(aspect));

                view.ProceedToDeskButton.onClick.Invoke();
                view.StepOutsideButton.onClick.Invoke();
                var doorBtn = view.WorkshopDoorButton;

                atAfternoon = false;
                doorBtn.onClick.Invoke();
                Assert.That(host.IsCompleted, Is.False);

                atAfternoon = true;
                host.Dispose();
                doorBtn.onClick.Invoke();
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
