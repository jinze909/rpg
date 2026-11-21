using System;

namespace Rpg.Soundwell
{
    public enum ST03BathhousePhase
    {
        InspectClosedBathhouse = 0,
        MeetResidentsAtCareHome = 1,
        RepairRouteAndTransport = 2,
        TrialReopening = 3,
        CompareOperatingPlans = 4,
        Completed = 5
    }

    public enum ST03BathhouseClue
    {
        LeakingBoilerAndUnpaidWorkers = 1,
        OldLockersAndRepairs = 2,
        ThreeLettersAndWuSuHandwriting = 3
    }

    /// <summary>
    /// Scene-callable ST-03 investigation and decision sequence. The letter's
    /// handwriting is investigated rather than assumed to be a ghost; care-home
    /// residents may refuse the downhill journey. A replacement schedule and
    /// overlooked carriage fare must be dealt with before a permanent decision.
    /// The scene host is responsible for proving these interactions actually
    /// took place before invoking the corresponding session methods.
    /// </summary>
    public sealed class ST03BathhouseSession
    {
        private int inspectedClues;
        private ST03BathhouseChoice? chosen;
        public ST03BathhousePhase Phase { get; private set; } =
            ST03BathhousePhase.InspectClosedBathhouse;
        public bool RouteCleared { get; private set; }
        public bool TripRetimed { get; private set; }
        public bool TrialCompleted { get; private set; }
        public bool TransportBudgetBalanced { get; private set; }
        public ST03BathhouseRecord? CompletedRecord { get; private set; }
        public int InspectedClueCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < 3; i++)
                    if ((inspectedClues & (1 << i)) != 0) count++;
                return count;
            }
        }

        public static bool TryCreate(Func<string, int> read,
            out ST03BathhouseSession session)
        {
            session = null;
            if (!ST03BathhouseFacts.TryRead(read, out ST03BathhouseRecord? existing) ||
                existing.HasValue ||
                // A previous write may be incomplete. Do not start a new
                // decision until the original save is recovered.
                read(ST03BathhouseFacts.ConfirmedKey) != 0)
                return false;
            session = new ST03BathhouseSession();
            return true;
        }

        public bool TryInspectClue(ST03BathhouseClue clue)
        {
            if (Phase != ST03BathhousePhase.InspectClosedBathhouse ||
                !Enum.IsDefined(typeof(ST03BathhouseClue), clue))
                return false;
            int bit = 1 << ((int)clue - 1);
            if ((inspectedClues & bit) != 0) return false;
            inspectedClues |= bit;
            if (inspectedClues == 7)
                Phase = ST03BathhousePhase.MeetResidentsAtCareHome;
            return true;
        }

        public bool TryInterviewWuSu(bool letterAuthorshipVerified,
            bool willingVisitorsHeard, bool residentsPreferringCareHomeHeard)
        {
            if (Phase != ST03BathhousePhase.MeetResidentsAtCareHome ||
                !letterAuthorshipVerified || !willingVisitorsHeard ||
                !residentsPreferringCareHomeHeard)
                return false;
            Phase = ST03BathhousePhase.RepairRouteAndTransport;
            return true;
        }

        public bool TryMakeSafeRoute(bool fallenShelfCleared,
            bool shelteredRestStopFound, bool returnVehicleTimetableUpdated)
        {
            if (Phase != ST03BathhousePhase.RepairRouteAndTransport ||
                !fallenShelfCleared || !shelteredRestStopFound ||
                !returnVehicleTimetableUpdated)
                return false;
            RouteCleared = true;
            TripRetimed = true;
            Phase = ST03BathhousePhase.TrialReopening;
            return true;
        }

        public bool TryCompleteTrialNight(bool drainCleared,
            bool towelsSorted, bool oldBathTicketsResolved)
        {
            if (Phase != ST03BathhousePhase.TrialReopening ||
                !drainCleared || !towelsSorted || !oldBathTicketsResolved)
                return false;
            TrialCompleted = true;
            return true;
        }

        public bool TryReconcileTransportBudget(bool newCarriageFareAccountedFor)
        {
            if (Phase != ST03BathhousePhase.TrialReopening ||
                !TrialCompleted || !newCarriageFareAccountedFor)
                return false;
            TransportBudgetBalanced = true;
            Phase = ST03BathhousePhase.CompareOperatingPlans;
            return true;
        }

        public bool TryChoose(ST03BathhouseChoice choice)
        {
            if (Phase != ST03BathhousePhase.CompareOperatingPlans ||
                chosen.HasValue || !Enum.IsDefined(typeof(ST03BathhouseChoice), choice))
                return false;
            chosen = choice;
            return true;
        }

        public bool TryCommit(Func<string, int> read, Action<string, int> write)
        {
            if (Phase != ST03BathhousePhase.CompareOperatingPlans ||
                !chosen.HasValue || !RouteCleared || !TripRetimed ||
                !TrialCompleted || !TransportBudgetBalanced ||
                read == null || write == null)
                return false;

            var expected = new ST03BathhouseRecord(chosen.Value,
                RouteCleared, TripRetimed, TrialCompleted, TransportBudgetBalanced);
            int marker = read(ST03BathhouseFacts.ConfirmedKey);
            if (marker == 1)
            {
                // The save may have succeeded before acknowledgement threw.
                if (!ST03BathhouseFacts.TryRead(read, out ST03BathhouseRecord? saved) ||
                    !saved.HasValue || saved.Value.Choice != expected.Choice)
                    return false;
                CompletedRecord = saved;
            }
            else if (marker == 0)
            {
                // ST03BathhouseFacts.CanRecord enforces matching partial values.
                if (!ST03BathhouseFacts.TryRecord(expected, read, write) ||
                    !ST03BathhouseFacts.TryRead(read, out ST03BathhouseRecord? saved) ||
                    !saved.HasValue || saved.Value.Choice != expected.Choice)
                    return false;
                CompletedRecord = saved;
            }
            else
            {
                return false;
            }

            Phase = ST03BathhousePhase.Completed;
            return true;
        }
    }
}
