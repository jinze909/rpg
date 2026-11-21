using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    public sealed class ST05TwoDoorsRetrySafetyTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        private static Action<string, int> Writer(Dictionary<string, int> values)
        {
            return (key, value) => values[key] = value;
        }

        private static ST05TwoDoorsSession CreateAtHomeDecision(Func<string, int> read)
        {
            Assert.That(ST05TwoDoorsSession.TryCreate(read,
                out ST05TwoDoorsSession session), Is.True);
            Assert.That(session.TryClearStaircase(
                ST05BedResolution.RemoveDoorFrame, returnHaulageFeePaid: false), Is.True);
            Assert.That(session.TryChoosePracticalGifts(
                drawingLampTested: true,
                coolingStoneCracksChecked: true,
                stallholderAdviceHeard: true), Is.True);
            Assert.That(session.TryResolveSharedCourtyard(
                windblownMapsRecovered: true,
                mapRackMovedAwayFromOven: true,
                guestsRedirected: true), Is.True);
            Assert.That(session.TryHearOwnWords(
                yuheSpokeToAunt: true,
                chengSpokeToFriends: true,
                shopAccessDiscussed: true,
                keysAndVisitorsDiscussed: true,
                travelCostsDiscussed: true), Is.True);
            return session;
        }

        [Test]
        public void SameHomeDecisionCanRetryAfterTornWriteButCannotBeReplaced()
        {
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = Writer(values);
            ST05TwoDoorsSession session = CreateAtHomeDecision(read);

            Assert.That(session.TryAgreeLivingArrangement(
                ST05HomeArrangement.KeepTwoDoorsAndCourtyardTable,
                eachConsentedToSharedTrial: false), Is.True);

            Assert.Throws<InvalidOperationException>(() =>
                session.TryCommitLivingAgreement(read, (key, value) =>
                {
                    write(key, value);
                    if (key == ST05TwoDoorsFacts.HomeIntentKey)
                        throw new InvalidOperationException("Interrupted after home intent.");
                }));

            Assert.That(session.TryAgreeLivingArrangement(
                ST05HomeArrangement.KeepTwoDoorsAndCourtyardTable,
                eachConsentedToSharedTrial: false), Is.True);
            Assert.That(session.TryAgreeLivingArrangement(
                ST05HomeArrangement.MutuallyAgreedTrialSharedHomeAndStudio,
                eachConsentedToSharedTrial: true), Is.False);

            Assert.That(session.TryCommitLivingAgreement(read, write), Is.True);
            Assert.That(session.CompletedHome.HasValue, Is.True);
            Assert.That(session.Phase,
                Is.EqualTo(ST05TwoDoorsPhase.AwaitChengOwnJobDecision));
        }

        [Test]
        public void SameChengDecisionCanRetryAfterTornWriteButCannotBeReplaced()
        {
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = Writer(values);
            ST05TwoDoorsSession session = CreateAtHomeDecision(read);

            Assert.That(session.TryAgreeLivingArrangement(
                ST05HomeArrangement.KeepTwoDoorsAndCourtyardTable,
                eachConsentedToSharedTrial: false), Is.True);
            Assert.That(session.TryCommitLivingAgreement(read, write), Is.True);

            Assert.That(session.TryHearChengAssignment(
                ST05ChengAssignment.StayLocal,
                chengPersonallyAcceptedOrDeclined: true,
                shopCoverArranged: true,
                keysChecked: true,
                returnDateTalkedThrough: true), Is.True);

            Assert.Throws<InvalidOperationException>(() =>
                session.TryCommitChengAssignment(read, (key, value) =>
                {
                    write(key, value);
                    if (key == ST05TwoDoorsFacts.AssignmentKey)
                        throw new InvalidOperationException("Interrupted after Cheng's choice.");
                }));

            Assert.That(session.TryHearChengAssignment(
                ST05ChengAssignment.StayLocal, true, true, true, true), Is.True);
            Assert.That(session.TryHearChengAssignment(
                ST05ChengAssignment.AcceptOutsideSurvey, true, true, true, true), Is.False);

            Assert.That(session.TryCommitChengAssignment(read, write), Is.True);
            Assert.That(session.Phase, Is.EqualTo(ST05TwoDoorsPhase.Completed));
            Assert.That(ST05TwoDoorsFacts.TryRead(read,
                out ST05TwoDoorsOutcome? saved), Is.True);
            Assert.That(saved.HasValue, Is.True);
            Assert.That(saved.Value.Assignment, Is.EqualTo(ST05ChengAssignment.StayLocal));
        }
    }
}