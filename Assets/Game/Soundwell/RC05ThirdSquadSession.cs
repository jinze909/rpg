using System;

namespace Rpg.Soundwell
{
    public enum RC05Stage
    {
        MountainPassTracking = 0,
        SaltGrainBoatTrial = 1,
        MillMeeting = 2,
        ThreePartyDecision = 3,
        Completed = 4
    }

    /// <summary>
    /// Authoritative RC-05 ("山上的第三班") tracking, mill meeting, real
    /// Zheng Yuan identity verification, and three-party resolution state machine.
    /// </summary>
    public sealed class RC05ThirdSquadSession
    {
        public RC05Stage Stage { get; private set; } = RC05Stage.MountainPassTracking;
        public bool OldWomanStoneTriggered { get; }
        public bool RollCallObserved { get; private set; }
        public bool TrialDeliveryVerified { get; private set; }
        public bool OldWomanStoneMentioned { get; private set; }
        public bool RealZhengYuanVerified { get; private set; }
        public bool SkirmishOccurred { get; private set; }
        public RC05ThirdSquadRecord? CompletedRecord { get; private set; }
        public bool IsResolved => Stage == RC05Stage.Completed;

        public RC05ThirdSquadSession(bool oldWomanStoneTriggered = false)
        {
            OldWomanStoneTriggered = oldWomanStoneTriggered;
        }

        public static bool TryCreateFromSavedFacts(
            Func<string, int> getInt,
            bool oldWomanStoneTriggered,
            out RC05ThirdSquadSession session)
        {
            session = null;
            if (getInt == null)
                return false;
            if (!M08FirstWorldNoticeFacts.TryCaseAvailable(
                    RC05ThirdSquadFacts.RosterCaseNumber, getInt, out bool available) ||
                !available)
                return false;

            session = new RC05ThirdSquadSession(oldWomanStoneTriggered);
            return true;
        }

        public bool TryObserveMountainPassRollCall()
        {
            if (Stage != RC05Stage.MountainPassTracking)
                return false;
            RollCallObserved = true;
            Stage = RC05Stage.SaltGrainBoatTrial;
            return true;
        }

        public bool TryVerifySaltGrainBoatDelivery(bool boatmanCountMatched)
        {
            if (Stage != RC05Stage.SaltGrainBoatTrial || !boatmanCountMatched)
                return false;
            TrialDeliveryVerified = true;
            Stage = RC05Stage.MillMeeting;
            return true;
        }

        public bool TryMentionOldWomanAtStele()
        {
            if (Stage != RC05Stage.MillMeeting ||
                !OldWomanStoneTriggered || OldWomanStoneMentioned)
                return false;
            OldWomanStoneMentioned = true;
            return true;
        }

        /// <summary>
        /// Real Zheng Yuan identity cannot be inferred from left-handedness
        /// alone; requires checking the original squad roster and confirming
        /// personal/comrade testimony.
        /// </summary>
        public bool TryVerifyRealZhengYuan(bool squadRosterChecked, bool testimonyConfirmed)
        {
            if (Stage != RC05Stage.MillMeeting ||
                RealZhengYuanVerified ||
                !squadRosterChecked || !testimonyConfirmed)
                return false;
            RealZhengYuanVerified = true;
            return true;
        }

        public bool TryProceedToThreePartyTalks(bool skirmishTriggered = false)
        {
            if (Stage != RC05Stage.MillMeeting)
                return false;
            SkirmishOccurred = skirmishTriggered;
            Stage = RC05Stage.ThreePartyDecision;
            return true;
        }

        public bool TrySettleResolution(
            RC05Resolution resolution,
            Func<string, int> getInt,
            Action<string, int> setInt)
        {
            if (Stage != RC05Stage.ThreePartyDecision ||
                !Enum.IsDefined(typeof(RC05Resolution), resolution) ||
                getInt == null || setInt == null)
                return false;

            var record = new RC05ThirdSquadRecord(
                TrialDeliveryVerified,
                resolution,
                SkirmishOccurred,
                RealZhengYuanVerified,
                OldWomanStoneMentioned);

            if (!RC05ThirdSquadFacts.TryRead(getInt, out RC05ThirdSquadRecord? existing))
                return false;

            // Recover idempotently if a prior write committed ConfirmedKey = 1
            // before its acknowledgement was lost.
            if (existing.HasValue)
            {
                if (!Matches(record, existing.Value))
                    return false;
            }
            else if (getInt(RC05ThirdSquadFacts.ConfirmedKey) != 0 ||
                     !RC05ThirdSquadFacts.TryRecord(record, getInt, setInt))
            {
                return false;
            }

            if (!RC05ThirdSquadFacts.TryRead(getInt, out RC05ThirdSquadRecord? saved) ||
                !saved.HasValue || !Matches(record, saved.Value))
                return false;

            CompletedRecord = saved.Value;
            Stage = RC05Stage.Completed;
            return true;
        }

        private static bool Matches(RC05ThirdSquadRecord expected, RC05ThirdSquadRecord saved)
        {
            return expected.TrialDeliveryVerified == saved.TrialDeliveryVerified &&
                expected.Resolution == saved.Resolution &&
                expected.SkirmishOccurred == saved.SkirmishOccurred &&
                expected.RealZhengYuanVerified == saved.RealZhengYuanVerified &&
                expected.OldWomanStoneMentioned == saved.OldWomanStoneMentioned;
        }
    }
}
