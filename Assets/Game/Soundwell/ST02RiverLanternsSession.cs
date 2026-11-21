using System;

namespace Rpg.Soundwell
{
    public enum ST02Stage
    {
        WorkshopInspection = 0,
        DockTrialAndFeedback = 1,
        TrialDisclosureChoice = 2,
        BiddingDecision = 3,
        OldLockRescue = 4,
        Completed = 5
    }

    /// <summary>
    /// Authoritative ST-02 ("河面上两盏头灯") state machine.
    /// Enforces inspecting both workshops, gathering dock feedback, choosing
    /// trial-result disclosure and bidding mode, and resolving the old-lock
    /// emergency with retry-safe idempotent Makinom persistence.
    /// </summary>
    public sealed class ST02RiverLanternsSession
    {
        public ST02Stage Stage { get; private set; } = ST02Stage.WorkshopInspection;
        public ST02InspectionFlags Inspections { get; private set; } = ST02InspectionFlags.None;
        public bool OriginalWordsRelayed { get; private set; }
        public bool DockFeedbackGathered { get; private set; }
        public ST02TrialDisclosure? TrialDisclosure { get; private set; }
        public ST02BidMode? BidMode { get; private set; }
        public ST02LockRescueMethod? LockRescue { get; private set; }
        public ST02RiverLanternsRecord? CompletedRecord { get; private set; }
        public bool IsCompleted => Stage == ST02Stage.Completed;

        public bool TryInspectBoat(ST02InspectionFlags boat)
        {
            if (Stage != ST02Stage.WorkshopInspection)
                return false;
            if (boat != ST02InspectionFlags.XieZhiOldBoat &&
                boat != ST02InspectionFlags.WenShengNewBoat)
                return false;
            if ((Inspections & boat) != 0)
                return false;

            Inspections |= boat;
            if (Inspections == ST02InspectionFlags.All)
                Stage = ST02Stage.DockTrialAndFeedback;
            return true;
        }

        public bool TryRelayOriginalWords()
        {
            if ((Stage != ST02Stage.WorkshopInspection &&
                 Stage != ST02Stage.DockTrialAndFeedback) ||
                Inspections != ST02InspectionFlags.All ||
                OriginalWordsRelayed)
                return false;

            OriginalWordsRelayed = true;
            return true;
        }

        public bool TryGatherDockFeedback()
        {
            if (Stage != ST02Stage.DockTrialAndFeedback ||
                Inspections != ST02InspectionFlags.All)
                return false;

            DockFeedbackGathered = true;
            Stage = ST02Stage.TrialDisclosureChoice;
            return true;
        }

        public bool TryChooseTrialDisclosure(ST02TrialDisclosure disclosure)
        {
            if (Stage != ST02Stage.TrialDisclosureChoice ||
                !Enum.IsDefined(typeof(ST02TrialDisclosure), disclosure))
                return false;

            TrialDisclosure = disclosure;
            Stage = ST02Stage.BiddingDecision;
            return true;
        }

        public bool TryChooseBidMode(ST02BidMode bidMode)
        {
            if (Stage != ST02Stage.BiddingDecision ||
                !Enum.IsDefined(typeof(ST02BidMode), bidMode))
                return false;

            BidMode = bidMode;
            Stage = ST02Stage.OldLockRescue;
            return true;
        }

        public bool TryResolveOldLockAndCommit(
            ST02LockRescueMethod rescueMethod,
            Func<string, int> getInt,
            Action<string, int> setInt)
        {
            if (Stage != ST02Stage.OldLockRescue ||
                !DockFeedbackGathered ||
                !TrialDisclosure.HasValue ||
                !BidMode.HasValue ||
                !Enum.IsDefined(typeof(ST02LockRescueMethod), rescueMethod) ||
                getInt == null || setInt == null)
                return false;

            var record = new ST02RiverLanternsRecord(
                Inspections,
                OriginalWordsRelayed,
                DockFeedbackGathered,
                TrialDisclosure.Value,
                BidMode.Value,
                rescueMethod);

            if (!ST02RiverLanternsFacts.TryRead(getInt, out ST02RiverLanternsRecord? existing))
            {
                // A missing confirmation with a matching pending signature
                // is recoverable; a different choice or corrupted record is not.
                if (getInt(ST02RiverLanternsFacts.ConfirmedKey) != 0 ||
                    !ST02RiverLanternsFacts.CanRecord(record, getInt))
                    return false;
            }

            // Recover idempotently if a prior write committed ConfirmedKey = 1
            // before its acknowledgement was lost.
            if (existing.HasValue)
            {
                if (!Matches(record, existing.Value))
                    return false;
            }
            else if (getInt(ST02RiverLanternsFacts.ConfirmedKey) != 0 ||
                     !ST02RiverLanternsFacts.TryRecord(record, getInt, setInt))
            {
                return false;
            }

            if (!ST02RiverLanternsFacts.TryRead(getInt, out ST02RiverLanternsRecord? saved) ||
                !saved.HasValue || !Matches(record, saved.Value))
                return false;

            LockRescue = rescueMethod;
            CompletedRecord = saved.Value;
            Stage = ST02Stage.Completed;
            return true;
        }

        private static bool Matches(ST02RiverLanternsRecord expected, ST02RiverLanternsRecord saved)
        {
            return expected.Inspections == saved.Inspections &&
                expected.OriginalWordsRelayed == saved.OriginalWordsRelayed &&
                expected.DockFeedbackGathered == saved.DockFeedbackGathered &&
                expected.TrialDisclosure == saved.TrialDisclosure &&
                expected.BidMode == saved.BidMode &&
                expected.LockRescue == saved.LockRescue;
        }
    }
}
