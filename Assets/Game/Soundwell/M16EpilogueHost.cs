using System;
using UnityEngine;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Scene-callable M16 ("一个普通的下午") playable epilogue host. Enforces
    /// verified M15FinalRollCallFacts, live Linchao afternoon presence, monotonic
    /// M05 arrival identity, and clean ConfirmedKey before opening M16EpilogueView,
    /// and persists the chosen standing spot through M16EpilogueSession.
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
        private bool disposed;

        public M16EpilogueSession Session => session;
        public M16EpilogueView ActiveView => activeView;
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
            if (disposed || activeView != null || !atLinchaoAfternoon() ||
                getInt(M16EpilogueFacts.ConfirmedKey) != 0)
                return false;

            if (!M16EpilogueSession.TryCreateFromSavedFacts(
                    getInt, shenLiWillingToMeet(), resolveClosingTone(),
                    out M16EpilogueSession created))
                return false;

            session = created;
            openedArrivalSequence = getInt(M05LandingFacts.ArrivalSequenceKey);
            activeView = new M16EpilogueView(
                parent,
                session,
                HandleStandingSpotSubmitted);
            return true;
        }

        private void HandleStandingSpotSubmitted(M16StandingSpot spot)
        {
            if (disposed || session == null || !atLinchaoAfternoon() ||
                !openedArrivalSequence.HasValue ||
                getInt(M05LandingFacts.ArrivalSequenceKey) != openedArrivalSequence.Value)
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
