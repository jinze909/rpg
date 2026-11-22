using System;
using UnityEngine;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Scene-callable RC-02 ("借来的名字") host. Enforces M08 case-2
    /// availability, live intake-desk presence, monotonic M05 arrival identity,
    /// live adoption consent revalidation at commit time, and clean
    /// RC02ZhengYuanFacts state (blocking torn/partial saves) before opening.
    /// </summary>
    public sealed class RC02ZhengYuanHost : IDisposable
    {
        private readonly Transform parent;
        private readonly Func<bool> atIntakeDesk;
        private readonly Func<bool> realZhengYuanFound;
        private readonly Func<bool> shiLengWillingForAdoption;
        private readonly Func<string, int> getInt;
        private readonly Action<string, int> setInt;
        private RC02ZhengYuanSession session;
        private RC02ZhengYuanView activeView;
        private int? openedArrivalSequence;
        private bool disposed;

        public RC02ZhengYuanSession Session => session;
        public RC02ZhengYuanView ActiveView =>
            (activeView != null && !activeView.IsDisposed) ? activeView : null;
        public bool IsDisposed => disposed;
        public bool IsResolved =>
            RC02ZhengYuanFacts.TryRead(getInt, out RC02ZhengYuanRecord? saved) &&
            saved.HasValue;

        public RC02ZhengYuanHost(
            Transform parent,
            Func<bool> atIntakeDesk,
            Func<bool> realZhengYuanFound,
            Func<bool> shiLengWillingForAdoption,
            Func<string, int> getInt,
            Action<string, int> setInt)
        {
            this.parent = parent ?? throw new ArgumentNullException(nameof(parent));
            this.atIntakeDesk = atIntakeDesk ??
                throw new ArgumentNullException(nameof(atIntakeDesk));
            this.realZhengYuanFound = realZhengYuanFound ??
                throw new ArgumentNullException(nameof(realZhengYuanFound));
            this.shiLengWillingForAdoption = shiLengWillingForAdoption ??
                throw new ArgumentNullException(nameof(shiLengWillingForAdoption));
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
            if (disposed || parent == null || activeView != null || !atIntakeDesk() ||
                HasAnyPersistedFacts())
                return false;

            if (!RC02ZhengYuanSession.TryCreateFromSavedFacts(
                    getInt, realZhengYuanFound(), out RC02ZhengYuanSession created))
                return false;

            session = created;
            openedArrivalSequence = getInt(M05LandingFacts.ArrivalSequenceKey);
            activeView = new RC02ZhengYuanView(
                parent, session, shiLengWillingForAdoption, HandleResolutionSubmitted);
            return true;
        }

        private bool HasAnyPersistedFacts()
        {
            return getInt(RC02ZhengYuanFacts.ConfirmedKey) != 0 ||
                getInt(RC02ZhengYuanFacts.PendingSignatureKey) != 0 ||
                getInt(RC02ZhengYuanFacts.ChecksMaskKey) != 0 ||
                getInt(RC02ZhengYuanFacts.RoofConfessionKey) != 0 ||
                getInt(RC02ZhengYuanFacts.ResolutionKey) != 0 ||
                getInt(RC02ZhengYuanFacts.MotherCalledKey) != 0 ||
                getInt(RC02ZhengYuanFacts.ShiLengConsentedKey) != 0 ||
                getInt(RC02ZhengYuanFacts.RealZhengYuanFoundKey) != 0;
        }

        private void HandleResolutionSubmitted(RC02Resolution resolution)
        {
            if (disposed || session == null || activeView == null || activeView.IsDisposed ||
                !atIntakeDesk() ||
                !openedArrivalSequence.HasValue ||
                getInt(M05LandingFacts.ArrivalSequenceKey) != openedArrivalSequence.Value ||
                // This panel snapshots whether the real Zheng Yuan was found
                // through actual roster and testimony. If RC-05 evidence
                // changes while the desk is open, never persist a stale claim
                // (or erase a new discovery) under Shi Leng's decision.
                realZhengYuanFound() != session.RealZhengYuanFound)
                return;

            if (resolution == RC02Resolution.AdoptAsShiLeng && !shiLengWillingForAdoption())
                return;

            if (!session.TrySettleResolution(resolution, getInt, setInt))
                return;

            activeView.Dispose();
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
