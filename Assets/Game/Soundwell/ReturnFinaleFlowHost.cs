using System;
using UnityEngine;

namespace Rpg.Soundwell
{
    public enum ReturnFinaleStage
    {
        None = 0,
        M10Assembly = 10,
        M11ReverseRescue = 11,
        M12BridgeTruth = 12,
        M13DepartureRisk = 13,
        M14FinalPassage = 14,
        M15FinalRollCall = 15,
        M16Epilogue = 16,
        Completed = 17
    }

    /// <summary>
    /// Configuration delegates for ReturnFinaleFlowHost supplying live scene
    /// readiness and narrative context across M10 through M16.
    /// An unwired scene must never fabricate presence, proofs, civilian counts,
    /// or consented roll-call names.
    /// </summary>
    public sealed class ReturnFinaleFlowContext
    {
        public Func<bool> AtLinchaoFerry { get; set; } = () => false;
        public Func<bool> AtJingwanAssembly { get; set; } = () => false;
        public Func<bool> ThreeRecordsMatched { get; set; } = () => false;
        public Func<bool> CarriesM02BattleReport { get; set; } = () => false;

        public Func<bool> AtHuilanPass { get; set; } = () => false;
        public Func<bool> ShenLiAccompanying { get; set; } = () => false;
        public Func<bool> OulinParticipating { get; set; } = () => false;

        public Func<bool> AtBellTowerCamp { get; set; } = () => false;
        public Func<bool> M03PriorConfession { get; set; } = () => false;
        public Func<bool> M03SecrecyPromised { get; set; } = () => false;
        public Func<bool> LinSuPresent { get; set; } = () => false;

        public Func<bool> FleetBriefingReady { get; set; } = () => false;
        public Func<bool> SafetyPrepComplete { get; set; } = () => false;
        public Func<bool> CanDepartNow { get; set; } = () => false;

        public Func<bool> AtStraitAction { get; set; } = () => false;
        public Func<bool> ValidSupervisionCredentials { get; set; } = () => false;
        public Func<bool> ValidPortGuarantee { get; set; } = () => false;
        public Func<bool> ThreeOpenDoorBellsRung { get; set; } = () => false;
        public Func<bool> FogRelayCompleted { get; set; } = () => false;
        public Func<bool> FogMasteryBonusAvailable { get; set; } = () => false;
        public Func<M14CivilianCounts> ReadCivilianCounts { get; set; } =
            () => default;

        public Func<bool> CeremonyReady { get; set; } = () => false;
        public Func<int> ConsentedVerifiedNamesCount { get; set; } = () => 0;
        public Func<string> XiaoqiCustomName { get; set; } = () => null;

        public Func<bool> AtLinchaoAfternoon { get; set; } = () => false;
        public Func<bool> ShenLiWillingToMeet { get; set; } = () => false;
        public Func<M16RespectAgencyTone> ResolveClosingTone { get; set; } =
            () => M16RespectAgencyTone.RespectedOthersChoices;
    }

    /// <summary>
    /// Scene-callable Act-3 / Finale flow coordinator sequencing M10 through M16
    /// strictly from verified saved Makinom facts. Enforces verified M09BerthTableFacts
    /// before entering M10, resumes at the exact first uncompleted chapter after save/load,
    /// derives M15's bridge-truth-sealed flag directly from M12BridgeTruthFacts, and
    /// disposes each sub-host cleanly between stages.
    /// </summary>
    public sealed class ReturnFinaleFlowHost : IDisposable
    {
        private readonly Transform uiParent;
        private readonly ReturnFinaleFlowContext context;
        private readonly Func<string, int> getInt;
        private readonly Action<string, int> setInt;

        private IDisposable activeHost;
        private ReturnFinaleStage currentStage = ReturnFinaleStage.None;
        private bool disposed;
        private bool openingStage;
        private bool openingCancelled;

        public ReturnFinaleStage CurrentStage => currentStage;
        public bool IsDisposed => disposed;
        public bool HasActiveHost => IsActiveHostOpen();

        public M10AssemblyHost ActiveM10Host => activeHost as M10AssemblyHost;
        public M11ReverseRescueHost ActiveM11Host => activeHost as M11ReverseRescueHost;
        public M12BridgeTruthHost ActiveM12Host => activeHost as M12BridgeTruthHost;
        public M13DepartureRiskHost ActiveM13Host => activeHost as M13DepartureRiskHost;
        public M14FinalPassageHost ActiveM14Host => activeHost as M14FinalPassageHost;
        public M15FinalRollCallHost ActiveM15Host => activeHost as M15FinalRollCallHost;
        public M16EpilogueHost ActiveM16Host => activeHost as M16EpilogueHost;

        public ReturnFinaleFlowHost(
            Transform uiParent,
            ReturnFinaleFlowContext context,
            Func<string, int> getInt,
            Action<string, int> setInt)
        {
            this.uiParent = uiParent ?? throw new ArgumentNullException(nameof(uiParent));
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            this.getInt = getInt ?? throw new ArgumentNullException(nameof(getInt));
            this.setInt = setInt ?? throw new ArgumentNullException(nameof(setInt));
        }

        public ReturnFinaleStage EvaluateNextStage()
        {
            if (!M09BerthTableFacts.TryRead(getInt, out M09BerthTableRecord? m09) || !m09.HasValue)
                return ReturnFinaleStage.None;
            if (!M10AssemblyFacts.TryRead(getInt, out M10AssemblyRecord? m10) || !m10.HasValue)
                return M10AssemblyFacts.HasAnyPersistedFacts(getInt) ||
                    M10AssemblyFacts.HasOrphanedDownstreamFacts(getInt)
                    ? ReturnFinaleStage.None
                    : ReturnFinaleStage.M10Assembly;
            if (!M11ReverseRescueFacts.TryRead(getInt, out M11ReverseRescueRecord? m11) || !m11.HasValue)
                return M11ReverseRescueFacts.HasAnyPersistedFacts(getInt) ||
                    M11ReverseRescueFacts.HasOrphanedDownstreamFacts(getInt)
                    ? ReturnFinaleStage.None
                    : ReturnFinaleStage.M11ReverseRescue;
            if (!M12BridgeTruthFacts.TryRead(getInt, out M12BridgeTruthRecord? m12) || !m12.HasValue)
                return M12BridgeTruthFacts.HasAnyPersistedFacts(getInt) ||
                    M12BridgeTruthFacts.HasOrphanedDownstreamFacts(getInt)
                    ? ReturnFinaleStage.None
                    : ReturnFinaleStage.M12BridgeTruth;
            if (!M13DepartureRiskFacts.TryReadDeparture(getInt, out M13DepartureRecord? m13) || !m13.HasValue)
                return M13DepartureRiskFacts.HasAnyPersistedFacts(getInt) ||
                    M13DepartureRiskFacts.HasOrphanedDownstreamFacts(getInt)
                    ? ReturnFinaleStage.None
                    : ReturnFinaleStage.M13DepartureRisk;
            if (!M14FinalPassageFacts.TryRead(getInt, out M14FinalPassageRecord? m14) || !m14.HasValue)
                return M14FinalPassageFacts.HasAnyPersistedFacts(getInt) ||
                    M14FinalPassageFacts.HasOrphanedDownstreamFacts(getInt)
                    ? ReturnFinaleStage.None
                    : ReturnFinaleStage.M14FinalPassage;
            if (!M15FinalRollCallFacts.TryRead(getInt, out M15FinalRollCallRecord? m15) || !m15.HasValue)
                return M15FinalRollCallFacts.HasAnyPersistedFacts(getInt) ||
                    M15FinalRollCallFacts.HasOrphanedDownstreamFacts(getInt)
                    ? ReturnFinaleStage.None
                    : ReturnFinaleStage.M15FinalRollCall;
            if (!M16EpilogueFacts.TryRead(getInt, out M16EpilogueRecord? m16) || !m16.HasValue)
                return M16EpilogueFacts.HasAnyPersistedFacts(getInt)
                    ? ReturnFinaleStage.None
                    : ReturnFinaleStage.M16Epilogue;
            return ReturnFinaleStage.Completed;
        }

        private bool IsActiveHostOpen()
        {
            if (activeHost == null)
                return false;
            if (activeHost is M10AssemblyHost m10)
                return !m10.IsDisposed &&
                    (m10.ActiveView != null || m10.ActiveWorldNoticeView != null);
            if (activeHost is M11ReverseRescueHost m11)
                return !m11.IsDisposed && m11.ActiveView != null;
            if (activeHost is M12BridgeTruthHost m12)
                return !m12.IsDisposed && m12.ActiveView != null;
            if (activeHost is M13DepartureRiskHost m13)
                return !m13.IsDisposed && m13.ActiveView != null;
            if (activeHost is M14FinalPassageHost m14)
                return !m14.IsDisposed && m14.ActiveView != null;
            if (activeHost is M15FinalRollCallHost m15)
                return !m15.IsDisposed && m15.ActiveView != null;
            if (activeHost is M16EpilogueHost m16)
                return !m16.IsDisposed && m16.ActiveView != null;
            return false;
        }

        // Validate scene callbacks before closing a prior host so missing
        // wiring fails closed without throwing or orphaning active UI.
        private bool HasRequiredDelegates(ReturnFinaleStage stage)
        {
            switch (stage)
            {
                case ReturnFinaleStage.M10Assembly:
                    return context.AtLinchaoFerry != null &&
                        context.AtJingwanAssembly != null &&
                        context.ThreeRecordsMatched != null &&
                        context.CarriesM02BattleReport != null;
                case ReturnFinaleStage.M11ReverseRescue:
                    return context.AtHuilanPass != null &&
                        context.ShenLiAccompanying != null &&
                        context.OulinParticipating != null;
                case ReturnFinaleStage.M12BridgeTruth:
                    return context.AtBellTowerCamp != null &&
                        context.M03PriorConfession != null &&
                        context.M03SecrecyPromised != null &&
                        context.ShenLiAccompanying != null &&
                        context.LinSuPresent != null;
                case ReturnFinaleStage.M13DepartureRisk:
                    return context.FleetBriefingReady != null &&
                        context.SafetyPrepComplete != null &&
                        context.CanDepartNow != null;
                case ReturnFinaleStage.M14FinalPassage:
                    return context.AtStraitAction != null &&
                        context.ValidSupervisionCredentials != null &&
                        context.ValidPortGuarantee != null &&
                        context.ThreeOpenDoorBellsRung != null &&
                        context.FogRelayCompleted != null &&
                        context.FogMasteryBonusAvailable != null &&
                        context.ReadCivilianCounts != null;
                case ReturnFinaleStage.M15FinalRollCall:
                    return context.CeremonyReady != null &&
                        context.ConsentedVerifiedNamesCount != null &&
                        context.XiaoqiCustomName != null;
                case ReturnFinaleStage.M16Epilogue:
                    return context.AtLinchaoAfternoon != null &&
                        context.ShenLiWillingToMeet != null &&
                        context.ResolveClosingTone != null;
                default:
                    return false;
            }
        }

        public bool TryOpenNextStage()
        {
            if (disposed || uiParent == null || openingStage)
                return false;

            openingStage = true;
            openingCancelled = false;
            try
            {
                return TryOpenNextStageCore();
            }
            finally
            {
                openingStage = false;
                openingCancelled = false;
            }
        }

        private bool CanContinueOpen() =>
            !disposed && uiParent != null && !openingCancelled;

        private bool TryOpenNextStageCore()
        {
            ReturnFinaleStage next = EvaluateNextStage();
            if (!CanContinueOpen())
                return false;

            if (next == ReturnFinaleStage.None)
            {
                CloseActiveHostCore();
                if (CanContinueOpen())
                    currentStage = ReturnFinaleStage.None;
                return false;
            }
            if (next == ReturnFinaleStage.Completed)
            {
                CloseActiveHostCore();
                if (CanContinueOpen())
                    currentStage = ReturnFinaleStage.Completed;
                return false;
            }

            if (IsActiveHostOpen() && currentStage == next)
                return false;
            if (!HasRequiredDelegates(next))
                return false;

            CloseActiveHostCore();
            if (!CanContinueOpen())
                return false;

            switch (next)
            {
                case ReturnFinaleStage.M10Assembly:
                {
                    var host = new M10AssemblyHost(
                        uiParent,
                        context.AtJingwanAssembly,
                        context.ThreeRecordsMatched,
                        context.CarriesM02BattleReport,
                        getInt, setInt,
                        context.AtLinchaoFerry);
                    return ActivateStage(next, host, host.TryOpen());
                }
                case ReturnFinaleStage.M11ReverseRescue:
                {
                    var host = new M11ReverseRescueHost(
                        uiParent,
                        context.AtHuilanPass,
                        context.ShenLiAccompanying,
                        context.OulinParticipating,
                        getInt, setInt);
                    return ActivateStage(next, host, host.TryOpen());
                }
                case ReturnFinaleStage.M12BridgeTruth:
                {
                    var host = new M12BridgeTruthHost(
                        uiParent,
                        context.AtBellTowerCamp,
                        context.M03PriorConfession,
                        context.M03SecrecyPromised,
                        context.ShenLiAccompanying,
                        context.LinSuPresent,
                        getInt, setInt);
                    return ActivateStage(next, host, host.TryOpen());
                }
                case ReturnFinaleStage.M13DepartureRisk:
                {
                    var host = new M13DepartureRiskHost(
                        uiParent,
                        context.FleetBriefingReady,
                        context.SafetyPrepComplete,
                        context.CanDepartNow,
                        getInt, setInt);
                    return ActivateStage(next, host, host.TryOpen());
                }
                case ReturnFinaleStage.M14FinalPassage:
                {
                    var host = new M14FinalPassageHost(
                        uiParent,
                        context.AtStraitAction,
                        context.ValidSupervisionCredentials,
                        context.ValidPortGuarantee,
                        context.ThreeOpenDoorBellsRung,
                        context.FogRelayCompleted,
                        context.FogMasteryBonusAvailable,
                        context.ReadCivilianCounts,
                        getInt, setInt);
                    return ActivateStage(next, host, host.TryOpen());
                }
                case ReturnFinaleStage.M15FinalRollCall:
                {
                    Func<bool> bridgeTruthSealed = () =>
                        M12BridgeTruthFacts.TryRead(getInt, out M12BridgeTruthRecord? m12) &&
                        m12.HasValue &&
                        m12.Value.Disclosure == M12TruthDisclosure.SealTruth;
                    var host = new M15FinalRollCallHost(
                        uiParent,
                        context.CeremonyReady,
                        bridgeTruthSealed,
                        context.ConsentedVerifiedNamesCount,
                        context.XiaoqiCustomName,
                        getInt, setInt);
                    return ActivateStage(next, host, host.TryOpen());
                }
                case ReturnFinaleStage.M16Epilogue:
                {
                    var host = new M16EpilogueHost(
                        uiParent,
                        context.AtLinchaoAfternoon,
                        context.ShenLiWillingToMeet,
                        context.ResolveClosingTone,
                        getInt, setInt);
                    return ActivateStage(next, host, host.TryOpen());
                }
                default:
                    return false;
            }
        }

        private bool ActivateStage(ReturnFinaleStage stage, IDisposable host, bool opened)
        {
            // A live scene callback may close or dispose this flow before
            // the next chapter host is ready to be published.
            if (!opened || !CanContinueOpen())
            {
                host.Dispose();
                return false;
            }
            activeHost = host;
            currentStage = stage;
            return true;
        }

        public void CloseActiveHost()
        {
            if (openingStage)
                openingCancelled = true;
            CloseActiveHostCore();
        }

        private void CloseActiveHostCore()
        {
            if (activeHost == null)
                return;
            var current = activeHost;
            activeHost = null;
            current.Dispose();
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            CloseActiveHost();
        }
    }
}
