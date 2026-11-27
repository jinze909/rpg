using System;
using UnityEngine;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Authoritative observations supplied by the actual Unity world,
    /// scene triggers, or ORK encounters. Reading these properties must have
    /// no side effects. The UGUI panel does not simulate these events.
    /// </summary>
    public interface IST04EchoValleyWorldEvidence
    {
        bool AtValley { get; }
        bool SangJingAndMengHuiPresent { get; }
        bool HasInspected(ST04RoadClue clue);
        bool HasEnteredVia(ST04ValleyEntry approach);
        int SurveyorsActuallyRescued { get; }
        bool ClearContinuousEchoAttractsBeasts { get; }
        bool LowVoicesDoNotAttractBeasts { get; }
        bool WetBellLosesBeastTarget { get; }
        bool AncientRainAmplifierIdentified { get; }
        bool QuarryWaterDiversionConfirmed { get; }
        bool HasEnteredChamberVia(ST04ChamberEntry approach);
        bool CompanionsMarkedBeforeDrain { get; }
        bool DrainActuallyOpened { get; }
        bool HasResolvedBeastsAs(ST04BeastHandling outcome);
        bool RoadOutcomeActuallyEstablished(ST04RoadResolution outcome);
        bool LongRoadActuallyFunded { get; }
        bool AlternativeWorksiteActuallyArranged { get; }
    }

    /// <summary>
    /// ST-04 scene-callable UI host. Makinom owns regional facts, ORK/world
    /// owns real beasts and rescue actions, Unity only presents the UGUI.
    /// A pending resolution is immutable across retry after a torn write.
    /// </summary>
    public sealed class ST04EchoValleyHost : IDisposable
    {
        private readonly Transform parent;
        private readonly IST04EchoValleyWorldEvidence world;
        private readonly Func<string, int> read;
        private readonly Action<string, int> write;
        private ST04EchoValleyView activeView;
        private ST04EchoValleySession session;
        private ST04RoadResolution? pendingResolution;
        private bool disposed;
        private bool opening;

        public ST04EchoValleyHost(Transform parent,
            IST04EchoValleyWorldEvidence world,
            Func<string, int> read, Action<string, int> write)
        {
            this.parent = parent ?? throw new ArgumentNullException(nameof(parent));
            this.world = world ?? throw new ArgumentNullException(nameof(world));
            this.read = read ?? throw new ArgumentNullException(nameof(read));
            this.write = write ?? throw new ArgumentNullException(nameof(write));
        }

        public ST04EchoValleyView ActiveView =>
            activeView != null && !activeView.IsDisposed ? activeView : null;
        public ST04EchoValleySession Session => session;
        public bool IsDisposed => disposed;

        public bool IsCommitted =>
            ST04EchoValleyFacts.TryRead(read, out ST04EchoValleyOutcome? result) &&
            result.HasValue;

        public bool TryOpen()
        {
            if (disposed || parent == null || opening)
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
            if (disposed || parent == null || activeView != null ||
                !world.AtValley || !world.SangJingAndMengHuiPresent ||
                read(ST04EchoValleyFacts.ConfirmedKey) != 0)
                return false;

            // Do not open a corrupt empty record. An interrupted story
            // intent must replay through the identical recorded choice.
            if (disposed || parent == null ||
                (read(ST04EchoValleyFacts.IntentKey) == 0 &&
                 !ST04EchoValleyFacts.TryRead(read, out _)))
                return false;
            if (disposed || parent == null ||
                !ST04EchoValleySession.TryCreate(read, out ST04EchoValleySession created) ||
                disposed || parent == null || activeView != null)
                return false;

            // Fact access may retire this host synchronously. Publish the
            // session only once the original opening still owns a live view.
            var createdView = new ST04EchoValleyView(
                parent, created, world, CanChooseRoad, HandleRoadChoice);
            if (disposed || parent == null || createdView.IsDisposed)
            {
                createdView.Dispose();
                return false;
            }
            session = created;
            // The persisted intent, not a retired session's transient choice,
            // governs replay after a close and reopen.
            pendingResolution = null;
            activeView = createdView;
            return true;
        }

        private bool CanChooseRoad(ST04RoadResolution choice)
        {
            if (disposed || !world.AtValley ||
                !world.SangJingAndMengHuiPresent ||
                (pendingResolution.HasValue && pendingResolution.Value != choice) ||
                !world.RoadOutcomeActuallyEstablished(choice))
                return false;
            return world.LongRoadActuallyFunded ==
                       (choice == ST04RoadResolution.ReturnBeastsAndUseCostlyLongRoad) &&
                   world.AlternativeWorksiteActuallyArranged ==
                       (choice == ST04RoadResolution.PreserveRainWallAndCloseRoad);
        }

        private bool IsCurrentSubmission(ST04EchoValleyView view,
            ST04EchoValleySession expectedSession) =>
            !disposed && parent != null && view != null && !view.IsDisposed &&
            expectedSession != null && ReferenceEquals(activeView, view) &&
            ReferenceEquals(session, expectedSession);

        private void HandleRoadChoice(ST04RoadResolution choice)
        {
            var expectedView = activeView;
            var expectedSession = session;
            if (!IsCurrentSubmission(expectedView, expectedSession) ||
                !CanChooseRoad(choice) ||
                !IsCurrentSubmission(expectedView, expectedSession))
                return;

            if (!pendingResolution.HasValue)
            {
                bool funded = world.LongRoadActuallyFunded;
                bool alternative = world.AlternativeWorksiteActuallyArranged;
                if (!IsCurrentSubmission(expectedView, expectedSession) ||
                    !expectedSession.TryChooseResolution(choice, funded, alternative))
                    return;
                pendingResolution = choice;
            }

            bool abandoned = false;
            void WriteWhileCurrent(string key, int value)
            {
                if (!IsCurrentSubmission(expectedView, expectedSession))
                {
                    abandoned = true;
                    return;
                }
                write(key, value);
            }

            // Preserve interrupted-write retry semantics while preventing a
            // retired submission from writing or closing a replacement view.
            if (!expectedSession.TryCommit(read, WriteWhileCurrent) || abandoned ||
                !IsCurrentSubmission(expectedView, expectedSession))
                return;

            expectedView.Dispose();
            activeView = null;
        }

        public void RefreshView()
        {
            if (disposed) return;
            if (activeView != null && activeView.IsDisposed)
            {
                activeView = null;
                return;
            }
            activeView?.Refresh();
            if (activeView != null && activeView.IsDisposed)
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
