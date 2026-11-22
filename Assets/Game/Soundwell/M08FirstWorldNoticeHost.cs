using System;
using UnityEngine;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Scene-callable M08 end-of-chapter notice host. The scene supplies
    /// actual dawn observation, not a fabricated timer. Shen Li's optional
    /// night-dock response never becomes a compulsory chapter gate.
    /// </summary>
    public sealed class M08FirstWorldNoticeHost : IDisposable
    {
        private readonly Transform parent;
        private readonly Func<bool> realDaybreakObserved;
        private readonly Func<string, int> getInt;
        private readonly Action<string, int> setInt;
        // Old dawn notices cannot unlock RC-01..RC-05 for a replayed voyage
        // whose M05 arrival happened to have identical facts.
        private int? openedArrivalSequence;
        private M08FirstWorldNoticeView activeView;
        private bool disposed;

        public M08FirstWorldNoticeView ActiveView =>
            activeView != null && !activeView.IsDisposed ? activeView : null;
        public bool IsDisposed => disposed;
        public bool NoticeRead =>
            M08FirstWorldNoticeFacts.TryRead(getInt, out bool read) && read;

        public M08FirstWorldNoticeHost(Transform parent,
            Func<bool> realDaybreakObserved,
            Func<string, int> getInt,
            Action<string, int> setInt)
        {
            this.parent = parent ?? throw new ArgumentNullException(nameof(parent));
            this.realDaybreakObserved = realDaybreakObserved ??
                throw new ArgumentNullException(nameof(realDaybreakObserved));
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

            if (disposed || parent == null || activeView != null || !realDaybreakObserved() ||
                getInt(M08FirstWorldNoticeFacts.ConfirmedKey) != 0 ||
                M08FirstWorldNoticeFacts.HasOrphanedRosterCaseFacts(getInt))
                return false;
            if (!M08WorkshopDepositFacts.TryRead(getInt, out bool paid) ||
                !paid)
                return false;

            int arrivalSequence = getInt(M05LandingFacts.ArrivalSequenceKey);
            if (arrivalSequence < 0)
                return false;
            openedArrivalSequence = arrivalSequence;
            activeView = new M08FirstWorldNoticeView(parent, ConfirmNotice);
            return true;
        }

        public bool IsCaseAvailable(int caseNumber)
        {
            return !disposed &&
                M08FirstWorldNoticeFacts.TryCaseAvailable(
                    caseNumber, getInt, out bool available) &&
                available;
        }

        private bool ConfirmNotice()
        {
            if (disposed || activeView == null || activeView.IsDisposed ||
                !openedArrivalSequence.HasValue ||
                getInt(M05LandingFacts.ArrivalSequenceKey) != openedArrivalSequence.Value ||
                !realDaybreakObserved())
                return false;

            if (!M08FirstWorldNoticeFacts.TryRead(getInt, out bool existing))
                return false;

            // A successful commit with a lost acknowledgement is complete.
            // Never write or publish the notice twice.
            if (!existing &&
                !M08FirstWorldNoticeFacts.TryRecord(
                    true, getInt, setInt))
                return false;
            if (!M08FirstWorldNoticeFacts.TryRead(getInt, out bool saved) ||
                !saved)
                return false;

            // Mirror the established M08 workshop host: the view is owned
            // by this host and is closed only after verified persistence.
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
