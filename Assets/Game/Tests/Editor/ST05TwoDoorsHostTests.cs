using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class ST05TwoDoorsHostTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        private static Action<string, int> Writer(Dictionary<string, int> values)
        {
            return (key, value) => values[key] = value;
        }

        private static ST05TwoDoorsWorldEvidence ReadyForHomeDecision()
        {
            return new ST05TwoDoorsWorldEvidence
            {
                AtTwoDoorsHome = true,
                BedResolution = ST05BedResolution.ConvertBedToCourtyardBenches,
                DrawingLampTested = true,
                CoolingStoneCracksChecked = true,
                StallholderAdviceHeard = true,
                WindblownMapsRecovered = true,
                MapRackMovedAwayFromOven = true,
                GuestsRedirected = true,
                YuheSpokeToAunt = true,
                ChengSpokeToFriends = true,
                ShopAccessDiscussed = true,
                KeysAndVisitorsDiscussed = true,
                TravelCostsDiscussed = true,
                AgreedArrangement = ST05HomeArrangement.MutuallyAgreedTrialSharedHomeAndStudio,
                YuheConsentedToSharedTrial = true
            };
        }

        [Test]
        public void HostRequiresTheRealCourtyardAndBothPartnersConsentBeforeRecording()
        {
            var root = new GameObject("ST05 host partner consent");
            var values = new Dictionary<string, int>();
            ST05TwoDoorsWorldEvidence evidence = ReadyForHomeDecision();
            int completedCallbacks = 0;
            ST05TwoDoorsOutcome? completed = null;
            ST05TwoDoorsHost host = null;
            try
            {
                host = new ST05TwoDoorsHost(root.transform, () => evidence,
                    Reader(values), Writer(values), outcome =>
                    {
                        completedCallbacks++;
                        completed = outcome;
                    });

                Assert.That(host.TryOpen(), Is.True);
                Assert.That(host.Session.Phase, Is.EqualTo(ST05TwoDoorsPhase.AgreeHomeAndKeys));
                Assert.That(host.ActiveView.AdvanceButton.interactable, Is.False);
                Assert.That(host.TryAdvance(), Is.False);
                Assert.That(values.ContainsKey(ST05TwoDoorsFacts.HomeConfirmedKey), Is.False);

                evidence.ChengConsentedToSharedTrial = true;
                host.Refresh();
                Assert.That(host.ActiveView.AdvanceButton.interactable, Is.True);
                Assert.That(host.TryAdvance(), Is.True);
                Assert.That(host.Session.Phase,
                    Is.EqualTo(ST05TwoDoorsPhase.AwaitChengOwnJobDecision));
                Assert.That(values[ST05TwoDoorsFacts.HomeConfirmedKey], Is.EqualTo(1));
                Assert.That(values.ContainsKey(ST05TwoDoorsFacts.AssignmentConfirmedKey), Is.False);

                evidence.ChengAssignment = ST05ChengAssignment.AcceptOutsideSurvey;
                evidence.ChengPersonallyAcceptedOrDeclined = true;
                evidence.ShopCoverArranged = true;
                evidence.KeysChecked = true;
                evidence.ReturnDateTalkedThrough = true;
                host.Refresh();
                Assert.That(host.ActiveView.AdvanceButton.interactable, Is.True);
                Assert.That(host.TryAdvance(), Is.True);
                Assert.That(host.Session.Phase, Is.EqualTo(ST05TwoDoorsPhase.Completed));
                Assert.That(host.HasRecordedOutcome, Is.True);
                Assert.That(completedCallbacks, Is.EqualTo(1));
                Assert.That(completed.HasValue, Is.True);
                Assert.That(completed.Value.Assignment,
                    Is.EqualTo(ST05ChengAssignment.AcceptOutsideSurvey));
                Assert.That(completed.Value.Home.MutualTrialConsent, Is.True);

                host.Refresh();
                Assert.That(completedCallbacks, Is.EqualTo(1),
                    "A completed scene must not deliver its callback twice.");
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ReturningBedRequiresObservedHaulagePaymentBeforeProgressCanAdvance()
        {
            var root = new GameObject("ST05 haulage evidence");
            var values = new Dictionary<string, int>();
            var evidence = new ST05TwoDoorsWorldEvidence
            {
                AtTwoDoorsHome = true,
                BedResolution = ST05BedResolution.ReturnBedAndPayHaulage
            };
            ST05TwoDoorsHost host = null;
            try
            {
                host = new ST05TwoDoorsHost(root.transform, () => evidence,
                    Reader(values), Writer(values));
                Assert.That(host.TryOpen(), Is.True);
                Assert.That(host.Session.Phase, Is.EqualTo(ST05TwoDoorsPhase.UnblockStaircase));
                Assert.That(host.TryAdvance(), Is.False);
                Assert.That(values.ContainsKey(ST05TwoDoorsFacts.HomeConfirmedKey), Is.False);

                evidence.ReturnHaulageFeePaid = true;
                host.Refresh();
                Assert.That(host.Session.Phase, Is.EqualTo(ST05TwoDoorsPhase.TestMarketGifts));
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ReopenedSessionResumesChengDecisionAndRetriesOnlyTheOriginalChoice()
        {
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = Writer(values);
            var home = new ST05HouseholdRecord(
                ST05BedResolution.RemoveDoorFrame,
                ST05HomeArrangement.KeepTwoDoorsAndCourtyardTable,
                mutualTrialConsent: false);
            Assert.That(ST05TwoDoorsFacts.TryRecordHome(home, read, write), Is.True);

            Assert.That(ST05TwoDoorsSession.TryCreate(read,
                out ST05TwoDoorsSession session), Is.True);
            Assert.That(session.Phase,
                Is.EqualTo(ST05TwoDoorsPhase.AwaitChengOwnJobDecision));
            Assert.That(session.CompletedHome.HasValue, Is.True);

            Assert.That(session.TryHearChengAssignment(
                ST05ChengAssignment.StayLocal, true, true, true, true), Is.True);
            Assert.Throws<InvalidOperationException>(() =>
                session.TryCommitChengAssignment(read, (key, value) =>
                {
                    write(key, value);
                    if (key == ST05TwoDoorsFacts.AssignmentKey)
                        throw new InvalidOperationException("Interrupted after Cheng's own choice.");
                }));

            Assert.That(session.TryHearChengAssignment(
                ST05ChengAssignment.StayLocal, true, true, true, true), Is.True);
            Assert.That(session.TryHearChengAssignment(
                ST05ChengAssignment.AcceptOutsideSurvey, true, true, true, true), Is.False);
            Assert.That(session.TryCommitChengAssignment(read, write), Is.True);
            Assert.That(session.IsCompleted, Is.True);
            Assert.That(ST05TwoDoorsFacts.TryRead(read,
                out ST05TwoDoorsOutcome? saved), Is.True);
            Assert.That(saved.HasValue, Is.True);
            Assert.That(saved.Value.Assignment, Is.EqualTo(ST05ChengAssignment.StayLocal));
        }

        [Test]
        public void CompletedStoryCannotBeReopenedAsANewTwoDoorsSession()
        {
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = Writer(values);
            var home = new ST05HouseholdRecord(
                ST05BedResolution.ConvertBedToCourtyardBenches,
                ST05HomeArrangement.KeepTwoDoorsAndCourtyardTable,
                mutualTrialConsent: false);
            Assert.That(ST05TwoDoorsFacts.TryRecordHome(home, read, write), Is.True);
            Assert.That(ST05TwoDoorsFacts.TryRecordAssignment(
                ST05ChengAssignment.StayLocal, read, write), Is.True);
            Assert.That(ST05TwoDoorsSession.TryCreate(read, out _), Is.False);
        }
    }
}