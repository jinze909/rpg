using System;
using UnityEngine;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Scene-callable RC-02 ("借来的名字") host. Enforces M08 case-2
    /// availability, live intake-desk presence, and clean ConfirmedKey before
    /// opening RC02ZhengYuanView, and persists the chosen resolution through
    /// RC02ZhengYuanSession with retry-safe idempotent commit recovery.
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
        private bool disposed;

        public RC02ZhengYuanSession Session => session;
        public RC02ZhengYuanView ActiveView => activeView;
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
            if (disposed || activeView != null || !atIntakeDesk() ||
                getInt(RC02ZhengYuanFacts.ConfirmedKey) != 0)
                return false;

            if (!RC02ZhengYuanSession.TryCreateFromSavedFacts(
                    getInt, realZhengYuanFound(), out RC02ZhengYuanSession created))
                return false;

            session = created;
            activeView = new RC02ZhengYuanView(
                parent, session, shiLengWillingForAdoption, HandleResolutionSubmitted);
            return true;
        }

        private void HandleResolutionSubmitted(RC02Resolution resolution)
        {
            if (disposed || session == null || !atIntakeDesk())
                return;

            if (!session.TrySettleResolution(resolution, getInt, setInt))
                return;

            activeView?.Dispose();
            activeView = null;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            activeView?.Dispose();
            activeView = null;
        }
    }
}
