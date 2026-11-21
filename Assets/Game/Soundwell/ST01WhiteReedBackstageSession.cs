using System;

namespace Rpg.Soundwell
{
    public enum ST01WhiteReedPhase
    {
        InspectBackstage = 0,
        HearLiuLingAndLiuMan = 1,
        DecideDeparture = 2,
        RescueA ShaPerformance = 3,
        ReadyToCommit = 4,
        Completed = 5
    }

    public enum ST01PerformanceRescue
    {
        UnnamedMaidOnStage = 1,
        VoiceBehindCurtain = 2
    }

    /// <summary>
    /// ST-01 first encounter at White Reed (借来的掌声).
    /// The performer chooses whether to rescue A Sha; the player can hand
    /// over props, prompt the missed lyric and calm audience refund demands.
    /// This session does not create a Jingwan meeting or mail a ticket.
    /// </summary>
    public sealed class ST01WhiteReedBackstageSession
    {
        private readonly ST01DepartureChoice? interruptedChoice;
        private ST01DepartureChoice? chosen;
        private ST01PerformanceRescue? rescue;

        private ST01WhiteReedBackstageSession(ST01DepartureChoice? interrupted)
        {
            interruptedChoice = interrupted;
        }

        public ST01WhiteReedPhase Phase { get; private set; } =
            ST01WhiteReedPhase.InspectBackstage;
        public ST01DepartureChoice? Departure => chosen;
        public ST01PerformanceRescue? PerformanceRescue => rescue;
        public bool DebtAndUnderstudyConsequencesRemain =>
            chosen == ST01DepartureChoice.StagedAbduction;

        public static bool TryCreate(Func<string, int> read,
            out ST01WhiteReedBackstageSession session)
        {
            session = null;
            if (read == null ||
                read(ST01RegionalStoryFacts.DepartureConfirmedKey) != 0 ||
                read(ST01RegionalStoryFacts.TheaterConfirmedKey) != 0 ||
                read(ST01RegionalStoryFacts.TheaterChoiceKey) != 0 ||
                read(ST01RegionalStoryFacts.TicketStatusKey) != 0)
                return false;

            int unfinished = read(ST01RegionalStoryFacts.DepartureChoiceKey);
            if (unfinished != 0 &&
                !Enum.IsDefined(typeof(ST01DepartureChoice), unfinished))
                return false;

            session = new ST01WhiteReedBackstageSession(unfinished == 0
                ? (ST01DepartureChoice?)null : (ST01DepartureChoice)unfinished);
            return true;
        }

        public bool TryInspectBackstage(bool ransomDraftsAndRedPencilSeen,
            bool aShaoRehearsalBookRead, bool liuManAdvanceLedgerRead,
            bool jingwanPassageTicketSeen)
        {
            if (Phase != ST01WhiteReedPhase.InspectBackstage ||
                !ransomDraftsAndRedPencilSeen || !aShaoRehearsalBookRead ||
                !liuManAdvanceLedgerRead || !jingwanPassageTicketSeen)
                return false;

            Phase = ST01WhiteReedPhase.HearLiuLingAndLiuMan;
            return true;
        }

        public bool TryHearBothVoices(bool liuLingExpressedHerOwnChoice,
            bool liuManExplainedAdvanceAndBorrowedUmbrella,
            bool aShaoWasInformedOfPerformanceDuties)
        {
            if (Phase != ST01WhiteReedPhase.HearLiuLingAndLiuMan ||
                !liuLingExpressedHerOwnChoice ||
                !liuManExplainedAdvanceAndBorrowedUmbrella ||
                !aShaoWasInformedOfPerformanceDuties)
                return false;

            Phase = ST01WhiteReedPhase.DecideDeparture;
            return true;
        }

        public bool TryChooseDeparture(ST01DepartureChoice choice)
        {
            if (Phase != ST01WhiteReedPhase.DecideDeparture ||
                !Enum.IsDefined(typeof(ST01DepartureChoice), choice) ||
                (interruptedChoice.HasValue && interruptedChoice.Value != choice))
                return false;

            chosen = choice;
            Phase = ST01WhiteReedPhase.RescueA ShaPerformance;
            return true;
        }

        public bool TryHelpA ShaAtCurtain(bool propHandedToStage,
            bool missedLyricPromptGiven, bool refundAudienceCalmed,
            bool liuLingPersonallyChoseToHelp,
            ST01PerformanceRescue observedRescue)
        {
            if (Phase != ST01WhiteReedPhase.RescueA ShaPerformance ||
                !chosen.HasValue || !propHandedToStage || !missedLyricPromptGiven ||
                !refundAudienceCalmed || !liuLingPersonallyChoseToHelp ||
                !Enum.IsDefined(typeof(ST01PerformanceRescue), observedRescue))
                return false;

            ST01PerformanceRescue expected =
                chosen.Value == ST01DepartureChoice.TalkTogether
                    ? ST01PerformanceRescue.UnnamedMaidOnStage
                    : ST01PerformanceRescue.VoiceBehindCurtain;
            if (observedRescue != expected)
                return false;
            rescue = observedRescue;
            Phase = ST01WhiteReedPhase.ReadyToCommit;
            return true;
        }

        public bool TryCommit(Func<string, int> read, Action<string, int> write)
        {
            if (Phase != ST01WhiteReedPhase.ReadyToCommit ||
                !chosen.HasValue || !rescue.HasValue ||
                read == null || write == null)
                return false;

            if (ST01RegionalStoryFacts.TryRead(read, out ST01RegionalOutcome existing))
            {
                if (existing.Departure.HasValue)
                {
                    if (existing.Departure.Value != chosen.Value ||
                        existing.Theater.HasValue)
                        return false;
                }
                else if (!ST01RegionalStoryFacts.TryRecordDeparture(
                    chosen.Value, read, write))
                    return false;
            }
            else if (!ST01RegionalStoryFacts.TryRecordDeparture(
                chosen.Value, read, write))
            {
                // The existing partial raw choice can only be recovered
                // with exactly the same documented backstage decision.
                return false;
            }

            if (!ST01RegionalStoryFacts.TryRead(read, out ST01RegionalOutcome saved) ||
                saved.Departure != chosen.Value || saved.Theater.HasValue ||
                saved.Ticket != ST01TicketStatus.NotSent)
                return false;

            Phase = ST01WhiteReedPhase.Completed;
            return true;
        }
    }
}
