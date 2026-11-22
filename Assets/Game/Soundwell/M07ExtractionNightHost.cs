using System;
using UnityEngine;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Scene-callable M07 ("把还活着的人带出来") extraction-night and pre-dawn
    /// dock roll-call host. Enforces upstream M06RescuePlanFacts, live scene
    /// readiness, M05 arrival voyage identity, and clean M07ExtractionFacts
    /// before opening M07ExtractionNightView, and persists roll-call settlement
    /// to Makinom with interrupted-write retry and idempotent commit-then-throw
    /// recovery.
    /// </summary>
    public sealed class M07ExtractionNightHost : IDisposable
    {
        private readonly Transform parent;
        private readonly Func<bool> nightExtractionReady;
        private readonly Func<bool> taoJiRescuedInScene;
        private readonly Func<string, int> getInt;
        private readonly Action<string, int> setInt;
        private readonly int defaultBoardedCount;
        private readonly int fallbackBoardedCount;
        private readonly M07YidaChoice defaultYidaChoice;
        private int? openedArrivalSequence;
        private M07ExtractionNightSession session;
        private M07ExtractionNightView activeView;
        private bool disposed;

        public M07ExtractionNightSession Session => session;
        public M07ExtractionNightView ActiveView
        {
            get
            {
                PruneDisposedView();
                return activeView;
            }
        }
        public bool IsDisposed => disposed;
        public bool IsCompleted =>
            M07ExtractionFacts.TryRead(getInt, out M07ExtractionRecord? saved) &&
            saved.HasValue;

        public M07ExtractionNightHost(
            Transform parent,
            Func<bool> nightExtractionReady,
            Func<bool> taoJiRescuedInScene,
            Func<string, int> getInt,
            Action<string, int> setInt,
            int defaultBoardedCount = 300,
            int fallbackBoardedCount = 180,
            M07YidaChoice defaultYidaChoice = M07YidaChoice.HandToInspector)
        {
            this.parent = parent ?? throw new ArgumentNullException(nameof(parent));
            this.nightExtractionReady = nightExtractionReady ??
                throw new ArgumentNullException(nameof(nightExtractionReady));
            this.taoJiRescuedInScene = taoJiRescuedInScene ??
                throw new ArgumentNullException(nameof(taoJiRescuedInScene));
            this.getInt = getInt ?? throw new ArgumentNullException(nameof(getInt));
            this.setInt = setInt ?? throw new ArgumentNullException(nameof(setInt));
            if (defaultBoardedCount <= 0 || defaultBoardedCount > M07ExtractionFacts.MaxFirstBatchBoarded)
                throw new ArgumentOutOfRangeException(nameof(defaultBoardedCount));
            if (fallbackBoardedCount <= 0 || fallbackBoardedCount > M07ExtractionFacts.MaxFirstBatchBoarded)
                throw new ArgumentOutOfRangeException(nameof(fallbackBoardedCount));
            if (!Enum.IsDefined(typeof(M07YidaChoice), defaultYidaChoice))
                throw new ArgumentOutOfRangeException(nameof(defaultYidaChoice));

            this.defaultBoardedCount = defaultBoardedCount;
            this.fallbackBoardedCount = fallbackBoardedCount;
            this.defaultYidaChoice = defaultYidaChoice;
        }

        private bool HasAnyPersistedFacts()
        {
            return getInt(M07ExtractionFacts.ConfirmedKey) != 0 ||
                getInt(M07ExtractionFacts.ExecutedRouteKey) != 0 ||
                getInt(M07ExtractionFacts.PumpStateKey) != 0 ||
                getInt(M07ExtractionFacts.RouteCostsKey) != 0 ||
                getInt(M07ExtractionFacts.BoardedFirstBatchKey) != 0 ||
                getInt(M07ExtractionFacts.LowerLevelEvacuatedKey) != 0 ||
                getInt(M07ExtractionFacts.RusFateKey) != 0 ||
                getInt(M07ExtractionFacts.LedgerStateKey) != 0 ||
                getInt(M07ExtractionFacts.LiangChoiceKey) != 0 ||
                getInt(M07ExtractionFacts.YidaChoiceKey) != 0 ||
                getInt(M07ExtractionFacts.TaoJiRescuedKey) != 0 ||
                M07ExtractionFacts.HasOrphanedM08SuccessorFacts(getInt);
        }

        private void PruneDisposedView()
        {
            if (activeView != null && activeView.IsDisposed)
            {
                activeView = null;
                session = null;
                openedArrivalSequence = null;
            }
        }

        public bool TryOpen()
        {
            PruneDisposedView();

            if (disposed || parent == null || activeView != null || !nightExtractionReady() ||
                HasAnyPersistedFacts())
                return false;

            int arrivalSequence = getInt(M05LandingFacts.ArrivalSequenceKey);
            if (arrivalSequence < 0)
                return false;

            if (!M07ExtractionNightSession.TryCreateFromSavedFacts(
                    getInt, out M07ExtractionNightSession created))
                return false;

            openedArrivalSequence = arrivalSequence;
            session = created;
            activeView = new M07ExtractionNightView(
                parent,
                session,
                HandleRollCallConfirmed,
                defaultBoardedCount,
                fallbackBoardedCount,
                defaultYidaChoice);
            return true;
        }

        private void HandleRollCallConfirmed(
            M07LiangChoice liangChoice,
            M07YidaChoice yidaChoice)
        {
            PruneDisposedView();
            if (disposed || parent == null || session == null || activeView == null ||
                !nightExtractionReady() ||
                !openedArrivalSequence.HasValue ||
                getInt(M05LandingFacts.ArrivalSequenceKey) != openedArrivalSequence.Value)
                return;

            // The session owns all ten authoritative extraction fields.
            // Do not acknowledge a different roll call just because its
            // Liang and Yida choices happen to match the current dialogue.
            if (!session.TryCompleteDockRollCall(
                    liangChoice, yidaChoice, taoJiRescuedInScene(), getInt, setInt))
                return;

            activeView?.Dispose();
            activeView = null;
            openedArrivalSequence = null;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            activeView?.Dispose();
            activeView = null;
            openedArrivalSequence = null;
        }
    }
}
