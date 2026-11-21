using System;

namespace Rpg.Soundwell
{
    public enum RC03Stage
    {
        IslandInvestigation = 0,
        ResolutionDecision = 1,
        Completed = 2
    }

    /// <summary>
    /// Authoritative RC-03 ("白矶岛的多数") island investigation and charter
    /// resolution state machine. Reads M07 executed route to check whether
    /// Shen Li's pump diagram copy was carried from MinerShutdown, requires
    /// real pump-diagram verification and successor training before amending
    /// the charter, requires Lan Ruo's explicit consent before recording a
    /// one-year stay, and supports retry-safe + commit-then-throw Makinom saves.
    /// </summary>
    public sealed class RC03BaijiMineSession
    {
        public RC03Stage Stage { get; private set; } = RC03Stage.IslandInvestigation;
        public RC03IslandVisits VisitsCompleted { get; private set; } = RC03IslandVisits.None;
        public bool CarriedM07PumpDiagramCopy { get; }
        public bool PumpDiagramVerified { get; private set; }
        public bool SuccessorTrainingSecured { get; private set; }
        public bool LanRuoConsentedToStay { get; private set; }
        public RC03BaijiMineRecord? CompletedRecord { get; private set; }
        public bool IsResolved => Stage == RC03Stage.Completed;

        public RC03BaijiMineSession(bool carriedM07PumpDiagramCopy)
        {
            CarriedM07PumpDiagramCopy = carriedM07PumpDiagramCopy;
        }

        public static bool TryCreateFromSavedFacts(
            Func<string, int> getInt,
            out RC03BaijiMineSession session)
        {
            session = null;
            if (getInt == null)
                return false;
            if (!M08FirstWorldNoticeFacts.TryCaseAvailable(
                    RC03BaijiMineFacts.RosterCaseNumber, getInt, out bool available) ||
                !available)
                return false;

            if (!M07ExtractionFacts.TryRead(getInt, out M07ExtractionRecord? m07) ||
                !m07.HasValue)
                return false;

            bool hasCopy = m07.Value.ExecutedRoute == M07ExecutedRoute.MinerShutdown;
            session = new RC03BaijiMineSession(hasCopy);
            return true;
        }

        public bool TryCompleteVisit(RC03IslandVisits visit)
        {
            if (Stage != RC03Stage.IslandInvestigation)
                return false;
            if (visit != RC03IslandVisits.CanteenAndNightSchool &&
                visit != RC03IslandVisits.PumpRoomTwoAndLanRuo &&
                visit != RC03IslandVisits.MutualAidCharterAndTalks)
                return false;
            if ((VisitsCompleted & visit) != 0)
                return false;

            VisitsCompleted |= visit;
            if (VisitsCompleted == RC03IslandVisits.All)
                Stage = RC03Stage.ResolutionDecision;
            return true;
        }

        public bool TryVerifyPumpDiagram(bool matchedAtPumpTwo)
        {
            if (Stage != RC03Stage.ResolutionDecision ||
                PumpDiagramVerified || !matchedAtPumpTwo)
                return false;
            PumpDiagramVerified = true;
            return true;
        }

        public bool TrySecureSuccessorTraining(bool trainingConfirmed)
        {
            if (Stage != RC03Stage.ResolutionDecision ||
                !PumpDiagramVerified || SuccessorTrainingSecured ||
                !trainingConfirmed)
                return false;
            SuccessorTrainingSecured = true;
            return true;
        }

        public bool TryRecordLanRuoStayDecision(bool lanRuoAgreed)
        {
            if (Stage != RC03Stage.ResolutionDecision)
                return false;
            LanRuoConsentedToStay = lanRuoAgreed;
            return lanRuoAgreed;
        }

        public bool CanSettle(RC03Resolution resolution)
        {
            if (Stage != RC03Stage.ResolutionDecision ||
                !Enum.IsDefined(typeof(RC03Resolution), resolution))
                return false;

            switch (resolution)
            {
                case RC03Resolution.AmendCharterWithSuccessors:
                    return PumpDiagramVerified && SuccessorTrainingSecured;
                case RC03Resolution.SecretlyEvacuateLanRuo:
                    return true;
                case RC03Resolution.StayOneYearByConsent:
                    return LanRuoConsentedToStay;
                default:
                    return false;
            }
        }

        public bool TrySettleResolution(
            RC03Resolution resolution,
            Func<string, int> getInt,
            Action<string, int> setInt)
        {
            if (!CanSettle(resolution) || getInt == null || setInt == null)
                return false;

            var record = new RC03BaijiMineRecord(
                VisitsCompleted,
                resolution,
                PumpDiagramVerified,
                SuccessorTrainingSecured,
                resolution == RC03Resolution.StayOneYearByConsent && LanRuoConsentedToStay,
                chairmanLuoRegisterNoteRecorded: true);

            if (!RC03BaijiMineFacts.TryRead(
                    getInt, out RC03BaijiMineRecord? existing) &&
                // A torn payload is recoverable only if the previous decision
                // and all already-persisted proof flags match this record.
                (getInt(RC03BaijiMineFacts.ConfirmedKey) != 0 ||
                 !RC03BaijiMineFacts.CanRecord(record, getInt)))
                return false;

            // Recover idempotently if a prior write committed ConfirmedKey = 1
            // before its acknowledgement was lost.
            if (existing.HasValue)
            {
                if (!Matches(record, existing.Value))
                    return false;
            }
            else if (getInt(RC03BaijiMineFacts.ConfirmedKey) != 0 ||
                     !RC03BaijiMineFacts.TryRecord(record, getInt, setInt))
            {
                return false;
            }

            if (!RC03BaijiMineFacts.TryRead(getInt, out RC03BaijiMineRecord? saved) ||
                !saved.HasValue || !Matches(record, saved.Value))
                return false;

            CompletedRecord = saved.Value;
            Stage = RC03Stage.Completed;
            return true;
        }

        private static bool Matches(RC03BaijiMineRecord expected, RC03BaijiMineRecord saved)
        {
            return expected.VisitsCompleted == saved.VisitsCompleted &&
                expected.Resolution == saved.Resolution &&
                expected.PumpDiagramVerified == saved.PumpDiagramVerified &&
                expected.SuccessorTrainingSecured == saved.SuccessorTrainingSecured &&
                expected.LanRuoConsentedToStay == saved.LanRuoConsentedToStay &&
                expected.ChairmanLuoRegisterNoteRecorded == saved.ChairmanLuoRegisterNoteRecorded;
        }
    }
}
