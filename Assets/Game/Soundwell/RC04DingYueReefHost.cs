using System;
using UnityEngine;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Scene-callable RC-04 ("丁岳与借声兽") host orchestrating the M08 roster
    /// availability gate, monotonic M05 arrival identity, three-round salt-fog
    /// listening view, verified tidal-window reef approach handoff, and
    /// four-choice on-reef resolution view with retry-safe Makinom persistence.
    /// </summary>
    public sealed class RC04DingYueReefHost : IDisposable
    {
        public const int RosterCaseNumber = 4;

        private readonly Transform parent;
        private readonly Func<bool> canApproachReef;
        private readonly Func<RC04Resolution, bool> verifiedSceneConditions;
        private readonly Func<string, int> getInt;
        private readonly Action<string, int> setInt;
        private RC04DingYueReefSession session;
        private RC04ReefListeningView listeningView;
        private RC04ReefResolutionView resolutionView;
        private int? openedArrivalSequence;
        private bool approachConfirmed;
        private bool disposed;

        public RC04DingYueReefSession Session => session;
        public RC04ReefListeningView ActiveListeningView => listeningView;
        public RC04ReefResolutionView ActiveResolutionView => resolutionView;
        public bool ApproachConfirmed => approachConfirmed;
        public bool IsDisposed => disposed;
        public bool IsResolved =>
            RC04DingYueReefFacts.TryRead(getInt, out RC04ReefRecord? saved) &&
            saved.HasValue;

        public RC04DingYueReefHost(
            Transform parent,
            Func<bool> canApproachReef,
            Func<RC04Resolution, bool> verifiedSceneConditions,
            Func<string, int> getInt,
            Action<string, int> setInt)
        {
            this.parent = parent ?? throw new ArgumentNullException(nameof(parent));
            this.canApproachReef = canApproachReef ??
                throw new ArgumentNullException(nameof(canApproachReef));
            this.verifiedSceneConditions = verifiedSceneConditions ??
                throw new ArgumentNullException(nameof(verifiedSceneConditions));
            this.getInt = getInt ?? throw new ArgumentNullException(nameof(getInt));
            this.setInt = setInt ?? throw new ArgumentNullException(nameof(setInt));
        }

        public bool TryOpenListening()
        {
            if (disposed || listeningView != null || resolutionView != null ||
                getInt(RC04DingYueReefFacts.ConfirmedKey) != 0 ||
                getInt(RC04DingYueReefFacts.RoundsCompletedKey) != 0 ||
                getInt(RC04DingYueReefFacts.AyuanSongUsedKey) != 0 ||
                getInt(RC04DingYueReefFacts.ResolutionKey) != 0 ||
                getInt(RC04DingYueReefFacts.DecoyPreparedKey) != 0)
                return false;

            if (!M08FirstWorldNoticeFacts.TryCaseAvailable(
                    RosterCaseNumber, getInt, out bool available) ||
                !available)
                return false;

            session = session ?? new RC04DingYueReefSession();
            openedArrivalSequence = getInt(M05LandingFacts.ArrivalSequenceKey);
            listeningView = new RC04ReefListeningView(
                parent, session, HandleApproachRequested);
            return true;
        }

        private bool HandleApproachRequested(RC04DingYueReefSession activeSession)
        {
            if (disposed || activeSession != session || session == null ||
                !session.TidalWindowLocated || session.IsResolved ||
                !canApproachReef() ||
                !openedArrivalSequence.HasValue ||
                getInt(M05LandingFacts.ArrivalSequenceKey) != openedArrivalSequence.Value)
                return false;

            if (!M08FirstWorldNoticeFacts.TryCaseAvailable(
                    RosterCaseNumber, getInt, out bool available) ||
                !available)
                return false;

            approachConfirmed = true;
            listeningView?.Dispose();
            listeningView = null;
            resolutionView = new RC04ReefResolutionView(
                parent, session, verifiedSceneConditions, HandleResolutionRequested);
            return true;
        }

        private bool HandleResolutionRequested(RC04Resolution resolution)
        {
            if (disposed || !approachConfirmed || session == null ||
                !session.TidalWindowLocated || !verifiedSceneConditions(resolution) ||
                !openedArrivalSequence.HasValue ||
                getInt(M05LandingFacts.ArrivalSequenceKey) != openedArrivalSequence.Value)
                return false;

            if (!M08FirstWorldNoticeFacts.TryCaseAvailable(
                    RosterCaseNumber, getInt, out bool available) ||
                !available)
                return false;

            if (!RC04DingYueReefFacts.TryRead(getInt, out RC04ReefRecord? existing))
                return false;

            // Handle a commit that succeeded before a lost acknowledgement
            // without issuing duplicate writes.
            if (existing.HasValue)
            {
                if (existing.Value.Resolution != resolution)
                    return false;
            }
            else if (!session.TryResolveReef(resolution, getInt, setInt))
            {
                return false;
            }

            if (!RC04DingYueReefFacts.TryRead(getInt, out RC04ReefRecord? persisted) ||
                !persisted.HasValue ||
                persisted.Value.Resolution != resolution)
                return false;

            resolutionView?.Dispose();
            resolutionView = null;
            openedArrivalSequence = null;
            return true;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            listeningView?.Dispose();
            listeningView = null;
            resolutionView?.Dispose();
            resolutionView = null;
            openedArrivalSequence = null;
        }
    }
}
