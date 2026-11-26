using System;
using GamingIsLove.Makinom;

namespace Rpg.Soundwell
{
    public enum ReturnMainChapter
    {
        BeforeM08 = 0,
        M08FirstWorldNotice = 8,
        M09BerthTable = 9,
        M10Assembly = 10,
        M11ReverseRescue = 11,
        M12BridgeTruth = 12,
        M13DepartureRisk = 13,
        M14FinalPassage = 14,
        M15FinalRollCall = 15,
        M16Epilogue = 16,
        StoryCompleted = 17
    }

    public enum ReturnRosterCaseState
    {
        Locked = 0,
        Available = 1,
        Settled = 2
    }

    /// <summary>
    /// Fail-closed runtime snapshot of the player's verified main-story chapter
    /// (M07-M16), all five roster cases (RC-01..RC-05), permanent regional tales
    /// (ST-01, ST-02, ST-03, ST-04, ST-05), voyage-bound ST-06 homecoming, and eligible M16
    /// epilogue letters derived strictly from verified Makinom facts.
    /// </summary>
    public readonly struct ReturnStoryProgressSnapshot
    {
        public ReturnStoryProgressSnapshot(
            ReturnMainChapter activeChapter,
            ReturnRosterCaseState rc01State,
            RC01ChenYanRecord? rc01Record,
            ReturnRosterCaseState rc02State,
            RC02ZhengYuanRecord? rc02Record,
            ReturnRosterCaseState rc03State,
            RC03BaijiMineRecord? rc03Record,
            ReturnRosterCaseState rc04State,
            RC04ReefRecord? rc04Record,
            ReturnRosterCaseState rc05State,
            RC05ThirdSquadRecord? rc05Record,
            ST02RiverLanternsRecord? st02Record,
            ST03BathhouseRecord? st03Record,
            ST05TwoDoorsOutcome? st05Outcome,
            ST06HomecomingRecord? st06Record,
            M16DeliveredLettersFlags eligibleEpilogueLetters,
            ST01RegionalOutcome? st01Outcome = null,
            ST04EchoValleyOutcome? st04Outcome = null)
        {
            ActiveChapter = activeChapter;
            RC01State = rc01State;
            RC01Record = rc01Record;
            RC02State = rc02State;
            RC02Record = rc02Record;
            RC03State = rc03State;
            RC03Record = rc03Record;
            RC04State = rc04State;
            RC04Record = rc04Record;
            RC05State = rc05State;
            RC05Record = rc05Record;
            ST01Outcome = st01Outcome;
            ST02Record = st02Record;
            ST03Record = st03Record;
            ST04Outcome = st04Outcome;
            ST05Outcome = st05Outcome;
            ST06Record = st06Record;
            EligibleEpilogueLetters = eligibleEpilogueLetters;
        }

        public ReturnMainChapter ActiveChapter { get; }
        public ReturnRosterCaseState RC01State { get; }
        public RC01ChenYanRecord? RC01Record { get; }
        public ReturnRosterCaseState RC02State { get; }
        public RC02ZhengYuanRecord? RC02Record { get; }
        public ReturnRosterCaseState RC03State { get; }
        public RC03BaijiMineRecord? RC03Record { get; }
        public ReturnRosterCaseState RC04State { get; }
        public RC04ReefRecord? RC04Record { get; }
        public ReturnRosterCaseState RC05State { get; }
        public RC05ThirdSquadRecord? RC05Record { get; }
        public ST01RegionalOutcome? ST01Outcome { get; }
        public ST02RiverLanternsRecord? ST02Record { get; }
        public ST03BathhouseRecord? ST03Record { get; }
        public ST04EchoValleyOutcome? ST04Outcome { get; }
        public ST05TwoDoorsOutcome? ST05Outcome { get; }
        public ST06HomecomingRecord? ST06Record { get; }
        public M16DeliveredLettersFlags EligibleEpilogueLetters { get; }

        public int SettledRosterCaseCount =>
            (RC01State == ReturnRosterCaseState.Settled ? 1 : 0) +
            (RC02State == ReturnRosterCaseState.Settled ? 1 : 0) +
            (RC03State == ReturnRosterCaseState.Settled ? 1 : 0) +
            (RC04State == ReturnRosterCaseState.Settled ? 1 : 0) +
            (RC05State == ReturnRosterCaseState.Settled ? 1 : 0);

        public int SettledRegionalStoryCount =>
            (ST01Outcome.HasValue && ST01Outcome.Value.Theater.HasValue ? 1 : 0) +
            (ST02Record.HasValue ? 1 : 0) +
            (ST03Record.HasValue ? 1 : 0) +
            (ST04Outcome.HasValue ? 1 : 0) +
            (ST05Outcome.HasValue ? 1 : 0) +
            (ST06Record.HasValue ? 1 : 0);
    }

    /// <summary>
    /// Unified story-progress reader and full downstream voyage-reset coordinator
    /// for Return ("归来"). Reads verified Makinom facts without trusting raw flags
    /// and resets all voyage-bound chapters (M06-M16, RC-01..RC-05, ST-06) in
    /// marker-first order while preserving permanent regional stories (ST-01, ST-02, ST-03, ST-04, ST-05).
    /// </summary>
    public static class ReturnStoryLedger
    {
        public static bool TryBuildSnapshot(out ReturnStoryProgressSnapshot snapshot)
        {
            snapshot = default;
            var variables = Maki.Initialized ? Maki.Game?.Variables : null;
            return variables != null &&
                TryBuildSnapshot(key => variables.GetInt(key), out snapshot);
        }

        public static bool TryBuildSnapshot(
            Func<string, int> getInt,
            out ReturnStoryProgressSnapshot snapshot)
        {
            snapshot = default;
            if (getInt == null)
                return false;

            ReturnMainChapter activeChapter = ResolveActiveMainChapter(getInt);

            ReturnRosterCaseState rc01State = ResolveRosterCaseState(
                RC01ChenYanFacts.RosterCaseNumber, getInt,
                RC01ChenYanFacts.TryRead(getInt, out RC01ChenYanRecord? rc01) && rc01.HasValue);
            ReturnRosterCaseState rc02State = ResolveRosterCaseState(
                RC02ZhengYuanFacts.RosterCaseNumber, getInt,
                RC02ZhengYuanFacts.TryRead(getInt, out RC02ZhengYuanRecord? rc02) && rc02.HasValue);
            ReturnRosterCaseState rc03State = ResolveRosterCaseState(
                RC03BaijiMineFacts.RosterCaseNumber, getInt,
                RC03BaijiMineFacts.TryRead(getInt, out RC03BaijiMineRecord? rc03) && rc03.HasValue);
            ReturnRosterCaseState rc04State = ResolveRosterCaseState(
                4, getInt,
                RC04DingYueReefFacts.TryRead(getInt, out RC04ReefRecord? rc04) && rc04.HasValue);
            ReturnRosterCaseState rc05State = ResolveRosterCaseState(
                RC05ThirdSquadFacts.RosterCaseNumber, getInt,
                RC05ThirdSquadFacts.TryRead(getInt, out RC05ThirdSquadRecord? rc05) && rc05.HasValue);

            ST01RegionalOutcome? st01 =
                ST01RegionalStoryFacts.TryRead(getInt, out ST01RegionalOutcome st01Outcome) &&
                (st01Outcome.Departure.HasValue || st01Outcome.Theater.HasValue)
                    ? (ST01RegionalOutcome?)st01Outcome
                    : null;
            ST02RiverLanternsFacts.TryRead(getInt, out ST02RiverLanternsRecord? st02);
            ST03BathhouseFacts.TryRead(getInt, out ST03BathhouseRecord? st03);
            ST04EchoValleyOutcome? st04 =
                ST04EchoValleyFacts.TryRead(getInt, out ST04EchoValleyOutcome? st04Outcome)
                    ? st04Outcome
                    : null;
            ST05TwoDoorsFacts.TryRead(getInt, out ST05TwoDoorsOutcome? st05);
            ST06TaoJiHomecomingFacts.TryRead(getInt, out ST06HomecomingRecord? st06);

            M16DeliveredLettersFlags eligibleLetters =
                M16EpilogueFacts.EvaluateEligibleLetters(getInt);

            snapshot = new ReturnStoryProgressSnapshot(
                activeChapter,
                rc01State, rc01,
                rc02State, rc02,
                rc03State, rc03,
                rc04State, rc04,
                rc05State, rc05,
                st02, st03, st05, st06,
                eligibleLetters,
                st01,
                st04);
            return true;
        }

        public static bool TryResetAllDownstreamVoyageFacts()
        {
            var variables = Maki.Initialized ? Maki.Game?.Variables : null;
            return variables != null &&
                TryResetAllDownstreamVoyageFacts((key, value) => variables.Set(key, value));
        }

        /// <summary>
        /// Invalidates every voyage-bound public confirmation marker across
        /// M06-M16, RC-01..RC-05, and ST-06 BEFORE clearing individual fields,
        /// so even a torn write mid-reset cannot leave stale downstream chapters
        /// readable. Permanent regional tales (ST-01, ST-02, ST-03, ST-04, ST-05) are preserved.
        /// </summary>
        public static bool TryResetAllDownstreamVoyageFacts(Action<string, int> setInt)
        {
            if (setInt == null)
                return false;

            // Step 1: Invalidate all public confirmation markers first.
            setInt(M06MineCampReconFacts.ObservedMaskKey, 0);
            setInt(M06FamilyLetterDeliveryFacts.StatusMaskKey, 0);
            setInt(M06RescuePlanFacts.ConfirmedKey, 0);
            setInt(M07ExtractionFacts.ConfirmedKey, 0);
            setInt(M08DockTruthFacts.ConfirmedKey, 0);
            setInt(M08FirstWorldNoticeFacts.ConfirmedKey, 0);
            setInt(M08WorkshopDepositFacts.CompletedKey, 0);
            setInt(M09BerthTableFacts.ConfirmedKey, 0);
            setInt(M10AssemblyFacts.ConfirmedKey, 0);
            setInt(M10WorldNoticeThreeFacts.ConfirmedKey, 0);
            setInt(M11ReverseRescueFacts.ConfirmedKey, 0);
            setInt(M12BridgeTruthFacts.ConfirmedKey, 0);
            setInt(M13DepartureRiskFacts.DepartureConfirmedKey, 0);
            setInt(M14FinalPassageFacts.ConfirmedKey, 0);
            setInt(M15FinalRollCallFacts.ConfirmedKey, 0);
            setInt(M16EpilogueFacts.ConfirmedKey, 0);
            setInt(RC01ChenYanFacts.ConfirmedKey, 0);
            setInt(RC02ZhengYuanFacts.ConfirmedKey, 0);
            setInt(RC03BaijiMineFacts.ConfirmedKey, 0);
            setInt(RC04DingYueReefFacts.ConfirmedKey, 0);
            setInt(RC05ThirdSquadFacts.ConfirmedKey, 0);
            setInt(ST06TaoJiHomecomingFacts.ConfirmedKey, 0);

            // Step 2: Clear all voyage-bound fields through each Facts owner.
            return M06MineCampReconFacts.TryResetForNewVoyage(setInt) &&
                M06FamilyLetterDeliveryFacts.TryResetForNewVoyage(setInt) &&
                M06RescuePlanFacts.TryResetForNewVoyage(setInt) &&
                M07ExtractionFacts.TryResetForNewVoyage(setInt) &&
                M08DockTruthFacts.TryResetForNewVoyage(setInt) &&
                M08FirstWorldNoticeFacts.TryResetForNewVoyage(setInt) &&
                M08WorkshopDepositFacts.TryResetForNewVoyage(setInt) &&
                M09BerthTableFacts.TryResetForNewVoyage(setInt) &&
                M10WorldNoticeThreeFacts.TryResetForNewVoyage(setInt) &&
                M10AssemblyFacts.TryResetForNewVoyage(setInt) &&
                M11ReverseRescueFacts.TryResetForNewVoyage(setInt) &&
                M12BridgeTruthFacts.TryResetForNewVoyage(setInt) &&
                M13DepartureRiskFacts.TryResetForNewVoyage(setInt) &&
                M14FinalPassageFacts.TryResetForNewVoyage(setInt) &&
                M15FinalRollCallFacts.TryResetForNewVoyage(setInt) &&
                M16EpilogueFacts.TryResetForNewVoyage(setInt) &&
                RC01ChenYanFacts.TryResetForNewVoyage(setInt) &&
                RC02ZhengYuanFacts.TryResetForNewVoyage(setInt) &&
                RC03BaijiMineFacts.TryResetForNewVoyage(setInt) &&
                RC04DingYueReefFacts.TryResetForNewVoyage(setInt) &&
                RC05ThirdSquadFacts.TryResetForNewVoyage(setInt) &&
                ST06TaoJiHomecomingFacts.TryResetForNewVoyage(setInt);
        }

        private static ReturnMainChapter ResolveActiveMainChapter(Func<string, int> getInt)
        {
            if (!M07ExtractionFacts.TryRead(getInt, out M07ExtractionRecord? m07) || !m07.HasValue)
                return ReturnMainChapter.BeforeM08;
            if (!M08FirstWorldNoticeFacts.TryRead(getInt, out bool noticeRead) || !noticeRead)
                return ReturnMainChapter.M08FirstWorldNotice;
            if (!M09BerthTableFacts.TryRead(getInt, out M09BerthTableRecord? m09) || !m09.HasValue)
                return ReturnMainChapter.M09BerthTable;
            if (!M10AssemblyFacts.TryRead(getInt, out M10AssemblyRecord? m10) || !m10.HasValue)
                return ReturnMainChapter.M10Assembly;
            if (!M11ReverseRescueFacts.TryRead(getInt, out M11ReverseRescueRecord? m11) || !m11.HasValue)
                return ReturnMainChapter.M11ReverseRescue;
            if (!M12BridgeTruthFacts.TryRead(getInt, out M12BridgeTruthRecord? m12) || !m12.HasValue)
                return ReturnMainChapter.M12BridgeTruth;
            if (!M13DepartureRiskFacts.TryReadDeparture(getInt, out M13DepartureRecord? m13) || !m13.HasValue)
                return ReturnMainChapter.M13DepartureRisk;
            if (!M14FinalPassageFacts.TryRead(getInt, out M14FinalPassageRecord? m14) || !m14.HasValue)
                return ReturnMainChapter.M14FinalPassage;
            if (!M15FinalRollCallFacts.TryRead(getInt, out M15FinalRollCallRecord? m15) || !m15.HasValue)
                return ReturnMainChapter.M15FinalRollCall;
            if (!M16EpilogueFacts.TryRead(getInt, out M16EpilogueRecord? m16) || !m16.HasValue)
                return ReturnMainChapter.M16Epilogue;
            return ReturnMainChapter.StoryCompleted;
        }

        private static ReturnRosterCaseState ResolveRosterCaseState(
            int caseNumber,
            Func<string, int> getInt,
            bool settled)
        {
            if (settled)
                return ReturnRosterCaseState.Settled;
            if (HasAnyPersistedRosterCaseFacts(caseNumber, getInt))
                return ReturnRosterCaseState.Locked;
            if (caseNumber == RC02ZhengYuanFacts.RosterCaseNumber &&
                RC02ZhengYuanFacts.HasOrphanedDownstreamFacts(getInt))
                return ReturnRosterCaseState.Locked;
            if (caseNumber == RC05ThirdSquadFacts.RosterCaseNumber &&
                RC05ThirdSquadFacts.HasOrphanedDownstreamFacts(getInt))
                return ReturnRosterCaseState.Locked;
            if (M08FirstWorldNoticeFacts.TryCaseAvailable(caseNumber, getInt, out bool available) &&
                available)
                return ReturnRosterCaseState.Available;
            return ReturnRosterCaseState.Locked;
        }

        private static bool HasAnyPersistedRosterCaseFacts(int caseNumber, Func<string, int> getInt)
        {
            switch (caseNumber)
            {
                case 1:
                    return getInt(RC01ChenYanFacts.ConfirmedKey) != 0 ||
                        getInt(RC01ChenYanFacts.PendingSignatureKey) != 0 ||
                        getInt(RC01ChenYanFacts.CluesMaskKey) != 0 ||
                        getInt(RC01ChenYanFacts.TalkOrderKey) != 0 ||
                        getInt(RC01ChenYanFacts.OutcomeKey) != 0 ||
                        getInt(RC01ChenYanFacts.LinSuNoteKey) != 0;
                case 2:
                    return getInt(RC02ZhengYuanFacts.ConfirmedKey) != 0 ||
                        getInt(RC02ZhengYuanFacts.PendingSignatureKey) != 0 ||
                        getInt(RC02ZhengYuanFacts.ChecksMaskKey) != 0 ||
                        getInt(RC02ZhengYuanFacts.RoofConfessionKey) != 0 ||
                        getInt(RC02ZhengYuanFacts.ResolutionKey) != 0 ||
                        getInt(RC02ZhengYuanFacts.MotherCalledKey) != 0 ||
                        getInt(RC02ZhengYuanFacts.ShiLengConsentedKey) != 0 ||
                        getInt(RC02ZhengYuanFacts.RealZhengYuanFoundKey) != 0;
                case 3:
                    return getInt(RC03BaijiMineFacts.ConfirmedKey) != 0 ||
                        getInt(RC03BaijiMineFacts.PendingSignatureKey) != 0 ||
                        getInt(RC03BaijiMineFacts.VisitsMaskKey) != 0 ||
                        getInt(RC03BaijiMineFacts.ResolutionKey) != 0 ||
                        getInt(RC03BaijiMineFacts.PumpDiagramKey) != 0 ||
                        getInt(RC03BaijiMineFacts.SuccessorTrainingKey) != 0 ||
                        getInt(RC03BaijiMineFacts.LanRuoStayConsentKey) != 0 ||
                        getInt(RC03BaijiMineFacts.ChairmanLuoNoteKey) != 0;
                case 4:
                    return getInt(RC04DingYueReefFacts.ConfirmedKey) != 0 ||
                        getInt(RC04DingYueReefFacts.PendingSignatureKey) != 0 ||
                        getInt(RC04DingYueReefFacts.RoundsCompletedKey) != 0 ||
                        getInt(RC04DingYueReefFacts.AyuanSongUsedKey) != 0 ||
                        getInt(RC04DingYueReefFacts.ResolutionKey) != 0 ||
                        getInt(RC04DingYueReefFacts.DecoyPreparedKey) != 0;
                case 5:
                    return getInt(RC05ThirdSquadFacts.ConfirmedKey) != 0 ||
                        getInt(RC05ThirdSquadFacts.PendingSignatureKey) != 0 ||
                        getInt(RC05ThirdSquadFacts.TrialDeliveryKey) != 0 ||
                        getInt(RC05ThirdSquadFacts.ResolutionKey) != 0 ||
                        getInt(RC05ThirdSquadFacts.SkirmishKey) != 0 ||
                        getInt(RC05ThirdSquadFacts.RealZhengYuanVerifiedKey) != 0 ||
                        getInt(RC05ThirdSquadFacts.OldWomanStoneMentionedKey) != 0;
                default:
                    return false;
            }
        }
    }
}
