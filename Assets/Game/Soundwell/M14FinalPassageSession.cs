using System;

namespace Rpg.Soundwell
{
    public enum M14Phase
    {
        RouteExecution = 0,
        CivilianExitVerification = 1,
        AyuanPositionResolution = 2,
        Completed = 3
    }

    /// <summary>
    /// Authoritative M14 ("同一个海峡，不同的进场") state machine.
    /// Executes the M13-confirmed route (A/B/C), requires explicit verification
    /// of voluntary civilian exit counts, settles A Yuan's position without
    /// violating M13's safety guarantees, and persists idempotently to Makinom.
    /// </summary>
    public sealed class M14FinalPassageSession
    {
        public M14Phase Phase { get; private set; } = M14Phase.RouteExecution;
        public M13DepartureRecord Departure { get; }
        public M14BorderForceAction BorderForceAction { get; private set; } =
            M14BorderForceAction.NotRouteB;
        public M14LuJunDisposition LuJunDisposition { get; private set; } =
            M14LuJunDisposition.NotSettledInRouteA;
        public bool FogMasteryBonusApplied { get; private set; }
        public bool RouteExecutionCleared { get; private set; }
        public bool CivilianExitVerified { get; private set; }
        public int VerifiedDepartingCount { get; private set; }
        public int VerifiedStayingCount { get; private set; }
        public int UndecidedHoldingTicketCount { get; private set; }
        public M14AyuanOutcome? AyuanOutcome { get; private set; }
        public M14FinalPassageRecord? CompletedRecord { get; private set; }
        public bool IsCompleted => Phase == M14Phase.Completed;

        private M14FinalPassageSession(M13DepartureRecord departure)
        {
            Departure = departure;
        }

        public static bool TryCreateFromSavedFacts(
            Func<string, int> getInt,
            out M14FinalPassageSession session)
        {
            session = null;
            if (getInt == null || M14FinalPassageFacts.HasOrphanedDownstreamFacts(getInt))
                return false;
            if (!M13DepartureRiskFacts.TryReadDeparture(getInt, out M13DepartureRecord? departure) ||
                !departure.HasValue)
                return false;

            session = new M14FinalPassageSession(departure.Value);
            return true;
        }

        public bool TryResolveRouteASupervisedSquare(
            bool validSupervisionCredentials,
            bool validPortGuarantee,
            M14LuJunDisposition luJunDisposition)
        {
            if (Phase != M14Phase.RouteExecution ||
                Departure.Route != M13ApproachRoute.RouteASupervisedCivilian ||
                !validSupervisionCredentials ||
                !validPortGuarantee ||
                !Enum.IsDefined(typeof(M14LuJunDisposition), luJunDisposition) ||
                luJunDisposition == M14LuJunDisposition.NotSettledInRouteA)
                return false;

            LuJunDisposition = luJunDisposition;
            RouteExecutionCleared = true;
            Phase = M14Phase.CivilianExitVerification;
            return true;
        }

        public bool TryResolveRouteBBellTowerAssault(
            bool threeOpenDoorBellsRung,
            M14BorderForceAction borderForceAction)
        {
            if (Phase != M14Phase.RouteExecution ||
                Departure.Route != M13ApproachRoute.RouteBBellTowerAssault ||
                !threeOpenDoorBellsRung ||
                !Enum.IsDefined(typeof(M14BorderForceAction), borderForceAction) ||
                borderForceAction == M14BorderForceAction.NotRouteB)
                return false;

            BorderForceAction = borderForceAction;
            RouteExecutionCleared = true;
            Phase = M14Phase.CivilianExitVerification;
            return true;
        }

        public bool TryResolveRouteCOldCanalRun(
            bool fogRelayCompleted,
            bool fogMasteryBonusAvailable)
        {
            if (Phase != M14Phase.RouteExecution ||
                Departure.Route != M13ApproachRoute.RouteCOldCanalNightRun ||
                !fogRelayCompleted)
                return false;

            FogMasteryBonusApplied = fogMasteryBonusAvailable;
            RouteExecutionCleared = true;
            Phase = M14Phase.CivilianExitVerification;
            return true;
        }

        public bool TryVerifyCivilianExit(
            int departingCount,
            int stayingCount,
            int undecidedHoldingTicketCount)
        {
            if (Phase != M14Phase.CivilianExitVerification ||
                !RouteExecutionCleared ||
                !M14FinalPassageFacts.HasValidCivilianCounts(Departure.Route,
                    departingCount, stayingCount, undecidedHoldingTicketCount))
                return false;

            VerifiedDepartingCount = departingCount;
            VerifiedStayingCount = stayingCount;
            UndecidedHoldingTicketCount = undecidedHoldingTicketCount;
            CivilianExitVerified = true;
            Phase = M14Phase.AyuanPositionResolution;
            return true;
        }

        public bool TrySettleAyuanAndCommit(
            M14AyuanOutcome outcome,
            Func<string, int> getInt,
            Action<string, int> setInt)
        {
            if (Phase != M14Phase.AyuanPositionResolution ||
                !CivilianExitVerified ||
                !Enum.IsDefined(typeof(M14AyuanOutcome), outcome) ||
                getInt == null || setInt == null)
                return false;

            if (outcome != M14AyuanOutcome.Safe && !Departure.CanAyuanSufferHighRiskOutcome)
                return false;

            var record = new M14FinalPassageRecord(
                Departure.Route,
                M14FinalPassageFacts.ExpectedEstablishedOutcome(Departure.Route),
                civilianExitVerified: true,
                VerifiedDepartingCount,
                VerifiedStayingCount,
                UndecidedHoldingTicketCount,
                BorderForceAction,
                LuJunDisposition,
                outcome,
                FogMasteryBonusApplied);

            if (!M14FinalPassageFacts.TryRead(getInt, out M14FinalPassageRecord? existing))
                return false;

            // Recover idempotently if a prior write committed ConfirmedKey = 1
            // before its acknowledgement was lost.
            if (existing.HasValue)
            {
                if (!Matches(record, existing.Value))
                    return false;
            }
            else if (getInt(M14FinalPassageFacts.ConfirmedKey) != 0 ||
                     !M14FinalPassageFacts.TryRecord(record, getInt, setInt))
            {
                return false;
            }

            if (!M14FinalPassageFacts.TryRead(getInt, out M14FinalPassageRecord? saved) ||
                !saved.HasValue || !Matches(record, saved.Value))
                return false;

            AyuanOutcome = outcome;
            CompletedRecord = saved.Value;
            Phase = M14Phase.Completed;
            return true;
        }

        private static bool Matches(M14FinalPassageRecord expected, M14FinalPassageRecord saved)
        {
            return expected.ExecutedRoute == saved.ExecutedRoute &&
                expected.PassageOutcome == saved.PassageOutcome &&
                expected.CivilianExitVerified == saved.CivilianExitVerified &&
                expected.VerifiedDepartingCount == saved.VerifiedDepartingCount &&
                expected.VerifiedStayingCount == saved.VerifiedStayingCount &&
                expected.UndecidedHoldingTicketCount == saved.UndecidedHoldingTicketCount &&
                expected.BorderForceAction == saved.BorderForceAction &&
                expected.LuJunDisposition == saved.LuJunDisposition &&
                expected.AyuanOutcome == saved.AyuanOutcome &&
                expected.FogMasteryBonusApplied == saved.FogMasteryBonusApplied;
        }
    }
}
