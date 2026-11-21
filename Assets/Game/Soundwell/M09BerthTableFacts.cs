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
            bool shenReconciled,
            bool shipwrightFeePaid = false)
        {
            TotalBerths = totalBerths;
            PendingNames = pendingNames;
            Exchanges = exchanges;
            RuiyaOutcome = ruiyaOutcome;
            QiDebtDisclosedHere = qiDebtDisclosedHere;
            ShenReconciled = shenReconciled;
            ShipwrightFeePaid = shipwrightFeePaid;
        }

        public int TotalBerths { get; }
        public int PendingNames { get; }
        public M09BerthExchangeFlags Exchanges { get; }
        public M09RuiyaOutcome RuiyaOutcome { get; }
        public bool QiDebtDisclosedHere { get; }
        public bool ShenReconciled { get; }
        // True only after the party actually pays the shipyard in place
        // of asking Shen Li to teach the apprentices for two days.
        public bool ShipwrightFeePaid { get; }
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
        public const string PendingSignatureKey = "rpg.return.m09.pending-table-signature";
        public const string TotalBerthsKey = "rpg.return.m09.landing-capacity";
        public const string PendingNamesKey = "rpg.return.m09.pending-names";
        public const string ExchangesKey = "rpg.return.m09.berth-exchanges";
        public const string RuiyaOutcomeKey = "rpg.return.m09.survivor-ruiya";
        public const string QiDebtDisclosedKey = "rpg.return.m09.qi-debt-disclosed";
        public const string ShenReconciledKey = "rpg.return.m09.shen-reconciled";
        public const string ShipwrightFeePaidKey = "rpg.return.m09.shipwright-fee-paid";

        public const int DefaultBaseBerths = 18;
        public const int DefaultPendingNames = 23;
        public const int AllExchangesMask =
            (int)(M09BerthExchangeFlags.HanQiaoCivilianVoyage |
                  M09BerthExchangeFlags.ShipwrightExtraBerths |
                  M09BerthExchangeFlags.QiQueCargoVoyage);

        // A torn Makinom write must not be retried with a different set of
        // passengers, exchanges, debt, Shen Li consent or Ruiya outcome.
        // The signed intent is stored first, before any individual payload.
        private static uint MixSignature(uint hash, int value)
        {
            unchecked
            {
                for (int shift = 0; shift < 32; shift += 8)
                {
                    hash ^= (byte)(value >> shift);
                    hash *= 16777619u;
                }
                return hash;
            }
        }

        private static int Signature(M09BerthTableRecord record, Func<string, int> read)
        {
            unchecked
            {
                uint hash = 2166136261u;
                hash = MixSignature(hash, record.TotalBerths);
                hash = MixSignature(hash, record.PendingNames);
                hash = MixSignature(hash, (int)record.Exchanges);
                hash = MixSignature(hash, (int)record.RuiyaOutcome);
                hash = MixSignature(hash, record.QiDebtDisclosedHere ? 1 : 0);
                hash = MixSignature(hash, record.ShenReconciled ? 1 : 0);
                hash = MixSignature(hash, record.ShipwrightFeePaid ? 1 : 0);
                hash = MixSignature(hash, read(M05LandingFacts.ArrivalSequenceKey));
                int signature = (int)hash;
                return signature == 0 ? 1 : signature;
            }
        }

        private static bool CompatiblePending(Func<string, int> read, string key, int expected)
        {
            int actual = read(key);
            return actual == 0 || actual == expected;
        }

        public static bool HasOrphanedDownstreamFacts(Func<string, int> getInt)
        {
            return getInt == null ||
                M10AssemblyFacts.HasAnyPersistedFacts(getInt) ||
                M10AssemblyFacts.HasOrphanedDownstreamFacts(getInt);
        }

        public static bool CanRecord(M09BerthTableRecord record, Func<string, int> getInt)
        {
            if (getInt == null || getInt(ConfirmedKey) != 0 ||
                HasOrphanedDownstreamFacts(getInt) ||
                !IsValidRecord(record, getInt))
                return false;

            int pending = getInt(PendingSignatureKey);
            if (pending == 0)
            {
                // A legacy orphan payload with no signed intent is ambiguous:
                // only an empty table may start an entirely new write.
                return getInt(TotalBerthsKey) == 0 &&
                    getInt(PendingNamesKey) == 0 &&
                    getInt(ExchangesKey) == 0 &&
                    getInt(RuiyaOutcomeKey) == 0 &&
                    getInt(QiDebtDisclosedKey) == 0 &&
                    getInt(ShenReconciledKey) == 0 &&
                    getInt(ShipwrightFeePaidKey) == 0;
            }

            return pending == Signature(record, getInt) &&
                CompatiblePending(getInt, TotalBerthsKey, record.TotalBerths) &&
                CompatiblePending(getInt, PendingNamesKey, record.PendingNames) &&
                CompatiblePending(getInt, ExchangesKey, (int)record.Exchanges) &&
                CompatiblePending(getInt, RuiyaOutcomeKey, (int)record.RuiyaOutcome) &&
                CompatiblePending(getInt, QiDebtDisclosedKey, record.QiDebtDisclosedHere ? 1 : 0) &&
                CompatiblePending(getInt, ShenReconciledKey, record.ShenReconciled ? 1 : 0) &&
                CompatiblePending(getInt, ShipwrightFeePaidKey, record.ShipwrightFeePaid ? 1 : 0);
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

            setInt(PendingSignatureKey, Signature(record, getInt));
            setInt(ConfirmedKey, 0);
            setInt(TotalBerthsKey, record.TotalBerths);
            setInt(PendingNamesKey, record.PendingNames);
            setInt(ExchangesKey, (int)record.Exchanges);
            setInt(RuiyaOutcomeKey, (int)record.RuiyaOutcome);
            setInt(QiDebtDisclosedKey, record.QiDebtDisclosedHere ? 1 : 0);
            setInt(ShenReconciledKey, record.ShenReconciled ? 1 : 0);
            setInt(ShipwrightFeePaidKey, record.ShipwrightFeePaid ? 1 : 0);
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
            int rawShipwrightFee = getInt(ShipwrightFeePaidKey);

            if (rawExchanges < 0 || (rawExchanges & ~AllExchangesMask) != 0 ||
                !Enum.IsDefined(typeof(M09RuiyaOutcome), rawRuiya) ||
                (rawQiDebt != 0 && rawQiDebt != 1) ||
                (rawShen != 0 && rawShen != 1) ||
                (rawShipwrightFee != 0 && rawShipwrightFee != 1))
                return true;

            var saved = new M09BerthTableRecord(
                berths,
                pending,
                (M09BerthExchangeFlags)rawExchanges,
                (M09RuiyaOutcome)rawRuiya,
                rawQiDebt == 1,
                rawShen == 1,
                rawShipwrightFee == 1);

            // Old confirmed saves predate this intent key. New confirmed
            // saves must still match their original signed facts and arrival.
            int pendingSignature = getInt(PendingSignatureKey);
            if (IsValidRecord(saved, getInt) &&
                (pendingSignature == 0 || pendingSignature == Signature(saved, getInt)))
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
            setInt(PendingSignatureKey, 0);
            setInt(TotalBerthsKey, 0);
            setInt(PendingNamesKey, 0);
            setInt(ExchangesKey, 0);
            setInt(RuiyaOutcomeKey, 0);
            setInt(QiDebtDisclosedKey, 0);
            setInt(ShenReconciledKey, 0);
            setInt(ShipwrightFeePaidKey, 0);
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
            // A shipyard fee has a real, separately negotiated cost: it
            // cannot be saved unless the shipwright berth deal exists.
            if (record.ShipwrightFeePaid &&
                (record.Exchanges & M09BerthExchangeFlags.ShipwrightExtraBerths) == 0)
                return false;

            if (!M07ExtractionFacts.TryRead(getInt, out M07ExtractionRecord? m07) ||
                !m07.HasValue)
                return false;

            return true;
        }
    }
}
