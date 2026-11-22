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

        public M08DockTruthView ActiveView
        {
            get
            {
                PruneDisposedView();
                return activeView;
            }
        }
        public bool IsDisposed => disposed;

        private void PruneDisposedView()
        {
            if (activeView != null && activeView.IsDisposed)
            {
                activeView = null;
                openedArrivalSequence = null;
            }
        }
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
            PruneDisposedView();
            if (disposed || parent == null || activeView != null ||
                !shenLiPresent() ||
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
            PruneDisposedView();
            if (disposed || parent == null || activeView == null ||
                !openedArrivalSequence.HasValue ||
                getInt(M05LandingFacts.ArrivalSequenceKey) != openedArrivalSequence.Value ||
                !shenLiPresent() ||
                !M08WorkshopDepositFacts.TryRead(getInt, out bool completed) ||
                !completed)
                return false;

            if (!M08DockTruthFacts.TryRead(getInt,
                out M08DockTruthResponse? existing))
                return false;

            if (existing.HasValue)
            {
                if (existing.Value != response)
                    return false;
            }
            else if (!M08DockTruthFacts.TryRecord(
                response, shenLiPresent: true, getInt, setInt))
                return false;

            // Handle a save that succeeded before its caller lost the
            // acknowledgement. Do not reissue the write on retry.
            if (!M08DockTruthFacts.TryRead(getInt,
                    out M08DockTruthResponse? persisted) ||
                persisted != response)
                return false;

            activeView?.Dispose();
            activeView = null;
            openedArrivalSequence = null;
            return true;
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
