using System;
using System.Collections.Generic;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Factual multi-trip voyage batch planner for M04 when confirmed returnees
    /// exceed a single vessel's verified berths. Computes exact trip counts and
    /// per-trip headcounts without inventing extra bunks or dropping returnees.
    /// </summary>
    public sealed class M04VoyageBatchPlan
    {
        private readonly int[] tripHeadcounts;

        public M04VoyageBatchPlan(int confirmedReturnees, int verifiedBerthsPerTrip)
        {
            if (confirmedReturnees < 0)
                throw new ArgumentOutOfRangeException(nameof(confirmedReturnees));
            if (verifiedBerthsPerTrip <= 0)
                throw new ArgumentOutOfRangeException(nameof(verifiedBerthsPerTrip));

            TotalReturnees = confirmedReturnees;
            VerifiedBerthsPerTrip = verifiedBerthsPerTrip;

            if (confirmedReturnees == 0)
            {
                tripHeadcounts = Array.Empty<int>();
                return;
            }

            int trips = (confirmedReturnees + verifiedBerthsPerTrip - 1) / verifiedBerthsPerTrip;
            tripHeadcounts = new int[trips];
            int remaining = confirmedReturnees;
            for (int i = 0; i < trips; i++)
            {
                int batch = remaining > verifiedBerthsPerTrip ? verifiedBerthsPerTrip : remaining;
                tripHeadcounts[i] = batch;
                remaining -= batch;
            }
        }

        public int TotalReturnees { get; }
        public int VerifiedBerthsPerTrip { get; }
        public int RequiredTrips => tripHeadcounts.Length;
        public bool ExceedsSingleTrip => TotalReturnees > VerifiedBerthsPerTrip;
        public int OverflowReturnees =>
            TotalReturnees > VerifiedBerthsPerTrip ? TotalReturnees - VerifiedBerthsPerTrip : 0;

        public int[] TripHeadcounts()
        {
            var copy = new int[tripHeadcounts.Length];
            Array.Copy(tripHeadcounts, copy, tripHeadcounts.Length);
            return copy;
        }

        /// <summary>
        /// Partitions eligible confirmed/booked entries from a reconciled
        /// M04RosterLedger into ordered sailing batches within verifiedBerthsPerTrip.
        /// Rejects ledgers with unresolved dead-roll discrepancies and excludes
        /// dead-roll, unconfirmed, or declined entries.
        /// </summary>
        public static M04RosterEntry[][] PartitionConfirmed(
            M04RosterLedger ledger,
            int verifiedBerthsPerTrip)
        {
            if (ledger == null)
                throw new ArgumentNullException(nameof(ledger));
            if (verifiedBerthsPerTrip <= 0)
                throw new ArgumentOutOfRangeException(nameof(verifiedBerthsPerTrip));
            if (ledger.UnresolvedDiscrepancyCount > 0)
                throw new InvalidOperationException(
                    "Cannot batch voyages while dead-roll discrepancies remain unresolved.");

            M04RosterEntry[] snapshot = ledger.Snapshot();
            var eligible = new List<M04RosterEntry>();
            for (int i = 0; i < snapshot.Length; i++)
            {
                M04RosterStatus status = snapshot[i].Status;
                if (status == M04RosterStatus.ConfirmedAtDock ||
                    status == M04RosterStatus.BookedForVoyage)
                {
                    eligible.Add(snapshot[i]);
                }
            }

            if (eligible.Count == 0)
                return Array.Empty<M04RosterEntry[]>();

            int trips = (eligible.Count + verifiedBerthsPerTrip - 1) / verifiedBerthsPerTrip;
            var batches = new M04RosterEntry[trips][];
            int offset = 0;
            for (int t = 0; t < trips; t++)
            {
                int remaining = eligible.Count - offset;
                int size = remaining > verifiedBerthsPerTrip ? verifiedBerthsPerTrip : remaining;
                var batch = new M04RosterEntry[size];
                for (int j = 0; j < size; j++)
                    batch[j] = eligible[offset + j];
                batches[t] = batch;
                offset += size;
            }

            return batches;
        }
    }
}
