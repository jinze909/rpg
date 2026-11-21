using System;
using UnityEngine;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Scene-callable M10 ("靖湾会盟") host. Enforces verified M09BerthTableFacts,
    /// live Jingwan assembly presence, monotonic M05 arrival identity, and clean
    /// M10AssemblyFacts state (blocking torn/partial saves) before opening
    /// M10AssemblyView, and persists the outcome through M10AssemblySession with
    /// retry-safe idempotent commit recovery.
    /// </summary>
    public sealed class M10AssemblyHost : IDisposable
    {
        private readonly Transform parent;
        private readonly Func<bool> atJingwanAssembly;
        private readonly Func<bool> threeRecordsMatched;
        private readonly Func<bool> carriesM02BattleReport;
        private readonly Func<string, int> getInt;
        private readonly Action<string, int> setInt;
        private M10AssemblySession session;
        private M10AssemblyView activeView;
        private int? openedArrivalSequence;
        private bool disposed;

        public M10AssemblySession Session => session;
        public M10AssemblyView ActiveView => activeView;
        public bool IsDisposed => disposed;
        public bool IsCompleted =>
            M10AssemblyFacts.TryRead(getInt, out M10AssemblyRecord? saved) &&
            saved.HasValue;

        public M10AssemblyHost(
            Transform parent,
            Func<bool> atJingwanAssembly,
            Func<bool> threeRecordsMatched,
            Func<bool> carriesM02BattleReport,
            Func<string, int> getInt,
            Action<string, int> setInt)
        {
            this.parent = parent ?? throw new ArgumentNullException(nameof(parent));
            this.atJingwanAssembly = atJingwanAssembly ??
                throw new ArgumentNullException(nameof(atJingwanAssembly));
            this.threeRecordsMatched = threeRecordsMatched ??
                throw new ArgumentNullException(nameof(threeRecordsMatched));
            this.carriesM02BattleReport = carriesM02BattleReport ??
                throw new ArgumentNullException(nameof(carriesM02BattleReport));
            this.getInt = getInt ?? throw new ArgumentNullException(nameof(getInt));
            this.setInt = setInt ?? throw new ArgumentNullException(nameof(setInt));
        }

        public bool TryOpen()
        {
            if (disposed || activeView != null || !atJingwanAssembly() ||
                HasAnyPersistedFacts())
                return false;

            if (!M10AssemblySession.TryCreateFromSavedFacts(getInt, out M10AssemblySession created))
                return false;

            session = created;
            openedArrivalSequence = getInt(M05LandingFacts.ArrivalSequenceKey);
            activeView = new M10AssemblyView(
                parent,
                session,
                threeRecordsMatched,
                carriesM02BattleReport,
                () => M10AssemblyFacts.EvaluateRC02LeveragePenalty(getInt),
                () => M10AssemblyFacts.EvaluateRC05SupplyNeed(getInt),
                HandleCommitRequested);
            return true;
        }

        private bool HasAnyPersistedFacts()
        {
            return getInt(M10AssemblyFacts.ConfirmedKey) != 0 ||
                getInt(M10AssemblyFacts.WorldNoticeThreeKey) != 0 ||
                getInt(M10AssemblyFacts.RosterAccuracyKey) != 0 ||
                getInt(M10AssemblyFacts.BattleReportSourceKey) != 0 ||
                getInt(M10AssemblyFacts.HanLedgerUseKey) != 0 ||
                getInt(M10AssemblyFacts.SupplyContractKey) != 0 ||
                getInt(M10AssemblyFacts.LeverageWeakenedRC02Key) != 0 ||
                getInt(M10AssemblyFacts.CoversRC05FamiliesKey) != 0 ||
                getInt(M10AssemblyFacts.BellTowerSeizureKey) != 0;
        }

        private void HandleCommitRequested()
        {
            // A match at the earlier reconciliation table is not proof that
            // the three live records still agree when the assembly is saved.
            // Keep the panel retryable rather than publishing a stale clearance.
            if (disposed || session == null || !atJingwanAssembly() ||
                !threeRecordsMatched() ||
                !openedArrivalSequence.HasValue ||
                getInt(M05LandingFacts.ArrivalSequenceKey) != openedArrivalSequence.Value)
                return;

            if (!session.TryCommit(getInt, setInt))
                return;

            activeView?.Dispose();
            activeView = null;
            openedArrivalSequence = null;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            activeView?.Dispose();
            activeView = null;
            openedArrivalSequence = null;
        }
    }
}
