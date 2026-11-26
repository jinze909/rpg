using System;
using UnityEngine;

namespace Rpg.Soundwell
{
    /// <summary>
    /// M08 night-dock interaction host. The owning scene supplies a live
    /// Shen Li party-presence check and the Makinom read/write authority.
    /// The choice is saved only after the player actually responds; it
    /// never triggers the chapter world notice or changes ORK combat data.
    /// </summary>
    public sealed class M08DockTruthHost : IDisposable
    {
        private readonly Transform parent;
        private readonly Func<bool> shenLiPresent;
        private readonly Func<string, int> getInt;
        private readonly Action<string, int> setInt;
        // This conversation belongs to one concrete M05 arrival, not just
        // an identical-looking M08 workshop outcome on a later voyage.
        private int? openedArrivalSequence;
        private M08DockTruthView activeView;
        private bool disposed;

        public M08DockTruthView ActiveView =>
            activeView != null && !activeView.IsDisposed ? activeView : null;
        public bool IsDisposed => disposed;
        public bool HasRecordedResponse =>
            M08DockTruthFacts.TryRead(getInt, out M08DockTruthResponse? response) &&
            response.HasValue;

        public M08DockTruthHost(
            Transform parent, Func<bool> shenLiPresent,
            Func<string, int> getInt, Action<string, int> setInt)
        {
            this.parent = parent ?? throw new ArgumentNullException(nameof(parent));
            this.shenLiPresent = shenLiPresent ??
                throw new ArgumentNullException(nameof(shenLiPresent));
            this.getInt = getInt ?? throw new ArgumentNullException(nameof(getInt));
            this.setInt = setInt ?? throw new ArgumentNullException(nameof(setInt));
        }

        public bool TryOpen()
        {
            if (activeView != null && activeView.IsDisposed)
            {
                activeView.Dispose();
                activeView = null;
                openedArrivalSequence = null;
            }

            if (disposed || parent == null || activeView != null ||
                !shenLiPresent() || disposed || parent == null || activeView != null ||
                // Refuse a damaged or torn record until the voyage is
                // explicitly reset, rather than allowing contradictory edits.
                getInt(M08DockTruthFacts.ConfirmedKey) != 0 ||
                getInt(M08DockTruthFacts.ShenRelationResponseKey) != 0)
                return false;

            if (!M08WorkshopDepositFacts.TryRead(getInt, out bool completed) ||
                !completed)
                return false;

            int arrivalSequence = getInt(M05LandingFacts.ArrivalSequenceKey);
            if (arrivalSequence < 0)
                return false;
            if (disposed || parent == null || activeView != null)
                return false;
            openedArrivalSequence = arrivalSequence;
            var session = new M08DockTruthSession(
                shenLiPresent: true, firstWinchConfirmed: true);
            activeView = new M08DockTruthView(parent, session, SaveResponse);
            return true;
        }

        private bool SaveResponse(M08DockTruthResponse response)
        {
            // Presence and prior commission are checked again at commit time.
            // A party split mid-conversation never grants dialogue credit.
            var submittingView = activeView;
            if (!IsCurrentView(submittingView) ||
                !shenLiPresent() || !IsCurrentView(submittingView) ||
                !M08WorkshopDepositFacts.TryRead(getInt, out bool completed) ||
                !completed || !IsCurrentView(submittingView))
                return false;

            if (!M08DockTruthFacts.TryRead(getInt,
                out M08DockTruthResponse? existing))
                return false;

            if (existing.HasValue)
            {
                if (existing.Value != response)
                    return false;
            }
            else if (!IsCurrentView(submittingView) || !M08DockTruthFacts.TryRecord(
                response, shenLiPresent: true, getInt, setInt))
                return false;

            // Handle a save that succeeded before its caller lost the
            // acknowledgement. Do not reissue the write on retry.
            if (!M08DockTruthFacts.TryRead(getInt,
                    out M08DockTruthResponse? persisted) ||
                persisted != response)
                return false;

            if (activeView == submittingView)
            {
                submittingView.Dispose();
                activeView = null;
                openedArrivalSequence = null;
            }
            return true;
        }

        private bool IsCurrentView(M08DockTruthView expected)
        {
            int? arrival = openedArrivalSequence;
            if (!IsOpenView(expected) || !arrival.HasValue)
                return false;

            // Reading live voyage evidence can retire this host or its view.
            // Compare against the snapshot, then recheck the same live view.
            int currentArrival = getInt(M05LandingFacts.ArrivalSequenceKey);
            return IsOpenView(expected) && openedArrivalSequence == arrival &&
                currentArrival == arrival.Value;
        }

        private bool IsOpenView(M08DockTruthView expected)
        {
            return !disposed && parent != null && expected != null &&
                activeView == expected && !expected.IsDisposed;
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
