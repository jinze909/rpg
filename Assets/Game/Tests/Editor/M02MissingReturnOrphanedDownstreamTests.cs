using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    public sealed class M02MissingReturnOrphanedDownstreamTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        private static M02MissingReturnOutcome ApprovedOutcome()
        {
            var review = new M02MissingReturnReview(
                M01PensionChoice.RequestMissingReturnReview,
                M02BridgeEvidence.WarReportDraft, false);
            review.Begin();
            review.Request();
            review.Stamp();
            M02MissingReturnOutcome outcome = review.Leave();
            Assert.That(outcome.Approved, Is.True);
            return outcome;
        }

        [Test]
        public void TryRecordApprovedFailsClosedOnNullDelegatesOrOrphanedDownstreamFacts()
        {
            M02MissingReturnOutcome outcome = ApprovedOutcome();
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            int approvalWrites = 0;
            Action<string, int> write = (key, value) =>
            {
                if (key == M02MissingReturnFacts.ApprovalKey)
                    approvalWrites++;
                values[key] = value;
            };

            Assert.That(M02MissingReturnFacts.TryRecordApproved(outcome, null, write), Is.False);
            Assert.That(M02MissingReturnFacts.TryRecordApproved(outcome, read, null), Is.False);
            Assert.That(M02MissingReturnFacts.TryReadApproved(null, out bool missing), Is.False);
            Assert.That(missing, Is.False);
            Assert.That(approvalWrites, Is.Zero);

            string[] orphanKeys =
            {
                M07ExtractionFacts.ExecutedRouteKey,
                M08FirstWorldNoticeFacts.ConfirmedKey,
                M09BerthTableFacts.ConfirmedKey,
                RC05ThirdSquadFacts.ConfirmedKey,
                M10AssemblyFacts.ConfirmedKey,
                M16EpilogueFacts.ConfirmedKey
            };

            foreach (string orphanKey in orphanKeys)
            {
                values[orphanKey] = 1;
                Assert.That(M02MissingReturnFacts.TryRecordApproved(outcome, read, write),
                    Is.False, orphanKey);
                Assert.That(approvalWrites, Is.Zero, orphanKey);
                values[orphanKey] = 0;
            }

            Assert.That(M02MissingReturnFacts.TryRecordApproved(outcome, read, write), Is.True);
            Assert.That(M02MissingReturnFacts.TryReadApproved(read, out bool approved), Is.True);
            Assert.That(approved, Is.True);
        }
    }
}
