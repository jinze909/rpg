using System;
using UnityEngine;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Scene-callable ST-06 ("陶霁返乡") homecoming host. Enforces verified
    /// M07 Tao Ji rescue and live woodshop presence, drives
    /// ST06TaoJiHomecomingView, and persists settlement to Makinom with
    /// interrupted-write retry and idempotent commit-then-throw recovery.
    /// </summary>
    public sealed class ST06TaoJiHomecomingHost : IDisposable
    {
        private readonly Transform parent;
        private readonly Func<bool> taoJiAtWoodshop;
        private readonly Func<string, int> getInt;
        private readonly Action<string, int> setInt;
        private ST06TaoJiHomecomingSession session;
        private ST06TaoJiHomecomingView activeView;
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
                getInt(ST06TaoJiHomecomingFacts.ConfirmedKey) != 0)
                return false;

            if (!ST06TaoJiHomecomingSession.TryCreateFromSavedFacts(
                    getInt, out ST06TaoJiHomecomingSession created))
                return false;

            session = created;
            activeView = new ST06TaoJiHomecomingView(
                parent, session, HandleSettleRequested);
            return true;
        }

        private void HandleSettleRequested(ST06TaoJiHomecomingSession activeSession)
        {
            if (disposed || activeSession != session || session == null ||
                !session.ChairResolution.HasValue || !taoJiAtWoodshop())
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
