using System;

namespace Rpg.Soundwell
{
    public enum M10AssemblyPhase
    {
        NoticeAndArrival = 0,
        ThreeRecordReconciliation = 1,
        HanQiaoConfrontation = 2,
        SupplyContractTable = 3,
        BellTowerTurn = 4,
        Completed = 5
    }

    /// <summary>
    /// Authoritative M10 ("靖湾会盟") state machine.
    /// Guides the player from the third world notice ("桥在我在") through
    /// three-record spelling reconciliation (re-queueable without soft-lock),
    /// battle-report copy verification before confronting Han Qiao, supply
    /// contract signing, and the chapter-ending Huilan Pass bell-tower turn.
    /// </summary>
    public sealed class M10AssemblySession
    {
        public M10AssemblyPhase Phase { get; private set; } = M10AssemblyPhase.NoticeAndArrival;
        public bool WorldNoticeThreeObserved { get; private set; }
        public bool HadReconciliationDelay { get; private set; }
        public M10RosterAccuracy? RosterAccuracy { get; private set; }
        public M10BattleReportSource? BattleReportSource { get; private set; }
        public M10HanLedgerUse? HanLedgerUse { get; private set; }
        public M10SupplyContractStatus? SupplyContract { get; private set; }
        public bool BellTowerSeizureHeard { get; private set; }
        public M10AssemblyRecord? CompletedRecord { get; private set; }
        public bool IsCompleted => Phase == M10AssemblyPhase.Completed;

        public static bool TryCreateFromSavedFacts(
            Func<string, int> getInt,
            out M10AssemblySession session)
        {
            session = null;
            if (getInt == null)
                return false;
            if (!M09BerthTableFacts.TryRead(getInt, out M09BerthTableRecord? m09) ||
                !m09.HasValue)
                return false;

            session = new M10AssemblySession();
            return true;
        }

        public bool TryObserveWorldNoticeThree()
        {
            if (Phase != M10AssemblyPhase.NoticeAndArrival)
                return false;

            WorldNoticeThreeObserved = true;
            Phase = M10AssemblyPhase.ThreeRecordReconciliation;
            return true;
        }

        /// <summary>
        /// Reconciles the three records (east-shore list, M09 boarding log,
        /// and personal testimony). A mismatch delays and re-queues without
        /// soft-locking; a subsequent accurate match lets Yi Lei stamp clearance.
        /// </summary>
        public bool TryReconcileThreeRecords(bool matchedAccurately)
        {
            if (Phase != M10AssemblyPhase.ThreeRecordReconciliation)
                return false;

            if (!matchedAccurately)
            {
                HadReconciliationDelay = true;
                return false;
            }

            RosterAccuracy = HadReconciliationDelay
                ? M10RosterAccuracy.DelayedRequeued
                : M10RosterAccuracy.VerifiedFirstPass;
            Phase = M10AssemblyPhase.HanQiaoConfrontation;
            return true;
        }

        /// <summary>
        /// Establishes the actual battle-report copy (carried from M02 or
        /// fetched by Han Qiao from the archive) before han_ledger_use can be
        /// decided.
        /// </summary>
        public bool TrySecureBattleReportCopy(M10BattleReportSource source)
        {
            if (Phase != M10AssemblyPhase.HanQiaoConfrontation ||
                BattleReportSource.HasValue ||
                !Enum.IsDefined(typeof(M10BattleReportSource), source))
                return false;

            BattleReportSource = source;
            return true;
        }

        public bool TryDecideHanLedgerUse(M10HanLedgerUse use)
        {
            if (Phase != M10AssemblyPhase.HanQiaoConfrontation ||
                !BattleReportSource.HasValue ||
                !Enum.IsDefined(typeof(M10HanLedgerUse), use))
                return false;

            HanLedgerUse = use;
            Phase = M10AssemblyPhase.SupplyContractTable;
            return true;
        }

        public bool TrySignSupplyContract(M10SupplyContractStatus status)
        {
            if (Phase != M10AssemblyPhase.SupplyContractTable ||
                !Enum.IsDefined(typeof(M10SupplyContractStatus), status))
                return false;

            SupplyContract = status;
            Phase = M10AssemblyPhase.BellTowerTurn;
            return true;
        }

        public bool TryReceiveBellTowerSeizureReport()
        {
            if (Phase != M10AssemblyPhase.BellTowerTurn || BellTowerSeizureHeard)
                return false;

            BellTowerSeizureHeard = true;
            return true;
        }

        public bool TryCommit(Func<string, int> getInt, Action<string, int> setInt)
        {
            if (Phase != M10AssemblyPhase.BellTowerTurn ||
                !WorldNoticeThreeObserved ||
                !RosterAccuracy.HasValue ||
                !BattleReportSource.HasValue ||
                !HanLedgerUse.HasValue ||
                !SupplyContract.HasValue ||
                !BellTowerSeizureHeard ||
                getInt == null || setInt == null)
                return false;

            bool weakenedByRC02 = M10AssemblyFacts.EvaluateRC02LeveragePenalty(getInt);
            bool coversRC05 = M10AssemblyFacts.EvaluateRC05SupplyNeed(getInt) &&
                SupplyContract.Value == M10SupplyContractStatus.SignedFullCoverage;

            var record = new M10AssemblyRecord(
                worldNoticeThreeObserved: true,
                RosterAccuracy.Value,
                BattleReportSource.Value,
                HanLedgerUse.Value,
                SupplyContract.Value,
                weakenedByRC02,
                coversRC05,
                bellTowerSeizureHeard: true);

            if (!M10AssemblyFacts.TryRead(getInt, out M10AssemblyRecord? existing))
                return false;

            // Recover idempotently if a prior write committed ConfirmedKey = 1
            // before its acknowledgement was lost.
            if (existing.HasValue)
            {
                if (!Matches(record, existing.Value))
                    return false;
            }
            else if (getInt(M10AssemblyFacts.ConfirmedKey) != 0 ||
                     !M10AssemblyFacts.TryRecord(record, getInt, setInt))
            {
                return false;
            }

            if (!M10AssemblyFacts.TryRead(getInt, out M10AssemblyRecord? saved) ||
                !saved.HasValue || !Matches(record, saved.Value))
                return false;

            CompletedRecord = saved.Value;
            Phase = M10AssemblyPhase.Completed;
            return true;
        }

        private static bool Matches(M10AssemblyRecord expected, M10AssemblyRecord saved)
        {
            return expected.WorldNoticeThreeObserved == saved.WorldNoticeThreeObserved &&
                expected.RosterAccuracy == saved.RosterAccuracy &&
                expected.BattleReportSource == saved.BattleReportSource &&
                expected.HanLedgerUse == saved.HanLedgerUse &&
                expected.SupplyContract == saved.SupplyContract &&
                expected.LeverageWeakenedByRC02 == saved.LeverageWeakenedByRC02 &&
                expected.CoversRC05MountainFamilies == saved.CoversRC05MountainFamilies &&
                expected.BellTowerSeizureHeard == saved.BellTowerSeizureHeard;
        }
    }
}
