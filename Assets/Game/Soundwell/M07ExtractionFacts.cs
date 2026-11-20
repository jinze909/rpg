using System;
using GamingIsLove.Makinom;

namespace Rpg.Soundwell
{
    public enum M07ExecutedRoute
    {
        OfficialHandover = 1,
        CargoCover = 2,
        MinerShutdown = 3,
        ForceBreakout = 4,
        FallbackOfficialHandover = 5
    }

    public enum M07PumpShutdownState
    {
        KeptRunning = 0,
        SafelyStoppedThreeHours = 1
    }

    [Flags]
    public enum M07RouteCostFlags
    {
        None = 0,
        HarborSupplyDebt = 1 << 0,
        FreightCapacityLoss = 1 << 1,
        PumpOverhaulShiftCost = 1 << 2,
        PierRepairAndInjuryCost = 1 << 3,
        FallbackTransferCost = 1 << 4
    }

    public enum M07RusFate
    {
        Arrested = 1,
        Fled = 2,
        LostContract = 3,
        Sheltered = 4
    }

    public enum M07LedgerState
    {
        Preserved = 1,
        PartiallyBurned = 2
    }

    public enum M07LiangChoice
    {
        StayOnIsland = 1,
        PersuadedToLeave = 2,
        ForcedToBoard = 3
    }

    public enum M07YidaChoice
    {
        TakeAlong = 1,
        HandToInspector = 2,
        LeaveBehind = 3
    }

    /// <summary>
    /// Immutable M07 ("把还活着的人带出来") extraction night outcome persisted in
    /// Makinom. Keeps lower-level evacuation (500 to upper gallery under
    /// MinerShutdown) strictly separate from first-batch off-island boarding
    /// (up to 300 across multiple boats/trips), preserves incurred route costs
    /// on fallback, and never borrows the 3-hour pump shutdown for other plans.
    /// </summary>
    public readonly struct M07ExtractionRecord
    {
        public M07ExtractionRecord(
            M07ExecutedRoute executedRoute,
            M07PumpShutdownState pumpState,
            M07RouteCostFlags routeCosts,
            int boardedFirstBatchCount,
            int lowerLevelEvacuatedCount,
            M07RusFate rusFate,
            M07LedgerState ledgerState,
            M07LiangChoice liangChoice,
            M07YidaChoice yidaChoice,
            bool taoJiRescued)
        {
            ExecutedRoute = executedRoute;
            PumpState = pumpState;
            RouteCosts = routeCosts;
            BoardedFirstBatchCount = boardedFirstBatchCount;
            LowerLevelEvacuatedCount = lowerLevelEvacuatedCount;
            RusFate = rusFate;
            LedgerState = ledgerState;
            LiangChoice = liangChoice;
            YidaChoice = yidaChoice;
            TaoJiRescued = taoJiRescued;
        }

        public M07ExecutedRoute ExecutedRoute { get; }
        public M07PumpShutdownState PumpState { get; }
        public M07RouteCostFlags RouteCosts { get; }
        public int BoardedFirstBatchCount { get; }
        public int LowerLevelEvacuatedCount { get; }
        public M07RusFate RusFate { get; }
        public M07LedgerState LedgerState { get; }
        public M07LiangChoice LiangChoice { get; }
        public M07YidaChoice YidaChoice { get; }
        public bool TaoJiRescued { get; }
        public bool StrainsShenRelation => LiangChoice == M07LiangChoice.ForcedToBoard;
    }

    /// <summary>
    /// Persists and reads back M07 extraction outcomes through the single
    /// existing Makinom global variable store. Gated strictly on verified
    /// M06RescuePlanFacts and enforces narrative route/cost invariants.
    /// </summary>
    public static class M07ExtractionFacts
    {
        public const string ConfirmedKey = "rpg.return.m07.extraction-confirmed";
        public const string ExecutedRouteKey = "rpg.return.m07.executed-route";
        public const string PumpStateKey = "rpg.return.m07.pump-shutdown-state";
        public const string RouteCostsKey = "rpg.return.m07.route-cost-flags";
        public const string BoardedFirstBatchKey = "rpg.return.m07.boarded-first-batch";
        public const string LowerLevelEvacuatedKey = "rpg.return.m07.lower-level-evacuated";
        public const string RusFateKey = "rpg.return.m07.rus-fate";
        public const string LedgerStateKey = "rpg.return.m07.ledger-state";
        public const string LiangChoiceKey = "rpg.return.m07.survivor-liang";
        public const string YidaChoiceKey = "rpg.return.m07.survivor-yida";
        public const string TaoJiRescuedKey = "rpg.return.m07.survivor-taoji";

        public const int MaxFirstBatchBoarded = 300;
        public const int LowerLevelMinerCount = 500;
        public const int AllCostFlagsMask =
            (int)(M07RouteCostFlags.HarborSupplyDebt |
                  M07RouteCostFlags.FreightCapacityLoss |
                  M07RouteCostFlags.PumpOverhaulShiftCost |
                  M07RouteCostFlags.PierRepairAndInjuryCost |
                  M07RouteCostFlags.FallbackTransferCost);

        public static bool CanRecord(M07ExtractionRecord record, Func<string, int> getInt)
        {
            if (getInt == null || getInt(ConfirmedKey) != 0)
                return false;
            return IsValidRecord(record, getInt);
        }

        public static bool TryRecord(M07ExtractionRecord record)
        {
            var variables = Maki.Initialized ? Maki.Game?.Variables : null;
            return variables != null && TryRecord(
                record,
                key => variables.GetInt(key),
                (key, value) => variables.Set(key, value));
        }

        public static bool TryRecord(
            M07ExtractionRecord record,
            Func<string, int> getInt,
            Action<string, int> setInt)
        {
            if (getInt == null || setInt == null || !CanRecord(record, getInt))
                return false;

            setInt(ConfirmedKey, 0);
            setInt(ExecutedRouteKey, (int)record.ExecutedRoute);
            setInt(PumpStateKey, (int)record.PumpState);
            setInt(RouteCostsKey, (int)record.RouteCosts);
            setInt(BoardedFirstBatchKey, record.BoardedFirstBatchCount);
            setInt(LowerLevelEvacuatedKey, record.LowerLevelEvacuatedCount);
            setInt(RusFateKey, (int)record.RusFate);
            setInt(LedgerStateKey, (int)record.LedgerState);
            setInt(LiangChoiceKey, (int)record.LiangChoice);
            setInt(YidaChoiceKey, (int)record.YidaChoice);
            setInt(TaoJiRescuedKey, record.TaoJiRescued ? 1 : 0);
            setInt(ConfirmedKey, 1);
            return true;
        }

        public static bool TryRead(out M07ExtractionRecord? record)
        {
            record = null;
            var variables = Maki.Initialized ? Maki.Game?.Variables : null;
            return variables != null && TryRead(key => variables.GetInt(key), out record);
        }

        public static bool TryRead(Func<string, int> getInt, out M07ExtractionRecord? record)
        {
            record = null;
            if (getInt == null)
                return false;
            if (getInt(ConfirmedKey) != 1)
                return true;

            int rawRoute = getInt(ExecutedRouteKey);
            int rawPump = getInt(PumpStateKey);
            int rawCosts = getInt(RouteCostsKey);
            int rawBoarded = getInt(BoardedFirstBatchKey);
            int rawLower = getInt(LowerLevelEvacuatedKey);
            int rawRus = getInt(RusFateKey);
            int rawLedger = getInt(LedgerStateKey);
            int rawLiang = getInt(LiangChoiceKey);
            int rawYida = getInt(YidaChoiceKey);
            int rawTaoJi = getInt(TaoJiRescuedKey);

            if (!Enum.IsDefined(typeof(M07ExecutedRoute), rawRoute) ||
                !Enum.IsDefined(typeof(M07PumpShutdownState), rawPump) ||
                rawCosts < 0 || (rawCosts & ~AllCostFlagsMask) != 0 ||
                !Enum.IsDefined(typeof(M07RusFate), rawRus) ||
                !Enum.IsDefined(typeof(M07LedgerState), rawLedger) ||
                !Enum.IsDefined(typeof(M07LiangChoice), rawLiang) ||
                !Enum.IsDefined(typeof(M07YidaChoice), rawYida) ||
                (rawTaoJi != 0 && rawTaoJi != 1))
                return true;

            var saved = new M07ExtractionRecord(
                (M07ExecutedRoute)rawRoute,
                (M07PumpShutdownState)rawPump,
                (M07RouteCostFlags)rawCosts,
                rawBoarded,
                rawLower,
                (M07RusFate)rawRus,
                (M07LedgerState)rawLedger,
                (M07LiangChoice)rawLiang,
                (M07YidaChoice)rawYida,
                rawTaoJi == 1);

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
            setInt(PumpStateKey, 0);
            setInt(RouteCostsKey, 0);
            setInt(BoardedFirstBatchKey, 0);
            setInt(LowerLevelEvacuatedKey, 0);
            setInt(RusFateKey, 0);
            setInt(LedgerStateKey, 0);
            setInt(LiangChoiceKey, 0);
            setInt(YidaChoiceKey, 0);
            setInt(TaoJiRescuedKey, 0);
            return true;
        }

        public static M07RouteCostFlags RequiredCostForPlan(M06RescuePlan plan)
        {
            switch (plan)
            {
                case M06RescuePlan.OfficialHandover:
                    return M07RouteCostFlags.HarborSupplyDebt;
                case M06RescuePlan.CargoCover:
                    return M07RouteCostFlags.FreightCapacityLoss;
                case M06RescuePlan.MinerShutdown:
                    return M07RouteCostFlags.PumpOverhaulShiftCost;
                case M06RescuePlan.ForceBreakout:
                    return M07RouteCostFlags.PierRepairAndInjuryCost;
                default:
                    throw new ArgumentOutOfRangeException(nameof(plan));
            }
        }

        private static bool IsValidRecord(M07ExtractionRecord record, Func<string, int> getInt)
        {
            if (!Enum.IsDefined(typeof(M07ExecutedRoute), record.ExecutedRoute) ||
                !Enum.IsDefined(typeof(M07PumpShutdownState), record.PumpState) ||
                !Enum.IsDefined(typeof(M07RusFate), record.RusFate) ||
                !Enum.IsDefined(typeof(M07LedgerState), record.LedgerState) ||
                !Enum.IsDefined(typeof(M07LiangChoice), record.LiangChoice) ||
                !Enum.IsDefined(typeof(M07YidaChoice), record.YidaChoice))
                return false;

            int rawCosts = (int)record.RouteCosts;
            if (rawCosts <= 0 || (rawCosts & ~AllCostFlagsMask) != 0)
                return false;

            if (record.BoardedFirstBatchCount <= 0 ||
                record.BoardedFirstBatchCount > MaxFirstBatchBoarded)
                return false;

            if (!M06RescuePlanFacts.TryRead(getInt, out M06ReunionRecord? m06) ||
                !m06.HasValue)
                return false;

            M06RescuePlan chosenPlan = m06.Value.Plan;
            M07RouteCostFlags requiredOriginalCost = RequiredCostForPlan(chosenPlan);

            // Executed route must match the M06 plan or be a fallback official handover.
            if (record.ExecutedRoute == M07ExecutedRoute.FallbackOfficialHandover)
            {
                // Fallback preserves both the incurred original-route loss and fallback cost.
                if ((record.RouteCosts & requiredOriginalCost) == 0 ||
                    (record.RouteCosts & M07RouteCostFlags.FallbackTransferCost) == 0)
                    return false;
            }
            else
            {
                if ((int)record.ExecutedRoute != (int)chosenPlan)
                    return false;
                if ((record.RouteCosts & requiredOriginalCost) == 0)
                    return false;
            }

            // Only MinerShutdown performs the 3-hour safe pump shutdown and 500-miner
            // upper-level evacuation; other routes keep the main pump running.
            if (record.ExecutedRoute == M07ExecutedRoute.MinerShutdown)
            {
                if (record.PumpState != M07PumpShutdownState.SafelyStoppedThreeHours ||
                    record.LowerLevelEvacuatedCount != LowerLevelMinerCount)
                    return false;
            }
            else
            {
                if (record.PumpState != M07PumpShutdownState.KeptRunning ||
                    record.LowerLevelEvacuatedCount != 0)
                    return false;
            }

            return true;
        }
    }
}
