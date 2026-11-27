using System;
using UnityEngine;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Scene-callable M13 approach route and A Yuan risk-consent host.
    /// Enforces live fleet-briefing readiness, live safety-prep revalidation
    /// at commit time, monotonic M05 arrival identity, and clean
    /// M13DepartureRiskFacts state (blocking torn/partial saves and orphaned
    /// downstream Act-3 facts) before opening M13DepartureRiskView.
    /// </summary>
    public sealed class M13DepartureRiskHost : IDisposable
    {
        private readonly Transform parent;
        private readonly Func<bool> fleetBriefingReady;
        private readonly Func<bool> initialSafetyPrepComplete;
        private readonly Func<bool> canDepartNow;
        private readonly Func<string, int> getInt;
        private readonly Action<string, int> setInt;
        private M13DepartureRiskSession session;
        private M13DepartureRiskView activeView;
        private int? openedArrivalSequence;
        private bool openedSafetyPrepComplete;
        private bool disposed;
        private bool opening;

        public M13DepartureRiskSession Session => session;
        public M13DepartureRiskView ActiveView =>
            activeView != null && !activeView.IsDisposed ? activeView : null;
        public bool IsDisposed => disposed;
        public bool IsDeparted =>
            M13DepartureRiskFacts.TryReadDeparture(getInt, out M13DepartureRecord? saved) &&
            saved.HasValue;

        public M13DepartureRiskHost(
            Transform parent,
            Func<bool> fleetBriefingReady,
            Func<bool> initialSafetyPrepComplete,
            Func<bool> canDepartNow,
            Func<string, int> getInt,
            Action<string, int> setInt)
        {
            this.parent = parent ?? throw new ArgumentNullException(nameof(parent));
            this.fleetBriefingReady = fleetBriefingReady ??
                throw new ArgumentNullException(nameof(fleetBriefingReady));
            this.initialSafetyPrepComplete = initialSafetyPrepComplete ??
                throw new ArgumentNullException(nameof(initialSafetyPrepComplete));
            this.canDepartNow = canDepartNow ??
                throw new ArgumentNullException(nameof(canDepartNow));
            this.getInt = getInt ?? throw new ArgumentNullException(nameof(getInt));
            this.setInt = setInt ?? throw new ArgumentNullException(nameof(setInt));
        }

        public bool TryOpen()
        {
            if (disposed || parent == null || opening)
                return false;
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

            if (disposed || parent == null || activeView != null)
                return false;

            int arrivalSequence = getInt(M05LandingFacts.ArrivalSequenceKey);
            if (arrivalSequence < 0 || !fleetBriefingReady() ||
                HasAnyPersistedFacts() ||
                M13DepartureRiskFacts.HasOrphanedDownstreamFacts(getInt))
                return false;

            bool safetyPrep = initialSafetyPrepComplete();
            if (disposed || parent == null ||
                getInt(M05LandingFacts.ArrivalSequenceKey) != arrivalSequence ||
                disposed || parent == null)
                return false;

            session = new M13DepartureRiskSession(safetyPrep);
            openedArrivalSequence = arrivalSequence;
            openedSafetyPrepComplete = safetyPrep;
            activeView = new M13DepartureRiskView(
                parent, session, HandleDepartureRequested);
            return true;
        }

        private bool HasAnyPersistedFacts()
        {
            return getInt(M13DepartureRiskFacts.DepartureConfirmedKey) != 0 ||
                getInt(M13DepartureRiskFacts.RouteKey) != 0 ||
                getInt(M13DepartureRiskFacts.SafetyPrepKey) != 0 ||
                getInt(M13DepartureRiskFacts.QiWarningKey) != 0 ||
                getInt(M13DepartureRiskFacts.RiskConsentKey) != 0 ||
                getInt(M13DepartureRiskFacts.ThreeBellSignalKey) != 0 ||
                getInt(M13DepartureRiskFacts.AyuanOutcomeKey) != 0;
        }

        public bool TryDeliverQiQueWarning()
        {
            if (activeView != null && activeView.IsDisposed)
            {
                activeView.Dispose();
                activeView = null;
                openedArrivalSequence = null;
            }

            var warningView = activeView;
            var warningSession = session;
            int? arrivalSequence = openedArrivalSequence;
            if (!IsCurrentOwner(warningView, warningSession) ||
                warningSession.IsDeparted || warningSession.QiQueWarningGiven ||
                !arrivalSequence.HasValue ||
                getInt(M05LandingFacts.ArrivalSequenceKey) != arrivalSequence.Value ||
                !IsCurrentOwner(warningView, warningSession) ||
                warningSession.IsDeparted || warningSession.QiQueWarningGiven)
                return false;

            warningSession.DeliverQiQueWarning();
            warningView.Refresh();
            return true;
        }

        private void HandleDepartureRequested(M13ApproachRoute route)
        {
            var departingView = activeView;
            var departingSession = session;
            int? arrivalSequence = openedArrivalSequence;
            bool safetyPrep = openedSafetyPrepComplete;
            if (!IsCurrentOwner(departingView, departingSession) ||
                !arrivalSequence.HasValue || !fleetBriefingReady() || !canDepartNow() ||
                initialSafetyPrepComplete() != safetyPrep ||
                getInt(M05LandingFacts.ArrivalSequenceKey) != arrivalSequence.Value ||
                !IsCurrentOwner(departingView, departingSession))
                return;

            if (!M13DepartureRiskFacts.TryReadDeparture(
                    getInt, out M13DepartureRecord? existing) ||
                !IsCurrentOwner(departingView, departingSession))
                return;

            // Handle a write that succeeded before a lost acknowledgement
            // without issuing duplicate writes.
            if (existing.HasValue)
            {
                if (existing.Value.Route != route)
                    return;
            }
            else if (!departingSession.TryConfirmDeparture(route, getInt, setInt))
            {
                return;
            }

            if (!M13DepartureRiskFacts.TryReadDeparture(
                    getInt, out M13DepartureRecord? persisted) ||
                !persisted.HasValue ||
                persisted.Value.Route != route)
                return;

            // A successful write may retire this panel synchronously.
            // Never close a replacement or dereference a retired owner.
            departingView.Dispose();
            if (ReferenceEquals(activeView, departingView))
            {
                activeView = null;
                openedArrivalSequence = null;
            }
        }

        private bool IsCurrentOwner(M13DepartureRiskView expectedView,
            M13DepartureRiskSession expectedSession)
        {
            return !disposed && parent != null && expectedView != null &&
                !expectedView.IsDisposed && ReferenceEquals(activeView, expectedView) &&
                expectedSession != null && ReferenceEquals(session, expectedSession);
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
