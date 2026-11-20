using System;
using GamingIsLove.Makinom;

namespace Rpg.Soundwell
{
    public enum M13ApproachRoute
    {
        RouteASupervisedCivilian = 1,
        RouteBBellTowerAssault = 2,
        RouteCOldCanalNightRun = 3
    }

    public enum M14AyuanOutcome
    {
        Safe = 1,
        SeverelyInjuredRetired = 2,
        Lost = 3
    }

    /// <summary>
    /// Immutable M13 departure commitment and M14 A Yuan outcome record.
    /// Enforces the hard narrative consent rules from SCENE_SCRIPT_M13.md and
    /// SCENE_SCRIPT_M14.md:
    /// - Route B/C cannot depart when preparation is incomplete without both
    ///   Qi Que's explicit warning and a separate risk-acceptance consent.
    /// - Route B requires the 3-bell reverse-signal agreement with the fleet.
    /// - Route A or complete safety preparation guarantees A Yuan is Safe.
    /// - Combat retry never changes A Yuan's settled narrative outcome.
    /// </summary>
    public readonly struct M13DepartureRecord
    {
        public M13DepartureRecord(
            M13ApproachRoute route,
            bool safetyPreparationComplete,
            bool qiQueWarningGiven,
            bool explicitHighRiskConsent,
            bool threeBellReverseSignalAgreed)
        {
            Route = route;
            SafetyPreparationComplete = safetyPreparationComplete;
            QiQueWarningGiven = qiQueWarningGiven;
            ExplicitHighRiskConsent = explicitHighRiskConsent;
            ThreeBellReverseSignalAgreed = threeBellReverseSignalAgreed;
        }

        public M13ApproachRoute Route { get; }
        public bool SafetyPreparationComplete { get; }
        public bool QiQueWarningGiven { get; }
        public bool ExplicitHighRiskConsent { get; }
        public bool ThreeBellReverseSignalAgreed { get; }

        public bool CanAyuanSufferHighRiskOutcome =>
            Route != M13ApproachRoute.RouteASupervisedCivilian &&
            !SafetyPreparationComplete &&
            QiQueWarningGiven &&
            ExplicitHighRiskConsent;
    }

    public static class M13DepartureRiskFacts
    {
        public const string DepartureConfirmedKey = "rpg.return.m13.departure-confirmed";
        public const string RouteKey = "rpg.return.m13.approach-route";
        public const string SafetyPrepKey = "rpg.return.m13.safety-prep-complete";
        public const string QiWarningKey = "rpg.return.m13.qi-warning-given";
        public const string RiskConsentKey = "rpg.return.m13.ayuan-risk-consent";
        public const string ThreeBellSignalKey = "rpg.return.m13.three-bell-reverse-signal";
        public const string AyuanOutcomeKey = "rpg.return.m14.crew-ayuan";

        public static bool IsValidDeparture(M13DepartureRecord record)
        {
            if (!Enum.IsDefined(typeof(M13ApproachRoute), record.Route))
                return false;

            // Consent cannot exist without Qi Que's explicit warning first.
            if (record.ExplicitHighRiskConsent && !record.QiQueWarningGiven)
                return false;

            if (record.Route == M13ApproachRoute.RouteBBellTowerAssault &&
                !record.ThreeBellReverseSignalAgreed)
                return false;

            if (record.Route != M13ApproachRoute.RouteASupervisedCivilian)
            {
                // Route B/C requires either complete safety prep or explicit high-risk consent.
                if (!record.SafetyPreparationComplete &&
                    !(record.QiQueWarningGiven && record.ExplicitHighRiskConsent))
                    return false;
            }

            return true;
        }

        public static bool CanRecordDeparture(M13DepartureRecord record, Func<string, int> getInt)
        {
            return getInt != null &&
                getInt(DepartureConfirmedKey) == 0 &&
                IsValidDeparture(record);
        }

        public static bool TryRecordDeparture(M13DepartureRecord record)
        {
            var variables = Maki.Initialized ? Maki.Game?.Variables : null;
            return variables != null && TryRecordDeparture(
                record,
                key => variables.GetInt(key),
                (key, value) => variables.Set(key, value));
        }

        public static bool TryRecordDeparture(
            M13DepartureRecord record,
            Func<string, int> getInt,
            Action<string, int> setInt)
        {
            if (getInt == null || setInt == null || !CanRecordDeparture(record, getInt))
                return false;

            setInt(DepartureConfirmedKey, 0);
            setInt(RouteKey, (int)record.Route);
            setInt(SafetyPrepKey, record.SafetyPreparationComplete ? 1 : 0);
            setInt(QiWarningKey, record.QiQueWarningGiven ? 1 : 0);
            setInt(RiskConsentKey, record.ExplicitHighRiskConsent ? 1 : 0);
            setInt(ThreeBellSignalKey, record.ThreeBellReverseSignalAgreed ? 1 : 0);
            setInt(DepartureConfirmedKey, 1);
            return true;
        }

        public static bool TryReadDeparture(out M13DepartureRecord? record)
        {
            record = null;
            var variables = Maki.Initialized ? Maki.Game?.Variables : null;
            return variables != null && TryReadDeparture(key => variables.GetInt(key), out record);
        }

        public static bool TryReadDeparture(Func<string, int> getInt, out M13DepartureRecord? record)
        {
            record = null;
            if (getInt == null)
                return false;
            if (getInt(DepartureConfirmedKey) != 1)
                return true;

            int rawRoute = getInt(RouteKey);
            int rawPrep = getInt(SafetyPrepKey);
            int rawWarn = getInt(QiWarningKey);
            int rawConsent = getInt(RiskConsentKey);
            int rawBell = getInt(ThreeBellSignalKey);

            if (!Enum.IsDefined(typeof(M13ApproachRoute), rawRoute) ||
                (rawPrep != 0 && rawPrep != 1) ||
                (rawWarn != 0 && rawWarn != 1) ||
                (rawConsent != 0 && rawConsent != 1) ||
                (rawBell != 0 && rawBell != 1))
                return true;

            var saved = new M13DepartureRecord(
                (M13ApproachRoute)rawRoute,
                rawPrep == 1,
                rawWarn == 1,
                rawConsent == 1,
                rawBell == 1);
            if (IsValidDeparture(saved))
                record = saved;
            return true;
        }

        public static bool TryRecordAyuanOutcome(
            M14AyuanOutcome outcome,
            Func<string, int> getInt,
            Action<string, int> setInt)
        {
            if (getInt == null || setInt == null ||
                !Enum.IsDefined(typeof(M14AyuanOutcome), outcome) ||
                getInt(AyuanOutcomeKey) != 0)
                return false;

            if (!TryReadDeparture(getInt, out M13DepartureRecord? departure) ||
                !departure.HasValue)
                return false;

            // Only explicit warning + incomplete prep + separate risk consent on B/C
            // can ever settle a non-Safe outcome for A Yuan.
            if (outcome != M14AyuanOutcome.Safe && !departure.Value.CanAyuanSufferHighRiskOutcome)
                return false;

            setInt(AyuanOutcomeKey, (int)outcome);
            return true;
        }

        public static bool TryReadAyuanOutcome(Func<string, int> getInt, out M14AyuanOutcome? outcome)
        {
            outcome = null;
            if (getInt == null)
                return false;
            int raw = getInt(AyuanOutcomeKey);
            if (raw == 0)
                return true;
            if (!Enum.IsDefined(typeof(M14AyuanOutcome), raw))
                return true;
            if (!TryReadDeparture(getInt, out M13DepartureRecord? departure) ||
                !departure.HasValue)
                return true;

            var parsed = (M14AyuanOutcome)raw;
            if (parsed != M14AyuanOutcome.Safe && !departure.Value.CanAyuanSufferHighRiskOutcome)
                return true;

            outcome = parsed;
            return true;
        }

        public static bool TryResetForNewVoyage(Action<string, int> setInt)
        {
            if (setInt == null)
                return false;
            setInt(DepartureConfirmedKey, 0);
            setInt(RouteKey, 0);
            setInt(SafetyPrepKey, 0);
            setInt(QiWarningKey, 0);
            setInt(RiskConsentKey, 0);
            setInt(ThreeBellSignalKey, 0);
            setInt(AyuanOutcomeKey, 0);
            return true;
        }
    }
}
