using System;
using GamingIsLove.Makinom;

namespace Rpg.Soundwell
{
    [Flags]
    public enum M09BerthExchangeFlags
    {
        None = 0,
        HanQiaoCivilianVoyage = 1 << 0,
        ShipwrightExtraBerths = 1 << 1,
        QiQueCargoVoyage = 1 << 2
    }

    public enum M09RuiyaOutcome
    {
        Rescued = 1,
        Deferred = 2,
        NotRescued = 3
    }

    /// <summary>
    /// Immutable M09 second-batch berth table and landing capacity record.
    /// Never double-counts M07's first-batch off-island evacuees, preserves
    /// unassigned pending names accurately, and gates Shen Li's reconciliation
    /// on her actual presence in the party.
    /// </summary>
    public readonly struct M09BerthTableRecord
    {
        public M09BerthTableRecord(
            int totalBerths,
            int pendingNames,
            M09BerthExchangeFlags exchanges,
            M09RuiyaOutcome ruiyaOutcome,
            bool qiDebtDisclosedHere,
            bool shenReconciled)
        {
            TotalBerths = totalBerths;
            PendingNames = pendingNames;
            Exchanges = exchanges;
            RuiyaOutcome = ruiyaOutcome;
            QiDebtDisclosedHere = qiDebtDisclosedHere;
            ShenReconciled = shenReconciled;
        }

        public int TotalBerths { get; }
        public int PendingNames { get; }
        public M09BerthExchangeFlags Exchanges { get; }
        public M09RuiyaOutcome RuiyaOutcome { get; }
        public bool QiDebtDisclosedHere { get; }
        public bool ShenReconciled { get; }
        public int AssignedCount => Math.Min(TotalBerths, PendingNames);
        public int RemainingUnassigned => Math.Max(0, PendingNames - TotalBerths);
    }

    /// <summary>
    /// Persists and reads back M09 second-batch landing capacity and Ruiya
    /// outcomes through Makinom's single global variable store, gated strictly
    /// on verified M07ExtractionFacts.
    /// </summary>
    public static class M09BerthTableFacts
    {
        public const string ConfirmedKey = "rpg.return.m09.berth-table-confirmed";
        public const string TotalBerthsKey = "rpg.return.m09.landing-capacity";
        public const string PendingNamesKey = "rpg.return.m09.pending-names";
        public const string ExchangesKey = "rpg.return.m09.berth-exchanges";
        public const string RuiyaOutcomeKey = "rpg.return.m09.survivor-ruiya";
        public const string QiDebtDisclosedKey = "rpg.return.m09.qi-debt-disclosed";
        public const string ShenReconciledKey = "rpg.return.m09.shen-reconciled";

        public const int DefaultBaseBerths = 18;
        public const int DefaultPendingNames = 23;
        public const int AllExchangesMask =
            (int)(M09BerthExchangeFlags.HanQiaoCivilianVoyage |
                  M09BerthExchangeFlags.ShipwrightExtraBerths |
                  M09BerthExchangeFlags.QiQueCargoVoyage);

        public static bool CanRecord(M09BerthTableRecord record, Func<string, int> getInt)
        {
            if (getInt == null || getInt(ConfirmedKey) != 0)
                return false;
            return IsValidRecord(record, getInt);
        }

        public static bool TryRecord(M09BerthTableRecord record)
        {
            var variables = Maki.Initialized ? Maki.Game?.Variables : null;
            return variables != null && TryRecord(
                record,
                key => variables.GetInt(key),
                (key, value) => variables.Set(key, value));
        }

        public static bool TryRecord(
            M09BerthTableRecord record,
            Func<string, int> getInt,
            Action<string, int> setInt)
        {
            if (getInt == null || setInt == null || !CanRecord(record, getInt))
                return false;

            setInt(ConfirmedKey, 0);
            setInt(TotalBerthsKey, record.TotalBerths);
            setInt(PendingNamesKey, record.PendingNames);
            setInt(ExchangesKey, (int)record.Exchanges);
            setInt(RuiyaOutcomeKey, (int)record.RuiyaOutcome);
            setInt(QiDebtDisclosedKey, record.QiDebtDisclosedHere ? 1 : 0);
            setInt(ShenReconciledKey, record.ShenReconciled ? 1 : 0);
            setInt(ConfirmedKey, 1);
            return true;
        }

        public static bool TryRead(out M09BerthTableRecord? record)
        {
            record = null;
            var variables = Maki.Initialized ? Maki.Game?.Variables : null;
            return variables != null && TryRead(key => variables.GetInt(key), out record);
        }

        public static bool TryRead(Func<string, int> getInt, out M09BerthTableRecord? record)
        {
            record = null;
            if (getInt == null)
                return false;
            if (getInt(ConfirmedKey) != 1)
                return true;

            int berths = getInt(TotalBerthsKey);
            int pending = getInt(PendingNamesKey);
            int rawExchanges = getInt(ExchangesKey);
            int rawRuiya = getInt(RuiyaOutcomeKey);
            int rawQiDebt = getInt(QiDebtDisclosedKey);
            int rawShen = getInt(ShenReconciledKey);

            if (rawExchanges < 0 || (rawExchanges & ~AllExchangesMask) != 0 ||
                !Enum.IsDefined(typeof(M09RuiyaOutcome), rawRuiya) ||
                (rawQiDebt != 0 && rawQiDebt != 1) ||
                (rawShen != 0 && rawShen != 1))
                return true;

            var saved = new M09BerthTableRecord(
                berths,
                pending,
                (M09BerthExchangeFlags)rawExchanges,
                (M09RuiyaOutcome)rawRuiya,
                rawQiDebt == 1,
                rawShen == 1);

            if (IsValidRecord(saved, getInt))
                record = saved;
            return true;
        }

        public static bool TryResetForNewVoyage()
        {
            var variables = Maki.Initialized ? Maki.Game?.Variables : null;
            return variables != null && TryResetForNewVoyage(
                (key, value) => variables.Set(key, value));
        }

        public static bool TryResetForNewVoyage(Action<string, int> setInt)
        {
            if (setInt == null)
                return false;
            setInt(ConfirmedKey, 0);
            setInt(TotalBerthsKey, 0);
            setInt(PendingNamesKey, 0);
            setInt(ExchangesKey, 0);
            setInt(RuiyaOutcomeKey, 0);
            setInt(QiDebtDisclosedKey, 0);
            setInt(ShenReconciledKey, 0);
            return true;
        }

        private static bool IsValidRecord(M09BerthTableRecord record, Func<string, int> getInt)
        {
            if (record.TotalBerths <= 0 || record.PendingNames <= 0)
                return false;
            int rawExchanges = (int)record.Exchanges;
            if (rawExchanges < 0 || (rawExchanges & ~AllExchangesMask) != 0)
                return false;
            if (!Enum.IsDefined(typeof(M09RuiyaOutcome), record.RuiyaOutcome))
                return false;
            if (record.QiDebtDisclosedHere &&
                (record.Exchanges & M09BerthExchangeFlags.QiQueCargoVoyage) == 0)
                return false;

            if (!M07ExtractionFacts.TryRead(getInt, out M07ExtractionRecord? m07) ||
                !m07.HasValue)
                return false;

            return true;
        }
    }
}
