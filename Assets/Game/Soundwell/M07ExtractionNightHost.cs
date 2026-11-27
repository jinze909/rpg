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
        private bool opening;

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
            if (opening)
                return false;

            // A readiness or persisted-fact reader can synchronously dispatch
            // another open before the outer call publishes its view.
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
            PruneDisposedView();

            if (!CanOpenView() || !nightExtractionReady() || !CanOpenView() ||
                HasAnyPersistedFacts() || !CanOpenView())
                return false;

            int arrivalSequence = getInt(M05LandingFacts.ArrivalSequenceKey);
            if (arrivalSequence < 0)
                return false;

            if (!M07ExtractionNightSession.TryCreateFromSavedFacts(
                    getInt, out M07ExtractionNightSession created) ||
                !CanOpenView() ||
                getInt(M05LandingFacts.ArrivalSequenceKey) != arrivalSequence ||
                !CanOpenView())
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
            var submittingView = activeView;
            var submittingSession = session;
            if (!IsCurrentView(submittingView, submittingSession) ||
                !nightExtractionReady() ||
                !IsCurrentView(submittingView, submittingSession))
                return;

            bool taoJiRescued = taoJiRescuedInScene();
            if (!IsCurrentView(submittingView, submittingSession))
                return;

            // The session owns all ten authoritative extraction fields.
            // Do not acknowledge a different roll call just because its
            // Liang and Yida choices happen to match the current dialogue.
            if (!submittingSession.TryCompleteDockRollCall(
                    liangChoice, yidaChoice, taoJiRescued, getInt, setInt))
                return;

            if (activeView == submittingView)
            {
                submittingView.Dispose();
                activeView = null;
                openedArrivalSequence = null;
            }
        }

        private bool CanOpenView() =>
            !disposed && parent != null && activeView == null;

        private bool IsOpenView(M07ExtractionNightView expectedView,
            M07ExtractionNightSession expectedSession) =>
            !disposed && parent != null && expectedView != null &&
            expectedSession != null && activeView == expectedView &&
            session == expectedSession && !expectedView.IsDisposed;

        private bool IsCurrentView(M07ExtractionNightView expectedView,
            M07ExtractionNightSession expectedSession)
        {
            int? arrival = openedArrivalSequence;
            if (!IsOpenView(expectedView, expectedSession) || !arrival.HasValue)
                return false;

            int currentArrival = getInt(M05LandingFacts.ArrivalSequenceKey);
            return IsOpenView(expectedView, expectedSession) &&
                openedArrivalSequence == arrival && currentArrival == arrival.Value;
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
