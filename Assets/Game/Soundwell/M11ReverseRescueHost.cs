using System;
using UnityEngine;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Scene-callable M11 ("这次轮到她来找你") host. Enforces verified
    /// M10AssemblyFacts, live Huilan Pass presence, monotonic M05 arrival identity,
    /// and clean ConfirmedKey before opening M11ReverseRescueView, and persists
    /// the outcome through M11ReverseRescueSession with retry-safe recovery.
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
        private bool disposed;

        public M11ReverseRescueSession Session => session;
        public M11ReverseRescueView ActiveView => activeView;
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
            if (disposed || activeView != null || !atHuilanPass() ||
                getInt(M11ReverseRescueFacts.ConfirmedKey) != 0)
                return false;

            if (!M11ReverseRescueSession.TryCreateFromSavedFacts(
                    getInt, shenLiAccompanying(), oulinParticipating(),
                    out M11ReverseRescueSession created))
                return false;

            session = created;
            openedArrivalSequence = getInt(M05LandingFacts.ArrivalSequenceKey);
            activeView = new M11ReverseRescueView(
                parent,
                session,
                HandleCommitRequested);
            return true;
        }

        private void HandleCommitRequested()
        {
            if (disposed || session == null || !atHuilanPass() ||
                !openedArrivalSequence.HasValue ||
                getInt(M05LandingFacts.ArrivalSequenceKey) != openedArrivalSequence.Value)
                return;

            if (!session.TryCommit(getInt, setInt))
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
