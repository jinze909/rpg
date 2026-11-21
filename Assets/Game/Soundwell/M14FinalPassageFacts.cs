using System;
using GamingIsLove.Makinom;

namespace Rpg.Soundwell
{
    public enum M14PassageOutcome
    {
        PendingRemedy = 0,
        RouteASupervisedCivilianEstablished = 1,
        RouteBArmedPassageEstablished = 2,
        RouteCNeutralBypassEstablished = 3
    }

    public enum M14BorderForceAction
    {
        NotRouteB = 0,
        StopAtEscort = 1,
        PursueAndSeizeWarehouses = 2
    }

    public enum M14LuJunDisposition
    {
        NotSettledInRouteA = 0,
        RelievedOfCommand = 1,
        Detained = 2,
        WithdrewWithVolunteers = 3
    }

    /// <summary>
    /// Immutable M14 ("同一个海峡，不同的进场") final passage outcome persisted
    /// in Makinom. Enforces SCENE_SCRIPT_M14.md v1.2 (including 4122fd46) and
    /// NARRATIVE_FACTS.md:
    /// - Strictly gated on verified M13DepartureRiskFacts departure commitment.
    /// - Enforces 1-to-1 route-to-outcome alignment (Route A/B/C cannot claim
    ///   another route's main ending; PendingRemedy cannot trigger any ending).
    /// - Requires verified voluntary civilian exit counts (departing, staying,
    ///   undecided ticket-holders) rather than assuming gate opening = full exit.
    /// - Enforces Route B border_force_action isolation, Route A single Lu Jun
    ///   disposition, and M13DepartureRiskFacts A Yuan high-risk safety rules.
    /// </summary>
    public readonly struct M14FinalPassageRecord
    {
        public M14FinalPassageRecord(
            M13ApproachRoute executedRoute,
            M14PassageOutcome passageOutcome,
            bool civilianExitVerified,
            int verifiedDepartingCount,
            int verifiedStayingCount,
            int undecidedHoldingTicketCount,
            M14BorderForceAction borderForceAction,
            M14LuJunDisposition luJunDisposition,
            M14AyuanOutcome ayuanOutcome,
            bool fogMasteryBonusApplied)
        {
            ExecutedRoute = executedRoute;
            PassageOutcome = passageOutcome;
            CivilianExitVerified = civilianExitVerified;
            VerifiedDepartingCount = verifiedDepartingCount;
            VerifiedStayingCount = verifiedStayingCount;
            UndecidedHoldingTicketCount = undecidedHoldingTicketCount;
            BorderForceAction = borderForceAction;
            LuJunDisposition = luJunDisposition;
            AyuanOutcome = ayuanOutcome;
            FogMasteryBonusApplied = fogMasteryBonusApplied;
        }

        public M13ApproachRoute ExecutedRoute { get; }
        public M14PassageOutcome PassageOutcome { get; }
        public bool CivilianExitVerified { get; }
        public int VerifiedDepartingCount { get; }
        public int VerifiedStayingCount { get; }
        public int UndecidedHoldingTicketCount { get; }
        public M14BorderForceAction BorderForceAction { get; }
        public M14LuJunDisposition LuJunDisposition { get; }
        public M14AyuanOutcome AyuanOutcome { get; }
        public bool FogMasteryBonusApplied { get; }

        public int TotalAccountedCivilians =>
            VerifiedDepartingCount + VerifiedStayingCount + UndecidedHoldingTicketCount;

        public bool UnlocksMainEnding =>
            PassageOutcome != M14PassageOutcome.PendingRemedy && CivilianExitVerified;
    }

    public static class M14FinalPassageFacts
    {
        public const string ConfirmedKey = "rpg.return.m14.passage-confirmed";
        public const string ExecutedRouteKey = "rpg.return.m14.executed-route";
        public const string PassageOutcomeKey = "rpg.return.m14.final-passage-outcome";
        public const string CivilianExitVerifiedKey = "rpg.return.m14.civilian-exit-verified";
        public const string DepartingCountKey = "rpg.return.m14.civilian-departing-count";
        public const string StayingCountKey = "rpg.return.m14.civilian-staying-count";
        public const string UndecidedCountKey = "rpg.return.m14.civilian-undecided-count";
        public const string BorderForceActionKey = "rpg.return.m14.border-force-action";
        public const string LuJunDispositionKey = "rpg.return.m14.lu-jun-disposition";
        public const string FogMasteryBonusKey = "rpg.return.m14.fog-mastery-bonus";

        public static M14PassageOutcome ExpectedEstablishedOutcome(M13ApproachRoute route)
        {
            switch (route)
            {
                case M13ApproachRoute.RouteASupervisedCivilian:
                    return M14PassageOutcome.RouteASupervisedCivilianEstablished;
                case M13ApproachRoute.RouteBBellTowerAssault:
                    return M14PassageOutcome.RouteBArmedPassageEstablished;
                case M13ApproachRoute.RouteCOldCanalNightRun:
                    return M14PassageOutcome.RouteCNeutralBypassEstablished;
                default:
                    return M14PassageOutcome.PendingRemedy;
            }
        }

        public static bool IsValidRecord(M14FinalPassageRecord record, Func<string, int> getInt)
        {
            if (getInt == null ||
                !Enum.IsDefined(typeof(M13ApproachRoute), record.ExecutedRoute) ||
                !Enum.IsDefined(typeof(M14PassageOutcome), record.PassageOutcome) ||
                !Enum.IsDefined(typeof(M14BorderForceAction), record.BorderForceAction) ||
                !Enum.IsDefined(typeof(M14LuJunDisposition), record.LuJunDisposition) ||
                !Enum.IsDefined(typeof(M14AyuanOutcome), record.AyuanOutcome))
                return false;

            if (record.VerifiedDepartingCount < 0 ||
                record.VerifiedStayingCount < 0 ||
                record.UndecidedHoldingTicketCount < 0)
                return false;

            if (!M13DepartureRiskFacts.TryReadDeparture(getInt, out M13DepartureRecord? departure) ||
                !departure.HasValue ||
                departure.Value.Route != record.ExecutedRoute)
                return false;

            if (record.PassageOutcome == M14PassageOutcome.PendingRemedy)
                return false;

            if (record.PassageOutcome != ExpectedEstablishedOutcome(record.ExecutedRoute))
                return false;

            if (!record.CivilianExitVerified || record.TotalAccountedCivilians <= 0)
                return false;

            // Route B border_force_action isolation.
            if (record.ExecutedRoute == M13ApproachRoute.RouteBBellTowerAssault)
            {
                if (record.BorderForceAction == M14BorderForceAction.NotRouteB)
                    return false;
            }
            else if (record.BorderForceAction != M14BorderForceAction.NotRouteB)
            {
                return false;
            }

            // Route A single Lu Jun disposition isolation.
            if (record.ExecutedRoute == M13ApproachRoute.RouteASupervisedCivilian)
            {
                if (record.LuJunDisposition == M14LuJunDisposition.NotSettledInRouteA)
                    return false;
            }
            else if (record.LuJunDisposition != M14LuJunDisposition.NotSettledInRouteA)
            {
                return false;
            }

            // Fog mastery bonus applies only to Route C night canal run.
            if (record.FogMasteryBonusApplied &&
                record.ExecutedRoute != M13ApproachRoute.RouteCOldCanalNightRun)
                return false;

            // Enforce M13/M14 A Yuan safety guarantee.
            if (record.AyuanOutcome != M14AyuanOutcome.Safe &&
                !departure.Value.CanAyuanSufferHighRiskOutcome)
                return false;

            return true;
        }

        public static bool HasAnyPersistedFacts(Func<string, int> getInt)
        {
            return getInt == null ||
                getInt(ConfirmedKey) != 0 ||
                getInt(ExecutedRouteKey) != 0 ||
                getInt(PassageOutcomeKey) != 0 ||
                getInt(CivilianExitVerifiedKey) != 0 ||
                getInt(DepartingCountKey) != 0 ||
                getInt(StayingCountKey) != 0 ||
                getInt(UndecidedCountKey) != 0 ||
                getInt(BorderForceActionKey) != 0 ||
                getInt(LuJunDispositionKey) != 0 ||
                getInt(FogMasteryBonusKey) != 0 ||
                getInt(M13DepartureRiskFacts.AyuanOutcomeKey) != 0;
        }

        public static bool HasOrphanedDownstreamFacts(Func<string, int> getInt)
        {
            return getInt == null ||
                M15FinalRollCallFacts.HasAnyPersistedFacts(getInt) ||
                M15FinalRollCallFacts.HasOrphanedDownstreamFacts(getInt);
        }

        private static bool CompatiblePending(Func<string, int> getInt, string key, int expected)
        {
            int raw = getInt(key);
            return raw == 0 || raw == expected;
        }

        public static bool CanRecord(M14FinalPassageRecord record, Func<string, int> getInt)
        {
            return getInt != null &&
                getInt(ConfirmedKey) == 0 &&
                !HasOrphanedDownstreamFacts(getInt) &&
                IsValidRecord(record, getInt) &&
                CompatiblePending(getInt, M13DepartureRiskFacts.AyuanOutcomeKey, (int)record.AyuanOutcome) &&
                CompatiblePending(getInt, ExecutedRouteKey, (int)record.ExecutedRoute) &&
                CompatiblePending(getInt, PassageOutcomeKey, (int)record.PassageOutcome) &&
                CompatiblePending(getInt, CivilianExitVerifiedKey, record.CivilianExitVerified ? 1 : 0) &&
                CompatiblePending(getInt, DepartingCountKey, record.VerifiedDepartingCount) &&
                CompatiblePending(getInt, StayingCountKey, record.VerifiedStayingCount) &&
                CompatiblePending(getInt, UndecidedCountKey, record.UndecidedHoldingTicketCount) &&
                CompatiblePending(getInt, BorderForceActionKey, (int)record.BorderForceAction) &&
                CompatiblePending(getInt, LuJunDispositionKey, (int)record.LuJunDisposition) &&
                CompatiblePending(getInt, FogMasteryBonusKey, record.FogMasteryBonusApplied ? 1 : 0);
        }

        public static bool TryRecord(M14FinalPassageRecord record)
        {
            var variables = Maki.Initialized ? Maki.Game?.Variables : null;
            return variables != null && TryRecord(
                record,
                key => variables.GetInt(key),
                (key, value) => variables.Set(key, value));
        }

        public static bool TryRecord(
            M14FinalPassageRecord record,
            Func<string, int> getInt,
            Action<string, int> setInt)
        {
            if (getInt == null || setInt == null || !CanRecord(record, getInt))
                return false;

            // Persist or verify A Yuan outcome through M13DepartureRiskFacts first
            // so combat retries or partial writes never contradict settled fate.
            if (!M13DepartureRiskFacts.TryReadAyuanOutcome(getInt, out M14AyuanOutcome? settledAyuan))
                return false;
            if (settledAyuan.HasValue)
            {
                if (settledAyuan.Value != record.AyuanOutcome)
                    return false;
            }
            else if (!M13DepartureRiskFacts.TryRecordAyuanOutcome(record.AyuanOutcome, getInt, setInt))
            {
                return false;
            }

            // Write primary passage disposition and outcome keys first so an
            // interrupted save locks out competing outcomes while allowing retries.
            setInt(ExecutedRouteKey, (int)record.ExecutedRoute);
            setInt(PassageOutcomeKey, (int)record.PassageOutcome);
            setInt(BorderForceActionKey, (int)record.BorderForceAction);
            setInt(LuJunDispositionKey, (int)record.LuJunDisposition);
            setInt(CivilianExitVerifiedKey, record.CivilianExitVerified ? 1 : 0);
            setInt(DepartingCountKey, record.VerifiedDepartingCount);
            setInt(StayingCountKey, record.VerifiedStayingCount);
            setInt(UndecidedCountKey, record.UndecidedHoldingTicketCount);
            setInt(FogMasteryBonusKey, record.FogMasteryBonusApplied ? 1 : 0);
            setInt(ConfirmedKey, 1);
            return true;
        }

        public static bool TryRead(out M14FinalPassageRecord? record)
        {
            record = null;
            var variables = Maki.Initialized ? Maki.Game?.Variables : null;
            return variables != null && TryRead(key => variables.GetInt(key), out record);
        }

        public static bool TryRead(Func<string, int> getInt, out M14FinalPassageRecord? record)
        {
            record = null;
            if (getInt == null)
                return false;
            if (getInt(ConfirmedKey) != 1)
                return true;

            if (!M13DepartureRiskFacts.TryReadAyuanOutcome(getInt, out M14AyuanOutcome? ayuan) ||
                !ayuan.HasValue)
                return true;

            int rawRoute = getInt(ExecutedRouteKey);
            int rawOutcome = getInt(PassageOutcomeKey);
            int rawExit = getInt(CivilianExitVerifiedKey);
            int departing = getInt(DepartingCountKey);
            int staying = getInt(StayingCountKey);
            int undecided = getInt(UndecidedCountKey);
            int rawForce = getInt(BorderForceActionKey);
            int rawLu = getInt(LuJunDispositionKey);
            int rawFogBonus = getInt(FogMasteryBonusKey);

            if (!Enum.IsDefined(typeof(M13ApproachRoute), rawRoute) ||
                !Enum.IsDefined(typeof(M14PassageOutcome), rawOutcome) ||
                (rawExit != 0 && rawExit != 1) ||
                !Enum.IsDefined(typeof(M14BorderForceAction), rawForce) ||
                !Enum.IsDefined(typeof(M14LuJunDisposition), rawLu) ||
                (rawFogBonus != 0 && rawFogBonus != 1))
                return true;

            var saved = new M14FinalPassageRecord(
                (M13ApproachRoute)rawRoute,
                (M14PassageOutcome)rawOutcome,
                rawExit == 1,
                departing,
                staying,
                undecided,
                (M14BorderForceAction)rawForce,
                (M14LuJunDisposition)rawLu,
                ayuan.Value,
                rawFogBonus == 1);

            if (IsValidRecord(saved, getInt))
                record = saved;
            return true;
        }

        public static bool TryResetForNewVoyage()
        {
            var variables = Maki.Initialized ? Maki.Game?.Variables : null;
            return variables != null && TryResetForNewVoyage(
                (key, value) => variables.Set(key, value));
        }

        public static bool TryResetForNewVoyage(Action<string, int> setInt)
        {
            if (setInt == null)
                return false;
            setInt(ConfirmedKey, 0);
            setInt(ExecutedRouteKey, 0);
            setInt(PassageOutcomeKey, 0);
            setInt(CivilianExitVerifiedKey, 0);
            setInt(DepartingCountKey, 0);
            setInt(StayingCountKey, 0);
            setInt(UndecidedCountKey, 0);
            setInt(BorderForceActionKey, 0);
            setInt(LuJunDispositionKey, 0);
            setInt(FogMasteryBonusKey, 0);
            return true;
        }
    }
}
