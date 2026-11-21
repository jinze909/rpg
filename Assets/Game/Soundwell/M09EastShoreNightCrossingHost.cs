using System;
using UnityEngine;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Scene-callable M09 east-shore night crossing host. Enforces live
    /// night-ferry readiness, valid berth/household counts, and an unsettled
    /// M09 berth-table state before opening M09EastShoreNightCrossingView,
    /// then hands off the verified Ruiya outcome to the owning scene's
    /// authoritative commit callback without creating a parallel save store.
    /// </summary>
    public sealed class M09EastShoreNightCrossingHost : IDisposable
    {
        private readonly Transform parent;
        private readonly Func<bool> nightFerryReady;
        private readonly Func<int> availableBerths;
        private readonly Func<int> observedHouseholdSize;
        private readonly Func<int, bool> confirmActuallyBoarded;
        private readonly Func<M09EastShoreNightCrossingSession, bool> commitVerifiedOutcome;
        private readonly Func<string, int> getInt;
        // An otherwise identical replayed voyage is a different M05 arrival;
        // stale UI must not invoke actual-boarding or save callbacks.
        private int? openedArrivalSequence;
        private M09EastShoreNightCrossingSession session;
        private M09EastShoreNightCrossingView activeView;
        private M09RuiyaOutcome? committedOutcome;
        private int committedBoardedCount;
        private bool disposed;

        public M09EastShoreNightCrossingSession Session => session;
        public M09EastShoreNightCrossingView ActiveView => activeView;
        public M09RuiyaOutcome? CommittedOutcome => committedOutcome;
        public int CommittedBoardedCount => committedBoardedCount;
        public bool IsCommitted => committedOutcome.HasValue;
        public bool IsDisposed => disposed;

        public M09EastShoreNightCrossingHost(
            Transform parent,
            Func<bool> nightFerryReady,
            Func<int> availableBerths,
            Func<int> observedHouseholdSize,
            Func<int, bool> confirmActuallyBoarded,
            Func<M09EastShoreNightCrossingSession, bool> commitVerifiedOutcome,
            Func<string, int> getInt)
        {
            this.parent = parent ?? throw new ArgumentNullException(nameof(parent));
            this.nightFerryReady = nightFerryReady ??
                throw new ArgumentNullException(nameof(nightFerryReady));
            this.availableBerths = availableBerths ??
                throw new ArgumentNullException(nameof(availableBerths));
            this.observedHouseholdSize = observedHouseholdSize ??
                throw new ArgumentNullException(nameof(observedHouseholdSize));
            this.confirmActuallyBoarded = confirmActuallyBoarded ??
                throw new ArgumentNullException(nameof(confirmActuallyBoarded));
            this.commitVerifiedOutcome = commitVerifiedOutcome ??
                throw new ArgumentNullException(nameof(commitVerifiedOutcome));
            this.getInt = getInt ?? throw new ArgumentNullException(nameof(getInt));
        }

        public bool TryOpen()
        {
            if (disposed || activeView != null || IsCommitted || !nightFerryReady() ||
                getInt(M09BerthTableFacts.ConfirmedKey) != 0)
                return false;

            int berths = availableBerths();
            int household = observedHouseholdSize();
            int arrivalSequence = getInt(M05LandingFacts.ArrivalSequenceKey);
            if (berths < 0 || household <= 0 || arrivalSequence < 0)
                return false;

            openedArrivalSequence = arrivalSequence;
            session = new M09EastShoreNightCrossingSession(berths);
            activeView = new M09EastShoreNightCrossingView(
                parent,
                session,
                household,
                count => IsCurrentVoyage() && nightFerryReady() &&
                    confirmActuallyBoarded(count),
                HandleCompleted);
            return true;
        }

        private void HandleCompleted(M09EastShoreNightCrossingSession completed)
        {
            if (disposed || completed != session || !completed.IsComplete ||
                !completed.Outcome.HasValue || !nightFerryReady() ||
                !IsCurrentVoyage())
                throw new InvalidOperationException(
                    "Night crossing outcome cannot be committed while scene is not ready.");

            if (!commitVerifiedOutcome(completed))
                throw new InvalidOperationException(
                    "Authoritative M09 night crossing commit was declined or incomplete.");

            committedOutcome = completed.Outcome;
            committedBoardedCount = completed.ActuallyBoardedCount;
            activeView?.Dispose();
            activeView = null;
        }

        private bool IsCurrentVoyage()
        {
            return !disposed && openedArrivalSequence.HasValue &&
                getInt(M05LandingFacts.ArrivalSequenceKey) == openedArrivalSequence.Value;
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
