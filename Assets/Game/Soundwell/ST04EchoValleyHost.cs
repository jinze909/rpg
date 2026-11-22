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

        public ST04EchoValleyHost(Transform parent,
            IST04EchoValleyWorldEvidence world,
            Func<string, int> read, Action<string, int> write)
        {
            this.parent = parent ?? throw new ArgumentNullException(nameof(parent));
            this.world = world ?? throw new ArgumentNullException(nameof(world));
            this.read = read ?? throw new ArgumentNullException(nameof(read));
            this.write = write ?? throw new ArgumentNullException(nameof(write));
        }

        public ST04EchoValleyView ActiveView => activeView;
        public ST04EchoValleySession Session => session;
        public bool IsDisposed => disposed;

        public bool IsCommitted =>
            ST04EchoValleyFacts.TryRead(read, out ST04EchoValleyOutcome? result) &&
            result.HasValue;

        public bool TryOpen()
        {
            if (disposed || activeView != null ||
                !world.AtValley || !world.SangJingAndMengHuiPresent ||
                read(ST04EchoValleyFacts.ConfirmedKey) != 0)
                return false;

            // Do not open a corrupt empty record. An interrupted story
            // intent must replay through the identical recorded choice.
            if (read(ST04EchoValleyFacts.IntentKey) == 0 &&
                !ST04EchoValleyFacts.TryRead(read, out _))
                return false;
            if (!ST04EchoValleySession.TryCreate(read, out session))
                return false;

            activeView = new ST04EchoValleyView(
                parent, session, world, CanChooseRoad, HandleRoadChoice);
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

        private void HandleRoadChoice(ST04RoadResolution choice)
        {
            if (disposed || session == null || activeView == null ||
                !CanChooseRoad(choice))
                return;

            if (!pendingResolution.HasValue)
            {
                if (!session.TryChooseResolution(choice,
                        world.LongRoadActuallyFunded,
                        world.AlternativeWorksiteActuallyArranged))
                    return;
                pendingResolution = choice;
            }

            // May throw if a real Makinom write was interrupted. The
            // surviving view only offers a retry of the same choice.
            if (!session.TryCommit(read, write))
                return;

            activeView.Dispose();
            activeView = null;
        }

        public void RefreshView()
        {
            if (!disposed) activeView?.Refresh();
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
