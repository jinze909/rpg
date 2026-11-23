using System;
using GamingIsLove.Makinom;

namespace Rpg.Soundwell
{
    public enum M11PassEntryMethod
    {
        LandGateNegotiation = 1,
        MerchantCaravanSurvey = 2,
        ReliefSupplyPass = 3
    }

    public enum M11ThirdSquadConscriptionStance
    {
        NotInPass = 0,
        RefusedForcedConscription = 1,
        ParticipatedInEnforcement = 2
    }

    public enum M11RescueProvider
    {
        ShenLiShallowBoat = 1,
        FallbackPaidArtisans = 2
    }

    public enum M11ShenDivisionPact
    {
        NoneShenAbsent = 0,
        MutualConsultAndDivision = 1,
        CallItEvenNextTime = 2
    }

    /// <summary>
    /// Immutable M11 ("这次轮到她来找你") outcome persisted in Makinom.
    /// Enforces SCENE_SCRIPT_M11.md v1.2 (including 7bcd1577, 5a8989b1,
    /// 54805883, 570899bf, and VALIDATION_PLAN N53):
    /// - Strictly gated on verified M10AssemblyFacts (Huilan Pass bell-tower turn).
    /// - Cross-checks live RC05ThirdSquadFacts so Hao Lie's conscription stance
    ///   is recorded if and only if RC-05 enlisted under Lu Jun.
    /// - Distinguishes Shen Li's 3-call fog shallow-boat extraction (tracking
    ///   whether Oulin actually participated vs shore boatman taking the helm)
    ///   from the fallback paid-artisan route when Shen Li is absent (never
    ///   summoning her out of thin air or faking her echo/reconciliation).
    /// </summary>
    public readonly struct M11ReverseRescueRecord
    {
        public M11ReverseRescueRecord(
            bool threeBellsObserved,
            M11PassEntryMethod entryMethod,
            bool conscriptionObserved,
            M11ThirdSquadConscriptionStance thirdSquadStance,
            M11RescueProvider rescueProvider,
            int echoCallsHeard,
            bool oulinJoinedExtraction,
            M11ShenDivisionPact divisionPact)
        {
            ThreeBellsObserved = threeBellsObserved;
            EntryMethod = entryMethod;
            ConscriptionObserved = conscriptionObserved;
            ThirdSquadStance = thirdSquadStance;
            RescueProvider = rescueProvider;
            EchoCallsHeard = echoCallsHeard;
            OulinJoinedExtraction = oulinJoinedExtraction;
            DivisionPact = divisionPact;
        }

        public bool ThreeBellsObserved { get; }
        public M11PassEntryMethod EntryMethod { get; }
        public bool ConscriptionObserved { get; }
        public M11ThirdSquadConscriptionStance ThirdSquadStance { get; }
        public M11RescueProvider RescueProvider { get; }
        public int EchoCallsHeard { get; }
        public bool OulinJoinedExtraction { get; }
        public M11ShenDivisionPact DivisionPact { get; }

        public bool ShoreBoatmanTookHelm =>
            RescueProvider == M11RescueProvider.ShenLiShallowBoat &&
            !OulinJoinedExtraction;

        public bool ImprovesShenRelation =>
            RescueProvider == M11RescueProvider.ShenLiShallowBoat &&
            DivisionPact == M11ShenDivisionPact.MutualConsultAndDivision;

        public bool UsedHigherCostFallback =>
            RescueProvider == M11RescueProvider.FallbackPaidArtisans;
    }

    public static class M11ReverseRescueFacts
    {
        public const string ConfirmedKey = "rpg.return.m11.rescue-confirmed";
        public const string ThreeBellsKey = "rpg.return.m11.three-bells-observed";
        public const string EntryMethodKey = "rpg.return.m11.entry-method";
        public const string ConscriptionObservedKey = "rpg.return.m11.conscription-observed";
        public const string ThirdSquadStanceKey = "rpg.return.m11.third-squad-stance";
        public const string RescueProviderKey = "rpg.return.m11.rescue-provider";
        public const string EchoCallsKey = "rpg.return.m11.echo-calls-heard";
        public const string OulinJoinedKey = "rpg.return.m11.oulin-joined-extraction";
        public const string DivisionPactKey = "rpg.return.m11.shen-division-pact";

        // A torn or corrupt RC-05 outcome is not the same as an unplayed case.
        // Otherwise M11 can incorrectly omit Hao Lie's squad from the pass.
        public static bool TryEvaluateRC05EnlistedUnderLuJun(
            Func<string, int> getInt, out bool enlisted)
        {
            enlisted = false;
            if (getInt == null ||
                !RC05ThirdSquadFacts.TryRead(getInt, out RC05ThirdSquadRecord? rc05))
                return false;

            if (!rc05.HasValue)
                return getInt(RC05ThirdSquadFacts.ConfirmedKey) == 0;

            enlisted = rc05.Value.EnlistedUnderLuJun;
            return true;
        }

        public static bool EvaluateRC05EnlistedUnderLuJun(Func<string, int> getInt)
        {
            return TryEvaluateRC05EnlistedUnderLuJun(getInt, out bool enlisted) &&
                enlisted;
        }

        public static bool IsValidRecord(M11ReverseRescueRecord record, Func<string, int> getInt)
        {
            if (getInt == null ||
                !record.ThreeBellsObserved ||
                !record.ConscriptionObserved ||
                !Enum.IsDefined(typeof(M11PassEntryMethod), record.EntryMethod) ||
                !Enum.IsDefined(typeof(M11ThirdSquadConscriptionStance), record.ThirdSquadStance) ||
                !Enum.IsDefined(typeof(M11RescueProvider), record.RescueProvider) ||
                !Enum.IsDefined(typeof(M11ShenDivisionPact), record.DivisionPact))
                return false;

            if (!M10AssemblyFacts.TryRead(getInt, out M10AssemblyRecord? m10) ||
                !m10.HasValue)
                return false;

            if (!TryEvaluateRC05EnlistedUnderLuJun(getInt, out bool rc05InPass))
                return false;

            if (rc05InPass)
            {
                if (record.ThirdSquadStance == M11ThirdSquadConscriptionStance.NotInPass)
                    return false;
            }
            else if (record.ThirdSquadStance != M11ThirdSquadConscriptionStance.NotInPass)
            {
                return false;
            }

            if (record.RescueProvider == M11RescueProvider.ShenLiShallowBoat)
            {
                if (record.EchoCallsHeard != 3 ||
                    record.DivisionPact == M11ShenDivisionPact.NoneShenAbsent)
                    return false;
            }
            else
            {
                if (record.EchoCallsHeard != 0 ||
                    record.OulinJoinedExtraction ||
                    record.DivisionPact != M11ShenDivisionPact.NoneShenAbsent)
                    return false;
            }

            return true;
        }

        public static bool HasAnyPersistedFacts(Func<string, int> getInt)
        {
            return getInt == null ||
                getInt(ConfirmedKey) != 0 ||
                getInt(ThreeBellsKey) != 0 ||
                getInt(EntryMethodKey) != 0 ||
                getInt(ConscriptionObservedKey) != 0 ||
                getInt(ThirdSquadStanceKey) != 0 ||
                getInt(RescueProviderKey) != 0 ||
                getInt(EchoCallsKey) != 0 ||
                getInt(OulinJoinedKey) != 0 ||
                getInt(DivisionPactKey) != 0;
        }

        public static bool HasOrphanedDownstreamFacts(Func<string, int> getInt)
        {
            return getInt == null ||
                M12BridgeTruthFacts.HasAnyPersistedFacts(getInt) ||
                M12BridgeTruthFacts.HasOrphanedDownstreamFacts(getInt);
        }

        private static bool CompatiblePending(Func<string, int> getInt, string key, int expected)
        {
            int raw = getInt(key);
            return raw == 0 || raw == expected;
        }

        public static bool CanRecord(M11ReverseRescueRecord record, Func<string, int> getInt)
        {
            return getInt != null &&
                getInt(ConfirmedKey) == 0 &&
                !HasOrphanedDownstreamFacts(getInt) &&
                IsValidRecord(record, getInt) &&
                CompatiblePending(getInt, ThreeBellsKey, record.ThreeBellsObserved ? 1 : 0) &&
                CompatiblePending(getInt, EntryMethodKey, (int)record.EntryMethod) &&
                CompatiblePending(getInt, ConscriptionObservedKey, record.ConscriptionObserved ? 1 : 0) &&
                CompatiblePending(getInt, ThirdSquadStanceKey, (int)record.ThirdSquadStance) &&
                CompatiblePending(getInt, RescueProviderKey, (int)record.RescueProvider) &&
                CompatiblePending(getInt, EchoCallsKey, record.EchoCallsHeard) &&
                CompatiblePending(getInt, OulinJoinedKey, record.OulinJoinedExtraction ? 1 : 0) &&
                CompatiblePending(getInt, DivisionPactKey, (int)record.DivisionPact);
        }

        public static bool TryRecord(M11ReverseRescueRecord record)
        {
            var variables = Maki.Initialized ? Maki.Game?.Variables : null;
            return variables != null && TryRecord(
                record,
                key => variables.GetInt(key),
                (key, value) => variables.Set(key, value));
        }

        public static bool TryRecord(
            M11ReverseRescueRecord record,
            Func<string, int> getInt,
            Action<string, int> setInt)
        {
            if (getInt == null || setInt == null || !CanRecord(record, getInt))
                return false;

            // Write primary rescue-provider and pact decisions first so an
            // interrupted save locks out competing outcomes while allowing retries.
            setInt(RescueProviderKey, (int)record.RescueProvider);
            setInt(DivisionPactKey, (int)record.DivisionPact);
            setInt(EntryMethodKey, (int)record.EntryMethod);
            setInt(ThirdSquadStanceKey, (int)record.ThirdSquadStance);
            setInt(ThreeBellsKey, record.ThreeBellsObserved ? 1 : 0);
            setInt(ConscriptionObservedKey, record.ConscriptionObserved ? 1 : 0);
            setInt(EchoCallsKey, record.EchoCallsHeard);
            setInt(OulinJoinedKey, record.OulinJoinedExtraction ? 1 : 0);
            setInt(ConfirmedKey, 1);
            return true;
        }

        public static bool TryRead(out M11ReverseRescueRecord? record)
        {
            record = null;
            var variables = Maki.Initialized ? Maki.Game?.Variables : null;
            return variables != null && TryRead(key => variables.GetInt(key), out record);
        }

        public static bool TryRead(Func<string, int> getInt, out M11ReverseRescueRecord? record)
        {
            record = null;
            if (getInt == null)
                return false;
            if (getInt(ConfirmedKey) != 1)
                return true;

            int rawBells = getInt(ThreeBellsKey);
            int rawEntry = getInt(EntryMethodKey);
            int rawConscription = getInt(ConscriptionObservedKey);
            int rawSquad = getInt(ThirdSquadStanceKey);
            int rawProvider = getInt(RescueProviderKey);
            int rawEcho = getInt(EchoCallsKey);
            int rawOulin = getInt(OulinJoinedKey);
            int rawPact = getInt(DivisionPactKey);

            if ((rawBells != 0 && rawBells != 1) ||
                !Enum.IsDefined(typeof(M11PassEntryMethod), rawEntry) ||
                (rawConscription != 0 && rawConscription != 1) ||
                !Enum.IsDefined(typeof(M11ThirdSquadConscriptionStance), rawSquad) ||
                !Enum.IsDefined(typeof(M11RescueProvider), rawProvider) ||
                (rawOulin != 0 && rawOulin != 1) ||
                !Enum.IsDefined(typeof(M11ShenDivisionPact), rawPact))
                return true;

            var saved = new M11ReverseRescueRecord(
                rawBells == 1,
                (M11PassEntryMethod)rawEntry,
                rawConscription == 1,
                (M11ThirdSquadConscriptionStance)rawSquad,
                (M11RescueProvider)rawProvider,
                rawEcho,
                rawOulin == 1,
                (M11ShenDivisionPact)rawPact);

            if (IsValidRecord(saved, getInt))
                record = saved;
            return true;
        }

        public static bool TryResetForNewVoyage()
        {
            var variables = Maki.Initialized ? Maki.Game?.Variables : null;
            return variables != null && TryResetForNewVoyage(
                (key, value) => variables.Set(key, value));
        }

        public static bool TryResetForNewVoyage(Action<string, int> setInt)
        {
            if (setInt == null)
                return false;
            setInt(ConfirmedKey, 0);
            setInt(ThreeBellsKey, 0);
            setInt(EntryMethodKey, 0);
            setInt(ConscriptionObservedKey, 0);
            setInt(ThirdSquadStanceKey, 0);
            setInt(RescueProviderKey, 0);
            setInt(EchoCallsKey, 0);
            setInt(OulinJoinedKey, 0);
            setInt(DivisionPactKey, 0);
            return true;
        }
    }
}
