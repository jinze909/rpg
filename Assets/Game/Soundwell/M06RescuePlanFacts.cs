using System;
using GamingIsLove.Makinom;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Mutually exclusive rescue plan decided with Shen Li in M06 ("她没有在原地等你").
    /// M07 executes only the chosen plan's actions and records its actual cost.
    /// </summary>
    public enum M06RescuePlan
    {
        OfficialHandover = 1,
        CargoCover = 2,
        MinerShutdown = 3,
        ForceBreakout = 4
    }

    /// <summary>
    /// Actual family-letter handoff outcome during M06. Never marks letters
    /// delivered if M05 refused or never opened Seran's eight-letter packet.
    /// </summary>
    public enum M06LetterDeliveryOutcome
    {
        NotCarried = 0,
        DeliveredWithPending = 1
    }

    /// <summary>
    /// Verified snapshot of M06 pump-room reunion facts persisted in Makinom.
    /// </summary>
    public readonly struct M06ReunionRecord
    {
        public M06ReunionRecord(
            M06RescuePlan plan,
            bool reportedUsableCapacity,
            M06LetterDeliveryOutcome letterDelivery,
            bool retaliationHistoryHeard)
        {
            Plan = plan;
            ReportedUsableCapacity = reportedUsableCapacity;
            LetterDelivery = letterDelivery;
            RetaliationHistoryHeard = retaliationHistoryHeard;
        }

        public M06RescuePlan Plan { get; }
        public bool ReportedUsableCapacity { get; }
        public M06LetterDeliveryOutcome LetterDelivery { get; }
        public bool RetaliationHistoryHeard { get; }
    }

    /// <summary>
    /// Persists and reads back M06 rescue plan and reunion facts through the
    /// single existing Makinom global variable store. Strictly gated on a
    /// verified M05 landing and consistent with actual M05 landing capacity
    /// (N77) and M05 family-letter carry status (N76).
    /// </summary>
    public static class M06RescuePlanFacts
    {
        public const string ConfirmedKey = "rpg.return.m06.reunion-confirmed";
        public const string PlanKey = "rpg.return.m06.rescue-plan";
        public const string UsableCapacityKey = "rpg.return.m06.reported-usable-capacity";
        public const string LetterDeliveryKey = "rpg.return.m06.letter-delivery";
        public const string RetaliationHeardKey = "rpg.return.m06.retaliation-history-heard";

        public static bool CanRecord(M06ReunionRecord record, Func<string, int> getInt)
        {
            if (getInt == null || getInt(ConfirmedKey) != 0)
                return false;
            return IsValidRecord(record, getInt);
        }

        public static bool TryRecord(M06ReunionRecord record)
        {
            var variables = Maki.Initialized ? Maki.Game?.Variables : null;
            return variables != null && TryRecord(record,
                key => variables.GetInt(key),
                (key, value) => variables.Set(key, value));
        }

        public static bool TryRecord(
            M06ReunionRecord record,
            Func<string, int> getInt,
            Action<string, int> setInt)
        {
            if (getInt == null || setInt == null || !CanRecord(record, getInt))
                return false;

            // Keep confirmation last so an interrupted Makinom write never
            // publishes a half-written rescue plan or blocks retry.
            setInt(ConfirmedKey, 0);
            setInt(PlanKey, (int)record.Plan);
            setInt(UsableCapacityKey, record.ReportedUsableCapacity ? 1 : 0);
            setInt(LetterDeliveryKey, (int)record.LetterDelivery);
            setInt(RetaliationHeardKey, record.RetaliationHistoryHeard ? 1 : 0);
            setInt(ConfirmedKey, 1);
            return true;
        }

        public static bool TryRead(out M06ReunionRecord? record)
        {
            record = null;
            var variables = Maki.Initialized ? Maki.Game?.Variables : null;
            return variables != null && TryRead(
                key => variables.GetInt(key), out record);
        }

        public static bool TryRead(Func<string, int> getInt, out M06ReunionRecord? record)
        {
            record = null;
            if (getInt == null)
                return false;
            if (getInt(ConfirmedKey) != 1)
                return true;

            int rawPlan = getInt(PlanKey);
            int rawUsable = getInt(UsableCapacityKey);
            int rawLetters = getInt(LetterDeliveryKey);
            int rawRetaliation = getInt(RetaliationHeardKey);

            if (!Enum.IsDefined(typeof(M06RescuePlan), rawPlan) ||
                !Enum.IsDefined(typeof(M06LetterDeliveryOutcome), rawLetters) ||
                (rawUsable != 0 && rawUsable != 1) ||
                (rawRetaliation != 0 && rawRetaliation != 1))
                return true;

            var saved = new M06ReunionRecord(
                (M06RescuePlan)rawPlan,
                rawUsable == 1,
                (M06LetterDeliveryOutcome)rawLetters,
                rawRetaliation == 1);
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
            setInt(PlanKey, 0);
            setInt(UsableCapacityKey, 0);
            setInt(LetterDeliveryKey, 0);
            setInt(RetaliationHeardKey, 0);
            return true;
        }

        private static bool IsValidRecord(M06ReunionRecord record, Func<string, int> getInt)
        {
            if (!Enum.IsDefined(typeof(M06RescuePlan), record.Plan) ||
                !Enum.IsDefined(typeof(M06LetterDeliveryOutcome), record.LetterDelivery))
                return false;

            if (!M05LandingFacts.TryRead(getInt, out M05LandingReport? landing) ||
                !landing.HasValue)
                return false;

            // Validation N77: M06 capacity report must match verified M05 landing.
            if (record.ReportedUsableCapacity != landing.Value.HasUsableLandingCapacity)
                return false;

            // Validation N76: letter delivery requires an actually taken M05 packet.
            if (record.LetterDelivery == M06LetterDeliveryOutcome.DeliveredWithPending)
            {
                if (!M05LettersFacts.TryRead(getInt, out M05LetterDecision? decision) ||
                    decision != M05LetterDecision.TakePacket)
                    return false;
            }

            return true;
        }
    }
}
