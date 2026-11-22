using System;
using UnityEngine;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Scene-callable ST-02 ("河面上两盏头灯") host. Enforces live Luting dock
    /// presence and clean ConfirmedKey / PendingSignatureKey before opening
    /// ST02RiverLanternsView, and persists the chosen rescue outcome through
    /// ST02RiverLanternsSession with retry-safe idempotent commit recovery.
    /// </summary>
    public sealed class ST02RiverLanternsHost : IDisposable
    {
        private readonly Transform parent;
        private readonly Func<bool> atLutingDock;
        private readonly Func<string, int> getInt;
        private readonly Action<string, int> setInt;
        private ST02RiverLanternsSession session;
        private ST02RiverLanternsView activeView;
        private bool disposed;

        public ST02RiverLanternsSession Session => session;
        public ST02RiverLanternsView ActiveView =>
            (activeView != null && !activeView.IsDisposed) ? activeView : null;
        public bool IsDisposed => disposed;
        public bool IsCompleted =>
            ST02RiverLanternsFacts.TryRead(getInt, out ST02RiverLanternsRecord? saved) &&
            saved.HasValue;

        public ST02RiverLanternsHost(
            Transform parent,
            Func<bool> atLutingDock,
            Func<string, int> getInt,
            Action<string, int> setInt)
        {
            this.parent = parent ?? throw new ArgumentNullException(nameof(parent));
            this.atLutingDock = atLutingDock ??
                throw new ArgumentNullException(nameof(atLutingDock));
            this.getInt = getInt ?? throw new ArgumentNullException(nameof(getInt));
            this.setInt = setInt ?? throw new ArgumentNullException(nameof(setInt));
        }

        public bool TryOpen()
        {
            if (activeView != null && activeView.IsDisposed)
            {
                activeView.Dispose();
                activeView = null;
            }
            if (disposed || parent == null || activeView != null || !atLutingDock() ||
                getInt(ST02RiverLanternsFacts.ConfirmedKey) != 0 ||
                getInt(ST02RiverLanternsFacts.PendingSignatureKey) != 0 ||
                !ST02RiverLanternsFacts.TryRead(getInt, out _))
                return false;

            session = new ST02RiverLanternsSession();
            activeView = new ST02RiverLanternsView(
                parent,
                session,
                HandleRescueSubmitted);
            return true;
        }

        private void HandleRescueSubmitted(ST02LockRescueMethod rescueMethod)
        {
            if (disposed || session == null || activeView == null || activeView.IsDisposed || !atLutingDock())
                return;

            if (!session.TryResolveOldLockAndCommit(rescueMethod, getInt, setInt))
                return;

            activeView.Dispose();
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
