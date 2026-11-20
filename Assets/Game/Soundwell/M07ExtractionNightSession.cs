using System;

namespace Rpg.Soundwell
{
    public enum M07ExtractionStage
    {
        RouteExecution = 0,
        OulinLockAndYidaGate = 1,
        RusLedgerConfrontation = 2,
        ReturnLettersCheck = 3,
        DockRollCall = 4,
        Completed = 5
    }

    /// <summary>
    /// Authoritative M07 ("把还活着的人带出来") extraction-night and pre-dawn
    /// dock roll-call state machine. Executes only the chosen M06 rescue plan
    /// (or pivots to FallbackOfficialHandover with stacked costs), restricts
    /// the 3-hour pump shutdown & "对不住了，老张" strictly to MinerShutdown,
    /// gates return letters on actually delivered M06 letters, and persists
    /// the final M07ExtractionRecord with retry safety.
    /// </summary>
    public sealed class M07ExtractionNightSession
    {
        public M06ReunionRecord M06Record { get; }
        public M07ExtractionStage Stage { get; private set; }
        public M07ExecutedRoute? ExecutedRoute { get; private set; }
        public M07PumpShutdownState PumpState { get; private set; }
        public M07RouteCostFlags RouteCosts { get; private set; }
        public int BoardedFirstBatchCount { get; private set; }
        public int LowerLevelEvacuatedCount { get; private set; }
        public bool OldZhangFarewellSpoken { get; private set; }
        public bool TunnelGateOpened { get; private set; }
        public M07RusFate? RusFate { get; private set; }
        public M07LedgerState? LedgerState { get; private set; }
        public bool AccountabilityPreserved => LedgerState.HasValue;
        public bool HasReturnLetters =>
            M06Record.LetterDelivery == M06LetterDeliveryOutcome.DeliveredWithPending;
        public bool ReturnLettersCollected { get; private set; }
        public bool XiaoqiAnsweredByNumberSeventyTwo { get; private set; }
        public M07ExtractionRecord? CompletedRecord { get; private set; }

        public M07ExtractionNightSession(M06ReunionRecord m06Record)
        {
            if (!Enum.IsDefined(typeof(M06RescuePlan), m06Record.Plan))
                throw new ArgumentOutOfRangeException(nameof(m06Record));
            M06Record = m06Record;
            Stage = M07ExtractionStage.RouteExecution;
        }

        public static bool TryCreateFromSavedFacts(
            Func<string, int> getInt,
            out M07ExtractionNightSession session)
        {
            session = null;
            if (getInt == null)
                return false;
            if (!M06RescuePlanFacts.TryRead(getInt, out M06ReunionRecord? m06) ||
                !m06.HasValue)
                return false;

            session = new M07ExtractionNightSession(m06.Value);
            return true;
        }

        public void ExecuteRoute(bool blockedInField, int boardedFirstBatchCount)
        {
            EnsureStage(M07ExtractionStage.RouteExecution);
            if (boardedFirstBatchCount <= 0 ||
                boardedFirstBatchCount > M07ExtractionFacts.MaxFirstBatchBoarded)
                throw new ArgumentOutOfRangeException(nameof(boardedFirstBatchCount),
                    "First-batch off-island boarding must be between 1 and 300.");

            M07RouteCostFlags baseCost = M07ExtractionFacts.RequiredCostForPlan(M06Record.Plan);
            if (blockedInField)
            {
                ExecutedRoute = M07ExecutedRoute.FallbackOfficialHandover;
                PumpState = M07PumpShutdownState.KeptRunning;
                LowerLevelEvacuatedCount = 0;
                OldZhangFarewellSpoken = false;
                RouteCosts = baseCost | M07RouteCostFlags.FallbackTransferCost;
            }
            else
            {
                ExecutedRoute = (M07ExecutedRoute)(int)M06Record.Plan;
                bool isShutdown = M06Record.Plan == M06RescuePlan.MinerShutdown;
                PumpState = isShutdown
                    ? M07PumpShutdownState.SafelyStoppedThreeHours
                    : M07PumpShutdownState.KeptRunning;
                LowerLevelEvacuatedCount = isShutdown
                    ? M07ExtractionFacts.LowerLevelMinerCount
                    : 0;
                OldZhangFarewellSpoken = isShutdown;
                RouteCosts = baseCost;
            }

            BoardedFirstBatchCount = boardedFirstBatchCount;
            Stage = M07ExtractionStage.OulinLockAndYidaGate;
        }

        public void AdvanceTunnelOpening()
        {
            EnsureStage(M07ExtractionStage.OulinLockAndYidaGate);
            TunnelGateOpened = true;
            Stage = M07ExtractionStage.RusLedgerConfrontation;
        }

        public void ResolveRusConfrontation(M07RusFate rusFate, M07LedgerState ledgerState)
        {
            EnsureStage(M07ExtractionStage.RusLedgerConfrontation);
            if (!Enum.IsDefined(typeof(M07RusFate), rusFate))
                throw new ArgumentOutOfRangeException(nameof(rusFate));
            if (!Enum.IsDefined(typeof(M07LedgerState), ledgerState))
                throw new ArgumentOutOfRangeException(nameof(ledgerState));

            RusFate = rusFate;
            LedgerState = ledgerState;
            Stage = M07ExtractionStage.ReturnLettersCheck;
        }

        public void AdvanceReturnLettersCheck()
        {
            EnsureStage(M07ExtractionStage.ReturnLettersCheck);
            ReturnLettersCollected = HasReturnLetters;
            Stage = M07ExtractionStage.DockRollCall;
        }

        public string FormatRollCallRosterSource(bool zh)
        {
            bool burned = LedgerState == M07LedgerState.PartiallyBurned;
            if (burned)
            {
                return zh
                    ? "伊蕾监察副本与矿工抄录的核验名单"
                    : "Yi Lei's inspection copy and the miners' transcribed verification roster";
            }
            return zh
                ? "从账房保下的转运名册原件"
                : "the preserved transfer roster from Rus's ledger office";
        }

        public bool TryCompleteDockRollCall(
            M07LiangChoice liangChoice,
            M07YidaChoice yidaChoice,
            bool taoJiRescued,
            Func<string, int> getInt,
            Action<string, int> setInt)
        {
            EnsureStage(M07ExtractionStage.DockRollCall);
            if (!Enum.IsDefined(typeof(M07LiangChoice), liangChoice))
                throw new ArgumentOutOfRangeException(nameof(liangChoice));
            if (!Enum.IsDefined(typeof(M07YidaChoice), yidaChoice))
                throw new ArgumentOutOfRangeException(nameof(yidaChoice));

            var record = new M07ExtractionRecord(
                ExecutedRoute.Value,
                PumpState,
                RouteCosts,
                BoardedFirstBatchCount,
                LowerLevelEvacuatedCount,
                RusFate.Value,
                LedgerState.Value,
                liangChoice,
                yidaChoice,
                taoJiRescued);

            if (!M07ExtractionFacts.TryRecord(record, getInt, setInt))
                return false;

            XiaoqiAnsweredByNumberSeventyTwo = true;
            CompletedRecord = record;
            Stage = M07ExtractionStage.Completed;
            return true;
        }

        private void EnsureStage(M07ExtractionStage expected)
        {
            if (Stage != expected)
                throw new InvalidOperationException(
                    $"M07 extraction expected stage {expected}, but is currently in {Stage}.");
        }
    }
}
