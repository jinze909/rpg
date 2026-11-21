using System;

namespace Rpg.Soundwell
{
    public enum RC02Stage
    {
        IntakeVerification = 0,
        RoofConfession = 1,
        DeskDecision = 2,
        AdoptionNameMoment = 3,
        Completed = 4
    }

    public enum RC02AdoptionCompanionship
    {
        WaitQuietly = 1,
        SpeakForHer = 2
    }

    /// <summary>
    /// Authoritative RC-02 ("借来的名字") intake, roof confession, and adoption
    /// state machine. Adoption is governed by player companionship (never a
    /// hidden stat check) and strictly requires the mother speaking "石棱"
    /// aloud plus Shi Leng's consent; if she cannot say it, Yi Lei sets down
    /// her pen and control returns to DeskDecision.
    /// </summary>
    public sealed class RC02ZhengYuanSession
    {
        public RC02Stage Stage { get; private set; } = RC02Stage.IntakeVerification;
        public RC02IntakeChecks ChecksCompleted { get; private set; } = RC02IntakeChecks.None;
        public bool RoofConfessionHeard { get; private set; }
        public bool MotherCalledShiLeng { get; private set; }
        public bool ShiLengConsented { get; private set; }
        public bool RealZhengYuanFound { get; }
        public RC02ZhengYuanRecord? CompletedRecord { get; private set; }
        public bool IsResolved => Stage == RC02Stage.Completed;

        public RC02ZhengYuanSession(bool realZhengYuanFound = false)
        {
            RealZhengYuanFound = realZhengYuanFound;
        }

        public static bool TryCreateFromSavedFacts(
            Func<string, int> getInt,
            bool realZhengYuanFound,
            out RC02ZhengYuanSession session)
        {
            session = null;
            if (getInt == null || RC02ZhengYuanFacts.HasOrphanedDownstreamFacts(getInt))
                return false;
            if (!M08FirstWorldNoticeFacts.TryCaseAvailable(
                    RC02ZhengYuanFacts.RosterCaseNumber, getInt, out bool available) ||
                !available)
                return false;

            session = new RC02ZhengYuanSession(realZhengYuanFound);
            return true;
        }

        public bool TryCompleteIntakeCheck(RC02IntakeChecks check)
        {
            if (Stage != RC02Stage.IntakeVerification)
                return false;
            if (check != RC02IntakeChecks.UpperLeftToothCheck &&
                check != RC02IntakeChecks.BattleFlankQuestion &&
                check != RC02IntakeChecks.DominantHandQuestion)
                return false;
            if ((ChecksCompleted & check) != 0)
                return false;

            ChecksCompleted |= check;
            if (ChecksCompleted == RC02IntakeChecks.All)
                Stage = RC02Stage.RoofConfession;
            return true;
        }

        public bool TryHearRoofConfession()
        {
            if (Stage != RC02Stage.RoofConfession ||
                ChecksCompleted != RC02IntakeChecks.All)
                return false;

            RoofConfessionHeard = true;
            Stage = RC02Stage.DeskDecision;
            return true;
        }

        public bool TryBeginAdoptionNegotiation()
        {
            if (Stage != RC02Stage.DeskDecision)
                return false;
            MotherCalledShiLeng = false;
            ShiLengConsented = false;
            Stage = RC02Stage.AdoptionNameMoment;
            return true;
        }

        /// <summary>
        /// Resolves the adoption naming moment. Waiting quietly lets the mother
        /// speak "石棱" and Shi Leng nod ("娘") when Shi Leng consents; if she
        /// cannot say his name or Shi Leng declines, Yi Lei puts down her pen
        /// and returns to DeskDecision.
        /// </summary>
        public bool TryResolveAdoptionMoment(
            RC02AdoptionCompanionship companionship,
            bool shiLengWilling)
        {
            if (Stage != RC02Stage.AdoptionNameMoment ||
                !Enum.IsDefined(typeof(RC02AdoptionCompanionship), companionship))
                return false;

            if (companionship == RC02AdoptionCompanionship.WaitQuietly && shiLengWilling)
            {
                MotherCalledShiLeng = true;
                ShiLengConsented = true;
                return true;
            }

            MotherCalledShiLeng = false;
            ShiLengConsented = false;
            Stage = RC02Stage.DeskDecision;
            return false;
        }

        public bool TrySettleResolution(
            RC02Resolution resolution,
            Func<string, int> getInt,
            Action<string, int> setInt)
        {
            if (!Enum.IsDefined(typeof(RC02Resolution), resolution) ||
                getInt == null || setInt == null)
                return false;

            if (resolution == RC02Resolution.AdoptAsShiLeng)
            {
                if (Stage != RC02Stage.AdoptionNameMoment ||
                    !MotherCalledShiLeng || !ShiLengConsented)
                    return false;
            }
            else if (Stage != RC02Stage.DeskDecision)
            {
                return false;
            }

            var record = new RC02ZhengYuanRecord(
                ChecksCompleted,
                RoofConfessionHeard,
                resolution,
                resolution == RC02Resolution.AdoptAsShiLeng && MotherCalledShiLeng,
                resolution == RC02Resolution.AdoptAsShiLeng && ShiLengConsented,
                RealZhengYuanFound);

            if (!RC02ZhengYuanFacts.TryRead(
                    getInt, out RC02ZhengYuanRecord? existing) &&
                // Incomplete writes may resume only with the matching
                // immutable registration and verified identity intent.
                (getInt(RC02ZhengYuanFacts.ConfirmedKey) != 0 ||
                 !RC02ZhengYuanFacts.CanRecord(record, getInt)))
                return false;

            // Recover idempotently if a prior write committed ConfirmedKey = 1
            // before its acknowledgement was lost.
            if (existing.HasValue)
            {
                if (!Matches(record, existing.Value))
                    return false;
            }
            else if (getInt(RC02ZhengYuanFacts.ConfirmedKey) != 0 ||
                     !RC02ZhengYuanFacts.TryRecord(record, getInt, setInt))
            {
                return false;
            }

            if (!RC02ZhengYuanFacts.TryRead(getInt, out RC02ZhengYuanRecord? saved) ||
                !saved.HasValue || !Matches(record, saved.Value))
                return false;

            CompletedRecord = saved.Value;
            Stage = RC02Stage.Completed;
            return true;
        }

        private static bool Matches(RC02ZhengYuanRecord expected, RC02ZhengYuanRecord saved)
        {
            return expected.ChecksCompleted == saved.ChecksCompleted &&
                expected.RoofConfessionHeard == saved.RoofConfessionHeard &&
                expected.Resolution == saved.Resolution &&
                expected.MotherCalledShiLeng == saved.MotherCalledShiLeng &&
                expected.ShiLengConsented == saved.ShiLengConsented &&
                expected.RealZhengYuanFound == saved.RealZhengYuanFound;
        }
    }
}
