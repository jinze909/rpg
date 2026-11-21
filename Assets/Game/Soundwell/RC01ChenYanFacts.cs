using System;
using GamingIsLove.Makinom;

namespace Rpg.Soundwell
{
    [Flags]
    public enum RC01FerryClues
    {
        None = 0,
        WristScar = 1 << 0,
        DoughHumTune = 1 << 1,
        BlankShrineTablet = 1 << 2,
        All = WristScar | DoughHumTune | BlankShrineTablet
    }

    public enum RC01TalkOrder
    {
        LinSuFirst = 1,
        QiuniangFirst = 2
    }

    public enum RC01TalkAtmosphere
    {
        GuardedWithQiuniang = 1,
        CalmWithLinSu = 2
    }

    public enum RC01ChenYanOutcome
    {
        RestoreLivingName = 1,
        KeepMarkedDead = 2,
        IssueNewReturneeName = 3
    }

    /// <summary>
    /// Immutable RC-01 ("砚叔的两个家") roster-case outcome persisted in Makinom.
    /// Enforces SCENE_SCRIPT_RC01.md v1.2:
    /// - Strictly requires M08 first world notice and M07 rescue roster (case 1).
    /// - Requires all 3 ferry clues (wrist scar, dough-kneading tune, blank shrine
    ///   tablet) and interviews with both Lin Su and Qiuniang before settling.
    /// - Records Lin Su's handwritten register note alongside the chosen outcome.
    /// </summary>
    public readonly struct RC01ChenYanRecord
    {
        public RC01ChenYanRecord(
            RC01FerryClues cluesFound,
            RC01TalkOrder talkOrder,
            RC01ChenYanOutcome outcome,
            bool linSuRegisterNoteRecorded)
        {
            CluesFound = cluesFound;
            TalkOrder = talkOrder;
            Outcome = outcome;
            LinSuRegisterNoteRecorded = linSuRegisterNoteRecorded;
        }

        public RC01FerryClues CluesFound { get; }
        public RC01TalkOrder TalkOrder { get; }
        public RC01ChenYanOutcome Outcome { get; }
        public bool LinSuRegisterNoteRecorded { get; }

        public RC01TalkAtmosphere Atmosphere =>
            TalkOrder == RC01TalkOrder.LinSuFirst
                ? RC01TalkAtmosphere.GuardedWithQiuniang
                : RC01TalkAtmosphere.CalmWithLinSu;
    }

    public static class RC01ChenYanFacts
    {
        public const int RosterCaseNumber = 1;
        public const string ConfirmedKey = "rpg.return.rc01.case-confirmed";
        public const string CluesMaskKey = "rpg.return.rc01.clues-mask";
        public const string TalkOrderKey = "rpg.return.rc01.talk-order";
        public const string OutcomeKey = "rpg.return.rc01.chen-yan";
        public const string LinSuNoteKey = "rpg.return.rc01.linsu-register-note";

        public static bool IsValidRecord(RC01ChenYanRecord record, Func<string, int> getInt)
        {
            if (record.CluesFound != RC01FerryClues.All ||
                !Enum.IsDefined(typeof(RC01TalkOrder), record.TalkOrder) ||
                !Enum.IsDefined(typeof(RC01ChenYanOutcome), record.Outcome) ||
                !record.LinSuRegisterNoteRecorded)
                return false;

            if (!M08FirstWorldNoticeFacts.TryCaseAvailable(
                    RosterCaseNumber, getInt, out bool available) ||
                !available)
                return false;

            return true;
        }

        public static bool CanRecord(RC01ChenYanRecord record, Func<string, int> getInt)
        {
            return getInt != null &&
                getInt(ConfirmedKey) == 0 &&
                IsValidRecord(record, getInt);
        }

        public static bool TryRecord(RC01ChenYanRecord record)
        {
            var variables = Maki.Initialized ? Maki.Game?.Variables : null;
            return variables != null && TryRecord(
                record,
                key => variables.GetInt(key),
                (key, value) => variables.Set(key, value));
        }

        public static bool TryRecord(
            RC01ChenYanRecord record,
            Func<string, int> getInt,
            Action<string, int> setInt)
        {
            if (getInt == null || setInt == null || !CanRecord(record, getInt))
                return false;

            setInt(ConfirmedKey, 0);
            setInt(CluesMaskKey, (int)record.CluesFound);
            setInt(TalkOrderKey, (int)record.TalkOrder);
            setInt(OutcomeKey, (int)record.Outcome);
            setInt(LinSuNoteKey, record.LinSuRegisterNoteRecorded ? 1 : 0);
            setInt(ConfirmedKey, 1);
            return true;
        }

        public static bool TryRead(out RC01ChenYanRecord? record)
        {
            record = null;
            var variables = Maki.Initialized ? Maki.Game?.Variables : null;
            return variables != null && TryRead(key => variables.GetInt(key), out record);
        }

        public static bool TryRead(Func<string, int> getInt, out RC01ChenYanRecord? record)
        {
            record = null;
            if (getInt == null)
                return false;
            if (getInt(ConfirmedKey) != 1)
                return true;

            int rawClues = getInt(CluesMaskKey);
            int rawOrder = getInt(TalkOrderKey);
            int rawOutcome = getInt(OutcomeKey);
            int rawNote = getInt(LinSuNoteKey);

            if (rawClues != (int)RC01FerryClues.All ||
                !Enum.IsDefined(typeof(RC01TalkOrder), rawOrder) ||
                !Enum.IsDefined(typeof(RC01ChenYanOutcome), rawOutcome) ||
                rawNote != 1)
                return true;

            var saved = new RC01ChenYanRecord(
                (RC01FerryClues)rawClues,
                (RC01TalkOrder)rawOrder,
                (RC01ChenYanOutcome)rawOutcome,
                linSuRegisterNoteRecorded: true);
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
            setInt(CluesMaskKey, 0);
            setInt(TalkOrderKey, 0);
            setInt(OutcomeKey, 0);
            setInt(LinSuNoteKey, 0);
            return true;
        }
    }
}
