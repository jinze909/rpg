using System;
using UnityEngine;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Scene-callable RC-03 ("白矶岛的多数") host. Enforces M08 case-3
    /// availability, live island presence, and clean ConfirmedKey before
    /// opening RC03BaijiMineView, and persists the chosen resolution through
    /// RC03BaijiMineSession with retry-safe idempotent commit recovery.
    /// </summary>
    public sealed class RC03BaijiMineHost : IDisposable
    {
        private readonly Transform parent;
        private readonly Func<bool> onBaijiIsland;
        private readonly Func<bool> diagramMatchedAtPumpTwo;
        private readonly Func<bool> successorTrainingConfirmed;
        private readonly Func<bool> lanRuoWillingToStayOneYear;
        private readonly Func<string, int> getInt;
        private readonly Action<string, int> setInt;
        private RC03BaijiMineSession session;
        private RC03BaijiMineView activeView;
        private bool disposed;

        public RC03BaijiMineSession Session => session;
        public RC03BaijiMineView ActiveView => activeView;
        public bool IsDisposed => disposed;
        public bool IsResolved =>
            RC03BaijiMineFacts.TryRead(getInt, out RC03BaijiMineRecord? saved) &&
            saved.HasValue;

        public RC03BaijiMineHost(
            Transform parent,
            Func<bool> onBaijiIsland,
            Func<bool> diagramMatchedAtPumpTwo,
            Func<bool> successorTrainingConfirmed,
            Func<bool> lanRuoWillingToStayOneYear,
            Func<string, int> getInt,
            Action<string, int> setInt)
        {
            this.parent = parent ?? throw new ArgumentNullException(nameof(parent));
            this.onBaijiIsland = onBaijiIsland ??
                throw new ArgumentNullException(nameof(onBaijiIsland));
            this.diagramMatchedAtPumpTwo = diagramMatchedAtPumpTwo ??
                throw new ArgumentNullException(nameof(diagramMatchedAtPumpTwo));
            this.successorTrainingConfirmed = successorTrainingConfirmed ??
                throw new ArgumentNullException(nameof(successorTrainingConfirmed));
            this.lanRuoWillingToStayOneYear = lanRuoWillingToStayOneYear ??
                throw new ArgumentNullException(nameof(lanRuoWillingToStayOneYear));
            this.getInt = getInt ?? throw new ArgumentNullException(nameof(getInt));
            this.setInt = setInt ?? throw new ArgumentNullException(nameof(setInt));
        }

        public bool TryOpen()
        {
            if (disposed || activeView != null || !onBaijiIsland() ||
                getInt(RC03BaijiMineFacts.ConfirmedKey) != 0)
                return false;

            if (!RC03BaijiMineSession.TryCreateFromSavedFacts(
                    getInt, out RC03BaijiMineSession created))
                return false;

            session = created;
            activeView = new RC03BaijiMineView(
                parent,
                session,
                diagramMatchedAtPumpTwo,
                successorTrainingConfirmed,
                lanRuoWillingToStayOneYear,
                HandleResolutionSubmitted);
            return true;
        }

        private void HandleResolutionSubmitted(RC03Resolution resolution)
        {
            if (disposed || session == null || !onBaijiIsland())
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
