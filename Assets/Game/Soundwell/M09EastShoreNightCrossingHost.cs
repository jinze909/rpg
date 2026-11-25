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
        private int? openedAvailableBerths;
        private int? openedHouseholdSize;
        private M09EastShoreNightCrossingSession session;
        private M09EastShoreNightCrossingView activeView;
        private M09RuiyaOutcome? committedOutcome;
        private int committedBoardedCount;
        private bool verifyingBoarding;
        private bool disposed;

        public M09EastShoreNightCrossingSession Session => session;
        public M09EastShoreNightCrossingView ActiveView =>
            activeView != null && !activeView.IsDisposed ? activeView : null;
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
            if (activeView != null && activeView.IsDisposed)
            {
                activeView.Dispose();
                activeView = null;
                openedArrivalSequence = null;
                openedAvailableBerths = null;
                openedHouseholdSize = null;
            }

            if (disposed || parent == null || activeView != null || IsCommitted || !nightFerryReady() ||
                !HasUnsettledCleanM09State())
                return false;

            int berths = availableBerths();
            int household = observedHouseholdSize();
            int arrivalSequence = getInt(M05LandingFacts.ArrivalSequenceKey);
            if (berths < 0 || household <= 0 || arrivalSequence < 0)
                return false;

            openedArrivalSequence = arrivalSequence;
            openedAvailableBerths = berths;
            openedHouseholdSize = household;
            session = new M09EastShoreNightCrossingSession(berths);
            activeView = new M09EastShoreNightCrossingView(
                parent,
                session,
                household,
                TryVerifyActualBoarding,
                HandleCompleted);
            return true;
        }

        private bool TryVerifyActualBoarding(int count)
        {
            // Boarding verification can consume real manifest capacity.
            // A callback that reenters the UI must not verify it twice.
            if (verifyingBoarding || disposed || activeView == null || activeView.IsDisposed)
                return false;

            verifyingBoarding = true;
            try
            {
                // A stale voyage, competing settlement or torn payload
                // invalidates the panel before real boarding begins.
                return IsCurrentVoyage() && nightFerryReady() &&
                    HasUnsettledCleanM09State() && LiveTransferCountsMatch() &&
                    confirmActuallyBoarded(count);
            }
            finally
            {
                verifyingBoarding = false;
            }
        }

        private void HandleCompleted(M09EastShoreNightCrossingSession completed)
        {
            if (disposed || activeView == null || activeView.IsDisposed ||
                completed != session || !completed.IsComplete ||
                !completed.Outcome.HasValue || !nightFerryReady() ||
                // A competing, torn, or corrupted M09 settlement—or orphaned
                // Act-3 downstream facts—must not be overwritten by this panel.
                !HasUnsettledCleanM09State() ||
                !IsCurrentVoyage() || !LiveTransferCountsMatch(
                    allowVerifiedBoardingConsumption: true))
                throw new InvalidOperationException(
                    "Night crossing outcome cannot be committed while scene is not ready.");

            if (!commitVerifiedOutcome(completed))
                throw new InvalidOperationException(
                    "Authoritative M09 night crossing commit was declined or incomplete.");

            committedOutcome = completed.Outcome;
            committedBoardedCount = completed.ActuallyBoardedCount;
            activeView?.Dispose();
            activeView = null;
            openedArrivalSequence = null;
            openedAvailableBerths = null;
            openedHouseholdSize = null;
        }

        private bool HasUnsettledCleanM09State()
        {
            return !M09BerthTableFacts.HasAnyPersistedFacts(getInt) &&
                !M09BerthTableFacts.HasOrphanedDownstreamFacts(getInt);
        }

        // Before verifying boarding, the live berth and household counts
        // must still equal those observed when the dialogue opened. After
        // confirmed boarding, some ferry controllers immediately deduct
        // precisely those occupied berths. That change is expected evidence
        // consumption, not another party stealing capacity. Refusals and
        // deferrals never occupy any berths, so they require an exact match.
        private bool LiveTransferCountsMatch(
            bool allowVerifiedBoardingConsumption = false)
        {
            if (!openedAvailableBerths.HasValue || !openedHouseholdSize.HasValue ||
                observedHouseholdSize() != openedHouseholdSize.Value)
                return false;

            int liveBerths = availableBerths();
            if (liveBerths == openedAvailableBerths.Value)
                return true;
            return allowVerifiedBoardingConsumption &&
                session != null && session.Outcome == M09RuiyaOutcome.Rescued &&
                session.ActuallyBoardedCount > 0 &&
                liveBerths == openedAvailableBerths.Value - session.ActuallyBoardedCount;
        }

        private bool IsCurrentVoyage()
        {
            if (disposed || !openedArrivalSequence.HasValue ||
                getInt(M05LandingFacts.ArrivalSequenceKey) != openedArrivalSequence.Value)
                return false;

            // Legacy saves and scene-only tests may not carry a monotonic
            // arrival counter yet. Once an actual M05 arrival has minted one,
            // a reset clears its completion marker BEFORE the next arrival
            // increments the sequence; do not verify boarding in that gap.
            if (openedArrivalSequence.Value == 0)
                return true;
            return M05LandingFacts.TryRead(getInt, out M05LandingReport? landing) &&
                landing.HasValue;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            activeView?.Dispose();
            activeView = null;
            openedArrivalSequence = null;
            openedAvailableBerths = null;
            openedHouseholdSize = null;
        }
    }
}
