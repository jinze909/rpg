using System;
using GamingIsLove.Makinom;

namespace Rpg.Soundwell
{
    public enum ST01DepartureChoice
    {
        TalkTogether = 1,
        StagedAbduction = 2
    }

    public enum ST01TheaterChoice
    {
        RewritePlayWithFundingRisk = 1,
        KeepContractAndHearResidents = 2,
        PublicBoycott = 3
    }

    public enum ST01TicketStatus
    {
        NotSent = 0,
        Sent = 1,
        Delivered = 2
    }

    public enum ST01TicketInscription
    {
        None = 0,
        GeneralInvitation = 1,
        WelcomeToWatch = 2,
        ThanksForHelping = 3
    }

    /// <summary>
    /// ST-01 ("借来的掌声") regional outcome. The optional White Reed meeting
    /// is not invented when the player first meets Liu Ling at Jingwan.
    /// A ticket only appears at M15 after a separately recorded send AND delivery.
    /// The unsigned general invitation is used if the earlier meeting never happened.
    /// </summary>
    public readonly struct ST01RegionalOutcome
    {
        public ST01RegionalOutcome(ST01DepartureChoice? departure,
            ST01TheaterChoice? theater, ST01TicketStatus ticket)
        {
            Departure = departure;
            Theater = theater;
            Ticket = ticket;
        }

        public ST01DepartureChoice? Departure { get; }
        public ST01TheaterChoice? Theater { get; }
        public ST01TicketStatus Ticket { get; }

        public bool ShowM15Ticket =>
            Theater.HasValue && Theater.Value != ST01TheaterChoice.PublicBoycott &&
            Ticket == ST01TicketStatus.Delivered;

        public ST01TicketInscription M15TicketInscription
        {
            get
            {
                if (!ShowM15Ticket)
                    return ST01TicketInscription.None;
                if (!Departure.HasValue)
                    return ST01TicketInscription.GeneralInvitation;
                return Departure.Value == ST01DepartureChoice.TalkTogether
                    ? ST01TicketInscription.WelcomeToWatch
                    : ST01TicketInscription.ThanksForHelping;
            }
        }
    }

    /// <summary>
    /// Makinom-owned persistent facts for ST-01 (SCENE_SCRIPT_ST01.md).
    /// Call the departure/theater writers only after the corresponding physical
    /// backstage or theater interactions actually happened; these methods never
    /// fabricate visits, witnesses, performances, mailed tickets or reconciliation.
    /// The three milestones are independent so White Reed remains saved before
    /// the later Jingwan scene. A new voyage does not erase regional-story facts.
    /// </summary>
    public static class ST01RegionalStoryFacts
    {
        public const string DepartureConfirmedKey = "rpg.return.st01.departure-confirmed";
        public const string DepartureChoiceKey = "rpg.return.st01.departure-choice";
        public const string TheaterConfirmedKey = "rpg.return.st01.theater-confirmed";
        public const string TheaterChoiceKey = "rpg.return.st01.theater-choice";
        public const string TicketStatusKey = "rpg.return.st01.ticket-status";

        public static bool TryRead(out ST01RegionalOutcome outcome)
        {
            outcome = default;
            var variables = Maki.Initialized ? Maki.Game?.Variables : null;
            return variables != null && TryRead(
                key => variables.GetInt(key), out outcome);
        }

        public static bool TryRead(Func<string, int> read, out ST01RegionalOutcome outcome)
        {
            outcome = default;
            if (read == null)
                return false;

            int firstConfirmed = read(DepartureConfirmedKey);
            int firstRaw = read(DepartureChoiceKey);
            int theaterConfirmed = read(TheaterConfirmedKey);
            int theaterRaw = read(TheaterChoiceKey);
            int ticketRaw = read(TicketStatusKey);

            if ((firstConfirmed != 0 && firstConfirmed != 1) ||
                (theaterConfirmed != 0 && theaterConfirmed != 1) ||
                (!Enum.IsDefined(typeof(ST01TicketStatus), ticketRaw)))
                return false;

            // Nonzero choices without their commit marker are torn writes, not
            // proof of an encounter. They must not unlock later story rewards.
            if ((firstConfirmed == 0 && firstRaw != 0) ||
                (theaterConfirmed == 0 && theaterRaw != 0) ||
                (firstConfirmed == 1 && !Enum.IsDefined(typeof(ST01DepartureChoice), firstRaw)) ||
                (theaterConfirmed == 1 && !Enum.IsDefined(typeof(ST01TheaterChoice), theaterRaw)))
                return false;

            ST01DepartureChoice? departure = firstConfirmed == 1
                ? (ST01DepartureChoice?)firstRaw : null;
            ST01TheaterChoice? theater = theaterConfirmed == 1
                ? (ST01TheaterChoice?)theaterRaw : null;
            ST01TicketStatus ticket = (ST01TicketStatus)ticketRaw;

            // Do not grant a correspondence reward to an unplayed theater
            // outcome or to a boycott that has not been explicitly reconciled.
            // Reconciliation requires a future, separately verified increment.
            if (ticket != ST01TicketStatus.NotSent &&
                (!theater.HasValue || theater.Value == ST01TheaterChoice.PublicBoycott))
                return false;

            outcome = new ST01RegionalOutcome(departure, theater, ticket);
            return true;
        }

        public static bool TryRecordDeparture(ST01DepartureChoice choice,
            Func<string, int> read, Action<string, int> write)
        {
            if (read == null || write == null ||
                !Enum.IsDefined(typeof(ST01DepartureChoice), choice))
                return false;

            int confirmed = read(DepartureConfirmedKey);
            int existing = read(DepartureChoiceKey);
            if (confirmed == 1)
                return existing == (int)choice;
            if (confirmed != 0 || (existing != 0 && existing != (int)choice) ||
                read(TheaterConfirmedKey) != 0 || read(TheaterChoiceKey) != 0 ||
                read(TicketStatusKey) != 0)
                return false;

            // Write the commit marker LAST. An interrupted write may resume
            // only with the exact same choice, never replace an earlier decision.
            write(DepartureChoiceKey, (int)choice);
            write(DepartureConfirmedKey, 1);
            return true;
        }

        public static bool TryRecordTheater(ST01TheaterChoice choice,
            Func<string, int> read, Action<string, int> write)
        {
            if (read == null || write == null ||
                !Enum.IsDefined(typeof(ST01TheaterChoice), choice))
                return false;
            int confirmed = read(TheaterConfirmedKey);
            int existing = read(TheaterChoiceKey);
            if (confirmed == 1)
                return existing == (int)choice && TryRead(read, out _);
            if (confirmed != 0 || (existing != 0 && existing != (int)choice) ||
                read(TicketStatusKey) != 0)
                return false;

            // A player may first meet Liu Ling here; absence of an earlier
            // departure record must stay absent, not be treated as a failure.
            if (!TryRead(read, out _))
                return false;
            write(TheaterChoiceKey, (int)choice);
            write(TheaterConfirmedKey, 1);
            return true;
        }

        public static bool TryMarkTicketSent(Func<string, int> read, Action<string, int> write)
        {
            if (read == null || write == null ||
                !TryRead(read, out ST01RegionalOutcome outcome) ||
                !outcome.Theater.HasValue ||
                outcome.Theater.Value == ST01TheaterChoice.PublicBoycott)
                return false;
            if (outcome.Ticket != ST01TicketStatus.NotSent)
                return true;
            write(TicketStatusKey, (int)ST01TicketStatus.Sent);
            return true;
        }

        public static bool TryMarkTicketDelivered(Func<string, int> read, Action<string, int> write)
        {
            if (read == null || write == null ||
                !TryRead(read, out ST01RegionalOutcome outcome) ||
                !outcome.Theater.HasValue ||
                outcome.Theater.Value == ST01TheaterChoice.PublicBoycott ||
                outcome.Ticket == ST01TicketStatus.NotSent)
                return false;
            if (outcome.Ticket == ST01TicketStatus.Delivered)
                return true;
            write(TicketStatusKey, (int)ST01TicketStatus.Delivered);
            return true;
        }

        /// <summary>Explicit new-game reset only. Never call on an M03/M04 voyage replay.</summary>
        public static bool TryResetForNewGame(Action<string, int> write)
        {
            if (write == null) return false;
            write(DepartureConfirmedKey, 0);
            write(TheaterConfirmedKey, 0);
            write(TicketStatusKey, 0);
            write(DepartureChoiceKey, 0);
            write(TheaterChoiceKey, 0);
            return true;
        }
    }
}
