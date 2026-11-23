using System;
using System.Collections.Generic;
using GamingIsLove.Makinom;
using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    public sealed class M16EpilogueTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        private static void SeedM15RollCall(
            Dictionary<string, int> values,
            bool seedST02Completed = false)
        {
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            // An optional regional outcome must precede the final M15 roll call.
            if (seedST02Completed)
            {
                Assert.That(ST02RiverLanternsFacts.TryRecord(new ST02RiverLanternsRecord(
                    ST02InspectionFlags.All, true, true,
                    ST02TrialDisclosure.PublishToBoth,
                    ST02BidMode.JointBidWithWrittenTerms,
                    ST02LockRescueMethod.BraceHullStressPoint), read, write), Is.True);
            }

            Assert.That(M15FinalRollCallFacts.TryRecord(new M15FinalRollCallRecord(
                M15RollCallReader.Protagonist,
                M15FinalIdentityResponse.PublicTrueName,
                M15PensionClosure.StoppedAndReconciled,
                M15SteleInscription.AddReturnedAliveLine,
                consentedVerifiedNamesCount: 0,
                xiaoqiCalledByNumberSeventyTwo: false), read, write), Is.True);
        }

        [Test]
        public void EpilogueRequiresM15AllowsFreeWalkAndBlocksUncompletedCaseLetters()
        {
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            Assert.That(M16EpilogueSession.TryCreateFromSavedFacts(
                read, true, M16RespectAgencyTone.RespectedOthersChoices, out var blocked), Is.False);
            Assert.That(blocked, Is.Null);

            SeedM15RollCall(values, seedST02Completed: true);
            Assert.That(M16EpilogueSession.TryCreateFromSavedFacts(
                read, true, M16RespectAgencyTone.RespectedOthersChoices, out var session), Is.True);

            Assert.That(session.TryVisitStop(M16WalkStops.WorkshopStop), Is.True);
            Assert.That(session.TryVisitStop(M16WalkStops.WorkshopStop), Is.False);
            Assert.That(session.TryVisitStop(M16WalkStops.QiQueWarehouseStop), Is.True);
            Assert.That(session.TryProceedToDesk(), Is.True);

            // RC-01 was not completed, so its letter must be blocked; ST-02 was completed.
            Assert.That(session.TryReadLetter(M16DeliveredLettersFlags.RC01ChenYan), Is.False);
            Assert.That(session.TryReadLetter(M16DeliveredLettersFlags.ST02RiverLanterns), Is.True);
            Assert.That(session.TryReviewRegisterNotes(), Is.True);
            Assert.That(session.TryStepOutsideForEvening(), Is.True);

            Assert.That(session.TryChooseStandingSpotAndCommit(
                M16StandingSpot.PierSeaBreeze, read, write), Is.True);
            Assert.That(session.IsCompleted, Is.True);
            Assert.That(session.CompletedRecord.Value.ShenLiSpokeClosingLine, Is.True);
            Assert.That(session.CompletedRecord.Value.LettersReadMask,
                Is.EqualTo(M16DeliveredLettersFlags.ST02RiverLanterns));
        }

        [Test]
        public void DeclinedMeetingNeverSpeaksShenLiClosingLine()
        {
            var values = new Dictionary<string, int>();
            SeedM15RollCall(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            // Cannot pair shenLiWillingToMeet = false with a spoken Shen Li tone.
            Assert.That(M16EpilogueSession.TryCreateFromSavedFacts(
                read, shenLiWillingToMeet: false,
                M16RespectAgencyTone.RespectedOthersChoices, out _), Is.False);

            Assert.That(M16EpilogueSession.TryCreateFromSavedFacts(
                read, shenLiWillingToMeet: false,
                M16RespectAgencyTone.ShenLiDeclinedMeeting, out var session), Is.True);
            Assert.That(session.TryProceedToDesk(), Is.True);
            Assert.That(session.TryStepOutsideForEvening(), Is.True);
            Assert.That(session.TryChooseStandingSpotAndCommit(
                M16StandingSpot.MemorialSteleSunset, read, write), Is.True);

            Assert.That(M16EpilogueFacts.TryRead(read, out M16EpilogueRecord? saved), Is.True);
            Assert.That(saved.Value.ShenLiSpokeClosingLine, Is.False);
            Assert.That(saved.Value.ClosingTone, Is.EqualTo(M16RespectAgencyTone.ShenLiDeclinedMeeting));
        }

        [Test]
        public void CommitRecoversFromInterruptedWriteAndLostAckAfterConfirm()
        {
            var values = new Dictionary<string, int>();
            SeedM15RollCall(values);
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

            Assert.That(M16EpilogueSession.TryCreateFromSavedFacts(
                read, true, M16RespectAgencyTone.DecidedForOthersOften, out var session), Is.True);
            Assert.That(session.TryProceedToDesk(), Is.True);
            Assert.That(session.TryStepOutsideForEvening(), Is.True);

            Assert.Throws<InvalidOperationException>(() =>
                session.TryChooseStandingSpotAndCommit(M16StandingSpot.WorkshopDoorway, read, unstable));
            Assert.That(session.IsCompleted, Is.False);

            Assert.Throws<InvalidOperationException>(() =>
                session.TryChooseStandingSpotAndCommit(M16StandingSpot.WorkshopDoorway, read, unstable));
            Assert.That(confirmCommits, Is.EqualTo(1));

            Assert.That(session.TryChooseStandingSpotAndCommit(
                M16StandingSpot.PierSeaBreeze, read, unstable), Is.False);
            Assert.That(session.TryChooseStandingSpotAndCommit(
                M16StandingSpot.WorkshopDoorway, read, unstable), Is.True);
            Assert.That(confirmCommits, Is.EqualTo(1));
        }

        [Test]
        public void ForgedLettersCorruptDataAndUpstreamResetFailClosed()
        {
            var values = new Dictionary<string, int>();
            SeedM15RollCall(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            // Forging a letter from uncompleted RC-01 must be rejected.
            Assert.That(M16EpilogueFacts.TryRecord(new M16EpilogueRecord(
                M16WalkStops.None, M16DeliveredLettersFlags.RC01ChenYan, false,
                M16StandingSpot.PierSeaBreeze, true, M16RespectAgencyTone.NeutralMixedExperience),
                read, write), Is.False);

            Assert.That(M16EpilogueFacts.TryRecord(new M16EpilogueRecord(
                M16WalkStops.None, M16DeliveredLettersFlags.None, false,
                M16StandingSpot.PierSeaBreeze, true, M16RespectAgencyTone.NeutralMixedExperience),
                read, write), Is.True);

            values[M16EpilogueFacts.StandingSpotKey] = 99;
            Assert.That(M16EpilogueFacts.TryRead(read, out M16EpilogueRecord? corrupt), Is.True);
            Assert.That(corrupt, Is.Null);

            values[M16EpilogueFacts.StandingSpotKey] = (int)M16StandingSpot.PierSeaBreeze;
            Assert.That(M15FinalRollCallFacts.TryResetForNewVoyage(write), Is.True);
            Assert.That(M16EpilogueFacts.TryRead(read, out M16EpilogueRecord? stale), Is.True);
            Assert.That(stale, Is.Null);

            Assert.That(M16EpilogueFacts.TryResetForNewVoyage(write), Is.True);
            Assert.That(values[M16EpilogueFacts.ConfirmedKey], Is.Zero);
            Assert.That(M16EpilogueFacts.TryRead(null, out _), Is.False);
            Assert.That(M16EpilogueFacts.TryResetForNewVoyage(null), Is.False);
            if (!Maki.Initialized || Maki.Game?.Variables == null)
                Assert.That(M16EpilogueFacts.TryRead(out _), Is.False);
        }
    }
}
