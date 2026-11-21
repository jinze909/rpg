using System;
using UnityEngine;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Scene-callable ST-06 ("陶霁返乡") homecoming host. Enforces verified
    /// M07 Tao Ji rescue, monotonic M05 arrival identity, live woodshop presence,
    /// and clean ST06TaoJiHomecomingFacts state (blocking torn/partial saves)
    /// before opening ST06TaoJiHomecomingView.
    /// </summary>
    public sealed class ST06TaoJiHomecomingHost : IDisposable
    {
        private readonly Transform parent;
        private readonly Func<bool> taoJiAtWoodshop;
        private readonly Func<string, int> getInt;
        private readonly Action<string, int> setInt;
        private ST06TaoJiHomecomingSession session;
        private ST06TaoJiHomecomingView activeView;
        private int? openedArrivalSequence;
        private bool disposed;

        public ST06TaoJiHomecomingSession Session => session;
        public ST06TaoJiHomecomingView ActiveView => activeView;
        public bool IsDisposed => disposed;
        public bool IsSettled =>
            ST06TaoJiHomecomingFacts.TryRead(getInt, out ST06HomecomingRecord? saved) &&
            saved.HasValue;

        public ST06TaoJiHomecomingHost(
            Transform parent,
            Func<bool> taoJiAtWoodshop,
            Func<string, int> getInt,
            Action<string, int> setInt)
        {
            this.parent = parent ?? throw new ArgumentNullException(nameof(parent));
            this.taoJiAtWoodshop = taoJiAtWoodshop ??
                throw new ArgumentNullException(nameof(taoJiAtWoodshop));
            this.getInt = getInt ?? throw new ArgumentNullException(nameof(getInt));
            this.setInt = setInt ?? throw new ArgumentNullException(nameof(setInt));
        }

        public bool TryOpen()
        {
            if (disposed || activeView != null || !taoJiAtWoodshop() ||
                HasAnyPersistedFacts())
                return false;

            if (!ST06TaoJiHomecomingSession.TryCreateFromSavedFacts(
                    getInt, out ST06TaoJiHomecomingSession created))
                return false;

            session = created;
            openedArrivalSequence = getInt(M05LandingFacts.ArrivalSequenceKey);
            activeView = new ST06TaoJiHomecomingView(
                parent, session, HandleSettleRequested);
            return true;
        }

        private bool HasAnyPersistedFacts()
        {
            return getInt(ST06TaoJiHomecomingFacts.ConfirmedKey) != 0 ||
                getInt(ST06TaoJiHomecomingFacts.ChairResolutionKey) != 0 ||
                getInt(ST06TaoJiHomecomingFacts.CoverUpRemediatedKey) != 0 ||
                getInt(ST06TaoJiHomecomingFacts.WelcomeDinnerKey) != 0 ||
                getInt(ST06TaoJiHomecomingFacts.TraveledFarKey) != 0 ||
                getInt(ST06TaoJiHomecomingFacts.LetterStatusKey) != 0 ||
                getInt(ST06TaoJiHomecomingFacts.MarketTicketKey) != 0;
        }

        private void HandleSettleRequested(ST06TaoJiHomecomingSession activeSession)
        {
            if (disposed || activeSession != session || session == null ||
                !session.ChairResolution.HasValue || !taoJiAtWoodshop() ||
                !openedArrivalSequence.HasValue ||
                getInt(M05LandingFacts.ArrivalSequenceKey) != openedArrivalSequence.Value)
                return;

            if (!ST06TaoJiHomecomingFacts.TryRead(
                    getInt, out ST06HomecomingRecord? existing))
                return;

            // Handle a write that succeeded before a lost acknowledgement
            // without issuing duplicate writes.
            if (existing.HasValue)
            {
                if (existing.Value.ChairResolution != session.ChairResolution.Value)
                    return;
            }
            else if (!session.TrySettle(getInt, setInt))
            {
                return;
            }

            if (!ST06TaoJiHomecomingFacts.TryRead(
                    getInt, out ST06HomecomingRecord? persisted) ||
                !persisted.HasValue ||
                persisted.Value.ChairResolution != session.ChairResolution.Value)
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
