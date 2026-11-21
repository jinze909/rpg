using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    public sealed class ST05TwoDoorsTests
    {
        private static Func<string, int> Read(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        private static Action<string, int> Write(Dictionary<string, int> values)
        {
            return (key, value) => values[key] = value;
        }

        private static ST05TwoDoorsSession ReachHomeDecision(
            Dictionary<string, int> values, ST05BedResolution bed)
        {
            Assert.That(ST05TwoDoorsSession.TryCreate(Read(values),
                out ST05TwoDoorsSession session), Is.True);
            Assert.That(session.TryClearStaircase(bed, true), Is.True);
            Assert.That(session.TryChoosePracticalGifts(true, true, true), Is.True);
            Assert.That(session.TryResolveSharedCourtyard(true, true, true), Is.True);
            Assert.That(session.TryHearOwnWords(true, true, true, true, true), Is.True);
            return session;
        }

        private static ST05TwoDoorsSession CommitFullDecision(
            Dictionary<string, int> values, ST05HomeArrangement home,
            ST05ChengAssignment job)
        {
            var session = ReachHomeDecision(values,
                ST05BedResolution.ConvertBedToCourtyardBenches);
            Assert.That(session.TryAgreeLivingArrangement(home,
                home == ST05HomeArrangement.MutuallyAgreedTrialSharedHomeAndStudio), Is.True);
            Assert.That(session.TryCommitLivingAgreement(Read(values), Write(values)), Is.True);
            Assert.That(session.TryHearChengAssignment(job, true, true, true, true), Is.True);
            Assert.That(session.TryCommitChengAssignment(Read(values), Write(values)), Is.True);
            return session;
        }

        [Test]
        public void MovingBedGiftsMapOvenAndOwnWordsAreAllMandatory()
        {
            var values = new Dictionary<string, int>();
            Assert.That(ST05TwoDoorsSession.TryCreate(Read(values),
                out ST05TwoDoorsSession session), Is.True);
            Assert.That(session.TryChoosePracticalGifts(true, true, true), Is.False);
            Assert.That(session.TryClearStaircase(
                ST05BedResolution.ReturnBedAndPayHaulage, false), Is.False);
            Assert.That(session.TryClearStaircase(
                ST05BedResolution.ReturnBedAndPayHaulage, true), Is.True);
            Assert.That(session.TryChoosePracticalGifts(true, false, true), Is.False);
            Assert.That(session.TryChoosePracticalGifts(true, true, true), Is.True);
            Assert.That(session.TryResolveSharedCourtyard(true, false, true), Is.False);
            Assert.That(session.TryResolveSharedCourtyard(true, true, true), Is.True);
            Assert.That(session.TryHearOwnWords(true, false, true, true, true), Is.False,
                "Neither partner's account can be replaced with the player's vote.");
            Assert.That(session.TryHearOwnWords(true, true, true, true, true), Is.True);
            Assert.That(session.TryAgreeLivingArrangement(
                ST05HomeArrangement.MutuallyAgreedTrialSharedHomeAndStudio, false), Is.False);
            Assert.That(session.TryAgreeLivingArrangement(
                ST05HomeArrangement.MutuallyAgreedTrialSharedHomeAndStudio, true), Is.True);
            Assert.That(session.TryCommitLivingAgreement(Read(values), Write(values)), Is.True);
            Assert.That(session.TryHearChengAssignment(
                ST05ChengAssignment.AcceptOutsideSurvey, false, true, true, true), Is.False);
            Assert.That(session.TryHearChengAssignment(
                ST05ChengAssignment.AcceptOutsideSurvey, true, true, true, false), Is.False);
            Assert.That(session.TryHearChengAssignment(
                ST05ChengAssignment.AcceptOutsideSurvey, true, true, true, true), Is.True);
            Assert.That(session.TryCommitChengAssignment(Read(values), Write(values)), Is.True);
            Assert.That(session.Phase, Is.EqualTo(ST05TwoDoorsPhase.Completed));
        }

        [Test]
        public void SeparateHomesAndLocalWorkDoNotFabricateLongDistanceLetters()
        {
            var values = new Dictionary<string, int>();
            var session = CommitFullDecision(values,
                ST05HomeArrangement.KeepTwoDoorsAndCourtyardTable,
                ST05ChengAssignment.StayLocal);
            Assert.That(session.CompletedHome.Value.KeepsSeparateDoors, Is.True);
            Assert.That(ST05TwoDoorsFacts.TryStartOutsideWork(Read(values), Write(values)),
                Is.False);
            Assert.That(ST05TwoDoorsFacts.TryMarkLetterSent(Read(values), Write(values)),
                Is.False);
            Assert.That(ST05TwoDoorsFacts.TryMarkLetterDelivered(Read(values), Write(values)),
                Is.False);
            Assert.That(ST05TwoDoorsFacts.TryRead(Read(values),
                out ST05TwoDoorsOutcome? saved), Is.True);
            Assert.That(saved.HasValue, Is.True);
            Assert.That(saved.Value.ShowM15LetterInsteadOfCheng, Is.False);
            Assert.That(saved.Value.ShowM15Bread, Is.False);
            Assert.That(saved.Value.ShowM15HandedMap, Is.False);
            Assert.That(ST05TwoDoorsFacts.TryMarkYuheM15Arrival(false,
                Read(values), Write(values)), Is.True);
            Assert.That(ST05TwoDoorsFacts.TryMarkChengM15Arrival(false,
                Read(values), Write(values)), Is.True);
            Assert.That(ST05TwoDoorsFacts.TryRead(Read(values),
                out ST05TwoDoorsOutcome? arrived), Is.True);
            Assert.That(arrived.Value.SeatYuheAtM15, Is.True);
            Assert.That(arrived.Value.SeatChengAtM15, Is.True);
            Assert.That(arrived.Value.ShowM15Bread, Is.False);
            Assert.That(arrived.Value.ShowM15HandedMap, Is.False);
        }

        [Test]
        public void RemoteWorkRequiresStartMailSendAndDeliveryAndGuestsAreNotAssumed()
        {
            var values = new Dictionary<string, int>();
            CommitFullDecision(values,
                ST05HomeArrangement.MutuallyAgreedTrialSharedHomeAndStudio,
                ST05ChengAssignment.AcceptOutsideSurvey);
            Assert.That(ST05TwoDoorsFacts.TryMarkLetterSent(Read(values), Write(values)),
                Is.False);
            Assert.That(ST05TwoDoorsFacts.TryStartOutsideWork(Read(values), Write(values)),
                Is.True);
            Assert.That(ST05TwoDoorsFacts.TryMarkLetterDelivered(Read(values), Write(values)),
                Is.False);
            Assert.That(ST05TwoDoorsFacts.TryMarkLetterSent(Read(values), Write(values)),
                Is.True);
            Assert.That(ST05TwoDoorsFacts.TryRead(Read(values),
                out ST05TwoDoorsOutcome? inTransit), Is.True);
            Assert.That(inTransit.Value.ShowM15LetterInsteadOfCheng, Is.False);
            Assert.That(ST05TwoDoorsFacts.TryMarkLetterDelivered(Read(values), Write(values)),
                Is.True);
            Assert.That(ST05TwoDoorsFacts.TryRead(Read(values),
                out ST05TwoDoorsOutcome? delivered), Is.True);
            Assert.That(delivered.Value.ShowM15LetterInsteadOfCheng, Is.True);
            Assert.That(delivered.Value.SeatChengAtM15, Is.False);
            Assert.That(ST05TwoDoorsFacts.TryMarkYuheM15Arrival(true,
                Read(values), Write(values)), Is.True);
            Assert.That(ST05TwoDoorsFacts.TryRead(Read(values),
                out ST05TwoDoorsOutcome? bread), Is.True);
            Assert.That(bread.Value.ShowM15Bread, Is.True);
            Assert.That(bread.Value.ShowM15HandedMap, Is.False);
            Assert.That(ST05TwoDoorsFacts.TryMarkChengM15Arrival(true,
                Read(values), Write(values)), Is.True);
            Assert.That(ST05TwoDoorsFacts.TryRead(Read(values),
                out ST05TwoDoorsOutcome? returned), Is.True);
            Assert.That(returned.Value.ShowM15LetterInsteadOfCheng, Is.False);
            Assert.That(returned.Value.ShowM15HandedMap, Is.True);
            Assert.That(returned.Value.SeatChengAtM15, Is.True);
        }

        [Test]
        public void InterruptedHomeAndAssignmentCommitAllowOnlyOriginalDecisions()
        {
            var values = new Dictionary<string, int>();
            var session = ReachHomeDecision(values,
                ST05BedResolution.RemoveDoorFrame);
            var read = Read(values);
            var write = Write(values);
            Assert.That(session.TryAgreeLivingArrangement(
                ST05HomeArrangement.KeepTwoDoorsAndCourtyardTable, false), Is.True);
            Assert.Throws<InvalidOperationException>(() =>
                session.TryCommitLivingAgreement(read, (key, value) =>
                {
                    write(key, value);
                    if (key == ST05TwoDoorsFacts.HomeIntentKey)
                        throw new InvalidOperationException("Home intent only");
                }));
            Assert.That(ST05TwoDoorsFacts.TryRead(read, out _), Is.False);
            Assert.That(ST05TwoDoorsFacts.TryRecordHome(
                new ST05HouseholdRecord(ST05BedResolution.ReturnBedAndPayHaulage,
                    ST05HomeArrangement.KeepTwoDoorsAndCourtyardTable, false),
                read, write), Is.False);
            Assert.That(session.TryCommitLivingAgreement(read, write), Is.True);
            Assert.That(session.TryHearChengAssignment(ST05ChengAssignment.StayLocal,
                true, true, true, true), Is.True);
            Assert.Throws<InvalidOperationException>(() =>
                session.TryCommitChengAssignment(read, (key, value) =>
                {
                    write(key, value);
                    if (key == ST05TwoDoorsFacts.AssignmentKey)
                        throw new InvalidOperationException("Assignment choice only");
                }));
            Assert.That(ST05TwoDoorsFacts.TryRead(read, out _), Is.False);
            Assert.That(ST05TwoDoorsFacts.TryRecordAssignment(
                ST05ChengAssignment.AcceptOutsideSurvey, read, write), Is.False);
            Assert.That(session.TryCommitChengAssignment(read, write), Is.True);
            Assert.That(ST05TwoDoorsFacts.TryRead(read,
                out ST05TwoDoorsOutcome? recovered), Is.True);
            Assert.That(recovered.Value.Assignment, Is.EqualTo(ST05ChengAssignment.StayLocal));
            Assert.That(ST05TwoDoorsFacts.TryRecordAssignment(
                ST05ChengAssignment.AcceptOutsideSurvey, read, write), Is.False);
        }

        [Test]
        public void CorruptAttributionAndLetterHistoryStayInvisibleUntilNewGame()
        {
            var values = new Dictionary<string, int>();
            CommitFullDecision(values, ST05HomeArrangement.KeepTwoDoorsAndCourtyardTable,
                ST05ChengAssignment.StayLocal);
            var read = Read(values);
            values[ST05TwoDoorsFacts.LetterStatusKey] = (int)ST05LetterStatus.Delivered;
            Assert.That(ST05TwoDoorsFacts.TryRead(read, out _), Is.False,
                "A local job cannot spontaneously generate remote correspondence.");
            values[ST05TwoDoorsFacts.LetterStatusKey] = 0;
            values[ST05TwoDoorsFacts.BreadBroughtKey] = 1;
            Assert.That(ST05TwoDoorsFacts.TryRead(read, out _), Is.False,
                "No bread on an M15 table unless Yu He arrived carrying it.");
            values[ST05TwoDoorsFacts.BreadBroughtKey] = 0;
            values[ST05TwoDoorsFacts.AssignmentKey] = 99;
            Assert.That(ST05TwoDoorsFacts.TryRead(read, out _), Is.False);
            Assert.That(ST05TwoDoorsFacts.TryResetForNewGame(Write(values)), Is.True);
            Assert.That(ST05TwoDoorsFacts.TryRead(read,
                out ST05TwoDoorsOutcome? empty), Is.True);
            Assert.That(empty.HasValue, Is.False);
        }
    }
}
