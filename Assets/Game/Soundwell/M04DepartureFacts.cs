using System;
using GamingIsLove.Makinom;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Immutable snapshot of an actually confirmed M04 departure read back from
    /// the single existing Makinom variable store.
    /// </summary>
    public readonly struct M04DepartureRecord
    {
        public M04DepartureRecord(
            M03VoyageChoice route,
            int assignedCrew,
            int availableBerths,
            int returneesToBook,
            bool officialRegistrationEffective,
            bool escortFlagAccepted)
        {
            Route = route;
            AssignedCrew = assignedCrew;
            AvailableBerths = availableBerths;
            ReturneesToBook = returneesToBook;
            OfficialRegistrationEffective = officialRegistrationEffective;
            EscortFlagAccepted = escortFlagAccepted;
        }

        public M03VoyageChoice Route { get; }
        public int AssignedCrew { get; }
        public int AvailableBerths { get; }
        public int ReturneesToBook { get; }
        public bool OfficialRegistrationEffective { get; }
        public bool EscortFlagAccepted { get; }
    }

    /// <summary>
    /// Persists and reads back confirmed M04 departure readiness facts through
    /// Makinom's existing global variable store (single RPG/ORK save authority).
    /// Unconfirmed or blocked manifests are rejected, and replays must clear
    /// prior voyage records before a new readiness check begins.
    /// </summary>
    public static class M04DepartureFacts
    {
        public const string ConfirmedKey = "rpg.return.m04.departure-confirmed";
        public const string RouteKey = "rpg.return.m04.departure-route";
        public const string AssignedCrewKey = "rpg.return.m04.departure-assigned-crew";
        public const string AvailableBerthsKey = "rpg.return.m04.departure-available-berths";
        public const string ReturneesToBookKey = "rpg.return.m04.departure-returnees-to-book";
        public const string OfficialStampKey = "rpg.return.m04.departure-official-stamp";
        public const string EscortFlagKey = "rpg.return.m04.departure-escort-flag";

        public static bool TryResetForNewVisit()
        {
            var variables = Maki.Initialized ? Maki.Game?.Variables : null;
            if (variables == null)
                return false;
            return TryResetForNewVisit((key, value) => variables.Set(key, value));
        }

        public static bool TryResetForNewVisit(Action<string, int> setInt)
        {
            if (setInt == null)
                return false;
            // A replay cannot inherit an earlier voyage's confirmed departure.
            setInt(ConfirmedKey, 0);
            setInt(RouteKey, 0);
            setInt(AssignedCrewKey, 0);
            setInt(AvailableBerthsKey, 0);
            setInt(ReturneesToBookKey, 0);
            setInt(OfficialStampKey, 0);
            setInt(EscortFlagKey, 0);
            return true;
        }

        public static bool TryRecord(M04DepartureManifest manifest)
        {
            ValidateConfirmed(manifest);
            var variables = Maki.Initialized ? Maki.Game?.Variables : null;
            if (variables == null)
                return false;
            return TryRecord(manifest, (key, value) => variables.Set(key, value));
        }

        public static bool TryRecord(M04DepartureManifest manifest, Action<string, int> setInt)
        {
            ValidateConfirmed(manifest);
            if (setInt == null)
                return false;
            // Invalidate any previous voyage before updating this record.
            // The confirmation marker must be committed last: a failed or
            // interrupted field write may never expose a mixed old/new trip.
            setInt(ConfirmedKey, 0);
            setInt(RouteKey, (int)manifest.Route + 1);
            setInt(AssignedCrewKey, manifest.AssignedCrew);
            setInt(AvailableBerthsKey, manifest.AvailableBerths);
            setInt(ReturneesToBookKey, manifest.ReturneesToBook);
            setInt(OfficialStampKey, manifest.OfficialRegistrationEffective ? 1 : 0);
            setInt(EscortFlagKey, manifest.EscortFlagAccepted ? 1 : 0);
            setInt(ConfirmedKey, 1);
            return true;
        }

        public static bool TryRead(out M04DepartureRecord? record)
        {
            record = null;
            var variables = Maki.Initialized ? Maki.Game?.Variables : null;
            if (variables == null)
                return false;
            return TryRead(key => variables.GetInt(key), out record);
        }

        public static bool TryRead(Func<string, int> getInt, out M04DepartureRecord? record)
        {
            record = null;
            if (getInt == null)
                return false;
            if (getInt(ConfirmedKey) != 1)
                return true;

            int rawRoute = getInt(RouteKey);
            if (rawRoute <= 0)
                return true;
            var route = (M03VoyageChoice)(rawRoute - 1);
            if (!Enum.IsDefined(typeof(M03VoyageChoice), route))
                return true;

            int crew = getInt(AssignedCrewKey);
            int berths = getInt(AvailableBerthsKey);
            int returnees = getInt(ReturneesToBookKey);
            bool officialStamp = getInt(OfficialStampKey) == 1;
            bool escortFlag = getInt(EscortFlagKey) == 1;

            // Reject corrupted or partial save state rather than fabricating a voyage.
            if (crew <= 0 || berths <= 0 || returnees <= 0 || returnees > berths)
                return true;
            if ((route == M03VoyageChoice.HanQiaoOfficial ||
                 route == M03VoyageChoice.MixedWithOfficial) && !officialStamp)
                return true;
            if (route == M03VoyageChoice.LuJunEscort && !escortFlag)
                return true;

            record = new M04DepartureRecord(
                route,
                crew,
                berths,
                returnees,
                officialStamp,
                escortFlag);
            return true;
        }

        private static void ValidateConfirmed(M04DepartureManifest manifest)
        {
            if (manifest == null)
                throw new ArgumentNullException(nameof(manifest));
            if (!manifest.DepartureConfirmed || manifest.Check().Length != 0)
                throw new InvalidOperationException(
                    "Unconfirmed or blocked M04 departure manifests cannot be recorded.");
        }
    }
}
