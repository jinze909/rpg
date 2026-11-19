using System;
using GamingIsLove.Makinom;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Immutable summary snapshot of a reconciled M04 roster read back from
    /// the single existing Makinom global variable store.
    /// </summary>
    public readonly struct M04RosterSummaryRecord
    {
        public M04RosterSummaryRecord(
            int totalEntries,
            int confirmedAtDockCount,
            int bookedCount,
            int declinedCount)
        {
            TotalEntries = totalEntries;
            ConfirmedAtDockCount = confirmedAtDockCount;
            BookedCount = bookedCount;
            DeclinedCount = declinedCount;
        }

        public int TotalEntries { get; }
        public int ConfirmedAtDockCount { get; }
        public int BookedCount { get; }
        public int DeclinedCount { get; }
    }

    /// <summary>
    /// Persists, reads back, and replay-resets reconciled M04 roster summary
    /// counts through Makinom's global variable store (single RPG/ORK save
    /// authority). Ledgers with unresolved dead-roll discrepancies are
    /// rejected, and corrupted or inflated variable counts are never accepted.
    /// </summary>
    public static class M04RosterFacts
    {
        public const string ReconciledKey = "rpg.return.m04.roster-reconciled";
        public const string TotalEntriesKey = "rpg.return.m04.roster-total-entries";
        public const string ConfirmedAtDockKey = "rpg.return.m04.roster-confirmed-at-dock";
        public const string BookedCountKey = "rpg.return.m04.roster-booked-count";
        public const string DeclinedCountKey = "rpg.return.m04.roster-declined-count";

        public static bool TryResetForNewVisit()
        {
            var variables = Maki.Initialized ? Maki.Game?.Variables : null;
            if (variables == null)
                return false;
            return TryResetForNewVisit((key, value) => variables.Set(key, value));
        }

        public static bool TryResetForNewVisit(Action<string, int> setInt)
        {
            if (setInt == null)
                return false;
            // A replay cannot inherit an earlier voyage's reconciled roster counts.
            setInt(ReconciledKey, 0);
            setInt(TotalEntriesKey, 0);
            setInt(ConfirmedAtDockKey, 0);
            setInt(BookedCountKey, 0);
            setInt(DeclinedCountKey, 0);
            return true;
        }

        public static bool TryRecord(M04RosterLedger ledger)
        {
            ValidateReconciled(ledger);
            var variables = Maki.Initialized ? Maki.Game?.Variables : null;
            if (variables == null)
                return false;
            return TryRecord(ledger, (key, value) => variables.Set(key, value));
        }

        public static bool TryRecord(M04RosterLedger ledger, Action<string, int> setInt)
        {
            ValidateReconciled(ledger);
            if (setInt == null)
                return false;
            setInt(ReconciledKey, 1);
            setInt(TotalEntriesKey, ledger.TotalEntries);
            setInt(ConfirmedAtDockKey, ledger.ConfirmedAtDockCount);
            setInt(BookedCountKey, ledger.BookedCount);
            setInt(DeclinedCountKey, ledger.DeclinedCount);
            return true;
        }

        public static bool TryRead(out M04RosterSummaryRecord? record)
        {
            record = null;
            var variables = Maki.Initialized ? Maki.Game?.Variables : null;
            if (variables == null)
                return false;
            return TryRead(key => variables.GetInt(key), out record);
        }

        public static bool TryRead(Func<string, int> getInt, out M04RosterSummaryRecord? record)
        {
            record = null;
            if (getInt == null)
                return false;
            if (getInt(ReconciledKey) != 1)
                return true;

            int total = getInt(TotalEntriesKey);
            int confirmed = getInt(ConfirmedAtDockKey);
            int booked = getInt(BookedCountKey);
            int declined = getInt(DeclinedCountKey);

            // Reject negative or inflated counts rather than fabricating extra returnees.
            if (total < 0 || confirmed < 0 || booked < 0 || declined < 0)
                return true;
            if ((long)confirmed + booked + declined > total)
                return true;

            record = new M04RosterSummaryRecord(total, confirmed, booked, declined);
            return true;
        }

        private static void ValidateReconciled(M04RosterLedger ledger)
        {
            if (ledger == null)
                throw new ArgumentNullException(nameof(ledger));
            if (ledger.UnresolvedDiscrepancyCount > 0)
                throw new InvalidOperationException(
                    "Roster ledgers with unresolved dead-roll discrepancies cannot be recorded.");
        }
    }
}
