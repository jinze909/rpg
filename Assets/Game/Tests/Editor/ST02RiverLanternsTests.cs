using System;
using System.Collections.Generic;
using GamingIsLove.Makinom;
using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    public sealed class ST02RiverLanternsTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        [Test]
        public void SessionEnforcesBothBoatInspectionsDockFeedbackAndOrderedDecisions()
        {
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            var session = new ST02RiverLanternsSession();
            Assert.That(session.TryGatherDockFeedback(), Is.False);
            Assert.That(session.TryRelayOriginalWords(), Is.False);

            Assert.That(session.TryInspectBoat(ST02InspectionFlags.XieZhiOldBoat), Is.True);
            Assert.That(session.TryInspectBoat(ST02InspectionFlags.XieZhiOldBoat), Is.False);
            Assert.That(session.Stage, Is.EqualTo(ST02Stage.WorkshopInspection));

            Assert.That(session.TryInspectBoat(ST02InspectionFlags.WenShengNewBoat), Is.True);
            Assert.That(session.Stage, Is.EqualTo(ST02Stage.DockTrialAndFeedback));
            Assert.That(session.TryRelayOriginalWords(), Is.True);
            Assert.That(session.TryRelayOriginalWords(), Is.False);

            Assert.That(session.TryChooseTrialDisclosure(ST02TrialDisclosure.PublishToBoth), Is.False);
            Assert.That(session.TryGatherDockFeedback(), Is.True);
            Assert.That(session.TryChooseTrialDisclosure(ST02TrialDisclosure.PublishToBoth), Is.True);
            Assert.That(session.TryChooseBidMode(ST02BidMode.JointBidWithWrittenTerms), Is.True);

            Assert.That(session.TryResolveOldLockAndCommit(
                ST02LockRescueMethod.BraceHullStressPoint, read, write), Is.True);
            Assert.That(session.IsCompleted, Is.True);
            Assert.That(session.CompletedRecord.Value.OriginalWordsRelayed, Is.True);
            Assert.That(session.CompletedRecord.Value.LanternPreservedIntact, Is.True);
            Assert.That(session.CompletedRecord.Value.JointFreeRepairDisputeEligible, Is.True);
            Assert.That(session.CompletedRecord.Value.SharedJointLanternSignature, Is.True);
        }

        [Test]
        public void IndependentBidNeverFabricatesJointDisputeAndDismantleRecordsDamage()
        {
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            var session = new ST02RiverLanternsSession();
            Assert.That(session.TryInspectBoat(ST02InspectionFlags.XieZhiOldBoat), Is.True);
            Assert.That(session.TryInspectBoat(ST02InspectionFlags.WenShengNewBoat), Is.True);
            Assert.That(session.TryGatherDockFeedback(), Is.True);
            Assert.That(session.TryChooseTrialDisclosure(ST02TrialDisclosure.ReturnPrivatelyToEach), Is.True);
            Assert.That(session.TryChooseBidMode(ST02BidMode.IndependentWithSubcontract), Is.True);
            Assert.That(session.TryResolveOldLockAndCommit(
                ST02LockRescueMethod.DismantleLanternFrame, read, write), Is.True);

            Assert.That(ST02RiverLanternsFacts.TryRead(read, out ST02RiverLanternsRecord? saved), Is.True);
            Assert.That(saved.HasValue, Is.True);
            Assert.That(saved.Value.TrialDisclosure, Is.EqualTo(ST02TrialDisclosure.ReturnPrivatelyToEach));
            Assert.That(saved.Value.JointFreeRepairDisputeEligible, Is.False);
            Assert.That(saved.Value.SharedJointLanternSignature, Is.False);
            Assert.That(saved.Value.LanternPreservedIntact, Is.False);
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
                if (k == ST02RiverLanternsFacts.BidModeKey && preCommitFails++ == 0)
                    throw new InvalidOperationException("Interrupted before confirm");
                values[k] = v;
                if (k == ST02RiverLanternsFacts.ConfirmedKey && v == 1 && ++confirmCommits == 1)
                    throw new InvalidOperationException("Lost ack after confirm");
            };

            var session = new ST02RiverLanternsSession();
            Assert.That(session.TryInspectBoat(ST02InspectionFlags.XieZhiOldBoat), Is.True);
            Assert.That(session.TryInspectBoat(ST02InspectionFlags.WenShengNewBoat), Is.True);
            Assert.That(session.TryGatherDockFeedback(), Is.True);
            Assert.That(session.TryChooseTrialDisclosure(ST02TrialDisclosure.PublishToBoth), Is.True);
            Assert.That(session.TryChooseBidMode(ST02BidMode.JointBidWithWrittenTerms), Is.True);

            Assert.Throws<InvalidOperationException>(() =>
                session.TryResolveOldLockAndCommit(
                    ST02LockRescueMethod.BraceHullStressPoint, read, unstable));
            Assert.That(session.IsCompleted, Is.False);

            Assert.Throws<InvalidOperationException>(() =>
                session.TryResolveOldLockAndCommit(
                    ST02LockRescueMethod.BraceHullStressPoint, read, unstable));
            Assert.That(confirmCommits, Is.EqualTo(1));

            // Mismatched rescue choice must fail closed after confirm was already written.
            Assert.That(session.TryResolveOldLockAndCommit(
                ST02LockRescueMethod.DismantleLanternFrame, read, unstable), Is.False);

            Assert.That(session.TryResolveOldLockAndCommit(
                ST02LockRescueMethod.BraceHullStressPoint, read, unstable), Is.True);
            Assert.That(confirmCommits, Is.EqualTo(1));
            Assert.That(session.IsCompleted, Is.True);
        }

        [Test]
        public void CorruptRecordsAndResetFailClosedCleanly()
        {
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            // Missing dock feedback must be rejected by facts guard.
            Assert.That(ST02RiverLanternsFacts.TryRecord(new ST02RiverLanternsRecord(
                ST02InspectionFlags.All, true, dockFeedbackGathered: false,
                ST02TrialDisclosure.PublishToBoth, ST02BidMode.JointBidWithWrittenTerms,
                ST02LockRescueMethod.BraceHullStressPoint), read, write), Is.False);

            Assert.That(ST02RiverLanternsFacts.TryRecord(new ST02RiverLanternsRecord(
                ST02InspectionFlags.All, true, dockFeedbackGathered: true,
                ST02TrialDisclosure.PublishToBoth, ST02BidMode.JointBidWithWrittenTerms,
                ST02LockRescueMethod.BraceHullStressPoint), read, write), Is.True);

            values[ST02RiverLanternsFacts.BidModeKey] = 99;
            Assert.That(ST02RiverLanternsFacts.TryRead(read, out ST02RiverLanternsRecord? corrupt), Is.True);
            Assert.That(corrupt, Is.Null);

            Assert.That(ST02RiverLanternsFacts.TryResetForNewVoyage(write), Is.True);
            Assert.That(values[ST02RiverLanternsFacts.ConfirmedKey], Is.Zero);
            Assert.That(ST02RiverLanternsFacts.TryRead(null, out _), Is.False);
            Assert.That(ST02RiverLanternsFacts.TryResetForNewVoyage(null), Is.False);
            if (!Maki.Initialized || Maki.Game?.Variables == null)
                Assert.That(ST02RiverLanternsFacts.TryRead(out _), Is.False);
        }
    }
}
