using System;
using UnityEngine;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Scene-callable host for opening and refreshing the unified Return story
    /// ledger panel from Makinom or an injected variable reader.
    /// </summary>
    public sealed class ReturnStoryLedgerHost : IDisposable
    {
        private readonly Transform uiParent;
        private readonly Func<string, int> customGetInt;
        private ReturnStoryLedgerView view;
        private bool disposed;
        private bool readingSnapshot;
        private bool snapshotCancelled;

        public ReturnStoryLedgerView ActiveView => (view != null && !view.IsDisposed) ? view : null;
        public bool IsOpen => ActiveView != null;

        public ReturnStoryLedgerHost(Transform uiParent, Func<string, int> customGetInt = null)
        {
            this.uiParent = uiParent ?? throw new ArgumentNullException(nameof(uiParent));
            this.customGetInt = customGetInt;
        }

        public bool TryOpenOrRefresh(Action onClosed = null)
        {
            if (disposed || uiParent == null || readingSnapshot)
                return false;

            readingSnapshot = true;
            snapshotCancelled = false;
            try
            {
                return TryOpenOrRefreshCore(onClosed);
            }
            finally
            {
                readingSnapshot = false;
                snapshotCancelled = false;
            }
        }

        private bool TryOpenOrRefreshCore(Action onClosed)
        {
            if (view != null && view.IsDisposed)
            {
                var stale = view;
                view = null;
                stale.Dispose();
            }

            var originalView = view;
            bool ok = customGetInt != null
                ? ReturnStoryLedger.TryBuildSnapshot(customGetInt, out ReturnStoryProgressSnapshot snap)
                : ReturnStoryLedger.TryBuildSnapshot(out snap);
            // Reading live Makinom facts can synchronously close this panel
            // or destroy its scene. Do not recreate or refresh a stale view.
            if (!ok || disposed || uiParent == null || snapshotCancelled ||
                view != originalView || (originalView != null && originalView.IsDisposed))
                return false;

            if (view == null)
            {
                view = new ReturnStoryLedgerView(uiParent, snap, () =>
                {
                    view = null;
                    onClosed?.Invoke();
                });
            }
            else
            {
                view.UpdateSnapshot(snap);
            }
            return true;
        }

        public void Close()
        {
            if (readingSnapshot)
                snapshotCancelled = true;
            if (view == null)
                return;
            var current = view;
            view = null;
            current.Dispose();
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            Close();
        }
    }
}
