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

        public ReturnStoryLedgerView ActiveView => view;
        public bool IsOpen => view != null;

        public ReturnStoryLedgerHost(Transform uiParent, Func<string, int> customGetInt = null)
        {
            this.uiParent = uiParent ?? throw new ArgumentNullException(nameof(uiParent));
            this.customGetInt = customGetInt;
        }

        public bool TryOpenOrRefresh(Action onClosed = null)
        {
            if (disposed)
                return false;

            bool ok = customGetInt != null
                ? ReturnStoryLedger.TryBuildSnapshot(customGetInt, out ReturnStoryProgressSnapshot snap)
                : ReturnStoryLedger.TryBuildSnapshot(out snap);
            if (!ok)
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
