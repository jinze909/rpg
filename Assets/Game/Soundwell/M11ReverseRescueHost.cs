using System;
using UnityEngine;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Scene-callable M11 ("这次轮到她来找你") host. Enforces verified
    /// M10AssemblyFacts, live Huilan Pass presence, live Shen Li / Oulin
    /// participation revalidation at commit time, monotonic M05 arrival identity,
    /// and clean M11ReverseRescueFacts state (blocking torn/partial saves) before
    /// opening M11ReverseRescueView, and persists the outcome with retry safety.
    /// </summary>
    public sealed class M11ReverseRescueHost : IDisposable
    {
        private readonly Transform parent;
        private readonly Func<bool> atHuilanPass;
        private readonly Func<bool> shenLiAccompanying;
        private readonly Func<bool> oulinParticipating;
        private readonly Func<string, int> getInt;
        private readonly Action<string, int> setInt;
        private M11ReverseRescueSession session;
        private M11ReverseRescueView activeView;
        private int? openedArrivalSequence;
        private bool openedShenLiAccompanying;
        private bool openedOulinParticipating;
        private bool disposed;

        public M11ReverseRescueSession Session => session;
        public M11ReverseRescueView ActiveView =>
            (activeView != null && !activeView.IsDisposed) ? activeView : null;
        public bool IsDisposed => disposed;
        public bool IsCompleted =>
            M11ReverseRescueFacts.TryRead(getInt, out M11ReverseRescueRecord? saved) &&
            saved.HasValue;

        public M11ReverseRescueHost(
            Transform parent,
            Func<bool> atHuilanPass,
            Func<bool> shenLiAccompanying,
            Func<bool> oulinParticipating,
            Func<string, int> getInt,
            Action<string, int> setInt)
        {
            this.parent = parent ?? throw new ArgumentNullException(nameof(parent));
            this.atHuilanPass = atHuilanPass ??
                throw new ArgumentNullException(nameof(atHuilanPass));
            this.shenLiAccompanying = shenLiAccompanying ??
                throw new ArgumentNullException(nameof(shenLiAccompanying));
            this.oulinParticipating = oulinParticipating ??
                throw new ArgumentNullException(nameof(oulinParticipating));
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
            if (disposed || parent == null || activeView != null || !atHuilanPass() ||
                HasAnyPersistedFacts())
                return false;

            bool shenLi = shenLiAccompanying();
            bool oulin = oulinParticipating();
            if (!M11ReverseRescueSession.TryCreateFromSavedFacts(
                    getInt, shenLi, oulin,
                    out M11ReverseRescueSession created))
                return false;

            session = created;
            openedArrivalSequence = getInt(M05LandingFacts.ArrivalSequenceKey);
            openedShenLiAccompanying = shenLi;
            openedOulinParticipating = oulin;
            activeView = new M11ReverseRescueView(
                parent,
                session,
                HandleCommitRequested);
            return true;
        }

        private bool HasAnyPersistedFacts()
        {
            return getInt(M11ReverseRescueFacts.ConfirmedKey) != 0 ||
                getInt(M11ReverseRescueFacts.ThreeBellsKey) != 0 ||
                getInt(M11ReverseRescueFacts.EntryMethodKey) != 0 ||
                getInt(M11ReverseRescueFacts.ConscriptionObservedKey) != 0 ||
                getInt(M11ReverseRescueFacts.ThirdSquadStanceKey) != 0 ||
                getInt(M11ReverseRescueFacts.RescueProviderKey) != 0 ||
                getInt(M11ReverseRescueFacts.EchoCallsKey) != 0 ||
                getInt(M11ReverseRescueFacts.OulinJoinedKey) != 0 ||
                getInt(M11ReverseRescueFacts.DivisionPactKey) != 0;
        }

        private void HandleCommitRequested()
        {
            if (disposed || session == null || activeView == null || activeView.IsDisposed ||
                !atHuilanPass() ||
                !openedArrivalSequence.HasValue ||
                getInt(M05LandingFacts.ArrivalSequenceKey) != openedArrivalSequence.Value ||
                shenLiAccompanying() != openedShenLiAccompanying ||
                oulinParticipating() != openedOulinParticipating)
                return;

            // Persistence callbacks can synchronously close the host or its view.
            // Only retire the view that requested this commit, never a replacement.
            var committingView = activeView;
            var committingSession = session;
            if (!committingSession.TryCommit(getInt, setInt))
                return;

            committingView.Dispose();
            if (ReferenceEquals(activeView, committingView))
            {
                activeView = null;
                openedArrivalSequence = null;
            }
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
