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

            if (!CanOpenView() || !atLinchaoAfternoon() || !CanOpenView() ||
                HasAnyPersistedFacts() || !CanOpenView())
                return false;

            int arrivalSequence = getInt(M05LandingFacts.ArrivalSequenceKey);
            if (arrivalSequence < 0 || !CanOpenView())
                return false;

            bool shenWilling = shenLiWillingToMeet();
            if (!CanOpenView())
                return false;
            M16RespectAgencyTone closingTone = resolveClosingTone();
            if (!CanOpenView() ||
                !M16EpilogueSession.TryCreateFromSavedFacts(
                    getInt, shenWilling, closingTone,
                    out M16EpilogueSession created) ||
                !CanOpenView() ||
                getInt(M05LandingFacts.ArrivalSequenceKey) != arrivalSequence ||
                !CanOpenView())
                return false;

            session = created;
            openedArrivalSequence = arrivalSequence;
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
            var submittingView = activeView;
            var submittingSession = session;
            if (!IsCurrentView(submittingView, submittingSession) ||
                !atLinchaoAfternoon() ||
                !IsCurrentView(submittingView, submittingSession) ||
                shenLiWillingToMeet() != openedShenLiWillingToMeet ||
                !IsCurrentView(submittingView, submittingSession) ||
                resolveClosingTone() != openedClosingTone ||
                !IsCurrentView(submittingView, submittingSession))
                return;

            if (!submittingSession.TryChooseStandingSpotAndCommit(spot, getInt, setInt))
                return;

            if (activeView == submittingView)
            {
                submittingView.Dispose();
                activeView = null;
                openedArrivalSequence = null;
            }
        }

        private bool CanOpenView() =>
            !disposed && parent != null && activeView == null;

        private bool IsOpenView(M16EpilogueView view, M16EpilogueSession expectedSession) =>
            !disposed && parent != null && view != null && expectedSession != null &&
            activeView == view && session == expectedSession && !view.IsDisposed;

        private bool IsCurrentView(M16EpilogueView view, M16EpilogueSession expectedSession)
        {
            int? arrival = openedArrivalSequence;
            if (!IsOpenView(view, expectedSession) || !arrival.HasValue)
                return false;
            int currentArrival = getInt(M05LandingFacts.ArrivalSequenceKey);
            return IsOpenView(view, expectedSession) &&
                openedArrivalSequence == arrival && currentArrival == arrival.Value;
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
