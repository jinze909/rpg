using System;
using UnityEngine;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Scene-callable M09 second-batch berth table host. The owning scene
    /// supplies live location, party, berth count and Ruiya outcome facts;
    /// Makinom remains the only persistence authority.
    /// </summary>
    public sealed class M09BerthTableHost : IDisposable
    {
        private readonly Transform parent;
        private readonly Func<bool> atSecondBatchTable;
        private readonly Func<bool> shenLiPresent;
        private readonly Func<bool> qiDebtAlreadyKnown;
        private readonly Func<int> baseBerths;
        private readonly Func<int> pendingNames;
        private readonly Func<M09RuiyaOutcome?> ruiyaOutcome;
        private readonly Func<string, int> getInt;
        private readonly Action<string, int> setInt;
        // M05's monotonic arrival identity distinguishes otherwise identical
        // replayed voyages; an old berth negotiation must not settle the next one.
        private int? openedArrivalSequence;
        private M09BerthTableSession session;
        private M09BerthTableView view;
        private bool disposed;

        public M09BerthTableSession Session => session;
        public M09BerthTableView ActiveView => view;
        public bool IsDisposed => disposed;
        public bool HasRecordedTable =>
            M09BerthTableFacts.TryRead(getInt, out M09BerthTableRecord? record) &&
            record.HasValue;

        public M09BerthTableHost(
            Transform parent,
            Func<bool> atSecondBatchTable,
            Func<bool> shenLiPresent,
            Func<bool> qiDebtAlreadyKnown,
            Func<int> baseBerths,
            Func<int> pendingNames,
            Func<M09RuiyaOutcome?> ruiyaOutcome,
            Func<string, int> getInt,
            Action<string, int> setInt)
        {
            this.parent = parent ?? throw new ArgumentNullException(nameof(parent));
            this.atSecondBatchTable = atSecondBatchTable ??
                throw new ArgumentNullException(nameof(atSecondBatchTable));
            this.shenLiPresent = shenLiPresent ??
                throw new ArgumentNullException(nameof(shenLiPresent));
            this.qiDebtAlreadyKnown = qiDebtAlreadyKnown ??
                throw new ArgumentNullException(nameof(qiDebtAlreadyKnown));
            this.baseBerths = baseBerths ?? throw new ArgumentNullException(nameof(baseBerths));
            this.pendingNames = pendingNames ?? throw new ArgumentNullException(nameof(pendingNames));
            this.ruiyaOutcome = ruiyaOutcome ??
                throw new ArgumentNullException(nameof(ruiyaOutcome));
            this.getInt = getInt ?? throw new ArgumentNullException(nameof(getInt));
            this.setInt = setInt ?? throw new ArgumentNullException(nameof(setInt));
        }

        public bool TryOpen()
        {
            if (disposed || view != null || !atSecondBatchTable() ||
                // A corrupt or already-confirmed M09 record must not be rewritten.
                getInt(M09BerthTableFacts.ConfirmedKey) != 0 ||
                !M07ExtractionFacts.TryRead(getInt, out M07ExtractionRecord? m07) ||
                !m07.HasValue || getInt(M05LandingFacts.ArrivalSequenceKey) < 0)
                return false;

            int available = baseBerths();
            int waiting = pendingNames();
            if (available <= 0 || waiting <= 0)
                return false;

            openedArrivalSequence = getInt(M05LandingFacts.ArrivalSequenceKey);
            session = new M09BerthTableSession(
                available, waiting, shenLiPresent(), qiDebtAlreadyKnown());
            view = new M09BerthTableView(parent, session, ConfirmTable);
            return true;
        }

        private bool LiveSceneStillMatches()
        {
            return !disposed && atSecondBatchTable() && session != null &&
                openedArrivalSequence.HasValue &&
                getInt(M05LandingFacts.ArrivalSequenceKey) == openedArrivalSequence.Value &&
                shenLiPresent() == session.ShenLiPresent &&
                qiDebtAlreadyKnown() == session.QiDebtPreviouslyKnown &&
                baseBerths() == session.BaseBerths &&
                pendingNames() == session.PendingNames &&
                M07ExtractionFacts.TryRead(getInt, out M07ExtractionRecord? m07) &&
                m07.HasValue;
        }

        private void ConfirmTable(M09BerthTableSession proposed)
        {
            if (view == null || proposed != session || !LiveSceneStillMatches())
                return;

            M09RuiyaOutcome? outcome = ruiyaOutcome();
            if (!outcome.HasValue ||
                !Enum.IsDefined(typeof(M09RuiyaOutcome), outcome.Value) ||
                !M09BerthTableFacts.TryRead(getInt, out M09BerthTableRecord? existing))
                return;

            // The confirmed flag is a commit marker. If a prior write
            // committed but its acknowledgement was lost, recognize the
            // identical snapshot and never write it a second time.
            if (existing.HasValue)
            {
                if (!Matches(existing.Value, proposed, outcome.Value))
                    return;
            }
            else if (getInt(M09BerthTableFacts.ConfirmedKey) != 0 ||
                     !proposed.TrySettleBerthTable(outcome.Value, getInt, setInt))
            {
                return;
            }

            if (!M09BerthTableFacts.TryRead(getInt, out M09BerthTableRecord? saved) ||
                !saved.HasValue || !Matches(saved.Value, proposed, outcome.Value))
                return;

            view.Dispose();
            view = null;
        }

        private static bool Matches(
            M09BerthTableRecord record,
            M09BerthTableSession active,
            M09RuiyaOutcome outcome)
        {
            return record.TotalBerths == active.TotalBerths &&
                record.PendingNames == active.PendingNames &&
                record.Exchanges == active.NegotiatedExchanges &&
                record.RuiyaOutcome == outcome &&
                record.QiDebtDisclosedHere == active.QiDebtDisclosedHere &&
                record.ShenReconciled == active.ShenReconciled &&
                record.ShipwrightFeePaid == active.ShipwrightFeePaid;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            view?.Dispose();
            view = null;
            openedArrivalSequence = null;
        }
    }
}
