using System;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Authoritative M09 second-batch berth table negotiation session.
    /// Computes shortfall dynamically from actual base berths and pending
    /// second-batch names (default 18 berths / 23 pending = 5 shortfall,
    /// separate from M07's first-batch evacuees), allows combining any subset
    /// of the three capacity exchanges, discloses Qi Que's ship debt if not
    /// already known, and never plays Shen Li's reconciliation when she is
    /// absent or has split from the party.
    /// </summary>
    public sealed class M09BerthTableSession
    {
        public int BaseBerths { get; }
        public int PendingNames { get; }
        public bool ShenLiPresent { get; }
        public bool QiDebtPreviouslyKnown { get; }
        public int AddedBerths { get; private set; }
        public M09BerthExchangeFlags NegotiatedExchanges { get; private set; }
        public bool OfficialLivingRegistrationRequired =>
            (NegotiatedExchanges & M09BerthExchangeFlags.HanQiaoCivilianVoyage) != 0;
        public bool ShenLiTwoDayApprenticeLeave =>
            (NegotiatedExchanges & M09BerthExchangeFlags.ShipwrightExtraBerths) != 0;
        public bool QiDebtDisclosedHere { get; private set; }
        public bool ShenReconciled { get; private set; }
        public bool IsSettled { get; private set; }

        public int TotalBerths => BaseBerths + AddedBerths;
        public int CurrentShortfall => Math.Max(0, PendingNames - TotalBerths);
        public int AssignedNames => Math.Min(TotalBerths, PendingNames);

        public M09BerthTableSession(
            int baseBerths = M09BerthTableFacts.DefaultBaseBerths,
            int pendingNames = M09BerthTableFacts.DefaultPendingNames,
            bool shenLiPresent = true,
            bool qiDebtPreviouslyKnown = false)
        {
            if (baseBerths <= 0)
                throw new ArgumentOutOfRangeException(nameof(baseBerths));
            if (pendingNames <= 0)
                throw new ArgumentOutOfRangeException(nameof(pendingNames));

            BaseBerths = baseBerths;
            PendingNames = pendingNames;
            ShenLiPresent = shenLiPresent;
            QiDebtPreviouslyKnown = qiDebtPreviouslyKnown;
        }

        public bool TryNegotiateExchange(M09BerthExchangeFlags exchange, int berthsGained)
        {
            if (IsSettled || berthsGained <= 0)
                return false;

            if (exchange != M09BerthExchangeFlags.HanQiaoCivilianVoyage &&
                exchange != M09BerthExchangeFlags.ShipwrightExtraBerths &&
                exchange != M09BerthExchangeFlags.QiQueCargoVoyage)
                throw new ArgumentOutOfRangeException(nameof(exchange),
                    "Negotiate one authored M09 exchange at a time.");

            if ((NegotiatedExchanges & exchange) != 0)
                return false;

            // Shipwright extra berths require Shen Li's contact & 2-day teaching commitment.
            if (exchange == M09BerthExchangeFlags.ShipwrightExtraBerths && !ShenLiPresent)
                return false;

            NegotiatedExchanges |= exchange;
            AddedBerths += berthsGained;

            if (exchange == M09BerthExchangeFlags.QiQueCargoVoyage && !QiDebtPreviouslyKnown)
                QiDebtDisclosedHere = true;

            return true;
        }

        public bool TryPlayShenReconciliation()
        {
            if (IsSettled || !ShenLiPresent || ShenReconciled)
                return false;
            ShenReconciled = true;
            return true;
        }

        public bool TrySettleBerthTable(
            M09RuiyaOutcome ruiyaOutcome,
            Func<string, int> getInt,
            Action<string, int> setInt)
        {
            if (IsSettled)
                return false;
            if (!Enum.IsDefined(typeof(M09RuiyaOutcome), ruiyaOutcome))
                throw new ArgumentOutOfRangeException(nameof(ruiyaOutcome));

            var record = new M09BerthTableRecord(
                TotalBerths,
                PendingNames,
                NegotiatedExchanges,
                ruiyaOutcome,
                QiDebtDisclosedHere,
                ShenReconciled);

            if (!M09BerthTableFacts.TryRecord(record, getInt, setInt))
                return false;

            IsSettled = true;
            return true;
        }
    }
}
