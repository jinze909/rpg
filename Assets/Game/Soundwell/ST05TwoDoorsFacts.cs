using System;
using GamingIsLove.Makinom;

namespace Rpg.Soundwell
{
    public enum ST05BedResolution
    {
        RemoveDoorFrame = 1,
        ConvertBedToCourtyardBenches = 2,
        ReturnBedAndPayHaulage = 3
    }

    public enum ST05HomeArrangement
    {
        KeepTwoDoorsAndCourtyardTable = 1,
        MutuallyAgreedTrialSharedHomeAndStudio = 2
    }

    public enum ST05ChengAssignment
    {
        StayLocal = 1,
        AcceptOutsideSurvey = 2
    }

    public enum ST05LetterStatus
    {
        NotSent = 0,
        Sent = 1,
        Delivered = 2
    }

    /// <summary>
    /// Permanent ST-05 living agreement. The two characters make their own
    /// decisions: a player never votes for them, and a trial shared home is
    /// valid only with explicit consent from both Yu He and Cheng Wu.
    /// </summary>
    public readonly struct ST05HouseholdRecord
    {
        public ST05HouseholdRecord(ST05BedResolution bed,
            ST05HomeArrangement arrangement, bool mutualTrialConsent)
        {
            Bed = bed;
            Arrangement = arrangement;
            MutualTrialConsent = mutualTrialConsent;
        }

        public ST05BedResolution Bed { get; }
        public ST05HomeArrangement Arrangement { get; }
        public bool MutualTrialConsent { get; }
        public bool KeepsSeparateDoors =>
            Arrangement == ST05HomeArrangement.KeepTwoDoorsAndCourtyardTable;
    }

    /// <summary>
    /// ST-05 Makinom-backed outcome. Remote employment and letters are separate
    /// events, not automatically granted when an offer is accepted. At M15
    /// names, bread, maps and absent-worker correspondence reflect real
    /// arrivals and deliveries, not seating-chart expectations.
    /// </summary>
    public readonly struct ST05TwoDoorsOutcome
    {
        public ST05TwoDoorsOutcome(ST05HouseholdRecord home,
            ST05ChengAssignment? assignment, bool outsideWorkStarted,
            ST05LetterStatus letter, bool yuheArrived, bool breadBrought,
            bool chengArrived, bool mapBrought)
        {
            Home = home;
            Assignment = assignment;
            OutsideWorkStarted = outsideWorkStarted;
            Letter = letter;
            YuheArrived = yuheArrived;
            BreadBrought = breadBrought;
            ChengArrived = chengArrived;
            MapBrought = mapBrought;
        }

        public ST05HouseholdRecord Home { get; }
        public ST05ChengAssignment? Assignment { get; }
        public bool OutsideWorkStarted { get; }
        public ST05LetterStatus Letter { get; }
        public bool YuheArrived { get; }
        public bool BreadBrought { get; }
        public bool ChengArrived { get; }
        public bool MapBrought { get; }

        public bool ShowM15Bread => YuheArrived && BreadBrought;
        public bool ShowM15HandedMap => ChengArrived && MapBrought;
        public bool ShowM15LetterInsteadOfCheng =>
            !ChengArrived && OutsideWorkStarted &&
            Assignment == ST05ChengAssignment.AcceptOutsideSurvey &&
            Letter == ST05LetterStatus.Delivered;
        public bool SeatYuheAtM15 => YuheArrived;
        public bool SeatChengAtM15 => ChengArrived;
    }

    /// <summary>
    /// Makinom-owned ST-05 facts. Living arrangements, Cheng's own assignment
    /// decision, actually starting work, actual correspondence and M15
    /// arrivals are different events. No ordinary voyage replay resets them.
    /// </summary>
    public static class ST05TwoDoorsFacts
    {
        public const string HomeConfirmedKey = "rpg.return.st05.home-confirmed";
        public const string HomeIntentKey = "rpg.return.st05.home-intent";
        public const string BedKey = "rpg.return.st05.bed-resolution";
        public const string HomeArrangementKey = "rpg.return.st05.home-arrangement";
        public const string MutualTrialConsentKey = "rpg.return.st05.mutual-trial-consent";
        public const string AssignmentConfirmedKey = "rpg.return.st05.assignment-confirmed";
        public const string AssignmentKey = "rpg.return.st05.cheng-assignment";
        public const string OutsideWorkStartedKey = "rpg.return.st05.outside-work-started";
        public const string LetterStatusKey = "rpg.return.st05.letter-status";
        public const string YuheArrivedKey = "rpg.return.st05.m15-yuhe-arrived";
        public const string BreadBroughtKey = "rpg.return.st05.m15-bread-brought";
        public const string ChengArrivedKey = "rpg.return.st05.m15-cheng-arrived";
        public const string MapBroughtKey = "rpg.return.st05.m15-map-brought";
        public const string YuheArrivalIntentKey = "rpg.return.st05.m15-yuhe-arrival-intent";
        public const string ChengArrivalIntentKey = "rpg.return.st05.m15-cheng-arrival-intent";

        private static int Signature(ST05HouseholdRecord record)
        {
            return 1000 + (int)record.Bed * 100 +
                (int)record.Arrangement * 10 + (record.MutualTrialConsent ? 1 : 0);
        }

        private static bool Valid(ST05HouseholdRecord record)
        {
            return Enum.IsDefined(typeof(ST05BedResolution), record.Bed) &&
                Enum.IsDefined(typeof(ST05HomeArrangement), record.Arrangement) &&
                record.MutualTrialConsent ==
                    (record.Arrangement == ST05HomeArrangement.MutuallyAgreedTrialSharedHomeAndStudio);
        }

        private static bool EmptyFurtherFacts(Func<string, int> read)
        {
            return read(AssignmentConfirmedKey) == 0 &&
                read(AssignmentKey) == 0 &&
                read(OutsideWorkStartedKey) == 0 &&
                read(LetterStatusKey) == 0 &&
                read(YuheArrivedKey) == 0 &&
                read(BreadBroughtKey) == 0 &&
                read(ChengArrivedKey) == 0 &&
                read(MapBroughtKey) == 0 &&
                read(YuheArrivalIntentKey) == 0 &&
                read(ChengArrivalIntentKey) == 0;
        }

        public static bool CanRecordHome(ST05HouseholdRecord record, Func<string, int> read)
        {
            if (read == null || !Valid(record) || read(HomeConfirmedKey) != 0 ||
                !EmptyFurtherFacts(read))
                return false;
            int intent = read(HomeIntentKey);
            if (intent == 0)
                return read(BedKey) == 0 && read(HomeArrangementKey) == 0 &&
                    read(MutualTrialConsentKey) == 0;
            return intent == Signature(record) &&
                (read(BedKey) == 0 || read(BedKey) == (int)record.Bed) &&
                (read(HomeArrangementKey) == 0 ||
                    read(HomeArrangementKey) == (int)record.Arrangement) &&
                (read(MutualTrialConsentKey) == 0 ||
                    read(MutualTrialConsentKey) == (record.MutualTrialConsent ? 1 : 0));
        }

        public static bool TryRecordHome(ST05HouseholdRecord record,
            Func<string, int> read, Action<string, int> write)
        {
            if (write == null || !CanRecordHome(record, read))
                return false;
            write(HomeIntentKey, Signature(record));
            write(BedKey, (int)record.Bed);
            write(HomeArrangementKey, (int)record.Arrangement);
            write(MutualTrialConsentKey, record.MutualTrialConsent ? 1 : 0);
            write(HomeConfirmedKey, 1);
            return true;
        }

        private static bool TryReadHome(Func<string, int> read,
            out ST05HouseholdRecord? home)
        {
            home = null;
            if (read == null)
                return false;
            int confirmed = read(HomeConfirmedKey);
            if (confirmed == 0)
                return read(HomeIntentKey) == 0 && read(BedKey) == 0 &&
                    read(HomeArrangementKey) == 0 && read(MutualTrialConsentKey) == 0;
            if (confirmed != 1)
                return false;
            int consent = read(MutualTrialConsentKey);
            if (consent != 0 && consent != 1)
                return false;
            var value = new ST05HouseholdRecord(
                (ST05BedResolution)read(BedKey),
                (ST05HomeArrangement)read(HomeArrangementKey),
                consent == 1);
            if (!Valid(value) || read(HomeIntentKey) != Signature(value))
                return false;
            home = value;
            return true;
        }

        public static bool TryRecordAssignment(ST05ChengAssignment assignment,
            Func<string, int> read, Action<string, int> write)
        {
            if (read == null || write == null ||
                !Enum.IsDefined(typeof(ST05ChengAssignment), assignment) ||
                !TryReadHome(read, out ST05HouseholdRecord? home) || !home.HasValue)
                return false;

            int confirmed = read(AssignmentConfirmedKey);
            int oldChoice = read(AssignmentKey);
            if (confirmed == 1)
                return oldChoice == (int)assignment && TryRead(read, out _);
            if (confirmed != 0 || (oldChoice != 0 && oldChoice != (int)assignment) ||
                read(OutsideWorkStartedKey) != 0 || read(LetterStatusKey) != 0 ||
                read(YuheArrivedKey) != 0 || read(BreadBroughtKey) != 0 ||
                read(ChengArrivedKey) != 0 || read(MapBroughtKey) != 0 ||
                read(YuheArrivalIntentKey) != 0 ||
                read(ChengArrivalIntentKey) != 0)
                return false;

            // A partially written assignment may be replayed only with the
            // original decision personally communicated by Cheng Wu.
            write(AssignmentKey, (int)assignment);
            write(AssignmentConfirmedKey, 1);
            return true;
        }

        public static bool TryRead(out ST05TwoDoorsOutcome? outcome)
        {
            outcome = null;
            var variables = Maki.Initialized ? Maki.Game?.Variables : null;
            return variables != null && TryRead(
                key => variables.GetInt(key), out outcome);
        }

        public static bool TryRead(Func<string, int> read,
            out ST05TwoDoorsOutcome? outcome)
        {
            outcome = null;
            if (!TryReadHome(read, out ST05HouseholdRecord? home))
                return false;
            if (!home.HasValue)
                return EmptyFurtherFacts(read);

            int assignmentConfirmed = read(AssignmentConfirmedKey);
            int assignmentRaw = read(AssignmentKey);
            if (assignmentConfirmed != 0 && assignmentConfirmed != 1)
                return false;
            if ((assignmentConfirmed == 0 && assignmentRaw != 0) ||
                (assignmentConfirmed == 1 &&
                 !Enum.IsDefined(typeof(ST05ChengAssignment), assignmentRaw)))
                return false;

            int rawWork = read(OutsideWorkStartedKey);
            int rawLetter = read(LetterStatusKey);
            int rawYuhe = read(YuheArrivedKey);
            int rawBread = read(BreadBroughtKey);
            int rawCheng = read(ChengArrivedKey);
            int rawMap = read(MapBroughtKey);
            int rawYuheIntent = read(YuheArrivalIntentKey);
            int rawChengIntent = read(ChengArrivalIntentKey);
            if ((rawWork != 0 && rawWork != 1) ||
                !Enum.IsDefined(typeof(ST05LetterStatus), rawLetter) ||
                (rawYuhe != 0 && rawYuhe != 1) ||
                (rawBread != 0 && rawBread != 1) ||
                (rawCheng != 0 && rawCheng != 1) ||
                (rawMap != 0 && rawMap != 1) ||
                (rawYuheIntent != 0 && rawYuheIntent != 1 && rawYuheIntent != 2) ||
                (rawChengIntent != 0 && rawChengIntent != 1 && rawChengIntent != 2) ||
                (rawBread == 1 && rawYuhe == 0) ||
                (rawMap == 1 && rawCheng == 0) ||
                (rawYuhe == 0 && rawYuheIntent != 0) ||
                (rawCheng == 0 && rawChengIntent != 0) ||
                (rawYuhe == 1 && rawYuheIntent != rawBread + 1) ||
                (rawCheng == 1 && rawChengIntent != rawMap + 1))
                return false;

            ST05ChengAssignment? assignment = assignmentConfirmed == 1
                ? (ST05ChengAssignment?)assignmentRaw : null;
            if ((!assignment.HasValue &&
                 (rawWork != 0 || rawLetter != 0 ||
                  rawYuhe != 0 || rawBread != 0 || rawCheng != 0 || rawMap != 0 ||
                  rawYuheIntent != 0 || rawChengIntent != 0)) ||
                (rawWork == 1 && assignment != ST05ChengAssignment.AcceptOutsideSurvey) ||
                (rawLetter != 0 &&
                 (rawWork != 1 || assignment != ST05ChengAssignment.AcceptOutsideSurvey)))
                return false;

            outcome = new ST05TwoDoorsOutcome(home.Value, assignment,
                rawWork == 1, (ST05LetterStatus)rawLetter,
                rawYuhe == 1, rawBread == 1, rawCheng == 1, rawMap == 1);
            return true;
        }

        public static bool TryStartOutsideWork(Func<string, int> read,
            Action<string, int> write)
        {
            if (write == null || !TryRead(read, out ST05TwoDoorsOutcome? saved) ||
                !saved.HasValue ||
                saved.Value.Assignment != ST05ChengAssignment.AcceptOutsideSurvey)
                return false;
            if (saved.Value.OutsideWorkStarted)
                return true;
            // Invoke only after Cheng has actually departed and begun the job.
            write(OutsideWorkStartedKey, 1);
            return true;
        }

        public static bool TryMarkLetterSent(Func<string, int> read,
            Action<string, int> write)
        {
            if (write == null || !TryRead(read, out ST05TwoDoorsOutcome? saved) ||
                !saved.HasValue || !saved.Value.OutsideWorkStarted)
                return false;
            if (saved.Value.Letter != ST05LetterStatus.NotSent)
                return true;
            write(LetterStatusKey, (int)ST05LetterStatus.Sent);
            return true;
        }

        public static bool TryMarkLetterDelivered(Func<string, int> read,
            Action<string, int> write)
        {
            if (write == null || !TryRead(read, out ST05TwoDoorsOutcome? saved) ||
                !saved.HasValue || saved.Value.Letter == ST05LetterStatus.NotSent)
                return false;
            if (saved.Value.Letter == ST05LetterStatus.Delivered)
                return true;
            write(LetterStatusKey, (int)ST05LetterStatus.Delivered);
            return true;
        }

        /// <summary>
        /// M15 guest arrival is a three-key commit: the actual gift choice
        /// intent is written first, gift status second, attendee last.
        /// Incomplete public records are fail-closed, but the same event may
        /// resume without inventing or swapping the guest's gift.
        /// </summary>
        private static bool TryMarkM15Arrival(bool itemActuallyBrought,
            string intentKey, string itemKey, string arrivedKey,
            Func<string, int> read, Action<string, int> write)
        {
            if (read == null || write == null)
                return false;
            int intended = itemActuallyBrought ? 2 : 1;
            int item = itemActuallyBrought ? 1 : 0;

            if (TryRead(read, out ST05TwoDoorsOutcome? saved) &&
                saved.HasValue && saved.Value.Assignment.HasValue)
            {
                if (read(arrivedKey) == 1)
                    return read(intentKey) == intended && read(itemKey) == item;
                if (read(intentKey) != 0 || read(itemKey) != 0)
                    return false;
            }
            else
            {
                int pendingItem = read(itemKey);
                if (read(arrivedKey) != 0 || read(intentKey) != intended ||
                    (pendingItem != 0 && pendingItem != item))
                    return false;

                // Project precisely this guest's incomplete event back to
                // clean pre-arrival state. Every unrelated fact, including
                // the other guest's arrival, must still validate normally.
                if (!TryRead(key =>
                        key == intentKey || key == itemKey ? 0 : read(key),
                        out saved) ||
                    !saved.HasValue || !saved.Value.Assignment.HasValue)
                    return false;
            }

            write(intentKey, intended);
            write(itemKey, item);
            write(arrivedKey, 1);
            return true;
        }

        public static bool TryMarkYuheM15Arrival(bool actuallyBroughtBread,
            Func<string, int> read, Action<string, int> write)
        {
            return TryMarkM15Arrival(actuallyBroughtBread,
                YuheArrivalIntentKey, BreadBroughtKey, YuheArrivedKey, read, write);
        }

        public static bool TryMarkChengM15Arrival(bool actuallyBroughtMap,
            Func<string, int> read, Action<string, int> write)
        {
            return TryMarkM15Arrival(actuallyBroughtMap,
                ChengArrivalIntentKey, MapBroughtKey, ChengArrivedKey, read, write);
        }

        public static bool TryResetForNewGame(Action<string, int> write)
        {
            if (write == null) return false;
            write(HomeConfirmedKey, 0);
            write(AssignmentConfirmedKey, 0);
            write(HomeIntentKey, 0);
            write(BedKey, 0);
            write(HomeArrangementKey, 0);
            write(MutualTrialConsentKey, 0);
            write(AssignmentKey, 0);
            write(OutsideWorkStartedKey, 0);
            write(LetterStatusKey, 0);
            write(YuheArrivedKey, 0);
            write(BreadBroughtKey, 0);
            write(ChengArrivedKey, 0);
            write(MapBroughtKey, 0);
            write(YuheArrivalIntentKey, 0);
            write(ChengArrivalIntentKey, 0);
            return true;
        }
    }
}
