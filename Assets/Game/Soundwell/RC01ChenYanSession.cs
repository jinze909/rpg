using System;

namespace Rpg.Soundwell
{
    public enum RC01InvestigationStage
    {
        FerryClues = 0,
        IdentityConfrontation = 1,
        TwoHomesTalk = 2,
        RosterDecision = 3,
        Completed = 4
    }

    /// <summary>
    /// Authoritative RC-01 ("砚叔的两个家") investigation and decision state machine.
    /// Requires all 3 ferry clues before Chen Yan admits his identity, tracks
    /// talk order between Lin Su and Qiuniang (shaping conversation atmosphere),
    /// requires hearing both households before settling the roster entry, and
    /// supports retry-safe + commit-then-throw idempotent Makinom persistence.
    /// </summary>
    public sealed class RC01ChenYanSession
    {
        public RC01InvestigationStage Stage { get; private set; } =
            RC01InvestigationStage.FerryClues;
        public RC01FerryClues CluesFound { get; private set; } =
            RC01FerryClues.None;
        public bool IdentityAdmitted { get; private set; }
        public bool LinSuTalked { get; private set; }
        public bool QiuniangTalked { get; private set; }
        public RC01TalkOrder? TalkOrder { get; private set; }
        public RC01TalkAtmosphere? Atmosphere =>
            TalkOrder.HasValue
                ? (TalkOrder.Value == RC01TalkOrder.LinSuFirst
                    ? RC01TalkAtmosphere.GuardedWithQiuniang
                    : RC01TalkAtmosphere.CalmWithLinSu)
                : (RC01TalkAtmosphere?)null;
        public RC01ChenYanRecord? CompletedRecord { get; private set; }
        public bool IsResolved => Stage == RC01InvestigationStage.Completed;

        public static bool TryCreateFromSavedFacts(
            Func<string, int> getInt,
            out RC01ChenYanSession session)
        {
            session = null;
            if (getInt == null)
                return false;
            if (!M08FirstWorldNoticeFacts.TryCaseAvailable(
                    RC01ChenYanFacts.RosterCaseNumber, getInt, out bool available) ||
                !available)
                return false;

            session = new RC01ChenYanSession();
            return true;
        }

        public bool TryObserveClue(RC01FerryClues clue)
        {
            if (Stage != RC01InvestigationStage.FerryClues)
                return false;
            if (clue != RC01FerryClues.WristScar &&
                clue != RC01FerryClues.DoughHumTune &&
                clue != RC01FerryClues.BlankShrineTablet)
                return false;
            if ((CluesFound & clue) != 0)
                return false;

            CluesFound |= clue;
            if (CluesFound == RC01FerryClues.All)
                Stage = RC01InvestigationStage.IdentityConfrontation;
            return true;
        }

        public bool TryConfrontAtFerry()
        {
            if (Stage != RC01InvestigationStage.IdentityConfrontation ||
                CluesFound != RC01FerryClues.All)
                return false;

            IdentityAdmitted = true;
            Stage = RC01InvestigationStage.TwoHomesTalk;
            return true;
        }

        public bool TryTalkToLinSu()
        {
            if (Stage != RC01InvestigationStage.TwoHomesTalk || LinSuTalked)
                return false;

            if (!TalkOrder.HasValue)
                TalkOrder = RC01TalkOrder.LinSuFirst;
            LinSuTalked = true;
            if (LinSuTalked && QiuniangTalked)
                Stage = RC01InvestigationStage.RosterDecision;
            return true;
        }

        public bool TryTalkToQiuniang()
        {
            if (Stage != RC01InvestigationStage.TwoHomesTalk || QiuniangTalked)
                return false;

            if (!TalkOrder.HasValue)
                TalkOrder = RC01TalkOrder.QiuniangFirst;
            QiuniangTalked = true;
            if (LinSuTalked && QiuniangTalked)
                Stage = RC01InvestigationStage.RosterDecision;
            return true;
        }

        public bool TrySettleRosterEntry(
            RC01ChenYanOutcome outcome,
            Func<string, int> getInt,
            Action<string, int> setInt)
        {
            if (Stage != RC01InvestigationStage.RosterDecision ||
                !TalkOrder.HasValue ||
                !Enum.IsDefined(typeof(RC01ChenYanOutcome), outcome) ||
                getInt == null || setInt == null)
                return false;

            var record = new RC01ChenYanRecord(
                CluesFound,
                TalkOrder.Value,
                outcome,
                linSuRegisterNoteRecorded: true);

            if (!RC01ChenYanFacts.TryRead(getInt, out RC01ChenYanRecord? existing))
                return false;

            // Recover idempotently if a prior write committed ConfirmedKey = 1
            // before its acknowledgement was lost.
            if (existing.HasValue)
            {
                if (!Matches(record, existing.Value))
                    return false;
            }
            else if (getInt(RC01ChenYanFacts.ConfirmedKey) != 0 ||
                     !RC01ChenYanFacts.TryRecord(record, getInt, setInt))
            {
                return false;
            }

            if (!RC01ChenYanFacts.TryRead(getInt, out RC01ChenYanRecord? saved) ||
                !saved.HasValue || !Matches(record, saved.Value))
                return false;

            CompletedRecord = saved.Value;
            Stage = RC01InvestigationStage.Completed;
            return true;
        }

        private static bool Matches(RC01ChenYanRecord expected, RC01ChenYanRecord saved)
        {
            return expected.CluesFound == saved.CluesFound &&
                expected.TalkOrder == saved.TalkOrder &&
                expected.Outcome == saved.Outcome &&
                expected.LinSuRegisterNoteRecorded == saved.LinSuRegisterNoteRecorded;
        }
    }
}
