using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    public sealed class ST03OrphanedDownstreamTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int found) ? found : 0;
        }

        [Test]
        public void TryRecordAndSessionFailClosedWhenM15OrM16FactsExistAndRecoverOnceCleared()
        {
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            int st03Writes = 0;
            Action<string, int> write = (key, value) =>
            {
                if (key == ST03BathhouseFacts.ConfirmedKey ||
                    key == ST03BathhouseFacts.PendingChoiceKey ||
                    key == ST03BathhouseFacts.ChoiceKey ||
                    key == ST03BathhouseFacts.SafeRouteKey ||
                    key == ST03BathhouseFacts.ReturnTripKey ||
                    key == ST03BathhouseFacts.TrialNightKey ||
                    key == ST03BathhouseFacts.TransportCostKey)
                    st03Writes++;
                values[key] = value;
            };

            var record = new ST03BathhouseRecord(
                ST03BathhouseChoice.ReopenWithSubsidizedHours,
                safeRouteCleared: true,
                returnTripRetimed: true,
                trialNightCompleted: true,
                transportCostBalanced: true);

            Assert.That(ST03BathhouseSession.TryCreate(read, out ST03BathhouseSession session), Is.True);
            Assert.That(session.TryInspectClue(ST03BathhouseClue.LeakingBoilerAndUnpaidWorkers), Is.True);
            Assert.That(session.TryInspectClue(ST03BathhouseClue.OldLockersAndRepairs), Is.True);
            Assert.That(session.TryInspectClue(ST03BathhouseClue.ThreeLettersAndWuSuHandwriting), Is.True);
            Assert.That(session.TryInterviewWuSu(true, true, true), Is.True);
            Assert.That(session.TryMakeSafeRoute(true, true, true), Is.True);
            Assert.That(session.TryCompleteTrialNight(true, true, true), Is.True);
            Assert.That(session.TryReconcileTransportBudget(true), Is.True);
            Assert.That(session.TryChoose(ST03BathhouseChoice.ReopenWithSubsidizedHours), Is.True);

            Assert.That(ST03BathhouseFacts.HasOrphanedDownstreamFacts(null), Is.True);
            Assert.That(ST03BathhouseFacts.HasOrphanedDownstreamFacts(read), Is.False);
            Assert.That(ST03BathhouseFacts.CanRecord(record, read), Is.True);

            string[] endgameKeys =
            {
                M15FinalRollCallFacts.ConfirmedKey,
                M16EpilogueFacts.ConfirmedKey
            };

            foreach (string key in endgameKeys)
            {
                values[key] = 1;
                Assert.That(ST03BathhouseFacts.HasOrphanedDownstreamFacts(read), Is.True, key);
                Assert.That(ST03BathhouseFacts.CanRecord(record, read), Is.False, key);
                Assert.That(ST03BathhouseFacts.TryRecord(record, read, write), Is.False, key);
                Assert.That(session.TryCommit(read, write), Is.False, key);
                Assert.That(session.Phase, Is.EqualTo(ST03BathhousePhase.CompareOperatingPlans), key);
                Assert.That(st03Writes, Is.Zero, key);
                values[key] = 0;
            }

            Assert.That(session.TryCommit(read, write), Is.True);
            Assert.That(session.Phase, Is.EqualTo(ST03BathhousePhase.Completed));
            Assert.That(ST03BathhouseFacts.TryRead(read, out ST03BathhouseRecord? saved), Is.True);
            Assert.That(saved.HasValue, Is.True);
            Assert.That(saved.Value.M15Sign, Is.EqualTo(ST03M15BathhouseSign.OriginalBathhouse));
        }
    }
}
