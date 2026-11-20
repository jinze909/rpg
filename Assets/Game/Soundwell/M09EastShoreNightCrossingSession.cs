using System;

namespace Rpg.Soundwell
{
    public enum M09NightCrossingStage
    {
        FogApproach,
        PatrolPassing,
        PatrolAlerted,
        ClearApproach,
        JettyRollCall,
        AwaitingActualBoarding,
        Completed
    }

    /// <summary>
    /// M09 east-shore night ferry: listen for patrol oars, remain silent,
    /// approach the jetty, obtain Ruiya's own consent, and only count a rescue
    /// after the caller has verified actual boarding. This local scene is not
    /// a second save authority: a host must commit the resulting M09 outcome
    /// through the established Makinom berth-table facts.
    /// </summary>
    public sealed class M09EastShoreNightCrossingSession
    {
        public M09NightCrossingStage Stage { get; private set; } =
            M09NightCrossingStage.FogApproach;
        public int AvailableBerths { get; }
        public int PatrolAlerts { get; private set; }
        public int ConfirmedHouseholdSize { get; private set; }
        public int ActuallyBoardedCount { get; private set; }
        public bool RuiyaConsented { get; private set; }
        public M09RuiyaOutcome? Outcome { get; private set; }
        public bool IsComplete => Stage == M09NightCrossingStage.Completed;

        public M09EastShoreNightCrossingSession(int availableBerths)
        {
            if (availableBerths < 0)
                throw new ArgumentOutOfRangeException(nameof(availableBerths));
            AvailableBerths = availableBerths;
        }

        public bool TryListenForOars()
        {
            if (Stage != M09NightCrossingStage.FogApproach)
                return false;
            Stage = M09NightCrossingStage.PatrolPassing;
            return true;
        }

        public bool TryHoldSilent()
        {
            if (Stage != M09NightCrossingStage.PatrolPassing)
                return false;
            Stage = M09NightCrossingStage.ClearApproach;
            return true;
        }

        public bool TryRowToJetty()
        {
            if (Stage == M09NightCrossingStage.FogApproach ||
                Stage == M09NightCrossingStage.PatrolPassing)
            {
                // Rushing into fog before the patrol has passed exposes
                // the boat. Hide and listen again; never grant a free landing.
                PatrolAlerts++;
                Stage = M09NightCrossingStage.PatrolAlerted;
                return false;
            }
            if (Stage != M09NightCrossingStage.ClearApproach)
                return false;
            Stage = M09NightCrossingStage.JettyRollCall;
            return true;
        }

        public bool TryHideAndRelisten()
        {
            if (Stage != M09NightCrossingStage.PatrolAlerted)
                return false;
            Stage = M09NightCrossingStage.FogApproach;
            return true;
        }

        public bool TryRecordRuiyaConsent(bool willing, int observedHouseholdSize)
        {
            if (Stage != M09NightCrossingStage.JettyRollCall ||
                observedHouseholdSize <= 0)
                return false;

            ConfirmedHouseholdSize = observedHouseholdSize;
            RuiyaConsented = willing;
            if (!willing)
            {
                Complete(M09RuiyaOutcome.NotRescued);
                return true;
            }
            if (observedHouseholdSize > AvailableBerths)
            {
                Complete(M09RuiyaOutcome.Deferred);
                return true;
            }

            Stage = M09NightCrossingStage.AwaitingActualBoarding;
            return true;
        }

        public bool TryConfirmActualBoarding(int actuallyBoarded)
        {
            if (Stage != M09NightCrossingStage.AwaitingActualBoarding ||
                actuallyBoarded <= 0 ||
                actuallyBoarded != ConfirmedHouseholdSize ||
                actuallyBoarded > AvailableBerths)
                return false;
            ActuallyBoardedCount = actuallyBoarded;
            Complete(M09RuiyaOutcome.Rescued);
            return true;
        }

        public bool TryDeferForUnmetTransfer()
        {
            if (Stage != M09NightCrossingStage.JettyRollCall &&
                Stage != M09NightCrossingStage.AwaitingActualBoarding)
                return false;
            Complete(M09RuiyaOutcome.Deferred);
            return true;
        }

        private void Complete(M09RuiyaOutcome result)
        {
            Outcome = result;
            Stage = M09NightCrossingStage.Completed;
        }
    }
}
