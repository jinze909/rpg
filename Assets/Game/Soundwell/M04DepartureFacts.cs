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
        // Confirming resources is not the same as actually sailing.
        // An old save without this newer key correctly means "not sailed".
        public const string SailedKey = "rpg.return.m04.vessel-sailed";
        // Monotonic cast-off identity belongs to the same Makinom save.
        // Legacy saved voyages without this key read as zero; the sequence
        // is never reset when M03 begins another trip.
        public const string SailingSequenceKey = "rpg.return.m04.sailing-sequence";

        /// <summary>
        /// A new cast-off must not launch into a prior completed, corrupt or
        /// interrupted fog outcome. The already-open fog panel owns retries of
        /// its unfinished write; this gate is only for *new* departures.
        /// </summary>
        public static bool CanBeginFreshFogLeg(Func<string, int> getInt)
        {
            return getInt != null &&
                getInt(FogListeningFacts.CompletedKey) == 0 &&
                getInt(FogListeningFacts.PendingSignatureKey) == 0 &&
                getInt(FogListeningFacts.SucceededKey) == 0 &&
                getInt(FogListeningFacts.HullIntegrityKey) == 0 &&
                getInt(FogListeningFacts.AvoidedHazardsKey) == 0 &&
                getInt(FogListeningFacts.MatchedSignalsKey) == 0 &&
                getInt(FogListeningFacts.RemainingFocusKey) == 0;
        }

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
            setInt(SailedKey, 0);
            // Intentionally retain SailingSequenceKey across replays.
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
            setInt(SailedKey, 0);
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
            int rawOfficialStamp = getInt(OfficialStampKey);
            int rawEscortFlag = getInt(EscortFlagKey);
            // Both persisted flags must be canonical booleans. Treating an
            // unexpected integer as false would silently trust a corrupted
            // merchant-route departure despite malformed saved state.
            if ((rawOfficialStamp != 0 && rawOfficialStamp != 1) ||
                (rawEscortFlag != 0 && rawEscortFlag != 1))
                return true;
            bool officialStamp = rawOfficialStamp == 1;
            bool escortFlag = rawEscortFlag == 1;

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

        /// <summary>
        /// Record the separate, actual cast-off event. The voyage host calls
        /// this when sailing begins, never merely when readiness is approved.
        /// A corrupt, missing, or unconfirmed manifest cannot become a voyage,
        /// and a repeated cast-off cannot be counted twice.
        /// </summary>
        public static bool TryMarkSailed()
        {
            var variables = Maki.Initialized ? Maki.Game?.Variables : null;
            if (variables == null)
                return false;
            return TryMarkSailed(key => variables.GetInt(key),
                (key, value) => variables.Set(key, value));
        }

        public static bool TryMarkSailed(Func<string, int> getInt, Action<string, int> setInt)
        {
            if (getInt == null || setInt == null)
                return false;
            if (!TryRead(getInt, out M04DepartureRecord? readiness) ||
                !readiness.HasValue || getInt(SailedKey) != 0)
                return false;

            // A newer M03 itinerary must not sail an older confirmed hull.
            // Retain compatibility with saves that predate the M03 route key,
            // but fail closed for a present, malformed, or mismatched choice.
            if (getInt(M03LuJunFacts.VoyageKey) != 0 &&
                (!M03LuJunFacts.TryRead(getInt, out _, out _,
                    out M03VoyageChoice? currentRoute) ||
                 !currentRoute.HasValue ||
                 currentRoute.Value != readiness.Value.Route))
                return false;

            // Write the new identity before the public sailed marker. A
            // torn write cannot publish a sailed trip with the old identity.
            // A retry may skip an unused sequence number, which is safe.
            int previousSequence = getInt(SailingSequenceKey);
            if (previousSequence < 0 || previousSequence == int.MaxValue)
                return false;
            setInt(SailingSequenceKey, previousSequence + 1);
            setInt(SailedKey, 1);
            return true;
        }

        public static bool TryReadSailed(out bool sailed)
        {
            sailed = false;
            var variables = Maki.Initialized ? Maki.Game?.Variables : null;
            if (variables == null)
                return false;
            return TryReadSailed(key => variables.GetInt(key), out sailed);
        }

        public static bool TryReadSailed(Func<string, int> getInt, out bool sailed)
        {
            sailed = false;
            if (getInt == null)
                return false;
            if (!TryRead(getInt, out M04DepartureRecord? readiness))
                return false;
            // A stale "sailed" bit cannot turn a missing or corrupt
            // readiness record into a real outbound voyage.
            sailed = readiness.HasValue && getInt(SailedKey) == 1;
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
