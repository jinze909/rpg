using System;
using UnityEngine;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Scene-callable ST-03 ("第三声钟") host. Enforces live Zhongling bathhouse
    /// presence and clean ST03BathhouseFacts state before opening ST03BathhouseView,
    /// and persists the chosen permanent plan through ST03BathhouseSession with
    /// torn-write and lost-acknowledgement idempotent recovery.
    /// </summary>
    public sealed class ST03BathhouseHost : IDisposable
    {
        private readonly Transform parent;
        private readonly Func<bool> atZhonglingBathhouse;
        private readonly Func<bool> careHomeInterviewReady;
        private readonly Func<bool> safeRouteReady;
        private readonly Func<bool> trialNightReady;
        private readonly Func<bool> transportBudgetReady;
        private readonly Func<string, int> getInt;
        private readonly Action<string, int> setInt;
        private ST03BathhouseSession session;
        private ST03BathhouseView activeView;
        private bool disposed;

        public ST03BathhouseSession Session => session;
        public ST03BathhouseView ActiveView => activeView;
        public bool IsDisposed => disposed;
        public bool IsCompleted =>
            ST03BathhouseFacts.TryRead(getInt, out ST03BathhouseRecord? saved) &&
            saved.HasValue;

        public ST03BathhouseHost(
            Transform parent,
            Func<bool> atZhonglingBathhouse,
            Func<bool> careHomeInterviewReady,
            Func<bool> safeRouteReady,
            Func<bool> trialNightReady,
            Func<bool> transportBudgetReady,
            Func<string, int> getInt,
            Action<string, int> setInt)
        {
            this.parent = parent ?? throw new ArgumentNullException(nameof(parent));
            this.atZhonglingBathhouse = atZhonglingBathhouse ??
                throw new ArgumentNullException(nameof(atZhonglingBathhouse));
            this.careHomeInterviewReady = careHomeInterviewReady ??
                throw new ArgumentNullException(nameof(careHomeInterviewReady));
            this.safeRouteReady = safeRouteReady ??
                throw new ArgumentNullException(nameof(safeRouteReady));
            this.trialNightReady = trialNightReady ??
                throw new ArgumentNullException(nameof(trialNightReady));
            this.transportBudgetReady = transportBudgetReady ??
                throw new ArgumentNullException(nameof(transportBudgetReady));
            this.getInt = getInt ?? throw new ArgumentNullException(nameof(getInt));
            this.setInt = setInt ?? throw new ArgumentNullException(nameof(setInt));
        }

        public bool TryOpen()
        {
            if (disposed || activeView != null || !atZhonglingBathhouse())
                return false;

            if (!ST03BathhouseSession.TryCreate(getInt, out ST03BathhouseSession created))
                return false;

            session = created;
            activeView = new ST03BathhouseView(
                parent,
                session,
                careHomeInterviewReady,
                safeRouteReady,
                trialNightReady,
                transportBudgetReady,
                HandleCommitRequested);
            return true;
        }

        private void HandleCommitRequested()
        {
            if (disposed || session == null || !atZhonglingBathhouse())
                return;

            if (!session.TryCommit(getInt, setInt))
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
