using System;

namespace Rpg.Soundwell
{
    public enum ST05TwoDoorsPhase
    {
        UnblockStaircase = 0,
        TestMarketGifts = 1,
        ClearCourtyardConflict = 2,
        HearBothPartners = 3,
        AgreeHomeAndKeys = 4,
        AwaitChengOwnJobDecision = 5,
        Completed = 6
    }

    /// <summary>
    /// Scene-callable ST-05 narrative session. Yu He and Cheng Wu make all
    /// living and assignment decisions themselves; the player only clears
    /// obstacles, checks costs and communicates their own words.
    /// </summary>
    public sealed class ST05TwoDoorsSession
    {
        private ST05BedResolution? bed;
        private ST05HomeArrangement? arrangement;
        private bool mutualTrialConsent;
        private ST05ChengAssignment? assignment;

        public ST05TwoDoorsPhase Phase { get; private set; } =
            ST05TwoDoorsPhase.UnblockStaircase;
        public ST05HouseholdRecord? CompletedHome { get; private set; }
        public ST05ChengAssignment? CompletedAssignment { get; private set; }
        public bool IsCompleted => Phase == ST05TwoDoorsPhase.Completed;

        public static bool TryCreate(Func<string, int> read,
            out ST05TwoDoorsSession session)
        {
            session = null;
            if (read == null ||
                !ST05TwoDoorsFacts.TryRead(read, out ST05TwoDoorsOutcome? saved))
                return false;

            // A scene reload after the home agreement must resume Cheng's
            // own work decision, not strand the player behind HomeConfirmed.
            // A fully completed story cannot be reopened as a fresh session.
            if (saved.HasValue)
            {
                if (saved.Value.Assignment.HasValue)
                    return false;

                session = new ST05TwoDoorsSession
                {
                    bed = saved.Value.Home.Bed,
                    arrangement = saved.Value.Home.Arrangement,
                    mutualTrialConsent = saved.Value.Home.MutualTrialConsent,
                    CompletedHome = saved.Value.Home,
                    Phase = ST05TwoDoorsPhase.AwaitChengOwnJobDecision
                };
                return true;
            }

            session = new ST05TwoDoorsSession();
            return true;
        }

        public bool TryClearStaircase(ST05BedResolution resolution,
            bool returnHaulageFeePaid)
        {
            if (Phase != ST05TwoDoorsPhase.UnblockStaircase ||
                !Enum.IsDefined(typeof(ST05BedResolution), resolution) ||
                (resolution == ST05BedResolution.ReturnBedAndPayHaulage &&
                 !returnHaulageFeePaid))
                return false;
            bed = resolution;
            Phase = ST05TwoDoorsPhase.TestMarketGifts;
            return true;
        }

        public bool TryChoosePracticalGifts(bool drawingLampTested,
            bool coolingStoneCracksChecked, bool stallholderAdviceHeard)
        {
            if (Phase != ST05TwoDoorsPhase.TestMarketGifts ||
                !drawingLampTested || !coolingStoneCracksChecked ||
                !stallholderAdviceHeard)
                return false;
            Phase = ST05TwoDoorsPhase.ClearCourtyardConflict;
            return true;
        }

        public bool TryResolveSharedCourtyard(bool windblownMapsRecovered,
            bool mapRackMovedAwayFromOven, bool guestsRedirected)
        {
            if (Phase != ST05TwoDoorsPhase.ClearCourtyardConflict ||
                !windblownMapsRecovered || !mapRackMovedAwayFromOven ||
                !guestsRedirected)
                return false;
            Phase = ST05TwoDoorsPhase.HearBothPartners;
            return true;
        }

        public bool TryHearOwnWords(bool yuheSpokeToAunt,
            bool chengSpokeToFriends, bool shopAccessDiscussed,
            bool keysAndVisitorsDiscussed, bool travelCostsDiscussed)
        {
            if (Phase != ST05TwoDoorsPhase.HearBothPartners ||
                !yuheSpokeToAunt || !chengSpokeToFriends ||
                !shopAccessDiscussed || !keysAndVisitorsDiscussed ||
                !travelCostsDiscussed)
                return false;
            Phase = ST05TwoDoorsPhase.AgreeHomeAndKeys;
            return true;
        }

        public bool TryAgreeLivingArrangement(ST05HomeArrangement decidedByBoth,
            bool eachConsentedToSharedTrial)
        {
            if (Phase != ST05TwoDoorsPhase.AgreeHomeAndKeys ||
                !Enum.IsDefined(typeof(ST05HomeArrangement), decidedByBoth) ||
                eachConsentedToSharedTrial !=
                    (decidedByBoth == ST05HomeArrangement.MutuallyAgreedTrialSharedHomeAndStudio))
                return false;

            // The same witnessed agreement may be retried after a torn save;
            // a different arrangement or consent state may never replace it.
            if (arrangement.HasValue)
                return arrangement.Value == decidedByBoth &&
                    mutualTrialConsent == eachConsentedToSharedTrial;

            arrangement = decidedByBoth;
            mutualTrialConsent = eachConsentedToSharedTrial;
            return true;
        }

        public bool TryCommitLivingAgreement(Func<string, int> read,
            Action<string, int> write)
        {
            if (Phase != ST05TwoDoorsPhase.AgreeHomeAndKeys ||
                !bed.HasValue || !arrangement.HasValue ||
                read == null || write == null)
                return false;
            var expected = new ST05HouseholdRecord(
                bed.Value, arrangement.Value, mutualTrialConsent);
            // Recover a successful store with a lost acknowledgement. A home
            // write that stopped early may only retry its original intent.
            if (read(ST05TwoDoorsFacts.HomeConfirmedKey) == 1)
            {
                if (!ST05TwoDoorsFacts.TryRead(read, out ST05TwoDoorsOutcome? saved) ||
                    !saved.HasValue || !Matches(expected, saved.Value.Home))
                    return false;
            }
            else if (!ST05TwoDoorsFacts.TryRecordHome(expected, read, write))
                return false;

            if (!ST05TwoDoorsFacts.TryRead(read, out ST05TwoDoorsOutcome? committed) ||
                !committed.HasValue || !Matches(expected, committed.Value.Home))
                return false;
            CompletedHome = committed.Value.Home;
            Phase = ST05TwoDoorsPhase.AwaitChengOwnJobDecision;
            return true;
        }

        public bool TryHearChengAssignment(ST05ChengAssignment hisOwnChoice,
            bool chengPersonallyAcceptedOrDeclined,
            bool shopCoverArranged, bool keysChecked, bool returnDateTalkedThrough)
        {
            if (Phase != ST05TwoDoorsPhase.AwaitChengOwnJobDecision ||
                !Enum.IsDefined(typeof(ST05ChengAssignment), hisOwnChoice) ||
                !chengPersonallyAcceptedOrDeclined || !shopCoverArranged ||
                !keysChecked || !returnDateTalkedThrough)
                return false;

            // The same personal decision can be re-acknowledged after an
            // interrupted commit; the player's alternative cannot replace it.
            if (assignment.HasValue)
                return assignment.Value == hisOwnChoice;

            assignment = hisOwnChoice;
            return true;
        }

        public bool TryCommitChengAssignment(Func<string, int> read,
            Action<string, int> write)
        {
            if (Phase != ST05TwoDoorsPhase.AwaitChengOwnJobDecision ||
                !assignment.HasValue || read == null || write == null ||
                !ST05TwoDoorsFacts.TryRecordAssignment(assignment.Value, read, write))
                return false;
            if (!ST05TwoDoorsFacts.TryRead(read, out ST05TwoDoorsOutcome? saved) ||
                !saved.HasValue || saved.Value.Assignment != assignment)
                return false;
            CompletedAssignment = assignment;
            Phase = ST05TwoDoorsPhase.Completed;
            return true;
        }

        private static bool Matches(ST05HouseholdRecord a, ST05HouseholdRecord b)
        {
            return a.Bed == b.Bed &&
                a.Arrangement == b.Arrangement &&
                a.MutualTrialConsent == b.MutualTrialConsent;
        }
    }
}