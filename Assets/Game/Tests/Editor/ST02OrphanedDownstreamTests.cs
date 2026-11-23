using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    public sealed class ST02OrphanedDownstreamTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        [Test]
        public void TryRecordAndSessionFailClosedWhenM15OrM16FactsExistAndRecoverOnceCleared()
        {
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            int st02Writes = 0;
            Action<string, int> write = (key, value) =>
            {
                if (key == ST02RiverLanternsFacts.ConfirmedKey ||
                    key == ST02RiverLanternsFacts.PendingSignatureKey ||
                    key == ST02RiverLanternsFacts.InspectionsMaskKey ||
                    key == ST02RiverLanternsFacts.OriginalWordsRelayedKey ||
                    key == ST02RiverLanternsFacts.DockFeedbackKey ||
                    key == ST02RiverLanternsFacts.TrialDisclosureKey ||
                    key == ST02RiverLanternsFacts.BidModeKey ||
                    key == ST02RiverLanternsFacts.LockRescueKey)
                    st02Writes++;
                values[key] = value;
            };

            var record = new ST02RiverLanternsRecord(
                ST02InspectionFlags.All,
                originalWordsRelayed: true,
                dockFeedbackGathered: true,
                ST02TrialDisclosure.PublishToBoth,
                ST02BidMode.JointBidWithWrittenTerms,
                ST02LockRescueMethod.BraceHullStressPoint);

            var session = new ST02RiverLanternsSession();
            Assert.That(session.TryInspectBoat(ST02InspectionFlags.XieZhiOldBoat), Is.True);
            Assert.That(session.TryInspectBoat(ST02InspectionFlags.WenShengNewBoat), Is.True);
            Assert.That(session.TryRelayOriginalWords(), Is.True);
            Assert.That(session.TryGatherDockFeedback(), Is.True);
            Assert.That(session.TryChooseTrialDisclosure(ST02TrialDisclosure.PublishToBoth), Is.True);
            Assert.That(session.TryChooseBidMode(ST02BidMode.JointBidWithWrittenTerms), Is.True);

            Assert.That(ST02RiverLanternsFacts.HasOrphanedDownstreamFacts(null), Is.True);
            Assert.That(ST02RiverLanternsFacts.HasOrphanedDownstreamFacts(read), Is.False);
            Assert.That(ST02RiverLanternsFacts.CanRecord(record, read), Is.True);

            string[] endgameKeys =
            {
                M15FinalRollCallFacts.ConfirmedKey,
                M16EpilogueFacts.ConfirmedKey
            };

            foreach (string key in endgameKeys)
            {
                values[key] = 1;
                Assert.That(ST02RiverLanternsFacts.HasOrphanedDownstreamFacts(read), Is.True, key);
                Assert.That(ST02RiverLanternsFacts.CanRecord(record, read), Is.False, key);
                Assert.That(ST02RiverLanternsFacts.TryRecord(record, read, write), Is.False, key);
                Assert.That(session.TryResolveOldLockAndCommit(
                    ST02LockRescueMethod.BraceHullStressPoint, read, write), Is.False, key);
                Assert.That(session.IsCompleted, Is.False, key);
                Assert.That(st02Writes, Is.Zero, key);
                values[key] = 0;
            }

            Assert.That(session.TryResolveOldLockAndCommit(
                ST02LockRescueMethod.BraceHullStressPoint, read, write), Is.True);
            Assert.That(session.IsCompleted, Is.True);
            Assert.That(ST02RiverLanternsFacts.TryRead(read, out ST02RiverLanternsRecord? saved), Is.True);
            Assert.That(saved.HasValue, Is.True);
            Assert.That(saved.Value.SharedJointLanternSignature, Is.True);
        }
    }
}
