using System;
using GamingIsLove.Makinom;

namespace Rpg.Soundwell
{
    public enum ST03BathhouseChoice
    {
        ReopenWithSubsidizedHours = 1,
        ElderRunLaundryBackroom = 2,
        SellAndRelocateToCareHome = 3
    }

    public enum ST03M15BathhouseSign
    {
        NoNewSignAtOldSite = 0,
        OriginalBathhouse = 1,
        LaundryBackroom = 2
    }

    public enum ST03WusuWorkplace
    {
        OriginalBathhouseDesk = 1,
        ElderRunBackroom = 2,
        CareHome = 3
    }

    /// <summary>
    /// ST-03 "第三声钟" outcome. Only the completed bathhouse, care-home
    /// interview, safe route, trial reopening and balanced transport ledger
    /// can authorize one of the three permanent business arrangements.
    /// </summary>
    public readonly struct ST03BathhouseRecord
    {
        public ST03BathhouseRecord(ST03BathhouseChoice choice,
            bool safeRouteCleared, bool returnTripRetimed,
            bool trialNightCompleted, bool transportCostBalanced)
        {
            Choice = choice;
            SafeRouteCleared = safeRouteCleared;
            ReturnTripRetimed = returnTripRetimed;
            TrialNightCompleted = trialNightCompleted;
            TransportCostBalanced = transportCostBalanced;
        }

        public ST03BathhouseChoice Choice { get; }
        public bool SafeRouteCleared { get; }
        public bool ReturnTripRetimed { get; }
        public bool TrialNightCompleted { get; }
        public bool TransportCostBalanced { get; }

        // The M15 return visit must NOT paint a new He Di bathhouse sign on
        // the old address after the building has been sold.
        public ST03M15BathhouseSign M15Sign =>
            Choice == ST03BathhouseChoice.ReopenWithSubsidizedHours
                ? ST03M15BathhouseSign.OriginalBathhouse
                : Choice == ST03BathhouseChoice.ElderRunLaundryBackroom
                    ? ST03M15BathhouseSign.LaundryBackroom
                    : ST03M15BathhouseSign.NoNewSignAtOldSite;

        public ST03WusuWorkplace WusuWorkplace =>
            Choice == ST03BathhouseChoice.ReopenWithSubsidizedHours
                ? ST03WusuWorkplace.OriginalBathhouseDesk
                : Choice == ST03BathhouseChoice.ElderRunLaundryBackroom
                    ? ST03WusuWorkplace.ElderRunBackroom
                    : ST03WusuWorkplace.CareHome;
    }

    /// <summary>
    /// Makinom is the sole authority for ST-03 permanent consequences.
    /// Only call TryRecord after a scene has actually gathered the bathhouse
    /// evidence, interviewed Wu Su, completed the safe trial and chosen an
    /// operating model. This facts layer does not create phantom scene visits.
    /// ST-03 is regional and survives M03/M04 outbound voyage replay.
    /// </summary>
    public static class ST03BathhouseFacts
    {
        public const string ConfirmedKey = "rpg.return.st03.bathhouse-confirmed";
        public const string PendingChoiceKey = "rpg.return.st03.pending-choice-lock";
        public const string ChoiceKey = "rpg.return.st03.bathhouse-choice";
        public const string SafeRouteKey = "rpg.return.st03.safe-route-cleared";
        public const string ReturnTripKey = "rpg.return.st03.return-trip-retimed";
        public const string TrialNightKey = "rpg.return.st03.trial-night-completed";
        public const string TransportCostKey = "rpg.return.st03.transport-cost-balanced";

        public static bool HasOrphanedDownstreamFacts(Func<string, int> read)
        {
            return read == null ||
                M15FinalRollCallFacts.HasAnyPersistedFacts(read) ||
                M16EpilogueFacts.HasAnyPersistedFacts(read);
        }

        private static bool Valid(ST03BathhouseRecord record)
        {
            return Enum.IsDefined(typeof(ST03BathhouseChoice), record.Choice) &&
                record.SafeRouteCleared && record.ReturnTripRetimed &&
                record.TrialNightCompleted && record.TransportCostBalanced;
        }

        private static bool CompatiblePending(Func<string, int> read,
            string key, int expected)
        {
            int raw = read(key);
            return raw == 0 || raw == expected;
        }

        public static bool CanRecord(ST03BathhouseRecord record, Func<string, int> read)
        {
            if (read == null || !Valid(record) || read(ConfirmedKey) != 0 ||
                HasOrphanedDownstreamFacts(read))
                return false;
            // A saved intent survives a torn write even if the outcome payload
            // is not yet present. Legacy unsigned partial records still use
            // the original compatible-fields check below.
            int pendingChoice = read(PendingChoiceKey);
            return (pendingChoice == 0 || pendingChoice == (int)record.Choice) &&
                CompatiblePending(read, ChoiceKey, (int)record.Choice) &&
                CompatiblePending(read, SafeRouteKey, 1) &&
                CompatiblePending(read, ReturnTripKey, 1) &&
                CompatiblePending(read, TrialNightKey, 1) &&
                CompatiblePending(read, TransportCostKey, 1);
        }

        public static bool TryRecord(ST03BathhouseRecord record,
            Func<string, int> read, Action<string, int> write)
        {
            if (write == null || !CanRecord(record, read))
                return false;

            // Lock the first permanent decision before writing the payload.
            // A torn write can only resume the identical original choice.
            write(PendingChoiceKey, (int)record.Choice);
            write(ChoiceKey, (int)record.Choice);
            write(SafeRouteKey, 1);
            write(ReturnTripKey, 1);
            write(TrialNightKey, 1);
            write(TransportCostKey, 1);
            write(ConfirmedKey, 1);
            return true;
        }

        public static bool TryRead(out ST03BathhouseRecord? record)
        {
            record = null;
            var variables = Maki.Initialized ? Maki.Game?.Variables : null;
            return variables != null && TryRead(
                key => variables.GetInt(key), out record);
        }

        public static bool TryRead(Func<string, int> read,
            out ST03BathhouseRecord? record)
        {
            record = null;
            if (read == null) return false;
            int confirmed = read(ConfirmedKey);
            if (confirmed != 0 && confirmed != 1)
                return false;

            int rawChoice = read(ChoiceKey);
            int rawPendingChoice = read(PendingChoiceKey);
            int rawRoute = read(SafeRouteKey);
            int rawTrip = read(ReturnTripKey);
            int rawTrial = read(TrialNightKey);
            int rawCost = read(TransportCostKey);

            if (confirmed == 0)
            {
                // An interrupted commit is not yet an actual business result.
                return rawPendingChoice == 0 && rawChoice == 0 &&
                    rawRoute == 0 && rawTrip == 0 && rawTrial == 0 &&
                    rawCost == 0;
            }

            // Existing confirmed saves have no intent key and remain valid.
            // Newly saved decisions cannot silently turn into another valid
            // permanent outcome after an interrupted/corrupted save.
            if ((rawPendingChoice != 0 && rawPendingChoice != rawChoice) ||
                !Enum.IsDefined(typeof(ST03BathhouseChoice), rawChoice) ||
                rawRoute != 1 || rawTrip != 1 ||
                rawTrial != 1 || rawCost != 1)
                return false;

            record = new ST03BathhouseRecord(
                (ST03BathhouseChoice)rawChoice,
                safeRouteCleared: true,
                returnTripRetimed: true,
                trialNightCompleted: true,
                transportCostBalanced: true);
            return true;
        }

        /// <summary>
        /// Only used when starting an actually new game, not a new sea voyage.
        /// </summary>
        public static bool TryResetForNewGame(Action<string, int> write)
        {
            if (write == null) return false;
            write(ConfirmedKey, 0);
            write(PendingChoiceKey, 0);
            write(ChoiceKey, 0);
            write(SafeRouteKey, 0);
            write(ReturnTripKey, 0);
            write(TrialNightKey, 0);
            write(TransportCostKey, 0);
            return true;
        }
    }
}
