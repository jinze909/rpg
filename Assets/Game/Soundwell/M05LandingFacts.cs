using System;
using GamingIsLove.Makinom;

namespace Rpg.Soundwell
{
    /// <summary>
    /// What the player actually verified after reaching the Red Salt outer
    /// harbor. Ready rescue berths are accessible now, not merely the berths
    /// reserved on the M04 departure manifest. Missing tender or receiver
    /// therefore leaves zero usable berths for this landing.
    /// </summary>
    public readonly struct M05LandingReport
    {
        public M05LandingReport(int crewArrived, int readyRescueBerths,
            bool tenderArrived, bool receiverMet)
        {
            CrewArrived = crewArrived;
            ReadyRescueBerths = readyRescueBerths;
            TenderArrived = tenderArrived;
            ReceiverMet = receiverMet;
        }

        public int CrewArrived { get; }
        public int ReadyRescueBerths { get; }
        public bool TenderArrived { get; }
        public bool ReceiverMet { get; }
        public bool HasUsableLandingCapacity =>
            TenderArrived && ReceiverMet && ReadyRescueBerths > 0;
    }

    /// <summary>
    /// One confirmed M05 arrival snapshot per voyage, stored in the existing
    /// Makinom variable authority. The host must call TryRecord only after
    /// actual arrival and checking the crew, tender, receiver and available
    /// rescue berths. A M03 route selection, M04 readiness confirmation or
    /// completed fog mini-game never creates an arrival automatically.
    /// </summary>
    public static class M05LandingFacts
    {
        public const string ConfirmedKey = "rpg.return.m05.landing-confirmed";
        public const string CrewArrivedKey = "rpg.return.m05.landing-crew-arrived";
        public const string ReadyBerthsKey = "rpg.return.m05.landing-ready-berths";
        public const string TenderArrivedKey = "rpg.return.m05.landing-tender-arrived";
        public const string ReceiverMetKey = "rpg.return.m05.landing-receiver-met";

        public static bool TryRecord(M05LandingReport report)
        {
            var variables = Maki.Initialized ? Maki.Game?.Variables : null;
            return variables != null && TryRecord(report,
                key => variables.GetInt(key),
                (key, value) => variables.Set(key, value));
        }

        public static bool TryRecord(M05LandingReport report,
            Func<string, int> getInt, Action<string, int> setInt)
        {
            if (getInt == null || setInt == null ||
                getInt(ConfirmedKey) != 0 ||
                !TryReadCompletedCrossing(getInt, out M04DepartureRecord departure) ||
                !IsValidReport(report, departure))
                return false;

            // An interrupted Makinom write cannot publish a partial arrival.
            // Keep confirmation last, as with the M04 departure and fog facts.
            setInt(ConfirmedKey, 0);
            setInt(CrewArrivedKey, report.CrewArrived);
            setInt(ReadyBerthsKey, report.ReadyRescueBerths);
            setInt(TenderArrivedKey, report.TenderArrived ? 1 : 0);
            setInt(ReceiverMetKey, report.ReceiverMet ? 1 : 0);
            setInt(ConfirmedKey, 1);
            return true;
        }

        public static bool TryRead(out M05LandingReport? report)
        {
            report = null;
            var variables = Maki.Initialized ? Maki.Game?.Variables : null;
            return variables != null && TryRead(
                key => variables.GetInt(key), out report);
        }

        public static bool TryRead(Func<string, int> getInt,
            out M05LandingReport? report)
        {
            report = null;
            if (getInt == null) return false;
            if (getInt(ConfirmedKey) != 1 ||
                !TryReadCompletedCrossing(getInt, out M04DepartureRecord departure))
                return true;

            int tender = getInt(TenderArrivedKey);
            int receiver = getInt(ReceiverMetKey);
            if ((tender != 0 && tender != 1) ||
                (receiver != 0 && receiver != 1))
                return true;

            var saved = new M05LandingReport(getInt(CrewArrivedKey),
                getInt(ReadyBerthsKey), tender == 1, receiver == 1);
            if (IsValidReport(saved, departure))
                report = saved;
            return true;
        }

        public static bool TryResetForNewVisit()
        {
            var variables = Maki.Initialized ? Maki.Game?.Variables : null;
            return variables != null && TryResetForNewVisit(
                (key, value) => variables.Set(key, value));
        }

        public static bool TryResetForNewVisit(Action<string, int> setInt)
        {
            if (setInt == null) return false;
            setInt(ConfirmedKey, 0);
            setInt(CrewArrivedKey, 0);
            setInt(ReadyBerthsKey, 0);
            setInt(TenderArrivedKey, 0);
            setInt(ReceiverMetKey, 0);
            return true;
        }

        private static bool TryReadCompletedCrossing(Func<string, int> getInt,
            out M04DepartureRecord departure)
        {
            departure = default;
            if (!M04DepartureFacts.TryRead(getInt, out M04DepartureRecord? ready) ||
                !ready.HasValue ||
                !M04DepartureFacts.TryReadSailed(getInt, out bool sailed) ||
                !sailed ||
                !FogListeningFacts.TryRead(getInt, out FogListeningRecord? fog) ||
                !fog.HasValue || !fog.Value.Succeeded)
                return false;

            departure = ready.Value;
            return true;
        }

        private static bool IsValidReport(M05LandingReport report,
            M04DepartureRecord departure)
        {
            if (report.CrewArrived <= 0 ||
                report.CrewArrived > departure.AssignedCrew ||
                report.ReadyRescueBerths < 0 ||
                report.ReadyRescueBerths > departure.AvailableBerths)
                return false;

            // A boat still at sea or an unmet receiver is not transferable
            // rescue capacity, even if the original hull had free berths.
            return report.ReadyRescueBerths == 0 ||
                (report.TenderArrived && report.ReceiverMet);
        }
    }
}
