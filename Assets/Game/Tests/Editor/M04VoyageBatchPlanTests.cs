using System;
using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    public sealed class M04VoyageBatchPlanTests
    {
        [Test]
        public void InvalidBerthsOrNegativeReturneesAreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new M04VoyageBatchPlan(-1, 18));
            Assert.Throws<ArgumentOutOfRangeException>(() => new M04VoyageBatchPlan(10, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new M04VoyageBatchPlan(10, -5));
            Assert.Throws<ArgumentNullException>(() => M04VoyageBatchPlan.PartitionConfirmed(null, 18));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                M04VoyageBatchPlan.PartitionConfirmed(new M04RosterLedger(), 0));
        }

        [Test]
        public void ZeroReturneesRequiresZeroTripsAndNoOverflow()
        {
            var plan = new M04VoyageBatchPlan(0, 18);
            Assert.That(plan.TotalReturnees, Is.Zero);
            Assert.That(plan.RequiredTrips, Is.Zero);
            Assert.That(plan.ExceedsSingleTrip, Is.False);
            Assert.That(plan.OverflowReturnees, Is.Zero);
            Assert.That(plan.TripHeadcounts(), Is.Empty);
        }

        [Test]
        public void ReturneesWithinSingleTripFitInOneBatchWithoutOverflow()
        {
            var plan = new M04VoyageBatchPlan(18, 18);
            Assert.That(plan.RequiredTrips, Is.EqualTo(1));
            Assert.That(plan.ExceedsSingleTrip, Is.False);
            Assert.That(plan.OverflowReturnees, Is.Zero);
            Assert.That(plan.TripHeadcounts(), Is.EqualTo(new[] { 18 }));
        }

        [Test]
        public void OvercapacitySplitsIntoExactFactualBatchesWithoutInventingBerths()
        {
            var plan = new M04VoyageBatchPlan(41, 18);
            Assert.That(plan.TotalReturnees, Is.EqualTo(41));
            Assert.That(plan.VerifiedBerthsPerTrip, Is.EqualTo(18));
            Assert.That(plan.RequiredTrips, Is.EqualTo(3));
            Assert.That(plan.ExceedsSingleTrip, Is.True);
            Assert.That(plan.OverflowReturnees, Is.EqualTo(23));

            int[] batches = plan.TripHeadcounts();
            Assert.That(batches, Is.EqualTo(new[] { 18, 18, 5 }));

            // Mutating the returned array must not corrupt internal plan state.
            batches[0] = 99;
            Assert.That(plan.TripHeadcounts(), Is.EqualTo(new[] { 18, 18, 5 }));
        }

        [Test]
        public void PartitionConfirmedFiltersEligibleEntriesAndRejectsUnresolvedDiscrepancies()
        {
            var ledger = new M04RosterLedger();
            ledger.AddEntry("dead-01", "Listed dead", M04RosterStatus.ListedDead);
            ledger.AddEntry("unconfirmed-01", "Unconfirmed", M04RosterStatus.RansomedUnconfirmed);
            ledger.AddEntry("roll-01", "Reconciled", M04RosterStatus.ListedDead);
            ledger.MarkConfirmedAtDock("roll-01");

            Assert.Throws<InvalidOperationException>(() =>
                M04VoyageBatchPlan.PartitionConfirmed(ledger, 2));

            ledger.ResolveRollCorrection("roll-01");
            ledger.AddEntry("dock-01", "Confirmed 1", M04RosterStatus.ConfirmedAtDock);
            ledger.AddEntry("dock-02", "Confirmed 2", M04RosterStatus.ConfirmedAtDock);
            ledger.AddEntry("declined-01", "Declined", M04RosterStatus.ConfirmedAtDock);
            ledger.MarkDeclinedVoyage("declined-01");
            Assert.That(ledger.TryBookForVoyage("dock-01", 2), Is.True);

            M04RosterEntry[][] batches = M04VoyageBatchPlan.PartitionConfirmed(ledger, 2);
            Assert.That(batches.Length, Is.EqualTo(2));
            Assert.That(batches[0].Length, Is.EqualTo(2));
            Assert.That(batches[0][0].EntryId, Is.EqualTo("roll-01"));
            Assert.That(batches[0][1].EntryId, Is.EqualTo("dock-01"));
            Assert.That(batches[1].Length, Is.EqualTo(1));
            Assert.That(batches[1][0].EntryId, Is.EqualTo("dock-02"));
        }
    }
}
