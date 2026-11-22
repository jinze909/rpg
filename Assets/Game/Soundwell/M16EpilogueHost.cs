using System;
using UnityEngine;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Scene-callable M16 ("一个普通的下午") playable epilogue host. Enforces
    /// verified M15FinalRollCallFacts, live Linchao afternoon presence, live
    /// Shen Li willingness and closing-tone revalidation at commit time,
    /// monotonic M05 arrival identity, and clean M16EpilogueFacts state
    /// (blocking torn/partial saves) before opening M16EpilogueView.
    /// </summary>
    public sealed class M16EpilogueHost : IDisposable
    {
        private readonly Transform parent;
        private readonly Func<bool> atLinchaoAfternoon;
        private readonly Func<bool> shenLiWillingToMeet;
        private readonly Func<M16RespectAgencyTone> resolveClosingTone;
        private readonly Func<string, int> getInt;
        private readonly Action<string, int> setInt;
        private M16EpilogueSession session;
        private M16EpilogueView activeView;
        private int? openedArrivalSequence;
        private bool openedShenLiWillingToMeet;
        private M16RespectAgencyTone openedClosingTone;
        private bool disposed;

        public M16EpilogueSession Session => session;
        public M16EpilogueView ActiveView =>
            activeView != null && !activeView.IsDisposed ? activeView : null;
        public bool IsDisposed => disposed;
        public bool IsCompleted =>
            M16EpilogueFacts.TryRead(getInt, out M16EpilogueRecord? saved) &&
            saved.HasValue;

        public M16EpilogueHost(
            Transform parent,
            Func<bool> atLinchaoAfternoon,
            Func<bool> shenLiWillingToMeet,
            Func<M16RespectAgencyTone> resolveClosingTone,
            Func<string, int> getInt,
            Action<string, int> setInt)
        {
            this.parent = parent ?? throw new ArgumentNullException(nameof(parent));
            this.atLinchaoAfternoon = atLinchaoAfternoon ??
                throw new ArgumentNullException(nameof(atLinchaoAfternoon));
            this.shenLiWillingToMeet = shenLiWillingToMeet ??
                throw new ArgumentNullException(nameof(shenLiWillingToMeet));
            this.resolveClosingTone = resolveClosingTone ??
                throw new ArgumentNullException(nameof(resolveClosingTone));
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

            if (disposed || parent == null || activeView != null || !atLinchaoAfternoon() ||
                HasAnyPersistedFacts())
                return false;

            bool shenWilling = shenLiWillingToMeet();
            M16RespectAgencyTone closingTone = resolveClosingTone();
            if (!M16EpilogueSession.TryCreateFromSavedFacts(
                    getInt, shenWilling, closingTone,
                    out M16EpilogueSession created))
                return false;

            session = created;
            openedArrivalSequence = getInt(M05LandingFacts.ArrivalSequenceKey);
            openedShenLiWillingToMeet = shenWilling;
            openedClosingTone = closingTone;
            activeView = new M16EpilogueView(
                parent,
                session,
                HandleStandingSpotSubmitted);
            return true;
        }

        private bool HasAnyPersistedFacts()
        {
            return getInt(M16EpilogueFacts.ConfirmedKey) != 0 ||
                getInt(M16EpilogueFacts.StopsVisitedKey) != 0 ||
                getInt(M16EpilogueFacts.LettersReadKey) != 0 ||
                getInt(M16EpilogueFacts.DeskRegisterKey) != 0 ||
                getInt(M16EpilogueFacts.StandingSpotKey) != 0 ||
                getInt(M16EpilogueFacts.ShenWillingKey) != 0 ||
                getInt(M16EpilogueFacts.ClosingToneKey) != 0;
        }

        private void HandleStandingSpotSubmitted(M16StandingSpot spot)
        {
            if (disposed || session == null || activeView == null || activeView.IsDisposed ||
                !atLinchaoAfternoon() ||
                !openedArrivalSequence.HasValue ||
                getInt(M05LandingFacts.ArrivalSequenceKey) != openedArrivalSequence.Value ||
                shenLiWillingToMeet() != openedShenLiWillingToMeet ||
                resolveClosingTone() != openedClosingTone)
                return;

            if (!session.TryChooseStandingSpotAndCommit(spot, getInt, setInt))
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
