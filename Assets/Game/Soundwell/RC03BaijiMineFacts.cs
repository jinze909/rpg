using System;
using GamingIsLove.Makinom;

namespace Rpg.Soundwell
{
    [Flags]
    public enum RC03IslandVisits
    {
        None = 0,
        CanteenAndNightSchool = 1 << 0,
        PumpRoomTwoAndLanRuo = 1 << 1,
        MutualAidCharterAndTalks = 1 << 2,
        All = CanteenAndNightSchool | PumpRoomTwoAndLanRuo | MutualAidCharterAndTalks
    }

    public enum RC03Resolution
    {
        AmendCharterWithSuccessors = 1,
        SecretlyEvacuateLanRuo = 2,
        StayOneYearByConsent = 3
    }

    /// <summary>
    /// Immutable RC-03 ("白矶岛的多数") roster-case outcome persisted in Makinom.
    /// Enforces SCENE_SCRIPT_RC03.md v1.2:
    /// - Strictly gated on M08FirstWorldNoticeFacts case-3 availability.
    /// - AmendCharterWithSuccessors requires both verified pump diagram and
    ///   actual secured successor training (never fakes absent companions).
    /// - StayOneYearByConsent requires Lan Ruo's explicit personal consent.
    /// - SecretlyEvacuateLanRuo queues only Lan Ruo for next-batch crossing
    ///   (never claims instant arrival in Linchao) and records water disruption
    ///   and broken trust with Chairman Luo.
    /// </summary>
    public readonly struct RC03BaijiMineRecord
    {
        public RC03BaijiMineRecord(
            RC03IslandVisits visitsCompleted,
            RC03Resolution resolution,
            bool pumpDiagramVerified,
            bool successorTrainingSecured,
            bool lanRuoConsentedToStay,
            bool chairmanLuoRegisterNoteRecorded)
        {
            VisitsCompleted = visitsCompleted;
            Resolution = resolution;
            PumpDiagramVerified = pumpDiagramVerified;
            SuccessorTrainingSecured = successorTrainingSecured;
            LanRuoConsentedToStay = lanRuoConsentedToStay;
            ChairmanLuoRegisterNoteRecorded = chairmanLuoRegisterNoteRecorded;
        }

        public RC03IslandVisits VisitsCompleted { get; }
        public RC03Resolution Resolution { get; }
        public bool PumpDiagramVerified { get; }
        public bool SuccessorTrainingSecured { get; }
        public bool LanRuoConsentedToStay { get; }
        public bool ChairmanLuoRegisterNoteRecorded { get; }

        public bool LanRuoQueuedForNextBatch =>
            Resolution == RC03Resolution.AmendCharterWithSuccessors ||
            Resolution == RC03Resolution.SecretlyEvacuateLanRuo;

        public bool IslandWaterDisrupted =>
            Resolution == RC03Resolution.SecretlyEvacuateLanRuo;

        public bool ChairmanLuoTrustBroken =>
            Resolution == RC03Resolution.SecretlyEvacuateLanRuo;
    }

    public static class RC03BaijiMineFacts
    {
        public const int RosterCaseNumber = 3;
        public const string ConfirmedKey = "rpg.return.rc03.case-confirmed";
        public const string VisitsMaskKey = "rpg.return.rc03.island-visits-mask";
        public const string ResolutionKey = "rpg.return.rc03.baiji";
        public const string PumpDiagramKey = "rpg.return.rc03.pump-diagram-verified";
        public const string SuccessorTrainingKey = "rpg.return.rc03.successor-training-secured";
        public const string LanRuoStayConsentKey = "rpg.return.rc03.lanruo-stay-consent";
        public const string ChairmanLuoNoteKey = "rpg.return.rc03.chairman-luo-register-note";

        public static bool IsValidRecord(RC03BaijiMineRecord record, Func<string, int> getInt)
        {
            if (record.VisitsCompleted != RC03IslandVisits.All ||
                !Enum.IsDefined(typeof(RC03Resolution), record.Resolution) ||
                !record.ChairmanLuoRegisterNoteRecorded)
                return false;

            if (record.Resolution == RC03Resolution.AmendCharterWithSuccessors &&
                (!record.PumpDiagramVerified || !record.SuccessorTrainingSecured))
                return false;

            if (record.Resolution == RC03Resolution.StayOneYearByConsent &&
                !record.LanRuoConsentedToStay)
                return false;

            if (record.Resolution != RC03Resolution.StayOneYearByConsent &&
                record.LanRuoConsentedToStay)
                return false;

            if (!M08FirstWorldNoticeFacts.TryCaseAvailable(
                    RosterCaseNumber, getInt, out bool available) ||
                !available)
                return false;

            return true;
        }

        public static bool CanRecord(RC03BaijiMineRecord record, Func<string, int> getInt)
        {
            return getInt != null &&
                getInt(ConfirmedKey) == 0 &&
                IsValidRecord(record, getInt);
        }

        public static bool TryRecord(RC03BaijiMineRecord record)
        {
            var variables = Maki.Initialized ? Maki.Game?.Variables : null;
            return variables != null && TryRecord(
                record,
                key => variables.GetInt(key),
                (key, value) => variables.Set(key, value));
        }

        public static bool TryRecord(
            RC03BaijiMineRecord record,
            Func<string, int> getInt,
            Action<string, int> setInt)
        {
            if (getInt == null || setInt == null || !CanRecord(record, getInt))
                return false;

            setInt(ConfirmedKey, 0);
            setInt(VisitsMaskKey, (int)record.VisitsCompleted);
            setInt(ResolutionKey, (int)record.Resolution);
            setInt(PumpDiagramKey, record.PumpDiagramVerified ? 1 : 0);
            setInt(SuccessorTrainingKey, record.SuccessorTrainingSecured ? 1 : 0);
            setInt(LanRuoStayConsentKey, record.LanRuoConsentedToStay ? 1 : 0);
            setInt(ChairmanLuoNoteKey, record.ChairmanLuoRegisterNoteRecorded ? 1 : 0);
            setInt(ConfirmedKey, 1);
            return true;
        }

        public static bool TryRead(out RC03BaijiMineRecord? record)
        {
            record = null;
            var variables = Maki.Initialized ? Maki.Game?.Variables : null;
            return variables != null && TryRead(key => variables.GetInt(key), out record);
        }

        public static bool TryRead(Func<string, int> getInt, out RC03BaijiMineRecord? record)
        {
            record = null;
            if (getInt == null)
                return false;
            if (getInt(ConfirmedKey) != 1)
                return true;

            int rawVisits = getInt(VisitsMaskKey);
            int rawRes = getInt(ResolutionKey);
            int rawDiagram = getInt(PumpDiagramKey);
            int rawTraining = getInt(SuccessorTrainingKey);
            int rawConsent = getInt(LanRuoStayConsentKey);
            int rawNote = getInt(ChairmanLuoNoteKey);

            if (rawVisits != (int)RC03IslandVisits.All ||
                !Enum.IsDefined(typeof(RC03Resolution), rawRes) ||
                (rawDiagram != 0 && rawDiagram != 1) ||
                (rawTraining != 0 && rawTraining != 1) ||
                (rawConsent != 0 && rawConsent != 1) ||
                rawNote != 1)
                return true;

            var saved = new RC03BaijiMineRecord(
                (RC03IslandVisits)rawVisits,
                (RC03Resolution)rawRes,
                rawDiagram == 1,
                rawTraining == 1,
                rawConsent == 1,
                chairmanLuoRegisterNoteRecorded: true);
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
            setInt(VisitsMaskKey, 0);
            setInt(ResolutionKey, 0);
            setInt(PumpDiagramKey, 0);
            setInt(SuccessorTrainingKey, 0);
            setInt(LanRuoStayConsentKey, 0);
            setInt(ChairmanLuoNoteKey, 0);
            return true;
        }
    }
}
