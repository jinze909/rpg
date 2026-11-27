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
        private readonly Func<bool> atLinchaoFerry;
        private readonly Func<bool> threeRecordsMatched;
        private readonly Func<bool> carriesM02BattleReport;
        private readonly Func<string, int> getInt;
        private readonly Action<string, int> setInt;
        private M10AssemblySession session;
        private M10AssemblyView activeView;
        private M10WorldNoticeThreeView activeWorldNoticeView;
        private int? openedArrivalSequence;
        private bool disposed;
        private bool opening;

        public M10AssemblySession Session => session;
        public M10AssemblyView ActiveView =>
            (activeView != null && !activeView.IsDisposed) ? activeView : null;
        public M10WorldNoticeThreeView ActiveWorldNoticeView
        {
            get
            {
                if (activeWorldNoticeView != null && activeWorldNoticeView.IsDisposed)
                {
                    activeWorldNoticeView.Dispose();
                    activeWorldNoticeView = null;
                }
                return activeWorldNoticeView;
            }
        }
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
            Action<string, int> setInt,
            Func<bool> atLinchaoFerry = null)
        {
            this.parent = parent ?? throw new ArgumentNullException(nameof(parent));
            this.atJingwanAssembly = atJingwanAssembly ??
                throw new ArgumentNullException(nameof(atJingwanAssembly));
            this.atLinchaoFerry = atLinchaoFerry ?? (() => false);
            this.threeRecordsMatched = threeRecordsMatched ??
                throw new ArgumentNullException(nameof(threeRecordsMatched));
            this.carriesM02BattleReport = carriesM02BattleReport ??
                throw new ArgumentNullException(nameof(carriesM02BattleReport));
            this.getInt = getInt ?? throw new ArgumentNullException(nameof(getInt));
            this.setInt = setInt ?? throw new ArgumentNullException(nameof(setInt));
        }

        public bool TryOpen()
        {
            if (disposed || parent == null || opening)
                return false;

            // The ferry notice and assembly share one opening slot, even
            // when scene or fact readers synchronously dispatch another open.
            opening = true;
            try
            {
                return TryOpenCore();
            }
            finally
            {
                opening = false;
            }
        }

        private bool TryOpenCore()
        {
            if (activeView != null && activeView.IsDisposed)
            {
                activeView.Dispose();
                activeView = null;
                openedArrivalSequence = null;
            }
            if (activeWorldNoticeView != null && activeWorldNoticeView.IsDisposed)
            {
                activeWorldNoticeView.Dispose();
                activeWorldNoticeView = null;
                openedArrivalSequence = null;
            }
            if (disposed || parent == null || activeView != null ||
                activeWorldNoticeView != null)
                return false;

            int arrivalSequence = getInt(M05LandingFacts.ArrivalSequenceKey);
            if (arrivalSequence < 0 || HasAnyPersistedFacts())
                return false;

            if (atLinchaoFerry())
            {
                if (M10AssemblyFacts.HasOrphanedDownstreamFacts(getInt) ||
                    !M05LandingFacts.TryRead(getInt, out M05LandingReport? landing) ||
                    !landing.HasValue ||
                    !M09BerthTableFacts.TryRead(getInt, out M09BerthTableRecord? berth) ||
                    !berth.HasValue ||
                    !M10WorldNoticeThreeFacts.TryReadForCurrentArrival(getInt, out bool noticeObserved) ||
                    noticeObserved)
                    return false;

                if (!CanPublishOpening(arrivalSequence))
                    return false;

                openedArrivalSequence = arrivalSequence;
                activeWorldNoticeView = new M10WorldNoticeThreeView(
                    parent, HandleNoticeAcknowledge);
                return true;
            }

            if (!atJingwanAssembly() ||
                !M10AssemblySession.TryCreateFromSavedFacts(getInt, out M10AssemblySession created) ||
                !CanPublishOpening(arrivalSequence))
                return false;

            session = created;
            openedArrivalSequence = arrivalSequence;
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

        private bool CanPublishOpening(int arrivalSequence)
        {
            return !disposed && parent != null &&
                getInt(M05LandingFacts.ArrivalSequenceKey) == arrivalSequence &&
                !disposed && parent != null;
        }

        private bool HandleNoticeAcknowledge()
        {
            if (disposed || activeWorldNoticeView == null ||
                activeWorldNoticeView.IsDisposed || !atLinchaoFerry() ||
                !openedArrivalSequence.HasValue ||
                getInt(M05LandingFacts.ArrivalSequenceKey) != openedArrivalSequence.Value)
                return false;

            return M10WorldNoticeThreeFacts.TryRecord(getInt, setInt);
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
            if (disposed || session == null || activeView == null || activeView.IsDisposed ||
                !atJingwanAssembly() ||
                !threeRecordsMatched() ||
                !openedArrivalSequence.HasValue ||
                getInt(M05LandingFacts.ArrivalSequenceKey) != openedArrivalSequence.Value)
                return;

            if (!session.TryCommit(getInt, setInt))
                return;

            activeView.Dispose();
            activeView = null;
            openedArrivalSequence = null;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            activeView?.Dispose();
            activeView = null;
            activeWorldNoticeView?.Dispose();
            activeWorldNoticeView = null;
            openedArrivalSequence = null;
        }
    }
}
