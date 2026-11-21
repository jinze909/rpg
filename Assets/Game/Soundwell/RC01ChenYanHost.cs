using System;
using UnityEngine;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Scene-callable RC-01 ("砚叔的两个家") host. Enforces M08 case-1
    /// availability, live ferry presence, and clean ConfirmedKey before
    /// opening RC01ChenYanView, and persists the roster decision through
    /// RC01ChenYanSession with retry-safe and idempotent commit recovery.
    /// </summary>
    public sealed class RC01ChenYanHost : IDisposable
    {
        private readonly Transform parent;
        private readonly Func<bool> atWhiteReedFerry;
        private readonly Func<string, int> getInt;
        private readonly Action<string, int> setInt;
        private RC01ChenYanSession session;
        private RC01ChenYanView activeView;
        private bool disposed;

        public RC01ChenYanSession Session => session;
        public RC01ChenYanView ActiveView => activeView;
        public bool IsDisposed => disposed;
        public bool IsResolved =>
            RC01ChenYanFacts.TryRead(getInt, out RC01ChenYanRecord? saved) &&
            saved.HasValue;

        public RC01ChenYanHost(
            Transform parent,
            Func<bool> atWhiteReedFerry,
            Func<string, int> getInt,
            Action<string, int> setInt)
        {
            this.parent = parent ?? throw new ArgumentNullException(nameof(parent));
            this.atWhiteReedFerry = atWhiteReedFerry ??
                throw new ArgumentNullException(nameof(atWhiteReedFerry));
            this.getInt = getInt ?? throw new ArgumentNullException(nameof(getInt));
            this.setInt = setInt ?? throw new ArgumentNullException(nameof(setInt));
        }

        public bool TryOpen()
        {
            if (disposed || activeView != null || !atWhiteReedFerry() ||
                getInt(RC01ChenYanFacts.ConfirmedKey) != 0)
                return false;

            if (!RC01ChenYanSession.TryCreateFromSavedFacts(
                    getInt, out RC01ChenYanSession created))
                return false;

            session = created;
            activeView = new RC01ChenYanView(parent, session, HandleOutcomeSubmitted);
            return true;
        }

        private void HandleOutcomeSubmitted(RC01ChenYanOutcome outcome)
        {
            if (disposed || session == null || !atWhiteReedFerry())
                return;

            if (!session.TrySettleRosterEntry(outcome, getInt, setInt))
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
