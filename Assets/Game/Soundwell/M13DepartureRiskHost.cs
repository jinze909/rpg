using System;
using UnityEngine;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Scene-callable M13 approach route and A Yuan risk-consent host.
    /// Enforces live fleet-briefing readiness and clean DepartureConfirmedKey
    /// before opening M13DepartureRiskView, supports scene-driven Qi Que
    /// warning delivery, and persists departure to Makinom with
    /// interrupted-write retry and idempotent commit-then-throw recovery.
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
        private bool disposed;

        public M13DepartureRiskSession Session => session;
        public M13DepartureRiskView ActiveView => activeView;
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
            if (disposed || activeView != null || !fleetBriefingReady() ||
                getInt(M13DepartureRiskFacts.DepartureConfirmedKey) != 0)
                return false;

            session = new M13DepartureRiskSession(initialSafetyPrepComplete());
            activeView = new M13DepartureRiskView(
                parent, session, HandleDepartureRequested);
            return true;
        }

        public bool TryDeliverQiQueWarning()
        {
            if (disposed || session == null || activeView == null ||
                session.IsDeparted || session.QiQueWarningGiven)
                return false;

            session.DeliverQiQueWarning();
            activeView.Refresh();
            return true;
        }

        private void HandleDepartureRequested(M13ApproachRoute route)
        {
            if (disposed || session == null || !fleetBriefingReady() || !canDepartNow())
                return;

            if (!M13DepartureRiskFacts.TryReadDeparture(
                    getInt, out M13DepartureRecord? existing))
                return;

            // Handle a write that succeeded before a lost acknowledgement
            // without issuing duplicate writes.
            if (existing.HasValue)
            {
                if (existing.Value.Route != route)
                    return;
            }
            else if (!session.TryConfirmDeparture(route, getInt, setInt))
            {
                return;
            }

            if (!M13DepartureRiskFacts.TryReadDeparture(
                    getInt, out M13DepartureRecord? persisted) ||
                !persisted.HasValue ||
                persisted.Value.Route != route)
                return;

            activeView?.Dispose();
            activeView = null;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            activeView?.Dispose();
            activeView = null;
        }
    }
}
