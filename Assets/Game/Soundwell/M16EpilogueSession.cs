using System;

namespace Rpg.Soundwell
{
    public enum M16Phase
    {
        AfternoonWalk = 0,
        DeskLettersAndRegister = 1,
        FinalStandingAndFadeOut = 2,
        Completed = 3
    }

    /// <summary>
    /// Authoritative M16 ("一个普通的下午") playable epilogue state machine.
    /// Allows free, pressure-free afternoon visits, reading only letters from
    /// actually completed cases, choosing the final standing spot, and settling
    /// the respect_agency closing tone without fabricating Shen Li's presence.
    /// </summary>
    public sealed class M16EpilogueSession
    {
        public M16Phase Phase { get; private set; } = M16Phase.AfternoonWalk;
        public M16DeliveredLettersFlags EligibleLetters { get; }
        public bool ShenLiWillingToMeet { get; }
        public M16RespectAgencyTone ClosingTone { get; }
        public M16WalkStops StopsVisited { get; private set; } = M16WalkStops.None;
        public M16DeliveredLettersFlags LettersRead { get; private set; } = M16DeliveredLettersFlags.None;
        public bool DeskRegisterReviewed { get; private set; }
        public M16StandingSpot? StandingSpot { get; private set; }
        public M16EpilogueRecord? CompletedRecord { get; private set; }
        public bool IsCompleted => Phase == M16Phase.Completed;

        private M16EpilogueSession(
            M16DeliveredLettersFlags eligibleLetters,
            bool shenLiWillingToMeet,
            M16RespectAgencyTone closingTone)
        {
            EligibleLetters = eligibleLetters;
            ShenLiWillingToMeet = shenLiWillingToMeet;
            ClosingTone = closingTone;
        }

        public static bool TryCreateFromSavedFacts(
            Func<string, int> getInt,
            bool shenLiWillingToMeet,
            M16RespectAgencyTone closingTone,
            out M16EpilogueSession session)
        {
            session = null;
            if (getInt == null || !Enum.IsDefined(typeof(M16RespectAgencyTone), closingTone))
                return false;
            if (!M15FinalRollCallFacts.TryRead(getInt, out M15FinalRollCallRecord? m15) ||
                !m15.HasValue)
                return false;

            if (!shenLiWillingToMeet && closingTone != M16RespectAgencyTone.ShenLiDeclinedMeeting)
                return false;
            if (shenLiWillingToMeet && closingTone == M16RespectAgencyTone.ShenLiDeclinedMeeting)
                return false;

            M16DeliveredLettersFlags eligible = M16EpilogueFacts.EvaluateEligibleLetters(getInt);
            session = new M16EpilogueSession(eligible, shenLiWillingToMeet, closingTone);
            return true;
        }

        public bool TryVisitStop(M16WalkStops stop)
        {
            if (Phase != M16Phase.AfternoonWalk)
                return false;
            if (stop != M16WalkStops.WorkshopStop &&
                stop != M16WalkStops.QiQueWarehouseStop &&
                stop != M16WalkStops.OulinDinnerTableStop &&
                stop != M16WalkStops.WestLaneAndHarborOfficeStop)
                return false;
            if ((StopsVisited & stop) != 0)
                return false;

            StopsVisited |= stop;
            return true;
        }

        public bool TryProceedToDesk()
        {
            if (Phase != M16Phase.AfternoonWalk)
                return false;
            Phase = M16Phase.DeskLettersAndRegister;
            return true;
        }

        public bool TryReadLetter(M16DeliveredLettersFlags letter)
        {
            if (Phase != M16Phase.DeskLettersAndRegister)
                return false;
            if (letter != M16DeliveredLettersFlags.RC01ChenYan &&
                letter != M16DeliveredLettersFlags.RC02ZhengYuan &&
                letter != M16DeliveredLettersFlags.RC05ThirdSquad &&
                letter != M16DeliveredLettersFlags.ST02RiverLanterns)
                return false;
            if ((EligibleLetters & letter) == 0 || (LettersRead & letter) != 0)
                return false;

            LettersRead |= letter;
            return true;
        }

        public bool TryReviewRegisterNotes()
        {
            if (Phase != M16Phase.DeskLettersAndRegister || DeskRegisterReviewed)
                return false;

            DeskRegisterReviewed = true;
            return true;
        }

        public bool TryStepOutsideForEvening()
        {
            if (Phase != M16Phase.DeskLettersAndRegister)
                return false;
            Phase = M16Phase.FinalStandingAndFadeOut;
            return true;
        }

        public bool TryChooseStandingSpotAndCommit(
            M16StandingSpot spot,
            Func<string, int> getInt,
            Action<string, int> setInt)
        {
            if (Phase != M16Phase.FinalStandingAndFadeOut ||
                !Enum.IsDefined(typeof(M16StandingSpot), spot) ||
                getInt == null || setInt == null)
                return false;

            var record = new M16EpilogueRecord(
                StopsVisited,
                LettersRead,
                DeskRegisterReviewed,
                spot,
                ShenLiWillingToMeet,
                ClosingTone);

            if (!M16EpilogueFacts.TryRead(getInt, out M16EpilogueRecord? existing))
                return false;

            // Recover idempotently if a prior write committed ConfirmedKey = 1
            // before its acknowledgement was lost.
            if (existing.HasValue)
            {
                if (!Matches(record, existing.Value))
                    return false;
            }
            else if (getInt(M16EpilogueFacts.ConfirmedKey) != 0 ||
                     !M16EpilogueFacts.TryRecord(record, getInt, setInt))
            {
                return false;
            }

            if (!M16EpilogueFacts.TryRead(getInt, out M16EpilogueRecord? saved) ||
                !saved.HasValue || !Matches(record, saved.Value))
                return false;

            StandingSpot = spot;
            CompletedRecord = saved.Value;
            Phase = M16Phase.Completed;
            return true;
        }

        private static bool Matches(M16EpilogueRecord expected, M16EpilogueRecord saved)
        {
            return expected.StopsVisited == saved.StopsVisited &&
                expected.LettersReadMask == saved.LettersReadMask &&
                expected.DeskRegisterReviewed == saved.DeskRegisterReviewed &&
                expected.StandingSpot == saved.StandingSpot &&
                expected.ShenLiWillingToMeet == saved.ShenLiWillingToMeet &&
                expected.ClosingTone == saved.ClosingTone;
        }
    }
}
