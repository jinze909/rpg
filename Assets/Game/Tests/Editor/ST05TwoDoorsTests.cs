using System;
using System.Collections.Generic;
using GamingIsLove.Makinom;
using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    public sealed class ST05TwoDoorsTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        [Test]
        public void SessionEnforcesBedHandlingBothGiftsYardMediationAndMutualCohabitConsent()
        {
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            var session = new ST05TwoDoorsSession();
            Assert.That(session.TrySelectGift(ST05GiftChecks.NightDraftingLamp), Is.False);
            Assert.That(session.TryHandleStuckBed(ST05BedHandling.ConvertToTwoYardBenches), Is.True);

            Assert.That(session.TrySelectGift(ST05GiftChecks.NightDraftingLamp), Is.True);
            Assert.That(session.TrySelectGift(ST05GiftChecks.NightDraftingLamp), Is.False);
            Assert.That(session.TryResolveYardDinnerIncident(), Is.False);

            Assert.That(session.TrySelectGift(ST05GiftChecks.BreadCoolingStoneSlab), Is.True);
            Assert.That(session.Stage, Is.EqualTo(ST05Stage.YardDinnerMediation));
            Assert.That(session.TryResolveYardDinnerIncident(), Is.True);

            // Trial cohabitation without mutual consent must be rejected.
            Assert.That(session.TryChooseLivingArrangement(
                ST05LivingArrangement.TrialCohabitAndStudio,
                yuHeConsented: true, chengWuConsented: false), Is.False);
            Assert.That(session.TryChooseLivingArrangement(
                ST05LivingArrangement.TrialCohabitAndStudio,
                yuHeConsented: true, chengWuConsented: true), Is.True);

            Assert.That(session.TryRecordChengAssignmentAndCommit(
                ST05ChengAssignment.OutOfTownSurvey, read, write), Is.True);
            Assert.That(session.IsCompleted, Is.True);
            Assert.That(session.CompletedRecord.Value.AllowsLongDistanceMapBread, Is.True);
            Assert.That(session.CompletedRecord.Value.SharedTableWhenBothPresent, Is.True);
        }

        [Test]
        public void LocalStayWithTwoDoorsNeverFabricatesRemoteSeparationOrSharedTable()
        {
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            var session = new ST05TwoDoorsSession();
            Assert.That(session.TryHandleStuckBed(ST05BedHandling.ReturnToShopWithFee), Is.True);
            Assert.That(session.TrySelectGift(ST05GiftChecks.NightDraftingLamp), Is.True);
            Assert.That(session.TrySelectGift(ST05GiftChecks.BreadCoolingStoneSlab), Is.True);
            Assert.That(session.TryResolveYardDinnerIncident(), Is.True);
            Assert.That(session.TryChooseLivingArrangement(
                ST05LivingArrangement.KeepTwoDoorsWithYardTable,
                yuHeConsented: false, chengWuConsented: false), Is.True);
            Assert.That(session.TryRecordChengAssignmentAndCommit(
                ST05ChengAssignment.LocalStay, read, write), Is.True);

            Assert.That(ST05TwoDoorsFacts.TryRead(read, out ST05TwoDoorsRecord? saved), Is.True);
            Assert.That(saved.HasValue, Is.True);
            Assert.That(saved.Value.AllowsLongDistanceMapBread, Is.False);
            Assert.That(saved.Value.SharedTableWhenBothPresent, Is.False);
            Assert.That(saved.Value.BothConsentedToCohabit, Is.False);
        }

        [Test]
        public void CommitRecoversFromInterruptedWriteAndLostAckAfterConfirm()
        {
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            int preCommitFails = 0;
            int confirmCommits = 0;
            Action<string, int> unstable = (k, v) =>
            {
                if (k == ST05TwoDoorsFacts.ChengAssignmentKey && preCommitFails++ == 0)
                    throw new InvalidOperationException("Interrupted before confirm");
                values[k] = v;
                if (k == ST05TwoDoorsFacts.ConfirmedKey && v == 1 && ++confirmCommits == 1)
                    throw new InvalidOperationException("Lost ack after confirm");
            };

            var session = new ST05TwoDoorsSession();
            Assert.That(session.TryHandleStuckBed(ST05BedHandling.RemoveDoorframeToMove), Is.True);
            Assert.That(session.TrySelectGift(ST05GiftChecks.NightDraftingLamp), Is.True);
            Assert.That(session.TrySelectGift(ST05GiftChecks.BreadCoolingStoneSlab), Is.True);
            Assert.That(session.TryResolveYardDinnerIncident(), Is.True);
            Assert.That(session.TryChooseLivingArrangement(
                ST05LivingArrangement.KeepTwoDoorsWithYardTable, false, false), Is.True);

            Assert.Throws<InvalidOperationException>(() =>
                session.TryRecordChengAssignmentAndCommit(
                    ST05ChengAssignment.OutOfTownSurvey, read, unstable));
            Assert.That(session.IsCompleted, Is.False);

            Assert.Throws<InvalidOperationException>(() =>
                session.TryRecordChengAssignmentAndCommit(
                    ST05ChengAssignment.OutOfTownSurvey, read, unstable));
            Assert.That(confirmCommits, Is.EqualTo(1));

            Assert.That(session.TryRecordChengAssignmentAndCommit(
                ST05ChengAssignment.LocalStay, read, unstable), Is.False);
            Assert.That(session.TryRecordChengAssignmentAndCommit(
                ST05ChengAssignment.OutOfTownSurvey, read, unstable), Is.True);
            Assert.That(confirmCommits, Is.EqualTo(1));
            Assert.That(session.IsCompleted, Is.True);
        }

        [Test]
        public void ForgedConsentCorruptDataAndResetFailClosedCleanly()
        {
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            // Trial cohabitation without mutual consent must be rejected by facts guard.
            Assert.That(ST05TwoDoorsFacts.TryRecord(new ST05TwoDoorsRecord(
                ST05BedHandling.ConvertToTwoYardBenches, ST05GiftChecks.All, true,
                ST05LivingArrangement.TrialCohabitAndStudio, bothConsentedToCohabit: false,
                ST05ChengAssignment.LocalStay), read, write), Is.False);

            Assert.That(ST05TwoDoorsFacts.TryRecord(new ST05TwoDoorsRecord(
                ST05BedHandling.ConvertToTwoYardBenches, ST05GiftChecks.All, true,
                ST05LivingArrangement.KeepTwoDoorsWithYardTable, bothConsentedToCohabit: false,
                ST05ChengAssignment.LocalStay), read, write), Is.True);

            values[ST05TwoDoorsFacts.ChengAssignmentKey] = 99;
            Assert.That(ST05TwoDoorsFacts.TryRead(read, out ST05TwoDoorsRecord? corrupt), Is.True);
            Assert.That(corrupt, Is.Null);

            Assert.That(ST05TwoDoorsFacts.TryResetForNewVoyage(write), Is.True);
            Assert.That(values[ST05TwoDoorsFacts.ConfirmedKey], Is.Zero);
            Assert.That(ST05TwoDoorsFacts.TryRead(null, out _), Is.False);
            Assert.That(ST05TwoDoorsFacts.TryResetForNewVoyage(null), Is.False);
            if (!Maki.Initialized || Maki.Game?.Variables == null)
                Assert.That(ST05TwoDoorsFacts.TryRead(out _), Is.False);
        }
    }
}
