using System;
using UnityEngine;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Scene-owned M08 workshop interaction entry point. The scene host supplies
    /// the Makinom reader/writer and calls TryOpen only at the actual workshop
    /// interaction. This class does not invent a street, a workshop deposit
    /// amount, ORK items, or the later world notice.
    /// </summary>
    public sealed class M08WorkshopOrderHost : IDisposable
    {
        private readonly Transform parent;
        private readonly Func<string, int> getInt;
        private readonly Action<string, int> setInt;
        private M08FirstWinchOrderView activeView;
        // Capture M05's monotonic voyage identity when this live workshop
        // interaction begins; completing the winch cannot reuse an old scene
        // after a new M03 sailing with otherwise identical facts.
        private int? openedArrivalSequence;
        private bool disposed;

        public M08FirstWinchOrderView ActiveView =>
            activeView != null && !activeView.IsDisposed ? activeView : null;
        public bool IsDisposed => disposed;
        public bool IsCompleted =>
            M08WorkshopDepositFacts.TryRead(getInt, out bool completed) && completed;

        public M08WorkshopOrderHost(
            Transform parent,
            Func<string, int> getInt,
            Action<string, int> setInt)
        {
            this.parent = parent ?? throw new ArgumentNullException(nameof(parent));
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

            if (disposed || parent == null || activeView != null || IsCompleted ||
                getInt(M08WorkshopDepositFacts.CompletedKey) != 0 ||
                M08WorkshopDepositFacts.HasOrphanedDownstreamFacts(getInt))
                return false;

            // No M08 commission before the actual M07 extraction is settled.
            if (!M07ExtractionFacts.TryRead(getInt, out M07ExtractionRecord? extraction) ||
                !extraction.HasValue)
                return false;

            int arrivalSequence = getInt(M05LandingFacts.ArrivalSequenceKey);
            if (arrivalSequence < 0)
                return false;

            var session = new M08FirstWinchOrderSession();
            openedArrivalSequence = arrivalSequence;
            activeView = new M08FirstWinchOrderView(parent, session, completedSession =>
            {
                if (disposed || activeView == null || activeView.IsDisposed ||
                    !openedArrivalSequence.HasValue ||
                    getInt(M05LandingFacts.ArrivalSequenceKey) != openedArrivalSequence.Value ||
                    !M07ExtractionFacts.TryRead(getInt, out M07ExtractionRecord? liveExtraction) ||
                    !liveExtraction.HasValue)
                    throw new InvalidOperationException(
                        "The M08 workshop belongs to a previous or invalid voyage.");

                // A writer may commit the flag and then throw: a second user
                // confirmation must acknowledge the persisted result rather
                // than paying twice or trapping a finished commission.
                if (!IsCompleted &&
                    !M08WorkshopDepositFacts.TryConfirm(completedSession, getInt, setInt))
                    throw new InvalidOperationException("Unable to persist the M08 workshop deposit.");

                if (!IsCompleted)
                    throw new InvalidOperationException("M08 deposit write was not retained.");

                activeView?.Dispose();
                activeView = null;
                openedArrivalSequence = null;
            });
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
