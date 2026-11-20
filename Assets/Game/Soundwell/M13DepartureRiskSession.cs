using System;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Authoritative M13 departure readiness & A Yuan risk-consent controller.
    /// Blocks Route B/C departure when safety preparation is incomplete and no
    /// separate high-risk consent has been given after Qi Que's warning, and
    /// ensures combat retries never alter A Yuan's settled M14 fate.
    /// </summary>
    public sealed class M13DepartureRiskSession
    {
        public bool SafetyPreparationComplete { get; private set; }
        public bool QiQueWarningGiven { get; private set; }
        public bool ExplicitHighRiskConsent { get; private set; }
        public bool ThreeBellReverseSignalAgreed { get; private set; }
        public M13ApproachRoute? ConfirmedRoute { get; private set; }
        public bool IsDeparted => ConfirmedRoute.HasValue;

        public M13DepartureRiskSession(bool safetyPreparationComplete = false)
        {
            SafetyPreparationComplete = safetyPreparationComplete;
        }

        public void CompleteSafetyPreparation()
        {
            if (IsDeparted) return;
            SafetyPreparationComplete = true;
            // Once safety prep is complete, high-risk consent is no longer active.
            ExplicitHighRiskConsent = false;
        }

        public void AgreeThreeBellReverseSignal()
        {
            if (IsDeparted) return;
            ThreeBellReverseSignalAgreed = true;
        }

        public void DeliverQiQueWarning()
        {
            if (IsDeparted) return;
            QiQueWarningGiven = true;
        }

        public bool TryAcceptHighRiskConsent()
        {
            if (IsDeparted || SafetyPreparationComplete || !QiQueWarningGiven)
                return false;
            ExplicitHighRiskConsent = true;
            return true;
        }

        public bool CanConfirmDeparture(M13ApproachRoute route)
        {
            if (IsDeparted)
                return false;
            return M13DepartureRiskFacts.IsValidDeparture(BuildRecord(route));
        }

        public bool TryConfirmDeparture(
            M13ApproachRoute route,
            Func<string, int> getInt,
            Action<string, int> setInt)
        {
            if (!CanConfirmDeparture(route))
                return false;

            var record = BuildRecord(route);
            if (!M13DepartureRiskFacts.TryRecordDeparture(record, getInt, setInt))
                return false;

            ConfirmedRoute = route;
            return true;
        }

        private M13DepartureRecord BuildRecord(M13ApproachRoute route)
        {
            return new M13DepartureRecord(
                route,
                SafetyPreparationComplete,
                QiQueWarningGiven,
                ExplicitHighRiskConsent,
                ThreeBellReverseSignalAgreed);
        }
    }
}
