using System;
using UnityEngine;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Scene-callable M10 ("靖湾会盟") host. Enforces verified M09BerthTableFacts,
    /// live Jingwan assembly presence, and clean ConfirmedKey before opening
    /// M10AssemblyView, and persists the outcome through M10AssemblySession with
    /// retry-safe idempotent commit recovery.
    /// </summary>
    public sealed class M10AssemblyHost : IDisposable
    {
        private readonly Transform parent;
        private readonly Func<bool> atJingwanAssembly;
        private readonly Func<bool> threeRecordsMatched;
        private readonly Func<bool> carriesM02BattleReport;
        private readonly Func<string, int> getInt;
        private readonly Action<string, int> setInt;
        private M10AssemblySession session;
        private M10AssemblyView activeView;
        private bool disposed;

        public M10AssemblySession Session => session;
        public M10AssemblyView ActiveView => activeView;
        public bool IsDisposed => disposed;
        public bool IsCompleted =>
            M10AssemblyFacts.TryRead(getInt, out M10AssemblyRecord? saved) &&
            saved.HasValue;

        public M10AssemblyHost(
            Transform parent,
            Func<bool> atJingwanAssembly,
            Func<bool> threeRecordsMatched,
            Func<bool> carriesM02BattleReport,
            Func<string, int> getInt,
            Action<string, int> setInt)
        {
            this.parent = parent ?? throw new ArgumentNullException(nameof(parent));
            this.atJingwanAssembly = atJingwanAssembly ??
                throw new ArgumentNullException(nameof(atJingwanAssembly));
            this.threeRecordsMatched = threeRecordsMatched ??
                throw new ArgumentNullException(nameof(threeRecordsMatched));
            this.carriesM02BattleReport = carriesM02BattleReport ??
                throw new ArgumentNullException(nameof(carriesM02BattleReport));
            this.getInt = getInt ?? throw new ArgumentNullException(nameof(getInt));
            this.setInt = setInt ?? throw new ArgumentNullException(nameof(setInt));
        }

        public bool TryOpen()
        {
            if (disposed || activeView != null || !atJingwanAssembly() ||
                getInt(M10AssemblyFacts.ConfirmedKey) != 0)
                return false;

            if (!M10AssemblySession.TryCreateFromSavedFacts(getInt, out M10AssemblySession created))
                return false;

            session = created;
            activeView = new M10AssemblyView(
                parent,
                session,
                threeRecordsMatched,
                carriesM02BattleReport,
                () => M10AssemblyFacts.EvaluateRC02LeveragePenalty(getInt),
                () => M10AssemblyFacts.EvaluateRC05SupplyNeed(getInt),
                HandleCommitRequested);
            return true;
        }

        private void HandleCommitRequested()
        {
            if (disposed || session == null || !atJingwanAssembly())
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
