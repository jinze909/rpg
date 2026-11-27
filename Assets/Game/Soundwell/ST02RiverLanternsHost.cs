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
        private bool opening;

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
            if (opening || disposed || parent == null)
                return false;
            opening = true;
            try
            {
                return TryOpenCore();
            }
            finally
            {
                opening = false;
            }
        }

        private bool TryOpenCore()
        {
            if (activeView != null && activeView.IsDisposed)
            {
                activeView.Dispose();
                activeView = null;
            }
            if (disposed || parent == null || activeView != null || !atLutingDock() ||
                getInt(ST02RiverLanternsFacts.ConfirmedKey) != 0 ||
                getInt(ST02RiverLanternsFacts.PendingSignatureKey) != 0 ||
                !ST02RiverLanternsFacts.TryRead(getInt, out _) ||
                disposed || parent == null || activeView != null)
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
            var submittingView = activeView;
            var submittingSession = session;
            if (!IsCurrentSubmission(submittingView, submittingSession) ||
                !atLutingDock() ||
                !IsCurrentSubmission(submittingView, submittingSession))
                return;

            // Fact reads and writes can synchronously retire this dialog.
            // Never write additional fields for a retired encounter or close
            // a replacement view. Preserve the existing immutable intent retry.
            bool abandoned = false;
            void WriteWhileCurrent(string key, int value)
            {
                if (abandoned || !IsCurrentSubmission(submittingView, submittingSession))
                {
                    abandoned = true;
                    return;
                }
                setInt(key, value);
            }

            if (!submittingSession.TryResolveOldLockAndCommit(
                    rescueMethod, getInt, WriteWhileCurrent) ||
                abandoned || !IsCurrentSubmission(submittingView, submittingSession))
                return;

            submittingView.Dispose();
            activeView = null;
        }

        private bool IsCurrentSubmission(
            ST02RiverLanternsView expectedView, ST02RiverLanternsSession expectedSession)
        {
            return !disposed && parent != null && expectedView != null &&
                expectedSession != null && activeView == expectedView &&
                session == expectedSession && !expectedView.IsDisposed;
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
