using System;
using System.Collections.Generic;

namespace Rpg.Soundwell
{
    public enum M04RosterStatus
    {
        ListedDead,
        RansomedUnconfirmed,
        ConfirmedAtDock,
        DeclinedVoyage,
        BookedForVoyage
    }

    /// <summary>
    /// Tracks one named person during M04 roster reconciliation without
    /// inventing extra returnees or silently erasing dead-roll discrepancies.
    /// </summary>
    public sealed class M04RosterEntry
    {
        internal M04RosterEntry(string entryId, string displayName, M04RosterStatus status)
        {
            EntryId = entryId;
            DisplayName = displayName;
            Status = status;
            WasListedDead = status == M04RosterStatus.ListedDead;
        }

        public string EntryId { get; }
        public string DisplayName { get; }
        public M04RosterStatus Status { get; internal set; }
        public bool WasListedDead { get; }
        public bool RequiresRollCorrection { get; internal set; }
    }

    /// <summary>
    /// Factual in-memory ledger for M04 roster reconciliation: deduplicates
    /// entries, tracks living-vs-dead roll discrepancies, and enforces that
    /// only confirmed dockside returnees can occupy verified berths.
    /// Does not create an independent save authority or fabricate ship capacity.
    /// </summary>
    public sealed class M04RosterLedger
    {
        private readonly Dictionary<string, M04RosterEntry> byId =
            new Dictionary<string, M04RosterEntry>(StringComparer.OrdinalIgnoreCase);
        private readonly List<M04RosterEntry> ordered = new List<M04RosterEntry>();

        public int TotalEntries => ordered.Count;

        public int ConfirmedAtDockCount => CountByStatus(M04RosterStatus.ConfirmedAtDock);

        public int BookedCount => CountByStatus(M04RosterStatus.BookedForVoyage);

        public int DeclinedCount => CountByStatus(M04RosterStatus.DeclinedVoyage);

        public int UnresolvedDiscrepancyCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < ordered.Count; i++)
                {
                    if (ordered[i].RequiresRollCorrection)
                        count++;
                }
                return count;
            }
        }

        public M04RosterEntry AddEntry(
            string entryId,
            string displayName,
            M04RosterStatus initialStatus)
        {
            if (string.IsNullOrWhiteSpace(entryId))
                throw new ArgumentException("Entry ID is required.", nameof(entryId));
            if (string.IsNullOrWhiteSpace(displayName))
                throw new ArgumentException("Display name is required.", nameof(displayName));
            if (!Enum.IsDefined(typeof(M04RosterStatus), initialStatus))
                throw new ArgumentOutOfRangeException(nameof(initialStatus));
            if (initialStatus == M04RosterStatus.BookedForVoyage)
                throw new InvalidOperationException(
                    "Entries must be booked against verified berths rather than pre-booked.");

            string normalizedId = entryId.Trim();
            if (byId.ContainsKey(normalizedId))
                throw new InvalidOperationException(
                    "Duplicate roster entry ID: " + normalizedId);

            var entry = new M04RosterEntry(normalizedId, displayName.Trim(), initialStatus);
            byId.Add(normalizedId, entry);
            ordered.Add(entry);
            return entry;
        }

        public M04RosterEntry GetEntry(string entryId)
        {
            if (string.IsNullOrWhiteSpace(entryId))
                return null;
            return byId.TryGetValue(entryId.Trim(), out M04RosterEntry entry) ? entry : null;
        }

        public M04RosterEntry[] Snapshot()
        {
            return ordered.ToArray();
        }

        public void MarkConfirmedAtDock(string entryId)
        {
            M04RosterEntry entry = RequireEntry(entryId);
            if (entry.Status != M04RosterStatus.ListedDead &&
                entry.Status != M04RosterStatus.RansomedUnconfirmed)
                throw new InvalidOperationException(
                    "Only unconfirmed or dead-roll entries can be newly confirmed at dock.");

            entry.Status = M04RosterStatus.ConfirmedAtDock;
            if (entry.WasListedDead)
                entry.RequiresRollCorrection = true;
        }

        public void ResolveRollCorrection(string entryId)
        {
            M04RosterEntry entry = RequireEntry(entryId);
            if (!entry.RequiresRollCorrection)
                throw new InvalidOperationException(
                    "Entry has no pending dead-roll discrepancy to resolve.");
            entry.RequiresRollCorrection = false;
        }

        public bool TryBookForVoyage(string entryId, int verifiedBerths)
        {
            if (verifiedBerths < 0)
                throw new ArgumentOutOfRangeException(nameof(verifiedBerths));
            M04RosterEntry entry = GetEntry(entryId);
            if (entry == null || entry.Status != M04RosterStatus.ConfirmedAtDock)
                return false;
            // A survivor found alive on the dead roll must have that record
            // corrected before they can occupy a booked rescue berth.
            if (entry.RequiresRollCorrection)
                return false;
            if (BookedCount >= verifiedBerths)
                return false;

            entry.Status = M04RosterStatus.BookedForVoyage;
            return true;
        }

        public void UnbookFromVoyage(string entryId)
        {
            M04RosterEntry entry = RequireEntry(entryId);
            if (entry.Status != M04RosterStatus.BookedForVoyage)
                throw new InvalidOperationException(
                    "Only currently booked returnees can be unbooked.");
            entry.Status = M04RosterStatus.ConfirmedAtDock;
        }

        public void MarkDeclinedVoyage(string entryId)
        {
            M04RosterEntry entry = RequireEntry(entryId);
            if (entry.Status != M04RosterStatus.ConfirmedAtDock &&
                entry.Status != M04RosterStatus.BookedForVoyage)
                throw new InvalidOperationException(
                    "Only confirmed or booked returnees can decline this voyage.");
            entry.Status = M04RosterStatus.DeclinedVoyage;
        }

        // A decline made during an unsubmitted reconciliation is reversible.
        // Reconsideration does not silently restore a previous berth booking:
        // the player must book again against the current verified capacity.
        public void ReconsiderDeclinedVoyage(string entryId)
        {
            M04RosterEntry entry = RequireEntry(entryId);
            if (entry.Status != M04RosterStatus.DeclinedVoyage)
                throw new InvalidOperationException(
                    "Only declined returnees can reconsider this voyage.");
            entry.Status = M04RosterStatus.ConfirmedAtDock;
        }

        private M04RosterEntry RequireEntry(string entryId)
        {
            M04RosterEntry entry = GetEntry(entryId);
            if (entry == null)
                throw new KeyNotFoundException("Roster entry not found: " + entryId);
            return entry;
        }

        private int CountByStatus(M04RosterStatus status)
        {
            int count = 0;
            for (int i = 0; i < ordered.Count; i++)
            {
                if (ordered[i].Status == status)
                    count++;
            }
            return count;
        }
    }
}
