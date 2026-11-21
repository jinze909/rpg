using System;

namespace Rpg.Soundwell
{
    public enum ST05Stage
    {
        StaircaseBedBlockage = 0,
        FleaMarketGifts = 1,
        YardDinnerMediation = 2,
        LivingArrangementChoice = 3,
        ChengAssignmentDecision = 4,
        Completed = 5
    }

    /// <summary>
    /// Authoritative ST-05 ("两扇门的乔迁宴") state machine.
    /// Guides the player through staircase bed removal, selecting both practical
    /// flea-market gifts, resolving the courtyard map/oven wind incident,
    /// confirming a mutual living arrangement, and recording Cheng Wu's own
    /// local vs out-of-town survey decision with retry-safe Makinom persistence.
    /// </summary>
    public sealed class ST05TwoDoorsSession
    {
        public ST05Stage Stage { get; private set; } = ST05Stage.StaircaseBedBlockage;
        public ST05BedHandling? BedHandling { get; private set; }
        public ST05GiftChecks GiftsSelected { get; private set; } = ST05GiftChecks.None;
        public bool YardIncidentResolved { get; private set; }
        public ST05LivingArrangement? LivingArrangement { get; private set; }
        public bool BothConsentedToCohabit { get; private set; }
        public ST05ChengAssignment? ChengAssignment { get; private set; }
        public ST05TwoDoorsRecord? CompletedRecord { get; private set; }
        public bool IsCompleted => Stage == ST05Stage.Completed;

        public bool TryHandleStuckBed(ST05BedHandling handling)
        {
            if (Stage != ST05Stage.StaircaseBedBlockage ||
                !Enum.IsDefined(typeof(ST05BedHandling), handling))
                return false;

            BedHandling = handling;
            Stage = ST05Stage.FleaMarketGifts;
            return true;
        }

        public bool TrySelectGift(ST05GiftChecks gift)
        {
            if (Stage != ST05Stage.FleaMarketGifts)
                return false;
            if (gift != ST05GiftChecks.NightDraftingLamp &&
                gift != ST05GiftChecks.BreadCoolingStoneSlab)
                return false;
            if ((GiftsSelected & gift) != 0)
                return false;

            GiftsSelected |= gift;
            if (GiftsSelected == ST05GiftChecks.All)
                Stage = ST05Stage.YardDinnerMediation;
            return true;
        }

        public bool TryResolveYardDinnerIncident()
        {
            if (Stage != ST05Stage.YardDinnerMediation ||
                GiftsSelected != ST05GiftChecks.All)
                return false;

            YardIncidentResolved = true;
            Stage = ST05Stage.LivingArrangementChoice;
            return true;
        }

        public bool TryChooseLivingArrangement(
            ST05LivingArrangement arrangement,
            bool yuHeConsented,
            bool chengWuConsented)
        {
            if (Stage != ST05Stage.LivingArrangementChoice ||
                !Enum.IsDefined(typeof(ST05LivingArrangement), arrangement))
                return false;

            if (arrangement == ST05LivingArrangement.TrialCohabitAndStudio)
            {
                if (!yuHeConsented || !chengWuConsented)
                    return false;
                BothConsentedToCohabit = true;
            }
            else
            {
                BothConsentedToCohabit = false;
            }

            LivingArrangement = arrangement;
            Stage = ST05Stage.ChengAssignmentDecision;
            return true;
        }

        public bool TryRecordChengAssignmentAndCommit(
            ST05ChengAssignment chengOwnDecision,
            Func<string, int> getInt,
            Action<string, int> setInt)
        {
            if (Stage != ST05Stage.ChengAssignmentDecision ||
                !BedHandling.HasValue ||
                GiftsSelected != ST05GiftChecks.All ||
                !YardIncidentResolved ||
                !LivingArrangement.HasValue ||
                !Enum.IsDefined(typeof(ST05ChengAssignment), chengOwnDecision) ||
                getInt == null || setInt == null)
                return false;

            var record = new ST05TwoDoorsRecord(
                BedHandling.Value,
                GiftsSelected,
                YardIncidentResolved,
                LivingArrangement.Value,
                BothConsentedToCohabit,
                chengOwnDecision);

            if (!ST05TwoDoorsFacts.TryRead(getInt, out ST05TwoDoorsRecord? existing))
                return false;

            // Recover idempotently if a prior write committed ConfirmedKey = 1
            // before its acknowledgement was lost.
            if (existing.HasValue)
            {
                if (!Matches(record, existing.Value))
                    return false;
            }
            else if (getInt(ST05TwoDoorsFacts.ConfirmedKey) != 0 ||
                     !ST05TwoDoorsFacts.TryRecord(record, getInt, setInt))
            {
                return false;
            }

            if (!ST05TwoDoorsFacts.TryRead(getInt, out ST05TwoDoorsRecord? saved) ||
                !saved.HasValue || !Matches(record, saved.Value))
                return false;

            ChengAssignment = chengOwnDecision;
            CompletedRecord = saved.Value;
            Stage = ST05Stage.Completed;
            return true;
        }

        private static bool Matches(ST05TwoDoorsRecord expected, ST05TwoDoorsRecord saved)
        {
            return expected.BedHandling == saved.BedHandling &&
                expected.GiftsSelected == saved.GiftsSelected &&
                expected.YardIncidentResolved == saved.YardIncidentResolved &&
                expected.LivingArrangement == saved.LivingArrangement &&
                expected.BothConsentedToCohabit == saved.BothConsentedToCohabit &&
                expected.ChengAssignment == saved.ChengAssignment;
        }
    }
}
